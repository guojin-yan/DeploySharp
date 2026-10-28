using System.Runtime.InteropServices;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using OrientedDetectionResult = JYPPX.DeploySharp.Visual.OrientedDetectionResult;

internal static class ResultWriter
{
    private static readonly Scalar[] Colors =
        [new(90, 210, 60), new(240, 150, 40), new(50, 170, 250), new(210, 70, 220), new(200, 220, 50)];

    public static object Classification(string path, string directory, ClassificationResult result)
    {
        using Mat canvas = ImageCodecs.ImRead(path);
        int line = 0;
        foreach (var item in result.Predictions)
        {
            string text = FormattableString.Invariant($"#{++line} {item.Label} {item.Score:F4}");
            Console.WriteLine(text);
            Caption(canvas, text, 12, 34 + (line - 1) * 31, .7);
        }
        Save(Path.Combine(directory, "result.jpg"), canvas);
        return new { predictions = result.Predictions.Select(p => new { p.Index, p.Label, p.Score }) };
    }

    public static object Segmentation(string path, string directory, InstanceSegmentationResult result)
    {
        using Mat canvas = ImageCodecs.ImRead(path);
        var records = new List<object>();
        IntPtr canvasData = canvas.Data;
        int canvasStep = checked((int)canvas.Step.ToUInt64());
        for (int index = 0; index < result.Instances.Count; index++)
        {
            var item = result.Instances[index];
            InstanceBinaryMask mask = item.Mask;
            if (mask.CoordinateSpace != InstanceMaskCoordinateSpace.SourceImage ||
                mask.OriginX != 0 || mask.OriginY != 0 || mask.Width != canvas.Cols || mask.Height != canvas.Rows)
                throw new InvalidDataException("本文绘图器要求完整源图坐标掩码。");
            byte[] pixels = mask.ToArray(); // 每实例只取一次，避免每像素拷贝整个掩码。
            Scalar color = Colors[index % Colors.Length];
            byte[] row = new byte[canvas.Cols * 3];
            byte[] maskRow = new byte[canvas.Cols];
            using var maskImage = new Mat(mask.Height, mask.Width, MatType.CV_8UC1);
            IntPtr maskData = maskImage.Data;
            int maskStep = checked((int)maskImage.Step.ToUInt64());
            for (int y = 0; y < canvas.Rows; y++)
            {
                IntPtr canvasRow = IntPtr.Add(canvasData, checked(y * canvasStep));
                Marshal.Copy(canvasRow, row, 0, row.Length);
                for (int x = 0; x < canvas.Cols; x++)
                {
                    bool foreground = pixels[y * mask.Width + x] != 0;
                    maskRow[x] = foreground ? (byte)255 : (byte)0;
                    if (!foreground) continue;
                    int offset = x * 3;
                    row[offset] = (byte)(row[offset] * .6 + color.V0 * .4);
                    row[offset + 1] = (byte)(row[offset + 1] * .6 + color.V1 * .4);
                    row[offset + 2] = (byte)(row[offset + 2] * .6 + color.V2 * .4);
                }
                Marshal.Copy(row, 0, canvasRow, row.Length);
                Marshal.Copy(maskRow, 0, IntPtr.Add(maskData, checked(y * maskStep)), maskRow.Length);
            }
            string file = $"mask-{index:D2}.png";
            Save(Path.Combine(directory, file), maskImage);
            records.Add(new { item.ClassIndex, item.Label, item.Score,
                box = Box(item.BoundingBox), mask = file, mask.Width, mask.Height,
                coordinateSpace = mask.CoordinateSpace.ToString(), mask.OriginX, mask.OriginY,
                foregroundPixels = pixels.Count(p => p != 0) });
        }
        // 掩码叠色全部完成后再画文字，避免后面的掩码覆盖说明。
        for (int index = 0; index < result.Instances.Count; index++)
        {
            var item = result.Instances[index];
            var box = item.BoundingBox;
            Drawing.Rectangle(canvas, new Point((int)box.X, (int)box.Y),
                new Point((int)box.Right, (int)box.Bottom), Colors[index % Colors.Length], 2);
            Caption(canvas, FormattableString.Invariant($"{item.Label} {item.Score:F3}"),
                (int)box.X, (int)box.Y - 7, .65);
        }
        Save(Path.Combine(directory, "result.jpg"), canvas);
        Console.WriteLine($"segment: instances={result.Instances.Count}; masks={records.Count}");
        return new { source = result.SourceSize, count = records.Count, instances = records };
    }

