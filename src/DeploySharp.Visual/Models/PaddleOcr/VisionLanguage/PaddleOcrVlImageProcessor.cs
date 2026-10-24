#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Threading;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Creates the official PaddleOCR-VL-1.6 normalized patch tensor from packed RGB pixels. / 从紧密 RGB 像素创建官方 PaddleOCR-VL-1.6 归一化 Patch Tensor。</summary>
    /// <remarks>The input is packed row-major RGB, immutable for the duration of the call. The output layout is [grid_h,grid_w,3,14,14], matching the pinned official processor. Encoded image decoding belongs to an optional adapter such as DeploySharp.Visual.OpenCV. / 输入为行优先紧密 RGB，调用期间不得修改；输出布局为 [grid_h,grid_w,3,14,14]，匹配固定官方 Processor。编码图像解码由可选适配器（例如 DeploySharp.Visual.OpenCV）负责。</remarks>
    public sealed class PaddleOcrVlImageProcessor
    {
        /// <summary>Creates one processor using the official V1.6 interpolation and normalization contract. / 创建使用官方 V1.6 插值和归一化合同的处理器。</summary>
        public PaddleOcrVlImageProcessor(int maximumSourcePixels = 128 * 1024 * 1024)
        {
            if (maximumSourcePixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSourcePixels));
            MaximumSourcePixels = maximumSourcePixels;
        }

        /// <summary>Gets the hard source pixel limit applied before allocation. / 获取分配前应用的源图像素硬上限。</summary>
        public int MaximumSourcePixels { get; }

        /// <summary>Plans and prepares one packed RGB image. / 规划并准备一张紧密 RGB 图像。</summary>
        public PaddleOcrVlPreparedInput Create(byte[] packedRgb, VisualSize sourceSize,
            PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition, int? minimumPixels = null,
            int? maximumPixels = null, string? sourceSha256 = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) throw new ArgumentException("A positive source size is required.", nameof(sourceSize));
            PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(sourceSize, task, minimumPixels, maximumPixels, MaximumSourcePixels);
            return Create(plan, packedRgb, sourceSha256, cancellationToken);
        }

        /// <summary>Prepares pixels using an already validated official geometry plan. / 使用已验证的官方几何规划准备像素。</summary>
        public PaddleOcrVlPreparedInput Create(PaddleOcrVlImagePlan plan, byte[] packedRgb, string? sourceSha256 = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (packedRgb == null) throw new ArgumentNullException(nameof(packedRgb));
            long expected = checked((long)plan.SourceSize.Width * plan.SourceSize.Height * 3L);
            if (expected > int.MaxValue || packedRgb.Length != expected)
                throw new ArgumentException("Packed RGB pixels must contain exactly width*height*3 bytes.", nameof(packedRgb));
            Observe(cancellationToken);

            byte[] prepared = packedRgb;
            if (plan.RequiresSpottingUpscale)
            {
                prepared = LanczosResize(packedRgb, plan.SourceSize.Width, plan.SourceSize.Height,
                    plan.PreparedSize.Width, plan.PreparedSize.Height, cancellationToken);
            }
            byte[] resized = prepared;
            if (plan.PreparedSize != plan.ResizedSize)
            {
                resized = BicubicResize(prepared, plan.PreparedSize.Width, plan.PreparedSize.Height,
                    plan.ResizedSize.Width, plan.ResizedSize.Height, cancellationToken);
            }

            int patch = 14;
            int plane = checked(patch * patch);
            int patchCount = checked(plan.GridHeight * plan.GridWidth);
            var values = new float[checked(patchCount * 3 * plane)];
            for (int gridRow = 0; gridRow < plan.GridHeight; gridRow++)
            {
                Observe(cancellationToken);
                for (int gridColumn = 0; gridColumn < plan.GridWidth; gridColumn++)
                {
                    int patchIndex = checked(gridRow * plan.GridWidth + gridColumn);
                    int patchOffset = checked(patchIndex * 3 * plane);
                    int sourceX = gridColumn * patch;
                    int sourceY = gridRow * patch;
                    for (int y = 0; y < patch; y++)
                    {
                        int sourceRow = checked((sourceY + y) * plan.ResizedSize.Width * 3);
                        int spatial = y * patch;
                        for (int x = 0; x < patch; x++)
                        {
                            int source = sourceRow + ((sourceX + x) * 3);
                            int destination = spatial + x;
                            values[patchOffset + destination] = Normalize(resized[source]);
                            values[patchOffset + plane + destination] = Normalize(resized[source + 1]);
                            values[patchOffset + (2 * plane) + destination] = Normalize(resized[source + 2]);
                        }
                    }
                }
            }

            return new PaddleOcrVlPreparedInput(plan,
                new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, patch, patch), values, TensorBufferOwnership.Transfer),
                sourceSha256);
        }

        private static float Normalize(byte value) => (value / 127.5f) - 1f;

        private static void Observe(CancellationToken cancellationToken) => cancellationToken.ThrowIfCancellationRequested();

        private static byte[] BicubicResize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, CancellationToken cancellationToken)
            => Resize(source, sourceWidth, sourceHeight, targetWidth, targetHeight, 2.0, static distance =>
            {
                if (distance < 1.0) return ((1.5 * distance - 2.5) * distance * distance) + 1.0;
                return distance < 2.0 ? (((-.5 * distance + 2.5) * distance - 4.0) * distance) + 2.0 : 0.0;
            }, cancellationToken);

        private static byte[] LanczosResize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, CancellationToken cancellationToken)
            => Resize(source, sourceWidth, sourceHeight, targetWidth, targetHeight, 3.0, static distance =>
            {
                if (distance == 0.0) return 1.0;
                if (distance >= 3.0) return 0.0;
                double piDistance = Math.PI * distance;
                return (Math.Sin(piDistance) / piDistance) * (Math.Sin(piDistance / 3.0) / (piDistance / 3.0));
            }, cancellationToken);

        private static byte[] Resize(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight,
            double supportRadius, Func<double, double> filter, CancellationToken cancellationToken)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            ResampleCoefficient[] horizontal = CreateCoefficients(sourceWidth, targetWidth, supportRadius, filter);
            ResampleCoefficient[] vertical = CreateCoefficients(sourceHeight, targetHeight, supportRadius, filter);
            var intermediate = new byte[checked(sourceHeight * targetWidth * 3)];
            var destination = new byte[checked(targetHeight * targetWidth * 3)];
            for (int y = 0; y < sourceHeight; y++)
            {
                Observe(cancellationToken);
                for (int x = 0; x < targetWidth; x++)
                {
                    ResampleCoefficient coefficient = horizontal[x];
                    for (int channel = 0; channel < 3; channel++)
                    {
                        double sum = 0.0;
                        for (int index = 0; index < coefficient.Weights.Length; index++)
                            sum += source[((y * sourceWidth + coefficient.Start + index) * 3) + channel] * coefficient.Weights[index];
                        intermediate[((y * targetWidth + x) * 3) + channel] = ClipByte(sum);
                    }
                }
            }
            for (int y = 0; y < targetHeight; y++)
            {
                Observe(cancellationToken);
                ResampleCoefficient coefficient = vertical[y];
                for (int x = 0; x < targetWidth; x++)
                {
                    for (int channel = 0; channel < 3; channel++)
                    {
                        double sum = 0.0;
                        for (int index = 0; index < coefficient.Weights.Length; index++)
                            sum += intermediate[(((coefficient.Start + index) * targetWidth + x) * 3) + channel] * coefficient.Weights[index];
                        destination[((y * targetWidth + x) * 3) + channel] = ClipByte(sum);
                    }
                }
            }
            return destination;
        }

        private static ResampleCoefficient[] CreateCoefficients(int sourceSize, int targetSize, double supportRadius, Func<double, double> filter)
        {
            double scale = sourceSize / (double)targetSize;
            double filterScale = Math.Max(scale, 1.0);
            double support = supportRadius * filterScale;
            var result = new ResampleCoefficient[targetSize];
            for (int output = 0; output < targetSize; output++)
            {
                double center = (output + .5) * scale;
                int start = Math.Max(0, (int)(center - support + .5));
                int end = Math.Min(sourceSize, (int)(center + support + .5));
                var weights = new double[end - start];
                double total = 0.0;
                for (int index = 0; index < weights.Length; index++)
                {
                    double distance = Math.Abs((start + index - center + .5) / filterScale);
                    double weight = filter(distance / 1.0);
                    weights[index] = weight;
                    total += weight;
                }
                if (Math.Abs(total) < double.Epsilon) throw new InvalidOperationException("The official image resampling filter produced an empty support.");
                for (int index = 0; index < weights.Length; index++) weights[index] /= total;
                result[output] = new ResampleCoefficient(start, weights);
            }
            return result;
        }

        private static byte ClipByte(double value)
        {
            int rounded = checked((int)Math.Floor(value + .5));
            return (byte)Math.Max(0, Math.Min(255, rounded));
        }

        private readonly struct ResampleCoefficient
        {
            internal ResampleCoefficient(int start, double[] weights) { Start = start; Weights = weights; }
            internal int Start { get; }
            internal double[] Weights { get; }
        }
    }
}
#endif
