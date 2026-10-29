using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;

public sealed record InspectionManifest(int SchemaVersion, string Name, InspectionCamera[] Cameras, InspectionSnapshot[] Snapshots);
public sealed record InspectionCamera(string Id, string Name, InspectionZone[] Zones);
public sealed record InspectionZone(string Id, string Name, float[][] Polygon, int[] ClassIds, int TriggerAtLeast, string Reason);
public sealed record InspectionSnapshot(string Id, string CameraId, string Image, double SourceSeconds);
public sealed record ZoneObservation(string Id, string Name, int Count, int[] ObjectIds, bool Triggered, string Reason);
public sealed record InspectionEvent(string Id, string ZoneId, string Reason, int[] ObjectIds, string Status, string Evidence);
public sealed record AnalyzedObject(int Id, PixelBox Crop, string PromptGroup, bool RequiresReview, double Margin, SemanticMatch[] Matches);
public sealed record SceneObject(int Id, DetectedObject Detection, string[] ZoneIds, AnalyzedObject? Semantic, string? SkippedReason);
public sealed record SceneAnalysisResult(string PackageVersion, string Backend, InspectionSnapshot Snapshot, InspectionCamera Camera,
    int Width, int Height, object Environment, object Configuration, PromptGroup[] PromptGroups,
    int[] ImageBatches, ZoneObservation[] Zones, SceneObject[] Objects, InspectionEvent[] Events);

internal static class InspectionConfig
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static InspectionManifest Load(string file)
    {
        var manifest = JsonSerializer.Deserialize<InspectionManifest>(File.ReadAllText(file), Options)
            ?? throw new InvalidDataException("巡检配置为空。");
        if (manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.Name) || manifest.Cameras is not { Length: > 0 } || manifest.Snapshots is not { Length: > 0 })
            throw new InvalidDataException("需要 SchemaVersion=1、名称、摄像头和截图。");
        UniqueIds(manifest.Cameras.Select(c => c.Id)); UniqueIds(manifest.Snapshots.Select(s => s.Id));
        foreach (var camera in manifest.Cameras)
        {
            if (string.IsNullOrWhiteSpace(camera.Name) || camera.Zones is not { Length: > 0 }) throw new InvalidDataException("摄像头需要名称和区域。");
            UniqueIds(camera.Zones.Select(z => z.Id));
            foreach (var zone in camera.Zones)
            {
                if (string.IsNullOrWhiteSpace(zone.Name) || string.IsNullOrWhiteSpace(zone.Reason) || zone.TriggerAtLeast < 1 ||
                    zone.ClassIds is not { Length: > 0 } || zone.ClassIds.Any(i => i < 0 || i >= 80) ||
                    zone.Polygon is not { Length: >= 3 } || zone.Polygon.Any(p => p is not { Length: 2 } || p.Any(v => !float.IsFinite(v) || v < 0 || v > 1)))
                    throw new InvalidDataException("区域需有效的归一化多边形、COCO 类别和正整数触发数量。");
                _ = Geometry(zone);
            }
        }
        foreach (var snapshot in manifest.Snapshots)
            if (!manifest.Cameras.Any(c => c.Id == snapshot.CameraId) || !double.IsFinite(snapshot.SourceSeconds) || snapshot.SourceSeconds < 0 ||
                string.IsNullOrWhiteSpace(snapshot.Image) || !File.Exists(Path.GetFullPath(snapshot.Image, Path.GetDirectoryName(file)!)))
                throw new InvalidDataException("截图必须绑定已配置的摄像头、有效来源时间和存在的图像文件。");
        return manifest;
    }
    public static PolygonRoiGeometry Geometry(InspectionZone zone) => new(zone.Polygon.Select(p => new PointF(p[0], p[1])));
    public static PromptGroup[] LoadPrompts(string file)
    {
        var groups = JsonSerializer.Deserialize<PromptGroup[]>(File.ReadAllText(file), Options) ?? throw new InvalidDataException("候选描述为空。");
        if (groups.Length == 0 || groups.Any(g => string.IsNullOrWhiteSpace(g.Name) || g.ClassIds is not { Length: > 0 } ||
            g.ClassIds.Any(c => c < 0 || c >= 80) || g.Candidates is not { Length: >= 2 } ||
            g.Candidates.Any(p => p is null || string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Text))))
            throw new InvalidDataException("候选组需要有效类别和至少两条 Key/Text 描述。");
        if (groups.SelectMany(g => g.ClassIds).Distinct().Count() != groups.Sum(g => g.ClassIds.Length) ||
            groups.SelectMany(g => g.Candidates).Select(p => p.Key).Distinct().Count() != groups.Sum(g => g.Candidates.Length))
            throw new InvalidDataException("候选类别不能跨组重复，Key 必须全局唯一。");
        return groups;
    }
    private static void UniqueIds(IEnumerable<string> ids)
    {
        string[] values = ids.ToArray();
        if (values.Any(v => v is null || !Regex.IsMatch(v, "^[a-z0-9][a-z0-9-]{0,63}$")) || values.Distinct().Count() != values.Length)
            throw new InvalidDataException("ID 需为唯一的小写字母、数字、短横线，长度 1—64。");
    }
}

internal static class InspectionRules
{
    public static ZoneObservation[] Evaluate(InspectionCamera camera, ObjectDetections detections, VisualSize size)
        => camera.Zones.Select(zone =>
        {
            PolygonRoiGeometry geometry = InspectionConfig.Geometry(zone);
            int[] ids = detections.Objects.Select((d, i) => new { d, i })
                .Where(x => zone.ClassIds.Contains(x.d.ClassId) && geometry.Contains(new PointF(
                    (x.d.Box.X + x.d.Box.Width / 2) / size.Width,
                    Math.Min(size.Height - .001f, x.d.Box.Y + x.d.Box.Height) / size.Height)))
                .Select(x => x.i).ToArray();
            return new ZoneObservation(zone.Id, zone.Name, ids.Length, ids, ids.Length >= zone.TriggerAtLeast, zone.Reason);
        }).ToArray();
    public static PixelBox Expand(PixelBox box, VisualSize size, float padding)
    {
        int left = Math.Max(0, (int)Math.Floor(box.X - box.Width * padding));
        int top = Math.Max(0, (int)Math.Floor(box.Y - box.Height * padding));
        int right = Math.Min(size.Width, (int)Math.Ceiling(box.X + box.Width * (1 + padding)));
        int bottom = Math.Min(size.Height, (int)Math.Ceiling(box.Y + box.Height * (1 + padding)));
        return new(left, top, right - left, bottom - top);
    }
}