    public static object Pose(string path, string directory, PoseEstimationResult result)
    {
        using Mat canvas = ImageCodecs.ImRead(path);
        for (int index = 0; index < result.Instances.Count; index++)
        {
            var item = result.Instances[index];
            var points = item.Keypoints.ToDictionary(p => p.Index);
            Scalar color = Colors[index % Colors.Length];
            foreach (var edge in result.Topology.Edges)
            {
                var a = points[edge.FirstKeypointIndex];
                var b = points[edge.SecondKeypointIndex];
                if (a.IsValid && b.IsValid)
                    Drawing.Line(canvas, Point(a.Point), Point(b.Point), color, 3);
            }
            foreach (var p in item.Keypoints.Where(p => p.IsValid))
                Drawing.Circle(canvas, Point(p.Point), 4, new Scalar(60, 80, 255), -1);
            if (item.BoundingBox is { } box)
                Caption(canvas, FormattableString.Invariant($"person {item.Score:F3}"),
                    (int)box.X, (int)box.Y - 8, .6);
        }
        Save(Path.Combine(directory, "result.jpg"), canvas);
        Console.WriteLine($"pose: people={result.Instances.Count}; valid_keypoints={result.Instances.Sum(i => i.Keypoints.Count(p => p.IsValid))}");
        return new { source = result.SourceSize, count = result.Instances.Count,
            instances = result.Instances.Select(i => new { i.Score,
                box = i.BoundingBox is { } b ? Box(b) : null,
                keypoints = i.Keypoints.Select(p => new { p.Index, p.Point.X, p.Point.Y, p.Score, p.IsValid }) }) };
    }

    public static object Obb(string path, string directory, OrientedDetectionResult result)
    {
        using Mat canvas = ImageCodecs.ImRead(path);
        foreach (var item in result.Detections)
        {
            var vertices = item.Quadrilateral.Vertices;
            for (int i = 0; i < vertices.Count; i++)
                Drawing.Line(canvas, Point(vertices[i]), Point(vertices[(i + 1) % vertices.Count]),
                    Colors[item.ClassIndex % Colors.Length], 2);
            var first = vertices.OrderBy(p => p.Y).First();
            Caption(canvas, FormattableString.Invariant($"{item.Label} {item.Score:F2}"),
                (int)first.X, (int)first.Y - 4, .42);
        }
        Save(Path.Combine(directory, "result.jpg"), canvas);
        Console.WriteLine($"obb: detections={result.Detections.Count}");
        return new { source = result.SourceSize, count = result.Detections.Count,
            detections = result.Detections.Select(i => new { i.ClassIndex, i.Label, i.Score,
                vertices = i.Quadrilateral.Vertices.Select(p => new { p.X, p.Y }),
                i.AngleRadiansCounterClockwise, i.HasExactRotatedRectangle }) };
    }

    private static object Box(JYPPX.DeploySharp.Geometry.RectangleF box)
        => new { box.X, box.Y, box.Width, box.Height };
    private static Point Point(JYPPX.DeploySharp.Geometry.PointF point)
        => new((int)Math.Round(point.X), (int)Math.Round(point.Y));
    private static void Caption(Mat canvas, string text, int x, int y, double scale)
    {
        Size size = Drawing.GetTextSize(text, HersheyFonts.HersheySimplex, scale, 1, out _);
        var point = new Point(Math.Clamp(x, 3, Math.Max(3, canvas.Cols - size.Width - 8)),
            Math.Clamp(y, size.Height + 5, Math.Max(size.Height + 5, canvas.Rows - 5)));
        Drawing.PutText(canvas, text, point, HersheyFonts.HersheySimplex, scale, new Scalar(10, 15, 25), 4);
        Drawing.PutText(canvas, text, point, HersheyFonts.HersheySimplex, scale, new Scalar(250, 250, 250), 1);
    }
    private static void Save(string path, Mat image)
    {
        if (!ImageCodecs.ImWrite(path, image)) throw new IOException("保存图片失败：" + path);
    }
}
