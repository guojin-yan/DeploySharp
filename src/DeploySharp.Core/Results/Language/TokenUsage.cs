using System;

namespace JYPPX.DeploySharp.Results.Language
{
    /// <summary>
    /// Contains prompt and generated token counts. / 包含提示词和生成令牌数量。
    /// </summary>
    public sealed class TokenUsage
    {
        /// <summary>Initializes token usage. / 初始化令牌用量。</summary>
        public TokenUsage(int promptTokens, int generatedTokens)
        {
            if (promptTokens < 0) throw new ArgumentOutOfRangeException(nameof(promptTokens));
            if (generatedTokens < 0) throw new ArgumentOutOfRangeException(nameof(generatedTokens));
            PromptTokens = promptTokens;
            GeneratedTokens = generatedTokens;
        }

        /// <summary>Gets prompt token count. / 获取提示词令牌数量。</summary>
        public int PromptTokens { get; }

        /// <summary>Gets generated token count. / 获取生成令牌数量。</summary>
        public int GeneratedTokens { get; }

        /// <summary>Gets total token count. / 获取令牌总数。</summary>
        public int TotalTokens => checked(PromptTokens + GeneratedTokens);
    }
}
