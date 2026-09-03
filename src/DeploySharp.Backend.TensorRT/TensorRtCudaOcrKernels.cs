using System;
using System.Collections.Generic;
using JYPPX.CudaSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Provides NVRTC definitions and stream-ordered launch helpers for GPU OCR stages. / 提供 GPU OCR 阶段的 NVRTC 定义和 stream 有序启动辅助方法。</summary>
    public static class TensorRtCudaOcrKernels
    {
        private const int DefaultBlockSize = 256;

        /// <summary>Gets the fused BGR bilinear resize, letterbox, and NCHW normalization kernel definition. / 获取融合 BGR 双线性缩放、letterbox 和 NCHW 归一化 kernel 定义。</summary>
        public static TensorRtCudaRtcKernelDefinition NormalizeLetterboxDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Preprocessing,
            NormalizeLetterboxSource,
            "deploysharp_normalize_letterbox",
            "deploysharp-ocr-preprocess.cu");

        /// <summary>Gets the quadrilateral-to-homography kernel definition. / 获取四边形到单应矩阵的 kernel 定义。</summary>
        public static TensorRtCudaRtcKernelDefinition HomographyDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Preprocessing,
            HomographySource,
            "deploysharp_quad_to_homography",
            "deploysharp-ocr-crop.cu");

        /// <summary>Gets the GPU perspective crop and normalization kernel definition. / 获取 GPU 透视裁剪和归一化 kernel 定义。</summary>
        public static TensorRtCudaRtcKernelDefinition PerspectiveCropDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Preprocessing,
            PerspectiveCropSource,
            "deploysharp_perspective_crop",
            "deploysharp-ocr-crop.cu");

        /// <summary>Gets the fused quadrilateral-to-perspective-crop kernel definition. / 获取融合四边形到透视裁剪的 kernel 定义。</summary>
        public static TensorRtCudaRtcKernelDefinition PerspectiveCropFromQuadrilateralDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Preprocessing,
            PerspectiveCropFromQuadrilateralSource,
            "deploysharp_perspective_crop_quad",
            "deploysharp-ocr-crop-quad.cu");

        /// <summary>Gets the greedy CTC argmax, blank collapse, and confidence kernel definition. / 获取 greedy CTC argmax、blank 折叠和置信度 kernel 定义。</summary>
        public static TensorRtCudaRtcKernelDefinition CtcDecodeDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Postprocessing,
            CtcDecodeSource,
            "deploysharp_ctc_decode",
            "deploysharp-ocr-postprocess.cu");

        /// <summary>Gets the GPU per-timestep argmax and confidence trace definition used by the automatic Visual CTC path. / 获取自动 Visual CTC 路径使用的 GPU 逐时间步 argmax 与置信度轨迹定义。</summary>
        public static TensorRtCudaRtcKernelDefinition CtcTraceDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Postprocessing,
            CtcTraceSource,
            "deploysharp_ctc_trace",
            "deploysharp-ocr-ctc-trace.cu");

        /// <summary>Launches fused normalization and letterbox work without synchronizing the caller-owned stream. / 在不同步调用方 stream 的情况下启动融合归一化和 letterbox。</summary>
        public static TensorRtCudaKernelLaunch LaunchNormalizeLetterbox(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer sourceBgr,
            TensorRtCudaDeviceBuffer destinationNchw,
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight,
            float padValue,
            float meanB,
            float meanG,
            float meanR,
            float scale,
            bool swapRedBlue = false)
        {
            ValidateImageDimensions(sourceWidth, sourceHeight, destinationWidth, destinationHeight);
            EnsureKernel(kernel, NormalizeLetterboxDefinition);
            // Letterbox geometry is invariant for the whole image. Compute it
            // once instead of repeating min/division/rounding per pixel.
            double resizeScale = Math.Min((double)destinationWidth / sourceWidth, (double)destinationHeight / sourceHeight);
            float inverseScale = checked((float)(1.0 / resizeScale));
            float resizedWidth = checked((float)(sourceWidth * resizeScale));
            float resizedHeight = checked((float)(sourceHeight * resizeScale));
            float padX = (destinationWidth - resizedWidth) * 0.5f;
            float padY = (destinationHeight - resizedHeight) * 0.5f;
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(sourceBgr),
                TensorRtCudaKernelArgument.FromDeviceBuffer(destinationNchw),
                TensorRtCudaKernelArgument.FromInt32(sourceWidth),
                TensorRtCudaKernelArgument.FromInt32(sourceHeight),
                TensorRtCudaKernelArgument.FromInt32(destinationWidth),
                TensorRtCudaKernelArgument.FromInt32(destinationHeight),
                TensorRtCudaKernelArgument.FromSingle(inverseScale),
                TensorRtCudaKernelArgument.FromSingle(padX),
                TensorRtCudaKernelArgument.FromSingle(padY),
                TensorRtCudaKernelArgument.FromSingle(padValue),
                TensorRtCudaKernelArgument.FromSingle(meanB),
                TensorRtCudaKernelArgument.FromSingle(meanG),
                TensorRtCudaKernelArgument.FromSingle(meanR),
                TensorRtCudaKernelArgument.FromSingle(scale),
                TensorRtCudaKernelArgument.FromInt32(swapRedBlue ? 1 : 0)
            };
            int pixels = checked(destinationWidth * destinationHeight);
            return kernel.Launch(stream, GridFor(pixels), arguments);
        }

        /// <summary>Launches GPU homography construction for one quadrilateral per region. / 在 GPU 上为每个区域启动四边形单应矩阵构建。</summary>
        public static TensorRtCudaKernelLaunch LaunchHomography(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer quadrilaterals,
            TensorRtCudaDeviceBuffer homographies,
            int regionCount)
        {
            if (regionCount <= 0) throw new ArgumentOutOfRangeException(nameof(regionCount));
            EnsureKernel(kernel, HomographyDefinition);
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(quadrilaterals),
                TensorRtCudaKernelArgument.FromDeviceBuffer(homographies),
                TensorRtCudaKernelArgument.FromInt32(regionCount)
            };
            return kernel.Launch(stream, GridFor(regionCount), arguments);
        }

        /// <summary>Launches GPU perspective crops and per-channel normalization for a crop batch. / 在 GPU 上启动 crop batch 的透视裁剪和逐通道归一化。</summary>
        public static TensorRtCudaKernelLaunch LaunchPerspectiveCrop(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer sourceBgr,
            TensorRtCudaDeviceBuffer homographies,
            TensorRtCudaDeviceBuffer destinationNchw,
            int sourceWidth,
            int sourceHeight,
            int cropWidth,
            int cropHeight,
            int regionCount,
            float meanB,
            float meanG,
            float meanR,
            float scale,
            float paddingValue = 0)
        {
            ValidateImageDimensions(sourceWidth, sourceHeight, cropWidth, cropHeight);
            if (regionCount <= 0) throw new ArgumentOutOfRangeException(nameof(regionCount));
            EnsureKernel(kernel, PerspectiveCropDefinition);
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(sourceBgr),
                TensorRtCudaKernelArgument.FromDeviceBuffer(homographies),
                TensorRtCudaKernelArgument.FromDeviceBuffer(destinationNchw),
                TensorRtCudaKernelArgument.FromInt32(sourceWidth),
                TensorRtCudaKernelArgument.FromInt32(sourceHeight),
                TensorRtCudaKernelArgument.FromInt32(cropWidth),
                TensorRtCudaKernelArgument.FromInt32(cropHeight),
                TensorRtCudaKernelArgument.FromInt32(regionCount),
                TensorRtCudaKernelArgument.FromSingle(meanB),
                TensorRtCudaKernelArgument.FromSingle(meanG),
                TensorRtCudaKernelArgument.FromSingle(meanR),
                TensorRtCudaKernelArgument.FromSingle(scale),
                TensorRtCudaKernelArgument.FromSingle(paddingValue)
            };
            int workItems = checked(regionCount * cropWidth * cropHeight);
            return kernel.Launch(stream, GridFor(workItems), arguments);
        }

        /// <summary>Launches the fused quadrilateral homography, perspective crop, and normalization kernel without an intermediate homography buffer. / 在没有中间单应矩阵缓冲区的情况下启动融合四边形单应矩阵、透视裁剪和归一化 kernel。</summary>
        public static TensorRtCudaKernelLaunch LaunchPerspectiveCropFromQuadrilaterals(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer sourceBgr,
            TensorRtCudaDeviceBuffer quadrilaterals,
            TensorRtCudaDeviceBuffer destinationNchw,
            int sourceWidth,
            int sourceHeight,
            int cropWidth,
            int cropHeight,
            int regionCount,
            float meanB,
            float meanG,
            float meanR,
            float scale,
            float paddingValue = 0)
        {
            ValidateImageDimensions(sourceWidth, sourceHeight, cropWidth, cropHeight);
            if (regionCount <= 0) throw new ArgumentOutOfRangeException(nameof(regionCount));
            EnsureKernel(kernel, PerspectiveCropFromQuadrilateralDefinition);
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(sourceBgr),
                TensorRtCudaKernelArgument.FromDeviceBuffer(quadrilaterals),
                TensorRtCudaKernelArgument.FromDeviceBuffer(destinationNchw),
                TensorRtCudaKernelArgument.FromInt32(sourceWidth),
                TensorRtCudaKernelArgument.FromInt32(sourceHeight),
                TensorRtCudaKernelArgument.FromInt32(cropWidth),
                TensorRtCudaKernelArgument.FromInt32(cropHeight),
                TensorRtCudaKernelArgument.FromInt32(regionCount),
                TensorRtCudaKernelArgument.FromSingle(meanB),
                TensorRtCudaKernelArgument.FromSingle(meanG),
                TensorRtCudaKernelArgument.FromSingle(meanR),
                TensorRtCudaKernelArgument.FromSingle(scale),
                TensorRtCudaKernelArgument.FromSingle(paddingValue)
            };
            return kernel.Launch(stream, GridForRegionPixels(cropWidth * cropHeight, regionCount), arguments);
        }

        /// <summary>Launches one-thread-per-sequence greedy CTC decoding; only compact token buffers are produced. / 以每序列一个线程启动 greedy CTC 解码，只生成紧凑 token 缓冲区。</summary>
        public static TensorRtCudaKernelLaunch LaunchCtcDecode(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer logits,
            TensorRtCudaDeviceBuffer tokenIds,
            TensorRtCudaDeviceBuffer lengths,
            TensorRtCudaDeviceBuffer confidences,
            int batch,
            int time,
            int classes,
            int blankIndex,
            int maximumTokens,
            bool applySoftmax = true,
            bool collapseRepeats = true)
        {
            if (batch <= 0) throw new ArgumentOutOfRangeException(nameof(batch));
            if (time <= 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (classes <= 0) throw new ArgumentOutOfRangeException(nameof(classes));
            if (blankIndex < 0 || blankIndex >= classes) throw new ArgumentOutOfRangeException(nameof(blankIndex));
            if (maximumTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            EnsureKernel(kernel, CtcDecodeDefinition);
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(logits),
                TensorRtCudaKernelArgument.FromDeviceBuffer(tokenIds),
                TensorRtCudaKernelArgument.FromDeviceBuffer(lengths),
                TensorRtCudaKernelArgument.FromDeviceBuffer(confidences),
                TensorRtCudaKernelArgument.FromInt32(batch),
                TensorRtCudaKernelArgument.FromInt32(time),
                TensorRtCudaKernelArgument.FromInt32(classes),
                TensorRtCudaKernelArgument.FromInt32(blankIndex),
                TensorRtCudaKernelArgument.FromInt32(maximumTokens),
                TensorRtCudaKernelArgument.FromInt32(applySoftmax ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(collapseRepeats ? 1 : 0)
            };
            // The CTC kernel assigns one whole block to each sequence rather
            // than one thread to each sequence, so grid.x is exactly batch.
            return kernel.Launch(stream, GridForSequences(batch), arguments);
        }

        /// <summary>Launches GPU sequence argmax while retaining one compact class/confidence pair per timestep. / 启动 GPU 序列 argmax，并为每个时间步保留一个紧凑的类别/置信度对。</summary>
        public static TensorRtCudaKernelLaunch LaunchCtcTrace(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer logits,
            TensorRtCudaDeviceBuffer classIndices,
            TensorRtCudaDeviceBuffer confidences,
            TensorRtCudaDeviceBuffer invalidOffsets,
            int batch,
            int time,
            int classes,
            bool timeBatchClasses,
            bool applySoftmax,
            bool requireUnitInterval)
        {
            if (batch <= 0) throw new ArgumentOutOfRangeException(nameof(batch));
            if (time <= 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (classes <= 0) throw new ArgumentOutOfRangeException(nameof(classes));
            EnsureKernel(kernel, CtcTraceDefinition);
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(logits),
                TensorRtCudaKernelArgument.FromDeviceBuffer(classIndices),
                TensorRtCudaKernelArgument.FromDeviceBuffer(confidences),
                TensorRtCudaKernelArgument.FromDeviceBuffer(invalidOffsets),
                TensorRtCudaKernelArgument.FromInt32(batch),
                TensorRtCudaKernelArgument.FromInt32(time),
                TensorRtCudaKernelArgument.FromInt32(classes),
                TensorRtCudaKernelArgument.FromInt32(timeBatchClasses ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(applySoftmax ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(requireUnitInterval ? 1 : 0)
            };
            return kernel.Launch(stream, GridForSequences(batch), arguments);
        }

        private static TensorRtCudaKernelLaunchOptions GridFor(int workItems)
        {
            uint grid = checked((uint)Math.Max(1, (workItems + DefaultBlockSize - 1) / DefaultBlockSize));
            return new TensorRtCudaKernelLaunchOptions(grid, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged);
        }

        private static TensorRtCudaKernelLaunchOptions GridForSequences(int sequenceCount)
        {
            uint grid = checked((uint)Math.Max(1, sequenceCount));
            return new TensorRtCudaKernelLaunchOptions(grid, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged);
        }

        private static TensorRtCudaKernelLaunchOptions GridForRegionPixels(int pixelsPerRegion, int regionCount)
        {
            uint gridX = checked((uint)Math.Max(1, (pixelsPerRegion + DefaultBlockSize - 1) / DefaultBlockSize));
            uint gridY = checked((uint)Math.Max(1, regionCount));
            return new TensorRtCudaKernelLaunchOptions(gridX, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged, gridY: gridY);
        }

        private static void ValidateImageDimensions(int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight)
        {
            if (sourceWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            if (sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceHeight));
            if (destinationWidth <= 0) throw new ArgumentOutOfRangeException(nameof(destinationWidth));
            if (destinationHeight <= 0) throw new ArgumentOutOfRangeException(nameof(destinationHeight));
        }

        private static void EnsureKernel(TensorRtCudaCompiledKernel kernel, TensorRtCudaRtcKernelDefinition expected)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (!string.Equals(kernel.Artifact.KernelName, expected.KernelName, StringComparison.Ordinal) || kernel.Artifact.Role != expected.Role)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "The loaded CUDA kernel does not match the requested OCR stage.", operation: "cuda-ocr-kernel", technicalDetails: "expected=" + expected.KernelName + ";actual=" + kernel.Artifact.KernelName);
            }
        }

        private const string NormalizeLetterboxSource = @"
extern ""C"" __global__ void deploysharp_normalize_letterbox(
    const unsigned char* source, float* destination,
    int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight,
    float inverseScale, float padX, float padY,
    float padValue, float meanB, float meanG, float meanR, float scale, int swapRedBlue)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int pixels = destinationWidth * destinationHeight;
    if (index >= pixels) return;
    int x = index % destinationWidth;
    int y = index / destinationWidth;
    float sx = ((float)x + 0.5f - padX) * inverseScale - 0.5f;
    float sy = ((float)y + 0.5f - padY) * inverseScale - 0.5f;
    bool inside = sx >= 0.0f && sy >= 0.0f && sx <= (float)(sourceWidth - 1) && sy <= (float)(sourceHeight - 1);
    int x0 = max(0, min(sourceWidth - 1, (int)floorf(sx)));
    int y0 = max(0, min(sourceHeight - 1, (int)floorf(sy)));
    int x1 = min(sourceWidth - 1, x0 + 1);
    int y1 = min(sourceHeight - 1, y0 + 1);
    float ax = sx - floorf(sx);
    float ay = sy - floorf(sy);
    float values[3];
    for (int channel = 0; channel < 3; ++channel) {
        float value = padValue;
        if (inside) {
            float p00 = (float)source[(y0 * sourceWidth + x0) * 3 + channel];
            float p01 = (float)source[(y0 * sourceWidth + x1) * 3 + channel];
            float p10 = (float)source[(y1 * sourceWidth + x0) * 3 + channel];
            float p11 = (float)source[(y1 * sourceWidth + x1) * 3 + channel];
            value = (p00 + (p01 - p00) * ax) + ((p10 + (p11 - p10) * ax) - (p00 + (p01 - p00) * ax)) * ay;
        }
        values[channel] = value;
    }
    if (swapRedBlue) { float temporary = values[0]; values[0] = values[2]; values[2] = temporary; }
    destination[index] = (values[0] - meanB) * scale;
    destination[pixels + index] = (values[1] - meanG) * scale;
    destination[2 * pixels + index] = (values[2] - meanR) * scale;
}
";

        private const string HomographySource = @"
