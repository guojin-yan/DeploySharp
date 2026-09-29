using System;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Prepares FormulaNet/UniMERNet images using margin cropping, Pillow resampling and float grayscale normalization. / 使用去边、Pillow 重采样及浮点灰度归一化准备公式模型输入。</summary>
    public sealed class PaddleDocumentFormulaInputFactory
    {
        /// <summary>Decodes and prepares one image for an official formula profile. / 解码并按官方公式 Profile 准备一张图像。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, VisualModelProfile profile, string? inputId = null, CancellationToken cancellationToken = default)
            => Create(new OpenCvBgrImageFactory().Create(source, inputId, cancellationToken), profile, cancellationToken);

        /// <summary>Prepares an already decoded BGR image without decoding again. / 准备已经解码的 BGR 图像，避免重复解码。</summary>
        public PreparedVisualInput Create(OpenCvBgrImage image, VisualModelProfile profile, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!(profile.Decoder is PaddleDocumentFormulaDecoder)) throw new ArgumentException("A Paddle formula profile is required.", nameof(profile));
            var shape = profile.Input.ShapePattern;
            if (shape.Rank != 4 || shape[1] != 1 || shape[2] <= 0 || shape[3] <= 0) throw new ArgumentException("Formula inputs require [N,1,H,W].", nameof(profile));
            int targetWidth = checked((int)shape[3]), targetHeight = checked((int)shape[2]);
            if (targetWidth % 16 != 0 || targetHeight % 16 != 0) throw new ArgumentException("Formula dimensions must be divisible by 16.", nameof(profile));
            cancellationToken.ThrowIfCancellationRequested();
            byte[] source = image.GetReadOnlyInteropBuffer();
            int min = 255, max = 0;
            for (int i = 0; i < source.Length; i += 3)
            {
                int gray = MarginGray(source, i);
                min = Math.Min(min, gray); max = Math.Max(max, gray);
            }
            int left = 0, top = 0, right = image.Width, bottom = image.Height;
            if (min != max)
            {
                left = image.Width; top = image.Height; right = bottom = 0;
                for (int y = 0; y < image.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (int x = 0; x < image.Width; x++)
                        if ((MarginGray(source, (y * image.Width + x) * 3) - min) * 255 < 200 * (max - min))
                        {
                            left = Math.Min(left, x); top = Math.Min(top, y);
                            right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                        }
                }
            }
            int width = right - left, height = bottom - top;
            var pixels = new byte[checked(width * height * 3)];
            // PaddleX's reader produces RGB before UniMERNetImgDecode.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int src = ((top + y) * image.Width + left + x) * 3, dst = (y * width + x) * 3;
                    pixels[dst] = source[src + 2]; pixels[dst + 1] = source[src + 1]; pixels[dst + 2] = source[src];
                }
            int shortEdge = Math.Min(targetWidth, targetHeight);
            int resizedWidth = width <= height ? shortEdge : checked((int)((long)shortEdge * width / height));
            int resizedHeight = height <= width ? shortEdge : checked((int)((long)shortEdge * height / width));
            // Bound the intermediate even for extremely thin/malicious inputs.
            if ((long)resizedWidth * resizedHeight * 3 > 256L * 1024 * 1024) throw new ArgumentException("Formula intermediate image exceeds the 256 MiB limit.", nameof(image));
            pixels = OpenCvVisualInputFactory.PillowBilinearResize(pixels, width, height, 3, resizedWidth, resizedHeight, cancellationToken);
            int finalWidth = resizedWidth, finalHeight = resizedHeight;
            if (resizedWidth > targetWidth || resizedHeight > targetHeight)
            {
                double aspect = (double)resizedWidth / resizedHeight;
                finalWidth = targetWidth; finalHeight = targetHeight;
                if ((double)targetWidth / targetHeight >= aspect)
                    finalWidth = RoundAspect(targetHeight * aspect, n => Math.Abs(aspect - (double)n / targetHeight));
                else finalHeight = RoundAspect(targetWidth / aspect, n => n == 0 ? 0 : Math.Abs(aspect - (double)targetWidth / n));
                int factorX = Math.Max(1, resizedWidth / finalWidth / 2), factorY = Math.Max(1, resizedHeight / finalHeight / 2);
                double extentWidth = (double)resizedWidth / factorX, extentHeight = (double)resizedHeight / factorY;
                if (factorX > 1 || factorY > 1)
                    pixels = Reduce(pixels, resizedWidth, resizedHeight, factorX, factorY, out resizedWidth, out resizedHeight, cancellationToken);
                pixels = OpenCvVisualInputFactory.PillowBicubicResize(pixels, resizedWidth, resizedHeight, 3, finalWidth, finalHeight, cancellationToken, extentWidth, extentHeight);
            }
            int padX = (targetWidth - finalWidth) / 2, padY = (targetHeight - finalHeight) / 2;
            var values = new float[checked(targetWidth * targetHeight)];
            const float mean = .7931f, std = .1738f;
            float background = -mean / std;
            for (int i = 0; i < values.Length; i++) values[i] = background;
            for (int y = 0; y < finalHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < finalWidth; x++)
                {
                    int i = (y * finalWidth + x) * 3;
                    float b = (pixels[i] * (1f / 255) - mean) / std;
                    float g = (pixels[i + 1] * (1f / 255) - mean) / std;
                    float r = (pixels[i + 2] * (1f / 255) - mean) / std;
                    values[(y + padY) * targetWidth + x + padX] = .114f * b + .587f * g + .299f * r;
                }
            }
            var sourceSize = new VisualSize(image.Width, image.Height);
            var modelSize = new VisualSize(targetWidth, targetHeight);
            float scaleX = (float)finalWidth / width, scaleY = (float)finalHeight / height;
            var transform = new ImageTransform(ImageTransformKind.Custom, sourceSize, modelSize, scaleX, scaleY, padX - left * scaleX, padY - top * scaleY);
            var tensor = new Tensor<float>(new TensorShape(1, 1, targetHeight, targetWidth), values, TensorBufferOwnership.Transfer);
            return new PreparedVisualInput(profile.Input.Name, tensor, sourceSize, modelSize, 1, VisualTensorLayout.Nchw, transform,
                new VisualPreprocessingDescriptor(VisualColorOrder.Gray, new[] { mean }, new[] { 1f / std }, "Paddle UniMERNetImgDecode + UniMERNetTestTransform; margin crop, bilinear short-edge resize, bicubic thumbnail, centered black padding, float grayscale."), image.InputId);
        }

        // PIL L conversion after the reader's BGR -> RGB conversion.
        private static int MarginGray(byte[] pixels, int offset)
            => (7471 * pixels[offset] + 38470 * pixels[offset + 1] + 19595 * pixels[offset + 2] + 32768) >> 16;

        private static int RoundAspect(double number, Func<int, double> error)
        {
            int lower = (int)Math.Floor(number), upper = (int)Math.Ceiling(number);
            return Math.Max(1, error(lower) <= error(upper) ? lower : upper);
        }

        private static byte[] Reduce(byte[] pixels, int width, int height, int factorX, int factorY, out int reducedWidth, out int reducedHeight, CancellationToken cancellationToken)
        {
            reducedWidth = (width + factorX - 1) / factorX; reducedHeight = (height + factorY - 1) / factorY;
            var reduced = new byte[checked(reducedWidth * reducedHeight * 3)];
            for (int y = 0; y < reducedHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < reducedWidth; x++)
                {
                    int endX = Math.Min(width, (x + 1) * factorX), endY = Math.Min(height, (y + 1) * factorY);
                    int count = (endX - x * factorX) * (endY - y * factorY);
                    for (int channel = 0; channel < 3; channel++)
                    {
                        long sum = count / 2;
                        for (int row = y * factorY; row < endY; row++)
                            for (int col = x * factorX; col < endX; col++) sum += pixels[(row * width + col) * 3 + channel];
                        reduced[(y * reducedWidth + x) * 3 + channel] = (byte)(sum / count);
                    }
                }
            }
            return reduced;
        }
    }
}
