using System;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Owns an immutable, reusable argument plan for one fixed-shape CUDA visual preprocessing path. / 拥有一个固定形状 CUDA 视觉预处理路径的不可变可复用参数计划。</summary>
    public sealed class TensorRtCudaVisualPreprocessPlan
    {
        private readonly TensorRtCudaCompiledKernel _kernel;
        private readonly TensorRtCudaPreparedLaunch _prepared;

        internal TensorRtCudaVisualPreprocessPlan(TensorRtCudaCompiledKernel kernel, TensorRtCudaPreparedLaunch prepared)
        {
            _kernel = kernel;
            _prepared = prepared;
        }

        /// <summary>Enqueues the cached fixed-shape preprocessing arguments on the caller-owned stream. / 在调用方拥有的 stream 上将缓存的固定形状预处理参数入队。</summary>
        public TensorRtCudaKernelLaunch Launch(CudaStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return _kernel.LaunchPrepared(stream, _prepared);
        }
    }

    /// <summary>Owns an immutable, reusable argument plan for one fixed-shape CUDA single-channel map restoration. / 拥有一个固定形状 CUDA 单通道图恢复路径的不可变可复用参数计划。</summary>
    public sealed class TensorRtCudaVisualMapRestorePlan
    {
        private readonly TensorRtCudaCompiledKernel _kernel;
        private readonly TensorRtCudaPreparedLaunch _prepared;

        internal TensorRtCudaVisualMapRestorePlan(TensorRtCudaCompiledKernel kernel, TensorRtCudaPreparedLaunch prepared)
        {
            _kernel = kernel;
            _prepared = prepared;
        }

        /// <summary>Enqueues cached validation, bilinear restoration, and optional thresholding on the caller-owned stream. / 在调用方拥有的 stream 上将缓存的校验、双线性恢复与可选阈值化操作入队。</summary>
        public TensorRtCudaKernelLaunch Launch(CudaStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return _kernel.LaunchPrepared(stream, _prepared);
        }
    }

    /// <summary>Owns reusable launches for YOLO prototype combination and source-space mask restoration. / 拥有 YOLO 原型组合和源图掩码恢复的可复用启动计划。</summary>
    public sealed class TensorRtCudaYoloMaskPlan
    {
        private readonly TensorRtCudaCompiledKernel _combineKernel;
        private readonly TensorRtCudaCompiledKernel _restoreKernel;
        private readonly TensorRtCudaPreparedLaunch _combinePrepared;
        private readonly TensorRtCudaPreparedLaunch _restorePrepared;

        internal TensorRtCudaYoloMaskPlan(TensorRtCudaCompiledKernel combineKernel, TensorRtCudaCompiledKernel restoreKernel, TensorRtCudaPreparedLaunch combinePrepared, TensorRtCudaPreparedLaunch restorePrepared)
        {
            _combineKernel = combineKernel;
            _restoreKernel = restoreKernel;
            _combinePrepared = combinePrepared;
            _restorePrepared = restorePrepared;
        }

        /// <summary>Enqueues prototype combination followed by mask restoration on one caller-owned stream. / 在同一个调用方 stream 上依次将原型组合和掩码恢复入队。</summary>
        public TensorRtCudaYoloMaskLaunch Launch(CudaStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            TensorRtCudaKernelLaunch combine = _combineKernel.LaunchPrepared(stream, _combinePrepared);
            try
            {
                TensorRtCudaKernelLaunch restore = _restoreKernel.LaunchPrepared(stream, _restorePrepared);
                return new TensorRtCudaYoloMaskLaunch(combine, restore);
            }
            catch
            {
                combine.Dispose();
                throw;
            }
        }
    }

    /// <summary>Owns the two enqueued YOLO mask kernel launches. / 拥有两个已入队的 YOLO 掩码 kernel 启动。</summary>
    public sealed class TensorRtCudaYoloMaskLaunch : IDisposable
    {
        private TensorRtCudaKernelLaunch? _combine;
        private TensorRtCudaKernelLaunch? _restore;

        internal TensorRtCudaYoloMaskLaunch(TensorRtCudaKernelLaunch combine, TensorRtCudaKernelLaunch restore)
        {
            _combine = combine;
            _restore = restore;
        }

        /// <summary>Releases both enqueued kernel-launch owners. / 释放两个已入队 kernel 的启动所有者。</summary>
        public void Dispose()
        {
            _restore?.Dispose();
            _restore = null;
            _combine?.Dispose();
            _combine = null;
        }
    }

    /// <summary>Owns a reusable YOLO candidate validation and threshold-filter launch. / 拥有可复用 YOLO 候选校验和阈值筛选启动。</summary>
    public sealed class TensorRtCudaYoloCandidatePlan
    {
        private readonly TensorRtCudaCompiledKernel _kernel;
        private readonly TensorRtCudaPreparedLaunch _prepared;

        internal TensorRtCudaYoloCandidatePlan(TensorRtCudaCompiledKernel kernel, TensorRtCudaPreparedLaunch prepared)
        {
            _kernel = kernel;
            _prepared = prepared;
        }

        /// <summary>Enqueues candidate validation and filtering on the caller-owned stream. / 在调用方拥有的 stream 上将候选校验和筛选入队。</summary>
        public TensorRtCudaKernelLaunch Launch(CudaStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return _kernel.LaunchPrepared(stream, _prepared);
        }
    }

    /// <summary>Provides fused CUDA preprocessing for TensorRT visual pipelines. / 为 TensorRT 视觉流水线提供融合 CUDA 预处理。</summary>
    public static class TensorRtCudaVisualKernels
    {
        private const int DefaultBlockSize = 256;

        /// <summary>Gets the fused packed-BGR resize, padding, channel conversion, and NCHW normalization definition. / 获取融合的紧凑 BGR 缩放、填充、通道转换与 NCHW 归一化定义。</summary>
        public static TensorRtCudaRtcKernelDefinition NormalizeBgrNchwDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Preprocessing,
            NormalizeBgrNchwSource,
            "deploysharp_visual_normalize_bgr_nchw",
            "deploysharp-visual-preprocess.cu");

        /// <summary>Gets the fused single-channel validation, source restoration, and threshold definition. / 获取融合的单通道校验、源图恢复与阈值化定义。</summary>
        public static TensorRtCudaRtcKernelDefinition RestoreSingleChannelMapDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Postprocessing,
            RestoreSingleChannelMapSource,
            "deploysharp_visual_restore_single_channel_map",
            "deploysharp-visual-map-postprocess.cu");

        /// <summary>Gets the YOLO prototype-combination definition. / 获取 YOLO 原型组合定义。</summary>
        public static TensorRtCudaRtcKernelDefinition CombineYoloPrototypeMasksDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Postprocessing,
            YoloPrototypeMaskSource,
            "deploysharp_visual_combine_yolo_prototypes",
            "deploysharp-visual-yolo-mask-postprocess.cu");

        /// <summary>Gets the YOLO source-space mask-restoration definition. / 获取 YOLO 源图掩码恢复定义。</summary>
        public static TensorRtCudaRtcKernelDefinition RestoreYoloPrototypeMasksDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Postprocessing,
            YoloPrototypeMaskSource,
            "deploysharp_visual_restore_yolo_masks",
            "deploysharp-visual-yolo-mask-postprocess.cu");

        /// <summary>Gets the YOLO packed-candidate validation and threshold-filter definition. / 获取 YOLO 打包候选校验和阈值筛选定义。</summary>
        public static TensorRtCudaRtcKernelDefinition FilterYoloCandidatesDefinition { get; } = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Postprocessing,
            YoloCandidateFilterSource,
            "deploysharp_visual_filter_yolo_candidates",
            "deploysharp-visual-yolo-candidate-filter.cu");

        /// <summary>Enqueues fused visual preprocessing without synchronizing the caller-owned stream. / 在不同步调用方 stream 的情况下将融合视觉预处理入队。</summary>
        public static TensorRtCudaKernelLaunch LaunchNormalizeBgrNchw(
            TensorRtCudaCompiledKernel kernel,
            CudaStream stream,
            TensorRtCudaDeviceBuffer sourceBgr,
            TensorRtCudaDeviceBuffer destinationNchw,
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight,
            int resizedWidth,
            int resizedHeight,
            int paddingLeft,
            int paddingTop,
            float paddingBlue,
            float paddingGreen,
            float paddingRed,
            float mean0,
            float mean1,
            float mean2,
            float scale0,
            float scale1,
            float scale2,
            bool swapRedBlue)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return CreateNormalizeBgrNchwPlan(
                kernel, sourceBgr, destinationNchw, sourceWidth, sourceHeight, destinationWidth, destinationHeight,
                resizedWidth, resizedHeight, paddingLeft, paddingTop, paddingBlue, paddingGreen, paddingRed,
                mean0, mean1, mean2, scale0, scale1, scale2, swapRedBlue).Launch(stream);
        }

        /// <summary>Creates a reusable fixed-shape fused visual preprocessing launch plan. / 创建可复用的固定形状融合视觉预处理启动计划。</summary>
        public static TensorRtCudaVisualPreprocessPlan CreateNormalizeBgrNchwPlan(
            TensorRtCudaCompiledKernel kernel,
            TensorRtCudaDeviceBuffer sourceBgr,
            TensorRtCudaDeviceBuffer destinationNchw,
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight,
            int resizedWidth,
            int resizedHeight,
            int paddingLeft,
            int paddingTop,
            float paddingBlue,
            float paddingGreen,
            float paddingRed,
            float mean0,
            float mean1,
            float mean2,
            float scale0,
            float scale1,
            float scale2,
            bool swapRedBlue)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (sourceBgr == null) throw new ArgumentNullException(nameof(sourceBgr));
            if (destinationNchw == null) throw new ArgumentNullException(nameof(destinationNchw));
            if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            if (resizedWidth <= 0 || resizedHeight <= 0 || paddingLeft < 0 || paddingTop < 0 || paddingLeft + resizedWidth > destinationWidth || paddingTop + resizedHeight > destinationHeight) throw new ArgumentOutOfRangeException(nameof(resizedWidth));
            if (!string.Equals(kernel.Artifact.KernelName, NormalizeBgrNchwDefinition.KernelName, StringComparison.Ordinal))
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "The loaded CUDA kernel does not match visual preprocessing.", operation: "cuda-visual-kernel", technicalDetails: "actual=" + kernel.Artifact.KernelName);
            }
            EnsureFinite(paddingBlue, nameof(paddingBlue));
            EnsureFinite(paddingGreen, nameof(paddingGreen));
            EnsureFinite(paddingRed, nameof(paddingRed));
            EnsureFinite(mean0, nameof(mean0));
            EnsureFinite(mean1, nameof(mean1));
            EnsureFinite(mean2, nameof(mean2));
            EnsureFinitePositive(scale0, nameof(scale0));
            EnsureFinitePositive(scale1, nameof(scale1));
            EnsureFinitePositive(scale2, nameof(scale2));

            float inverseScaleX = (float)sourceWidth / resizedWidth;
            float inverseScaleY = (float)sourceHeight / resizedHeight;
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(sourceBgr),
                TensorRtCudaKernelArgument.FromDeviceBuffer(destinationNchw),
                TensorRtCudaKernelArgument.FromInt32(sourceWidth),
                TensorRtCudaKernelArgument.FromInt32(sourceHeight),
                TensorRtCudaKernelArgument.FromInt32(destinationWidth),
                TensorRtCudaKernelArgument.FromInt32(destinationHeight),
                TensorRtCudaKernelArgument.FromInt32(resizedWidth),
                TensorRtCudaKernelArgument.FromInt32(resizedHeight),
                TensorRtCudaKernelArgument.FromInt32(paddingLeft),
                TensorRtCudaKernelArgument.FromInt32(paddingTop),
                TensorRtCudaKernelArgument.FromSingle(inverseScaleX),
                TensorRtCudaKernelArgument.FromSingle(inverseScaleY),
                TensorRtCudaKernelArgument.FromSingle(paddingBlue),
                TensorRtCudaKernelArgument.FromSingle(paddingGreen),
                TensorRtCudaKernelArgument.FromSingle(paddingRed),
                TensorRtCudaKernelArgument.FromSingle(mean0),
                TensorRtCudaKernelArgument.FromSingle(mean1),
                TensorRtCudaKernelArgument.FromSingle(mean2),
                TensorRtCudaKernelArgument.FromSingle(scale0),
                TensorRtCudaKernelArgument.FromSingle(scale1),
                TensorRtCudaKernelArgument.FromSingle(scale2),
                TensorRtCudaKernelArgument.FromInt32(swapRedBlue ? 1 : 0)
            };
            int pixels = checked(destinationWidth * destinationHeight);
            uint gridX = checked((uint)((pixels + DefaultBlockSize - 1) / DefaultBlockSize));
            var options = new TensorRtCudaKernelLaunchOptions(gridX, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged);
            return new TensorRtCudaVisualPreprocessPlan(kernel, kernel.PrepareLaunch(options, arguments));
        }

        /// <summary>Creates a reusable fixed-shape plan that validates and restores one Float32 map and optionally emits a binary mask. / 创建一个可复用固定形状计划，用于校验并恢复单个 Float32 图，并可选择输出二值掩码。</summary>
        public static TensorRtCudaVisualMapRestorePlan CreateRestoreSingleChannelMapPlan(
            TensorRtCudaCompiledKernel kernel,
            TensorRtCudaDeviceBuffer sourceMap,
            TensorRtCudaDeviceBuffer restoredMap,
            TensorRtCudaDeviceBuffer binaryMask,
            TensorRtCudaDeviceBuffer invalidFlag,
            TensorRtCudaDeviceBuffer positiveCount,
            int tensorWidth,
            int tensorHeight,
            int modelWidth,
            int modelHeight,
            int sourceWidth,
            int sourceHeight,
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY,
            float threshold,
            bool applySigmoid,
            bool validateProbability,
            bool writeMask)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (sourceMap == null) throw new ArgumentNullException(nameof(sourceMap));
            if (restoredMap == null) throw new ArgumentNullException(nameof(restoredMap));
            if (binaryMask == null) throw new ArgumentNullException(nameof(binaryMask));
            if (invalidFlag == null) throw new ArgumentNullException(nameof(invalidFlag));
            if (positiveCount == null) throw new ArgumentNullException(nameof(positiveCount));
            if (tensorWidth <= 0 || tensorHeight <= 0 || modelWidth <= 0 || modelHeight <= 0 || sourceWidth <= 0 || sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(tensorWidth));
            if (tensorWidth != modelWidth || tensorHeight != modelHeight) throw new NotSupportedException("CUDA map restoration currently requires tensor and model spatial dimensions to match.");
            if (!string.Equals(kernel.Artifact.KernelName, RestoreSingleChannelMapDefinition.KernelName, StringComparison.Ordinal))
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "The loaded CUDA kernel does not match visual map postprocessing.", operation: "cuda-visual-kernel", technicalDetails: "actual=" + kernel.Artifact.KernelName);
            }
            EnsureBuffer(sourceMap, TensorElementType.Float32, tensorWidth, tensorHeight, TensorRtCudaBufferAccess.Read, nameof(sourceMap));
            EnsureBuffer(restoredMap, TensorElementType.Float32, sourceWidth, sourceHeight, TensorRtCudaBufferAccess.Write, nameof(restoredMap));
            if (writeMask) EnsureBuffer(binaryMask, TensorElementType.UInt8, sourceWidth, sourceHeight, TensorRtCudaBufferAccess.Write, nameof(binaryMask));
            else EnsureScalarBuffer(binaryMask, TensorElementType.UInt8, TensorRtCudaBufferAccess.Write, nameof(binaryMask));
            EnsureScalarBuffer(invalidFlag, TensorElementType.Int32, TensorRtCudaBufferAccess.ReadWrite, nameof(invalidFlag));
            EnsureScalarBuffer(positiveCount, TensorElementType.Int32, TensorRtCudaBufferAccess.ReadWrite, nameof(positiveCount));
            EnsureFinitePositive(scaleX, nameof(scaleX));
            EnsureFinitePositive(scaleY, nameof(scaleY));
            EnsureFinite(offsetX, nameof(offsetX));
            EnsureFinite(offsetY, nameof(offsetY));
            EnsureFinite(threshold, nameof(threshold));

            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(sourceMap),
                TensorRtCudaKernelArgument.FromDeviceBuffer(restoredMap),
                TensorRtCudaKernelArgument.FromDeviceBuffer(binaryMask),
                TensorRtCudaKernelArgument.FromDeviceBuffer(invalidFlag),
                TensorRtCudaKernelArgument.FromDeviceBuffer(positiveCount),
                TensorRtCudaKernelArgument.FromInt32(tensorWidth),
                TensorRtCudaKernelArgument.FromInt32(tensorHeight),
                TensorRtCudaKernelArgument.FromInt32(modelWidth),
                TensorRtCudaKernelArgument.FromInt32(modelHeight),
                TensorRtCudaKernelArgument.FromInt32(sourceWidth),
                TensorRtCudaKernelArgument.FromInt32(sourceHeight),
                TensorRtCudaKernelArgument.FromSingle(scaleX),
                TensorRtCudaKernelArgument.FromSingle(scaleY),
                TensorRtCudaKernelArgument.FromSingle(offsetX),
                TensorRtCudaKernelArgument.FromSingle(offsetY),
                TensorRtCudaKernelArgument.FromSingle(threshold),
                TensorRtCudaKernelArgument.FromInt32(applySigmoid ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(validateProbability ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(writeMask ? 1 : 0)
            };
            int workItems = Math.Max(checked(tensorWidth * tensorHeight), checked(sourceWidth * sourceHeight));
            uint gridX = checked((uint)((workItems + DefaultBlockSize - 1) / DefaultBlockSize));
            var options = new TensorRtCudaKernelLaunchOptions(gridX, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged);
            return new TensorRtCudaVisualMapRestorePlan(kernel, kernel.PrepareLaunch(options, arguments));
        }

        /// <summary>Creates reusable fixed-shape YOLO prototype-combination and source-mask restoration launches. / 创建可复用固定形状 YOLO 原型组合和源图掩码恢复启动计划。</summary>
        public static TensorRtCudaYoloMaskPlan CreateYoloPrototypeMaskPlan(
            TensorRtCudaCompiledKernel combineKernel,
            TensorRtCudaCompiledKernel restoreKernel,
            TensorRtCudaDeviceBuffer prototypes,
            TensorRtCudaDeviceBuffer coefficients,
            TensorRtCudaDeviceBuffer modelBoxes,
            TensorRtCudaDeviceBuffer activatedMasks,
            TensorRtCudaDeviceBuffer sourceMasks,
            TensorRtCudaDeviceBuffer invalidFlag,
            TensorRtCudaDeviceBuffer positiveCounts,
            int instanceCount,
            int channels,
            int prototypeWidth,
            int prototypeHeight,
            int modelWidth,
            int modelHeight,
            int sourceWidth,
            int sourceHeight,
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY,
            float threshold)
        {
            if (combineKernel == null) throw new ArgumentNullException(nameof(combineKernel));
            if (restoreKernel == null) throw new ArgumentNullException(nameof(restoreKernel));
            if (prototypes == null || coefficients == null || modelBoxes == null || activatedMasks == null || sourceMasks == null || invalidFlag == null || positiveCounts == null) throw new ArgumentNullException(nameof(prototypes));
            if (instanceCount <= 0 || channels <= 0 || prototypeWidth <= 0 || prototypeHeight <= 0 || modelWidth <= 0 || modelHeight <= 0 || sourceWidth <= 0 || sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(instanceCount));
            if (!string.Equals(combineKernel.Artifact.KernelName, CombineYoloPrototypeMasksDefinition.KernelName, StringComparison.Ordinal)
                || !string.Equals(restoreKernel.Artifact.KernelName, RestoreYoloPrototypeMasksDefinition.KernelName, StringComparison.Ordinal))
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "The loaded CUDA kernels do not match YOLO mask postprocessing.", operation: "cuda-visual-kernel");
            }
            EnsureElements(prototypes, TensorElementType.Float32, checked((long)channels * prototypeWidth * prototypeHeight), TensorRtCudaBufferAccess.Read, nameof(prototypes));
            EnsureElements(coefficients, TensorElementType.Float32, checked((long)instanceCount * channels), TensorRtCudaBufferAccess.Read, nameof(coefficients));
            EnsureElements(modelBoxes, TensorElementType.Float32, checked((long)instanceCount * 4), TensorRtCudaBufferAccess.Read, nameof(modelBoxes));
            EnsureElements(activatedMasks, TensorElementType.Float32, checked((long)instanceCount * prototypeWidth * prototypeHeight), TensorRtCudaBufferAccess.Write, nameof(activatedMasks));
            EnsureElements(sourceMasks, TensorElementType.UInt8, checked((long)instanceCount * sourceWidth * sourceHeight), TensorRtCudaBufferAccess.Write, nameof(sourceMasks));
            EnsureScalarBuffer(invalidFlag, TensorElementType.Int32, TensorRtCudaBufferAccess.ReadWrite, nameof(invalidFlag));
            EnsureElements(positiveCounts, TensorElementType.Int32, instanceCount, TensorRtCudaBufferAccess.ReadWrite, nameof(positiveCounts));
            EnsureFinitePositive(scaleX, nameof(scaleX));
            EnsureFinitePositive(scaleY, nameof(scaleY));
            EnsureFinite(offsetX, nameof(offsetX));
            EnsureFinite(offsetY, nameof(offsetY));
            EnsureFinite(threshold, nameof(threshold));

            var combineArguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(prototypes),
                TensorRtCudaKernelArgument.FromDeviceBuffer(coefficients),
                TensorRtCudaKernelArgument.FromDeviceBuffer(modelBoxes),
                TensorRtCudaKernelArgument.FromDeviceBuffer(activatedMasks),
                TensorRtCudaKernelArgument.FromDeviceBuffer(invalidFlag),
                TensorRtCudaKernelArgument.FromInt32(channels),
                TensorRtCudaKernelArgument.FromInt32(prototypeWidth),
                TensorRtCudaKernelArgument.FromInt32(prototypeHeight),
                TensorRtCudaKernelArgument.FromInt32(modelWidth),
                TensorRtCudaKernelArgument.FromInt32(modelHeight)
            };
            uint combineGridX = checked((uint)((checked(prototypeWidth * prototypeHeight) + DefaultBlockSize - 1) / DefaultBlockSize));
            var combineOptions = new TensorRtCudaKernelLaunchOptions(combineGridX, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged, gridY: checked((uint)instanceCount));

            var restoreArguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(activatedMasks),
                TensorRtCudaKernelArgument.FromDeviceBuffer(modelBoxes),
                TensorRtCudaKernelArgument.FromDeviceBuffer(sourceMasks),
                TensorRtCudaKernelArgument.FromDeviceBuffer(positiveCounts),
                TensorRtCudaKernelArgument.FromInt32(prototypeWidth),
                TensorRtCudaKernelArgument.FromInt32(prototypeHeight),
                TensorRtCudaKernelArgument.FromInt32(modelWidth),
                TensorRtCudaKernelArgument.FromInt32(modelHeight),
                TensorRtCudaKernelArgument.FromInt32(sourceWidth),
                TensorRtCudaKernelArgument.FromInt32(sourceHeight),
                TensorRtCudaKernelArgument.FromSingle(scaleX),
                TensorRtCudaKernelArgument.FromSingle(scaleY),
                TensorRtCudaKernelArgument.FromSingle(offsetX),
                TensorRtCudaKernelArgument.FromSingle(offsetY),
                TensorRtCudaKernelArgument.FromSingle(threshold)
            };
            uint restoreGridX = checked((uint)((checked(sourceWidth * sourceHeight) + DefaultBlockSize - 1) / DefaultBlockSize));
            var restoreOptions = new TensorRtCudaKernelLaunchOptions(restoreGridX, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged, gridY: checked((uint)instanceCount), dynamicSharedMemoryBytes: sizeof(int));
            return new TensorRtCudaYoloMaskPlan(combineKernel, restoreKernel, combineKernel.PrepareLaunch(combineOptions, combineArguments), restoreKernel.PrepareLaunch(restoreOptions, restoreArguments));
        }

        /// <summary>Creates a reusable YOLO packed-candidate validation and threshold-filter plan. / 创建可复用 YOLO 打包候选校验和阈值筛选计划。</summary>
        public static TensorRtCudaYoloCandidatePlan CreateYoloCandidateFilterPlan(
            TensorRtCudaCompiledKernel kernel,
            TensorRtCudaDeviceBuffer packed,
            TensorRtCudaDeviceBuffer selectedFlags,
            TensorRtCudaDeviceBuffer classIndices,
            TensorRtCudaDeviceBuffer scores,
            TensorRtCudaDeviceBuffer boxes,
            TensorRtCudaDeviceBuffer coefficients,
            TensorRtCudaDeviceBuffer invalidFlag,
            int candidateCount,
            int fieldCount,
            int classCount,
            int coefficientCount,
            bool attributeMajor,
            bool hasObjectness,
            bool endToEnd,
            float scoreThreshold)
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));
            if (packed == null || selectedFlags == null || classIndices == null || scores == null || boxes == null || coefficients == null || invalidFlag == null) throw new ArgumentNullException(nameof(packed));
            if (candidateCount <= 0 || fieldCount <= 0 || classCount <= 0 || coefficientCount <= 0) throw new ArgumentOutOfRangeException(nameof(candidateCount));
            if (!string.Equals(kernel.Artifact.KernelName, FilterYoloCandidatesDefinition.KernelName, StringComparison.Ordinal))
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.CudaContractInvalid, "The loaded CUDA kernel does not match YOLO candidate filtering.", operation: "cuda-visual-kernel");
            }
            EnsureElements(packed, TensorElementType.Float32, checked((long)candidateCount * fieldCount), TensorRtCudaBufferAccess.Read, nameof(packed));
            EnsureElements(selectedFlags, TensorElementType.UInt8, candidateCount, TensorRtCudaBufferAccess.Write, nameof(selectedFlags));
            EnsureElements(classIndices, TensorElementType.Int32, candidateCount, TensorRtCudaBufferAccess.Write, nameof(classIndices));
            EnsureElements(scores, TensorElementType.Float32, candidateCount, TensorRtCudaBufferAccess.Write, nameof(scores));
            EnsureElements(boxes, TensorElementType.Float32, checked((long)candidateCount * 4), TensorRtCudaBufferAccess.Write, nameof(boxes));
            EnsureElements(coefficients, TensorElementType.Float32, checked((long)candidateCount * coefficientCount), TensorRtCudaBufferAccess.Write, nameof(coefficients));
            EnsureScalarBuffer(invalidFlag, TensorElementType.Int32, TensorRtCudaBufferAccess.ReadWrite, nameof(invalidFlag));
            EnsureFinite(scoreThreshold, nameof(scoreThreshold));
            var arguments = new[]
            {
                TensorRtCudaKernelArgument.FromDeviceBuffer(packed),
                TensorRtCudaKernelArgument.FromDeviceBuffer(selectedFlags),
                TensorRtCudaKernelArgument.FromDeviceBuffer(classIndices),
                TensorRtCudaKernelArgument.FromDeviceBuffer(scores),
                TensorRtCudaKernelArgument.FromDeviceBuffer(boxes),
                TensorRtCudaKernelArgument.FromDeviceBuffer(coefficients),
                TensorRtCudaKernelArgument.FromDeviceBuffer(invalidFlag),
                TensorRtCudaKernelArgument.FromInt32(candidateCount),
                TensorRtCudaKernelArgument.FromInt32(fieldCount),
                TensorRtCudaKernelArgument.FromInt32(classCount),
                TensorRtCudaKernelArgument.FromInt32(coefficientCount),
                TensorRtCudaKernelArgument.FromInt32(attributeMajor ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(hasObjectness ? 1 : 0),
                TensorRtCudaKernelArgument.FromInt32(endToEnd ? 1 : 0),
                TensorRtCudaKernelArgument.FromSingle(scoreThreshold)
            };
            uint gridX = checked((uint)((candidateCount + DefaultBlockSize - 1) / DefaultBlockSize));
            var options = new TensorRtCudaKernelLaunchOptions(gridX, DefaultBlockSize, TensorRtCudaSynchronizationMode.CallerManaged);
            return new TensorRtCudaYoloCandidatePlan(kernel, kernel.PrepareLaunch(options, arguments));
        }

        private static void EnsureElements(TensorRtCudaDeviceBuffer buffer, TensorElementType elementType, long elements, TensorRtCudaBufferAccess access, string name)
        {
            TensorRtCudaBufferDescriptor descriptor = buffer.Descriptor;
            if (descriptor.ElementType != elementType || descriptor.Access != access || descriptor.Shape.GetElementCount() != elements) throw new ArgumentException("The CUDA visual buffer contract is incompatible.", name);
        }

        private static void EnsureBuffer(TensorRtCudaDeviceBuffer buffer, TensorElementType elementType, int width, int height, TensorRtCudaBufferAccess access, string name)
        {
            TensorRtCudaBufferDescriptor descriptor = buffer.Descriptor;
            if (descriptor.ElementType != elementType || descriptor.Access != access || descriptor.Shape.GetElementCount() != checked((long)width * height)) throw new ArgumentException("The CUDA visual-map buffer contract is incompatible.", name);
        }

        private static void EnsureScalarBuffer(TensorRtCudaDeviceBuffer buffer, TensorElementType elementType, TensorRtCudaBufferAccess access, string name)
        {
            TensorRtCudaBufferDescriptor descriptor = buffer.Descriptor;
            if (descriptor.ElementType != elementType || descriptor.Access != access || descriptor.Shape.GetElementCount() != 1) throw new ArgumentException("The CUDA visual-map scalar buffer contract is incompatible.", name);
        }

        private static void EnsureFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }

        private static void EnsureFinitePositive(float value, string name)
        {
            EnsureFinite(value, name);
            if (value <= 0) throw new ArgumentOutOfRangeException(name);
        }

        private const string NormalizeBgrNchwSource = @"
