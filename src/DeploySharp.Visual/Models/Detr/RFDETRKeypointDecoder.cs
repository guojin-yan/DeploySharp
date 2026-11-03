using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.Yolo;

namespace JYPPX.DeploySharp.Visual.Models.Detr
{
    /// <summary>Decodes RF-DETR COCO-17 keypoints including official precision-trace score fusion. / 解码 RF-DETR COCO-17 关键点，包含官方精度矩阵迹分数融合。</summary>
    public sealed class RFDETRKeypointDecoder : IVisualDecoder
    {
        /// <summary>Initializes an exact raw-output preview decoder; top-k runs before uncertainty fusion and strict thresholding. / 初始化精确原始输出预览解码器；top-k 在不确定度融合和严格阈值过滤之前运行。</summary>
        public RFDETRKeypointDecoder(string boxesName = "dets", string logitsName = "labels", string keypointsName = "keypoints", int queryCount = 100, float scoreThreshold = 0.4f, int topK = 100, int maximumResults = 100, float traceAlpha = 0.2f)
        {
            if (string.IsNullOrWhiteSpace(boxesName) || string.IsNullOrWhiteSpace(logitsName) || string.IsNullOrWhiteSpace(keypointsName)) throw new ArgumentException("All output names are required.");
            if (boxesName == logitsName || boxesName == keypointsName || logitsName == keypointsName) throw new ArgumentException("Output names must be distinct.");
            if (queryCount <= 0 || queryCount > 3000) throw new ArgumentOutOfRangeException(nameof(queryCount));
            if (float.IsNaN(scoreThreshold) || scoreThreshold < 0 || scoreThreshold > 1) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (topK <= 0 || topK > checked(queryCount * 2)) throw new ArgumentOutOfRangeException(nameof(topK));
            if (maximumResults <= 0 || maximumResults > topK) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            if (float.IsNaN(traceAlpha) || float.IsInfinity(traceAlpha) || traceAlpha < 0) throw new ArgumentOutOfRangeException(nameof(traceAlpha));
            BoxesName = boxesName; LogitsName = logitsName; KeypointsName = keypointsName;
            QueryCount = queryCount; ScoreThreshold = scoreThreshold; TopK = topK; MaximumResults = maximumResults; TraceAlpha = traceAlpha;
        }

        /// <summary>Gets the pose task. / 获取姿态任务。</summary>
        public VisualTaskId Task => VisualTaskId.PoseEstimation;
        /// <summary>Gets the box port name. / 获取边界框端口名。</summary>
        public string BoxesName { get; }
        /// <summary>Gets the class-logit port name. / 获取类别 Logit 端口名。</summary>
        public string LogitsName { get; }
        /// <summary>Gets the raw keypoint port name. / 获取原始关键点端口名。</summary>
        public string KeypointsName { get; }
        /// <summary>Gets the exact query count. / 获取精确 Query 数。</summary>
        public int QueryCount { get; }
        /// <summary>Gets the strict fused-score threshold. / 获取严格融合分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the number selected before fusion. / 获取融合前选取的数量。</summary>
        public int TopK { get; }
        /// <summary>Gets the result limit applied after fusion and filtering. / 获取融合与过滤之后的结果上限。</summary>
        public int MaximumResults { get; }
        /// <summary>Gets the official uncertainty exponent; zero disables fusion. / 获取官方不确定度指数；零表示禁用融合。</summary>
        public float TraceAlpha { get; }

