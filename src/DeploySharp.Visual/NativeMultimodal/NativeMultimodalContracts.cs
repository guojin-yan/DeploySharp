using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an audited native multimodal instruction family. / 标识已审计的原生多模态指令模型族。</summary>
    public enum NativeMultimodalFamily
    {
        /// <summary>LLaVA and LLaVA-OneVision. / LLaVA 与 LLaVA-OneVision。</summary>
        Llava = 1,
        /// <summary>Qwen-VL, Qwen2-VL, and Qwen2.5-VL. / Qwen-VL、Qwen2-VL 与 Qwen2.5-VL。</summary>
        QwenVisionLanguage = 2,
        /// <summary>Microsoft Phi Vision and Multimodal. / Microsoft Phi Vision 与 Multimodal。</summary>
        PhiVision = 3
    }

    /// <summary>Defines one allowed any-resolution image grid. / 定义一个允许的任意分辨率图像网格。</summary>
    public readonly struct NativeMultimodalImageGrid : IEquatable<NativeMultimodalImageGrid>
    {
        /// <summary>Initializes a grid in model-patch rows and columns. / 按模型 Patch 行列初始化网格。</summary>
        public NativeMultimodalImageGrid(int rows, int columns)
        {
            if (rows <= 0 || columns <= 0) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Image-grid rows and columns must be positive.");
            Rows = rows;
            Columns = columns;
        }

        /// <summary>Gets patch rows. / 获取 Patch 行数。</summary>
        public int Rows { get; }
        /// <summary>Gets patch columns. / 获取 Patch 列数。</summary>
        public int Columns { get; }
        /// <summary>Gets grid patch count excluding the base image. / 获取不含基础图像的网格 Patch 数。</summary>
        public int PatchCount => checked(Rows * Columns);
        /// <summary>Determines whether another grid has the same row and column counts. / 确定另一个网格是否具有相同的行数和列数。</summary>
        public bool Equals(NativeMultimodalImageGrid other) => Rows == other.Rows && Columns == other.Columns;
        /// <summary>Determines whether an object represents the same image grid. / 确定对象是否表示相同的图像网格。</summary>
        public override bool Equals(object? obj) => obj is NativeMultimodalImageGrid other && Equals(other);
        /// <summary>Returns the stable hash code for this grid. / 返回此网格的稳定哈希码。</summary>
        public override int GetHashCode() => (Rows * 397) ^ Columns;
        /// <summary>Returns the grid as rows by columns. / 返回行数乘列数形式的网格字符串。</summary>
        public override string ToString() => Rows + "x" + Columns;
    }

    /// <summary>Binds official any-resolution preprocessing, patch packing, normalization, and image-newline identity. / 绑定官方任意分辨率预处理、Patch 打包、归一化与 Image-newline Identity。</summary>
    public sealed class NativeMultimodalProcessorContract
    {
        private readonly IReadOnlyList<NativeMultimodalImageGrid> _grids;

        /// <summary>Initializes one immutable image processor contract. / 初始化一个不可变图像 Processor 合同。</summary>
        public NativeMultimodalProcessorContract(string processorId, string configSha256, int patchSize, int visionPatchSize, int hiddenSize, int maximumInputGridPatches, int maximumPackedGridPatches, string imageNewlineSha256, IEnumerable<NativeMultimodalImageGrid> grids, string interpolation, int maximumImageBytes = 64 * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(processorId) || !GenerativeVisionLanguageHash.IsSha256(configSha256) || patchSize < visionPatchSize || visionPatchSize <= 0 || hiddenSize <= 0 || maximumInputGridPatches <= 0 || maximumPackedGridPatches <= 0 || maximumPackedGridPatches > maximumInputGridPatches || !GenerativeVisionLanguageHash.IsSha256(imageNewlineSha256) || grids == null || string.IsNullOrWhiteSpace(interpolation) || maximumImageBytes <= 0) throw Invalid("Processor provenance, dimensions, or capacity are invalid.");
            var values = grids.Distinct().OrderBy(value => value.Rows).ThenBy(value => value.Columns).ToList();
            if (values.Count == 0 || values.Any(value => value.PatchCount > maximumInputGridPatches)) throw Invalid("Processor image grids are empty or exceed capacity.");
            ProcessorId = processorId.Trim();
            ConfigSha256 = configSha256.ToLowerInvariant();
            PatchSize = patchSize;
            VisionPatchSize = visionPatchSize;
            TokensPerPatchSide = patchSize / visionPatchSize;
            HiddenSize = hiddenSize;
            MaximumInputGridPatches = maximumInputGridPatches;
            MaximumPackedGridPatches = maximumPackedGridPatches;
            ImageNewlineSha256 = imageNewlineSha256.ToLowerInvariant();
            _grids = new ReadOnlyCollection<NativeMultimodalImageGrid>(values);
            Interpolation = interpolation.Trim();
            MaximumImageBytes = maximumImageBytes;
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", new[] { ProcessorId, ConfigSha256, patchSize.ToString(), visionPatchSize.ToString(), hiddenSize.ToString(), maximumInputGridPatches.ToString(), maximumPackedGridPatches.ToString(), ImageNewlineSha256, Interpolation }.Concat(values.Select(value => value.ToString()))));
        }

        /// <summary>Gets processor ID. / 获取 Processor ID。</summary>
        public string ProcessorId { get; }
        /// <summary>Gets official processor-config SHA256. / 获取官方 Processor 配置 SHA256。</summary>
        public string ConfigSha256 { get; }
        /// <summary>Gets one Vision crop side. / 获取单个 Vision Crop 边长。</summary>
        public int PatchSize { get; }
        /// <summary>Gets Vision backbone patch side. / 获取 Vision Backbone Patch 边长。</summary>
        public int VisionPatchSize { get; }
        /// <summary>Gets token-grid side emitted by one crop. / 获取单 Crop 输出的 Token 网格边长。</summary>
        public int TokensPerPatchSide { get; }
        /// <summary>Gets projected language hidden size. / 获取投影后的语言 Hidden Size。</summary>
        public int HiddenSize { get; }
        /// <summary>Gets maximum high-resolution grid patches, excluding the base crop. / 获取不含基础 Crop 的最大高分辨率网格 Patch 数。</summary>
        public int MaximumInputGridPatches { get; }
        /// <summary>Gets the official packed-feature spatial budget used by anyres downsampling. / 获取 Anyres 下采样使用的官方 Packed-feature 空间预算。</summary>
        public int MaximumPackedGridPatches { get; }
        /// <summary>Gets image-newline sidecar SHA256. / 获取 Image-newline Sidecar SHA256。</summary>
        public string ImageNewlineSha256 { get; }
        /// <summary>Gets allowed grids. / 获取允许的网格。</summary>
        public IReadOnlyList<NativeMultimodalImageGrid> Grids => _grids;
        /// <summary>Gets interpolation identity. / 获取插值 Identity。</summary>
        public string Interpolation { get; }
        /// <summary>Gets maximum encoded image bytes. / 获取最大编码图像字节数。</summary>
        public int MaximumImageBytes { get; }
        /// <summary>Gets stable processor identity. / 获取稳定 Processor Identity。</summary>
        public string Identity { get; }

        /// <summary>Selects the exact official grid by effective then wasted resolution. / 按有效分辨率和浪费分辨率选择精确官方网格。</summary>
        public NativeMultimodalImageGrid SelectGrid(VisualSize sourceSize)
        {
            long maximumEffective = -1;
            long minimumWaste = long.MaxValue;
            NativeMultimodalImageGrid selected = default(NativeMultimodalImageGrid);
            foreach (NativeMultimodalImageGrid grid in _grids)
            {
                int targetHeight = checked(grid.Rows * PatchSize);
                int targetWidth = checked(grid.Columns * PatchSize);
                double scale = Math.Min((double)targetWidth / sourceSize.Width, (double)targetHeight / sourceSize.Height);
                int width = checked((int)(sourceSize.Width * scale));
                int height = checked((int)(sourceSize.Height * scale));
                long effective = Math.Min((long)width * height, (long)sourceSize.Width * sourceSize.Height);
                long waste = ((long)targetWidth * targetHeight) - effective;
                if (effective > maximumEffective || (effective == maximumEffective && waste < minimumWaste))
                {
                    maximumEffective = effective;
                    minimumWaste = waste;
                    selected = grid;
                }
            }
            return selected;
        }

        /// <summary>Computes packed image tokens after official unpadding and per-row newline insertion. / 计算官方 Unpad 与逐行 Newline 插入后的图像 Token 数。</summary>
        public int GetPackedTokenCount(VisualSize sourceSize, NativeMultimodalImageGrid grid)
        {
            if (!_grids.Contains(grid)) throw Invalid("The selected image grid is not allowed by this processor.");
            int side = TokensPerPatchSide;
            int currentHeight = checked(grid.Rows * side);
            int currentWidth = checked(grid.Columns * side);
            int unpaddedHeight = currentHeight;
            int unpaddedWidth = currentWidth;
            double originalAspect = (double)sourceSize.Width / sourceSize.Height;
            double currentAspect = (double)currentWidth / currentHeight;
            if (originalAspect > currentAspect)
            {
                double scale = (double)currentWidth / sourceSize.Width;
                int newHeight = checked((int)Math.Round(sourceSize.Height * scale, 7));
                int padding = (currentHeight - newHeight) / 2;
                unpaddedHeight = currentHeight - (2 * padding);
            }
            else
            {
                double scale = (double)currentHeight / sourceSize.Height;
                int newWidth = checked((int)Math.Round(sourceSize.Width * scale, 7));
                int padding = (currentWidth - newWidth) / 2;
                unpaddedWidth = currentWidth - (2 * padding);
            }
            double ratio = Math.Sqrt((double)unpaddedHeight * unpaddedWidth / (MaximumPackedGridPatches * side * side));
            if (ratio > 1.1)
            {
                unpaddedHeight = Math.Max(1, (int)(unpaddedHeight / ratio));
                unpaddedWidth = Math.Max(1, (int)(unpaddedWidth / ratio));
            }
            int baseTokens = checked(side * side);
            return checked(baseTokens + (unpaddedHeight * (unpaddedWidth + 1)));
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, message);
    }

    /// <summary>Binds Qwen-compatible BPE assets, chat template, image sentinel, and stop tokens. / 绑定 Qwen 兼容 BPE 资产、Chat Template、图像 Sentinel 与停止 Token。</summary>
    public sealed class NativeMultimodalTokenizerContract
    {
        /// <summary>Initializes exact tokenizer assets and token IDs. / 初始化精确 Tokenizer 资产与 Token ID。</summary>
        public NativeMultimodalTokenizerContract(string tokenizerId, string tokenizerJsonSha256, string vocabularySha256, string mergesSha256, string regexPattern, string chatTemplate, int vocabularySize, int imageTokenId, int endOfTextTokenId, int imStartTokenId, int imEndTokenId, int maximumContextTokens, string defaultCaptionPrompt = "Describe this image briefly.")
        {
            if (string.IsNullOrWhiteSpace(tokenizerId) || !GenerativeVisionLanguageHash.IsSha256(tokenizerJsonSha256) || !GenerativeVisionLanguageHash.IsSha256(vocabularySha256) || !GenerativeVisionLanguageHash.IsSha256(mergesSha256) || string.IsNullOrWhiteSpace(regexPattern) || string.IsNullOrWhiteSpace(chatTemplate) || string.IsNullOrWhiteSpace(defaultCaptionPrompt) || vocabularySize <= 0 || imageTokenId < 0 || endOfTextTokenId < 0 || imStartTokenId < 0 || imEndTokenId < 0 || maximumContextTokens <= 0 || new[] { imageTokenId, endOfTextTokenId, imStartTokenId, imEndTokenId }.Any(value => value >= vocabularySize)) throw Invalid("Tokenizer provenance, special tokens, template, or capacity are invalid.");
            TokenizerId = tokenizerId.Trim();
            TokenizerJsonSha256 = tokenizerJsonSha256.ToLowerInvariant();
            VocabularySha256 = vocabularySha256.ToLowerInvariant();
            MergesSha256 = mergesSha256.ToLowerInvariant();
            RegexPattern = regexPattern;
            ChatTemplate = chatTemplate;
            DefaultCaptionPrompt = defaultCaptionPrompt;
            VocabularySize = vocabularySize;
            ImageTokenId = imageTokenId;
            EndOfTextTokenId = endOfTextTokenId;
            ImStartTokenId = imStartTokenId;
            ImEndTokenId = imEndTokenId;
            MaximumContextTokens = maximumContextTokens;
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", TokenizerId, TokenizerJsonSha256, VocabularySha256, MergesSha256, RegexPattern, ChatTemplate, DefaultCaptionPrompt, vocabularySize, imageTokenId, endOfTextTokenId, imStartTokenId, imEndTokenId, maximumContextTokens));
        }

        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer.json SHA256. / 获取 tokenizer.json SHA256。</summary>
        public string TokenizerJsonSha256 { get; }
        /// <summary>Gets vocab.json SHA256. / 获取 vocab.json SHA256。</summary>
        public string VocabularySha256 { get; }
        /// <summary>Gets merges.txt SHA256. / 获取 merges.txt SHA256。</summary>
        public string MergesSha256 { get; }
        /// <summary>Gets official Regex Split pattern. / 获取官方 Regex Split 模式。</summary>
        public string RegexPattern { get; }
        /// <summary>Gets exact single-image chat template. / 获取精确单图 Chat Template。</summary>
        public string ChatTemplate { get; }
        /// <summary>Gets the exact instruction substituted for an empty Caption request. / 获取空 Caption 请求替换使用的精确指令。</summary>
        public string DefaultCaptionPrompt { get; }
        /// <summary>Gets decoder vocabulary size. / 获取 Decoder 词表大小。</summary>
        public int VocabularySize { get; }
        /// <summary>Gets image sentinel token ID. / 获取图像 Sentinel Token ID。</summary>
        public int ImageTokenId { get; }
        /// <summary>Gets end-of-text/padding token ID. / 获取 End-of-text/Padding Token ID。</summary>
        public int EndOfTextTokenId { get; }
        /// <summary>Gets chat message-start token ID. / 获取聊天消息开始 Token ID。</summary>
        public int ImStartTokenId { get; }
        /// <summary>Gets chat message-end/EOS token ID. / 获取聊天消息结束/EOS Token ID。</summary>
        public int ImEndTokenId { get; }
        /// <summary>Gets maximum expanded context tokens. / 获取最大展开 Context Token 数。</summary>
        public int MaximumContextTokens { get; }
        /// <summary>Gets stable tokenizer identity. / 获取稳定 Tokenizer Identity。</summary>
        public string Identity { get; }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, message);
    }

    /// <summary>Binds exact named past/present KV axes and bounded state capacity. / 绑定精确具名 Past/Present KV 轴和受限状态容量。</summary>
    public sealed class NativeMultimodalKvCacheContract
    {
        /// <summary>Initializes an exact decoder KV schema. / 初始化精确 Decoder KV Schema。</summary>
        public NativeMultimodalKvCacheContract(string schemaId, int layerCount, int keyValueHeads, int headDimension, int maximumPastTokens, string pastPrefix = "past_key_values", string presentPrefix = "present")
        {
            if (string.IsNullOrWhiteSpace(schemaId) || layerCount <= 0 || keyValueHeads <= 0 || headDimension <= 0 || maximumPastTokens <= 0 || string.IsNullOrWhiteSpace(pastPrefix) || string.IsNullOrWhiteSpace(presentPrefix)) throw Invalid("KV schema identity, axes, or capacity are invalid.");
            SchemaId = schemaId.Trim();
            LayerCount = layerCount;
            KeyValueHeads = keyValueHeads;
            HeadDimension = headDimension;
            MaximumPastTokens = maximumPastTokens;
            PastPrefix = pastPrefix.Trim();
            PresentPrefix = presentPrefix.Trim();
            Identity = GenerativeVisionLanguageHash.Text(string.Join("|", SchemaId, layerCount, keyValueHeads, headDimension, maximumPastTokens, PastPrefix, PresentPrefix));
        }

        /// <summary>Gets KV schema ID. / 获取 KV Schema ID。</summary>
        public string SchemaId { get; }
        /// <summary>Gets decoder layer count. / 获取 Decoder 层数。</summary>
        public int LayerCount { get; }
        /// <summary>Gets key/value head count. / 获取 Key/Value Head 数。</summary>
        public int KeyValueHeads { get; }
        /// <summary>Gets per-head dimension. / 获取每个 Head 的维度。</summary>
        public int HeadDimension { get; }
        /// <summary>Gets maximum cached past tokens. / 获取最大缓存 Past Token 数。</summary>
        public int MaximumPastTokens { get; }
        /// <summary>Gets past input prefix. / 获取 Past 输入前缀。</summary>
        public string PastPrefix { get; }
        /// <summary>Gets present output prefix. / 获取 Present 输出前缀。</summary>
        public string PresentPrefix { get; }
        /// <summary>Gets stable schema identity. / 获取稳定 Schema Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets exact past key name. / 获取精确 Past Key 名称。</summary>
        public string PastKey(int layer) => Port(PastPrefix, layer, "key");
        /// <summary>Gets exact past value name. / 获取精确 Past Value 名称。</summary>
        public string PastValue(int layer) => Port(PastPrefix, layer, "value");
        /// <summary>Gets exact present key name. / 获取精确 Present Key 名称。</summary>
        public string PresentKey(int layer) => Port(PresentPrefix, layer, "key");
        /// <summary>Gets exact present value name. / 获取精确 Present Value 名称。</summary>
        public string PresentValue(int layer) => Port(PresentPrefix, layer, "value");

        private string Port(string prefix, int layer, string suffix)
        {
            if (layer < 0 || layer >= LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
            return prefix + "." + layer + "." + suffix;
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, message);
    }

    /// <summary>Defines one immutable native multimodal multi-graph bundle contract. / 定义一个不可变原生多模态多图 Bundle 合同。</summary>
    public sealed class NativeMultimodalProfile
    {
        private readonly IReadOnlyList<GenerativeVisionLanguageArtifactContract> _artifacts;
        private readonly IReadOnlyList<GenerativeVisionLanguageTask> _tasks;

        /// <summary>Initializes an executable profile or explicit official blocker. / 初始化可执行 Profile 或显式官方 Blocker。</summary>
        public NativeMultimodalProfile(string profileId, NativeMultimodalFamily family, string variant, string version, NativeMultimodalProcessorContract processor, NativeMultimodalTokenizerContract tokenizer, NativeMultimodalKvCacheContract kvCache, GenerativeVisionLanguageGenerationContract generation, IEnumerable<GenerativeVisionLanguageTask> tasks, IEnumerable<GenerativeVisionLanguageArtifactContract> artifacts, bool executable, string? blocker = null, int maximumRequestCharacters = 4096)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (!Enum.IsDefined(typeof(NativeMultimodalFamily), family) || string.IsNullOrWhiteSpace(variant) || string.IsNullOrWhiteSpace(version) || processor == null || tokenizer == null || kvCache == null || generation == null || tasks == null || artifacts == null || maximumRequestCharacters <= 0) throw Invalid("Profile identity, contracts, or capacity are invalid.", ProfileId);
            var taskList = tasks.Distinct().ToList();
            if (taskList.Count == 0 || taskList.Any(value => !Enum.IsDefined(typeof(GenerativeVisionLanguageTask), value))) throw Invalid("At least one valid generation task is required.", ProfileId);
            var artifactList = artifacts.ToList();
            if (artifactList.Any(value => value == null) || artifactList.Select(value => value.Role).Distinct().Count() != artifactList.Count) throw Invalid("Artifact roles must be unique.", ProfileId);
            if (executable)
            {
                foreach (GenerativeVisionLanguageArtifactRole role in new[] { GenerativeVisionLanguageArtifactRole.VisionEncoder, GenerativeVisionLanguageArtifactRole.TokenEmbedding, GenerativeVisionLanguageArtifactRole.LanguageDecoder }) if (!artifactList.Any(value => value.Role == role)) throw Invalid("Executable profiles require Vision, Token Embedding, and Decoder artifacts.", ProfileId);
                if (generation.Mode != GenerativeVisionLanguageGenerationMode.Greedy || generation.CacheMode != GenerativeVisionLanguageCacheMode.PastPresent || generation.LengthMode != GenerativeVisionLanguageLengthMode.NewTokens) throw Invalid("The native session requires greedy, past/present, new-token generation semantics.", ProfileId);
            }
            Family = family;
            Variant = variant.Trim();
            Version = version.Trim();
            Processor = processor;
            Tokenizer = tokenizer;
            KvCache = kvCache;
            Generation = generation;
            _tasks = new ReadOnlyCollection<GenerativeVisionLanguageTask>(taskList);
            _artifacts = new ReadOnlyCollection<GenerativeVisionLanguageArtifactContract>(artifactList);
            Executable = executable;
            Blocker = string.IsNullOrWhiteSpace(blocker) ? null : blocker!.Trim();
            if (!executable && Blocker == null) throw Invalid("A non-executable profile requires a reproducible blocker.", ProfileId);
            MaximumRequestCharacters = maximumRequestCharacters;
            ArtifactIdentity = GenerativeVisionLanguageHash.Text(string.Join("|", artifactList.OrderBy(value => value.Role).Select(value => value.Role + ":" + value.Sha256).Concat(new[] { processor.Identity, tokenizer.Identity, kvCache.Identity, generation.Identity })));
        }

        /// <summary>Gets stable Profile ID. / 获取稳定 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets model family. / 获取模型族。</summary>
        public NativeMultimodalFamily Family { get; }
        /// <summary>Gets exact variant. / 获取精确变体。</summary>
        public string Variant { get; }
        /// <summary>Gets version/revision identity. / 获取版本/Revision Identity。</summary>
        public string Version { get; }
        /// <summary>Gets image processor contract. / 获取图像 Processor 合同。</summary>
        public NativeMultimodalProcessorContract Processor { get; }
        /// <summary>Gets tokenizer/chat-template contract. / 获取 Tokenizer/Chat Template 合同。</summary>
        public NativeMultimodalTokenizerContract Tokenizer { get; }
        /// <summary>Gets KV schema. / 获取 KV Schema。</summary>
        public NativeMultimodalKvCacheContract KvCache { get; }
        /// <summary>Gets deterministic generation contract. / 获取确定性生成合同。</summary>
        public GenerativeVisionLanguageGenerationContract Generation { get; }
        /// <summary>Gets supported tasks. / 获取支持的任务。</summary>
        public IReadOnlyList<GenerativeVisionLanguageTask> Tasks => _tasks;
        /// <summary>Gets sub-artifact contracts. / 获取子工件合同。</summary>
        public IReadOnlyList<GenerativeVisionLanguageArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets whether the complete native bundle is executable. / 获取完整 Native Bundle 是否可执行。</summary>
        public bool Executable { get; }
        /// <summary>Gets blocker for an unavailable family path. / 获取不可用模型族路径的 Blocker。</summary>
        public string? Blocker { get; }
        /// <summary>Gets maximum request characters. / 获取最大请求字符数。</summary>
        public int MaximumRequestCharacters { get; }
        /// <summary>Gets identity over every artifact and processing/state contract. / 获取覆盖全部工件及处理/状态合同的 Identity。</summary>
        public string ArtifactIdentity { get; }

        /// <summary>Gets one exact artifact contract. / 获取一个精确工件合同。</summary>
        public GenerativeVisionLanguageArtifactContract GetArtifact(GenerativeVisionLanguageArtifactRole role)
        {
            if (!Executable) throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, Blocker ?? "The native multimodal profile is unavailable.", profileId: ProfileId);
            return _artifacts.SingleOrDefault(value => value.Role == role) ?? throw Invalid("The requested artifact role is absent.", ProfileId);
        }

        /// <summary>Creates a concrete external artifact after the application locates it. / 应用定位外部工件后创建具体 Artifact。</summary>
        public ModelArtifact CreateArtifact(GenerativeVisionLanguageArtifactRole role, string path, BackendId backendId) => new ModelArtifact(GetArtifact(role).ModelId, GetArtifact(role).Format, path, GetArtifact(role).Sha256, backendId);

        private static VisualException Invalid(string message, string profileId) => new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, message, profileId: profileId);
    }

    /// <summary>Binds concrete external files to all native multimodal subgraphs. / 将具体外部文件绑定到全部原生多模态子图。</summary>
    public sealed class NativeMultimodalArtifactBundle
    {
        private readonly IReadOnlyDictionary<GenerativeVisionLanguageArtifactRole, ModelArtifact> _artifacts;

        /// <summary>Initializes and validates a complete concrete bundle. / 初始化并校验完整具体 Bundle。</summary>
        public NativeMultimodalArtifactBundle(NativeMultimodalProfile profile, IEnumerable<GenerativeVisionLanguageArtifactBinding> artifacts)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, profile.Blocker ?? "The profile is unavailable.", profileId: profile.ProfileId);
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            var dictionary = new Dictionary<GenerativeVisionLanguageArtifactRole, ModelArtifact>();
            foreach (GenerativeVisionLanguageArtifactBinding binding in artifacts)
            {
                if (binding == null || dictionary.ContainsKey(binding.Role)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Concrete artifact roles must be unique.", profileId: profile.ProfileId);
                GenerativeVisionLanguageArtifactContract contract = profile.GetArtifact(binding.Role);
                if (binding.Artifact.ModelId != contract.ModelId || !string.Equals(binding.Artifact.Format, contract.Format, StringComparison.OrdinalIgnoreCase) || !string.Equals(binding.Artifact.Sha256, contract.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.NativeMultimodalIdentityMismatch, "Concrete artifact identity differs from the profile.", profileId: profile.ProfileId, modelId: binding.Artifact.ModelId);
                dictionary.Add(binding.Role, binding.Artifact);
            }
            foreach (GenerativeVisionLanguageArtifactContract contract in profile.Artifacts) if (!dictionary.ContainsKey(contract.Role)) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "A required concrete artifact role is missing.", profileId: profile.ProfileId, modelId: contract.ModelId);
            _artifacts = new ReadOnlyDictionary<GenerativeVisionLanguageArtifactRole, ModelArtifact>(dictionary);
        }

        /// <summary>Gets immutable Profile. / 获取不可变 Profile。</summary>
        public NativeMultimodalProfile Profile { get; }
        /// <summary>Gets one concrete artifact. / 获取一个具体工件。</summary>
        public ModelArtifact GetArtifact(GenerativeVisionLanguageArtifactRole role) => _artifacts.TryGetValue(role, out ModelArtifact? value) ? value : throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "A concrete artifact role is missing.", profileId: Profile.ProfileId);
    }

    /// <summary>Contains one single-decode image tensor and exact any-resolution patch metadata. / 包含单次 Decode 图像张量与精确任意分辨率 Patch 元数据。</summary>
    public sealed class NativeMultimodalPreparedImage : IDisposable
    {
        /// <summary>Initializes an owned prepared-image contract around a common prepared tensor. / 基于通用已准备 Tensor 初始化自有图像合同。</summary>
        public NativeMultimodalPreparedImage(string profileId, PreparedVisualInput input, NativeMultimodalImageGrid grid, int packedImageTokens)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (grid.Rows <= 0 || grid.Columns <= 0 || packedImageTokens <= 0 || input.BatchSize != grid.PatchCount + 1) throw new VisualException(VisualErrorCodes.NativeMultimodalContractInvalid, "Prepared patch count, grid, or image-token count is invalid.", profileId: ProfileId);
            Grid = grid;
            PackedImageTokens = packedImageTokens;
        }

        /// <summary>Gets bound Profile ID. / 获取绑定的 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets common prepared tensor and source identity. / 获取通用已准备 Tensor 与源图 Identity。</summary>
        public PreparedVisualInput Input { get; }
        /// <summary>Gets selected any-resolution grid. / 获取选择的任意分辨率网格。</summary>
        public NativeMultimodalImageGrid Grid { get; }
        /// <summary>Gets exact packed image-token count. / 获取精确打包图像 Token 数。</summary>
        public int PackedImageTokens { get; }
        /// <inheritdoc />
        /// <remarks>Disposes only resources owned by the wrapped prepared input. / 仅释放被包装已准备输入拥有的资源。</remarks>
        public void Dispose() => Input.Dispose();
    }

    /// <summary>Contains exact expanded chat tokens and image-sentinel provenance. / 包含精确展开的聊天 Token 与图像 Sentinel Provenance。</summary>
    public sealed class NativeMultimodalTokenSequence
    {
        private readonly IReadOnlyList<long> _tokenIds;

        /// <summary>Initializes an owned expanded prompt. / 初始化自有展开 Prompt。</summary>
        public NativeMultimodalTokenSequence(string normalizedPrompt, IEnumerable<long> tokenIds, int imageTokenCount, string tokenizerId, string tokenizerSha256)
        {
            if (normalizedPrompt == null || tokenIds == null || imageTokenCount <= 0 || string.IsNullOrWhiteSpace(tokenizerId) || !GenerativeVisionLanguageHash.IsSha256(tokenizerSha256)) throw new VisualException(VisualErrorCodes.NativeMultimodalTokenizerInvalid, "Expanded prompt identity is invalid.");
            var values = tokenIds.ToList();
            if (values.Count == 0) throw new VisualException(VisualErrorCodes.NativeMultimodalTokenizerInvalid, "Expanded prompt cannot be empty.");
            NormalizedPrompt = normalizedPrompt;
            _tokenIds = new ReadOnlyCollection<long>(values);
            ImageTokenCount = imageTokenCount;
            TokenizerId = tokenizerId.Trim();
            TokenizerSha256 = tokenizerSha256.ToLowerInvariant();
            ContentSha256 = GenerativeVisionLanguageHash.Text(normalizedPrompt + "|" + string.Join(",", values));
        }

        /// <summary>Gets template-applied prompt before image-sentinel repetition. / 获取图像 Sentinel 重复前的模板化 Prompt。</summary>
        public string NormalizedPrompt { get; }
        /// <summary>Gets owned expanded token IDs. / 获取自有展开 Token ID。</summary>
        public IReadOnlyList<long> TokenIds => _tokenIds;
        /// <summary>Gets repeated image-sentinel count. / 获取重复图像 Sentinel 数。</summary>
        public int ImageTokenCount { get; }
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets tokenizer identity SHA256. / 获取 Tokenizer Identity SHA256。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Gets prompt/token content SHA256. / 获取 Prompt/Token 内容 SHA256。</summary>
        public string ContentSha256 { get; }
        /// <summary>Copies token IDs for backend submission. / 复制 Token ID 以提交 Backend。</summary>
        public long[] CopyTokenIds() => _tokenIds.ToArray();
    }

    /// <summary>Converts exact chat text to expanded IDs and decodes completion IDs. / 将精确聊天文本转换为展开 ID 并解码 Completion ID。</summary>
    public interface INativeMultimodalTokenizer
    {
        /// <summary>Gets tokenizer ID. / 获取 Tokenizer ID。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets verified composite tokenizer SHA256. / 获取已校验的复合 Tokenizer SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Applies the exact chat template and repeats one image sentinel to the packed feature count. / 应用精确 Chat Template，并将一个图像 Sentinel 重复到打包 Feature 数。</summary>
        public NativeMultimodalTokenSequence Encode(NativeMultimodalProfile profile, GenerativeVisionLanguageRequest request, int imageTokenCount);
        /// <summary>Decodes completion IDs while excluding bound stop/padding tokens. / 解码 Completion ID，并排除绑定的停止/Padding Token。</summary>
        public string DecodeCompletion(IEnumerable<int> tokenIds);
    }
}