extern ""C"" __global__ void deploysharp_quad_to_homography(const float* quadrilaterals, float* homographies, int regionCount)
{
    int region = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (region >= regionCount) return;
    const float* q = quadrilaterals + region * 8;
    float x0 = q[0], y0 = q[1], x1 = q[2], y1 = q[3], x2 = q[4], y2 = q[5], x3 = q[6], y3 = q[7];
    float dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
    float dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
    float denominator = dx1 * dy2 - dx2 * dy1;
    float* h = homographies + region * 9;
    if (fabsf(dx3) < 1e-6f && fabsf(dy3) < 1e-6f) {
        h[0] = x1 - x0; h[1] = x3 - x0; h[2] = x0;
        h[3] = y1 - y0; h[4] = y3 - y0; h[5] = y0;
        h[6] = 0.0f; h[7] = 0.0f; h[8] = 1.0f;
        return;
    }
    if (fabsf(denominator) < 1e-8f) {
        h[0] = x1 - x0; h[1] = x3 - x0; h[2] = x0;
        h[3] = y1 - y0; h[4] = y3 - y0; h[5] = y0;
        h[6] = 0.0f; h[7] = 0.0f; h[8] = 1.0f;
        return;
    }
    h[6] = (dx3 * dy2 - dx2 * dy3) / denominator;
    h[7] = (dx1 * dy3 - dx3 * dy1) / denominator;
    h[0] = x1 - x0 + h[6] * x1; h[1] = x3 - x0 + h[7] * x3; h[2] = x0;
    h[3] = y1 - y0 + h[6] * y1; h[4] = y3 - y0 + h[7] * y3; h[5] = y0;
    h[8] = 1.0f;
}
";

        private const string PerspectiveCropSource = @"
