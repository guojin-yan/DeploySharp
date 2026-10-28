using System.Text.Json;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;

// 本文公开模型的输出配置。换导出规格时，需同步修改尺寸、输出布局与标签。
internal static class Profiles
{
    public static VisualModelProfile Create(string task, string? labelsPath = null)
    {
        var id = new ModelId("blog/" + task);
        if (task == "classify")
        {
            string path = labelsPath ?? Path.Combine(AppContext.BaseDirectory, "imagenet-labels.json");
            string[] labels = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path))
                ?? throw new InvalidDataException("类别表不能为空。");
            if (labels.Length != 1000)
                throw new InvalidDataException("本文分类图输出 1000 类，类别表必须与输出索引一致。");
            return new VisualModelProfile(
                "blog.classify", id, VisualTaskId.ImageClassification, "1", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32,
                    new TensorShape(1, 3, 224, 224), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("output0", TensorElementType.Float32,
                    new TensorShape(1, 1000)) },
                labels.Select((name, index) => new VisualLabel(index, name)),
                new ClassificationDecoder("output0", ClassificationScoreMode.Probabilities, topK: 5),
                preprocessing: new VisualPreprocessingOptions(new VisualSize(224, 224),
                    VisualResizeMode.CenterCrop, VisualColorOrder.Rgb,
                    VisualNormalizationOptions.Scale(255f)));
        }

        // 2.0.0 的多任务工厂接受导出记录。这里保留默认 Release 图的真实记录，
        // 只取 VisualProfile 描述输入输出；Program 用本地 ModelArtifact 加载，不做文件摘要校验。
        const string sourceRevision = "6f6158be448c73471c000cf41db5cd9169300ed9";
        const string exporter = "8.4.0-end2end";
        var options = new YoloPackedProfileOptions(
            opset: 19, candidateCount: 300,
            modelSize: task == "obb" ? new VisualSize(1024, 1024) : new VisualSize(640, 640),
            decoderOptions: new YoloPackedDecoderOptions(
                scoreThreshold: .25f, maskThreshold: .5f, keypointThreshold: .5f,
                maximumWorkspaceBytes: 512L * 1024 * 1024));
        return task switch
        {
            "segment" => YoloMultiTaskProfiles.CreateInstanceSegmentation(
                YoloDetectionFamily.YoloV26, id,
                "79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f",
                YoloLabelSets.Coco80, sourceRevision, exporter, options).VisualProfile,
            "pose" => YoloMultiTaskProfiles.CreatePose(
                YoloDetectionFamily.YoloV26, id,
                "55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc",
                sourceRevision, exporter, options).VisualProfile,
            "obb" => YoloMultiTaskProfiles.CreateObb(
                YoloDetectionFamily.YoloV26, id,
                "bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df",
                YoloLabelSets.Dota15, sourceRevision, exporter, options).VisualProfile,
            _ => throw new ArgumentException("任务应为 classify / segment / pose / obb。")
        };
    }
}
