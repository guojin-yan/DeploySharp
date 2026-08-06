using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual.Models.Yolo
{
    /// <summary>Identifies a YOLO detection family without representing a weight size. / 标识 YOLO 检测模型族，不表示权重尺寸。</summary>
    public enum YoloDetectionFamily
    {
        /// <summary>YOLOv5 detection. / YOLOv5 检测。</summary>
        YoloV5 = 5,
        /// <summary>YOLOv6 detection. / YOLOv6 检测。</summary>
        YoloV6 = 6,
        /// <summary>YOLOv7 detection. / YOLOv7 检测。</summary>
        YoloV7 = 7,
        /// <summary>YOLOv8 detection. / YOLOv8 检测。</summary>
        YoloV8 = 8,
        /// <summary>YOLOv9 detection. / YOLOv9 检测。</summary>
        YoloV9 = 9,
        /// <summary>YOLOv10 detection. / YOLOv10 检测。</summary>
        YoloV10 = 10,
        /// <summary>YOLO11 detection. / YOLO11 检测。</summary>
        YoloV11 = 11,
        /// <summary>YOLO12 detection. / YOLO12 检测。</summary>
        YoloV12 = 12,
        /// <summary>YOLO13 detection. / YOLO13 检测。</summary>
        YoloV13 = 13,
        /// <summary>YOLO26 detection. / YOLO26 检测。</summary>
        YoloV26 = 26
    }

    /// <summary>Identifies the physical tensor contract of a YOLO detection export. / 标识 YOLO 检测导出的物理张量合同。</summary>
    public enum YoloDetectionOutputKind
    {
        /// <summary>Raw candidate-major output shaped as batch, candidates, fields. / 原始候选优先输出，形状为批次、候选、字段。</summary>
        RawCandidateMajor = 0,
        /// <summary>Raw attribute-major output shaped as batch, fields, candidates. / 原始属性优先输出，形状为批次、字段、候选。</summary>
        RawAttributeMajor = 1,
        /// <summary>NMS-free or end-to-end rows containing box, score, and class. / 包含边界框、分数和类别的 NMS-free 或端到端行。</summary>
        EndToEnd = 2,
        /// <summary>End-to-end rows that begin with an explicit batch index. / 以显式批次索引开头的端到端行。</summary>
        BatchedEndToEnd = 3
    }

    /// <summary>Identifies how class candidates are selected from a raw YOLO head. / 标识从原始 YOLO Head 中选择类别候选的方式。</summary>
    public enum YoloClassSelectionMode
    {
        /// <summary>Keep only the highest-scoring class for each spatial candidate. / 每个空间候选仅保留最高分的类别。</summary>
        BestClassOnly = 0,
        /// <summary>Keep every class whose combined score exceeds the threshold. / 保留组合分数超过阈值的所有类别。</summary>
        MultiLabel = 1
    }

    /// <summary>Identifies whether exported score fields are probabilities or logits. / 标识导出的分数字段是概率还是 Logit。</summary>
    public enum YoloScoreActivation
    {
        /// <summary>Use exported probability values directly. / 直接使用导出的概率值。</summary>
        Identity = 0,
        /// <summary>Apply a numerically stable sigmoid to exported logits. / 对导出的 Logit 应用数值稳定的 Sigmoid。</summary>
        Sigmoid = 1
    }

    /// <summary>Defines one immutable YOLO output tensor contract. / 定义一个不可变 YOLO 输出张量合同。</summary>
    public sealed class YoloDetectionOutputContract
    {
        /// <summary>Initializes a YOLO detection output contract. / 初始化 YOLO 检测输出合同。</summary>
        public YoloDetectionOutputContract(string outputName, YoloDetectionOutputKind kind, int classCount, YoloScoreActivation scoreActivation = YoloScoreActivation.Identity)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw Invalid("A YOLO output tensor name is required.", outputName);
            if (!Enum.IsDefined(typeof(YoloDetectionOutputKind), kind)) throw Invalid("The YOLO output kind is invalid.", outputName);
            if (!Enum.IsDefined(typeof(YoloScoreActivation), scoreActivation)) throw Invalid("The YOLO score activation is invalid.", outputName);
            if (classCount <= 0) throw Invalid("The YOLO class count must be positive.", outputName);
            OutputName = outputName;
            Kind = kind;
            ClassCount = classCount;
            ScoreActivation = scoreActivation;
        }

        /// <summary>Gets the exact backend output tensor name. / 获取精确的后端输出张量名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the physical output layout and semantics. / 获取物理输出布局与语义。</summary>
        public YoloDetectionOutputKind Kind { get; }
        /// <summary>Gets the number of class score fields. / 获取类别分数字段数量。</summary>
        public int ClassCount { get; }
        /// <summary>Gets score activation applied before thresholding. / 获取阈值筛选前应用的分数激活。</summary>
        public YoloScoreActivation ScoreActivation { get; }
        /// <summary>Gets whether raw rows contain objectness at field four. / 获取原始行的字段四是否包含 objectness。</summary>
        public bool HasObjectness => Kind == YoloDetectionOutputKind.RawCandidateMajor;
        /// <summary>Gets the raw field count or the fixed end-to-end field count. / 获取原始字段数或固定端到端字段数。</summary>
        public int FieldCount => Kind == YoloDetectionOutputKind.RawCandidateMajor ? 5 + ClassCount : Kind == YoloDetectionOutputKind.RawAttributeMajor ? 4 + ClassCount : Kind == YoloDetectionOutputKind.BatchedEndToEnd ? 7 : 6;
        /// <summary>Gets whether the model output has already completed candidate selection and NMS semantics. / 获取模型输出是否已完成候选选择和 NMS 语义。</summary>
        public bool IsEndToEnd => Kind == YoloDetectionOutputKind.EndToEnd || Kind == YoloDetectionOutputKind.BatchedEndToEnd;

        private static VisualException Invalid(string message, string? tensorName) => new VisualException(VisualErrorCodes.YoloContractInvalid, message, tensorName: tensorName);
    }

    /// <summary>Controls strict YOLO score filtering, candidate selection, and managed NMS. / 控制严格 YOLO 分数筛选、候选选择与托管 NMS。</summary>
    public sealed class YoloDetectionDecoderOptions
    {
        /// <summary>Initializes YOLO decoder options. / 初始化 YOLO 解码选项。</summary>
        public YoloDetectionDecoderOptions(
            float scoreThreshold = 0.25f,
            float iouThreshold = 0.45f,
            DetectionNmsMode nmsMode = DetectionNmsMode.ClassAware,
            YoloClassSelectionMode classSelection = YoloClassSelectionMode.BestClassOnly,
            int maximumCandidates = 30000,
            int maximumDetections = 300)
        {
            if (!FiniteUnit(scoreThreshold)) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (!FiniteUnit(iouThreshold)) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (!Enum.IsDefined(typeof(DetectionNmsMode), nmsMode)) throw new ArgumentOutOfRangeException(nameof(nmsMode));
            if (!Enum.IsDefined(typeof(YoloClassSelectionMode), classSelection)) throw new ArgumentOutOfRangeException(nameof(classSelection));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumDetections <= 0 || maximumDetections > maximumCandidates) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            ScoreThreshold = scoreThreshold;
            IouThreshold = iouThreshold;
            NmsMode = nmsMode;
            ClassSelection = classSelection;
            MaximumCandidates = maximumCandidates;
            MaximumDetections = maximumDetections;
        }

        /// <summary>Gets the strict confidence threshold; equal scores are rejected to match upstream NMS. / 获取严格置信度阈值；为匹配上游 NMS，相等分数会被拒绝。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the IoU suppression threshold. / 获取 IoU 抑制阈值。</summary>
        public float IouThreshold { get; }
        /// <summary>Gets class-aware or class-agnostic NMS mode. / 获取按类别或忽略类别的 NMS 模式。</summary>
        public DetectionNmsMode NmsMode { get; }
        /// <summary>Gets raw-head class selection mode. / 获取原始 Head 的类别选择模式。</summary>
        public YoloClassSelectionMode ClassSelection { get; }
        /// <summary>Gets the maximum candidates entering NMS. / 获取进入 NMS 的最大候选数量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the maximum returned detections. / 获取最大返回检测数量。</summary>
        public int MaximumDetections { get; }

        private static bool FiniteUnit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }

    /// <summary>Defines backend-neutral YOLO letterbox and normalization semantics. / 定义后端无关的 YOLO Letterbox 与归一化语义。</summary>
    public sealed class YoloPreprocessingContract
    {
        /// <summary>Initializes a centered RGB NCHW YOLO preprocessing contract. / 初始化居中的 RGB NCHW YOLO 预处理合同。</summary>
        public YoloPreprocessingContract(VisualSize modelSize, int stride = 32, byte paddingValue = 114, bool scaleUp = true)
        {
            if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
            ModelSize = modelSize;
            Stride = stride;
            PaddingValue = paddingValue;
            ScaleUp = scaleUp;
        }

        /// <summary>Gets the static model image size. / 获取静态模型图像尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the model stride used by export and letterbox validation. / 获取导出和 Letterbox 校验使用的模型步长。</summary>
        public int Stride { get; }
        /// <summary>Gets the equal RGB padding value. / 获取相等的 RGB 填充值。</summary>
        public byte PaddingValue { get; }
        /// <summary>Gets whether smaller source images may be enlarged. / 获取是否允许放大小源图。</summary>
        public bool ScaleUp { get; }
        /// <summary>Gets the fixed RGB color order. / 获取固定 RGB 颜色顺序。</summary>
        public VisualColorOrder ColorOrder => VisualColorOrder.Rgb;
        /// <summary>Gets the fixed NCHW tensor layout. / 获取固定 NCHW 张量布局。</summary>
        public VisualTensorLayout Layout => VisualTensorLayout.Nchw;
        /// <summary>Gets the pixel multiplication scale of one divided by 255. / 获取像素乘法比例 1/255。</summary>
        public float PixelScale => 1f / 255f;
    }
}
