using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the four-value bounding-box representation in a detection tensor. / 标识检测张量中的四值边界框表示。</summary>
    public enum DetectionBoxFormat
    {
        /// <summary>Left, top, right, bottom. / 左、上、右、下。</summary>
        Xyxy = 0,
        /// <summary>Left, top, width, height. / 左、上、宽、高。</summary>
        Xywh = 1,
        /// <summary>Center X, center Y, width, height. / 中心 X、中心 Y、宽、高。</summary>
        Cxcywh = 2
    }

    /// <summary>Identifies how a detection confidence is calculated. / 标识检测置信度的计算方式。</summary>
    public enum DetectionScoreMode
    {
        /// <summary>Use the best class score directly. / 直接使用最佳类别分数。</summary>
        ClassScore = 0,
        /// <summary>Multiply objectness by the best class score. / 将 objectness 与最佳类别分数相乘。</summary>
        ObjectnessTimesClassScore = 1
    }

    /// <summary>Identifies class-aware or class-agnostic non-maximum suppression. / 标识按类别或忽略类别的非极大值抑制。</summary>
    public enum DetectionNmsMode
    {
        /// <summary>Suppress boxes only within the same class. / 仅在同一类别内抑制边界框。</summary>
        ClassAware = 0,
        /// <summary>Suppress overlapping boxes regardless of class. / 无论类别如何都抑制重叠边界框。</summary>
        ClassAgnostic = 1
    }

    /// <summary>Defines the field layout of one generic dense detection tensor. / 定义一个通用密集检测张量的字段布局。</summary>
    public sealed class DetectionOutputSchema
    {
        /// <summary>Initializes a detection output schema. Box coordinates occupy fields 0 through 3. / 初始化检测输出 Schema；边界框坐标占用字段 0 到 3。</summary>
        public DetectionOutputSchema(string outputName, DetectionBoxFormat boxFormat, bool normalizedCoordinates, DetectionScoreMode scoreMode, int classCount, int classScoreOffset, int objectnessIndex = -1)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output tensor name is required.", nameof(outputName));
            if (!Enum.IsDefined(typeof(DetectionBoxFormat), boxFormat)) throw new ArgumentOutOfRangeException(nameof(boxFormat));
            if (!Enum.IsDefined(typeof(DetectionScoreMode), scoreMode)) throw new ArgumentOutOfRangeException(nameof(scoreMode));
            if (classCount <= 0) throw new ArgumentOutOfRangeException(nameof(classCount));
            if (classScoreOffset < 4) throw new ArgumentOutOfRangeException(nameof(classScoreOffset));
            if (scoreMode == DetectionScoreMode.ObjectnessTimesClassScore && (objectnessIndex < 4 || objectnessIndex >= classScoreOffset)) throw new ArgumentOutOfRangeException(nameof(objectnessIndex));
            if (scoreMode == DetectionScoreMode.ClassScore && objectnessIndex >= 0) throw new ArgumentException("Direct class scores cannot declare an objectness index.", nameof(objectnessIndex));
            OutputName = outputName;
            BoxFormat = boxFormat;
            NormalizedCoordinates = normalizedCoordinates;
            ScoreMode = scoreMode;
            ClassCount = classCount;
            ClassScoreOffset = classScoreOffset;
            ObjectnessIndex = objectnessIndex;
        }

        /// <summary>Gets the output tensor name. / 获取输出张量名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the box field format. / 获取边界框字段格式。</summary>
        public DetectionBoxFormat BoxFormat { get; }
        /// <summary>Gets whether box coordinates are normalized to model width and height. / 获取边界框坐标是否按模型宽高归一化。</summary>
        public bool NormalizedCoordinates { get; }
        /// <summary>Gets score calculation mode. / 获取分数计算模式。</summary>
        public DetectionScoreMode ScoreMode { get; }
        /// <summary>Gets the number of class score fields. / 获取类别分数字段数量。</summary>
        public int ClassCount { get; }
        /// <summary>Gets the first class score field index. / 获取第一个类别分数字段索引。</summary>
        public int ClassScoreOffset { get; }
        /// <summary>Gets the objectness field index or -1. / 获取 objectness 字段索引或 -1。</summary>
        public int ObjectnessIndex { get; }
    }

    /// <summary>Controls score filtering and deterministic non-maximum suppression. / 控制分数筛选和确定性非极大值抑制。</summary>
    public sealed class DetectionDecoderOptions
    {
        /// <summary>Initializes detection decoder options. / 初始化检测解码选项。</summary>
        public DetectionDecoderOptions(float scoreThreshold = 0.25f, float iouThreshold = 0.45f, DetectionNmsMode nmsMode = DetectionNmsMode.ClassAware, int maximumCandidates = 3000, int maximumDetections = 300)
        {
            if (float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold) || scoreThreshold < 0 || scoreThreshold > 1) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (!Enum.IsDefined(typeof(DetectionNmsMode), nmsMode)) throw new ArgumentOutOfRangeException(nameof(nmsMode));
            if (maximumCandidates <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            ScoreThreshold = scoreThreshold;
            IouThreshold = iouThreshold;
            NmsMode = nmsMode;
            MaximumCandidates = maximumCandidates;
            MaximumDetections = maximumDetections;
        }

        /// <summary>Gets the inclusive confidence threshold. / 获取包含边界的置信度阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the IoU suppression threshold. / 获取 IoU 抑制阈值。</summary>
        public float IouThreshold { get; }
        /// <summary>Gets NMS class handling mode. / 获取 NMS 类别处理模式。</summary>
        public DetectionNmsMode NmsMode { get; }
        /// <summary>Gets the maximum scored candidates entering NMS. / 获取进入 NMS 的最大候选数量。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the maximum returned detections. / 获取最大返回检测数量。</summary>
        public int MaximumDetections { get; }
    }

    /// <summary>Decodes a generic dense detection tensor and applies managed deterministic NMS. / 解码通用密集检测张量并应用托管确定性 NMS。</summary>
    public sealed class DetectionDecoder : IVisualDecoder
    {
        /// <summary>Initializes a generic detection decoder. / 初始化通用检测解码器。</summary>
        public DetectionDecoder(DetectionOutputSchema schema, DetectionDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new DetectionDecoderOptions();
        }

        /// <inheritdoc />
        /// <remarks>Detection decoder task is immutable. / 检测解码器任务不可变。</remarks>
        public VisualTaskId Task => VisualTaskId.ObjectDetection;
        /// <summary>Gets the output schema. / 获取输出 Schema。</summary>
        public DetectionOutputSchema Schema { get; }
        /// <summary>Gets decoder and NMS options. / 获取解码器和 NMS 选项。</summary>
        public DetectionDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Coordinates use half-open rectangles, are mapped back to source space, clipped, then suppressed. / 坐标使用半开区间矩形，逆向映射到源图空间并裁剪后再抑制。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw new VisualException(VisualErrorCodes.DecodeFailed, "Detection decoder currently requires batch size one.", profileId: context.Profile.ProfileId, tensorName: Schema.OutputName);
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(Schema.OutputName); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "Detection output tensor is missing.", exception, context.Profile.ProfileId, Schema.OutputName, modelId: context.Profile.ModelId); }
            int candidates;
            int fields;
            if (tensor.Shape.Rank == 2)
            {
                candidates = checked((int)tensor.Shape[0]);
                fields = checked((int)tensor.Shape[1]);
            }
            else if (tensor.Shape.Rank == 3 && tensor.Shape[0] == 1)
            {
                candidates = checked((int)tensor.Shape[1]);
                fields = checked((int)tensor.Shape[2]);
            }
            else throw new VisualException(VisualErrorCodes.TensorInvalid, "Detection output shape must be [candidates,fields] or [1,candidates,fields].", profileId: context.Profile.ProfileId, tensorName: Schema.OutputName, technicalDetails: tensor.Shape.ToString());
            if (candidates < 0 || fields < 4 || Schema.ClassScoreOffset + Schema.ClassCount > fields || (Schema.ObjectnessIndex >= fields)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Detection output field layout is incompatible with its schema.", profileId: context.Profile.ProfileId, tensorName: Schema.OutputName);
            if (tensor.Length != (long)candidates * fields) throw new VisualException(VisualErrorCodes.TensorInvalid, "Detection output element count is inconsistent with its shape.", profileId: context.Profile.ProfileId, tensorName: Schema.OutputName);
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.OutputName);
            var decoded = new List<DetectionCandidate>();
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int offset = candidateIndex * fields;
                float objectness = 1;
                if (Schema.ScoreMode == DetectionScoreMode.ObjectnessTimesClassScore)
                {
                    objectness = ValidateUnit(values[offset + Schema.ObjectnessIndex], context, candidateIndex, "objectness");
                }
                int bestClass = 0;
                float bestClassScore = ValidateUnit(values[offset + Schema.ClassScoreOffset], context, candidateIndex, "class-score");
                for (int classIndex = 1; classIndex < Schema.ClassCount; classIndex++)
                {
                    float classScore = ValidateUnit(values[offset + Schema.ClassScoreOffset + classIndex], context, candidateIndex, "class-score");
                    if (classScore > bestClassScore)
                    {
                        bestClassScore = classScore;
                        bestClass = classIndex;
                    }
                }
                float score = objectness * bestClassScore;
                if (score < Options.ScoreThreshold) continue;
                RectangleF modelBox = DecodeBox(values, offset, context.Input.ModelSize);
                RectangleF sourceBox = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0 || sourceBox.Height <= 0) continue;
                decoded.Add(new DetectionCandidate(candidateIndex, bestClass, score, sourceBox));
            }

            List<DetectionCandidate> ordered = decoded.OrderByDescending(value => value.Score).ThenBy(value => value.ClassIndex).ThenBy(value => value.SourceIndex).Take(Options.MaximumCandidates).ToList();
            List<DetectionCandidate> kept = Suppress(ordered, context.CancellationToken);
            var results = new List<Detection>(kept.Count);
            foreach (DetectionCandidate candidate in kept) results.Add(new Detection(candidate.Box, new LabelScore(candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score)));
            return new DetectionResult(results);
        }

        /// <summary>Calculates intersection over union for two half-open rectangles. / 计算两个半开区间矩形的交并比。</summary>
        public static float IntersectionOverUnion(RectangleF first, RectangleF second)
        {
            float left = Math.Max(first.X, second.X);
            float top = Math.Max(first.Y, second.Y);
            float right = Math.Min(first.Right, second.Right);
            float bottom = Math.Min(first.Bottom, second.Bottom);
            float intersectionWidth = Math.Max(0, right - left);
            float intersectionHeight = Math.Max(0, bottom - top);
            float intersection = intersectionWidth * intersectionHeight;
            float union = (first.Width * first.Height) + (second.Width * second.Height) - intersection;
            return union <= 0 ? 0 : intersection / union;
        }

        private RectangleF DecodeBox(float[] values, int offset, VisualSize modelSize)
        {
            float first = values[offset];
            float second = values[offset + 1];
            float third = values[offset + 2];
            float fourth = values[offset + 3];
            if (Schema.NormalizedCoordinates)
            {
                first *= modelSize.Width;
                third *= modelSize.Width;
                second *= modelSize.Height;
                fourth *= modelSize.Height;
            }
            float left;
            float top;
            float right;
            float bottom;
            if (Schema.BoxFormat == DetectionBoxFormat.Xyxy)
            {
                left = first; top = second; right = third; bottom = fourth;
            }
            else if (Schema.BoxFormat == DetectionBoxFormat.Xywh)
            {
                left = first; top = second; right = first + third; bottom = second + fourth;
            }
            else
            {
                left = first - (third / 2f); top = second - (fourth / 2f); right = first + (third / 2f); bottom = second + (fourth / 2f);
            }
            if (right < left || bottom < top) throw new VisualException(VisualErrorCodes.DecodeFailed, "Detection box has negative width or height.", tensorName: Schema.OutputName);
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private List<DetectionCandidate> Suppress(List<DetectionCandidate> ordered, System.Threading.CancellationToken cancellationToken)
        {
            var kept = new List<DetectionCandidate>();
            foreach (DetectionCandidate candidate in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool suppressed = false;
                foreach (DetectionCandidate existing in kept)
                {
                    if (Options.NmsMode == DetectionNmsMode.ClassAware && existing.ClassIndex != candidate.ClassIndex) continue;
                    if (IntersectionOverUnion(existing.Box, candidate.Box) > Options.IouThreshold) { suppressed = true; break; }
                }
                if (!suppressed) kept.Add(candidate);
                if (kept.Count >= Options.MaximumDetections) break;
            }
            return kept;
        }

        private float ValidateUnit(float value, VisualDecodeContext context, int candidateIndex, string field)
        {
            if (value < 0 || value > 1) throw new VisualException(VisualErrorCodes.DecodeFailed, "Detection score must be in [0,1].", profileId: context.Profile.ProfileId, tensorName: Schema.OutputName, technicalDetails: "candidate=" + candidateIndex + ";field=" + field);
            return value;
        }

        private sealed class DetectionCandidate
        {
            public DetectionCandidate(int sourceIndex, int classIndex, float score, RectangleF box) { SourceIndex = sourceIndex; ClassIndex = classIndex; Score = score; Box = box; }
            public int SourceIndex { get; }
            public int ClassIndex { get; }
            public float Score { get; }
            public RectangleF Box { get; }
        }
    }
}
