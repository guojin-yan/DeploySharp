using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.Detr;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Prepares RF-DETR images with the official float-before-bilinear-resize order. / 按官方先转浮点再双线性缩放的顺序准备 RF-DETR 图像。</summary>
    public sealed class RFDETRInputFactory
    {
        /// <summary>Decodes one image and creates an owned managed NCHW input using its profile's preprocessing contract. / 解码一张图像并按 Profile 前处理合同创建自有托管 NCHW 输入。</summary>
        /// <remarks>The official Resize/Linear/RGB contract uses float interpolation without byte quantization. Other declared geometry or interpolation modes use the general OpenCV factory and require separate quality validation. / 官方 Resize/Linear/RGB 合同使用浮点插值且无字节量化；其它声明的几何或插值模式使用通用 OpenCV 工厂，需要另行验证质量。</remarks>
        public PreparedVisualInput Create(OpenCvImageSource source, VisualModelProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            VisualPreprocessingOptions options = Validate(profile);
            if (!UsesFloatResize(options)) return new OpenCvVisualInputFactory().Create(source, profile, inputId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            OpenCvBgrImage image = new OpenCvBgrImageFactory().Create(source, inputId, cancellationToken);
            return Create(image, profile, inputId, cancellationToken);
        }

        /// <summary>Prepares an already decoded compact BGR image without decoding it again or retaining its pixel array. / 准备已解码的紧凑 BGR 图像，不重复解码或保留其像素数组。</summary>
        /// <remarks>This overload accepts Resize/Linear with RGB or BGR and dropped alpha. Use the encoded-source overload for other geometry, interpolation or alpha policies. / 此重载接受 Resize/Linear、RGB 或 BGR 和丢弃 Alpha；其它几何、插值或 Alpha 策略请使用编码输入重载。</remarks>
        public PreparedVisualInput Create(OpenCvBgrImage image, VisualModelProfile profile, string? inputId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            VisualPreprocessingOptions options = Validate(profile);
            if (!UsesFloatResize(options)) throw new ArgumentException("Decoded BGR input requires Resize/Linear and dropped alpha. Use the encoded-source overload for other preprocessing.", nameof(profile));
            cancellationToken.ThrowIfCancellationRequested();
            byte[] pixels = image.GetReadOnlyInteropBuffer();
            VisualSize target = options.ModelSize!.Value;
            int plane = checked(target.Width * target.Height);
            var values = new float[checked(plane * 3)];
            float scaleX = (float)image.Width / target.Width, scaleY = (float)image.Height / target.Height;
            for (int y = 0; y < target.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float sourceY = Math.Max(0, (y + .5f) * scaleY - .5f);
                int top = Math.Min(image.Height - 1, (int)sourceY), bottom = Math.Min(image.Height - 1, top + 1);
                float fy = sourceY - top;
                for (int x = 0; x < target.Width; x++)
                {
                    float sourceX = Math.Max(0, (x + .5f) * scaleX - .5f);
                    int left = Math.Min(image.Width - 1, (int)sourceX), right = Math.Min(image.Width - 1, left + 1);
                    float fx = sourceX - left;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int color = options.ColorOrder == VisualColorOrder.Rgb ? 2 - channel : channel;
                        // Convert each source pixel to [0,1] before interpolation, matching torchvision tensor Resize.
                        float a = pixels[(top * image.Width + left) * 3 + color] / 255f;
                        float b = pixels[(top * image.Width + right) * 3 + color] / 255f;
                        float c = pixels[(bottom * image.Width + left) * 3 + color] / 255f;
                        float d = pixels[(bottom * image.Width + right) * 3 + color] / 255f;
                        float value = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
                        float divisor = Component(options.Normalization.InputDivisors, channel, 1);
                        value = (value * (255f / divisor) - Component(options.Normalization.Means, channel, 0)) / Component(options.Normalization.StandardDeviations, channel, 1);
                        values[channel * plane + y * target.Width + x] = value;
                    }
                }
            }
            var sourceSize = new VisualSize(image.Width, image.Height);
            var means = new float[3];
            var scales = new float[3];
            for (int channel = 0; channel < 3; channel++)
            {
                means[channel] = Component(options.Normalization.Means, channel, 0);
                scales[channel] = 1f / Component(options.Normalization.StandardDeviations, channel, 1);
            }
            var descriptor = new VisualPreprocessingDescriptor(options.ColorOrder, means, scales, "RF-DETR float-before-bilinear resize; inputDivisors=" + string.Join(",", options.Normalization.InputDivisors));
            return new PreparedVisualInput(profile.Input.Name, new Tensor<float>(new TensorShape(1, 3, target.Height, target.Width), values, TensorBufferOwnership.Transfer), sourceSize, target, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(sourceSize, target), descriptor, inputId: inputId ?? image.InputId);
        }

        private static bool UsesFloatResize(VisualPreprocessingOptions options) => options.ResizeMode == VisualResizeMode.Resize && options.Interpolation == VisualInterpolationMode.Linear && options.AlphaMode == VisualAlphaMode.Drop;

        private static VisualPreprocessingOptions Validate(VisualModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!(profile.Decoder is RFDETRKeypointDecoder) && !(profile.Decoder is RFDETRInstanceSegmentationDecoder)) throw new ArgumentException("An RF-DETR keypoint or segmentation profile is required.", nameof(profile));
            VisualPreprocessingOptions options = profile.Preprocessing ?? throw new ArgumentException("The profile must declare preprocessing.", nameof(profile));
            if (profile.Input.ElementType != TensorElementType.Float32 || profile.Input.Layout != VisualTensorLayout.Nchw || profile.Input.ShapePattern.Rank != 4 || profile.Input.ShapePattern[0] != 1 || profile.Input.ShapePattern[1] != 3 || options.BatchSize != 1 || options.Layout != VisualTensorLayout.Nchw || options.OutputType != VisualPreprocessingOutputType.Float32 || !options.ModelSize.HasValue || (options.ColorOrder != VisualColorOrder.Rgb && options.ColorOrder != VisualColorOrder.Bgr)) throw new ArgumentException("RF-DETR input preparation requires fixed batch-one Float32 NCHW RGB/BGR.", nameof(profile));
            if (profile.Input.ShapePattern[2] != options.ModelSize.Value.Height || profile.Input.ShapePattern[3] != options.ModelSize.Value.Width) throw new ArgumentException("Preprocessing dimensions must match the artifact input canvas.", nameof(profile));
            return options;
        }

        private static float Component(IReadOnlyList<float> values, int channel, float fallback) => values.Count == 0 ? fallback : values[values.Count == 1 ? 0 : channel];
    }
}
