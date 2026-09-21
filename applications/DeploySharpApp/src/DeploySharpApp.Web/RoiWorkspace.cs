using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeploySharpApp.Web;

public enum RoiWorkspaceInclusionMode { Include, Exclude }
public enum RoiWorkspaceExecutionMode { FilterResults, CropAndInfer, SlidingWindow }
public enum RoiWorkspaceHitTestMode { CenterPoint, AnyIntersection, IntersectionOverResult, IntersectionOverRoi, IoU }

public readonly record struct RoiWorkspacePoint(double X, double Y);

public sealed record RoiWorkspaceRegion(
    string Id,
    string Name,
    bool Enabled,
    int Priority,
    RoiWorkspaceInclusionMode InclusionMode,
    RoiWorkspaceExecutionMode ExecutionMode,
    RoiWorkspaceHitTestMode HitTestMode,
    double HitThreshold,
    double? ConfidenceOverride,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<RoiWorkspacePoint>? Points = null)
{
    public bool IsPolygon => Points is { Count: >= 3 };

    public static RoiWorkspaceRegion Rectangle(string name, double x, double y, double width, double height, RoiWorkspaceInclusionMode inclusionMode = RoiWorkspaceInclusionMode.Include)
        => new(Guid.NewGuid().ToString("N"), name, true, 0, inclusionMode, RoiWorkspaceExecutionMode.FilterResults, RoiWorkspaceHitTestMode.CenterPoint, .5, null, x, y, width, height);
}

/// <summary>Web projection of the main-library ROI schema used while native ROI execution remains isolated in the Worker package.</summary>
public static class RoiWorkspace
{
    private const int MaximumRegions = 256;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static string Serialize(IEnumerable<RoiWorkspaceRegion> regions, int sourceWidth = 1, int sourceHeight = 1)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (sourceWidth <= 0 || sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        RoiWorkspaceRegion[] values = regions.Take(MaximumRegions + 1).ToArray();
        if (values.Length > MaximumRegions) throw new InvalidDataException($"ROI count exceeds the {MaximumRegions} item limit.");
        Validate(values);
        object[] rois = values.OrderByDescending(region => region.Priority).Select(region => new
        {
            region.Id,
            region.Name,
            region.Enabled,
            region.Priority,
            coordinateSpace = "Normalized",
            inclusionMode = region.InclusionMode.ToString(),
            executionMode = region.ExecutionMode.ToString(),
            hitTestMode = region.HitTestMode.ToString(),
            region.HitThreshold,
            region.ConfidenceOverride,
            taskFilter = new[] { "ocr" },
            classFilter = Array.Empty<string>(),
            tags = new[] { "deploysharp-app", "ocr-workspace" },
            metadata = new Dictionary<string, string> { ["owner"] = "DeploySharpApp.Web" },
            geometry = region.IsPolygon
                ? (object)new { type = "Polygon", points = region.Points!.Select(point => new { x = point.X, y = point.Y }) }
                : new { type = "Rectangle", x = region.X, y = region.Y, width = region.Width, height = region.Height }
        }).ToArray();
        return JsonSerializer.Serialize(new { schemaVersion = "1.0", source = new { width = sourceWidth, height = sourceHeight }, version = 1, rois }, JsonOptions);
    }

