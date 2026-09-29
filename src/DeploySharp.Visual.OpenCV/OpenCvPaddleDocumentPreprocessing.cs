using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates official Paddle document inputs and binds model-space NMS geometry after preprocessing. / 创建官方 Paddle 文档输入，并在前处理完成后绑定模型坐标系 NMS 几何。</summary>
    public static class OpenCvPaddleDocumentPreprocessing
    {
        /// <summary>Loads and prepares one document image, then adds the exact auxiliary tensors declared by the profile. / 加载并准备一张文档图像，然后附加 Profile 声明的精确辅助张量。</summary>
        public static PreparedVisualInput CreateFromFile(OpenCvVisualInputFactory factory, string path, PaddleDocumentProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An image path is required.", nameof(path));
            return Create(factory, OpenCvImageSource.FromFile(path), profile, inputId, cancellationToken);
        }

        /// <summary>Decodes and prepares encoded document bytes, then adds the exact auxiliary tensors declared by the profile. / 解码并准备编码文档字节，然后附加 Profile 声明的精确辅助张量。</summary>
        public static PreparedVisualInput CreateFromBytes(OpenCvVisualInputFactory factory, byte[] encodedBytes, PaddleDocumentProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (encodedBytes == null) throw new ArgumentNullException(nameof(encodedBytes));
            return Create(factory, OpenCvImageSource.FromBytes(encodedBytes), profile, inputId, cancellationToken);
        }

        /// <summary>Prepares the image without caller-supplied geometry and rebinds the profile's model-space geometry once the actual transform is known. / 不接受调用方手工几何值，先完成图像前处理，再根据实际 Transform 一次性绑定 Profile 的模型坐标系几何。</summary>
        public static PreparedVisualInput Create(OpenCvVisualInputFactory factory, OpenCvImageSource source, PaddleDocumentProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            PreparedVisualInput baseInput = factory.Create(source, profile.VisualProfile, inputId, cancellationToken);
            try
            {
                if (profile.VisualProfile.AuxiliaryInputs.Count == 0) return baseInput;
                return RebindWithGeometryInputs(baseInput, profile);
            }
            finally
            {
                // The returned input borrows the prepared tensor and does not own the
                // base resource.  Do not dispose the base input when it is returned.
                if (profile.VisualProfile.AuxiliaryInputs.Count > 0) baseInput.Dispose();
            }
        }

        /// <summary>Rebinds a prepared image with model-space Paddle geometry without copying its image tensor. / 在不复制图像张量的情况下，为已准备输入绑定模型坐标系 Paddle 几何。</summary>
        public static PreparedVisualInput RebindWithGeometryInputs(PreparedVisualInput input, PaddleDocumentProfile profile)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (input.IsDisposed) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The prepared image input has been disposed.");
            IReadOnlyList<NamedTensor> auxiliary = profile.CreateGeometryInputs(input);
            return new PreparedVisualInput(input.InputName, input.Tensor, input.SourceSize, input.ModelSize, input.BatchSize, input.Layout, input.Transform, input.Preprocessing, input.InputId, PreparedInputOwnership.Borrowed, null, auxiliary, input.BatchFrames);
        }
    }
}
