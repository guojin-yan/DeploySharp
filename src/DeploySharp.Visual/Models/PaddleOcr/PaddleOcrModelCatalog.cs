using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Describes one core PP-OCR detector, recognizer, or classifier row. / 描述一个核心 PP-OCR 检测、识别或分类模型行。</summary>
    public sealed class PaddleOcrModelDescriptor
    {
        internal PaddleOcrModelDescriptor(string modelId, string version, string family, string name, string inputName, string outputName, string codeProfile, bool legacyClassification, bool dictionaryRequired)
        {
            ModelId = new ModelId(modelId);
            Version = version;
            Family = family;
            Name = name;
            InputName = inputName;
            OutputName = outputName;
            CodeProfile = codeProfile;
            IsLegacyClassification = legacyClassification;
            DictionaryRequired = dictionaryRequired;
        }

        /// <summary>Gets the stable catalog model ID. / 获取稳定目录模型 ID。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the upstream model generation. / 获取上游模型代际。</summary>
        public string Version { get; }
        /// <summary>Gets det/rec/cls family. / 获取 det/rec/cls 模型族。</summary>
        public string Family { get; }
        /// <summary>Gets the upstream model name. / 获取上游模型名称。</summary>
        public string Name { get; }
        /// <summary>Gets the exact input tensor name. / 获取精确输入张量名称。</summary>
        public string InputName { get; }
        /// <summary>Gets the exact output tensor name. / 获取精确输出张量名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the factory method used by this row. / 获取该行对应的 Profile 工厂。</summary>
        public string CodeProfile { get; }
        /// <summary>Gets whether this is the legacy BGR classifier contract. / 获取是否为旧版 BGR 分类合同。</summary>
        public bool IsLegacyClassification { get; }
        /// <summary>Gets whether the row requires a CTC dictionary. / 获取是否需要 CTC 字典。</summary>
        public bool DictionaryRequired { get; }
    }

    /// <summary>Catalog of the core PP-OCR v4/v5/v6 model contracts. / PP-OCR v4/v5/v6 核心模型合同目录。</summary>
    public static class PaddleOcrModelCatalog
    {
        private static readonly IReadOnlyList<PaddleOcrModelDescriptor> Entries = new[]
        {
            Descriptor("paddleocr/ppocrv4/mobile-det", "v4", "det", "PP-OCRv4_mobile_det", "x", "sigmoid_0.tmp_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv4/server-det", "v4", "det", "PP-OCRv4_server_det", "x", "fetch_name_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv4/mobile-rec", "v4", "rec", "PP-OCRv4_mobile_rec", "x", "softmax_11.tmp_0", "CreateRecognition", false, true),
            Descriptor("paddleocr/ppocrv4/server-rec", "v4", "rec", "PP-OCRv4_server_rec", "x", "fetch_name_0", "CreateRecognition", false, true),
            Descriptor("paddleocr/ppocrv4/legacy-cls", "v4", "cls", "ch_ppocr_mobile_v2.0_cls", "x", "softmax_0.tmp_0", "CreateLegacyClassification", true, false),
            Descriptor("paddleocr/ppocrv5/mobile-det", "v5", "det", "PP-OCRv5_mobile_det", "x", "fetch_name_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv5/server-det", "v5", "det", "PP-OCRv5_server_det", "x", "fetch_name_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv5/mobile-rec", "v5", "rec", "PP-OCRv5_mobile_rec", "x", "fetch_name_0", "CreateRecognition", false, true),
            Descriptor("paddleocr/ppocrv5/server-rec", "v5", "rec", "PP-OCRv5_server_rec", "x", "fetch_name_0", "CreateRecognition", false, true),
            Descriptor("paddleocr/ppocrv5/mobile-cls", "v5", "cls", "PP-LCNet_x0_25_textline_ori", "x", "fetch_name_0", "CreateTextLineOrientationClassification", false, false),
            Descriptor("paddleocr/ppocrv5/server-cls", "v5", "cls", "PP-LCNet_x1_0_textline_ori", "x", "fetch_name_0", "CreateTextLineOrientationClassification", false, false),
            Descriptor("paddleocr/ppocrv6/tiny-det", "v6", "det", "PP-OCRv6_tiny_det", "x", "fetch_name_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv6/tiny-rec", "v6", "rec", "PP-OCRv6_tiny_rec", "x", "fetch_name_0", "CreateRecognition", false, true),
            Descriptor("paddleocr/ppocrv6/small-det", "v6", "det", "PP-OCRv6_small_det", "x", "fetch_name_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv6/small-rec", "v6", "rec", "PP-OCRv6_small_rec", "x", "fetch_name_0", "CreateRecognition", false, true),
            Descriptor("paddleocr/ppocrv6/medium-det", "v6", "det", "PP-OCRv6_medium_det", "x", "fetch_name_0", "CreateDetection", false, false),
            Descriptor("paddleocr/ppocrv6/medium-rec", "v6", "rec", "PP-OCRv6_medium_rec", "x", "fetch_name_0", "CreateRecognition", false, true)
        };

        /// <summary>Gets all catalogued core contracts. / 获取全部核心模型合同。</summary>
        public static IReadOnlyList<PaddleOcrModelDescriptor> All => Entries;

        /// <summary>Finds a contract by stable model ID. / 按稳定模型 ID 查找合同。</summary>
        public static PaddleOcrModelDescriptor Find(ModelId modelId)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            for (int index = 0; index < Entries.Count; index++)
                if (Entries[index].ModelId.Value.Equals(modelId.Value, StringComparison.OrdinalIgnoreCase)) return Entries[index];
            throw new KeyNotFoundException("Unknown PaddleOCR model ID: " + modelId.Value);
        }

        /// <summary>Creates the correct existing DeploySharp Profile for a catalog row. / 为目录行创建对应的 DeploySharp Profile。</summary>
        public static PaddleOcrProfile CreateProfile(PaddleOcrModelDescriptor descriptor, PaddleOcrArtifactContract artifact, OcrCharacterSet? characterSet = null, int maximumBatch = 64)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (descriptor.Family == "det") return PaddleOcrProfiles.CreateDetection(descriptor.ModelId, artifact, descriptor.InputName, descriptor.OutputName);
            if (descriptor.Family == "rec")
            {
                if (characterSet == null) throw new ArgumentNullException(nameof(characterSet), "A recognition dictionary is required for a PaddleOCR rec profile.");
                return PaddleOcrProfiles.CreateRecognition(descriptor.ModelId, artifact, characterSet, descriptor.InputName, descriptor.OutputName, maximumBatch: maximumBatch);
            }
            if (descriptor.IsLegacyClassification) return PaddleOcrProfiles.CreateLegacyClassification(descriptor.ModelId, artifact, descriptor.InputName, descriptor.OutputName, maximumBatch: maximumBatch, allowDynamicBatch: maximumBatch > 1);
            return PaddleOcrProfiles.CreateTextLineOrientationClassification(descriptor.ModelId, artifact, descriptor.InputName, descriptor.OutputName, maximumBatch: maximumBatch, allowDynamicBatch: maximumBatch > 1);
        }

        /// <summary>Finds a row and creates its Profile in one call. / 查找目录行并一次性创建 Profile。</summary>
        public static PaddleOcrProfile CreateProfile(ModelId modelId, PaddleOcrArtifactContract artifact, OcrCharacterSet? characterSet = null, int maximumBatch = 64)
            => CreateProfile(Find(modelId), artifact, characterSet, maximumBatch);

        private static PaddleOcrModelDescriptor Descriptor(string modelId, string version, string family, string name, string inputName, string outputName, string codeProfile, bool legacyClassification, bool dictionaryRequired)
            => new PaddleOcrModelDescriptor(modelId, version, family, name, inputName, outputName, codeProfile, legacyClassification, dictionaryRequired);
    }
}
