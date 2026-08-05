using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines a strict center-size-angle output schema. / 定义严格的中心宽高角输出 Schema。</summary>
    public sealed class CenterSizeAngleOutputSchema
    {
        /// <summary>Initializes a center-size-angle schema. / 初始化中心宽高角 Schema。</summary>
        public CenterSizeAngleOutputSchema(
            string boxesOutputName,
            string scoresOutputName,
            string classesOutputName,
            OrientedCenterSizeAngleOrder? boxOrder = null,
            OrientedCoordinateSpace coordinateSpace = OrientedCoordinateSpace.ModelPixels,
            OrientedAngleUnit angleUnit = OrientedAngleUnit.Radians,
            OrientedAngleDirection angleDirection = OrientedAngleDirection.Clockwise,
            OrientedAngleRange angleRange = OrientedAngleRange.MinusHalfPiToHalfPi,
            OrientedWidthConvention widthConvention = OrientedWidthConvention.WidthAxis,
            OrientedDetectionBoundaryMode boundaryMode = OrientedDetectionBoundaryMode.Preserve,
            float epsilon = 0.000001f)
        {
            BoxesOutputName = Required(boxesOutputName, nameof(boxesOutputName));
            ScoresOutputName = Required(scoresOutputName, nameof(scoresOutputName));
            ClassesOutputName = Required(classesOutputName, nameof(classesOutputName));
            EnsureDistinct(BoxesOutputName, ScoresOutputName, ClassesOutputName);
            ValidateEnums(coordinateSpace, angleUnit, angleDirection, angleRange, widthConvention, boundaryMode);
            ValidateEpsilon(epsilon);
            BoxOrder = boxOrder ?? new OrientedCenterSizeAngleOrder();
            CoordinateSpace = coordinateSpace;
            AngleUnit = angleUnit;
            AngleDirection = angleDirection;
            AngleRange = angleRange;
            WidthConvention = widthConvention;
            BoundaryMode = boundaryMode;
            Epsilon = epsilon;
        }

        /// <summary>Gets boxes output name with shape [1,N,5]. / 获取形状为 [1,N,5] 的边界框输出名称。</summary>
        public string BoxesOutputName { get; }
        /// <summary>Gets scores output name with shape [1,N]. / 获取形状为 [1,N] 的分数输出名称。</summary>
        public string ScoresOutputName { get; }
        /// <summary>Gets classes output name with shape [1,N]. / 获取形状为 [1,N] 的类别输出名称。</summary>
        public string ClassesOutputName { get; }
        /// <summary>Gets explicit five-component order. / 获取显式五分量顺序。</summary>
        public OrientedCenterSizeAngleOrder BoxOrder { get; }
        /// <summary>Gets coordinate space. / 获取坐标空间。</summary>
        public OrientedCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets angle unit. / 获取角度单位。</summary>
        public OrientedAngleUnit AngleUnit { get; }
        /// <summary>Gets positive angle direction. / 获取正角方向。</summary>
        public OrientedAngleDirection AngleDirection { get; }
        /// <summary>Gets accepted angle interval. / 获取允许角度区间。</summary>
        public OrientedAngleRange AngleRange { get; }
        /// <summary>Gets width/height convention. / 获取宽高约定。</summary>
        public OrientedWidthConvention WidthConvention { get; }
        /// <summary>Gets out-of-bounds behavior. / 获取越界行为。</summary>
        public OrientedDetectionBoundaryMode BoundaryMode { get; }
        /// <summary>Gets geometric epsilon. / 获取几何 epsilon。</summary>
        public float Epsilon { get; }

        internal static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An output tensor name is required.", parameterName);
            return value;
        }

        internal static void EnsureDistinct(params string[] names)
        {
            for (int first = 0; first < names.Length; first++) for (int second = first + 1; second < names.Length; second++) if (string.Equals(names[first], names[second], StringComparison.Ordinal)) throw new ArgumentException("Output tensor names must be unique.");
        }

        internal static void ValidateEpsilon(float epsilon)
        {
            if (float.IsNaN(epsilon) || float.IsInfinity(epsilon) || epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(epsilon));
        }

        internal static void ValidateEnums(OrientedCoordinateSpace coordinateSpace, OrientedAngleUnit angleUnit, OrientedAngleDirection angleDirection, OrientedAngleRange angleRange, OrientedWidthConvention widthConvention, OrientedDetectionBoundaryMode boundaryMode)
        {
            if (!Enum.IsDefined(typeof(OrientedCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (!Enum.IsDefined(typeof(OrientedAngleUnit), angleUnit)) throw new ArgumentOutOfRangeException(nameof(angleUnit));
            if (!Enum.IsDefined(typeof(OrientedAngleDirection), angleDirection)) throw new ArgumentOutOfRangeException(nameof(angleDirection));
            if (!Enum.IsDefined(typeof(OrientedAngleRange), angleRange)) throw new ArgumentOutOfRangeException(nameof(angleRange));
            if (!Enum.IsDefined(typeof(OrientedWidthConvention), widthConvention)) throw new ArgumentOutOfRangeException(nameof(widthConvention));
            if (!Enum.IsDefined(typeof(OrientedDetectionBoundaryMode), boundaryMode)) throw new ArgumentOutOfRangeException(nameof(boundaryMode));
        }
    }

    /// <summary>Defines a strict four-corner output schema. / 定义严格的四角点输出 Schema。</summary>
    public sealed class FourCornerOutputSchema
    {
        /// <summary>Initializes a four-corner schema. / 初始化四角点 Schema。</summary>
        public FourCornerOutputSchema(
            string cornersOutputName,
            string scoresOutputName,
            string classesOutputName,
            OrientedCoordinateSpace coordinateSpace = OrientedCoordinateSpace.ModelPixels,
            OrientedVertexOrder inputVertexOrder = OrientedVertexOrder.CounterClockwise,
            OrientedStartVertexRule startVertexRule = OrientedStartVertexRule.MinimumYThenX,
            OrientedDetectionBoundaryMode boundaryMode = OrientedDetectionBoundaryMode.Preserve,
            float epsilon = 0.000001f)
        {
            CornersOutputName = CenterSizeAngleOutputSchema.Required(cornersOutputName, nameof(cornersOutputName));
            ScoresOutputName = CenterSizeAngleOutputSchema.Required(scoresOutputName, nameof(scoresOutputName));
            ClassesOutputName = CenterSizeAngleOutputSchema.Required(classesOutputName, nameof(classesOutputName));
            CenterSizeAngleOutputSchema.EnsureDistinct(CornersOutputName, ScoresOutputName, ClassesOutputName);
            if (!Enum.IsDefined(typeof(OrientedCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (!Enum.IsDefined(typeof(OrientedVertexOrder), inputVertexOrder)) throw new ArgumentOutOfRangeException(nameof(inputVertexOrder));
            if (!Enum.IsDefined(typeof(OrientedStartVertexRule), startVertexRule)) throw new ArgumentOutOfRangeException(nameof(startVertexRule));
            if (!Enum.IsDefined(typeof(OrientedDetectionBoundaryMode), boundaryMode)) throw new ArgumentOutOfRangeException(nameof(boundaryMode));
            CenterSizeAngleOutputSchema.ValidateEpsilon(epsilon);
            CoordinateSpace = coordinateSpace;
            InputVertexOrder = inputVertexOrder;
            StartVertexRule = startVertexRule;
            BoundaryMode = boundaryMode;
            Epsilon = epsilon;
        }

        /// <summary>Gets corners output name with shape [1,N,8]. / 获取形状为 [1,N,8] 的角点输出名称。</summary>
        public string CornersOutputName { get; }
        /// <summary>Gets scores output name. / 获取分数输出名称。</summary>
        public string ScoresOutputName { get; }
        /// <summary>Gets classes output name. / 获取类别输出名称。</summary>
        public string ClassesOutputName { get; }
        /// <summary>Gets coordinate space. / 获取坐标空间。</summary>
        public OrientedCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets declared input vertex order. / 获取声明的输入顶点顺序。</summary>
        public OrientedVertexOrder InputVertexOrder { get; }
        /// <summary>Gets deterministic start rule. / 获取确定性首顶点规则。</summary>
        public OrientedStartVertexRule StartVertexRule { get; }
        /// <summary>Gets out-of-bounds behavior. / 获取越界行为。</summary>
        public OrientedDetectionBoundaryMode BoundaryMode { get; }
        /// <summary>Gets geometric epsilon. / 获取几何 epsilon。</summary>
        public float Epsilon { get; }
    }

    /// <summary>Controls common OBB score filtering, polygon NMS, cancellation, and work bounds. / 控制 OBB 分数筛选、多边形 NMS、取消和工作区边界。</summary>
    public sealed class OrientedDetectionDecoderOptions
    {
        /// <summary>Initializes bounded OBB decoder options. / 初始化有界 OBB 解码选项。</summary>
        public OrientedDetectionDecoderOptions(
            float scoreThreshold = 0.25f,
            float iouThreshold = 0.45f,
            DetectionNmsMode nmsMode = DetectionNmsMode.ClassAware,
            int maximumCandidates = 3000,
            int maximumDetections = 100,
            long maximumWorkspaceBytes = 64L * 1024 * 1024)
        {
            if (float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold) || scoreThreshold < 0) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (!Enum.IsDefined(typeof(DetectionNmsMode), nmsMode)) throw new ArgumentOutOfRangeException(nameof(nmsMode));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            if (maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkspaceBytes));
            ScoreThreshold = scoreThreshold;
            IouThreshold = iouThreshold;
            NmsMode = nmsMode;
            MaximumCandidates = maximumCandidates;
            MaximumDetections = maximumDetections;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets inclusive score threshold. / 获取包含边界的分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets rotated IoU NMS threshold. / 获取旋转 IoU NMS 阈值。</summary>
        public float IouThreshold { get; }
        /// <summary>Gets class-aware or class-agnostic NMS mode. / 获取分类别或忽略类别 NMS 模式。</summary>
        public DetectionNmsMode NmsMode { get; }
        /// <summary>Gets maximum accepted candidates. / 获取最大候选数量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets maximum retained detections. / 获取最大保留检测数量。</summary>
        public int MaximumDetections { get; }
        /// <summary>Gets maximum temporary workspace bytes. / 获取最大临时工作区字节数。</summary>
        public long MaximumWorkspaceBytes { get; }
    }
}
