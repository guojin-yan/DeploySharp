using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Adapts deterministic classification selection to the generic ROI merger contract. / 将确定性分类选择适配到通用 ROI 合并合同。</summary>
    public sealed class ClassificationRoiResultMergerAdapter : IRoiResultMerger<ClassificationResult>
    {
        /// <summary>Merges classification candidates without applying spatial NMS. / 合并分类候选，不应用空间 NMS。</summary>
        public IReadOnlyList<ClassificationResult> Merge(IReadOnlyList<RoiProjectedResult<ClassificationResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.ClassAwareNms || mode == RoiResultMergeMode.ClassAgnosticNms || mode == RoiResultMergeMode.TaskSpecific || mode == RoiResultMergeMode.WeightedBoxFusion)
            {
                throw new NotSupportedException("Classification ROI merging does not support spatial NMS; use KeepAll, HighestConfidence, or RoiPriority.");
            }

            var ordered = results.Select(value => value ?? throw new ArgumentException("Classification candidates cannot contain null values.", nameof(results))).ToList();
            ordered.Sort((left, right) => Compare(left, right, mode));
            if (mode != RoiResultMergeMode.KeepAll && ordered.Count > 1) ordered.RemoveRange(1, ordered.Count - 1);
            return ordered.Select(value => value.Result).ToList().AsReadOnly();
        }

        private static int Compare(RoiProjectedResult<ClassificationResult> left, RoiProjectedResult<ClassificationResult> right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0) return priority;
            }
            float leftScore = left.Result.TopPrediction?.Score ?? float.NegativeInfinity;
            float rightScore = right.Result.TopPrediction?.Score ?? float.NegativeInfinity;
            int score = rightScore.CompareTo(leftScore);
            if (score != 0) return score;
            int fallbackPriority = right.Priority.CompareTo(left.Priority);
            if (fallbackPriority != 0) return fallbackPriority;
            return string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
        }
    }

    /// <summary>Adapts label-only semantic segmentation fusion to the generic ROI merger contract. / 将仅标签语义分割融合适配到通用 ROI 合并合同。</summary>
    public sealed class SemanticSegmentationRoiResultMergerAdapter : IRoiResultMerger<SemanticSegmentationResult>
    {
        private readonly RoiSemanticSegmentationResultMerger _inner = new RoiSemanticSegmentationResultMerger();
        private readonly RoiSemanticLabelMergeMode _labelMode;
        private readonly RoiSemanticProbabilityMergeMode? _probabilityMode;

        /// <summary>Initializes a semantic segmentation adapter. Set a probability mode to retain probability maps. / 初始化语义分割适配器；设置概率模式可保留概率图。</summary>
        public SemanticSegmentationRoiResultMergerAdapter(RoiSemanticLabelMergeMode labelMode = RoiSemanticLabelMergeMode.RoiPriority, RoiSemanticProbabilityMergeMode? probabilityMode = null)
        {
            if (!Enum.IsDefined(typeof(RoiSemanticLabelMergeMode), labelMode)) throw new ArgumentOutOfRangeException(nameof(labelMode));
            if (probabilityMode.HasValue && !Enum.IsDefined(typeof(RoiSemanticProbabilityMergeMode), probabilityMode.Value)) throw new ArgumentOutOfRangeException(nameof(probabilityMode));
            _labelMode = labelMode;
            _probabilityMode = probabilityMode;
        }

        /// <inheritdoc />
        public IReadOnlyList<SemanticSegmentationResult> Merge(IReadOnlyList<RoiProjectedResult<SemanticSegmentationResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.ClassAwareNms || mode == RoiResultMergeMode.ClassAgnosticNms || mode == RoiResultMergeMode.HighestConfidence || mode == RoiResultMergeMode.TaskSpecific || mode == RoiResultMergeMode.WeightedBoxFusion)
            {
                throw new NotSupportedException("Semantic segmentation ROI merging requires an explicit label or probability fusion policy.");
            }
            if (_probabilityMode.HasValue)
            {
                RoiMergedSemanticSegmentationResult probability = _inner.MergeWithProbabilities(results, _probabilityMode.Value);
                return new[] { probability.Result };
            }
            RoiMergedSemanticSegmentationResult labels = _inner.Merge(results, _labelMode);
            return new[] { labels.Result };
        }
    }

    /// <summary>Adapts source-resolution anomaly-map composition to the generic ROI merger contract. / 将源分辨率异常图合成适配到通用 ROI 合并合同。</summary>
    public sealed class AnomalyRoiResultMergerAdapter : IRoiResultMerger<AnomalyDetectionResult>
    {
        private readonly RoiAnomalyResultComposer _inner = new RoiAnomalyResultComposer();
        private readonly RoiAnomalyMergeMode _mergeMode;

        /// <summary>Initializes an anomaly-map adapter. / 初始化异常图适配器。</summary>
        public AnomalyRoiResultMergerAdapter(RoiAnomalyMergeMode mergeMode = RoiAnomalyMergeMode.MaxScore)
        {
            if (!Enum.IsDefined(typeof(RoiAnomalyMergeMode), mergeMode)) throw new ArgumentOutOfRangeException(nameof(mergeMode));
            _mergeMode = mergeMode;
        }

        /// <inheritdoc />
        public IReadOnlyList<AnomalyDetectionResult> Merge(IReadOnlyList<RoiProjectedResult<AnomalyDetectionResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.ClassAwareNms || mode == RoiResultMergeMode.ClassAgnosticNms || mode == RoiResultMergeMode.HighestConfidence || mode == RoiResultMergeMode.TaskSpecific || mode == RoiResultMergeMode.WeightedBoxFusion)
            {
                throw new NotSupportedException("Anomaly ROI merging requires an explicit score-map fusion policy.");
            }
            return new[] { _inner.Compose(results, _mergeMode) };
        }
    }

    /// <summary>Adapts source-resolution alpha composition to the generic ROI merger contract. / 将源分辨率 Alpha 合成适配到通用 ROI 合并合同。</summary>
    public sealed class AlphaRoiResultMergerAdapter : IRoiResultMerger<BackgroundRemovalResult>
    {
        private readonly RoiAlphaResultComposer _inner = new RoiAlphaResultComposer();
        private readonly RoiAlphaMergeMode _mergeMode;

        /// <summary>Initializes an alpha-mask adapter. / 初始化 Alpha 掩码适配器。</summary>
        public AlphaRoiResultMergerAdapter(RoiAlphaMergeMode mergeMode = RoiAlphaMergeMode.MaxAlpha)
        {
            if (!Enum.IsDefined(typeof(RoiAlphaMergeMode), mergeMode)) throw new ArgumentOutOfRangeException(nameof(mergeMode));
            _mergeMode = mergeMode;
        }

        /// <inheritdoc />
        public IReadOnlyList<BackgroundRemovalResult> Merge(IReadOnlyList<RoiProjectedResult<BackgroundRemovalResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.ClassAwareNms || mode == RoiResultMergeMode.ClassAgnosticNms || mode == RoiResultMergeMode.HighestConfidence || mode == RoiResultMergeMode.TaskSpecific || mode == RoiResultMergeMode.WeightedBoxFusion)
            {
                throw new NotSupportedException("Alpha ROI merging requires an explicit per-pixel composition policy.");
            }
            return new[] { _inner.Compose(results, _mergeMode) };
        }
    }

    /// <summary>Adapts the built-in axis-aligned detection merge policy to the generic ROI merger contract. / 将内置轴对齐检测合并策略适配到通用 ROI 合并合同。</summary>
    public sealed class DetectionRoiResultMergerAdapter : IRoiResultMerger<DetectionResult>
    {
        private readonly float _iouThreshold;
        private readonly int _maximumDetections;

        /// <summary>Initializes an axis-aligned NMS adapter. / 初始化轴对齐 NMS 适配器。</summary>
        public DetectionRoiResultMergerAdapter(float iouThreshold = .45f, int maximumDetections = 300)
        {
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            _iouThreshold = iouThreshold;
            _maximumDetections = maximumDetections;
        }

        /// <inheritdoc />
        public IReadOnlyList<DetectionResult> Merge(IReadOnlyList<RoiProjectedResult<DetectionResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific detection merging requires an application-provided merger.");
            if (mode == RoiResultMergeMode.WeightedBoxFusion)
            {
                var projected = new List<RoiProjectedDetection>();
                foreach (RoiProjectedResult<DetectionResult> item in results)
                {
                    if (item == null) throw new ArgumentException("Detection candidates cannot contain null values.", nameof(results));
                    foreach (Detection detection in item.Result.Detections) projected.Add(new RoiProjectedDetection(item.RoiId, detection, item.WindowIndex, item.Priority));
                }
                IReadOnlyList<RoiProjectedDetection> fused = RoiDetectionResultMerger.RoiDetectionMergeMath.WeightedBoxFusion(projected, _iouThreshold, _maximumDetections);
                return new[] { new DetectionResult(fused.Select(value => value.Detection)) };
            }
            var candidates = new List<Candidate>();
            foreach (RoiProjectedResult<DetectionResult> projected in results)
            {
                if (projected == null) throw new ArgumentException("Detection candidates cannot contain null values.", nameof(results));
                foreach (Detection detection in projected.Result.Detections) candidates.Add(new Candidate(projected, detection));
            }
            candidates.Sort((left, right) => Compare(left, right, mode));
            var kept = new List<Candidate>(Math.Min(candidates.Count, _maximumDetections));
            bool classAgnostic = mode == RoiResultMergeMode.ClassAgnosticNms;
            foreach (Candidate candidate in candidates)
            {
                int overlap = -1;
                if (mode != RoiResultMergeMode.KeepAll)
                {
                    for (int index = 0; index < kept.Count; index++)
                    {
                        if (!classAgnostic && kept[index].Detection.Label.Index != candidate.Detection.Label.Index) continue;
                        if (AxisAlignedIou(kept[index].Detection.Box, candidate.Detection.Box) > _iouThreshold) { overlap = index; break; }
                    }
                }
                if (overlap < 0) kept.Add(candidate);
                if (kept.Count >= _maximumDetections) break;
            }
            return new[] { new DetectionResult(kept.Select(value => value.Detection)) };
        }

        private static int Compare(Candidate left, Candidate right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Source.Priority.CompareTo(left.Source.Priority);
                if (priority != 0) return priority;
            }
            int score = right.Detection.Label.Score.CompareTo(left.Detection.Label.Score);
            if (score != 0) return score;
            int fallback = right.Source.Priority.CompareTo(left.Source.Priority);
            if (fallback != 0) return fallback;
            int roi = string.Compare(left.Source.RoiId, right.Source.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            int label = left.Detection.Label.Index.CompareTo(right.Detection.Label.Index);
            if (label != 0) return label;
            int x = left.Detection.Box.X.CompareTo(right.Detection.Box.X);
            return x != 0 ? x : left.Detection.Box.Y.CompareTo(right.Detection.Box.Y);
        }

        private static float AxisAlignedIou(RectangleF first, RectangleF second)
        {
            float left = Math.Max(first.X, second.X);
            float top = Math.Max(first.Y, second.Y);
            float right = Math.Min(first.Right, second.Right);
            float bottom = Math.Min(first.Bottom, second.Bottom);
            float intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
            float union = Math.Max(0, first.Width) * Math.Max(0, first.Height) + Math.Max(0, second.Width) * Math.Max(0, second.Height) - intersection;
            return union <= 0 ? 0 : intersection / union;
        }

        private sealed class Candidate
        {
            internal Candidate(RoiProjectedResult<DetectionResult> source, Detection detection) { Source = source; Detection = detection; }
            internal RoiProjectedResult<DetectionResult> Source { get; }
            internal Detection Detection { get; }
        }
    }

    /// <summary>Adapts exact rotated IoU merging to the generic ROI merger contract. / 将精确旋转 IoU 合并适配到通用 ROI 合并合同。</summary>
    public sealed class OrientedDetectionRoiResultMergerAdapter : IRoiResultMerger<OrientedDetectionResult>
    {
        private readonly RoiOrientedDetectionResultMerger _inner = new RoiOrientedDetectionResultMerger();
        private readonly float _iouThreshold;
        private readonly int _maximumDetections;

        /// <summary>Initializes an OBB merger adapter. / 初始化 OBB 合并适配器。</summary>
        public OrientedDetectionRoiResultMergerAdapter(float iouThreshold = .45f, int maximumDetections = 300)
        {
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            _iouThreshold = iouThreshold;
            _maximumDetections = maximumDetections;
        }

        /// <inheritdoc />
        public IReadOnlyList<OrientedDetectionResult> Merge(IReadOnlyList<RoiProjectedResult<OrientedDetectionResult>> results, RoiResultMergeMode mode)
        {
            var candidates = new List<RoiProjectedOrientedDetection>();
            VisualSize? sourceSize = null;
            string? profile = null;
            ModelId model = default(ModelId);
            foreach (RoiProjectedResult<OrientedDetectionResult> projected in results ?? throw new ArgumentNullException(nameof(results)))
            {
                if (projected == null) throw new ArgumentException("OBB candidates cannot contain null values.", nameof(results));
                OrientedDetectionResult value = projected.Result;
                if (!sourceSize.HasValue) { sourceSize = value.SourceSize; profile = value.ProfileId; model = value.ModelId; }
                else if (sourceSize.Value != value.SourceSize || !string.Equals(profile, value.ProfileId, StringComparison.Ordinal) || model != value.ModelId) throw new VisualException(VisualErrorCodes.InputInvalid, "All OBB ROI results must use the same source dimensions and model provenance.");
                foreach (OrientedDetection detection in value.Detections) candidates.Add(new RoiProjectedOrientedDetection(projected.RoiId, projected.Priority, detection, projected.WindowIndex));
            }
            if (!sourceSize.HasValue) throw new ArgumentException("At least one OBB result is required.", nameof(results));
            IReadOnlyList<RoiProjectedOrientedDetection> merged = _inner.Merge(candidates, mode, _iouThreshold, _maximumDetections);
            var canonical = new List<OrientedDetection>(merged.Count);
            for (int index = 0; index < merged.Count; index++)
            {
                OrientedDetection item = merged[index].Detection;
                canonical.Add(new OrientedDetection(index, item.ClassIndex, item.Label, item.Score, item.Quadrilateral, item.AngleRadiansCounterClockwise, item.HasExactRotatedRectangle, item.ExternalId, item.Metadata));
            }
            return new[] { new OrientedDetectionResult(canonical, sourceSize.Value, profile!, model) };
        }
    }

    /// <summary>Adapts topology-aware Pose OKS merging to the generic ROI merger contract. / 将依赖拓扑的 Pose OKS 合并适配到通用 ROI 合并合同。</summary>
    public sealed class PoseRoiResultMergerAdapter : IRoiResultMerger<PoseEstimationResult>
    {
        private readonly PoseTopology _topology;
        private readonly PoseOksOptions? _options;
        private readonly RoiPoseResultMerger _inner = new RoiPoseResultMerger();

        /// <summary>Initializes a Pose merger adapter. / 初始化 Pose 合并适配器。</summary>
        public PoseRoiResultMergerAdapter(PoseTopology topology, PoseOksOptions? options = null)
        {
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _options = options;
        }

        /// <inheritdoc />
        public IReadOnlyList<PoseEstimationResult> Merge(IReadOnlyList<RoiProjectedResult<PoseEstimationResult>> results, RoiResultMergeMode mode)
        {
            var candidates = new List<RoiProjectedPoseInstance>();
            VisualSize? sourceSize = null;
            string? profile = null;
            ModelId model = default(ModelId);
            foreach (RoiProjectedResult<PoseEstimationResult> projected in results ?? throw new ArgumentNullException(nameof(results)))
            {
                if (projected == null) throw new ArgumentException("Pose candidates cannot contain null values.", nameof(results));
                PoseEstimationResult value = projected.Result;
                if (!TopologyEquals(value.Topology, _topology)) throw new VisualException(VisualErrorCodes.InputInvalid, "All Pose ROI results must use the configured topology.");
                if (!sourceSize.HasValue) { sourceSize = value.SourceSize; profile = value.ProfileId; model = value.ModelId; }
                else if (sourceSize.Value != value.SourceSize || !string.Equals(profile, value.ProfileId, StringComparison.Ordinal) || model != value.ModelId) throw new VisualException(VisualErrorCodes.InputInvalid, "All Pose ROI results must use the same source dimensions and model provenance.");
                foreach (PoseInstance instance in value.Instances) candidates.Add(new RoiProjectedPoseInstance(projected.RoiId, projected.Priority, instance, projected.WindowIndex));
            }
            if (!sourceSize.HasValue) throw new ArgumentException("At least one Pose result is required.", nameof(results));
            IReadOnlyList<RoiProjectedPoseInstance> merged = _inner.Merge(candidates, _topology, _options, mode);
            var instances = new List<PoseInstance>(merged.Count);
            for (int index = 0; index < merged.Count; index++)
            {
                PoseInstance item = merged[index].Instance;
                instances.Add(new PoseInstance(index, item.Score, item.Keypoints, item.BoundingBox, item.ClassIndex, item.ExternalId));
            }
            return new[] { new PoseEstimationResult(_topology, instances, sourceSize.Value, profile!, model) };
        }

        private static bool TopologyEquals(PoseTopology left, PoseTopology right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Keypoints.Count != right.Keypoints.Count || left.Edges.Count != right.Edges.Count) return false;
            for (int index = 0; index < left.Keypoints.Count; index++)
            {
                PoseKeypointDefinition first = left.Keypoints[index];
                PoseKeypointDefinition second = right.Keypoints[index];
                if (first.Index != second.Index || !string.Equals(first.Label, second.Label, StringComparison.Ordinal) || first.MirroredIndex != second.MirroredIndex || first.Color != second.Color || first.OksSigma != second.OksSigma) return false;
            }
            for (int index = 0; index < left.Edges.Count; index++)
            {
                PoseSkeletonEdge first = left.Edges[index];
                PoseSkeletonEdge second = right.Edges[index];
                if (first.FirstKeypointIndex != second.FirstKeypointIndex || first.SecondKeypointIndex != second.SecondKeypointIndex || first.Color != second.Color) return false;
            }
            return true;
        }
    }

    /// <summary>Adapts the task-specific OCR merger to the generic ROI merger contract. / 将任务专用 OCR 合并器适配到通用 ROI 合并合同。</summary>
    public sealed class OcrRoiResultMergerAdapter : IRoiResultMerger<OcrResult>
    {
        private readonly RoiOcrResultMerger _inner = new RoiOcrResultMerger();
        private readonly float _polygonIouThreshold;
        private readonly bool _ignoreCase;
        private readonly bool _collapseWhitespace;

        /// <summary>Initializes an OCR merger adapter. / 初始化 OCR 合并适配器。</summary>
        public OcrRoiResultMergerAdapter(float polygonIouThreshold = .5f, bool ignoreCase = true, bool collapseWhitespace = true)
        {
            if (float.IsNaN(polygonIouThreshold) || float.IsInfinity(polygonIouThreshold) || polygonIouThreshold < 0 || polygonIouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(polygonIouThreshold));
            _polygonIouThreshold = polygonIouThreshold;
            _ignoreCase = ignoreCase;
            _collapseWhitespace = collapseWhitespace;
        }

        /// <inheritdoc />
        public IReadOnlyList<OcrResult> Merge(IReadOnlyList<RoiProjectedResult<OcrResult>> results, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use text polygon IoU for OCR results.");
            RoiMergedOcrResult merged = _inner.Merge(results ?? throw new ArgumentNullException(nameof(results)), mode, _polygonIouThreshold, _ignoreCase, _collapseWhitespace);
            return new[] { merged.Result };
        }
    }

    /// <summary>Adapts polygon-IoU text-detection merging to the generic ROI merger contract. / 将多边形 IoU 文本检测合并适配到通用 ROI 合并合同。</summary>
    public sealed class TextDetectionRoiResultMergerAdapter : IRoiResultMerger<TextDetectionResult>
    {
        private readonly float _polygonIouThreshold;
        private readonly int _maximumRegions;

        /// <summary>Initializes a bounded text-detection polygon merger. / 初始化有界文本检测多边形合并器。</summary>
        public TextDetectionRoiResultMergerAdapter(float polygonIouThreshold = .3f, int maximumRegions = 1024)
        {
            if (float.IsNaN(polygonIouThreshold) || float.IsInfinity(polygonIouThreshold) || polygonIouThreshold < 0 || polygonIouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(polygonIouThreshold));
            if (maximumRegions <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRegions));
            _polygonIouThreshold = polygonIouThreshold;
            _maximumRegions = maximumRegions;
        }

        /// <inheritdoc />
        public IReadOnlyList<TextDetectionResult> Merge(IReadOnlyList<RoiProjectedResult<TextDetectionResult>> results, RoiResultMergeMode mode)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiResultMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RoiResultMergeMode.TaskSpecific) throw new NotSupportedException("TaskSpecific text-detection merging requires an application-provided merger.");
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use polygon IoU for text detection.");

            VisualSize? sourceSize = null;
            string? profileId = null;
            ModelId modelId = default(ModelId);
            var candidates = new List<Candidate>();
            foreach (RoiProjectedResult<TextDetectionResult> projected in results)
            {
                if (projected == null) throw new ArgumentException("Text-detection candidates cannot contain null values.", nameof(results));
                TextDetectionResult result = projected.Result;
                if (!sourceSize.HasValue)
                {
                    sourceSize = result.SourceSize;
                    profileId = result.ProfileId;
                    modelId = result.ModelId;
                }
                else if (sourceSize.Value != result.SourceSize || !string.Equals(profileId, result.ProfileId, StringComparison.Ordinal) || modelId != result.ModelId)
                {
                    throw new VisualException(VisualErrorCodes.InputInvalid, "All text-detection ROI results must use the same source dimensions and model provenance.");
                }
                foreach (TextRegion region in result.Regions) candidates.Add(new Candidate(projected, region));
            }
            if (!sourceSize.HasValue) throw new ArgumentException("At least one text-detection result is required.", nameof(results));

            candidates.Sort((left, right) => Compare(left, right, mode));
            var kept = new List<Candidate>(Math.Min(candidates.Count, _maximumRegions));
            foreach (Candidate candidate in candidates)
            {
                if (mode != RoiResultMergeMode.KeepAll)
                {
                    bool overlaps = false;
                    for (int index = 0; index < kept.Count; index++)
                    {
                        float intersection = VisualRoiTextDetectionFilter.CalculatePolygonIntersectionArea(candidate.Region.Polygon.Vertices, kept[index].Geometry);
                        float union = candidate.Region.Polygon.Area + kept[index].Region.Polygon.Area - intersection;
                        if (union > 0 && intersection / union >= _polygonIouThreshold) { overlaps = true; break; }
                    }
                    if (overlaps) continue;
                }
                kept.Add(candidate);
                if (kept.Count >= _maximumRegions) break;
            }
            kept.Sort(CompareReadingOrder);
            var regions = new List<TextRegion>(kept.Count);
            for (int index = 0; index < kept.Count; index++)
            {
                TextRegion region = kept[index].Region;
                regions.Add(new TextRegion(index, region.Score, region.Polygon, region.CropQuadrilateral, region.Orientation, region.AngleRadians, region.Language, region.Script, region.ExternalId, region.Metadata));
            }
            return new[] { new TextDetectionResult(regions, sourceSize.Value, profileId!, modelId) };
        }

        private static int Compare(Candidate left, Candidate right, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.RoiPriority)
            {
                int priority = right.Source.Priority.CompareTo(left.Source.Priority);
                if (priority != 0) return priority;
            }
            int score = right.Region.Score.CompareTo(left.Region.Score);
            if (score != 0) return score;
            int fallback = right.Source.Priority.CompareTo(left.Source.Priority);
            if (fallback != 0) return fallback;
            int roi = string.Compare(left.Source.RoiId, right.Source.RoiId, StringComparison.Ordinal);
            if (roi != 0) return roi;
            return left.Region.SourceIndex.CompareTo(right.Region.SourceIndex);
        }

        private static int CompareReadingOrder(Candidate left, Candidate right)
        {
            RectangleF a = left.Region.AxisAlignedBounds;
            RectangleF b = right.Region.AxisAlignedBounds;
            int y = a.Y.CompareTo(b.Y);
            if (y != 0) return y;
            int x = a.X.CompareTo(b.X);
            if (x != 0) return x;
            int score = right.Region.Score.CompareTo(left.Region.Score);
            if (score != 0) return score;
            int roi = string.Compare(left.Source.RoiId, right.Source.RoiId, StringComparison.Ordinal);
            return roi != 0 ? roi : left.Region.SourceIndex.CompareTo(right.Region.SourceIndex);
        }

        private sealed class Candidate
        {
            internal Candidate(RoiProjectedResult<TextDetectionResult> source, TextRegion region)
            {
                Source = source;
                Region = region;
                Geometry = new PolygonRoiGeometry(region.Polygon.Vertices);
            }
            internal RoiProjectedResult<TextDetectionResult> Source { get; }
            internal TextRegion Region { get; }
            internal PolygonRoiGeometry Geometry { get; }
        }
    }

    /// <summary>Adapts pixel-IoU instance-mask merging to the generic ROI merger contract. / 将实例掩码像素 IoU 合并适配到通用 ROI 合并合同。</summary>
    public sealed class InstanceSegmentationRoiResultMergerAdapter : IRoiResultMerger<InstanceSegmentationResult>
    {
        private readonly RoiInstanceSegmentationResultMerger _inner = new RoiInstanceSegmentationResultMerger();
        private readonly float _iouThreshold;
        private readonly InstanceMaskOverlapMode _overlapMode;

        /// <summary>Initializes an instance-mask merger adapter. / 初始化实例掩码合并适配器。</summary>
        public InstanceSegmentationRoiResultMergerAdapter(float iouThreshold = .5f, InstanceMaskOverlapMode overlapMode = InstanceMaskOverlapMode.Independent)
        {
            if (float.IsNaN(iouThreshold) || float.IsInfinity(iouThreshold) || iouThreshold < 0 || iouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(iouThreshold));
            if (!Enum.IsDefined(typeof(InstanceMaskOverlapMode), overlapMode)) throw new ArgumentOutOfRangeException(nameof(overlapMode));
            _iouThreshold = iouThreshold;
            _overlapMode = overlapMode;
        }

        /// <inheritdoc />
        public IReadOnlyList<InstanceSegmentationResult> Merge(IReadOnlyList<RoiProjectedResult<InstanceSegmentationResult>> results, RoiResultMergeMode mode)
        {
            if (mode == RoiResultMergeMode.WeightedBoxFusion) throw new NotSupportedException("WeightedBoxFusion is defined for axis-aligned detections; use mask IoU for instance segmentation.");
            RoiMergedInstanceSegmentationResult merged = _inner.Merge(results ?? throw new ArgumentNullException(nameof(results)), mode, _iouThreshold, _overlapMode);
            return new[] { merged.Result };
        }
    }
}
