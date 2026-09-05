using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Web;

/// <summary>Renders generic, contract-driven visual overlays without pretending to decode an unknown model format.</summary>
internal static class VisionResultRenderer
{
    public static string Render(string originalImage, string? outputJson, string? task)
    {
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"640\" height=\"420\" viewBox=\"0 0 640 420\"><rect width=\"640\" height=\"420\" fill=\"#eef1f7\"/><image href=\"");
        svg.Append(SecurityElement.Escape(originalImage));
        svg.Append("\" x=\"20\" y=\"20\" width=\"600\" height=\"360\" preserveAspectRatio=\"xMidYMid meet\"/>");
        var overlays = new List<string>();
        var labels = new List<string>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(outputJson ?? "{}");
            JsonElement root = document.RootElement;
            JsonElement outputs = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("outputs", out JsonElement named) ? named : root;
            if (outputs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement output in outputs.EnumerateArray()) ParseOutput(output, overlays, labels);
            }
        }
        catch (JsonException) { labels.Add("输出不是可解析的 tensor JSON"); }
        if (overlays.Count == 0) labels.Add("未发现可绘制的后处理契约");
        string title = string.IsNullOrWhiteSpace(task) ? "视觉结果" : task!;
        svg.Append(string.Join(string.Empty, overlays));
        svg.Append("<rect x=\"20\" y=\"380\" width=\"600\" height=\"30\" fill=\"#24345f\"/><text x=\"32\" y=\"400\" fill=\"white\" font-family=\"Segoe UI\" font-size=\"13\">");
        svg.Append(SecurityElement.Escape(title + " · " + string.Join("；", labels.Take(3))));
        svg.Append("</text></svg>");
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString()));
    }

    private static void ParseOutput(JsonElement output, List<string> overlays, List<string> labels)
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
        if (lower.Contains("keypoint") || lower.Contains("landmark") || lower.Contains("pose"))
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
        int widthPerRow = shape.Length > 0 && shape[^1] is >= 4 and <= 7 ? (int)shape[^1] : values.Count % 6 == 0 ? 6 : 0;
        if (widthPerRow is 6 or 7)
        {
            for (int index = 0; index + widthPerRow <= values.Count; index += widthPerRow)
            {
                int coordinateOffset = widthPerRow == 7 ? 1 : 0;
                double score = values[index + coordinateOffset + 4] > 1 ? values[index + coordinateOffset + 4] / 100 : values[index + coordinateOffset + 4];
                if (score < .15) continue;
                double x1 = X(values[index + coordinateOffset]); double y1 = Y(values[index + coordinateOffset + 1]);
                double x2 = X(values[index + coordinateOffset + 2]); double y2 = Y(values[index + coordinateOffset + 3]);
                overlays.Add("<rect x=\"" + F(Math.Min(x1, x2)) + "\" y=\"" + F(Math.Min(y1, y2)) + "\" width=\"" + F(Math.Abs(x2 - x1)) + "\" height=\"" + F(Math.Abs(y2 - y1)) + "\" fill=\"none\" stroke=\"#ff5c5c\" stroke-width=\"2\"/><text x=\"" + F(Math.Min(x1, x2) + 3) + "\" y=\"" + F(Math.Min(y1, y2) + 14) + "\" fill=\"#ff5c5c\" font-size=\"12\">" + F(score) + "</text>");
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

    private static bool IsMatrix(long[] shape, int count) => shape.Length >= 2 && shape[^1] >= 2 && shape[^2] >= 2 && count <= 4096;
    private static void Flatten(JsonElement value, List<double> result)
    {
        if (value.ValueKind == JsonValueKind.Array) { foreach (JsonElement item in value.EnumerateArray()) Flatten(item, result); return; }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number)) result.Add(number);
    }
    private static int SafeDimension(long value, int fallback) => value is > 0 and <= 4096 ? (int)value : fallback;
    private static double X(double value) => value >= 0 && value <= 1.01 ? 20 + value * 600 : 20 + Math.Clamp(value, 0, 640) * 600 / 640;
    private static double Y(double value) => value >= 0 && value <= 1.01 ? 20 + value * 360 : 20 + Math.Clamp(value, 0, 640) * 360 / 640;
    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
