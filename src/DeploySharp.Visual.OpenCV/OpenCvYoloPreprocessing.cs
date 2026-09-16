using System;
using JYPPX.DeploySharp.Visual.Models.Yolo;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Translates backend-neutral YOLO preprocessing contracts into OpenCV options. / 将后端无关 YOLO 预处理合同转换为 OpenCV 选项。</summary>
    public static class OpenCvYoloPreprocessing
    {
        /// <summary>Creates OpenCV options from the exact YOLO preprocessing contract. / 根据精确 YOLO 预处理合同创建 OpenCV 选项。</summary>
        public static OpenCvPreprocessOptions CreateOptions(YoloDetectionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            YoloPreprocessingContract contract = profile.Preprocessing;
            byte padding = contract.PaddingValue;
            OpenCvResizeMode resizeMode = contract.ResizeMode == YoloImageResizeMode.Resize ? OpenCvResizeMode.Resize : contract.ResizeMode == YoloImageResizeMode.CenterCrop ? OpenCvResizeMode.CenterCrop : OpenCvResizeMode.Letterbox;
            VisualPreprocessingOptions? declared = profile.VisualProfile.Preprocessing;
            if (declared != null) return declared.ToOpenCvOptions();
            return new OpenCvPreprocessOptions(
                contract.ModelSize,
                resizeMode,
                VisualColorOrder.Rgb,
                OpenCvAlphaMode.Drop,
                means: contract.Normalization.Means,
                standardDeviations: contract.Normalization.StandardDeviations,
                layout: VisualTensorLayout.Nchw,
                batchSize: 1,
                outputType: OpenCvOutputType.Float32,
                paddingColor: new OpenCvRgbColor(padding, padding, padding),
                inputDivisors: contract.Normalization.InputDivisors,
                scaleUp: contract.ScaleUp);
        }

        /// <summary>Creates options from a profile and an explicit geometry/normalization override. / 根据 Profile 及显式几何/归一化覆盖创建选项。</summary>
        public static OpenCvPreprocessOptions CreateOptions(YoloDetectionProfile profile, VisualPreprocessingOptions preprocessing)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (preprocessing == null) throw new ArgumentNullException(nameof(preprocessing));
            if (!preprocessing.ModelSize.HasValue || preprocessing.ModelSize.Value != profile.Preprocessing.ModelSize) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The YOLO preprocessing override must use the profile model size.");
            return preprocessing.ToOpenCvOptions();
        }

        /// <summary>Creates OpenCV options for any artifact-bound YOLO classification, segmentation, Pose, or OBB profile. / 为绑定工件的 YOLO 分类、分割、Pose 或 OBB Profile 创建 OpenCV 选项。</summary>
        public static OpenCvPreprocessOptions CreateOptions(YoloMultiTaskProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            YoloImagePreprocessingContract contract = profile.Preprocessing;
            OpenCvResizeMode resizeMode = contract.ResizeMode == YoloImageResizeMode.Resize ? OpenCvResizeMode.Resize : contract.ResizeMode == YoloImageResizeMode.CenterCrop ? OpenCvResizeMode.CenterCrop : OpenCvResizeMode.Letterbox;
            VisualPreprocessingOptions? declared = profile.VisualProfile.Preprocessing;
            if (declared != null) return declared.ToOpenCvOptions();
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