        /// <summary>Decodes owned source-space poses; raw visibility and precision fields remain model-specific and are not presented as ground-truth visibility. / 解码自有源图姿态；原始可见性和精度字段保留模型语义，不作为真实标注可见性。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw PortableDetectorDecoder.Failure(context, "RF-DETR Keypoint Preview requires fixed batch one.");
            float[] boxes = Read(context, BoxesName, new TensorShape(1, QueryCount, 4));
            float[] logits = Read(context, LogitsName, new TensorShape(1, QueryCount, 2));
            float[] points = Read(context, KeypointsName, new TensorShape(1, QueryCount, 34, 8));
            var candidates = new List<PortableRfCandidate>(logits.Length);
            for (int index = 0; index < logits.Length; index++) candidates.Add(new PortableRfCandidate(index / 2, index % 2, index, PortableDetectorDecoder.Sigmoid(logits[index])));
            candidates.Sort(PortableDetectorDecoder.CompareCandidates);
            var instances = new List<PoseInstance>();
            for (int index = 0; index < Math.Min(TopK, candidates.Count); index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                PortableRfCandidate candidate = candidates[index];
                if (candidate.ClassIndex != 1) continue;
                int offset = checked((candidate.Query * 34 + 17) * 8);
                float score = candidate.Score;
                if (TraceAlpha > 0)
                {
                    double fusedLog = Math.Log(score) - TraceAlpha * LogMeanTrace(points, offset);
                    double probability = fusedLog >= 0 ? 1 / (1 + Math.Exp(-fusedLog)) : Math.Exp(fusedLog) / (1 + Math.Exp(fusedLog));
                    score = Math.Min(0.99999994f, (float)probability);
                }
                if (score <= ScoreThreshold) continue;
                int boxOffset = candidate.Query * 4;
                float width = boxes[boxOffset + 2] * context.Input.ModelSize.Width;
                float height = boxes[boxOffset + 3] * context.Input.ModelSize.Height;
                if (width <= 0 || height <= 0) continue;
                var modelBox = new RectangleF(boxes[boxOffset] * context.Input.ModelSize.Width - width / 2, boxes[boxOffset + 1] * context.Input.ModelSize.Height - height / 2, width, height);
                RectangleF sourceBox = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0 || sourceBox.Height <= 0) continue;
                var keypoints = new List<PoseKeypoint>(17);
                for (int point = 0; point < 17; point++)
                {
                    int start = offset + point * 8;
                    PointF position = context.Input.Transform.ToSource(new PointF(points[start] * context.Input.ModelSize.Width, points[start + 1] * context.Input.ModelSize.Height));
                    float confidence = PortableDetectorDecoder.Sigmoid(points[start + 2]);
                    keypoints.Add(new PoseKeypoint(point, position, confidence, PoseKeypointVisibility.Unknown, true));
                }
                instances.Add(new PoseInstance(candidate.SourceIndex, score, keypoints, sourceBox, 1));
            }
            instances.Sort((left, right) => { int order = right.Score.CompareTo(left.Score); return order != 0 ? order : left.SourceIndex.CompareTo(right.SourceIndex); });
            if (instances.Count > MaximumResults) instances.RemoveRange(MaximumResults, instances.Count - MaximumResults);
            return new PoseEstimationResult(YoloPoseTopologies.Coco17, instances, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private static float[] Read(VisualDecodeContext context, string name, TensorShape expected)
        {
            ITensor tensor = PortableDetectorDecoder.Required(context, name);
            if (tensor.ElementType != TensorElementType.Float32 || !tensor.Shape.Equals(expected) || tensor.Length != expected.GetElementCount() || !(tensor.Buffer is float[] values)) throw PortableDetectorDecoder.Failure(context, "RF-DETR Keypoint Preview output must be Float32 " + expected + ".", name, technicalDetails: tensor.Shape.ToString());
            for (int index = 0; index < values.Length; index++)
            {
                if ((index & 1023) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                PortableDetectorDecoder.ValidateFinite(values[index], context, name);
            }
            return values;
        }

        private static double LogMeanTrace(float[] points, int offset)
        {
            double numerator = double.NegativeInfinity;
            double denominator = double.NegativeInfinity;
            for (int index = 0; index < 17; index++)
            {
                int start = offset + index * 8;
                double logWeight = Math.Log(Math.Max(1e-12, PortableDetectorDecoder.Sigmoid(points[start + 2])));
                double a = -2.0 * points[start + 4];
                double b = -2.0 * points[start + 6];
                double c = 2 * Math.Log(Math.Max(1e-12, Math.Abs(points[start + 5]))) + a + b;
                numerator = LogAdd(numerator, LogAdd(LogAdd(a, b), c) + logWeight);
                denominator = LogAdd(denominator, logWeight);
            }
            return numerator - denominator;
        }

        private static double LogAdd(double left, double right)
        {
            if (double.IsNegativeInfinity(left)) return right;
            double maximum = Math.Max(left, right);
            return maximum + Math.Log(Math.Exp(left - maximum) + Math.Exp(right - maximum));
        }
    }
}