extern ""C"" __global__ void deploysharp_visual_normalize_bgr_nchw(
    const unsigned char* source, float* destination,
    int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight,
    int resizedWidth, int resizedHeight, int paddingLeft, int paddingTop,
    float inverseScaleX, float inverseScaleY,
    float paddingBlue, float paddingGreen, float paddingRed,
    float mean0, float mean1, float mean2,
    float scale0, float scale1, float scale2, int swapRedBlue)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int pixels = destinationWidth * destinationHeight;
    if (index >= pixels) return;
    int x = index % destinationWidth;
    int y = index / destinationWidth;
    bool inside = x >= paddingLeft && y >= paddingTop && x < paddingLeft + resizedWidth && y < paddingTop + resizedHeight;
    float values[3] = { paddingBlue, paddingGreen, paddingRed };
    if (inside) {
        float sx = ((float)(x - paddingLeft) + 0.5f) * inverseScaleX - 0.5f;
        float sy = ((float)(y - paddingTop) + 0.5f) * inverseScaleY - 0.5f;
        sx = fmaxf(0.0f, fminf((float)(sourceWidth - 1), sx));
        sy = fmaxf(0.0f, fminf((float)(sourceHeight - 1), sy));
        int x0 = max(0, min(sourceWidth - 1, (int)floorf(sx)));
        int y0 = max(0, min(sourceHeight - 1, (int)floorf(sy)));
        int x1 = min(sourceWidth - 1, x0 + 1);
        int y1 = min(sourceHeight - 1, y0 + 1);
        float ax = sx - (float)x0;
        float ay = sy - (float)y0;
        for (int channel = 0; channel < 3; ++channel) {
            float p00 = (float)source[(y0 * sourceWidth + x0) * 3 + channel];
            float p01 = (float)source[(y0 * sourceWidth + x1) * 3 + channel];
            float p10 = (float)source[(y1 * sourceWidth + x0) * 3 + channel];
            float p11 = (float)source[(y1 * sourceWidth + x1) * 3 + channel];
            float top = p00 + (p01 - p00) * ax;
            float bottom = p10 + (p11 - p10) * ax;
            values[channel] = top + (bottom - top) * ay;
        }
    }
    if (swapRedBlue) { float temporary = values[0]; values[0] = values[2]; values[2] = temporary; }
    destination[index] = (values[0] - mean0) * scale0;
    destination[pixels + index] = (values[1] - mean1) * scale1;
    destination[2 * pixels + index] = (values[2] - mean2) * scale2;
}
";

        private const string RestoreSingleChannelMapSource = @"
