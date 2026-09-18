using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Bundles a Paddle document model descriptor with an executable backend-neutral profile. / 将 Paddle 文档模型描述符与后端无关的可执行 Profile 绑定。</summary>
    public sealed class PaddleDocumentProfile
    {
        public PaddleDocumentProfile(PaddleDocumentModelDescriptor descriptor, VisualModelProfile visualProfile)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            VisualProfile = visualProfile ?? throw new ArgumentNullException(nameof(visualProfile));
            if (!string.Equals(descriptor.ModelId, visualProfile.ModelId.Value, StringComparison.Ordinal)) throw new ArgumentException("The descriptor and visual profile model IDs must match.", nameof(visualProfile));
        }

        public PaddleDocumentModelDescriptor Descriptor { get; }
        public VisualModelProfile VisualProfile { get; }
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null) => new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, Descriptor.Sha256, preferredBackend);
    }

    /// <summary>Builds reusable PP-Structure profiles. These builders describe tensor contracts; exported ONNX names and preprocessing must be verified for each conversion. / 创建可复用 PP-Structure Profile；这些构建器描述张量合同，具体导出 ONNX 名称和预处理必须针对每次转换验证。</summary>
    public static class PaddleDocumentProfiles
    {
        /// <summary>Creates a document-orientation or table-classification score profile. / 创建文档方向或表格分类分数 Profile。</summary>
        public static PaddleDocumentProfile CreateClassification(PaddleDocumentModelDescriptor descriptor, IEnumerable<string> labels, VisualTaskId task, string inputName = "x", string outputName = "fetch_name_0", VisualSize? modelSize = null, int maximumBatch = 1, bool allowDynamicBatch = false, ClassificationScoreMode scoreMode = ClassificationScoreMode.Logits, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (task != VisualTaskId.DocumentOrientation && task != VisualTaskId.TableClassification) throw new ArgumentException("Classification builder only accepts document-orientation or table-classification tasks.", nameof(task));
            var labelValues = new List<string>(labels);
            if (labelValues.Count == 0) throw new ArgumentException("At least one label is required.", nameof(labels));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            if (maximumBatch > 1 && !allowDynamicBatch) throw new ArgumentException("A classification batch larger than one requires allowDynamicBatch=true.", nameof(allowDynamicBatch));
            if (string.IsNullOrWhiteSpace(inputName) || string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("Exact tensor names are required.");
            VisualSize size = modelSize ?? new VisualSize(224, 224);
            long batch = allowDynamicBatch ? -1 : 1;
            var visualLabels = new List<VisualLabel>(labelValues.Count);
            for (int index = 0; index < labelValues.Count; index++) visualLabels.Add(new VisualLabel(index, labelValues[index]));
            var decoder = new ClassificationDecoder(outputName, scoreMode, 1, 0, task);
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".classification", new ModelId(descriptor.ModelId), task, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(batch, 3, size.Height, size.Width), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(batch, labelValues.Count)) }, visualLabels, decoder,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(size, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet, VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Creates a dense region profile for layout, seal, or table-cell models after a converter has exposed a [batch,candidates,fields] tensor. / 为版面、印章或表格单元格模型创建密集区域 Profile；前提是转换器已暴露 [batch,candidates,fields] 张量。</summary>
        public static PaddleDocumentProfile CreateRegionDetection(PaddleDocumentModelDescriptor descriptor, IEnumerable<string> labels, VisualTaskId task, string inputName = "x", string outputName = "output", VisualSize? modelSize = null, DetectionBoxFormat boxFormat = DetectionBoxFormat.Xyxy, bool normalizedCoordinates = true, int classScoreOffset = 4, DetectionScoreMode scoreMode = DetectionScoreMode.ClassScore, int objectnessIndex = -1, DetectionDecoderOptions? decoderOptions = null, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (task != VisualTaskId.LayoutDetection && task != VisualTaskId.SealTextDetection && task != VisualTaskId.TableCellDetection) throw new ArgumentException("Region builder only accepts layout, seal, or table-cell tasks.", nameof(task));
            var labelValues = new List<string>(labels);
            if (labelValues.Count == 0) throw new ArgumentException("At least one label is required.", nameof(labels));
            if (string.IsNullOrWhiteSpace(inputName) || string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("Exact tensor names are required.");
            int fieldCount = checked(classScoreOffset + labelValues.Count);
            var visualLabels = new List<VisualLabel>(labelValues.Count);
            for (int index = 0; index < labelValues.Count; index++) visualLabels.Add(new VisualLabel(index, labelValues[index]));
            var schema = new DetectionOutputSchema(outputName, boxFormat, normalizedCoordinates, scoreMode, labelValues.Count, classScoreOffset, objectnessIndex);
            var decoder = new DetectionDecoder(schema, decoderOptions, task);
            VisualSize size = modelSize ?? new VisualSize(640, 640);
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".regions", new ModelId(descriptor.ModelId), task, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(1, 3, size.Height, size.Width), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(-1, -1, fieldCount)) }, visualLabels, decoder,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(size, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet, VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Creates a profile for Paddle's post-NMS [class,score,x1,y1,x2,y2] exports used by layout and cell models. / 为版面和单元格模型使用的 Paddle 后置 NMS 输出创建 Profile。</summary>
        public static PaddleDocumentProfile CreatePaddleNmsRegions(PaddleDocumentModelDescriptor descriptor, IEnumerable<string> labels, VisualSize modelSize, string imageInputName = "image", string outputName = "fetch_name_0", string countOutputName = "fetch_name_1", bool includeGeometryInputs = false, int maximumBatch = 1, float scoreThreshold = 0, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (descriptor.Module != PaddleDocumentModule.LayoutDetection && descriptor.Module != PaddleDocumentModule.TableCellDetection && descriptor.Module != PaddleDocumentModule.SealTextDetection) throw new ArgumentException("The descriptor must identify a region-detection module.", nameof(descriptor));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            var labelValues = new List<string>(labels);
            if (labelValues.Count == 0) throw new ArgumentException("At least one label is required.", nameof(labels));
            var decoder = new PaddleDocumentNmsDecoder(descriptor, labelValues, outputName, countOutputName, false, scoreThreshold);
            var auxiliary = new List<VisualAuxiliaryInputBinding>();
            if (includeGeometryInputs)
            {
                auxiliary.Add(new VisualAuxiliaryInputBinding("im_shape", TensorElementType.Float32, new TensorShape(-1, 2)));
                auxiliary.Add(new VisualAuxiliaryInputBinding("scale_factor", TensorElementType.Float32, new TensorShape(-1, 2)));
            }
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".paddle-nms", new ModelId(descriptor.ModelId), decoder.Task, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding(imageInputName, TensorElementType.Float32, new TensorShape(-1, 3, modelSize.Height, modelSize.Width), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(-1, 6)), new VisualOutputBinding(countOutputName, TensorElementType.Int64, new TensorShape(-1)) }, labelsToVisual(labelValues), decoder, auxiliaryInputs: auxiliary,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(modelSize, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet, VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Creates the official two-output SLANeXt table-structure profile. / 创建官方双输出 SLANeXt 表格结构 Profile。</summary>
        public static PaddleDocumentProfile CreateTableStructure(PaddleDocumentModelDescriptor descriptor, PaddleDocumentTableStructureSchema? schema = null, PaddleDocumentTableStructureDecoderOptions? decoderOptions = null, VisualSize? modelSize = null, int maximumBatch = 1, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.TableStructureRecognition) throw new ArgumentException("The descriptor must identify table-structure recognition.", nameof(descriptor));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            PaddleDocumentTableStructureSchema resolvedSchema = schema ?? PaddleDocumentTableStructureSchema.Standard();
            VisualSize size = modelSize ?? new VisualSize(512, 512);
            long batch = maximumBatch == 1 ? 1 : -1;
            var decoder = new PaddleDocumentTableStructureDecoder(descriptor, resolvedSchema, decoderOptions);
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".table-structure", new ModelId(descriptor.ModelId), VisualTaskId.TableStructureRecognition, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding("x", TensorElementType.Float32, new TensorShape(batch, 3, size.Height, size.Width), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[]
                {
                    new VisualOutputBinding(resolvedSchema.LocationOutputName, TensorElementType.Float32, new TensorShape(-1, -1, 8)),
                    new VisualOutputBinding(resolvedSchema.StructureOutputName, TensorElementType.Float32, new TensorShape(-1, -1, resolvedSchema.Tokens.Count))
                },
                Array.Empty<VisualLabel>(), decoder,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(size, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet, VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Creates a dynamic UVDoc corrected-image profile. / 创建动态 UVDoc 矫正图 Profile。</summary>
        public static PaddleDocumentProfile CreateUnwarping(PaddleDocumentModelDescriptor descriptor, VisualSize? modelSize = null, string outputName = "fetch_name_0", int maximumBatch = 1, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.TextImageUnwarping) throw new ArgumentException("The descriptor must identify text-image unwarping.", nameof(descriptor));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            VisualSize size = modelSize ?? new VisualSize(224, 224);
            var decoder = new PaddleDocumentUnwarpingDecoder(descriptor, outputName);
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".unwarping", new ModelId(descriptor.ModelId), VisualTaskId.DocumentUnwarping, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding("image", TensorElementType.Float32, new TensorShape(-1, 3, -1, -1), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(-1, 3, -1, -1)) }, Array.Empty<VisualLabel>(), decoder,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(size, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.None, VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Creates a FormulaNet/UniMERNet integer-token profile with an explicit vocabulary. / 使用显式词表创建 FormulaNet/UniMERNet 整数 token Profile。</summary>
        public static PaddleDocumentProfile CreateFormula(PaddleDocumentModelDescriptor descriptor, PaddleDocumentFormulaSchema schema, VisualSize? modelSize = null, string inputName = "x", string outputName = "fetch_name_0", int maximumSequenceLength = 4096, int maximumBatch = 1, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (descriptor.Module != PaddleDocumentModule.FormulaRecognition) throw new ArgumentException("The descriptor must identify formula recognition.", nameof(descriptor));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            VisualSize size = modelSize ?? new VisualSize(384, 384);
            var decoder = new PaddleDocumentFormulaDecoder(descriptor, schema, outputName, maximumSequenceLength);
            long batch = maximumBatch == 1 ? 1 : -1;
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".formula", new ModelId(descriptor.ModelId), VisualTaskId.FormulaRecognition, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(batch, 1, size.Height, size.Width), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Int64, new TensorShape(-1, -1)) }, Array.Empty<VisualLabel>(), decoder,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(size, VisualResizeMode.Resize, VisualColorOrder.Gray, VisualNormalizationOptions.DivideByStandardDeviation(new[] { 255f }), VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Creates a PP-OCRv4 seal probability-map profile. / 创建 PP-OCRv4 印章概率图 Profile。</summary>
        public static PaddleDocumentProfile CreateSealDetection(PaddleDocumentModelDescriptor descriptor, VisualSize? modelSize = null, string inputName = "x", string outputName = "fetch_name_0", int maximumBatch = 1, float threshold = .3f, int minimumArea = 16, VisualPreprocessingOptions? preprocessing = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.SealTextDetection) throw new ArgumentException("The descriptor must identify seal detection.", nameof(descriptor));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            VisualSize size = modelSize ?? new VisualSize(640, 640);
            var decoder = new PaddleDocumentSealDecoder(descriptor, outputName, threshold, minimumArea);
            var visual = new VisualModelProfile("paddle-document." + descriptor.ModelId + ".seal", new ModelId(descriptor.ModelId), VisualTaskId.SealTextDetection, "paddle-document-contract-v1", descriptor.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(-1, 3, -1, -1), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(-1, 1, -1, -1)) }, Array.Empty<VisualLabel>(), decoder,
                preprocessing: preprocessing ?? new VisualPreprocessingOptions(size, VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.ImageNet, VisualTensorLayout.Nchw, 1));
            return new PaddleDocumentProfile(descriptor, visual);
        }

        /// <summary>Admits an explicitly implemented formula, unwarping, table-structure, or chart profile without pretending that a generic decoder understands its sequence or geometry output. / 接纳已显式实现的公式、矫正、表格结构或图表 Profile，不假装通用解码器能够理解其序列或几何输出。</summary>
        public static PaddleDocumentProfile CreateCustom(PaddleDocumentModelDescriptor descriptor, VisualModelProfile visualProfile)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (visualProfile == null) throw new ArgumentNullException(nameof(visualProfile));
            return new PaddleDocumentProfile(descriptor, visualProfile);
        }

        /// <summary>Creates the standard four-way PP-LCNet document orientation labels. / 创建标准四分类 PP-LCNet 文档方向标签。</summary>
        public static IReadOnlyList<string> DocumentOrientationLabels { get; } = new[] { "0_degree", "90_degree", "180_degree", "270_degree" };

        /// <summary>Returns the official 17-class order used by PP-DocLayout/PicoDet/RT-DETR 17-class exports. / 返回官方 17 类模型使用的顺序。</summary>
        public static IReadOnlyList<string> Layout17Labels { get; } = new[] { "paragraph_title", "image", "text", "number", "abstract", "content", "figure_title", "formula", "table", "table_title", "reference", "doc_title", "footnote", "header", "algorithm", "footer", "seal" };
        /// <summary>Returns the official three-class layout order. / 返回官方三类版面顺序。</summary>
        public static IReadOnlyList<string> Layout3Labels { get; } = new[] { "image", "table", "seal" };
        /// <summary>Returns the five-class PicoDet layout order. / 返回 PicoDet 五类版面顺序。</summary>
        public static IReadOnlyList<string> Layout5Labels { get; } = new[] { "Text", "Title", "List", "Table", "Figure" };
        /// <summary>Returns the table-only PicoDet label order. / 返回仅表格 PicoDet 标签顺序。</summary>
        public static IReadOnlyList<string> TableOnlyLabels { get; } = new[] { "Table" };
        /// <summary>Returns the PP-DocBlockLayout region label order. / 返回 PP-DocBlockLayout 区域标签顺序。</summary>
        public static IReadOnlyList<string> RegionLabels { get; } = new[] { "Region" };
        /// <summary>Returns the current 23-class PP-DocLayout-L order. / 返回当前 PP-DocLayout-L 的 23 类顺序。</summary>
        public static IReadOnlyList<string> Layout23Labels { get; } = new[] { "paragraph_title", "image", "text", "number", "abstract", "content", "figure_title", "formula", "table", "table_title", "reference", "doc_title", "footnote", "header", "algorithm", "footer", "seal", "chart_title", "chart", "formula_number", "header_image", "footer_image", "aside_text" };
        /// <summary>Returns the PP-DocLayout-plus label order. / 返回 PP-DocLayout-plus 标签顺序。</summary>
        public static IReadOnlyList<string> LayoutPlusLabels { get; } = new[] { "paragraph_title", "image", "text", "number", "abstract", "content", "figure_title", "formula", "table", "reference", "doc_title", "footnote", "header", "algorithm", "footer", "seal", "chart", "formula_number", "aside_text", "reference_content" };

        private static IReadOnlyList<VisualLabel> labelsToVisual(IEnumerable<string> labels)
        {
            var values = new List<VisualLabel>();
            int index = 0;
            foreach (string label in labels) values.Add(new VisualLabel(index++, label));
            return values;
        }
    }
}

#pragma warning restore CS1591