extern ""C"" __global__ void deploysharp_perspective_crop(
    const unsigned char* source, const float* homographies, float* destination,
    int sourceWidth, int sourceHeight, int cropWidth, int cropHeight, int regionCount,
    float meanB, float meanG, float meanR, float scale, float paddingValue)
{
    int work = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int pixelsPerRegion = cropWidth * cropHeight;
    int total = regionCount * pixelsPerRegion;
    if (work >= total) return;
    int region = work / pixelsPerRegion;
    int pixel = work - region * pixelsPerRegion;
    int x = pixel % cropWidth;
    int y = pixel / cropWidth;
    const float* h = homographies + region * 9;
    float u = cropWidth == 1 ? 0.0f : (float)x / (float)(cropWidth - 1);
    float v = cropHeight == 1 ? 0.0f : (float)y / (float)(cropHeight - 1);
    float denominator = h[6] * u + h[7] * v + h[8];
    float sx = (h[0] * u + h[1] * v + h[2]) / denominator;
    float sy = (h[3] * u + h[4] * v + h[5]) / denominator;
    bool inside = sx >= 0.0f && sy >= 0.0f && sx <= (float)(sourceWidth - 1) && sy <= (float)(sourceHeight - 1);
    int x0 = max(0, min(sourceWidth - 1, (int)floorf(sx)));
    int y0 = max(0, min(sourceHeight - 1, (int)floorf(sy)));
    int x1 = min(sourceWidth - 1, x0 + 1);
    int y1 = min(sourceHeight - 1, y0 + 1);
    float ax = sx - floorf(sx), ay = sy - floorf(sy);
    float values[3];
    for (int channel = 0; channel < 3; ++channel) {
        float value = paddingValue;
        if (inside) {
            float p00 = (float)source[(y0 * sourceWidth + x0) * 3 + channel];
            float p01 = (float)source[(y0 * sourceWidth + x1) * 3 + channel];
            float p10 = (float)source[(y1 * sourceWidth + x0) * 3 + channel];
            float p11 = (float)source[(y1 * sourceWidth + x1) * 3 + channel];
            float top = p00 + (p01 - p00) * ax, bottom = p10 + (p11 - p10) * ax;
            value = top + (bottom - top) * ay;
        }
        values[channel] = value;
    }
    int outputBase = region * 3 * pixelsPerRegion + pixel;
    destination[outputBase] = (values[0] - meanB) * scale;
    destination[region * 3 * pixelsPerRegion + pixelsPerRegion + pixel] = (values[1] - meanG) * scale;
    destination[region * 3 * pixelsPerRegion + 2 * pixelsPerRegion + pixel] = (values[2] - meanR) * scale;
}
";

        private const string PerspectiveCropFromQuadrilateralSource = @"