__device__ __forceinline__ float deploysharp_visual_sigmoid(float value)
{
    if (value >= 0.0f) return 1.0f / (1.0f + expf(-value));
    float exponential = expf(value);
    return exponential / (1.0f + exponential);
}

extern ""C"" __global__ void deploysharp_visual_restore_single_channel_map(
    const float* source, float* restored, unsigned char* mask,
    int* invalidFlag, int* positiveCount,
    int tensorWidth, int tensorHeight, int modelWidth, int modelHeight,
    int sourceWidth, int sourceHeight,
    float scaleX, float scaleY, float offsetX, float offsetY, float threshold,
    int applySigmoid, int validateProbability, int writeMask)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int tensorPixels = tensorWidth * tensorHeight;
    int sourcePixels = sourceWidth * sourceHeight;
    if (index < tensorPixels) {
        float raw = source[index];
        if (!isfinite(raw) || (validateProbability && (raw < 0.0f || raw > 1.0f))) atomicExch(invalidFlag, 1);
    }
    if (index >= sourcePixels) return;

    int x = index % sourceWidth;
    int y = index / sourceWidth;
    float modelCenterX = ((float)x + 0.5f) * scaleX + offsetX;
    float modelCenterY = ((float)y + 0.5f) * scaleY + offsetY;
    float value = 0.0f;
    if (modelCenterX >= 0.0f && modelCenterX < (float)modelWidth && modelCenterY >= 0.0f && modelCenterY < (float)modelHeight) {
        float sx = fmaxf(0.0f, fminf((float)(tensorWidth - 1), modelCenterX - 0.5f));
        float sy = fmaxf(0.0f, fminf((float)(tensorHeight - 1), modelCenterY - 0.5f));
        int x0 = (int)floorf(sx);
        int y0 = (int)floorf(sy);
        int x1 = min(tensorWidth - 1, x0 + 1);
        int y1 = min(tensorHeight - 1, y0 + 1);
        float ax = sx - (float)x0;
        float ay = sy - (float)y0;
        float p00 = source[y0 * tensorWidth + x0];
        float p01 = source[y0 * tensorWidth + x1];
        float p10 = source[y1 * tensorWidth + x0];
        float p11 = source[y1 * tensorWidth + x1];
        if (applySigmoid) {
            p00 = deploysharp_visual_sigmoid(p00);
            p01 = deploysharp_visual_sigmoid(p01);
            p10 = deploysharp_visual_sigmoid(p10);
            p11 = deploysharp_visual_sigmoid(p11);
        }
        float top = p00 + (p01 - p00) * ax;
        float bottom = p10 + (p11 - p10) * ax;
        value = top * (1.0f - ay) + bottom * ay;
    }
    restored[index] = value;
    if (writeMask) {
        unsigned char positive = value >= threshold ? 1 : 0;
        mask[index] = positive;
        if (positive) atomicAdd(positiveCount, 1);
    }
}
";

        private const string YoloPrototypeMaskSource = @"
