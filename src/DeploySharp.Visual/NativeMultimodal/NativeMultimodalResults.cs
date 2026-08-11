using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Reports a packed image state without exposing its mutable feature tensor. / 报告 Packed Image State 且不公开可变 Feature Tensor。</summary>
    public sealed class NativeMultimodalImageState
    {
        /// <summary>Initializes an owned image-state summary. / 初始化自有图像状态摘要。</summary>
        public NativeMultimodalImageState(GenerativeVisionLanguageImageState featureState, NativeMultimodalImageGrid grid, int cropCount, int imageTokenCount, TimeSpan packingTime)
        {
            FeatureState = featureState ?? throw new ArgumentNullException(nameof(featureState));
            if (grid.Rows <= 0 || grid.Columns <= 0 || cropCount != grid.PatchCount + 1 || imageTokenCount <= 0 || packingTime < TimeSpan.Zero) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Image-state grid, crops, tokens, or timing are invalid.", profileId: featureState.Identity.ProfileId);
            Grid = grid;
            CropCount = cropCount;
            ImageTokenCount = imageTokenCount;
            PackingTime = packingTime;
        }

        /// <summary>Gets common feature identity and numeric summary. / 获取通用 Feature Identity 与数值摘要。</summary>
        public GenerativeVisionLanguageImageState FeatureState { get; }
        /// <summary>Gets selected any-resolution grid. / 获取选择的任意分辨率网格。</summary>
        public NativeMultimodalImageGrid Grid { get; }
        /// <summary>Gets Vision crop count including the base crop. / 获取包含基础 Crop 的 Vision Crop 数。</summary>
        public int CropCount { get; }
        /// <summary>Gets packed image-token count. / 获取 Packed Image Token 数。</summary>
        public int ImageTokenCount { get; }
        /// <summary>Gets managed anyres packing time. / 获取 Managed Anyres Packing 时间。</summary>
        public TimeSpan PackingTime { get; }
    }

    /// <summary>Summarizes the final owned KV tensors and their exact state identity. / 汇总最终自有 KV Tensor 及精确状态 Identity。</summary>
    public sealed class NativeMultimodalKvStateSummary
    {
        /// <summary>Initializes a bounded KV-state summary. / 初始化受限 KV State 摘要。</summary>
        public NativeMultimodalKvStateSummary(string schemaId, int layers, int keyValueHeads, int pastTokens, int headDimension, string valueSha256, string promptSha256)
        {
            if (string.IsNullOrWhiteSpace(schemaId) || layers <= 0 || keyValueHeads <= 0 || pastTokens <= 0 || headDimension <= 0 || !GenerativeVisionLanguageHash.IsSha256(valueSha256) || !GenerativeVisionLanguageHash.IsSha256(promptSha256)) throw new VisualException(VisualErrorCodes.NativeMultimodalGenerationInvalid, "KV-state shape or identity is invalid.");
            SchemaId = schemaId;
            Layers = layers;
            KeyValueHeads = keyValueHeads;
            PastTokens = pastTokens;
            HeadDimension = headDimension;
            ValueSha256 = valueSha256.ToLowerInvariant();
            PromptSha256 = promptSha256.ToLowerInvariant();
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", schemaId, layers, keyValueHeads, pastTokens, headDimension, ValueSha256, PromptSha256));
        }

        /// <summary>Gets KV schema ID. / 获取 KV Schema ID。</summary>
        public string SchemaId { get; }
        /// <summary>Gets layer count. / 获取层数。</summary>
        public int Layers { get; }
        /// <summary>Gets Key/Value Head count. / 获取 Key/Value Head 数。</summary>
        public int KeyValueHeads { get; }
        /// <summary>Gets final cached sequence length. / 获取最终缓存序列长度。</summary>
        public int PastTokens { get; }
        /// <summary>Gets per-head dimension. / 获取每个 Head 的维度。</summary>
        public int HeadDimension { get; }
        /// <summary>Gets SHA256 over every ordered Key/Value float. / 获取全部有序 Key/Value Float 的 SHA256。</summary>
        public string ValueSha256 { get; }
        /// <summary>Gets bound expanded-prompt SHA256. / 获取绑定的展开 Prompt SHA256。</summary>
        public string PromptSha256 { get; }
        /// <summary>Gets composite KV identity. / 获取复合 KV Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Contains one-run native multimodal stage timings; values are diagnostics, not benchmark claims. / 包含单次原生多模态分阶段 Timing；数值仅作诊断而非基准结论。</summary>
    public sealed class NativeMultimodalExecutionTiming
    {
        private readonly IReadOnlyList<TimeSpan> _decodeSteps;

        /// <summary>Initializes tokenizer, embedding, prefill, token-decode, and final-decode timings. / 初始化 Tokenizer、Embedding、Prefill、逐 Token Decode 与最终 Decode 时间。</summary>
        public NativeMultimodalExecutionTiming(TimeSpan tokenize, TimeSpan tokenEmbedding, TimeSpan prefill, IEnumerable<TimeSpan> decodeSteps, TimeSpan finalDecode)
        {
            if (decodeSteps == null) throw new ArgumentNullException(nameof(decodeSteps));
            Tokenize = tokenize;
            TokenEmbedding = tokenEmbedding;
            Prefill = prefill;
            _decodeSteps = new ReadOnlyCollection<TimeSpan>(decodeSteps.ToList());
            FinalDecode = finalDecode;
            DecodeTotal = TimeSpan.FromTicks(_decodeSteps.Sum(value => value.Ticks));
        }

        /// <summary>Gets chat-template and tokenizer time. / 获取 Chat Template 与 Tokenizer 时间。</summary>
        public TimeSpan Tokenize { get; }
        /// <summary>Gets prompt and generated-token embedding time. / 获取 Prompt 与生成 Token Embedding 时间。</summary>
        public TimeSpan TokenEmbedding { get; }
        /// <summary>Gets empty-past Prefill time. / 获取 Empty-past Prefill 时间。</summary>
        public TimeSpan Prefill { get; }
        /// <summary>Gets non-empty-past decode-step times. / 获取 Non-empty-past Decode Step 时间。</summary>
        public IReadOnlyList<TimeSpan> DecodeSteps => _decodeSteps;
        /// <summary>Gets total non-empty-past decode time. / 获取 Non-empty-past Decode 总时间。</summary>
        public TimeSpan DecodeTotal { get; }
        /// <summary>Gets final tokenizer decode time. / 获取最终 Tokenizer Decode 时间。</summary>
        public TimeSpan FinalDecode { get; }
    }

    /// <summary>Contains a common owned generation result plus KV and native pipeline evidence. / 包含通用自有生成结果及 KV 与 Native Pipeline 证据。</summary>
    public sealed class NativeMultimodalResult
    {
        /// <summary>Initializes one complete owned native multimodal result. / 初始化一个完整自有原生多模态结果。</summary>
        public NativeMultimodalResult(GenerativeVisionLanguageResult generation, NativeMultimodalKvStateSummary kvState, NativeMultimodalExecutionTiming timing)
        {
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            KvState = kvState ?? throw new ArgumentNullException(nameof(kvState));
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        }

        /// <summary>Gets reused common generation/result ownership contract. / 获取复用的通用生成/结果所有权合同。</summary>
        public GenerativeVisionLanguageResult Generation { get; }
        /// <summary>Gets final local KV-state summary; mutable KV tensors are not exposed or retained for reuse. / 获取最终本地 KV State 摘要；不公开或保留可变 KV Tensor 供复用。</summary>
        public NativeMultimodalKvStateSummary KvState { get; }
        /// <summary>Gets one-run native stage timings. / 获取单次 Native 分阶段 Timing。</summary>
        public NativeMultimodalExecutionTiming Timing { get; }
    }

    internal static class NativeMultimodalImagePacker
    {
        internal static Tensor<float> Pack(Tensor<float> raw, float[] newline, NativeMultimodalPreparedImage image, NativeMultimodalProcessorContract contract)
        {
            int crops = image.Grid.PatchCount + 1;
            int side = contract.TokensPerPatchSide;
            int perCrop = checked(side * side);
            int hidden = contract.HiddenSize;
            if (raw.Shape.Rank != 3 || raw.Shape[0] != crops || raw.Shape[1] != perCrop || raw.Shape[2] != hidden || newline == null || newline.Length != hidden) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Vision features or image-newline sidecar differ from the packing contract.", profileId: image.ProfileId);
            float[] source = (float[])raw.Buffer;
            if (source.Any(value => float.IsNaN(value) || float.IsInfinity(value)) || newline.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Vision features or image-newline contain NaN or Infinity.", profileId: image.ProfileId);
            int currentHeight = checked(image.Grid.Rows * side);
            int currentWidth = checked(image.Grid.Columns * side);
            var spatial = new float[checked(hidden * currentHeight * currentWidth)];
            for (int gridRow = 0; gridRow < image.Grid.Rows; gridRow++)
            {
                for (int gridColumn = 0; gridColumn < image.Grid.Columns; gridColumn++)
                {
                    int crop = 1 + (gridRow * image.Grid.Columns) + gridColumn;
                    for (int y = 0; y < side; y++)
                    {
                        for (int x = 0; x < side; x++)
                        {
                            int token = (y * side) + x;
                            int targetY = (gridRow * side) + y;
                            int targetX = (gridColumn * side) + x;
                            int sourceOffset = ((crop * perCrop) + token) * hidden;
                            for (int channel = 0; channel < hidden; channel++) spatial[((channel * currentHeight + targetY) * currentWidth) + targetX] = source[sourceOffset + channel];
                        }
                    }
                }
            }

            int startY = 0;
            int startX = 0;
            int height = currentHeight;
            int width = currentWidth;
            double originalAspect = (double)image.Input.SourceSize.Width / image.Input.SourceSize.Height;
            double currentAspect = (double)currentWidth / currentHeight;
            if (originalAspect > currentAspect)
            {
                double scale = (double)currentWidth / image.Input.SourceSize.Width;
                int newHeight = checked((int)Math.Round(image.Input.SourceSize.Height * scale, 7));
                startY = (currentHeight - newHeight) / 2;
                height = currentHeight - (2 * startY);
            }
            else
            {
                double scale = (double)currentHeight / image.Input.SourceSize.Height;
                int newWidth = checked((int)Math.Round(image.Input.SourceSize.Width * scale, 7));
                startX = (currentWidth - newWidth) / 2;
                width = currentWidth - (2 * startX);
            }
            float[] unpadded = Crop(spatial, hidden, currentHeight, currentWidth, startY, startX, height, width);
            double ratio = Math.Sqrt((double)height * width / (contract.MaximumPackedGridPatches * side * side));
            if (ratio > 1.1)
            {
                int resizedHeight = Math.Max(1, (int)(height / ratio));
                int resizedWidth = Math.Max(1, (int)(width / ratio));
                unpadded = ResizeBilinear(unpadded, hidden, height, width, resizedHeight, resizedWidth);
                height = resizedHeight;
                width = resizedWidth;
            }
            int total = checked(perCrop + (height * (width + 1)));
            if (total != image.PackedImageTokens || total != contract.GetPackedTokenCount(image.Input.SourceSize, image.Grid)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Packed image-token count differs from the single-source processor contract.", profileId: image.ProfileId);
            var packed = new float[checked(total * hidden)];
            Array.Copy(source, 0, packed, 0, checked(perCrop * hidden));
            int outputToken = perCrop;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int outputOffset = outputToken++ * hidden;
                    for (int channel = 0; channel < hidden; channel++) packed[outputOffset + channel] = unpadded[((channel * height + y) * width) + x];
                }
                Array.Copy(newline, 0, packed, outputToken++ * hidden, hidden);
            }
            return new Tensor<float>(new TensorShape(total, hidden), packed, TensorBufferOwnership.Transfer);
        }

        private static float[] Crop(float[] source, int channels, int sourceHeight, int sourceWidth, int startY, int startX, int height, int width)
        {
            var result = new float[checked(channels * height * width)];
            for (int channel = 0; channel < channels; channel++)
                for (int y = 0; y < height; y++)
                    Array.Copy(source, ((channel * sourceHeight + startY + y) * sourceWidth) + startX, result, (channel * height + y) * width, width);
            return result;
        }

        private static float[] ResizeBilinear(float[] source, int channels, int sourceHeight, int sourceWidth, int height, int width)
        {
            var result = new float[checked(channels * height * width)];
            for (int y = 0; y < height; y++)
            {
                double sourceY = ((y + .5) * sourceHeight / height) - .5;
                int y0 = Math.Max(0, Math.Min(sourceHeight - 1, (int)Math.Floor(sourceY)));
                int y1 = Math.Max(0, Math.Min(sourceHeight - 1, y0 + 1));
                float wy = (float)(sourceY - Math.Floor(sourceY));
                for (int x = 0; x < width; x++)
                {
                    double sourceX = ((x + .5) * sourceWidth / width) - .5;
                    int x0 = Math.Max(0, Math.Min(sourceWidth - 1, (int)Math.Floor(sourceX)));
                    int x1 = Math.Max(0, Math.Min(sourceWidth - 1, x0 + 1));
                    float wx = (float)(sourceX - Math.Floor(sourceX));
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int offset = channel * sourceHeight * sourceWidth;
                        float top = (source[offset + (y0 * sourceWidth) + x0] * (1 - wx)) + (source[offset + (y0 * sourceWidth) + x1] * wx);
                        float bottom = (source[offset + (y1 * sourceWidth) + x0] * (1 - wx)) + (source[offset + (y1 * sourceWidth) + x1] * wx);
                        result[((channel * height + y) * width) + x] = (top * (1 - wy)) + (bottom * wy);
                    }
                }
            }
            return result;
        }
    }
}
