using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Yolo
{
    /// <summary>Configures artifact-specific details while retaining a family export contract. / 配置工件特定细节，同时保留模型族导出合同。</summary>
    public sealed class YoloDetectionProfileOptions
    {
        /// <summary>Initializes YOLO profile options. / 初始化 YOLO Profile 选项。</summary>
        public YoloDetectionProfileOptions(
            int opset,
            VisualSize? modelSize = null,
            string inputName = "images",
            string? outputName = null,
            int stride = 32,
            byte paddingValue = 114,
            bool scaleUp = true,
            bool dynamicShapes = false,
            YoloScoreActivation scoreActivation = YoloScoreActivation.Identity,
            YoloDetectionDecoderOptions? decoderOptions = null,
            string? profileId = null,
            string preprocessingVersion = "ultralytics-letterbox-rgb-nchw-v1",
            string postprocessingVersion = "deploysharp-yolo-detection-v1",
            string modelFormat = "onnx")
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A YOLO model format is required.", nameof(modelFormat));
            if (string.IsNullOrWhiteSpace(inputName)) throw new ArgumentException("A YOLO input name is required.", nameof(inputName));
            if (outputName != null && string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("A YOLO output name cannot be empty.", nameof(outputName));
            if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
            if (!Enum.IsDefined(typeof(YoloScoreActivation), scoreActivation)) throw new ArgumentOutOfRangeException(nameof(scoreActivation));
            if (string.IsNullOrWhiteSpace(preprocessingVersion)) throw new ArgumentException("A preprocessing contract version is required.", nameof(preprocessingVersion));
            if (string.IsNullOrWhiteSpace(postprocessingVersion)) throw new ArgumentException("A postprocessing contract version is required.", nameof(postprocessingVersion));
            Opset = opset;
            ModelFormat = modelFormat.Trim();
            ModelSize = modelSize ?? new VisualSize(640, 640);
            InputName = inputName;
            OutputName = outputName;
            Stride = stride;
            PaddingValue = paddingValue;
            ScaleUp = scaleUp;
            DynamicShapes = dynamicShapes;
            ScoreActivation = scoreActivation;
            DecoderOptions = decoderOptions ?? new YoloDetectionDecoderOptions();
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? null : profileId;
            PreprocessingVersion = preprocessingVersion.Trim();
            PostprocessingVersion = postprocessingVersion.Trim();
        }

        /// <summary>Gets the ONNX opset imported by the exact artifact. / 获取精确工件导入的 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets the exact artifact format selected by the profile. / 获取 Profile 选择的精确工件格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets the model image size. / 获取模型图像尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the input tensor name. / 获取输入张量名称。</summary>
        public string InputName { get; }
        /// <summary>Gets an optional output tensor name override. / 获取可选输出张量名称覆盖。</summary>
        public string? OutputName { get; }
        /// <summary>Gets model stride. / 获取模型步长。</summary>
        public int Stride { get; }
        /// <summary>Gets equal RGB letterbox padding. / 获取相等 RGB Letterbox 填充值。</summary>
        public byte PaddingValue { get; }
        /// <summary>Gets whether smaller sources may be enlarged. / 获取是否允许放大小源图。</summary>
        public bool ScaleUp { get; }
        /// <summary>Gets whether the exported input height or width is dynamic. / 获取导出输入高度或宽度是否为动态维度。</summary>
        public bool DynamicShapes { get; }
        /// <summary>Gets score activation. / 获取分数激活。</summary>
        public YoloScoreActivation ScoreActivation { get; }
        /// <summary>Gets decoder options. / 获取解码选项。</summary>
        public YoloDetectionDecoderOptions DecoderOptions { get; }
        /// <summary>Gets an optional stable profile identifier override. / 获取可选稳定 Profile 标识符覆盖。</summary>
        public string? ProfileId { get; }
        /// <summary>Gets the pinned preprocessing contract version. / 获取锁定的预处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets the pinned postprocessing contract version. / 获取锁定的后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
    }

    /// <summary>Binds one exact YOLO artifact and upstream provenance to a Visual profile. / 将一个精确 YOLO 工件及其上游来源绑定到 Visual Profile。</summary>
    public sealed class YoloDetectionProfile
    {
        internal YoloDetectionProfile(
            YoloDetectionFamily family,
            string upstreamRepository,
            string upstreamCommit,
            string exporterVersion,
            string artifactSha256,
            int opset,
            bool dynamicShapes,
            string preprocessingVersion,
            string postprocessingVersion,
            YoloPreprocessingContract preprocessing,
            YoloDetectionOutputContract output,
            VisualModelProfile visualProfile)
        {
            Family = family;
            UpstreamRepository = upstreamRepository;
            UpstreamCommit = upstreamCommit;
            ExporterVersion = exporterVersion;
            ArtifactSha256 = artifactSha256;
            Opset = opset;
            DynamicShapes = dynamicShapes;
            PreprocessingVersion = preprocessingVersion;
            PostprocessingVersion = postprocessingVersion;
            Preprocessing = preprocessing;
            Output = output;
            VisualProfile = visualProfile;
        }

        /// <summary>Gets YOLO family. / 获取 YOLO 模型族。</summary>
        public YoloDetectionFamily Family { get; }
        /// <summary>Gets the authoritative upstream repository URL. / 获取权威上游仓库 URL。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets the pinned upstream commit or release identifier. / 获取锁定的上游提交或 Release 标识。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets the exporter version recorded for the artifact. / 获取工件记录的导出器版本。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets the lowercase artifact SHA256. / 获取小写工件 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets the exact ONNX opset. / 获取精确 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets whether the exported input uses dynamic spatial dimensions. / 获取导出输入是否使用动态空间维度。</summary>
        public bool DynamicShapes { get; }
        /// <summary>Gets the preprocessing contract version. / 获取预处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets the postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets backend-neutral preprocessing semantics. / 获取后端无关预处理语义。</summary>
        public YoloPreprocessingContract Preprocessing { get; }
        /// <summary>Gets exact output tensor semantics. / 获取精确输出张量语义。</summary>
        public YoloDetectionOutputContract Output { get; }
        /// <summary>Gets the backend-neutral Visual model profile. / 获取后端无关 Visual 模型 Profile。</summary>
        public VisualModelProfile VisualProfile { get; }

        /// <summary>Creates a Core artifact whose format and hash are bound to this profile. / 创建格式与 SHA 均绑定到此 Profile 的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
        {
            return new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, ArtifactSha256, preferredBackend);
        }
    }

    /// <summary>Creates exact V1-family YOLO detection profiles without duplicating weight-size classes. / 创建精确的 V1 模型族 YOLO 检测 Profile，避免复制权重尺寸类型。</summary>
    public static class YoloDetectionProfiles
    {
        /// <summary>Creates an artifact-bound YOLO detection profile. / 创建绑定工件的 YOLO 检测 Profile。</summary>
        public static YoloDetectionProfile Create(
            YoloDetectionFamily family,
            ModelId modelId,
            string artifactSha256,
            IEnumerable<string> labels,
            string upstreamCommit,
            string exporterVersion,
            YoloDetectionProfileOptions options)
        {
            if (!Enum.IsDefined(typeof(YoloDetectionFamily), family)) throw Invalid("The YOLO family is invalid.");
            if (modelId.IsEmpty) throw Invalid("A YOLO model identifier is required.");
            string hash = ValidateSha256(artifactSha256);
            string commit = Required(upstreamCommit, "An upstream commit or release is required.");
            string exporter = Required(exporterVersion, "An exporter version is required.");
            YoloDetectionProfileOptions effective = options ?? throw Invalid("Artifact-specific YOLO profile options are required.");
            List<VisualLabel> visualLabels = CopyLabels(labels);
            string outputName = effective.OutputName ?? DefaultOutputName(family);
            YoloDetectionOutputKind outputKind = DefaultOutputKind(family);
            var output = new YoloDetectionOutputContract(outputName, outputKind, visualLabels.Count, effective.ScoreActivation);
            TensorShape outputShape = Shape(output);
            var decoder = new YoloDetectionDecoder(output, effective.DecoderOptions);
            string profileId = effective.ProfileId ?? "yolo.detect." + FamilyId(family) + "." + OutputKindId(outputKind) + "." + effective.ModelFormat + "." + modelId.Value;
            var visual = new VisualModelProfile(
                profileId,
                modelId,
                VisualTaskId.ObjectDetection,
                "2.0.0",
                effective.ModelFormat,
                new VisualInputBinding(
                    effective.InputName,
                    TensorElementType.Float32,
                    effective.DynamicShapes ? new TensorShape(1, 3, -1, -1) : new TensorShape(1, 3, effective.ModelSize.Height, effective.ModelSize.Width),
                    VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, outputShape) },
                visualLabels,
                decoder);
            return new YoloDetectionProfile(
                family,
                UpstreamRepository(family),
                commit,
                exporter,
                hash,
                effective.Opset,
                effective.DynamicShapes,
                effective.PreprocessingVersion,
                effective.PostprocessingVersion,
                new YoloPreprocessingContract(effective.ModelSize, effective.Stride, effective.PaddingValue, effective.ScaleUp),
                output,
                visual);
        }

        private static List<VisualLabel> CopyLabels(IEnumerable<string> labels)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            var result = new List<VisualLabel>();
            foreach (string label in labels)
            {
                if (string.IsNullOrWhiteSpace(label)) throw Invalid("YOLO labels cannot contain empty values.");
                result.Add(new VisualLabel(result.Count, label));
            }
            if (result.Count == 0) throw Invalid("At least one YOLO class label is required.");
            return result;
        }

        private static TensorShape Shape(YoloDetectionOutputContract contract)
        {
            if (contract.Kind == YoloDetectionOutputKind.RawCandidateMajor) return new TensorShape(1, -1, contract.FieldCount);
            if (contract.Kind == YoloDetectionOutputKind.RawAttributeMajor) return new TensorShape(1, contract.FieldCount, -1);
            if (contract.Kind == YoloDetectionOutputKind.BatchedEndToEnd) return new TensorShape(-1, contract.FieldCount);
            return new TensorShape(1, -1, contract.FieldCount);
        }

        private static YoloDetectionOutputKind DefaultOutputKind(YoloDetectionFamily family)
        {
            if (family == YoloDetectionFamily.YoloV5 || family == YoloDetectionFamily.YoloV6) return YoloDetectionOutputKind.RawCandidateMajor;
            if (family == YoloDetectionFamily.YoloV7) return YoloDetectionOutputKind.BatchedEndToEnd;
            if (family == YoloDetectionFamily.YoloV10 || family == YoloDetectionFamily.YoloV26) return YoloDetectionOutputKind.EndToEnd;
            return YoloDetectionOutputKind.RawAttributeMajor;
        }

        private static string DefaultOutputName(YoloDetectionFamily family)
        {
            if (family == YoloDetectionFamily.YoloV6) return "outputs";
            if (family == YoloDetectionFamily.YoloV7) return "output";
            return "output0";
        }

        private static string UpstreamRepository(YoloDetectionFamily family)
        {
            if (family == YoloDetectionFamily.YoloV5) return "https://github.com/ultralytics/yolov5";
            if (family == YoloDetectionFamily.YoloV6) return "https://github.com/meituan/YOLOv6";
            if (family == YoloDetectionFamily.YoloV7) return "https://github.com/WongKinYiu/yolov7";
            if (family == YoloDetectionFamily.YoloV9) return "https://github.com/WongKinYiu/yolov9";
            if (family == YoloDetectionFamily.YoloV10) return "https://github.com/THU-MIG/yolov10";
            if (family == YoloDetectionFamily.YoloV12) return "https://github.com/sunsmarterjie/yolov12";
            if (family == YoloDetectionFamily.YoloV13) return "https://github.com/iMoonLab/YOLOv13";
            return "https://github.com/ultralytics/ultralytics";
        }

        private static string FamilyId(YoloDetectionFamily family) => "v" + ((int)family).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string OutputKindId(YoloDetectionOutputKind kind)
        {
            if (kind == YoloDetectionOutputKind.RawCandidateMajor) return "raw-candidate-major";
            if (kind == YoloDetectionOutputKind.RawAttributeMajor) return "raw-attribute-major";
            if (kind == YoloDetectionOutputKind.BatchedEndToEnd) return "batched-end-to-end";
            return "end-to-end";
        }

        private static string Required(string value, string message)
        {
            if (string.IsNullOrWhiteSpace(value)) throw Invalid(message);
            return value.Trim();
        }

        private static string ValidateSha256(string value)
        {
            string hash = Required(value, "A YOLO artifact SHA256 is required.").ToLowerInvariant();
            if (hash.Length != 64) throw Invalid("A YOLO artifact SHA256 must contain 64 hexadecimal characters.");
            for (int index = 0; index < hash.Length; index++)
            {
                char current = hash[index];
                if (!((current >= '0' && current <= '9') || (current >= 'a' && current <= 'f'))) throw Invalid("A YOLO artifact SHA256 contains a non-hexadecimal character.");
            }
            return hash;
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.YoloContractInvalid, message);
    }

    /// <summary>Provides the canonical COCO 80-class label order used by common YOLO detection checkpoints. / 提供常见 YOLO 检测权重使用的规范 COCO 80 类标签顺序。</summary>
    public static class YoloLabelSets
    {
        private static readonly IReadOnlyList<string> Coco = Array.AsReadOnly(new[]
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
            "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
            "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
            "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
            "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
            "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
            "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
            "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        });

        /// <summary>Gets an immutable COCO 80-class label sequence. / 获取不可变 COCO 80 类标签序列。</summary>
        public static IReadOnlyList<string> Coco80 => Coco;

        private static readonly IReadOnlyList<string> Dota = Array.AsReadOnly(new[]
        {
            "plane", "ship", "storage tank", "baseball diamond", "tennis court", "basketball court", "ground track field", "harbor", "bridge", "large vehicle", "small vehicle", "helicopter", "roundabout", "soccer ball field", "swimming pool"
        });

        /// <summary>Gets the immutable DOTA-v1 15-class label order used by YOLO OBB exports. / 获取 YOLO OBB 导出使用的不可变 DOTA-v1 15 类标签顺序。</summary>
        public static IReadOnlyList<string> Dota15 => Dota;
    }
}