__device__ __forceinline__ float deploysharp_visual_yolo_sigmoid(float value)
{
    if (value >= 0.0f) return 1.0f / (1.0f + expf(-value));
    float exponential = expf(value);
    return exponential / (1.0f + exponential);
}

extern ""C"" __global__ void deploysharp_visual_combine_yolo_prototypes(
    const float* prototypes, const float* coefficients, const float* boxes,
    float* activated, int* invalidFlag,
    int channels, int prototypeWidth, int prototypeHeight, int modelWidth, int modelHeight)
{
    int position = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int instance = (int)blockIdx.y;
    int plane = prototypeWidth * prototypeHeight;
    if (position >= plane) return;
    float combined = 0.0f;
    for (int channel = 0; channel < channels; ++channel) {
        float prototype = prototypes[channel * plane + position];
        float coefficient = coefficients[instance * channels + channel];
        if (instance == 0 && !isfinite(prototype)) atomicExch(invalidFlag, 1);
        combined += coefficient * prototype;
    }
    if (!isfinite(combined)) atomicExch(invalidFlag, 1);
    int x = position % prototypeWidth;
    int y = position / prototypeWidth;
    float modelX = ((float)x + 0.5f) * (float)modelWidth / (float)prototypeWidth;
    float modelY = ((float)y + 0.5f) * (float)modelHeight / (float)prototypeHeight;
    const float* box = boxes + instance * 4;
    float value = 0.0f;
    if (modelX >= box[0] && modelX < box[2] && modelY >= box[1] && modelY < box[3]) value = deploysharp_visual_yolo_sigmoid(combined);
    activated[instance * plane + position] = value;
}

