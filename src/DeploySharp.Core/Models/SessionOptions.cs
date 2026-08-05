using System;

namespace JYPPX.DeploySharp.Models
{
    /// <summary>
    /// Contains backend-neutral options used when creating an inference session. / 包含创建推理会话时使用的后端无关选项。
    /// </summary>
    public sealed class SessionOptions
    {
        /// <summary>Initializes session options. / 初始化会话选项。</summary>
        public SessionOptions(int maxConcurrency = 1, bool enableProfiling = false)
        {
            if (maxConcurrency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Concurrency must be greater than zero.");
            }

            MaxConcurrency = maxConcurrency;
            EnableProfiling = enableProfiling;
        }

        /// <summary>Gets the requested maximum concurrent inference operations. / 获取请求的最大并发推理操作数。</summary>
        public int MaxConcurrency { get; }

        /// <summary>Gets whether backend profiling should be enabled when supported. / 获取在后端支持时是否启用性能分析。</summary>
        public bool EnableProfiling { get; }

        /// <summary>Gets default session options. / 获取默认会话选项。</summary>
        public static SessionOptions Default { get; } = new SessionOptions();
    }
}
