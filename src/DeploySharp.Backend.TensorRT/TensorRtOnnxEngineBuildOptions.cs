using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Controls the TensorRT precision policy requested while building an ONNX model. / 定义或说明 ONNX 模型合同。</summary>
    public enum TensorRtOnnxEnginePrecision
    {
        /// <summary>Preserves TensorRT's runtime-default precision and TF32 policy. / 说明原生运行时公共 API。</summary>
        RuntimeDefault = 0,
        /// <summary>Disables TensorRT's TF32 builder flag for a weakly typed FP32 build. / 说明相关公共 API。</summary>
        Float32 = 1,
        /// <summary>Enables TensorRT's FP16 builder flag. / 说明相关公共 API。</summary>
        Float16 = 2,
        /// <summary>Requires an explicitly quantized ONNX Q/DQ graph without enabling legacy implicit INT8 calibration. / 说明 ONNX 模型公共 API。</summary>
        Int8ExplicitQuantization = 3
    }

    /// <summary>Defines one min/opt/max range for a dynamic ONNX network input. / 定义或说明 ONNX 模型合同。</summary>
    public sealed class TensorRtOnnxInputProfile
    {
        /// <summary>Initializes a dynamic input profile. / 初始化形状或执行配置对象。</summary>
        public TensorRtOnnxInputProfile(string inputName, TensorShape minimum, TensorShape optimum, TensorShape maximum)
        {
            if (string.IsNullOrWhiteSpace(inputName)) throw new ArgumentException("An ONNX input name is required.", nameof(inputName));
            InputName = inputName.Trim();
            Minimum = minimum ?? throw new ArgumentNullException(nameof(minimum));
            Optimum = optimum ?? throw new ArgumentNullException(nameof(optimum));
            Maximum = maximum ?? throw new ArgumentNullException(nameof(maximum));
            ValidateShapes();
        }

        /// <summary>Gets the exact ONNX network input name. / 获取 ONNX 模型信息。</summary>
        public string InputName { get; }
        /// <summary>Gets the minimum accepted runtime shape. / 获取原生运行时信息。</summary>
        public TensorShape Minimum { get; }
        /// <summary>Gets the TensorRT optimization target shape. / 获取形状或执行配置信息。</summary>
        public TensorShape Optimum { get; }
        /// <summary>Gets the maximum accepted runtime shape. / 获取原生运行时信息。</summary>
        public TensorShape Maximum { get; }

        private void ValidateShapes()
        {
            if (Minimum.Rank == 0 || Minimum.Rank != Optimum.Rank || Minimum.Rank != Maximum.Rank)
            {
                throw new ArgumentException("TensorRT input profile shapes must have the same non-zero rank.");
            }

            for (int index = 0; index < Minimum.Rank; index++)
            {
                long min = Minimum[index];
                long opt = Optimum[index];
                long max = Maximum[index];
                if (min <= 0 || opt <= 0 || max <= 0 || min > opt || opt > max || max > int.MaxValue)
                {
                    throw new ArgumentException("TensorRT input profile dimensions must satisfy 0 < min <= opt <= max <= Int32.MaxValue.");
                }
            }
        }
    }

    /// <summary>Contains managed ONNX-to-engine build settings; native runtime installation remains consumer-owned. / 定义或说明 TensorRT 引擎合同。</summary>
    public sealed class TensorRtOnnxEngineBuildOptions
    {
        /// <summary>Initializes ONNX-to-engine build settings. / 初始化 TensorRT 引擎对象。</summary>
        public TensorRtOnnxEngineBuildOptions(
            TensorRtApiVersion apiVersion = TensorRtApiVersion.TensorRt10,
            TensorRtOnnxEnginePrecision precision = TensorRtOnnxEnginePrecision.RuntimeDefault,
            long maximumOnnxBytes = int.MaxValue,
            long maximumEngineBytes = int.MaxValue,
            ulong workspaceBytes = 1073741824UL,
            int optimizationLevel = -1,
            bool stronglyTypedNetwork = false,
            bool overwrite = false,
            IEnumerable<TensorRtOnnxInputProfile>? inputProfiles = null)
        {
            if (!Enum.IsDefined(typeof(TensorRtApiVersion), apiVersion)) throw new ArgumentOutOfRangeException(nameof(apiVersion));
            if (!Enum.IsDefined(typeof(TensorRtOnnxEnginePrecision), precision)) throw new ArgumentOutOfRangeException(nameof(precision));
            if (maximumOnnxBytes < 8 || maximumOnnxBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumOnnxBytes));
            if (maximumEngineBytes < 8 || maximumEngineBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumEngineBytes));
            if (workspaceBytes == 0) throw new ArgumentOutOfRangeException(nameof(workspaceBytes));
            if (optimizationLevel < -1 || optimizationLevel > 5) throw new ArgumentOutOfRangeException(nameof(optimizationLevel));
            if (apiVersion == TensorRtApiVersion.TensorRt8 && stronglyTypedNetwork)
            {
                throw new ArgumentException("TensorRT 8 does not expose the strongly typed network policy.", nameof(stronglyTypedNetwork));
            }
            if (apiVersion == TensorRtApiVersion.TensorRt11 &&
                (precision == TensorRtOnnxEnginePrecision.Float32 || precision == TensorRtOnnxEnginePrecision.Float16))
            {
                throw new ArgumentException("TensorRT 11 strongly typed networks derive precision from the ONNX graph and do not accept a weakly typed FP32 or FP16 builder policy.", nameof(precision));
            }
            if (stronglyTypedNetwork &&
                (precision == TensorRtOnnxEnginePrecision.Float32 || precision == TensorRtOnnxEnginePrecision.Float16))
            {
                throw new ArgumentException("A strongly typed TensorRT network derives precision from the ONNX graph and cannot also request a weakly typed FP32 or FP16 builder policy.", nameof(precision));
            }

            var profiles = new List<TensorRtOnnxInputProfile>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (inputProfiles != null)
            {
                foreach (TensorRtOnnxInputProfile? profile in inputProfiles)
                {
                    if (profile == null) throw new ArgumentException("An input profile cannot be null.", nameof(inputProfiles));
                    if (!names.Add(profile.InputName)) throw new ArgumentException("Each ONNX input can have at most one profile.", nameof(inputProfiles));
                    profiles.Add(profile);
                }
            }

            ApiVersion = apiVersion;
            Precision = precision;
            MaximumOnnxBytes = maximumOnnxBytes;
            MaximumEngineBytes = maximumEngineBytes;
            WorkspaceBytes = workspaceBytes;
            OptimizationLevel = optimizationLevel;
            StronglyTypedNetwork = apiVersion == TensorRtApiVersion.TensorRt11 || stronglyTypedNetwork;
            Overwrite = overwrite;
            InputProfiles = profiles.AsReadOnly();
        }

        /// <summary>Gets the consumer-selected TensorRT API line. / 获取相关信息。</summary>
        public TensorRtApiVersion ApiVersion { get; }
        /// <summary>Gets the requested builder precision policy. / 获取配置信息。</summary>
        public TensorRtOnnxEnginePrecision Precision { get; }
        /// <summary>Gets the managed ONNX input size limit. / 获取 ONNX 模型信息。</summary>
        public long MaximumOnnxBytes { get; }
        /// <summary>Gets the serialized engine output size limit. / 获取 TensorRT 引擎信息。</summary>
        public long MaximumEngineBytes { get; }
        /// <summary>Gets the TensorRT workspace memory-pool limit. / 获取相关信息。</summary>
        public ulong WorkspaceBytes { get; }
        /// <summary>Gets the TensorRT builder optimization level, or -1 to preserve the runtime default. / 获取原生运行时信息。</summary>
        public int OptimizationLevel { get; }
        /// <summary>Gets whether a strongly typed network is requested where the selected TensorRT line supports it. / 获取相关信息。</summary>
        public bool StronglyTypedNetwork { get; }
        /// <summary>Gets whether an existing caller-owned output file may be replaced. / 获取路径信息。</summary>
        public bool Overwrite { get; }
        /// <summary>Gets the dynamic input profiles keyed by exact ONNX input name. / 获取 ONNX 模型信息。</summary>
        public IReadOnlyList<TensorRtOnnxInputProfile> InputProfiles { get; }

        /// <summary>Gets default ONNX-to-engine build settings. / 获取 TensorRT 引擎信息。</summary>
        public static TensorRtOnnxEngineBuildOptions Default { get; } = new TensorRtOnnxEngineBuildOptions();
    }

    /// <summary>Describes one completed caller-owned ONNX-to-engine build. / 定义或说明 TensorRT 引擎合同。</summary>
    public sealed class TensorRtOnnxEngineBuildResult
    {
        internal TensorRtOnnxEngineBuildResult(
            string onnxPath,
            string enginePath,
            long onnxBytes,
            long engineBytes,
            string onnxSha256,
            string engineSha256,
            string buildInputsSha256,
            TensorRtApiVersion apiVersion,
            TensorRtOnnxEnginePrecision precision,
            int optimizationProfileCount)
        {
            OnnxPath = onnxPath;
            EnginePath = enginePath;
            OnnxBytes = onnxBytes;
            EngineBytes = engineBytes;
            OnnxSha256 = onnxSha256;
            EngineSha256 = engineSha256;
            BuildInputsSha256 = buildInputsSha256;
            ApiVersion = apiVersion;
            Precision = precision;
            OptimizationProfileCount = optimizationProfileCount;
        }

        /// <summary>Gets the validated ONNX source path. / 获取 ONNX 模型信息。</summary>
        public string OnnxPath { get; }
        /// <summary>Gets the caller-owned serialized engine path. / 获取 TensorRT 引擎信息。</summary>
        public string EnginePath { get; }
        /// <summary>Gets the ONNX source length. / 获取 ONNX 模型信息。</summary>
        public long OnnxBytes { get; }
        /// <summary>Gets the serialized engine length. / 获取 TensorRT 引擎信息。</summary>
        public long EngineBytes { get; }
        /// <summary>Gets the validated ONNX SHA256. / 获取 ONNX 模型信息。</summary>
        public string OnnxSha256 { get; }
        /// <summary>Gets the generated engine SHA256. / 获取 TensorRT 引擎信息。</summary>
        public string EngineSha256 { get; }
        /// <summary>Gets a hash of managed build inputs; a device-safe cache key must additionally bind the full native runtime and GPU identity. / 获取缓存信息。</summary>
        public string BuildInputsSha256 { get; }
        /// <summary>Gets the TensorRT API line used for the build. / 获取相关信息。</summary>
        public TensorRtApiVersion ApiVersion { get; }
        /// <summary>Gets the requested builder precision policy. / 获取配置信息。</summary>
        public TensorRtOnnxEnginePrecision Precision { get; }
        /// <summary>Gets the number of optimization profiles attached to the build. / 获取形状或执行配置信息。</summary>
        public int OptimizationProfileCount { get; }
    }
}
