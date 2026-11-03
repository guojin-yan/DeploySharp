using JYPPX.DeploySharp.Visual;
using JYPPX.OpenCvSharp.Core;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using HersheyFonts = JYPPX.OpenCvSharp.ImgProc.HersheyFonts;

internal static class RoiDrawing
{
    public static void Save(string source, string path, VisualRoiSnapshot snapshot, IReadOnlyList<DetectionRow> detections)
    {
        using Mat image = ImageCodecs.ImRead(source);
        if (image.Empty) throw new IOException("Cannot read " + source);
        int thickness = Math.Max(2, image.Cols / 640);
        double font = Math.Max(.45, image.Cols / 2400d);
        foreach (VisualRoi roi in snapshot.Rois.Where(r => r.Enabled))
        {
            IVisualRoiGeometry geometry = snapshot.Resolve(roi);
            Scalar color = roi.InclusionMode == RoiInclusionMode.Exclude ? new Scalar(85, 100, 245)
                : roi.ExecutionMode == RoiExecutionMode.CropAndInfer ? new Scalar(245, 175, 80) : new Scalar(150, 225, 70);
            var points = geometry.Points;
            if (geometry is MaskRoiGeometry mask) ShadeMask(image, mask);
            else for (int i = 0; i < points.Count; i++)
            {
                var p = points[i]; var q = points[(i + 1) % points.Count];
                Drawing.Line(image, new Point((int)p.X, (int)p.Y), new Point((int)q.X, (int)q.Y), color, thickness);
            }
            var b = geometry.Bounds;
            Label(image, roi.Id, Math.Clamp((int)b.X + 4, 4, image.Cols - 180), (int)b.Y + 24, color, font);
        }
        foreach (DetectionRow d in detections)
        {
            var color = new Scalar(80, 220, 245);
            Drawing.Rectangle(image, new Point((int)d.X, (int)d.Y), new Point((int)(d.X + d.Width), (int)(d.Y + d.Height)), color, thickness);
            Label(image, $"{d.Label} {d.Score:F2}", (int)d.X, Math.Max(20, (int)d.Y - 7), color, font);
        }
        Label(image, $"{detections.Count} detections | snapshot {snapshot.Version}", 14, image.Rows - 18, new Scalar(255, 255, 255), font);
        if (!ImageCodecs.ImWrite(path, image)) throw new IOException("Cannot save " + path);
    }

    private static unsafe void ShadeMask(Mat image, MaskRoiGeometry mask)
    {
        byte[] values = mask.ToArray();
        int stride = checked((int)image.Step.ToUInt64());
        byte* data = (byte*)image.Data;
        for (int y = 0; y < image.Rows; y++) for (int x = 0; x < image.Cols; x++)
        {
            if (values[y * image.Cols + x] == 0) continue;
            byte* pixel = data + y * stride + x * 3;
            pixel[0] = (byte)(pixel[0] * .7f + 245 * .3f);
            pixel[1] = (byte)(pixel[1] * .7f + 175 * .3f);
            pixel[2] = (byte)(pixel[2] * .7f + 80 * .3f);
        }
    }

    private static void Label(Mat image, string text, int x, int y, Scalar color, double font)
    {
        Drawing.PutText(image, text, new Point(x, y), HersheyFonts.HersheySimplex, font, new Scalar(15, 25, 32), 4);
        Drawing.PutText(image, text, new Point(x, y), HersheyFonts.HersheySimplex, font, color, 1);
    }
}