    public static IReadOnlyList<RoiWorkspaceRegion> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("ROI JSON is empty.");
        if (json.Length > 1024 * 1024) throw new InvalidDataException("ROI JSON exceeds the 1 MiB limit.");
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        if (Text(root, "schemaVersion") != "1.0") throw new InvalidDataException("Only ROI schemaVersion 1.0 is supported.");
        if (!root.TryGetProperty("rois", out JsonElement elements) || elements.ValueKind != JsonValueKind.Array) throw new InvalidDataException("ROI JSON must contain a rois array.");
        if (elements.GetArrayLength() > MaximumRegions) throw new InvalidDataException($"ROI count exceeds the {MaximumRegions} item limit.");
        var result = new List<RoiWorkspaceRegion>();
        foreach (JsonElement element in elements.EnumerateArray())
        {
            if (!element.TryGetProperty("geometry", out JsonElement geometry) || geometry.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each ROI must contain geometry.");
            string geometryType = Text(geometry, "type") ?? throw new InvalidDataException("ROI geometry type is required.");
            IReadOnlyList<RoiWorkspacePoint>? points = null;
            double x = 0, y = 0, width = 0, height = 0;
            if (geometryType.Equals("Rectangle", StringComparison.OrdinalIgnoreCase))
            {
                x = Number(geometry, "x"); y = Number(geometry, "y"); width = Number(geometry, "width"); height = Number(geometry, "height");
            }
            else if (geometryType.Equals("Polygon", StringComparison.OrdinalIgnoreCase))
            {
                if (!geometry.TryGetProperty("points", out JsonElement vertices) || vertices.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Polygon ROI points are required.");
                points = vertices.EnumerateArray().Select(point => new RoiWorkspacePoint(Number(point, "x"), Number(point, "y"))).ToArray();
                if (points.Count < 3 || points.Count > 256) throw new InvalidDataException("Polygon ROI must contain between 3 and 256 points.");
                x = points.Min(point => point.X); y = points.Min(point => point.Y); width = points.Max(point => point.X) - x; height = points.Max(point => point.Y) - y;
            }
            else throw new InvalidDataException("This Web package currently supports Rectangle and Polygon ROI geometry only.");
            result.Add(new RoiWorkspaceRegion(
                Text(element, "id") ?? Guid.NewGuid().ToString("N"),
                Text(element, "name") ?? "ROI",
                Boolean(element, "enabled", true),
                Integer(element, "priority", 0),
                ParseEnum(Text(element, "inclusionMode"), RoiWorkspaceInclusionMode.Include),
                ParseEnum(Text(element, "executionMode"), RoiWorkspaceExecutionMode.FilterResults),
                ParseEnum(Text(element, "hitTestMode"), RoiWorkspaceHitTestMode.CenterPoint),
                OptionalNumber(element, "hitThreshold") ?? .5,
                OptionalNumber(element, "confidenceOverride"),
                x, y, width, height, points));
        }
        Validate(result);
        return result;
    }

    public static string FilterCanonicalOcrResult(string json, IEnumerable<RoiWorkspaceRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(regions);
        RoiWorkspaceRegion[] active = regions.Where(region => region.Enabled && region.ExecutionMode == RoiWorkspaceExecutionMode.FilterResults).OrderByDescending(region => region.Priority).ToArray();
        Validate(active);
        JsonNode? node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
        if (node is not JsonObject root) return json;
        string? kind = root["kind"]?.GetValue<string>();
        string collectionName = string.Equals(kind, "ocr-detection", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "ocr", StringComparison.OrdinalIgnoreCase) ? "regions" : string.Empty;
        if (collectionName.Length == 0 || root[collectionName] is not JsonArray items) return json;
        int inputCount = items.Count;
        var kept = new JsonArray();
        foreach (JsonNode? item in items)
        {
            if (item is not JsonObject value || !TryBounds(value, root, out Bounds bounds)) continue;
            RoiWorkspaceRegion[] includes = active.Where(region => region.InclusionMode == RoiWorkspaceInclusionMode.Include).ToArray();
            bool included = includes.Length == 0 || includes.Any(region => Hits(region, bounds));
            bool excluded = active.Any(region => region.InclusionMode == RoiWorkspaceInclusionMode.Exclude && Hits(region, bounds));
            RoiWorkspaceRegion? thresholdRegion = active.FirstOrDefault(region => Hits(region, bounds) && region.ConfidenceOverride.HasValue);
            double score = OptionalNodeNumber(value["score"] ?? value["confidence"]) ?? 1;
            if (included && !excluded && (thresholdRegion?.ConfidenceOverride is not double threshold || score >= threshold)) kept.Add(value.DeepClone());
        }
        root[collectionName] = kept;
        root["roiEvaluation"] = new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["coordinateSpace"] = "Normalized",
            ["inputCount"] = inputCount,
            ["outputCount"] = kept.Count,
            ["activeRoiIds"] = new JsonArray(active.Select(region => JsonValue.Create(region.Id)).ToArray())
        };
        return root.ToJsonString(JsonOptions);
    }

