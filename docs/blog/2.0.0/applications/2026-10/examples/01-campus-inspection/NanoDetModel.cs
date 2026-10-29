using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

public sealed record PixelBox(float X, float Y, float Width, float Height);
public sealed record DetectedObject(int ClassId, string Label, float Score, PixelBox Box);
public sealed record ObjectDetections(IReadOnlyList<DetectedObject> Objects);
internal sealed record NanoDetLevel(int Stride, int Rows, int Columns, string Scores, string Distances);

internal static class NanoDetModel
{
    public static readonly VisualTaskId Task = new("blog-nanodet-detection");
    public static readonly string[] Labels = (
        "person,bicycle,car,motorcycle,airplane,bus,train,truck,boat,traffic light,fire hydrant," +
        "stop sign,parking meter,bench,bird,cat,dog,horse,sheep,cow,elephant,bear,zebra,giraffe," +
        "backpack,umbrella,handbag,tie,suitcase,frisbee,skis,snowboard,sports ball,kite,baseball bat," +
        "baseball glove,skateboard,surfboard,tennis racket,bottle,wine glass,cup,fork,knife,spoon," +
        "bowl,banana,apple,sandwich,orange,broccoli,carrot,hot dog,pizza,donut,cake,chair,couch," +
        "potted plant,bed,dining table,toilet,tv,laptop,mouse,remote,keyboard,cell phone,microwave," +
        "oven,toaster,sink,refrigerator,book,clock,vase,scissors,teddy bear,hair drier,toothbrush").Split(',');

    public static VisualModelDefinition<OpenCvDecodedRoiImage, ObjectDetections> Create(
        float threshold, float nmsThreshold)
    {
        // 以实际 ONNX 图为准。该 Zoo 导出是三个尺度、六个输出，不能照搬其他训练配置的第四尺度。
        NanoDetLevel[] levels = [new(8, 52, 52, "792", "795"),
            new(16, 26, 26, "814", "817"), new(32, 13, 13, "836", "839")];

        // Zoo demo.py 先 BGR→RGB，再居中黑边 Letterbox；nanodet.py 本身不完成这两步。
        // 使用已发布适配器提供的 Linear 插值（官方 demo 为 Area），下文独立参考采用相同插值。
        var processing = new VisualPreprocessingOptions(
            new VisualSize(416, 416), VisualResizeMode.Letterbox, VisualColorOrder.Rgb,
            VisualNormalizationOptions.MeanStandardDeviation(
                [103.53f, 116.28f, 123.675f], [57.375f, 57.12f, 58.395f]),
            VisualTensorLayout.Nchw, paddingColor: VisualRgbColor.Black,
            dimensionRounding: VisualDimensionRounding.Floor, interpolation: VisualInterpolationMode.Linear);
        var builder = new VisualModelDefinitionBuilder<OpenCvDecodedRoiImage, ObjectDetections>(
                "blog.nanodet-plus-416", new ModelId("blog/nanodet-plus-416"), Task, "1", "onnx")
            .WithInput("input.1", TensorElementType.Float32, new TensorShape(1, 3, 416, 416), VisualTensorLayout.Nchw)
            .WithPreprocessing(processing)
            .WithInputPreprocessor((image, profile, ct) => image.PrepareRectangle(
                new RectangleRoiGeometry(new RectangleF(0, 0, image.SourceSize.Width, image.SourceSize.Height)),
                profile.Input.Name, profile.Preprocessing!.ToOpenCvOptions(), ct))
            .WithDecoder(new NanoDetDecoder(levels, threshold, nmsThreshold));
        foreach (var level in levels)
        {
            builder.AddOutput(level.Scores, TensorElementType.Float32, new TensorShape(1, level.Rows * level.Columns, 80));
            builder.AddOutput(level.Distances, TensorElementType.Float32, new TensorShape(1, level.Rows * level.Columns, 32));
        }
        return builder.Build();
    }
}

internal sealed class NanoDetDecoder(
    IReadOnlyList<NanoDetLevel> levels, float threshold, float nmsThreshold)
    : IVisualResultDecoder<ObjectDetections>
{
    public VisualTaskId Task => NanoDetModel.Task;

    public ObjectDetections Decode(VisualDecodeContext context)
    {
        var proposals = new List<DetectedObject>();
        foreach (var level in levels)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            float[] scores = (float[])context.GetOutput<float>(level.Scores).Buffer;
            float[] distances = (float[])context.GetOutput<float>(level.Distances).Buffer;
            if (scores.Any(x => !float.IsFinite(x) || x < 0 || x > 1) ||
                distances.Any(x => !float.IsFinite(x)))
                throw new InvalidDataException("NanoDet 输出非法；不把 NaN 或错误输出格式变成检测框。");
            for (int anchor = 0; anchor < level.Rows * level.Columns; anchor++)
            {
                int classId = 0, offset = anchor * 80;
                for (int c = 1; c < 80; c++)
                    if (scores[offset + c] > scores[offset + classId]) classId = c;
                float score = scores[offset + classId];
                if (score < threshold) continue;

                // DFL：每条边有 8 个离散 logit，稳定 Softmax 后求距离期望。
                float left = Distance(distances, anchor * 32) * level.Stride;
                float top = Distance(distances, anchor * 32 + 8) * level.Stride;
                float right = Distance(distances, anchor * 32 + 16) * level.Stride;
                float bottom = Distance(distances, anchor * 32 + 24) * level.Stride;
                float cx = anchor % level.Columns * level.Stride + .5f * (level.Stride - 1);
                float cy = anchor / level.Columns * level.Stride + .5f * (level.Stride - 1);
                float x1 = Math.Clamp(cx - left, 0, 416), y1 = Math.Clamp(cy - top, 0, 416);
                float x2 = Math.Clamp(cx + right, 0, 416), y2 = Math.Clamp(cy + bottom, 0, 416);
                var source = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(
                    new RectangleF(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1))));
                if (source.Width < 1 || source.Height < 1) continue;
                proposals.Add(new(classId, NanoDetModel.Labels[classId], score,
                    new(source.X, source.Y, source.Width, source.Height)));
            }
        }
        // 按类 NMS。保留不同类别的重叠目标；不会把行人与公交车互相抑制。
        var selected = new List<DetectedObject>();
        foreach (var proposal in proposals.OrderByDescending(x => x.Score))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (selected.Any(x => x.ClassId == proposal.ClassId && IoU(x.Box, proposal.Box) > nmsThreshold)) continue;
            selected.Add(proposal);
            if (selected.Count == 100) break;
        }
        return new(selected.AsReadOnly());
    }

    private static float Distance(float[] values, int offset)
    {
        double max = values[offset], sum = 0, weighted = 0;
        for (int i = 1; i < 8; i++) max = Math.Max(max, values[offset + i]);
        for (int i = 0; i < 8; i++)
        {
            double weight = Math.Exp(values[offset + i] - max);
            sum += weight; weighted += i * weight;
        }
        return (float)(weighted / sum);
    }

    private static float IoU(PixelBox a, PixelBox b)
    {
        float area = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X)) *
            Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        return area / Math.Max(float.Epsilon, a.Width * a.Height + b.Width * b.Height - area);
    }
}