extern ""C"" __global__ void deploysharp_visual_restore_yolo_masks(
    const float* activated, const float* boxes, unsigned char* masks, int* positiveCounts,
    int prototypeWidth, int prototypeHeight, int modelWidth, int modelHeight,
    int sourceWidth, int sourceHeight,
    float scaleX, float scaleY, float offsetX, float offsetY, float threshold)
{
    extern __shared__ int blockPositive[];
    if (threadIdx.x == 0) blockPositive[0] = 0;
    __syncthreads();
    int sourceIndex = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int instance = (int)blockIdx.y;
    int sourcePixels = sourceWidth * sourceHeight;
    unsigned char positive = 0;
    if (sourceIndex < sourcePixels) {
        int x = sourceIndex % sourceWidth;
        int y = sourceIndex / sourceWidth;
        float modelX = ((float)x + 0.5f) * scaleX + offsetX;
        float modelY = ((float)y + 0.5f) * scaleY + offsetY;
        const float* box = boxes + instance * 4;
        if (modelX >= 0.0f && modelX < (float)modelWidth && modelY >= 0.0f && modelY < (float)modelHeight
            && modelX >= box[0] && modelX < box[2] && modelY >= box[1] && modelY < box[3]) {
            float gridX = modelX * (float)prototypeWidth / (float)modelWidth - 0.5f;
            float gridY = modelY * (float)prototypeHeight / (float)modelHeight - 0.5f;
            int lowerX = (int)floorf(gridX);
            int lowerY = (int)floorf(gridY);
            float weightX = gridX - (float)lowerX;
            float weightY = gridY - (float)lowerY;
            int upperX = lowerX + 1;
            int upperY = lowerY + 1;
            lowerX = max(0, min(prototypeWidth - 1, lowerX));
            upperX = max(0, min(prototypeWidth - 1, upperX));
            lowerY = max(0, min(prototypeHeight - 1, lowerY));
            upperY = max(0, min(prototypeHeight - 1, upperY));
            int plane = prototypeWidth * prototypeHeight;
            const float* grid = activated + instance * plane;
            float top = grid[lowerY * prototypeWidth + lowerX] * (1.0f - weightX) + grid[lowerY * prototypeWidth + upperX] * weightX;
            float bottom = grid[upperY * prototypeWidth + lowerX] * (1.0f - weightX) + grid[upperY * prototypeWidth + upperX] * weightX;
            float sampled = top * (1.0f - weightY) + bottom * weightY;
            positive = sampled >= threshold ? 1 : 0;
        }
        masks[instance * sourcePixels + sourceIndex] = positive;
        if (positive) atomicAdd(&blockPositive[0], 1);
    }
    __syncthreads();
    if (threadIdx.x == 0 && blockPositive[0] != 0) atomicAdd(positiveCounts + instance, blockPositive[0]);
}
";

        private const string YoloCandidateFilterSource = @"
