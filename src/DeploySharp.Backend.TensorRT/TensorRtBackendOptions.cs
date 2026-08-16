using System;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Identifies the TensorRT API line selected by the consumer-owned native installation.</summary>
    public enum TensorRtApiVersion
    {
        /// <summary>TensorRT 8 API line.</summary>
        TensorRt8 = 8,
        /// <summary>TensorRT 10 API line.</summary>
        TensorRt10 = 10,
        /// <summary>TensorRT 11 API line.</summary>
        TensorRt11 = 11
    }

    /// <summary>Contains managed adapter settings; CUDA, TensorRT, and driver installation remain consumer-owned.</summary>
    public sealed class TensorRtBackendOptions
    {
        /// <summary>Initializes options for loading a caller-owned serialized engine.</summary>
        public TensorRtBackendOptions(
            TensorRtApiVersion apiVersion = TensorRtApiVersion.TensorRt10,
            int optimizationProfile = 0,
            long maximumEngineBytes = int.MaxValue)
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

            ApiVersion = apiVersion;
            OptimizationProfile = optimizationProfile;
            MaximumEngineBytes = maximumEngineBytes;
        }

        /// <summary>Gets the TensorRT API line selected by the consumer.</summary>
        public TensorRtApiVersion ApiVersion { get; }
        /// <summary>Gets the optimization profile selected for execution.</summary>
        public int OptimizationProfile { get; }
        /// <summary>Gets the maximum serialized plan size accepted by the managed loader.</summary>
        public long MaximumEngineBytes { get; }

        /// <summary>Gets default TensorRT adapter options.</summary>
        public static TensorRtBackendOptions Default { get; } = new TensorRtBackendOptions();
    }
}
