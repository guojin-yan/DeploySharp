using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Provides strongly typed sliding-window entry points for common Visual tasks. / 为常见 Visual 任务提供强类型滑窗入口。</summary>
    public static class VisualRoiTaskSlidingWindowExtensions
    {
        /// <summary>Runs all enabled Detection sliding-window ROIs and applies axis-aligned NMS or WBF. / 运行所有启用的 Detection 滑窗 ROI，并应用轴对齐 NMS 或 WBF。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<DetectionResult>> RunDetectionRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<DetectionResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, DetectionResult> decode,
            DetectionRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.ClassAwareNms,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceDetectionRoiProjector(), merger ?? new DetectionRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled OCR sliding-window ROIs and merges text polygons deterministically. / 运行所有启用的 OCR 滑窗 ROI，并确定性合并文本多边形。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<OcrResult>> RunOcrRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<OcrResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, OcrResult> decode,
            OcrRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.HighestConfidence,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceOcrRoiProjector(), merger ?? new OcrRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs text-detection sliding-window ROIs and merges polygon candidates by deterministic polygon IoU. / 运行文本检测滑窗 ROI，并按确定性多边形 IoU 合并候选。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<TextDetectionResult>> RunTextDetectionRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<TextDetectionResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, TextDetectionResult> decode,
            TextDetectionRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.ClassAgnosticNms,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceTextDetectionRoiProjector(), merger ?? new TextDetectionRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled semantic-segmentation sliding-window ROIs and fuses source-resolution labels or probabilities. / 运行所有启用的语义分割滑窗 ROI，并融合源分辨率标签或概率图。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<SemanticSegmentationResult>> RunSemanticSegmentationRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<SemanticSegmentationResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, SemanticSegmentationResult> decode,
            int backgroundClassIndex,
            SemanticSegmentationRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.RoiPriority,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            if (backgroundClassIndex < 0) throw new ArgumentOutOfRangeException(nameof(backgroundClassIndex));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceSemanticSegmentationRoiProjector(backgroundClassIndex), merger ?? new SemanticSegmentationRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled classification sliding-window ROIs and selects a deterministic business result. / 运行所有启用的分类滑窗 ROI，并确定性选择业务结果。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<ClassificationResult>> RunClassificationRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<ClassificationResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, ClassificationResult> decode,
            ClassificationRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.HighestConfidence,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new IdentityRoiResultProjector<ClassificationResult>(), merger ?? new ClassificationRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled oriented-detection sliding-window ROIs and merges quadrilaterals with rotated IoU. / 运行所有启用的旋转框滑窗 ROI，并使用旋转 IoU 合并四边形。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<OrientedDetectionResult>> RunOrientedDetectionRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<OrientedDetectionResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, OrientedDetectionResult> decode,
            OrientedDetectionRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.ClassAwareNms,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceOrientedDetectionRoiProjector(), merger ?? new OrientedDetectionRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled Pose sliding-window ROIs and merges instances with topology-aware OKS. / 运行所有启用的 Pose 滑窗 ROI，并使用拓扑感知 OKS 合并实例。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<PoseEstimationResult>> RunPoseRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<PoseEstimationResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, PoseEstimationResult> decode,
            PoseTopology topology,
            PoseRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.TaskSpecific,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpacePoseRoiProjector(), merger ?? new PoseRoiResultMergerAdapter(topology), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled instance-segmentation sliding-window ROIs and merges masks with mask IoU. / 运行所有启用的实例分割滑窗 ROI，并使用掩码 IoU 合并掩码。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<InstanceSegmentationResult>> RunInstanceSegmentationRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<InstanceSegmentationResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, InstanceSegmentationResult> decode,
            InstanceSegmentationRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.ClassAwareNms,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceInstanceSegmentationRoiProjector(), merger ?? new InstanceSegmentationRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled anomaly sliding-window ROIs and composes source-resolution maps. / 运行所有启用的异常检测滑窗 ROI，并合成源分辨率图。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<AnomalyDetectionResult>> RunAnomalyRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<AnomalyDetectionResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, AnomalyDetectionResult> decode,
            AnomalyRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.RoiPriority,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceAnomalyRoiProjector(), merger ?? new AnomalyRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }

        /// <summary>Runs all enabled RMBG Alpha sliding-window ROIs and composes source-resolution alpha. / 运行所有启用的 RMBG Alpha 滑窗 ROI，并合成源分辨率 Alpha。</summary>
        public static Task<VisualRoiSlidingWindowMergedResult<BackgroundRemovalResult>> RunAlphaRoisAndMergeAsync(
            this VisualRoiSlidingWindowRunner<BackgroundRemovalResult> runner,
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            Func<VisualInferenceResult, BackgroundRemovalResult> decode,
            AlphaRoiResultMergerAdapter? merger = null,
            VisualExecutionOptions? executionOptions = null,
            RoiResultMergeMode mergeMode = RoiResultMergeMode.RoiPriority,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken),
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (decode == null) throw new ArgumentNullException(nameof(decode));
            return runner.RunRoisAndMergeAsync(snapshot, options, prepareAsync, decode, new ModelSpaceAlphaRoiProjector(), merger ?? new AlphaRoiResultMergerAdapter(), executionOptions, mergeMode, maximumRois, cancellationToken, coordinateContext);
        }
    }
}