extern ""C"" __global__ void deploysharp_perspective_crop_quad(
    const unsigned char* source, const float* quadrilaterals, float* destination,
    int sourceWidth, int sourceHeight, int cropWidth, int cropHeight, int regionCount,
    float meanB, float meanG, float meanR, float scale, float paddingValue)
{
    int region = (int)blockIdx.y;
    int pixel = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int pixelsPerRegion = cropWidth * cropHeight;
    if (region >= regionCount) return;
    bool active = pixel < pixelsPerRegion;
    __shared__ float homography[8];
    const float* q = quadrilaterals + region * 8;
    if (threadIdx.x == 0) {
        float x0 = q[0], y0 = q[1], x1 = q[2], y1 = q[3], x2 = q[4], y2 = q[5], x3 = q[6], y3 = q[7];
        float dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        float dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
        float determinant = dx1 * dy2 - dx2 * dy1;
        float h0, h1, h2, h3, h4, h5, h6, h7;
        if ((fabsf(dx3) < 1e-6f && fabsf(dy3) < 1e-6f) || fabsf(determinant) < 1e-8f) {
            h0 = x1 - x0; h1 = x3 - x0; h2 = x0;
            h3 = y1 - y0; h4 = y3 - y0; h5 = y0; h6 = 0.0f; h7 = 0.0f;
        } else {
            h6 = (dx3 * dy2 - dx2 * dy3) / determinant;
            h7 = (dx1 * dy3 - dx3 * dy1) / determinant;
            h0 = x1 - x0 + h6 * x1; h1 = x3 - x0 + h7 * x3; h2 = x0;
            h3 = y1 - y0 + h6 * y1; h4 = y3 - y0 + h7 * y3; h5 = y0;
        }
        homography[0] = h0; homography[1] = h1; homography[2] = h2; homography[3] = h3;
        homography[4] = h4; homography[5] = h5; homography[6] = h6; homography[7] = h7;
    }
    __syncthreads();
    if (!active) return;
    int x = pixel % cropWidth;
    int y = pixel / cropWidth;
    float u = cropWidth == 1 ? 0.0f : (float)x / (float)(cropWidth - 1);
    float v = cropHeight == 1 ? 0.0f : (float)y / (float)(cropHeight - 1);
    float mapDenominator = homography[6] * u + homography[7] * v + 1.0f;
    float sx = (homography[0] * u + homography[1] * v + homography[2]) / mapDenominator;
    float sy = (homography[3] * u + homography[4] * v + homography[5]) / mapDenominator;
    bool inside = sx >= 0.0f && sy >= 0.0f && sx <= (float)(sourceWidth - 1) && sy <= (float)(sourceHeight - 1);
    int x0i = max(0, min(sourceWidth - 1, (int)floorf(sx)));
    int y0i = max(0, min(sourceHeight - 1, (int)floorf(sy)));
    int x1i = min(sourceWidth - 1, x0i + 1);
    int y1i = min(sourceHeight - 1, y0i + 1);
    float ax = sx - floorf(sx), ay = sy - floorf(sy);
    int sourceBase00 = (y0i * sourceWidth + x0i) * 3;
    int sourceBase01 = (y0i * sourceWidth + x1i) * 3;
    int sourceBase10 = (y1i * sourceWidth + x0i) * 3;
    int sourceBase11 = (y1i * sourceWidth + x1i) * 3;
    float values[3];
    for (int channel = 0; channel < 3; ++channel) {
        float value = paddingValue;
        if (inside) {
            float p00 = (float)source[sourceBase00 + channel];
            float p01 = (float)source[sourceBase01 + channel];
            float p10 = (float)source[sourceBase10 + channel];
            float p11 = (float)source[sourceBase11 + channel];
            float top = p00 + (p01 - p00) * ax, bottom = p10 + (p11 - p10) * ax;
            value = top + (bottom - top) * ay;
        }
        values[channel] = value;
    }
    int outputBase = region * 3 * pixelsPerRegion + pixel;
    destination[outputBase] = (values[0] - meanB) * scale;
    destination[region * 3 * pixelsPerRegion + pixelsPerRegion + pixel] = (values[1] - meanG) * scale;
    destination[region * 3 * pixelsPerRegion + 2 * pixelsPerRegion + pixel] = (values[2] - meanR) * scale;
}
";

        private const string CtcDecodeSource = @"
