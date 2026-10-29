using JYPPX.OpenCvSharp.Core;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

internal static class PreviewWriter
{
    public static void SaveResult(string source, string directory, SceneAnalysisResult report)
    {
        using var original = ImageCodecs.ImRead(source);
        using var canvas = original.Clone();
        for (int zoneIndex = 0; zoneIndex < report.Camera.Zones.Length; zoneIndex++)
        {
            var zone = report.Camera.Zones[zoneIndex];
            var points = zone.Polygon.Select(p => new Point((int)(p[0] * report.Width), (int)(p[1] * report.Height))).ToArray();
            for (int i = 0; i < points.Length; i++)
                Drawing.Line(canvas, points[i], points[(i + 1) % points.Length], new Scalar(50, 180, 240), 3);
            string zoneTag = zone.Id + ": " + report.Zones[zoneIndex].Count;
            var zonePoint = new Point(12, 26 + zoneIndex * 28);
            Drawing.Rectangle(canvas, new Rect(7, 6 + zoneIndex * 28, Math.Min(report.Width - 7, 320), 25), new Scalar(35, 38, 42), -1);
            Drawing.PutText(canvas, zoneTag, zonePoint, JYPPX.OpenCvSharp.ImgProc.HersheyFonts.HersheySimplex,
                .65, new Scalar(50, 180, 240), 1);
        }
        foreach (var obj in report.Objects)
        {
            var b = obj.Detection.Box;
            int left = Math.Clamp((int)Math.Floor(b.X), 0, report.Width - 1);
            int top = Math.Clamp((int)Math.Floor(b.Y), 0, report.Height - 1);
            int right = Math.Clamp((int)Math.Ceiling(b.X + b.Width), left + 1, report.Width);
            int bottom = Math.Clamp((int)Math.Ceiling(b.Y + b.Height), top + 1, report.Height);
            var color = obj.ZoneIds.Length > 0 ? new Scalar(175, 196, 50) : new Scalar(170, 170, 170);
            Drawing.Rectangle(canvas, new Rect(left, top, right - left, bottom - top), color, 3);
            Drawing.Circle(canvas, new Point((left + right) / 2, bottom - 1), 5, color, -1);
            string tag = obj.ZoneIds.Length > 0 ? $"#{obj.Id} {obj.Detection.Label}" : $"#{obj.Id}";
            if (obj.Semantic is { } semantic) tag += " / " + semantic.Matches[0].Key + (semantic.RequiresReview ? " ?" : "");
            var font = JYPPX.OpenCvSharp.ImgProc.HersheyFonts.HersheySimplex;
            var textSize = Drawing.GetTextSize(tag, font, .55, 1, out _);
            var point = new Point(Math.Clamp(left + 2, 2, Math.Max(2, report.Width - textSize.Width - 4)),
                Math.Max(textSize.Height + 4, top - 8));
            Drawing.Rectangle(canvas, new Rect(point.X - 1, point.Y - textSize.Height - 2,
                Math.Min(textSize.Width + 3, report.Width - point.X), textSize.Height + 4), new Scalar(35, 38, 42), -1);
            Drawing.PutText(canvas, tag, point, JYPPX.OpenCvSharp.ImgProc.HersheyFonts.HersheySimplex, .55, new Scalar(250, 250, 250), 1);
            if (obj.Semantic is { } match)
            {
                var c = match.Crop;
                using var roi = original.SubMat(new Rect((int)c.X, (int)c.Y, (int)c.Width, (int)c.Height));
                Save(Path.Combine(directory, $"roi-{obj.Id:D2}.jpg"), roi);
            }
        }
        Save(Path.Combine(directory, "scene.jpg"), canvas);
    }

    private static void Save(string path, Mat image)
    {
        if (!ImageCodecs.ImWrite(path, image)) throw new IOException("保存图片失败：" + path);
    }
}
