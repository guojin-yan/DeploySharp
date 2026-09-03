using System;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Identifies the TensorRT API line selected by the consumer-owned native installation. / 定义或说明原生运行时合同。</summary>
    public enum TensorRtApiVersion
    {
        /// <summary>TensorRT 8 API line. / 说明相关公共 API。</summary>
        TensorRt8 = 8,
        /// <summary>TensorRT 10 API line. / 说明相关公共 API。</summary>
        TensorRt10 = 10,
        /// <summary>TensorRT 11 API line. / 说明相关公共 API。</summary>
        TensorRt11 = 11
    }

    /// <summary>Contains managed adapter settings; CUDA, TensorRT, and driver installation remain consumer-owned. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtBackendOptions
    {
        /// <summary>Initializes options for loading a caller-owned serialized engine. / 初始化 TensorRT 引擎对象。</summary>
        public TensorRtBackendOptions(
            TensorRtApiVersion apiVersion = TensorRtApiVersion.TensorRt10,
            int optimizationProfile = 0,
            long maximumEngineBytes = int.MaxValue,
            string? cudaTargetArchitecture = null,
            bool cacheImmutableHostInputsOnDevice = false)
        {
            if (!Enum.IsDefined(typeof(TensorRtApiVersion), apiVersion))
            {
                throw new ArgumentOutOfRangeException(nameof(apiVersion));
            }

            if (optimizationProfile < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(optimizationProfile));
            }

            if (maximumEngineBytes < 8 || maximumEngineBytes > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEngineBytes), "The managed loader requires an engine size within the byte-array range.");
            }

            string? normalizedArchitecture = string.IsNullOrWhiteSpace(cudaTargetArchitecture) ? null : cudaTargetArchitecture.Trim();
            if (normalizedArchitecture != null &&
                !normalizedArchitecture.StartsWith("compute_", StringComparison.Ordinal) &&
                !normalizedArchitecture.StartsWith("sm_", StringComparison.Ordinal))
            {
                throw new ArgumentException("CUDA target architecture must use compute_XX or sm_XX syntax.", nameof(cudaTargetArchitecture));
            }

            ApiVersion = apiVersion;
            OptimizationProfile = optimizationProfile;
            MaximumEngineBytes = maximumEngineBytes;
            CudaTargetArchitecture = normalizedArchitecture;
            CacheImmutableHostInputsOnDevice = cacheImmutableHostInputsOnDevice;
        }

        /// <summary>Gets the TensorRT API line selected by the consumer. / 获取相关信息。</summary>
        public TensorRtApiVersion ApiVersion { get; }
        /// <summary>Gets the optimization profile selected for execution. / 获取形状或执行配置信息。</summary>
        public int OptimizationProfile { get; }
        /// <summary>Gets the maximum serialized plan size accepted by the managed loader. / 获取 TensorRT 引擎信息。</summary>
        public long MaximumEngineBytes { get; }
        /// <summary>Gets the optional NVRTC target that enables backend-side CUDA output reduction. / 获取用于启用后端侧 CUDA 输出归约的可选 NVRTC 目标。</summary>
        public string? CudaTargetArchitecture { get; }
        /// <summary>Gets whether repeated calls with the same immutable input object may reuse its device copy. / 获取是否允许同一不可变输入对象在重复调用时复用其显存副本。</summary>
        public bool CacheImmutableHostInputsOnDevice { get; }

        /// <summary>Gets default TensorRT adapter options. / 获取配置信息。</summary>
        public static TensorRtBackendOptions Default { get; } = new TensorRtBackendOptions();
    }
}
