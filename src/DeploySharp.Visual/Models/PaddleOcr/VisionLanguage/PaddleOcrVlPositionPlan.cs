using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Owns request-local, single-image PaddleOCR-VL-1.6 three-axis RoPE positions. / 持有请求独立的 PaddleOCR-VL-1.6 单图三轴 RoPE 位置。</summary>
    /// <remarks>Coordinates are not physical KV offsets. No mutable position state is stored on a shared model. / 坐标不等于 KV 物理偏移，不在共享模型上保存可变位置状态。</remarks>
    public sealed class PaddleOcrVlPositionPlan
    {
        private readonly long[] _positions;
        private readonly int _maximumNewTokens;

        private PaddleOcrVlPositionPlan(PaddleOcrVlTokenSequence prompt)
        {
            PromptLength = prompt.Count;
            _maximumNewTokens = prompt.MaximumNewTokens;
            int height = prompt.ImagePlan.GridHeight / 2;
            int width = prompt.ImagePlan.GridWidth / 2;
            int offset = prompt.ImageTokenOffset;
            int imageTokens = prompt.ImagePlan.ImageTokenCount;
            int imageEnd = checked(offset + imageTokens);
            if (offset <= 0 || imageEnd >= PromptLength || height <= 0 || width <= 0 || checked(height * width) != imageTokens)
                throw new ArgumentException("The prompt image span and grid are inconsistent.", nameof(prompt));
            _positions = new long[checked(3 * PromptLength)];
            for (int axis = 0; axis < 3; axis++)
            {
                int axisOffset = axis * PromptLength;
                for (int token = 0; token < offset; token++) _positions[axisOffset + token] = token;
                for (int patch = 0; patch < imageTokens; patch++)
                    _positions[axisOffset + offset + patch] = offset + (axis == 0 ? 0 : axis == 1 ? patch / width : patch % width);
                for (int token = imageEnd; token < PromptLength; token++)
                    _positions[axisOffset + token] = offset + Math.Max(height, width) + token - imageEnd;
            }
            // The post-image text is present in the pinned chat template, so
            // its last position is the maximum across all three axes.
            RopeDelta = checked((int)_positions[PromptLength - 1] + 1 - PromptLength);
            PositionIds = Array.AsReadOnly(_positions);
        }

        /// <summary>Gets the prefill sequence length, with batch fixed to one. / 获取预填充序列长度，Batch 固定为 1。</summary>
        public int PromptLength { get; }
        /// <summary>Gets flattened read-only positions in [3,1,prompt_length] axis-major order. / 获取按轴展平的只读 [3,1,prompt_length] 位置。</summary>
        public IReadOnlyList<long> PositionIds { get; }
        /// <summary>Gets the coordinate offset added to physical KV lengths during generation; it can be negative. / 获取生成时加在物理 KV 长度上的坐标偏移，可为负数。</summary>
        public int RopeDelta { get; }

        /// <summary>Builds positions for the exact unpadded, single-image prompt. / 为精确无填充单图 Prompt 构造位置。</summary>
        public static PaddleOcrVlPositionPlan Create(PaddleOcrVlTokenSequence prompt)
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));
            return new PaddleOcrVlPositionPlan(prompt);
        }

        /// <summary>Returns an independent prefill tensor buffer in [3,1,prompt_length] layout. / 返回独立的 [3,1,prompt_length] 预填充张量缓冲区。</summary>
        public long[] CopyPositionIds() => (long[])_positions.Clone();

        /// <summary>Writes positions for generated tokens from their physical KV length, without changing this plan. / 根据物理 KV 长度写入生成 Token 的位置，不修改本规划。</summary>
        /// <param name="pastTokenCount">Number of tokens already present in the cache, including the prompt. / KV 中已存在的 Token 数，包含 Prompt。</param>
        /// <param name="tokenCount">Number of generated tokens being processed in this call. / 本次处理的生成 Token 数。</param>
        /// <param name="destination">Caller-owned buffer receiving [3,1,tokenCount] values. / 调用方缓冲区，接收 [3,1,tokenCount] 数据。</param>
        /// <param name="destinationOffset">First destination element. / 目标起始元素。</param>
        /// <remarks>Prefill predicts the first generated token. Only maximumNewTokens-1 tokens may subsequently be fed back. / Prefill 已预测首个生成 Token，因此最多再反馈 maximumNewTokens-1 个 Token。</remarks>
        public void WriteDecodePositionIds(int pastTokenCount, int tokenCount, long[] destination, int destinationOffset = 0)
        {
            if (pastTokenCount < PromptLength) throw new ArgumentOutOfRangeException(nameof(pastTokenCount), "The complete image prompt must be cached before decoding.");
            if (tokenCount <= 0 || (long)pastTokenCount + tokenCount > (long)PromptLength + _maximumNewTokens - 1)
                throw new ArgumentOutOfRangeException(nameof(tokenCount), "Decode would exceed the prompt's reserved generation budget.");
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            long required = 3L * tokenCount;
            if (destinationOffset < 0 || required > (long)destination.Length - destinationOffset)
                throw new ArgumentOutOfRangeException(nameof(destinationOffset), "The destination cannot hold all three position axes.");
            for (int axis = 0; axis < 3; axis++)
                for (int token = 0; token < tokenCount; token++)
                    destination[destinationOffset + axis * tokenCount + token] = (long)pastTokenCount + RopeDelta + token;
        }
    }
}
