using System;
using JYPPX.DeploySharp.Visual.Models.Yolo;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Translates backend-neutral YOLO preprocessing contracts into OpenCV options. / 将后端无关 YOLO 预处理合同转换为 OpenCV 选项。</summary>
    public static class OpenCvYoloPreprocessing
    {
        /// <summary>Creates centered RGB NCHW letterbox options with 1/255 normalization. / 创建居中的 RGB NCHW Letterbox 选项并应用 1/255 归一化。</summary>
        public static OpenCvPreprocessOptions CreateOptions(YoloDetectionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            YoloPreprocessingContract contract = profile.Preprocessing;
            if (!contract.ScaleUp) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The current OpenCV adapter cannot yet represent YOLO scaleUp=false without changing pixel geometry.");
            byte padding = contract.PaddingValue;
            return new OpenCvPreprocessOptions(
                contract.ModelSize,
                OpenCvResizeMode.Letterbox,
                VisualColorOrder.Rgb,
                OpenCvAlphaMode.Drop,
                standardDeviations: new[] { 255f },
                layout: VisualTensorLayout.Nchw,
                batchSize: 1,
                outputType: OpenCvOutputType.Float32,
                paddingColor: new OpenCvRgbColor(padding, padding, padding));
        }

        /// <summary>Creates OpenCV options for any artifact-bound YOLO classification, segmentation, Pose, or OBB profile. / 为绑定工件的 YOLO 分类、分割、Pose 或 OBB Profile 创建 OpenCV 选项。</summary>
        public static OpenCvPreprocessOptions CreateOptions(YoloMultiTaskProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            YoloImagePreprocessingContract contract = profile.Preprocessing;
            OpenCvResizeMode resizeMode = contract.ResizeMode == YoloImageResizeMode.CenterCrop ? OpenCvResizeMode.CenterCrop : OpenCvResizeMode.Letterbox;
            return new OpenCvPreprocessOptions(
                contract.ModelSize,
                resizeMode,
                VisualColorOrder.Rgb,
                OpenCvAlphaMode.Drop,
                standardDeviations: new[] { contract.PixelDivisor },
                layout: VisualTensorLayout.Nchw,
                batchSize: 1,
                outputType: OpenCvOutputType.Float32,
                paddingColor: new OpenCvRgbColor(contract.PaddingValue, contract.PaddingValue, contract.PaddingValue));
        }
    }
}