extern ""C"" __global__ void deploysharp_ctc_decode(
    const float* logits, int* tokenIds, int* lengths, float* confidences,
    int batch, int time, int classes, int blankIndex, int maximumTokens,
    int applySoftmax, int collapseRepeats)
{
    // One block owns a sequence. Threads scan the vocabulary in parallel for
    // each timestep; thread zero then performs the ordered CTC collapse.
    __shared__ float reductionValues[256];
    __shared__ int reductionIndices[256];
    int sequence = (int)blockIdx.x;
    int lane = (int)threadIdx.x;
    if (sequence >= batch) return;
    int* output = tokenIds + sequence * maximumTokens;
    if (lane == 0) {
        for (int i = 0; i < maximumTokens; ++i) output[i] = -1;
    }
    int previous = -1, emitted = 0;
    float confidenceSum = 0.0f;
    for (int timestep = 0; timestep < time; ++timestep) {
        const float* row = logits + (sequence * time + timestep) * classes;
        // Keep the source self-contained for NVRTC installations that do not
        // expose CUDA runtime headers/macros to the caller.
        float localMaximum = -3.402823466e+38F;
        int localIndex = 0;
        for (int c = lane; c < classes; c += (int)blockDim.x) {
            float value = row[c];
            if (value > localMaximum || (value == localMaximum && c < localIndex)) {
                localMaximum = value;
                localIndex = c;
            }
        }
        reductionValues[lane] = localMaximum;
        reductionIndices[lane] = localIndex;
        __syncthreads();
        for (int stride = (int)blockDim.x / 2; stride > 0; stride >>= 1) {
            if (lane < stride) {
                float otherValue = reductionValues[lane + stride];
                int otherIndex = reductionIndices[lane + stride];
                if (otherValue > reductionValues[lane] ||
                    (otherValue == reductionValues[lane] && otherIndex < reductionIndices[lane])) {
                    reductionValues[lane] = otherValue;
                    reductionIndices[lane] = otherIndex;
                }
            }
            __syncthreads();
        }
        int selected = reductionIndices[0];
        float selectedValue = reductionValues[0];
        float confidence = selectedValue;
        if (applySoftmax) {
            float localSum = 0.0f;
            for (int c = lane; c < classes; c += (int)blockDim.x) localSum += expf(row[c] - selectedValue);
            reductionValues[lane] = localSum;
            __syncthreads();
            for (int stride = (int)blockDim.x / 2; stride > 0; stride >>= 1) {
                if (lane < stride) reductionValues[lane] += reductionValues[lane + stride];
                __syncthreads();
            }
            confidence = 1.0f / fmaxf(reductionValues[0], 1e-20f);
        }
        if (lane == 0) {
            bool blank = selected == blankIndex;
            bool repeated = collapseRepeats != 0 && !blank && selected == previous;
            if (!blank && !repeated) {
                if (emitted < maximumTokens) output[emitted] = selected;
                ++emitted;
                confidenceSum += confidence;
            }
            previous = selected;
        }
        __syncthreads();
    }
    if (lane == 0) {
        lengths[sequence] = emitted > maximumTokens ? maximumTokens : emitted;
        confidences[sequence] = emitted == 0 ? 0.0f : confidenceSum / (float)emitted;
    }
}
";

        private const string CtcTraceSource = @"
