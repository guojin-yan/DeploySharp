using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Yolo
{
    /// <summary>Identifies an explicitly declared YOLO packed-output layout. / 标识显式声明的 YOLO 打包输出布局。</summary>
    public enum YoloPackedTensorLayout
    {
        /// <summary>Fields are contiguous for each candidate as [1,N,F]. / 字段按候选连续排列为 [1,N,F]。</summary>
        CandidateMajor = 0,
        /// <summary>Candidates are contiguous for each field as [1,F,N]. / 候选按字段连续排列为 [1,F,N]。</summary>
        AttributeMajor = 1,
        /// <summary>Exporter-selected rows use [1,N,F] and already include score, class, and top-k selection. / 导出器筛选后的行使用 [1,N,F]，且已经包含分数、类别和 Top-K 选择。</summary>
        EndToEnd = 2
    }

    /// <summary>Identifies the image geometry required by an exact YOLO artifact. / 标识精确 YOLO 工件要求的图像几何处理。</summary>
    public enum YoloImageResizeMode
    {
        /// <summary>Preserve aspect ratio and add centered equal-color padding. / 保持宽高比并添加居中的等色填充。</summary>
        Letterbox = 0,
        /// <summary>Resize the shortest edge and take the centered target crop. / 缩放最短边并截取居中的目标区域。</summary>
        CenterCrop = 1
    }

    /// <summary>Defines backend-neutral preprocessing for one exact YOLO task artifact. / 定义一个精确 YOLO 任务工件的后端无关预处理。</summary>
    public sealed class YoloImagePreprocessingContract
    {
        /// <summary>Initializes immutable YOLO image preprocessing. / 初始化不可变的 YOLO 图像预处理。</summary>
        public YoloImagePreprocessingContract(VisualSize modelSize, YoloImageResizeMode resizeMode, int stride = 32, byte paddingValue = 114)
        {
            if (!Enum.IsDefined(typeof(YoloImageResizeMode), resizeMode)) throw new ArgumentOutOfRangeException(nameof(resizeMode));
            if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
            ModelSize = modelSize;
            ResizeMode = resizeMode;
            Stride = stride;
            PaddingValue = paddingValue;
        }

        /// <summary>Gets the model image size. / 获取模型图像尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the exact resize/crop mode. / 获取精确的缩放或裁剪模式。</summary>
        public YoloImageResizeMode ResizeMode { get; }
        /// <summary>Gets the exporter stride used by letterbox validation. / 获取 Letterbox 校验所用的导出器步长。</summary>
        public int Stride { get; }
        /// <summary>Gets the equal RGB padding value. / 获取相等的 RGB 填充值。</summary>
        public byte PaddingValue { get; }
        /// <summary>Gets the required RGB color order. / 获取要求的 RGB 颜色顺序。</summary>
        public VisualColorOrder ColorOrder => VisualColorOrder.Rgb;
        /// <summary>Gets the required NCHW tensor layout. / 获取要求的 NCHW 张量布局。</summary>
        public VisualTensorLayout Layout => VisualTensorLayout.Nchw;
        /// <summary>Gets the required pixel divisor. / 获取要求的像素除数。</summary>
        public float PixelDivisor => 255f;
    }

    /// <summary>Configures an exact packed YOLO export profile. / 配置精确的打包 YOLO 导出 Profile。</summary>
    public sealed class YoloPackedProfileOptions
    {
        /// <summary>Initializes packed profile options from inspected ONNX metadata. / 使用已检查的 ONNX 元数据初始化打包 Profile 选项。</summary>
        public YoloPackedProfileOptions(
            int opset,
            int candidateCount,
            VisualSize? modelSize = null,
            string inputName = "images",
            string outputName = "output0",
            string prototypeOutputName = "output1",
            int stride = 32,
            string modelFormat = "onnx",
            string? profileId = null,
            string preprocessingVersion = "ultralytics-letterbox-rgb-nchw-v1",
            string postprocessingVersion = "deploysharp-yolo-multitask-v1",
            YoloPackedDecoderOptions? decoderOptions = null)
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (candidateCount <= 0) throw new ArgumentOutOfRangeException(nameof(candidateCount));
            Opset = opset;
            CandidateCount = candidateCount;
            ModelSize = modelSize ?? new VisualSize(640, 640);
            InputName = Required(inputName, nameof(inputName));
            OutputName = Required(outputName, nameof(outputName));
            PrototypeOutputName = Required(prototypeOutputName, nameof(prototypeOutputName));
            if (string.Equals(OutputName, PrototypeOutputName, StringComparison.Ordinal)) throw new ArgumentException("YOLO output names must be unique.");
            if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
            Stride = stride;
            ModelFormat = Required(modelFormat, nameof(modelFormat));
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId;
            PreprocessingVersion = Required(preprocessingVersion, nameof(preprocessingVersion));
            PostprocessingVersion = Required(postprocessingVersion, nameof(postprocessingVersion));
            DecoderOptions = decoderOptions ?? new YoloPackedDecoderOptions();
            if (CandidateCount > DecoderOptions.MaximumCandidates) throw new ArgumentException("The declared candidate count exceeds the decoder bound.", nameof(decoderOptions));
        }

        /// <summary>Gets the exact ONNX opset. / 获取精确的 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets the exact exported candidate count. / 获取精确的导出候选数。</summary>
        public int CandidateCount { get; }
        /// <summary>Gets the model image size. / 获取模型图像尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the input tensor name. / 获取输入张量名称。</summary>
        public string InputName { get; }
        /// <summary>Gets the packed primary output name. / 获取打包主输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the prototype output name used by segmentation. / 获取分割任务使用的原型输出名称。</summary>
        public string PrototypeOutputName { get; }
        /// <summary>Gets model stride. / 获取模型步长。</summary>
        public int Stride { get; }
        /// <summary>Gets the model artifact format. / 获取模型工件格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets an optional stable profile identifier. / 获取可选的稳定 Profile 标识符。</summary>
        public string? ProfileId { get; }
        /// <summary>Gets the preprocessing contract version. / 获取预处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets the postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets bounded packed-decoder options. / 获取有界的打包解码选项。</summary>
        public YoloPackedDecoderOptions DecoderOptions { get; }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>Configures the exact YOLO classification export. / 配置精确的 YOLO 分类导出。</summary>
    public sealed class YoloClassificationProfileOptions
    {
        /// <summary>Initializes YOLO classification profile options. / 初始化 YOLO 分类 Profile 选项。</summary>
        public YoloClassificationProfileOptions(int opset, VisualSize? modelSize = null, string inputName = "images", string outputName = "output0", int topK = 5, string modelFormat = "onnx", string? profileId = null)
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (string.IsNullOrWhiteSpace(inputName)) throw new ArgumentException("An input name is required.", nameof(inputName));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            Opset = opset;
            ModelSize = modelSize ?? new VisualSize(224, 224);
            InputName = inputName.Trim();
            OutputName = outputName.Trim();
            TopK = topK;
            ModelFormat = modelFormat.Trim();
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId;
        }

        /// <summary>Gets the exact ONNX opset. / 获取精确的 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets the classification input size. / 获取分类输入尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the input tensor name. / 获取输入张量名称。</summary>
        public string InputName { get; }
        /// <summary>Gets the probability output name. / 获取概率输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the requested deterministic Top-K count. / 获取要求的确定性 Top-K 数量。</summary>
        public int TopK { get; }
        /// <summary>Gets the model artifact format. / 获取模型工件格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets an optional stable profile identifier. / 获取可选的稳定 Profile 标识符。</summary>
        public string? ProfileId { get; }
    }

    /// <summary>Controls score filtering, NMS, mask, keypoint, and workspace bounds for packed YOLO exports. / 控制打包 YOLO 导出的分数筛选、NMS、掩码、关键点和工作区边界。</summary>
    public sealed class YoloPackedDecoderOptions
    {
        /// <summary>Initializes bounded YOLO packed decoding. / 初始化有界的 YOLO 打包解码。</summary>
        public YoloPackedDecoderOptions(float scoreThreshold = 0.25f, float iouThreshold = 0.45f, DetectionNmsMode nmsMode = DetectionNmsMode.ClassAware, int maximumCandidates = 30000, int maximumDetections = 100, float maskThreshold = 0.5f, float keypointThreshold = 0.5f, long maximumWorkspaceBytes = 256L * 1024 * 1024)
        {
            if (float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold) || scoreThreshold < 0 || scoreThreshold > 1) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (!Enum.IsDefined(typeof(DetectionNmsMode), nmsMode)) throw new ArgumentOutOfRangeException(nameof(nmsMode));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            if (float.IsNaN(maskThreshold) || float.IsInfinity(maskThreshold) || maskThreshold < 0 || maskThreshold > 1) throw new ArgumentOutOfRangeException(nameof(maskThreshold));
            if (float.IsNaN(keypointThreshold) || float.IsInfinity(keypointThreshold) || keypointThreshold < 0 || keypointThreshold > 1) throw new ArgumentOutOfRangeException(nameof(keypointThreshold));
            if (maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkspaceBytes));
            ScoreThreshold = scoreThreshold;
            IouThreshold = iouThreshold;
            NmsMode = nmsMode;
            MaximumCandidates = maximumCandidates;
            MaximumDetections = maximumDetections;
            MaskThreshold = maskThreshold;
            KeypointThreshold = keypointThreshold;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets the strict score threshold. / 获取严格分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the NMS overlap threshold. / 获取 NMS 重叠阈值。</summary>
        public float IouThreshold { get; }
        /// <summary>Gets class-aware or class-agnostic NMS mode. / 获取分类别或忽略类别的 NMS 模式。</summary>
        public DetectionNmsMode NmsMode { get; }
        /// <summary>Gets the maximum input candidate count. / 获取最大输入候选数。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the maximum retained result count. / 获取最大保留结果数。</summary>
        public int MaximumDetections { get; }
        /// <summary>Gets the binary mask threshold. / 获取二值掩码阈值。</summary>
        public float MaskThreshold { get; }
        /// <summary>Gets the keypoint validity threshold. / 获取关键点有效性阈值。</summary>
        public float KeypointThreshold { get; }
        /// <summary>Gets the maximum temporary workspace. / 获取最大临时工作区。</summary>
        public long MaximumWorkspaceBytes { get; }
    }

    /// <summary>Defines an exact packed segmentation output and prototype contract. / 定义精确的打包分割输出和原型合同。</summary>
    public sealed class YoloInstanceSegmentationOutputContract
    {
        internal YoloInstanceSegmentationOutputContract(string outputName, string prototypeOutputName, YoloPackedTensorLayout layout, int candidates, int classes, int coefficients, bool objectness)
        {
            OutputName = outputName; PrototypeOutputName = prototypeOutputName; Layout = layout; CandidateCount = candidates; ClassCount = classes; MaskCoefficientCount = coefficients; HasObjectness = objectness;
        }
        /// <summary>Gets the packed output name. / 获取打包输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the prototype output name. / 获取原型输出名称。</summary>
        public string PrototypeOutputName { get; }
        /// <summary>Gets the exact packed layout. / 获取精确的打包布局。</summary>
        public YoloPackedTensorLayout Layout { get; }
        /// <summary>Gets the exact candidate count. / 获取精确的候选数。</summary>
        public int CandidateCount { get; }
        /// <summary>Gets the class count. / 获取类别数。</summary>
        public int ClassCount { get; }
        /// <summary>Gets the mask coefficient and prototype channel count. / 获取掩码系数和原型通道数。</summary>
        public int MaskCoefficientCount { get; }
        /// <summary>Gets whether an objectness field precedes class fields. / 获取类别字段前是否存在目标置信度字段。</summary>
        public bool HasObjectness { get; }
        /// <summary>Gets whether exporter selection replaces DeploySharp NMS. / 获取导出器筛选是否替代 DeploySharp NMS。</summary>
        public bool IsEndToEnd => Layout == YoloPackedTensorLayout.EndToEnd;
        /// <summary>Gets the exact packed field count. / 获取精确的打包字段数。</summary>
        public int FieldCount => IsEndToEnd ? 6 + MaskCoefficientCount : 4 + (HasObjectness ? 1 : 0) + ClassCount + MaskCoefficientCount;
    }

    /// <summary>Defines an exact packed YOLO Pose output contract. / 定义精确的打包 YOLO Pose 输出合同。</summary>
    public sealed class YoloPoseOutputContract
    {
        internal YoloPoseOutputContract(string outputName, YoloPackedTensorLayout layout, int candidates, int classes, int keypoints, int components)
        { OutputName = outputName; Layout = layout; CandidateCount = candidates; ClassCount = classes; KeypointCount = keypoints; ComponentsPerKeypoint = components; }
        /// <summary>Gets the packed output name. / 获取打包输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the exact packed layout. / 获取精确的打包布局。</summary>
        public YoloPackedTensorLayout Layout { get; }
        /// <summary>Gets the exact candidate count. / 获取精确的候选数。</summary>
        public int CandidateCount { get; }
        /// <summary>Gets the class count. / 获取类别数。</summary>
        public int ClassCount { get; }
        /// <summary>Gets the keypoint count. / 获取关键点数量。</summary>
        public int KeypointCount { get; }
        /// <summary>Gets components per keypoint. / 获取每个关键点的字段数。</summary>
        public int ComponentsPerKeypoint { get; }
        /// <summary>Gets whether exporter selection replaces DeploySharp NMS. / 获取导出器筛选是否替代 DeploySharp NMS。</summary>
        public bool IsEndToEnd => Layout == YoloPackedTensorLayout.EndToEnd;
        /// <summary>Gets the exact packed field count. / 获取精确的打包字段数。</summary>
        public int FieldCount => IsEndToEnd ? 6 + (KeypointCount * ComponentsPerKeypoint) : 4 + ClassCount + (KeypointCount * ComponentsPerKeypoint);
    }

    /// <summary>Defines an exact packed YOLO oriented-box output contract. / 定义精确的打包 YOLO 旋转框输出合同。</summary>
    public sealed class YoloObbOutputContract
    {
        internal YoloObbOutputContract(string outputName, YoloPackedTensorLayout layout, int candidates, int classes)
        { OutputName = outputName; Layout = layout; CandidateCount = candidates; ClassCount = classes; }
        /// <summary>Gets the packed output name. / 获取打包输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the exact packed layout. / 获取精确的打包布局。</summary>
        public YoloPackedTensorLayout Layout { get; }
        /// <summary>Gets the exact candidate count. / 获取精确的候选数。</summary>
        public int CandidateCount { get; }
        /// <summary>Gets the class count. / 获取类别数。</summary>
        public int ClassCount { get; }
        /// <summary>Gets whether exporter selection replaces DeploySharp rotated NMS. / 获取导出器筛选是否替代 DeploySharp 旋转 NMS。</summary>
        public bool IsEndToEnd => Layout == YoloPackedTensorLayout.EndToEnd;
        /// <summary>Gets the exact packed field count. / 获取精确的打包字段数。</summary>
        public int FieldCount => IsEndToEnd ? 7 : 5 + ClassCount;
    }

    /// <summary>Binds one exact multi-task YOLO artifact, provenance, preprocessing, and Visual profile. / 绑定一个精确的多任务 YOLO 工件、来源、预处理和 Visual Profile。</summary>
    public sealed class YoloMultiTaskProfile
    {
        internal YoloMultiTaskProfile(YoloDetectionFamily family, string repository, string commit, string exporter, string hash, int opset, string preprocessingVersion, string postprocessingVersion, YoloImagePreprocessingContract preprocessing, VisualModelProfile visualProfile)
        { Family = family; UpstreamRepository = repository; UpstreamCommit = commit; ExporterVersion = exporter; ArtifactSha256 = hash; Opset = opset; PreprocessingVersion = preprocessingVersion; PostprocessingVersion = postprocessingVersion; Preprocessing = preprocessing; VisualProfile = visualProfile; }
        /// <summary>Gets the YOLO version family. / 获取 YOLO 版本模型族。</summary>
        public YoloDetectionFamily Family { get; }
        /// <summary>Gets the authoritative upstream repository. / 获取权威上游仓库。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets the pinned upstream commit or release. / 获取锁定的上游提交或发行版。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets the exact exporter version. / 获取精确的导出器版本。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets the lowercase artifact SHA256. / 获取小写工件 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets the exact ONNX opset. / 获取精确的 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets the preprocessing contract version. / 获取预处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets the postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets exact image preprocessing. / 获取精确的图像预处理。</summary>
        public YoloImagePreprocessingContract Preprocessing { get; }
        /// <summary>Gets the backend-neutral Visual profile. / 获取后端无关的 Visual Profile。</summary>
        public VisualModelProfile VisualProfile { get; }
        /// <summary>Creates a Core artifact bound to this profile's hash and format. / 创建绑定到此 Profile 哈希和格式的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null) => new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, ArtifactSha256, preferredBackend);
    }

    /// <summary>Creates artifact-bound classification, segmentation, Pose, and OBB profiles for the V1 YOLO migration matrix. / 为 V1 YOLO 迁移矩阵创建绑定工件的分类、分割、Pose 和 OBB Profile。</summary>
    public static class YoloMultiTaskProfiles
    {
        /// <summary>Creates the V1 YOLOCls profile bound to an exact YOLOv8 classification export. / 创建绑定到精确 YOLOv8 分类导出的 V1 YOLOCls Profile。</summary>
        public static YoloMultiTaskProfile CreateClassification(ModelId modelId, string artifactSha256, IEnumerable<string> labels, string upstreamCommit, string exporterVersion, YoloClassificationProfileOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            List<VisualLabel> visualLabels = Labels(labels);
            if (options.TopK > visualLabels.Count) throw new ArgumentException("Top-K exceeds the class count.", nameof(options));
            var decoder = new ClassificationDecoder(options.OutputName, ClassificationScoreMode.Probabilities, options.TopK);
            var profile = new VisualModelProfile(
                options.ProfileId ?? "yolo.classify.v8." + options.ModelFormat + "." + modelId.Value,
                modelId, VisualTaskId.ImageClassification, "2.0.0", options.ModelFormat,
                new VisualInputBinding(options.InputName, TensorElementType.Float32, new TensorShape(1, 3, options.ModelSize.Height, options.ModelSize.Width), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(options.OutputName, TensorElementType.Float32, new TensorShape(1, visualLabels.Count)) }, visualLabels, decoder);
            return Build(YoloDetectionFamily.YoloV8, modelId, artifactSha256, upstreamCommit, exporterVersion, options.Opset, "ultralytics-classify-center-crop-rgb-nchw-v1", "ultralytics-exported-probabilities-v1", new YoloImagePreprocessingContract(options.ModelSize, YoloImageResizeMode.CenterCrop, 1, 0), profile);
        }

        /// <summary>Creates one exact YOLO instance-segmentation profile. / 创建一个精确的 YOLO 实例分割 Profile。</summary>
        public static YoloMultiTaskProfile CreateInstanceSegmentation(YoloDetectionFamily family, ModelId modelId, string artifactSha256, IEnumerable<string> labels, string upstreamCommit, string exporterVersion, YoloPackedProfileOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            EnsureFamily(family, YoloDetectionFamily.YoloV5, YoloDetectionFamily.YoloV8, YoloDetectionFamily.YoloV9, YoloDetectionFamily.YoloV11, YoloDetectionFamily.YoloV26);
            List<VisualLabel> visualLabels = Labels(labels);
            YoloPackedTensorLayout layout = Layout(family);
            bool objectness = family == YoloDetectionFamily.YoloV5;
            var contract = new YoloInstanceSegmentationOutputContract(options.OutputName, options.PrototypeOutputName, layout, options.CandidateCount, visualLabels.Count, 32, objectness);
            var decoder = new YoloInstanceSegmentationDecoder(contract, options.DecoderOptions);
            var outputs = new[]
            {
                new VisualOutputBinding(options.OutputName, TensorElementType.Float32, PackedShape(layout, options.CandidateCount, contract.FieldCount)),
                new VisualOutputBinding(options.PrototypeOutputName, TensorElementType.Float32, new TensorShape(1, contract.MaskCoefficientCount, options.ModelSize.Height / 4, options.ModelSize.Width / 4))
            };
            VisualModelProfile profile = VisualProfile(options.ProfileId, "segment", family, modelId, options, VisualTaskId.InstanceSegmentation, visualLabels, outputs, decoder);
            return Build(family, modelId, artifactSha256, upstreamCommit, exporterVersion, options.Opset, options.PreprocessingVersion, options.PostprocessingVersion, new YoloImagePreprocessingContract(options.ModelSize, YoloImageResizeMode.Letterbox, options.Stride), profile);
        }

        /// <summary>Creates one exact YOLO COCO-17 Pose profile. / 创建一个精确的 YOLO COCO-17 Pose Profile。</summary>
        public static YoloMultiTaskProfile CreatePose(YoloDetectionFamily family, ModelId modelId, string artifactSha256, string upstreamCommit, string exporterVersion, YoloPackedProfileOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            EnsureFamily(family, YoloDetectionFamily.YoloV8, YoloDetectionFamily.YoloV11, YoloDetectionFamily.YoloV26);
            YoloPackedTensorLayout layout = Layout(family);
            var contract = new YoloPoseOutputContract(options.OutputName, layout, options.CandidateCount, 1, 17, 3);
            var labels = new List<VisualLabel> { new VisualLabel(0, "person") };
            var decoder = new YoloPoseDecoder(contract, YoloPoseTopologies.Coco17, options.DecoderOptions);
            VisualModelProfile profile = VisualProfile(options.ProfileId, "pose", family, modelId, options, VisualTaskId.PoseEstimation, labels, new[] { new VisualOutputBinding(options.OutputName, TensorElementType.Float32, PackedShape(layout, options.CandidateCount, contract.FieldCount)) }, decoder);
            return Build(family, modelId, artifactSha256, upstreamCommit, exporterVersion, options.Opset, options.PreprocessingVersion, options.PostprocessingVersion, new YoloImagePreprocessingContract(options.ModelSize, YoloImageResizeMode.Letterbox, options.Stride), profile);
        }

        /// <summary>Creates one exact YOLO DOTA-15 OBB profile. / 创建一个精确的 YOLO DOTA-15 OBB Profile。</summary>
        public static YoloMultiTaskProfile CreateObb(YoloDetectionFamily family, ModelId modelId, string artifactSha256, IEnumerable<string> labels, string upstreamCommit, string exporterVersion, YoloPackedProfileOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            EnsureFamily(family, YoloDetectionFamily.YoloV8, YoloDetectionFamily.YoloV11, YoloDetectionFamily.YoloV26);
            List<VisualLabel> visualLabels = Labels(labels);
            YoloPackedTensorLayout layout = Layout(family);
            var contract = new YoloObbOutputContract(options.OutputName, layout, options.CandidateCount, visualLabels.Count);
            var decoder = new YoloObbDecoder(contract, options.DecoderOptions);
            VisualModelProfile profile = VisualProfile(options.ProfileId, "obb", family, modelId, options, VisualTaskId.OrientedObjectDetection, visualLabels, new[] { new VisualOutputBinding(options.OutputName, TensorElementType.Float32, PackedShape(layout, options.CandidateCount, contract.FieldCount)) }, decoder);
            return Build(family, modelId, artifactSha256, upstreamCommit, exporterVersion, options.Opset, options.PreprocessingVersion, options.PostprocessingVersion, new YoloImagePreprocessingContract(options.ModelSize, YoloImageResizeMode.Letterbox, options.Stride), profile);
        }

        private static VisualModelProfile VisualProfile(string? profileId, string task, YoloDetectionFamily family, ModelId modelId, YoloPackedProfileOptions options, VisualTaskId taskId, List<VisualLabel> labels, IEnumerable<VisualOutputBinding> outputs, IVisualDecoder decoder)
            => new VisualModelProfile(profileId ?? "yolo." + task + ".v" + ((int)family).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + options.ModelFormat + "." + modelId.Value, modelId, taskId, "2.0.0", options.ModelFormat, new VisualInputBinding(options.InputName, TensorElementType.Float32, new TensorShape(1, 3, options.ModelSize.Height, options.ModelSize.Width), VisualTensorLayout.Nchw), outputs, labels, decoder);

        private static TensorShape PackedShape(YoloPackedTensorLayout layout, int candidates, int fields) => layout == YoloPackedTensorLayout.AttributeMajor ? new TensorShape(1, fields, candidates) : new TensorShape(1, candidates, fields);
        private static YoloPackedTensorLayout Layout(YoloDetectionFamily family) => family == YoloDetectionFamily.YoloV5 ? YoloPackedTensorLayout.CandidateMajor : (family == YoloDetectionFamily.YoloV26 ? YoloPackedTensorLayout.EndToEnd : YoloPackedTensorLayout.AttributeMajor);

        private static YoloMultiTaskProfile Build(YoloDetectionFamily family, ModelId modelId, string hash, string commit, string exporter, int opset, string preprocessingVersion, string postprocessingVersion, YoloImagePreprocessingContract preprocessing, VisualModelProfile visual)
        {
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            string normalizedHash = RequiredHex(hash);
            if (string.IsNullOrWhiteSpace(commit)) throw new ArgumentException("An upstream commit or release is required.", nameof(commit));
            if (string.IsNullOrWhiteSpace(exporter)) throw new ArgumentException("An exporter version is required.", nameof(exporter));
            return new YoloMultiTaskProfile(family, Repository(family), commit.Trim(), exporter.Trim(), normalizedHash, opset, preprocessingVersion, postprocessingVersion, preprocessing, visual);
        }

        private static List<VisualLabel> Labels(IEnumerable<string> labels)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            var result = new List<VisualLabel>();
            foreach (string label in labels)
            {
                if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Labels cannot contain empty values.", nameof(labels));
                result.Add(new VisualLabel(result.Count, label.Trim()));
            }
            if (result.Count == 0) throw new ArgumentException("At least one label is required.", nameof(labels));
            return result;
        }

        private static void EnsureFamily(YoloDetectionFamily family, params YoloDetectionFamily[] supported)
        {
            for (int index = 0; index < supported.Length; index++) if (family == supported[index]) return;
            throw new VisualException(VisualErrorCodes.YoloContractInvalid, "The YOLO family does not support the selected task contract.");
        }

        private static string RequiredHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) throw new ArgumentException("An exact 64-character SHA256 is required.", nameof(value));
            string result = value.ToLowerInvariant();
            for (int index = 0; index < result.Length; index++) if (!((result[index] >= '0' && result[index] <= '9') || (result[index] >= 'a' && result[index] <= 'f'))) throw new ArgumentException("SHA256 contains a non-hexadecimal character.", nameof(value));
            return result;
        }

        private static string Repository(YoloDetectionFamily family) => family == YoloDetectionFamily.YoloV5 ? "https://github.com/ultralytics/yolov5" : (family == YoloDetectionFamily.YoloV9 ? "https://github.com/WongKinYiu/yolov9" : "https://github.com/ultralytics/ultralytics");
    }

    /// <summary>Provides official COCO-17 keypoint names, flip pairs, skeleton, and OKS sigmas. / 提供官方 COCO-17 关键点名称、翻转对、骨架和 OKS sigma。</summary>
    public static class YoloPoseTopologies
    {
        private static readonly PoseTopology Coco = CreateCoco17();
        /// <summary>Gets the immutable COCO-17 person topology. / 获取不可变的 COCO-17 人体拓扑。</summary>
        public static PoseTopology Coco17 => Coco;

        private static PoseTopology CreateCoco17()
        {
            string[] names = { "nose", "left_eye", "right_eye", "left_ear", "right_ear", "left_shoulder", "right_shoulder", "left_elbow", "right_elbow", "left_wrist", "right_wrist", "left_hip", "right_hip", "left_knee", "right_knee", "left_ankle", "right_ankle" };
            int?[] mirrors = { null, 2, 1, 4, 3, 6, 5, 8, 7, 10, 9, 12, 11, 14, 13, 16, 15 };
            float[] sigmas = { .026f, .025f, .025f, .035f, .035f, .079f, .079f, .072f, .072f, .062f, .062f, .107f, .107f, .087f, .087f, .089f, .089f };
            var points = new List<PoseKeypointDefinition>(17);
            for (int index = 0; index < names.Length; index++) points.Add(new PoseKeypointDefinition(index, names[index], mirrors[index], oksSigma: sigmas[index]));
            int[,] pairs = { { 15, 13 }, { 13, 11 }, { 16, 14 }, { 14, 12 }, { 11, 12 }, { 5, 11 }, { 6, 12 }, { 5, 6 }, { 5, 7 }, { 6, 8 }, { 7, 9 }, { 8, 10 }, { 1, 2 }, { 0, 1 }, { 0, 2 }, { 1, 3 }, { 2, 4 }, { 3, 5 }, { 4, 6 } };
            var edges = new List<PoseSkeletonEdge>(pairs.GetLength(0));
            for (int index = 0; index < pairs.GetLength(0); index++) edges.Add(new PoseSkeletonEdge(pairs[index, 0], pairs[index, 1]));
            return new PoseTopology(points, edges);
        }
    }
}
