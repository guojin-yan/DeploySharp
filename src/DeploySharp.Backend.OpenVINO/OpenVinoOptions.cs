using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    /// <summary>Defines the supported OpenVINO CPU performance hint. / 定义受支持的 OpenVINO CPU 性能提示。</summary>
    public enum OpenVinoPerformanceHint
    {
        /// <summary>Lets OpenVINO choose its default. / 让 OpenVINO 使用默认值。</summary>
        Default = 0,
        /// <summary>Optimizes for request latency. / 针对请求延迟进行优化。</summary>
        Latency = 1,
        /// <summary>Optimizes for aggregate throughput. / 针对总体吞吐量进行优化。</summary>
        Throughput = 2
    }

    /// <summary>Contains immutable OpenVINO adapter configuration without exposing vendor types. / 包含不暴露厂商类型的不可变 OpenVINO 适配器配置。</summary>
    public sealed class OpenVinoOptions
    {
        private static readonly HashSet<string> AllowedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "CACHE_DIR", "NUM_STREAMS", "INFERENCE_NUM_THREADS", "PERFORMANCE_HINT", "PERF_COUNT"
        };

        private readonly IReadOnlyDictionary<string, string> _compileProperties;

        /// <summary>Initializes OpenVINO options. / 初始化 OpenVINO 选项。</summary>
        public OpenVinoOptions(
            string device = "CPU",
            OpenVinoPerformanceHint performanceHint = OpenVinoPerformanceHint.Default,
            int? streams = null,
            int? inferenceThreads = null,
            string? cacheDirectory = null,
            bool enableProfiling = false,
            int? requestCount = null,
            IEnumerable<KeyValuePair<string, string>>? compileProperties = null,
            bool allowDynamicShapes = true)
        {
            if (!string.Equals(device, "CPU", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only the verified CPU device is accepted by this release.", nameof(device));
            if (streams.HasValue && streams.Value <= 0) throw new ArgumentOutOfRangeException(nameof(streams));
            if (inferenceThreads.HasValue && inferenceThreads.Value <= 0) throw new ArgumentOutOfRangeException(nameof(inferenceThreads));
            if (requestCount.HasValue && requestCount.Value <= 0) throw new ArgumentOutOfRangeException(nameof(requestCount));
            if (!string.IsNullOrWhiteSpace(cacheDirectory) && !Path.IsPathRooted(cacheDirectory)) throw new ArgumentException("The cache directory must be absolute.", nameof(cacheDirectory));

            Device = "CPU";
            PerformanceHint = performanceHint;
            Streams = streams;
            InferenceThreads = inferenceThreads;
            CacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory) ? null : Path.GetFullPath(cacheDirectory!);
            EnableProfiling = enableProfiling;
            RequestCount = requestCount;
            AllowDynamicShapes = allowDynamicShapes;

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (compileProperties != null)
            {
                foreach (KeyValuePair<string, string> property in compileProperties)
                {
                    if (!AllowedProperties.Contains(property.Key)) throw new ArgumentException("The compile property is unknown or not admitted by DeploySharp: " + property.Key, nameof(compileProperties));
                    if (string.IsNullOrWhiteSpace(property.Value)) throw new ArgumentException("Compile property values cannot be empty.", nameof(compileProperties));
                    if (values.ContainsKey(property.Key)) throw new ArgumentException("Compile properties cannot contain duplicate keys.", nameof(compileProperties));
                    values.Add(property.Key, property.Value);
                }
            }

            AddConfigured(values, "PERFORMANCE_HINT", performanceHint == OpenVinoPerformanceHint.Latency ? "LATENCY" : performanceHint == OpenVinoPerformanceHint.Throughput ? "THROUGHPUT" : null);
            AddConfigured(values, "NUM_STREAMS", streams?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AddConfigured(values, "INFERENCE_NUM_THREADS", inferenceThreads?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AddConfigured(values, "CACHE_DIR", CacheDirectory);
            AddConfigured(values, "PERF_COUNT", enableProfiling ? "YES" : null);
            _compileProperties = new ReadOnlyDictionary<string, string>(values);
        }

        /// <summary>Gets the verified target device. / 获取经过验证的目标设备。</summary>
        public string Device { get; }
        /// <summary>Gets the performance hint. / 获取性能提示。</summary>
        public OpenVinoPerformanceHint PerformanceHint { get; }
        /// <summary>Gets the optional stream count. / 获取可选流数量。</summary>
        public int? Streams { get; }
        /// <summary>Gets the optional CPU inference-thread count. / 获取可选 CPU 推理线程数。</summary>
        public int? InferenceThreads { get; }
        /// <summary>Gets the optional absolute model-cache directory. / 获取可选的绝对模型缓存目录。</summary>
        public string? CacheDirectory { get; }
        /// <summary>Gets whether native profiling is enabled. / 获取是否启用原生性能分析。</summary>
        public bool EnableProfiling { get; }
        /// <summary>Gets the optional request-pool capacity override. / 获取可选的请求池容量覆盖值。</summary>
        public int? RequestCount { get; }
        /// <summary>Gets whether partially dynamic model metadata is accepted. / 获取是否接受部分动态模型元数据。</summary>
        public bool AllowDynamicShapes { get; }
        /// <summary>Gets the validated native compile properties. / 获取已验证的原生编译属性。</summary>
        public IReadOnlyDictionary<string, string> CompileProperties => _compileProperties;

        /// <summary>Gets the default verified CPU configuration. / 获取默认的已验证 CPU 配置。</summary>
        public static OpenVinoOptions Default { get; } = new OpenVinoOptions();

        private static void AddConfigured(Dictionary<string, string> values, string key, string? value)
        {
            if (value == null) return;
            if (values.ContainsKey(key)) throw new ArgumentException("A strongly typed option and compile property configure the same key: " + key, "compileProperties");
            values.Add(key, value);
        }
    }
}