__device__ __forceinline__ float deploysharp_visual_yolo_value(const float* values, int candidates, int fields, int candidate, int field, int attributeMajor)
{
    return attributeMajor ? values[field * candidates + candidate] : values[candidate * fields + field];
}

extern ""C"" __global__ void deploysharp_visual_filter_yolo_candidates(
    const float* packed, unsigned char* selectedFlags, int* classIndices, float* scores,
    float* boxes, float* coefficients, int* invalidFlag,
    int candidates, int fields, int classes, int coefficientCount,
    int attributeMajor, int hasObjectness, int endToEnd, float scoreThreshold)
{
    int candidate = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (candidate >= candidates) return;
    for (int field = 0; field < fields; ++field) {
        if (!isfinite(deploysharp_visual_yolo_value(packed, candidates, fields, candidate, field, attributeMajor))) atomicExch(invalidFlag, 1);
    }
    int selectedClass = 0;
    float score = 0.0f;
    int coefficientOffset;
    if (endToEnd) {
        score = deploysharp_visual_yolo_value(packed, candidates, fields, candidate, 4, attributeMajor);
        float classValue = deploysharp_visual_yolo_value(packed, candidates, fields, candidate, 5, attributeMajor);
        if (score < 0.0f || score > 1.0f || classValue < 0.0f || classValue >= (float)classes || classValue != floorf(classValue)) atomicExch(invalidFlag, 1);
        selectedClass = (int)classValue;
        coefficientOffset = 6;
    } else {
        int classOffset = hasObjectness ? 5 : 4;
        float objectness = hasObjectness ? deploysharp_visual_yolo_value(packed, candidates, fields, candidate, 4, attributeMajor) : 1.0f;
        if (objectness < 0.0f || objectness > 1.0f) atomicExch(invalidFlag, 1);
        float classScore = deploysharp_visual_yolo_value(packed, candidates, fields, candidate, classOffset, attributeMajor);
        if (classScore < 0.0f || classScore > 1.0f) atomicExch(invalidFlag, 1);
        for (int classIndex = 1; classIndex < classes; ++classIndex) {
            float current = deploysharp_visual_yolo_value(packed, candidates, fields, candidate, classOffset + classIndex, attributeMajor);
            if (current < 0.0f || current > 1.0f) atomicExch(invalidFlag, 1);
            if (current > classScore) { classScore = current; selectedClass = classIndex; }
        }
        score = objectness * classScore;
        coefficientOffset = classOffset + classes;
    }
    unsigned char selected = score > scoreThreshold ? 1 : 0;
    selectedFlags[candidate] = selected;
    if (!selected) return;
    classIndices[candidate] = selectedClass;
    scores[candidate] = score;
    for (int field = 0; field < 4; ++field) boxes[candidate * 4 + field] = deploysharp_visual_yolo_value(packed, candidates, fields, candidate, field, attributeMajor);
    for (int channel = 0; channel < coefficientCount; ++channel) coefficients[candidate * coefficientCount + channel] = deploysharp_visual_yolo_value(packed, candidates, fields, candidate, coefficientOffset + channel, attributeMajor);
}
";
    }
}
