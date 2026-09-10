using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Web;

/// <summary>Renders generic, contract-driven visual overlays without pretending to decode an unknown model format.</summary>
public static class VisionResultRenderer
{
    public static string Render(string originalImage, string? outputJson, string? task, VisualPostprocessingProfile? profile = null)
    {
        JsonDocument? document = null;
        JsonElement root = default;
        bool canonical = false;
        var layout = CanvasLayout.Default;
        try
        {
            document = JsonDocument.Parse(outputJson ?? "{}");
            root = document.RootElement;
            canonical = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("schema", out JsonElement schema)
                && string.Equals(schema.GetString(), "deploysharp.visual.result.v1", StringComparison.OrdinalIgnoreCase);
            if (canonical) layout = CanvasLayout.Fit(Number(root, "sourceWidth", 640), Number(root, "sourceHeight", 640));
        }
        catch (JsonException) { }

        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"640\" height=\"420\" viewBox=\"0 0 640 420\"><rect width=\"640\" height=\"420\" fill=\"#eef1f7\"/><image href=\"");
        svg.Append(SecurityElement.Escape(originalImage));
        svg.Append("\" x=\"").Append(F(layout.X)).Append("\" y=\"").Append(F(layout.Y)).Append("\" width=\"").Append(F(layout.Width)).Append("\" height=\"").Append(F(layout.Height)).Append("\" preserveAspectRatio=\"none\"/>");
        var overlays = new List<string>();
        var labels = new List<string>();
        try
        {
            if (canonical)
            {
                RenderCanonical(root, layout, overlays, labels);
            }
            else
            {
                JsonElement outputs = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("outputs", out JsonElement named) ? named : root;
                if (outputs.ValueKind == JsonValueKind.Array)
                {
                    VisualPostprocessingProfile effectiveProfile = profile ?? VisualPostprocessingProfile.FromModel(task ?? string.Empty, string.Empty, string.Empty);
                    OutputData[] tensors = outputs.EnumerateArray().Select(ReadOutput).Where(item => item is not null).Cast<OutputData>().ToArray();
                    bool handled = TryRenderNamedDetectionSet(tensors, effectiveProfile, overlays, labels);
                    if (!handled) handled = effectiveProfile.Kind == VisualPostprocessingKind.Segmentation && TryRenderYoloSegmentation(tensors, effectiveProfile, overlays, labels);
                    if (!handled) foreach (JsonElement output in outputs.EnumerateArray()) ParseOutput(output, overlays, labels, effectiveProfile);
                }
            }
        }
        catch (JsonException) { labels.Add("输出不是可解析的 tensor JSON"); }
        finally { document?.Dispose(); }
        if (overlays.Count == 0) labels.Add("未发现可绘制的后处理契约");
        string title = string.IsNullOrWhiteSpace(task) ? "视觉结果" : task!;
        if (profile is not null) labels.Insert(0, profile.Kind + " · " + profile.Coordinates);
        svg.Append(string.Join(string.Empty, overlays));
        svg.Append("<rect x=\"20\" y=\"380\" width=\"600\" height=\"30\" fill=\"#24345f\"/><text x=\"32\" y=\"400\" fill=\"white\" font-family=\"Segoe UI\" font-size=\"13\">");
        svg.Append(SecurityElement.Escape(title + " · " + string.Join("；", labels.Take(3))));
        svg.Append("</text></svg>");
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString()));
    }

    public static string FormatForDisplay(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson)) return string.Empty;
        try
        {
            JsonNode? node = JsonNode.Parse(outputJson);
            if (node is null) return outputJson;
            SanitizeMaskValues(node, null);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException) { return outputJson; }
    }

    private static void RenderCanonical(JsonElement root, CanvasLayout layout, List<string> overlays, List<string> labels)
    {
        string kind = root.TryGetProperty("kind", out JsonElement kindElement) ? kindElement.GetString() ?? string.Empty : string.Empty;
        if (string.Equals(kind, "detection", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            if (root.TryGetProperty("detections", out JsonElement detections) && detections.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in detections.EnumerateArray())
            {
                if (!TryNumber(item, "x", out double x) || !TryNumber(item, "y", out double y) || !TryNumber(item, "width", out double width) || !TryNumber(item, "height", out double height)) continue;
                string label = StringValue(item, "label") ?? ("class " + (StringValue(item, "classIndex") ?? "0"));
                double score = Number(item, "score");
                DrawBox(overlays, x, y, width, height, score, label, "#ff5c5c", layout, center: false); count++;
            }
            labels.Add("主库 Detection 解码 · " + count.ToString(CultureInfo.InvariantCulture) + " 个结果"); return;
        }
        if (string.Equals(kind, "segmentation", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            if (root.TryGetProperty("instances", out JsonElement instances) && instances.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in instances.EnumerateArray())
            {
                if (item.TryGetProperty("box", out JsonElement box) && TryNumber(box, "x", out double x) && TryNumber(box, "y", out double y) && TryNumber(box, "width", out double width) && TryNumber(box, "height", out double height))
                    DrawBox(overlays, x, y, width, height, Number(item, "score"), StringValue(item, "label") ?? "segment", "#ff5c5c", layout, center: false);
                if (item.TryGetProperty("mask", out JsonElement mask)) RenderCanonicalMask(mask, layout, overlays);
                count++;
            }
            labels.Add("主库 Instance Segmentation 解码 · " + count.ToString(CultureInfo.InvariantCulture) + " 个实例"); return;
        }
        if (string.Equals(kind, "pose", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            if (root.TryGetProperty("instances", out JsonElement instances) && instances.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in instances.EnumerateArray())
            {
                if (item.TryGetProperty("keypoints", out JsonElement points) && points.ValueKind == JsonValueKind.Array)
                foreach (JsonElement point in points.EnumerateArray()) if (TryNumber(point, "x", out double x) && TryNumber(point, "y", out double y) && Number(point, "score") >= .2) overlays.Add("<circle cx=\"" + F(layout.MapX(x)) + "\" cy=\"" + F(layout.MapY(y)) + "\" r=\"4\" fill=\"#ffcc33\" stroke=\"#1d2742\" stroke-width=\"1\"/>");
                count++;
            }
            labels.Add("主库 Pose 解码 · " + count.ToString(CultureInfo.InvariantCulture) + " 个实例"); return;
        }
        if (string.Equals(kind, "obb", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            if (root.TryGetProperty("detections", out JsonElement detections) && detections.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in detections.EnumerateArray())
            {
                if (!item.TryGetProperty("points", out JsonElement points) || points.ValueKind != JsonValueKind.Array) continue;
                var polygon = new StringBuilder("<polygon points=\""); foreach (JsonElement point in points.EnumerateArray()) if (TryNumber(point, "x", out double x) && TryNumber(point, "y", out double y)) polygon.Append(F(layout.MapX(x))).Append(',').Append(F(layout.MapY(y))).Append(' ');
                polygon.Append("\" fill=\"none\" stroke=\"#ff9f43\" stroke-width=\"2\"/>"); overlays.Add(polygon.ToString()); count++;
            }
            labels.Add("主库 OBB 解码 · " + count.ToString(CultureInfo.InvariantCulture) + " 个结果"); return;
        }
        if (string.Equals(kind, "anomaly", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("map", out JsonElement map)) RenderCanonicalMask(map, layout, overlays);
            labels.Add("主库 Anomalib 解码 · score=" + F(Number(root, "score")) + " · anomalous ratio=" + F(Number(root, "anomalousPixelRatio"))); return;
        }
        if (string.Equals(kind, "foreground-matting", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("alpha", out JsonElement alpha)) RenderCanonicalMask(alpha, layout, overlays);
            labels.Add("主库 BRIA RMBG Alpha 解码 · 已映射回原图坐标"); return;
        }
        if (string.Equals(kind, "classification", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("predictions", out JsonElement predictions) && predictions.ValueKind == JsonValueKind.Array) labels.Add("主库 Classification · " + string.Join(", ", predictions.EnumerateArray().Take(5).Select(item => (StringValue(item, "label") ?? "class") + "=" + F(Number(item, "score"))))); return;
        }
        if (string.Equals(kind, "ocr-detection", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            if (root.TryGetProperty("regions", out JsonElement regions) && regions.ValueKind == JsonValueKind.Array)
            foreach (JsonElement region in regions.EnumerateArray())
            {
                if (!region.TryGetProperty("points", out JsonElement points) || points.ValueKind != JsonValueKind.Array) continue;
                var polygon = new StringBuilder("<polygon points=\"");
                foreach (JsonElement point in points.EnumerateArray()) if (TryNumber(point, "x", out double x) && TryNumber(point, "y", out double y)) polygon.Append(F(layout.MapX(x))).Append(',').Append(F(layout.MapY(y))).Append(' ');
                overlays.Add(polygon.Append("\" fill=\"none\" stroke=\"#00b894\" stroke-width=\"2\"/>").ToString()); count++;
            }
            labels.Add("主库 PaddleOCR 检测解码 · " + count.ToString(CultureInfo.InvariantCulture) + " 个文本区域"); return;
        }
        if (string.Equals(kind, "ocr-recognition", StringComparison.OrdinalIgnoreCase))
        {
            if (root.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
                labels.Add("主库 PaddleOCR 字典解码 · " + string.Join(" | ", items.EnumerateArray().Take(3).Select(item => (StringValue(item, "text") ?? string.Empty) + "=" + F(Number(item, "confidence")))));
            return;
        }
        if (string.Equals(kind, "ocr-orientation", StringComparison.OrdinalIgnoreCase))
        {
            string orientation = StringValue(root, "acceptedOrientation") ?? "置信度不足，保持原方向";
            labels.Add("主库 PaddleOCR 方向分类 · " + orientation + " · confidence=" + F(Number(root, "confidence")));
            return;
        }
        labels.Add("主库视觉结果：" + kind);
    }

    private static void RenderCanonicalMask(JsonElement mask, CanvasLayout layout, List<string> overlays)
    {
        if (!mask.TryGetProperty("values", out JsonElement values)) return;
        byte[]? bytes = null;
        int length;
        if (values.ValueKind == JsonValueKind.String)
        {
            try { bytes = Convert.FromBase64String(values.GetString() ?? string.Empty); }
            catch (FormatException) { return; }
            length = bytes.Length;
        }
        else if (values.ValueKind == JsonValueKind.Array) length = values.GetArrayLength();
        else return;
        if (length == 0) return;
        int sampleWidth = Math.Max(1, (int)Number(mask, "sampleWidth", Math.Round(Math.Sqrt(length))));
        int sampleHeight = Math.Max(1, (int)Number(mask, "sampleHeight", Math.Ceiling(length / (double)sampleWidth)));
        string coordinateSpace = StringValue(mask, "coordinateSpace") ?? "SourceImage";
        if (!string.Equals(coordinateSpace, "SourceImage", StringComparison.OrdinalIgnoreCase)) return;
        double maskWidth = Math.Max(1, Number(mask, "width", layout.SourceWidth));
        double maskHeight = Math.Max(1, Number(mask, "height", layout.SourceHeight));
        double originX = Number(mask, "originX");
        double originY = Number(mask, "originY");
        double cellWidth = maskWidth / sampleWidth;
        double cellHeight = maskHeight / sampleHeight;
        for (int index = 0; index < Math.Min(length, sampleWidth * sampleHeight); index++)
        {
            double value = bytes is null ? values[index].GetDouble() : bytes[index]; if (value <= .35) continue;
            int row = index / sampleWidth, column = index % sampleWidth;
            overlays.Add("<rect x=\"" + F(layout.MapX(originX + column * cellWidth)) + "\" y=\"" + F(layout.MapY(originY + row * cellHeight)) + "\" width=\"" + F(cellWidth * layout.ScaleX + .75) + "\" height=\"" + F(cellHeight * layout.ScaleY + .75) + "\" fill=\"#4d7cff\" opacity=\".30\"/>");
        }
    }

    private static void DrawBox(List<string> overlays, double x, double y, double width, double height, double score, string label, string color, CanvasLayout layout, bool center = true)
    {
        double left = center ? x - width / 2 : x;
        double top = center ? y - height / 2 : y;
        overlays.Add("<rect x=\"" + F(layout.MapX(left)) + "\" y=\"" + F(layout.MapY(top)) + "\" width=\"" + F(Math.Abs(width) * layout.ScaleX) + "\" height=\"" + F(Math.Abs(height) * layout.ScaleY) + "\" fill=\"none\" stroke=\"" + color + "\" stroke-width=\"2\"/><text x=\"" + F(layout.MapX(left) + 3) + "\" y=\"" + F(layout.MapY(top) + 14) + "\" fill=\"" + color + "\" font-size=\"12\">" + SecurityElement.Escape(label + " · " + F(score)) + "</text>");
    }

    private static bool TryNumber(JsonElement element, string name, out double value) { value = 0; return element.TryGetProperty(name, out JsonElement property) && property.TryGetDouble(out value); }
    private static double Number(JsonElement element, string name) => TryNumber(element, name, out double value) ? value : 0;
    private static double Number(JsonElement element, string name, double fallback) => TryNumber(element, name, out double value) ? value : fallback;
    private static string? StringValue(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement property) ? property.ToString() : null;

    private static void ParseOutput(JsonElement output, List<string> overlays, List<string> labels, VisualPostprocessingProfile profile)
    {
        if (output.ValueKind != JsonValueKind.Object) return;
        string name = output.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? "output" : "output";
        long[] shape = output.TryGetProperty("shape", out JsonElement shapeElement) && shapeElement.ValueKind == JsonValueKind.Array ? shapeElement.EnumerateArray().Select(value => value.GetInt64()).ToArray() : Array.Empty<long>();
        if (!output.TryGetProperty("values", out JsonElement valuesElement)) return;
        if (valuesElement.ValueKind == JsonValueKind.String)
        {
            labels.Add(name + ": " + valuesElement.GetString());
            return;
        }
        var values = new List<double>();
        Flatten(valuesElement, values);
        string lower = name.ToLowerInvariant();
        if (profile.Kind == VisualPostprocessingKind.Classification)
        {
            var ranked = values.Select((value, index) => (value, index)).OrderByDescending(item => item.value).Take(profile.TopK).ToArray();
            if (ranked.Length > 0) labels.Add(name + " top-" + ranked.Length + ": " + string.Join(", ", ranked.Select(item => "class " + item.index.ToString(CultureInfo.InvariantCulture) + "=" + F(item.value))));
            return;
        }
        if (profile.Kind == VisualPostprocessingKind.Embedding)
        {
            double norm = Math.Sqrt(values.Sum(value => value * value));
            labels.Add(name + " embedding · dim=" + values.Count.ToString(CultureInfo.InvariantCulture) + " · L2=" + F(norm));
            return;
        }
        if (profile.Kind == VisualPostprocessingKind.AnomalyDetection)
        {
            if (lower.Contains("score") && values.Count > 0) labels.Add(name + " anomaly score=" + F(values[0]));
            else if (lower.Contains("label") && values.Count > 0) labels.Add(name + " label=" + ((int)Math.Round(values[0])).ToString(CultureInfo.InvariantCulture));
            else if ((lower.Contains("map") || lower.Contains("mask")) && shape.Length >= 2 && shape[^1] > 1 && shape[^2] > 1)
            {
                RenderProbabilityMap(overlays, values, SafeDimension(shape[^2], 0), SafeDimension(shape[^1], 0), "#e67e22", 0.35);
                labels.Add(name + " anomaly map");
            }
            return;
        }
        if (profile.Kind == VisualPostprocessingKind.Captioning || profile.Kind == VisualPostprocessingKind.OcrRecognition)
        {
            labels.Add(name + " " + (values.Count == 0 ? "empty" : LooksLikeTokenIds(values) ? "token ids=" + string.Join(",", values.Take(12).Select(item => ((int)item).ToString(CultureInfo.InvariantCulture))) + " · 需要词表/解码器" : "logits " + ShapeText(shape) + " · 未提供词表/解码器"));
            return;
        }
        if (profile.Kind == VisualPostprocessingKind.OcrDetection && shape.Length >= 2 && shape[^1] > 16 && shape[^2] > 16)
        {
            RenderProbabilityMap(overlays, values, SafeDimension(shape[^2], 0), SafeDimension(shape[^1], 0), "#00b894", 0.3);
            labels.Add(name + " OCR probability map");
            return;
        }
        if (profile.Kind == VisualPostprocessingKind.BackgroundRemoval && shape.Length >= 2 && shape[^1] > 16 && shape[^2] > 16)
        {
            RenderProbabilityMap(overlays, values, SafeDimension(shape[^2], 0), SafeDimension(shape[^1], 0), "#4d7cff", 0.35);
            labels.Add(name + " foreground alpha");
            return;
        }
        if (TryRenderYoloRaw(name, shape, values, profile, overlays, labels)) return;
        if (profile.Kind == VisualPostprocessingKind.Pose)
        {
            int count = values.Count / 3;
            for (int index = 0; index < count; index++)
            {
                double x = X(values[index * 3]);
                double y = Y(values[index * 3 + 1]);
                double confidence = values[index * 3 + 2];
                if (confidence >= 0.2) overlays.Add("<circle cx=\"" + F(x) + "\" cy=\"" + F(y) + "\" r=\"4\" fill=\"#ffcc33\" stroke=\"#1d2742\" stroke-width=\"1\"/>");
            }
            labels.Add(name + " keypoints");
            return;
        }
        if (profile.Kind == VisualPostprocessingKind.OcrDetection && values.Count >= 8 && values.Count % 8 == 0)
        {
            for (int index = 0; index + 8 <= values.Count; index += 8)
            {
                var points = new StringBuilder("<polygon points=\"");
                for (int point = 0; point < 4; point++) points.Append(F(X(values[index + point * 2]))).Append(',').Append(F(Y(values[index + point * 2 + 1]))).Append(' ');
                overlays.Add(points.Append("\" fill=\"none\" stroke=\"#00b894\" stroke-width=\"2\"/>").ToString());
            }
            labels.Add(name + " text regions");
            return;
        }
        int widthPerRow = shape.Length > 0 && shape[^1] is >= 4 and <= 7 ? (int)shape[^1] : values.Count % 6 == 0 ? 6 : 0;
        if (widthPerRow is 6 or 7)
        {
            for (int index = 0; index + widthPerRow <= values.Count; index += widthPerRow)
            {
                bool oriented = profile.Kind == VisualPostprocessingKind.OrientedDetection && widthPerRow == 7;
                int coordinateOffset = widthPerRow == 7 && !oriented ? 1 : 0;
                int scoreIndex = oriented || widthPerRow == 7 ? index + 6 : index + 4;
                int classIndex = oriented || widthPerRow == 7 ? index + 5 : index + 5;
                if (oriented) { scoreIndex = index + 5; classIndex = index + 6; }
                double score = values[scoreIndex] > 1 ? values[scoreIndex] / 100 : values[scoreIndex];
                if (score < .15) continue;
                double x1 = X(values[index + coordinateOffset]); double y1 = Y(values[index + coordinateOffset + 1]);
                double x2 = X(values[index + coordinateOffset + 2]); double y2 = Y(values[index + coordinateOffset + 3]);
                if (oriented)
                {
                    DrawRotatedBox(overlays, new BoxCandidate(values[index], values[index + 1], Math.Abs(values[index + 2]), Math.Abs(values[index + 3]), score, (int)values[classIndex], values[index + 4]));
                }
                else overlays.Add("<rect x=\"" + F(Math.Min(x1, x2)) + "\" y=\"" + F(Math.Min(y1, y2)) + "\" width=\"" + F(Math.Abs(x2 - x1)) + "\" height=\"" + F(Math.Abs(y2 - y1)) + "\" fill=\"none\" stroke=\"#ff5c5c\" stroke-width=\"2\"/><text x=\"" + F(Math.Min(x1, x2) + 3) + "\" y=\"" + F(Math.Min(y1, y2) + 14) + "\" fill=\"#ff5c5c\" font-size=\"12\">class " + ((int)values[classIndex]).ToString(CultureInfo.InvariantCulture) + " · " + F(score) + "</text>");
            }
            labels.Add(name + " boxes");
            return;
        }
        if (lower.Contains("mask") || lower.Contains("seg") || IsMatrix(shape, values.Count))
        {
            int height = shape.Length >= 2 ? SafeDimension(shape[^2], 32) : 32;
            int width = shape.Length >= 1 ? SafeDimension(shape[^1], 32) : 32;
            height = Math.Min(height, 32); width = Math.Min(width, 32);
            int offset = Math.Max(0, values.Count - height * width);
            for (int row = 0; row < height; row++) for (int column = 0; column < width; column++)
            {
                double value = values[offset + row * width + column];
                if (value > 0.35) overlays.Add("<rect x=\"" + F(20 + column * 600d / width) + "\" y=\"" + F(20 + row * 360d / height) + "\" width=\"" + F(600d / width + .5) + "\" height=\"" + F(360d / height + .5) + "\" fill=\"#4d7cff\" opacity=\"" + F(Math.Min(.55, Math.Max(.12, value * .5))) + "\"/>");
            }
            labels.Add(name + " mask");
            return;
        }
        if (lower.Contains("ocr") || lower.Contains("text")) labels.Add(name + " tensor（需模型声明文本解码契约）");
    }

    private static OutputData? ReadOutput(JsonElement output)
    {
        if (output.ValueKind != JsonValueKind.Object || !output.TryGetProperty("values", out JsonElement valuesElement)) return null;
        string name = output.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? "output" : "output";
        long[] shape = output.TryGetProperty("shape", out JsonElement shapeElement) && shapeElement.ValueKind == JsonValueKind.Array ? shapeElement.EnumerateArray().Select(value => value.GetInt64()).ToArray() : Array.Empty<long>();
        var values = new List<double>();
        Flatten(valuesElement, values);
        return new OutputData(name, shape, values);
    }

    private static bool TryRenderNamedDetectionSet(IReadOnlyList<OutputData> outputs, VisualPostprocessingProfile profile, List<string> overlays, List<string> labels)
    {
        if (profile.Kind is not (VisualPostprocessingKind.Detection or VisualPostprocessingKind.OrientedDetection or VisualPostprocessingKind.Segmentation)) return false;
        OutputData? boxes = outputs.FirstOrDefault(item => (item.Name.Contains("box", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("det", StringComparison.OrdinalIgnoreCase)) && item.Shape.Length >= 1 && item.Shape[^1] == 4 && item.Values.Count >= 4);
        OutputData? scores = outputs.FirstOrDefault(item => item.Name.Contains("score", StringComparison.OrdinalIgnoreCase) && !item.Name.Contains("logit", StringComparison.OrdinalIgnoreCase));
        OutputData? labelsTensor = outputs.FirstOrDefault(item => item.Name.Equals("labels", StringComparison.OrdinalIgnoreCase) || item.Name.Contains("label", StringComparison.OrdinalIgnoreCase));
        if (boxes is null || labelsTensor is null) return false;
        int candidates = boxes.Values.Count / 4;
        if (candidates == 0 || labelsTensor.Values.Count == 0 || (scores is not null && scores.Values.Count == 0)) return false;
        int labelWidth = labelsTensor.Shape.Length >= 3 && labelsTensor.Shape[^1] > 1 ? SafeDimension(labelsTensor.Shape[^1], 0) : 1;
        bool rawQueryScores = labelWidth > 1;
        int usable = Math.Min(candidates, rawQueryScores ? labelsTensor.Values.Count / labelWidth : labelsTensor.Values.Count);
        if (scores is not null) usable = Math.Min(usable, scores.Values.Count);
        if (usable == 0) return false;
        var decoded = new List<BoxCandidate>(Math.Min(usable, 3000));
        for (int index = 0; index < usable; index++)
        {
            double score = scores is null ? 1 : NormalizeScore(scores.Values[index]);
            int classId = 0;
            if (rawQueryScores)
            {
                double best = double.NegativeInfinity;
                for (int classIndex = 0; classIndex < labelWidth; classIndex++)
                {
                    double candidate = labelsTensor.Values[index * labelWidth + classIndex];
                    if (candidate > best) { best = candidate; classId = classIndex; }
                }
                score *= NormalizeScore(best);
            }
            else classId = (int)Math.Round(labelsTensor.Values[index]);
            if (!double.IsFinite(score) || score < profile.ScoreThreshold) continue;
            int offset = index * 4;
            double a = boxes.Values[offset], b = boxes.Values[offset + 1], c = Math.Abs(boxes.Values[offset + 2]), d = Math.Abs(boxes.Values[offset + 3]);
            decoded.Add(rawQueryScores ? new BoxCandidate(a, b, c, d, score, classId, 0, index) : new BoxCandidate(a, b, c, d, score, classId, 0, index));
        }
        IReadOnlyList<BoxCandidate> kept = Suppress(decoded.OrderByDescending(item => item.Score).Take(3000), 0.45, 50);
        OutputData? masks = profile.Kind == VisualPostprocessingKind.Segmentation ? outputs.FirstOrDefault(item => item.Name.Contains("mask", StringComparison.OrdinalIgnoreCase) && item.Shape.Length >= 4) : null;
        int renderedMasks = 0;
        foreach (BoxCandidate item in kept)
        {
            if (rawQueryScores) DrawCenterBox(overlays, item.CenterX, item.CenterY, item.Width, item.Height, item.Score, item.ClassId, "#ff5c5c");
            else DrawXyxyBox(overlays, item.CenterX, item.CenterY, item.Width, item.Height, item.Score, item.ClassId, "#ff5c5c");
            if (masks is not null && item.SourceIndex >= 0 && renderedMasks++ < 8) RenderNamedMask(overlays, masks, item.SourceIndex);
        }
        labels.Add(boxes.Name + " + " + labelsTensor.Name + (scores is null ? string.Empty : " + " + scores.Name) + " · " + kept.Count.ToString(CultureInfo.InvariantCulture) + " decoded results");
        if (masks is not null) labels.Add(masks.Name + " instance masks");
        return true;
    }

    private static void RenderNamedMask(List<string> overlays, OutputData masks, int candidate)
    {
        int height = SafeDimension(masks.Shape[^2], 0), width = SafeDimension(masks.Shape[^1], 0);
        int candidates = SafeDimension(masks.Shape[^3], 0);
        if (height == 0 || width == 0 || candidates == 0 || masks.Values.Count < candidates * height * width) return;
        int step = Math.Max(1, Math.Max(height, width) / 48);
        int offset = candidate * height * width;
        for (int row = 0; row < height; row += step)
        for (int column = 0; column < width; column += step)
        {
            double probability = NormalizeScore(masks.Values[offset + row * width + column]);
            if (probability < 0.5) continue;
            overlays.Add("<rect x=\"" + F(20 + column * 600d / width) + "\" y=\"" + F(20 + row * 360d / height) + "\" width=\"" + F(step * 600d / width + .5) + "\" height=\"" + F(step * 360d / height + .5) + "\" fill=\"#4d7cff\" opacity=\"" + F(Math.Min(.5, .18 + probability * .3)) + "\"/>");
        }
    }

    private static bool TryRenderYoloSegmentation(IReadOnlyList<OutputData> outputs, VisualPostprocessingProfile profile, List<string> overlays, List<string> labels)
    {
        OutputData? prototype = outputs.FirstOrDefault(item => item.Shape.Length >= 3 && item.Shape[^3] is >= 8 and <= 64 && item.Shape[^2] > 16 && item.Shape[^1] > 16);
        if (prototype is null) return false;
        int maskChannels = SafeDimension(prototype.Shape[^3], 0);
        int maskHeight = SafeDimension(prototype.Shape[^2], 0);
        int maskWidth = SafeDimension(prototype.Shape[^1], 0);
        if (maskChannels == 0 || maskHeight == 0 || maskWidth == 0 || prototype.Values.Count < maskChannels * maskHeight * maskWidth) return false;
        OutputData? head = outputs.FirstOrDefault(item =>
        {
            if (ReferenceEquals(item, prototype) || item.Shape.Length < 2) return false;
            int a = SafeDimension(item.Shape[^2], 0), b = SafeDimension(item.Shape[^1], 0);
            int fields = a is >= 37 and <= 256 && b > a ? a : b is >= 37 and <= 256 && a > b ? b : 0;
            return fields > maskChannels + 4;
        });
        if (head is null) return false;
        int first = SafeDimension(head.Shape[^2], 0), second = SafeDimension(head.Shape[^1], 0);
        bool attributeMajor = first <= 256 && second > first;
        int fields = attributeMajor ? first : second;
        int candidates = attributeMajor ? second : first;
        if (head.Values.Count < fields * candidates) return false;
        double Read(int candidate, int field) => attributeMajor ? head.Values[field * candidates + candidate] : head.Values[candidate * fields + field];
        int classStart = attributeMajor ? 4 : 5;
        int classEnd = fields - maskChannels;
        if (classEnd <= classStart) return false;
        var decoded = new List<BoxCandidate>();
        for (int candidate = 0; candidate < candidates; candidate++)
        {
            int bestClass = 0; double bestScore = Read(candidate, classStart);
            for (int field = classStart + 1; field < classEnd; field++) if (Read(candidate, field) > bestScore) { bestScore = Read(candidate, field); bestClass = field - classStart; }
            double confidence = (classStart == 5 ? Read(candidate, 4) : 1) * bestScore;
            if (confidence >= profile.ScoreThreshold) decoded.Add(new BoxCandidate(Read(candidate, 0), Read(candidate, 1), Math.Abs(Read(candidate, 2)), Math.Abs(Read(candidate, 3)), confidence, bestClass, 0, candidate));
        }
        IReadOnlyList<BoxCandidate> kept = Suppress(decoded.OrderByDescending(item => item.Score).Take(3000), 0.45, 30);
        int renderedMasks = 0;
        foreach (BoxCandidate box in kept)
        {
            DrawCenterBox(overlays, box.CenterX, box.CenterY, box.Width, box.Height, box.Score, box.ClassId, "#ff5c5c");
            if (renderedMasks++ < 8) RenderInstanceMask(overlays, head, prototype, box, classEnd, maskChannels, maskHeight, maskWidth, attributeMajor, candidates, fields);
        }
        labels.Add(head.Name + " + " + prototype.Name + " · " + kept.Count.ToString(CultureInfo.InvariantCulture) + " instance masks");
        return true;
    }

    private static void RenderInstanceMask(List<string> overlays, OutputData head, OutputData prototype, BoxCandidate box, int coefficientOffset, int channels, int height, int width, bool attributeMajor, int candidates, int fields)
    {
        double ReadHead(int field) => attributeMajor ? head.Values[field * candidates + box.SourceIndex] : head.Values[box.SourceIndex * fields + field];
        int step = Math.Max(1, Math.Max(width, height) / 48);
        double left = box.CenterX - box.Width / 2, right = box.CenterX + box.Width / 2, top = box.CenterY - box.Height / 2, bottom = box.CenterY + box.Height / 2;
        for (int row = 0; row < height; row += step)
        for (int column = 0; column < width; column += step)
        {
            double modelX = (column + .5) * 640d / width, modelY = (row + .5) * 640d / height;
            if (modelX < left || modelX > right || modelY < top || modelY > bottom) continue;
            double logit = 0;
            for (int channel = 0; channel < channels; channel++) logit += ReadHead(coefficientOffset + channel) * prototype.Values[channel * height * width + row * width + column];
            double probability = 1d / (1d + Math.Exp(-Math.Clamp(logit, -30, 30)));
            if (probability < 0.5) continue;
            overlays.Add("<rect x=\"" + F(20 + column * 600d / width) + "\" y=\"" + F(20 + row * 360d / height) + "\" width=\"" + F(step * 600d / width + .5) + "\" height=\"" + F(step * 360d / height + .5) + "\" fill=\"#4d7cff\" opacity=\"" + F(Math.Min(.5, .18 + probability * .3)) + "\"/>");
        }
    }

    private static void RenderProbabilityMap(List<string> overlays, IReadOnlyList<double> values, int height, int width, string color, double threshold)
    {
        if (height <= 0 || width <= 0 || values.Count < height * width) return;
        int offset = values.Count - height * width;
        int step = Math.Max(1, Math.Max(width, height) / 48);
        for (int row = 0; row < height; row += step)
        for (int column = 0; column < width; column += step)
        {
            double probability = values[offset + row * width + column];
            if (probability < threshold) continue;
            overlays.Add("<rect x=\"" + F(20 + column * 600d / width) + "\" y=\"" + F(20 + row * 360d / height) + "\" width=\"" + F(step * 600d / width + .5) + "\" height=\"" + F(step * 360d / height + .5) + "\" fill=\"" + color + "\" opacity=\"" + F(Math.Min(.5, Math.Max(.12, probability * .45))) + "\"/>");
        }
    }

    private static void DrawXyxyBox(List<string> overlays, double x1, double y1, double x2, double y2, double score, int classId, string color)
    {
        double left = X(Math.Min(x1, x2)), top = Y(Math.Min(y1, y2)), right = X(Math.Max(x1, x2)), bottom = Y(Math.Max(y1, y2));
        overlays.Add("<rect x=\"" + F(left) + "\" y=\"" + F(top) + "\" width=\"" + F(Math.Abs(right - left)) + "\" height=\"" + F(Math.Abs(bottom - top)) + "\" fill=\"none\" stroke=\"" + color + "\" stroke-width=\"2\"/><text x=\"" + F(left + 3) + "\" y=\"" + F(top + 14) + "\" fill=\"" + color + "\" font-size=\"12\">class " + classId.ToString(CultureInfo.InvariantCulture) + " · " + F(score) + "</text>");
    }

    private static double NormalizeScore(double value) => value is >= 0 and <= 1 ? value : 1d / (1d + Math.Exp(-Math.Clamp(value, -30, 30)));
    private static bool LooksLikeTokenIds(IReadOnlyList<double> values) => values.Count > 0 && values.Take(Math.Min(values.Count, 64)).All(value => value >= 0 && value <= 100000 && Math.Abs(value - Math.Round(value)) < 0.0001);
    private static string ShapeText(IReadOnlyList<long> shape) => shape.Count == 0 ? "[unknown]" : "[" + string.Join(",", shape) + "]";

    private static bool TryRenderYoloRaw(string name, long[] shape, IReadOnlyList<double> values, VisualPostprocessingProfile profile, List<string> overlays, List<string> labels)
    {
        if (shape.Length < 2 || profile.Kind is VisualPostprocessingKind.Classification or VisualPostprocessingKind.OcrDetection or VisualPostprocessingKind.OcrRecognition or VisualPostprocessingKind.Captioning or VisualPostprocessingKind.Embedding) return false;
        int first = SafeDimension(shape[^2], 0);
        int second = SafeDimension(shape[^1], 0);
        bool attributeMajor = first is >= 8 and <= 256 && second > first;
        bool candidateMajor = second is >= 8 and <= 256 && first > second;
        if (!attributeMajor && !candidateMajor) return false;
        int fields = attributeMajor ? first : second;
        int candidates = attributeMajor ? second : first;
        if (values.Count < checked(fields * candidates)) return false;

        double Read(int candidate, int field) => attributeMajor ? values[field * candidates + candidate] : values[candidate * fields + field];
        if (profile.Kind == VisualPostprocessingKind.Pose && fields >= 8)
        {
            var people = Enumerable.Range(0, candidates).Select(index => (index, score: Read(index, 4))).Where(item => item.score >= profile.ScoreThreshold).OrderByDescending(item => item.score).Take(10).ToArray();
            foreach (var person in people)
            {
                DrawCenterBox(overlays, Read(person.index, 0), Read(person.index, 1), Read(person.index, 2), Read(person.index, 3), person.score, 0, "#ff5c5c");
                for (int field = 5; field + 2 < fields; field += 3)
                    if (Read(person.index, field + 2) >= profile.ScoreThreshold) overlays.Add("<circle cx=\"" + F(X(Read(person.index, field))) + "\" cy=\"" + F(Y(Read(person.index, field + 1))) + "\" r=\"4\" fill=\"#ffcc33\" stroke=\"#1d2742\" stroke-width=\"1\"/>");
            }
            labels.Add(name + " YOLO pose · " + people.Length.ToString(CultureInfo.InvariantCulture) + " candidates");
            return true;
        }

        int classStart = candidateMajor || fields is 85 or 117 ? 5 : 4;
        int classEnd = fields;
        if (profile.Kind == VisualPostprocessingKind.OrientedDetection && classEnd > classStart) classEnd--;
        if (profile.Kind == VisualPostprocessingKind.Segmentation && classEnd - classStart > 32) classEnd -= 32;
        if (classEnd <= classStart) return false;
        bool hasObjectness = classStart == 5;
        var decoded = new List<BoxCandidate>();
        for (int candidate = 0; candidate < candidates; candidate++)
        {
            int bestClass = 0;
            double bestScore = Read(candidate, classStart);
            for (int field = classStart + 1; field < classEnd; field++)
            {
                double score = Read(candidate, field);
                if (score > bestScore) { bestScore = score; bestClass = field - classStart; }
            }
            double confidence = (hasObjectness ? Read(candidate, 4) : 1) * bestScore;
            if (!double.IsFinite(confidence) || confidence < profile.ScoreThreshold) continue;
            decoded.Add(new BoxCandidate(Read(candidate, 0), Read(candidate, 1), Math.Abs(Read(candidate, 2)), Math.Abs(Read(candidate, 3)), confidence, bestClass, profile.Kind == VisualPostprocessingKind.OrientedDetection ? Read(candidate, fields - 1) : 0));
        }
        IReadOnlyList<BoxCandidate> kept = Suppress(decoded.OrderByDescending(item => item.Score).Take(3000), 0.45, 50);
        foreach (BoxCandidate item in kept)
        {
            if (profile.Kind == VisualPostprocessingKind.OrientedDetection) DrawRotatedBox(overlays, item);
            else DrawCenterBox(overlays, item.CenterX, item.CenterY, item.Width, item.Height, item.Score, item.ClassId, "#ff5c5c");
        }
        labels.Add(name + " YOLO " + (attributeMajor ? "attribute-major" : "candidate-major") + " · " + kept.Count.ToString(CultureInfo.InvariantCulture) + " results");
        if (profile.Kind == VisualPostprocessingKind.Segmentation) labels.Add("mask coefficients require paired prototype output");
        return true;
    }

    private static IReadOnlyList<BoxCandidate> Suppress(IEnumerable<BoxCandidate> candidates, double threshold, int maximum)
    {
        var kept = new List<BoxCandidate>();
        foreach (BoxCandidate candidate in candidates)
        {
            if (kept.Any(existing => existing.ClassId == candidate.ClassId && IntersectionOverUnion(existing, candidate) > threshold)) continue;
            kept.Add(candidate);
            if (kept.Count == maximum) break;
        }
        return kept;
    }

    private static double IntersectionOverUnion(BoxCandidate first, BoxCandidate second)
    {
        double left = Math.Max(first.CenterX - first.Width / 2, second.CenterX - second.Width / 2);
        double top = Math.Max(first.CenterY - first.Height / 2, second.CenterY - second.Height / 2);
        double right = Math.Min(first.CenterX + first.Width / 2, second.CenterX + second.Width / 2);
        double bottom = Math.Min(first.CenterY + first.Height / 2, second.CenterY + second.Height / 2);
        double intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        double union = first.Width * first.Height + second.Width * second.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static void DrawCenterBox(List<string> overlays, double centerX, double centerY, double width, double height, double score, int classId, string color)
    {
        double left = X(centerX - width / 2), top = Y(centerY - height / 2), right = X(centerX + width / 2), bottom = Y(centerY + height / 2);
        overlays.Add("<rect x=\"" + F(Math.Min(left, right)) + "\" y=\"" + F(Math.Min(top, bottom)) + "\" width=\"" + F(Math.Abs(right - left)) + "\" height=\"" + F(Math.Abs(bottom - top)) + "\" fill=\"none\" stroke=\"" + color + "\" stroke-width=\"2\"/><text x=\"" + F(Math.Min(left, right) + 3) + "\" y=\"" + F(Math.Min(top, bottom) + 14) + "\" fill=\"" + color + "\" font-size=\"12\">class " + classId.ToString(CultureInfo.InvariantCulture) + " · " + F(score) + "</text>");
    }

    private static void DrawRotatedBox(List<string> overlays, BoxCandidate box)
    {
        double cx = X(box.CenterX), cy = Y(box.CenterY), width = Math.Abs(X(box.CenterX + box.Width / 2) - X(box.CenterX - box.Width / 2)), height = Math.Abs(Y(box.CenterY + box.Height / 2) - Y(box.CenterY - box.Height / 2));
        double radians = Math.Abs(box.Angle) > 2 * Math.PI ? box.Angle * Math.PI / 180 : box.Angle;
        double cos = Math.Cos(radians), sin = Math.Sin(radians);
        var points = new StringBuilder("<polygon points=\"");
        foreach ((double x, double y) in new[] { (-width / 2, -height / 2), (width / 2, -height / 2), (width / 2, height / 2), (-width / 2, height / 2) }) points.Append(F(cx + x * cos - y * sin)).Append(',').Append(F(cy + x * sin + y * cos)).Append(' ');
        overlays.Add(points.Append("\" fill=\"none\" stroke=\"#e67e22\" stroke-width=\"2\"/>").ToString());
    }

    private static bool IsMatrix(long[] shape, int count) => shape.Length >= 2 && shape[^1] >= 2 && shape[^2] >= 2 && count <= 4096;

    private static void SanitizeMaskValues(JsonNode node, string? propertyName)
    {
        if (node is JsonObject item)
        {
            string? outputName = item["name"]?.GetValue<string>();
            bool maskPayload = IsMaskName(propertyName) || IsMaskName(outputName);
            if (maskPayload && item.ContainsKey("values"))
            {
                item.Remove("values");
                item["maskValuesOmitted"] = true;
            }
            foreach (KeyValuePair<string, JsonNode?> child in item.ToArray())
                if (child.Value is not null) SanitizeMaskValues(child.Value, child.Key);
            return;
        }
        if (node is JsonArray array)
            foreach (JsonNode? child in array)
                if (child is not null) SanitizeMaskValues(child, propertyName);
    }

    private static bool IsMaskName(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && (value.Contains("mask", StringComparison.OrdinalIgnoreCase)
                || value.Contains("prototype", StringComparison.OrdinalIgnoreCase)
                || value.Contains("proto", StringComparison.OrdinalIgnoreCase)
                || value.Equals("map", StringComparison.OrdinalIgnoreCase)
                || value.Equals("alpha", StringComparison.OrdinalIgnoreCase));

    private static void Flatten(JsonElement value, List<double> result)
    {
        if (value.ValueKind == JsonValueKind.Array) { foreach (JsonElement item in value.EnumerateArray()) Flatten(item, result); return; }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)) result.Add(number);
    }
    private static int SafeDimension(long value, int fallback) => value is > 0 and <= 4096 ? (int)value : fallback;
    private static double X(double value) => value >= 0 && value <= 1.01 ? 20 + value * 600 : 20 + Math.Clamp(value, 0, 640) * 600 / 640;
    private static double Y(double value) => value >= 0 && value <= 1.01 ? 20 + value * 360 : 20 + Math.Clamp(value, 0, 640) * 360 / 640;
    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private sealed record CanvasLayout(double X, double Y, double Width, double Height, double SourceWidth, double SourceHeight)
    {
        public static CanvasLayout Default { get; } = new(20, 20, 600, 360, 640, 640);
        public double ScaleX => Width / SourceWidth;
        public double ScaleY => Height / SourceHeight;
        public double MapX(double value) => X + Math.Clamp(value, 0, SourceWidth) * ScaleX;
        public double MapY(double value) => Y + Math.Clamp(value, 0, SourceHeight) * ScaleY;

        public static CanvasLayout Fit(double sourceWidth, double sourceHeight)
        {
            sourceWidth = double.IsFinite(sourceWidth) && sourceWidth > 0 ? sourceWidth : 640;
            sourceHeight = double.IsFinite(sourceHeight) && sourceHeight > 0 ? sourceHeight : 640;
            double scale = Math.Min(600 / sourceWidth, 360 / sourceHeight);
            double width = sourceWidth * scale;
            double height = sourceHeight * scale;
            return new CanvasLayout(20 + (600 - width) / 2, 20 + (360 - height) / 2, width, height, sourceWidth, sourceHeight);
        }
    }
    private sealed record BoxCandidate(double CenterX, double CenterY, double Width, double Height, double Score, int ClassId, double Angle, int SourceIndex = -1);
    private sealed record OutputData(string Name, long[] Shape, IReadOnlyList<double> Values);
}
