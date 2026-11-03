using System.Text.Json;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using PointF = JYPPX.DeploySharp.Geometry.PointF;
using SizeF = JYPPX.DeploySharp.Geometry.SizeF;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

internal static class RoiGeometryShowcase
{
    public static async Task RunAsync(VisualPipeline pipeline, string[] files, string output,
        OpenCvPreprocessOptions options, CancellationToken token)
    {
        string file = files.FirstOrDefault(f => Path.GetFileName(f) == "courtyard-04.jpg") ?? files[0];
        var factory = new OpenCvVisualInputFactory();
        using var decoded = factory.DecodeForRois(OpenCvImageSource.FromFile(file), token);
        VisualSize size = decoded.SourceSize;
        float w = size.Width, h = size.Height;
        var polygon = new PolygonRoiGeometry(new[] {
            new PointF(w*.25f,h*.25f), new PointF(w*.80f,h*.30f), new PointF(w*.92f,h*.65f),
            new PointF(w*.53f,h*.70f), new PointF(w*.23f,h*.48f) });
        var quad = new PolygonRoiGeometry(new[] {
            new PointF(w*.28f,h*.24f), new PointF(w*.87f,h*.31f),
            new PointF(w*.82f,h*.69f), new PointF(w*.23f,h*.61f) });
        byte[] pixels = new byte[size.Width * size.Height];
        // The binary ROI is generated from known geometry, not from a model segmentation prediction.
        for (int y = 0; y < size.Height; y++)
            for (int x = 0; x < size.Width; x++)
                if (polygon.Contains(new PointF(x+.5f,y+.5f)) &&
                    !(x > w*.51f && x < w*.57f && y > h*.30f && y < h*.46f)) pixels[y*size.Width+x] = 255;
        var mask = new MaskRoiGeometry(size, pixels);
        var rois = new[] {
            new VisualRoi("rectangle", new RectangleRoiGeometry(new RectangleF(w*.23f,h*.24f,w*.69f,h*.47f)), executionMode:RoiExecutionMode.CropAndInfer),
            new VisualRoi("polygon", polygon, executionMode:RoiExecutionMode.CropAndInfer),
            new VisualRoi("rotated", new RotatedRectangleRoiGeometry(new PointF(w*.58f,h*.47f),new SizeF(w*.66f,h*.40f),12), executionMode:RoiExecutionMode.CropAndInfer),
            new VisualRoi("mask", mask, executionMode:RoiExecutionMode.CropAndInfer),
            new VisualRoi("perspective", quad, executionMode:RoiExecutionMode.CropAndInfer) };
        var snapshot = new VisualRoiSnapshot(size, rois);
        var runner = new VisualRoiRunner(pipeline);
        var batch = await runner.RunCropAsync(snapshot, (roi,g,ct) => {
            var prepared = roi.Id == "perspective" ? decoded.PreparePerspective(g.Points,"images",options,ct)
                : decoded.Prepare(g,"images",options,ct);
            try
            {
                SaveTensor(prepared, Path.Combine(output,roi.Id+"-input.png"));
                return Task.FromResult(prepared);
            }
            catch { prepared.Dispose(); throw; }
        }, new RoiExecutionOptions(maximumRois:8,prefetch:1,maximumPreparedPixels:16_000_000), token);
        var summaries = new List<object>();
        foreach (var item in batch.Succeeded)
        {
            var detections = (DetectionResult)item.Result!.Value;
            var rows = detections.Detections.Select(d=>new DetectionRow(d.Label.Index,d.Label.Label,d.Label.Score,
                d.Box.X,d.Box.Y,d.Box.Width,d.Box.Height,new[]{item.Roi.Id})).ToArray();
            foreach (var d in rows)
                if (!float.IsFinite(d.X+d.Y+d.Width+d.Height) || d.X < -.01f || d.Y < -.01f ||
                    d.X+d.Width > w+.01f || d.Y+d.Height > h+.01f) throw new InvalidDataException("ROI result outside source coordinates.");
            RoiDrawing.Save(file,Path.Combine(output,item.Roi.Id+"-source.jpg"),new VisualRoiSnapshot(size,new[]{item.Roi}),rows);
            summaries.Add(new { id=item.Roi.Id, geometry=item.Geometry.Kind.ToString(), detections=rows });
            Console.WriteLine($"geometry {item.Roi.Id}: {rows.Length} detections, source={size.Width}x{size.Height}");
        }
        using var fullInput = decoded.PrepareRectangle(new RectangleRoiGeometry(new RectangleF(0,0,w,h)),"images",options,token);
        var full = (DetectionResult)pipeline.Run(fullInput).Value;
        var rules = new List<object>();
        foreach(var mode in new[] {RoiHitTestMode.CenterPoint,RoiHitTestMode.AnyIntersection,
            RoiHitTestMode.IntersectionOverResult,RoiHitTestMode.IntersectionOverRoi,RoiHitTestMode.IoU})
        {
            var rule = new VisualRoi("rule",polygon,hitTestMode:mode,hitThreshold:.5f,classFilter:new[]{0});
            rules.Add(new { mode=mode.ToString(), count=VisualRoiDetectionFilter.Filter(full,new VisualRoiSnapshot(size,new[]{rule}),VisualTaskId.ObjectDetection).Detections.Count });
        }
        foreach(var rule in new[] {
            new VisualRoi("person-only",polygon,classFilter:new[]{0}),
            new VisualRoi("high-confidence",polygon,classFilter:new[]{0},confidenceOverride:.9f),
            new VisualRoi("rectangle-margin-20",rois[0].Geometry,classFilter:new[]{0},margin:20),
            new VisualRoi("disabled",polygon,enabled:false) })
            rules.Add(new { mode=rule.Id, count=VisualRoiDetectionFilter.Filter(full,new VisualRoiSnapshot(size,new[]{rule}),VisualTaskId.ObjectDetection).Detections.Count });
        bool budgetRejected=false, preparationAttempted=false;
        try { await runner.RunCropAsync(snapshot,(_,g,ct) => {
            preparationAttempted=true;
            return Task.FromResult(decoded.Prepare(g,"images",options,ct));
        },
            new RoiExecutionOptions(maximumPreparedPixels:1),token); }
        catch (VisualException error) when (error.ErrorCode == VisualErrorCodes.InputInvalid) { budgetRejected=true; }
        if(!budgetRejected || preparationAttempted) throw new InvalidOperationException("Pixel budget should reject this request before preparation.");
        var dgn=batch.Diagnostics;
        File.WriteAllText(Path.Combine(output,"geometry.json"),JsonSerializer.Serialize(new {
            image=Path.GetFileName(file), source=new{width=size.Width,height=size.Height}, cases=summaries, rules,
            diagnostics=new{dgn.SelectedRoiCount,dgn.PreparedInputCount,dgn.InferenceCallCount,dgn.SucceededResultCount,
                dgn.FailedResultCount,dgn.UsedTrueBatch,dgn.PreparedPixelCount}, budgetRejected },
            new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));
        File.WriteAllText(Path.Combine(output,"geometry.snapshot.json"),VisualRoiJsonSerializer.Serialize(snapshot));
    }

    private static unsafe void SaveTensor(PreparedVisualInput input,string path)
    {
        if(input.Layout!=VisualTensorLayout.Nchw || input.Tensor.Buffer is not float[] values || input.BatchSize!=1)
            throw new InvalidDataException("Preview expects FP32 RGB NCHW batch one with divide-255 normalization.");
        int w=input.ModelSize.Width,h=input.ModelSize.Height,plane=w*h;
        using var image=new Mat(h,w,MatType.CV_8UC3);
        int stride=checked((int)image.Step.ToUInt64());
        byte* data=(byte*)image.Data;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int p=y*w+x;byte* pixel=data+y*stride+x*3;
            pixel[2]=(byte)Math.Clamp((int)Math.Round(values[p]*255),0,255);
            pixel[1]=(byte)Math.Clamp((int)Math.Round(values[plane+p]*255),0,255);
            pixel[0]=(byte)Math.Clamp((int)Math.Round(values[2*plane+p]*255),0,255);
        }
        if(!ImageCodecs.ImWrite(path,image))throw new IOException("Cannot save tensor preview.");
    }
}
