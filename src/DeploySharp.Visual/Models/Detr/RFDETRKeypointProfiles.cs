using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Detr
{
    /// <summary>Creates profiles for the official RF-DETR person-keypoint preview export. / 为官方 RF-DETR 人体关键点预览导出创建 Profile。</summary>
    public static class RFDETRKeypointProfiles
    {
        /// <summary>Creates the fixed-batch COCO-17 preview profile with background/person logits and two padded keypoint slots. / 创建具有背景/人体 Logit 和两个补齐关键点槽的固定批次 COCO-17 预览 Profile。</summary>
        /// <remarks>Use the official 1.11.2 raw ONNX export. Dynamic batch and fine-tuned keypoint schemas require an explicit custom profile. / 使用官方 1.11.2 原始 ONNX 导出；动态批次和微调后的关键点模式需要显式自定义 Profile。</remarks>
        public static VisualModelProfile CreatePreview(ModelId modelId, PortableDetectorProfileOptions options, string keypointsOutputName = "keypoints", float traceAlpha = 0.2f)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Labels.Count != 2 || options.RfDetrIncludesNoObjectClass) throw new ArgumentException("RF-DETR Keypoint Preview requires exactly two logit columns: background (0), person (1).", nameof(options));
            if (options.Batch.HasDynamicAxis || options.Batch.MinimumBatch != 1 || options.Batch.MaximumBatch != 1) throw new ArgumentException("RF-DETR Keypoint Preview currently accepts fixed batch one; use independent sessions for concurrent images.", nameof(options));
            if (options.ModelSize != new VisualSize(576, 576) || (options.RfDetrQueryCount != -1 && options.RfDetrQueryCount != 100)) throw new ArgumentException("The official preview export uses 576x576 inputs and 100 queries.", nameof(options));
            if (options.ArtifactSha256 != null)
            {
                if (options.ArtifactSha256.Length != 64) throw new ArgumentException("An artifact SHA256 must contain 64 hexadecimal characters.", nameof(options));
                foreach (char value in options.ArtifactSha256) if (!Uri.IsHexDigit(value)) throw new ArgumentException("An artifact SHA256 must be hexadecimal.", nameof(options));
            }
            string boxes = options.BoxesOutputName ?? "dets";
            string logits = options.LabelsOutputName ?? "labels";
            int topK = Math.Min(100, options.TopK);
            var decoder = new RFDETRKeypointDecoder(boxes, logits, keypointsOutputName, 100, options.ScoreThreshold, topK, Math.Min(topK, options.MaximumResults), traceAlpha);
            var preprocessing = options.Preprocessing ?? new VisualPreprocessingOptions(options.ModelSize, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet);
            if (!preprocessing.ModelSize.HasValue || preprocessing.ModelSize.Value != options.ModelSize) throw new ArgumentException("Preprocessing must retain the artifact's 576x576 canvas.", nameof(options));
            if (preprocessing.BatchSize != 1 || preprocessing.Layout != VisualTensorLayout.Nchw || preprocessing.OutputType != VisualPreprocessingOutputType.Float32 || (preprocessing.ColorOrder != VisualColorOrder.Rgb && preprocessing.ColorOrder != VisualColorOrder.Bgr)) throw new ArgumentException("Preprocessing must produce batch-one Float32 NCHW with three color channels.", nameof(options));
            return new VisualModelProfile(
                options.ProfileId ?? "rfdetr.keypoint-preview." + modelId.Value, modelId, VisualTaskId.PoseEstimation,
                "rfdetr-1.11.2", options.ModelFormat,
                new VisualInputBinding(options.InputName, TensorElementType.Float32, new TensorShape(1, 3, 576, 576), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding(boxes, TensorElementType.Float32, new TensorShape(1, 100, 4)),
                    new VisualOutputBinding(logits, TensorElementType.Float32, new TensorShape(1, 100, 2)),
                    new VisualOutputBinding(keypointsOutputName, TensorElementType.Float32, new TensorShape(1, 100, 34, 8))
                },
                new[] { new VisualLabel(0, options.Labels[0]), new VisualLabel(1, options.Labels[1]) }, decoder, preprocessing: preprocessing);
        }
    }
}