    public static string FormatCanonicalOcrSummary(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        JsonElement root = document.RootElement;
        string kind = Text(root, "kind") ?? "unknown";
        if (kind.Equals("ocr-detection", StringComparison.OrdinalIgnoreCase) || kind.Equals("ocr", StringComparison.OrdinalIgnoreCase))
        {
            int kept = root.TryGetProperty("regions", out JsonElement regions) && regions.ValueKind == JsonValueKind.Array ? regions.GetArrayLength() : 0;
            int input = root.TryGetProperty("roiEvaluation", out JsonElement evaluation) && evaluation.TryGetProperty("inputCount", out JsonElement inputCount) ? inputCount.GetInt32() : kept;
            string prefix = kind.Equals("ocr", StringComparison.OrdinalIgnoreCase) ? "PP-OCRv5 已识别" : "检测到";
            return $"{prefix} {input} 个文本区域，ROI 规则保留 {kept} 个。";
        }
        if (kind.Equals("ocr-recognition", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
        {
            string[] texts = items.EnumerateArray().Take(8).Select(item => Text(item, "text") ?? string.Empty).Where(value => value.Length > 0).ToArray();
            return texts.Length == 0 ? "识别完成，未产生非空文本。" : "识别文本：" + string.Join(" | ", texts);
        }
        if (kind.Equals("ocr-orientation", StringComparison.OrdinalIgnoreCase))
        {
            string orientation = Text(root, "acceptedOrientation") ?? Text(root, "orientation") ?? "未接受";
            double confidence = OptionalNumber(root, "confidence") ?? 0;
            return $"方向：{orientation}，置信度 {confidence.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}。";
        }
        return "Worker 已返回 " + kind + " 规范结果。";
    }

    private static bool TryBounds(JsonObject item, JsonObject root, out Bounds bounds)
    {
        JsonArray? points = item["points"] as JsonArray ?? item["polygon"] as JsonArray;
        if (points is null && item["region"] is JsonObject region) points = region["points"] as JsonArray ?? region["polygon"] as JsonArray;
        if (points is null || points.Count < 3) { bounds = default; return false; }
        double sourceWidth = OptionalNodeNumber(root["sourceWidth"]) ?? 1;
        double sourceHeight = OptionalNodeNumber(root["sourceHeight"]) ?? 1;
        if (sourceWidth <= 0 || sourceHeight <= 0) { bounds = default; return false; }
        double[] xs = points.Select(point => OptionalNodeNumber(point?["x"]) ?? double.NaN).ToArray();
        double[] ys = points.Select(point => OptionalNodeNumber(point?["y"]) ?? double.NaN).ToArray();
        if (xs.Any(double.IsNaN) || ys.Any(double.IsNaN)) { bounds = default; return false; }
        bounds = new Bounds(xs.Min() / sourceWidth, ys.Min() / sourceHeight, (xs.Max() - xs.Min()) / sourceWidth, (ys.Max() - ys.Min()) / sourceHeight);
        return true;
    }

    private static bool Hits(RoiWorkspaceRegion region, Bounds result)
    {
        Bounds roi = new(region.X, region.Y, region.Width, region.Height);
        double intersectionWidth = Math.Max(0, Math.Min(roi.Right, result.Right) - Math.Max(roi.X, result.X));
        double intersectionHeight = Math.Max(0, Math.Min(roi.Bottom, result.Bottom) - Math.Max(roi.Y, result.Y));
        double intersection = intersectionWidth * intersectionHeight;
        if (region.HitTestMode == RoiWorkspaceHitTestMode.CenterPoint) return result.CenterX >= roi.X && result.CenterX <= roi.Right && result.CenterY >= roi.Y && result.CenterY <= roi.Bottom;
        if (region.HitTestMode == RoiWorkspaceHitTestMode.AnyIntersection) return intersection > 0;
        double resultArea = Math.Max(double.Epsilon, result.Width * result.Height);
        double roiArea = Math.Max(double.Epsilon, roi.Width * roi.Height);
        double ratio = region.HitTestMode switch
        {
            RoiWorkspaceHitTestMode.IntersectionOverResult => intersection / resultArea,
            RoiWorkspaceHitTestMode.IntersectionOverRoi => intersection / roiArea,
            _ => intersection / Math.Max(double.Epsilon, resultArea + roiArea - intersection)
        };
        return ratio >= region.HitThreshold;
    }

    private static void Validate(IEnumerable<RoiWorkspaceRegion> regions)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (RoiWorkspaceRegion region in regions)
        {
            if (string.IsNullOrWhiteSpace(region.Id) || !ids.Add(region.Id)) throw new InvalidDataException("ROI identifiers must be non-empty and unique.");
            if (string.IsNullOrWhiteSpace(region.Name)) throw new InvalidDataException("ROI name is required.");
            if (!Finite01(region.X) || !Finite01(region.Y) || !Finite01(region.Width) || !Finite01(region.Height) || region.Width <= 0 || region.Height <= 0 || region.X + region.Width > 1.000001 || region.Y + region.Height > 1.000001) throw new InvalidDataException("Normalized ROI bounds must remain inside [0,1] and have positive size.");
            if (!Finite01(region.HitThreshold)) throw new InvalidDataException("ROI hitThreshold must be in [0,1].");
            if (region.ConfidenceOverride is double threshold && !Finite01(threshold)) throw new InvalidDataException("ROI confidenceOverride must be in [0,1].");
            if (region.Points is not null && region.Points.Any(point => !Finite01(point.X) || !Finite01(point.Y))) throw new InvalidDataException("Normalized polygon points must remain inside [0,1].");
        }
    }

    private static bool Finite01(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static double Number(JsonElement element, string name) => OptionalNumber(element, name) ?? throw new InvalidDataException($"ROI property '{name}' must be a number.");
    private static double? OptionalNumber(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result) ? result : null;
    private static double? OptionalNodeNumber(JsonNode? node) => node is JsonValue value && value.TryGetValue(out double result) ? result : null;
    private static bool Boolean(JsonElement element, string name, bool fallback) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
    private static int Integer(JsonElement element, string name, int fallback) => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : fallback;
    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum => Enum.TryParse(value, true, out T result) && Enum.IsDefined(result) ? result : fallback;
    private readonly record struct Bounds(double X, double Y, double Width, double Height) { public double Right => X + Width; public double Bottom => Y + Height; public double CenterX => X + Width / 2; public double CenterY => Y + Height / 2; }
}