extern ""C"" __global__ void deploysharp_ctc_trace(
    const float* logits, int* classIndices, float* confidences, int* invalidOffsets,
    int batch, int time, int classes, int timeBatchClasses,
    int applySoftmax, int requireUnitInterval)
{
    __shared__ float reductionValues[256];
    __shared__ int reductionIndices[256];
    __shared__ int reductionInvalid[256];
    int sequence = (int)blockIdx.x;
    int lane = (int)threadIdx.x;
    if (sequence >= batch) return;
    if (lane == 0) invalidOffsets[sequence] = -1;
    __syncthreads();
    for (int timestep = 0; timestep < time; ++timestep) {
        const float* row = timeBatchClasses != 0
            ? logits + (timestep * batch + sequence) * classes
            : logits + (sequence * time + timestep) * classes;
        float localMaximum = -3.402823466e+38F;
        int localIndex = classes;
        int localInvalid = classes;
        for (int c = lane; c < classes; c += (int)blockDim.x) {
            float value = row[c];
            bool finite = value >= -3.402823466e+38F && value <= 3.402823466e+38F;
            bool valid = finite && (requireUnitInterval == 0 || (value >= 0.0f && value <= 1.0f));
            if (!valid && c < localInvalid) localInvalid = c;
            if (value > localMaximum || (value == localMaximum && c < localIndex)) {
                localMaximum = value;
                localIndex = c;
            }
        }
        reductionValues[lane] = localMaximum;
        reductionIndices[lane] = localIndex;
        reductionInvalid[lane] = localInvalid;
        __syncthreads();
        for (int stride = (int)blockDim.x / 2; stride > 0; stride >>= 1) {
            if (lane < stride) {
                float otherValue = reductionValues[lane + stride];
                int otherIndex = reductionIndices[lane + stride];
                if (otherValue > reductionValues[lane] ||
                    (otherValue == reductionValues[lane] && otherIndex < reductionIndices[lane])) {
                    reductionValues[lane] = otherValue;
                    reductionIndices[lane] = otherIndex;
                }
                int otherInvalid = reductionInvalid[lane + stride];
                if (otherInvalid < reductionInvalid[lane]) reductionInvalid[lane] = otherInvalid;
            }
            __syncthreads();
        }
        int selected = reductionIndices[0];
        float selectedValue = reductionValues[0];
        float confidence = selectedValue;
        if (applySoftmax != 0) {
            float localSum = 0.0f;
            for (int c = lane; c < classes; c += (int)blockDim.x) localSum += expf(row[c] - selectedValue);
            reductionValues[lane] = localSum;
            __syncthreads();
            for (int stride = (int)blockDim.x / 2; stride > 0; stride >>= 1) {
                if (lane < stride) reductionValues[lane] += reductionValues[lane + stride];
                __syncthreads();
            }
            confidence = 1.0f / fmaxf(reductionValues[0], 1e-20f);
        }
        if (lane == 0) {
            int outputOffset = sequence * time + timestep;
            classIndices[outputOffset] = selected;
            confidences[outputOffset] = confidence;
            if (invalidOffsets[sequence] < 0 && reductionInvalid[0] < classes) {
                invalidOffsets[sequence] = timestep * classes + reductionInvalid[0];
            }
        }
        __syncthreads();
    }
}
";
    }
}
