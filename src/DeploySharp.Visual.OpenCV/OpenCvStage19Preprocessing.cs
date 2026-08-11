using System;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates official-style OpenCV preprocessing for PaddleOCR, Anomalib, and BRIA RMBG. / 为 PaddleOCR、Anomalib 与 BRIA RMBG 创建官方风格 OpenCV 预处理。</summary>
    public static class OpenCvStage19Preprocessing
    {
        /// <summary>Creates PP-OCRv5 DB detection options using the official max-side and stride-32 rule. / 使用官方最大边与 stride-32 规则创建 PP-OCRv5 DB 检测选项。</summary>
        public static OpenCvPreprocessOptions CreatePaddleOcrDetectionOptions(VisualSize sourceSize, int limitSideLength = 960, string limitType = "max")
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(sourceSize));
            if (limitSideLength <= 0) throw new ArgumentOutOfRangeException(nameof(limitSideLength));
            if (!string.Equals(limitType, "max", StringComparison.Ordinal) && !string.Equals(limitType, "min", StringComparison.Ordinal)) throw new ArgumentException("PaddleOCR limit type must be max or min.", nameof(limitType));
            float ratio = 1f;
            int shorter = Math.Min(sourceSize.Width, sourceSize.Height);
            int longer = Math.Max(sourceSize.Width, sourceSize.Height);
            if ((string.Equals(limitType, "max", StringComparison.Ordinal) && longer > limitSideLength) || (string.Equals(limitType, "min", StringComparison.Ordinal) && shorter < limitSideLength))
            {
                ratio = string.Equals(limitType, "max", StringComparison.Ordinal) ? (float)limitSideLength / longer : (float)limitSideLength / shorter;
            }
            int height = Math.Max(32, RoundStride((int)(sourceSize.Height * ratio), 32));
            int width = Math.Max(32, RoundStride((int)(sourceSize.Width * ratio), 32));
            // OpenCvPreprocessOptions applies (byte - mean) / deviation, so the official
            // (byte / 255 - mean) / std contract is represented in byte space. / OpenCvPreprocessOptions
            // 应用 (byte - mean) / deviation，因此在字节空间表达官方 (byte / 255 - mean) / std 合同。
            return new OpenCvPreprocessOptions(new VisualSize(width, height), OpenCvResizeMode.Resize, VisualColorOrder.Bgr, OpenCvAlphaMode.Drop,
                new[] { 123.675f, 116.28f, 103.53f }, new[] { 58.395f, 57.12f, 57.375f });
        }

        /// <summary>Creates PP-OCRv5 recognition options for direct BGR-to-NCHW normalization. / 创建 PP-OCRv5 识别选项，将 BGR 直接归一化为 NCHW。</summary>
        public static OpenCvPreprocessOptions CreatePaddleOcrRecognitionOptions(VisualSize modelSize)
        {
            return new OpenCvPreprocessOptions(modelSize, OpenCvResizeMode.Resize, VisualColorOrder.Bgr, OpenCvAlphaMode.Drop,
                means: new[] { 127.5f }, standardDeviations: new[] { 127.5f });
        }

        /// <summary>Creates legacy PaddleOCR BGR 0/180 classification preprocessing. / 创建旧版 PaddleOCR BGR 0/180 分类前处理。</summary>
        public static OpenCvPreprocessOptions CreatePaddleOcrLegacyClassificationOptions()
        {
            return new OpenCvPreprocessOptions(new VisualSize(192, 48), OpenCvResizeMode.Resize, VisualColorOrder.Bgr, OpenCvAlphaMode.Drop,
                means: new[] { 127.5f }, standardDeviations: new[] { 127.5f });
        }

        /// <summary>Creates PP-LCNet RGB text-line orientation preprocessing from its inference contract. / 根据推理合同创建 PP-LCNet RGB 文本行方向前处理。</summary>
        public static OpenCvPreprocessOptions CreatePaddleOcrTextLineOrientationOptions()
        {
            return new OpenCvPreprocessOptions(new VisualSize(160, 80), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop,
                means: new[] { 123.675f, 116.28f, 103.53f }, standardDeviations: new[] { 58.395f, 57.12f, 57.375f });
        }

        /// <summary>Creates Anomalib export options; the ONNX export owns ImageNet normalization, so OpenCV only scales bytes to [0,1]. / 创建 Anomalib 导出选项；ONNX 导出自带 ImageNet 归一化，因此 OpenCV 仅将字节缩放到 [0,1]。</summary>
        public static OpenCvPreprocessOptions CreateAnomalibOptions(AnomalibProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            TensorShape input = profile.VisualProfile.Input.ShapePattern;
            if (input.Rank != 4 || input[2] <= 0 || input[3] <= 0) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Anomalib preprocessing requires static model spatial dimensions.");
            return new OpenCvPreprocessOptions(new VisualSize(checked((int)input[3]), checked((int)input[2])), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, standardDeviations: new[] { 255f });
        }

        /// <summary>Creates BRIA RGB alpha preprocessing. / 创建 BRIA RGB Alpha 预处理。</summary>
        public static OpenCvPreprocessOptions CreateBriaRmbgOptions(BriaRmbgProfile profile, VisualSize? dynamicModelSize = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            VisualSize size = dynamicModelSize ?? profile.Options.ModelSize;
            if (size.Width <= 0 || size.Height <= 0 || size.Width > profile.Options.MaximumDynamicSide || size.Height > profile.Options.MaximumDynamicSide) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "BRIA model size exceeds its configured dynamic bound.");
            if (profile.Family == BriaRmbgFamily.Rmbg14)
            {
                return new OpenCvPreprocessOptions(size, OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, new[] { 127.5f, 127.5f, 127.5f }, new[] { 255f, 255f, 255f });
            }

            // RMBG 2.0's gated processor contract is bound to the selected artifact; this default matches its published transformer processor shape. / RMBG 2.0 的受限处理器合同绑定到所选工件；此默认值匹配其发布的 transformer 处理器形状。
            return new OpenCvPreprocessOptions(size, OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, new[] { 127.5f, 127.5f, 127.5f }, new[] { 127.5f, 127.5f, 127.5f });
        }

        private static int RoundStride(int value, int stride)
        {
            if (value <= 0) return stride;
            return checked((int)Math.Round(value / (double)stride, MidpointRounding.ToEven) * stride);
        }
    }
}
