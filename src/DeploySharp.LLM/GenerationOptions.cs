using System;
using System.Collections.Generic;
using System.Linq;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Controls deterministic and stochastic text sampling. / 控制文本采样的确定性和随机性。</summary>
    public sealed class GenerationOptions
    {
        private readonly IReadOnlyList<string> _stopSequences;

        /// <summary>Initializes generation options. / 初始化生成选项。</summary>
        public GenerationOptions(
            int maxTokens = 256,
            float temperature = 0.8f,
            float topP = 0.95f,
            int topK = 40,
            int? seed = null,
            IEnumerable<string>? stopSequences = null,
            TimeSpan? timeout = null)
        {
            if (maxTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maxTokens));
            if (float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature < 0) throw new ArgumentOutOfRangeException(nameof(temperature));
            if (float.IsNaN(topP) || float.IsInfinity(topP) || topP <= 0 || topP > 1) throw new ArgumentOutOfRangeException(nameof(topP));
            if (topK < 0) throw new ArgumentOutOfRangeException(nameof(topK));
            if (timeout.HasValue && (timeout.Value <= TimeSpan.Zero || timeout.Value == System.Threading.Timeout.InfiniteTimeSpan)) throw new ArgumentOutOfRangeException(nameof(timeout));

            MaxTokens = maxTokens;
            Temperature = temperature;
            TopP = topP;
            TopK = topK;
            Seed = seed;
            Timeout = timeout;

            var stops = new List<string>();
            if (stopSequences != null)
            {
                foreach (string stop in stopSequences)
                {
                    if (string.IsNullOrEmpty(stop)) throw new ArgumentException("Stop sequences cannot be empty.", nameof(stopSequences));
                    if (!stops.Contains(stop, StringComparer.Ordinal)) stops.Add(stop);
                }
            }

            _stopSequences = stops.AsReadOnly();
        }

        /// <summary>Gets the maximum number of generated tokens. / 获取最多生成的 token 数。</summary>
        public int MaxTokens { get; }
        /// <summary>Gets sampling temperature. / 获取采样温度。</summary>
        public float Temperature { get; }
        /// <summary>Gets nucleus sampling probability. / 获取 nucleus 采样概率。</summary>
        public float TopP { get; }
        /// <summary>Gets top-k sampling count; zero disables the limit. / 获取 top-k 采样数量，零表示不限制。</summary>
        public int TopK { get; }
        /// <summary>Gets an optional random seed. / 获取可选随机种子。</summary>
        public int? Seed { get; }
        /// <summary>Gets copied stop sequences. / 获取复制后的停止序列。</summary>
        public IReadOnlyList<string> StopSequences => _stopSequences;
        /// <summary>Gets an optional operation timeout. / 获取可选操作超时。</summary>
        public TimeSpan? Timeout { get; }

        /// <summary>Gets conservative defaults. / 获取保守的默认选项。</summary>
        public static GenerationOptions Default { get; } = new GenerationOptions();
    }
}
