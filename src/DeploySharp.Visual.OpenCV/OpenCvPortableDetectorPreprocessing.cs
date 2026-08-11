using System;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.Detr;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates official-style OpenCV inputs for DEIMv2, RF-DETR, Paddle RT-DETR, RT-DETRv2, and PP-YOLOE. / 为 DEIMv2、RF-DETR、Paddle RT-DETR、RT-DETRv2 与 PP-YOLOE 创建官方风格 OpenCV 输入。</summary>
    public static class OpenCvPortableDetectorPreprocessing
    {
        /// <summary>Creates OpenCV preprocessing options from an artifact-bound profile. / 根据绑定工件的 Profile 创建 OpenCV 预处理选项。</summary>
        public static OpenCvPreprocessOptions CreateOptions(PortableDetectorProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            PortableDetectorFamily family = profile.Family;
            if (family == PortableDetectorFamily.DEIMv2Det)
            {
                VisualSize modelSize = ModelSize(profile);
                return profile.DeimUsesImageNetNormalization
                    ? new OpenCvPreprocessOptions(modelSize, OpenCvResizeMode.Letterbox, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, new[] { .485f, .456f, .406f }, new[] { .229f, .224f, .225f }, VisualTensorLayout.Nchw, 1, OpenCvOutputType.Float32, OpenCvRgbColor.Black, letterboxRounding: OpenCvLetterboxRounding.Floor)
                    : new OpenCvPreprocessOptions(modelSize, OpenCvResizeMode.Letterbox, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, standardDeviations: new[] { 255f }, layout: VisualTensorLayout.Nchw, batchSize: 1, outputType: OpenCvOutputType.Float32, paddingColor: OpenCvRgbColor.Black, letterboxRounding: OpenCvLetterboxRounding.Floor);
            }

            if (family == PortableDetectorFamily.RFDETRDet || family == PortableDetectorFamily.RFDETRSeg)
            {
                return new OpenCvPreprocessOptions(ModelSize(profile), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, new[] { .485f, .456f, .406f }, new[] { .229f, .224f, .225f }, VisualTensorLayout.Nchw, 1, OpenCvOutputType.Float32);
            }

            // PaddleDetection TestReader decodes RGB, resizes directly, and scales pixels to [0,1]. / PaddleDetection TestReader 解码 RGB、直接缩放，并将像素缩放到 [0,1]。
            return new OpenCvPreprocessOptions(ModelSize(profile), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, standardDeviations: new[] { 255f }, layout: VisualTensorLayout.Nchw, batchSize: 1, outputType: OpenCvOutputType.Float32);
        }

        /// <summary>Loads an image and appends exact geometry auxiliary tensors required by the profile. / 加载图像并附加 Profile 所需的精确几何辅助张量。</summary>
        public static PreparedVisualInput CreateFromFile(OpenCvVisualInputFactory factory, string path, PortableDetectorProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return Create(factory, OpenCvImageSource.FromFile(path), profile, inputId, cancellationToken);
        }

        /// <summary>Decodes encoded PNG/JPEG bytes and attaches the same typed auxiliary tensors as file input. / 解码 PNG/JPEG 编码字节，并附加与文件输入相同的类型化辅助张量。</summary>
        public static PreparedVisualInput CreateFromBytes(OpenCvVisualInputFactory factory, byte[] encodedBytes, PortableDetectorProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (encodedBytes == null) throw new ArgumentNullException(nameof(encodedBytes));
            return Create(factory, OpenCvImageSource.FromBytes(encodedBytes), profile, inputId, cancellationToken);
        }

        /// <summary>Creates one prepared input from a file/byte source; cancellation is observed before and during native preprocessing and auxiliary values are generated once afterward. / 从文件或字节源创建一个已准备输入；取消在原生前处理前及期间观察，辅助值随后仅生成一次。</summary>
        public static PreparedVisualInput Create(OpenCvVisualInputFactory factory, OpenCvImageSource source, PortableDetectorProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            PreparedVisualInput baseInput = factory.Create(source, profile.VisualProfile.Input.Name, CreateOptions(profile), inputId, cancellationToken: cancellationToken);
            try
            {
                return RebindWithAuxiliaryInputs(baseInput, profile);
            }
            finally
            {
                // The base input borrows the managed tensor, so disposing it cannot release the returned tensor. / 基础输入借用托管张量，释放它不会释放返回张量。
                baseInput.Dispose();
            }
        }

        /// <summary>Attaches profile-specific geometry tensors to an already prepared image input; the returned input borrows the managed image tensor and auxiliary tensors require no native disposal. / 将 Profile 特定几何张量附加到已准备图像输入；返回输入借用托管图像张量，辅助张量无需释放原生资源。</summary>
        public static PreparedVisualInput RebindWithAuxiliaryInputs(PreparedVisualInput input, PortableDetectorProfile profile)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (input.IsDisposed) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The prepared image input has been disposed.");
            var auxiliary = profile.CreateAuxiliaryInputs(input);
            return new PreparedVisualInput(input.InputName, input.Tensor, input.SourceSize, input.ModelSize, input.BatchSize, input.Layout, input.Transform, input.Preprocessing, input.InputId, PreparedInputOwnership.Borrowed, null, auxiliary);
        }

        private static VisualSize ModelSize(PortableDetectorProfile profile)
        {
            TensorShape shape = profile.VisualProfile.Input.ShapePattern;
            if (shape.Rank != 4 || shape[2] <= 0 || shape[3] <= 0) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "Portable detector preprocessing requires a static NCHW model size.");
            return new VisualSize(checked((int)shape[3]), checked((int)shape[2]));
        }
    }
}
