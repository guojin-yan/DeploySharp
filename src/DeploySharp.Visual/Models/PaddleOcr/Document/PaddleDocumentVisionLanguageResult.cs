#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Stores one raw PaddleOCR-VL task result in page provenance without pretending that the VL text contains coordinates. / 保存一项带页面来源的 PaddleOCR-VL 原始任务结果，不把 VL 文本冒充为坐标结果。</summary>
    public sealed class PaddleDocumentVisionLanguageResult : PaddleDocumentModuleResult
    {
        /// <summary>Initializes one immutable raw VL result. / 初始化一个不可变的 VL 原始结果。</summary>
        public PaddleDocumentVisionLanguageResult(
            PaddleOcrVlTask task,
            PaddleDocumentResultMetadata metadata,
            string text,
            IEnumerable<int> tokenIds,
            GenerationFinishReason finishReason,
            IEnumerable<string>? warnings = null,
            int regionIndex = -1,
            PaddleDocumentRegion? sourceRegion = null)
            : base(ModuleForTask(task), metadata,
                regions: sourceRegion == null ? null : new[] { sourceRegion },
                warnings: warnings == null ? null : new List<string>(warnings).AsReadOnly())
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            if (!Enum.IsDefined(typeof(GenerationFinishReason), finishReason)) throw new ArgumentOutOfRangeException(nameof(finishReason));
            if (regionIndex < -1) throw new ArgumentOutOfRangeException(nameof(regionIndex));
            if (regionIndex >= 0 && sourceRegion == null) throw new ArgumentException("A source region is required when a region index is supplied.", nameof(sourceRegion));
            if (metadata.Model.Module != Module) throw new ArgumentException("The VL result model module does not match the requested task.", nameof(metadata));
            var copied = tokenIds.ToList();
            if (copied.Any(value => value < 0)) throw new ArgumentException("Generated token IDs cannot be negative.", nameof(tokenIds));
            Task = task;
            Text = text;
            TokenIds = copied.AsReadOnly();
            FinishReason = finishReason;
            RegionIndex = regionIndex;
            SourceRegion = sourceRegion;
        }

        /// <summary>Gets the original PaddleOCR-VL task. / 获取原始 PaddleOCR-VL 任务。</summary>
        public PaddleOcrVlTask Task { get; }
        /// <summary>Gets the raw generated text, including task-specific markup. / 获取包含任务标记的原始生成文本。</summary>
        public string Text { get; }
        /// <summary>Gets generated token IDs, including EOS when emitted. / 获取生成 Token ID，输出 EOS 时包含 EOS。</summary>
        public IReadOnlyList<int> TokenIds { get; }
        /// <summary>Gets the explicit generation termination reason. / 获取明确的生成结束原因。</summary>
        public GenerationFinishReason FinishReason { get; }
        /// <summary>Gets the zero-based page-region index, or -1 for an unrestricted single-image result. / 获取从零开始的页面区域索引；非区域单图结果为 -1。</summary>
        public int RegionIndex { get; }
        /// <summary>Gets the source page region used to prepare this result, when it was routed from layout detection. / 获取用于准备本结果的页面源区域；若未经过版面区域路由则为空。</summary>
        public PaddleDocumentRegion? SourceRegion { get; }
        /// <summary>Gets whether this result contains model-returned spatial coordinates. This is false for the current raw VL six-task output. / 获取结果是否包含模型返回的空间坐标；当前 VL 六类原始输出均为 false。</summary>
        public bool CoordinatesAvailable => false;
        /// <summary>Gets the stable explanation for the absent coordinates. / 获取没有坐标的稳定说明。</summary>
        public string SpatialOutputStatus => "not-returned-by-paddleocr-vl-task-output";

        /// <summary>Maps a VL task to the existing document module contract. / 将 VL 任务映射到现有文档模块合同。</summary>
        public static PaddleDocumentModule ModuleForTask(PaddleOcrVlTask task)
        {
            switch (task)
            {
                case PaddleOcrVlTask.TextRecognition:
                case PaddleOcrVlTask.TextSpotting:
                    return PaddleDocumentModule.TextRecognition;
                case PaddleOcrVlTask.TableRecognition:
                    return PaddleDocumentModule.TableStructureRecognition;
                case PaddleOcrVlTask.FormulaRecognition:
                    return PaddleDocumentModule.FormulaRecognition;
                case PaddleOcrVlTask.ChartRecognition:
                    return PaddleDocumentModule.ChartParsing;
                case PaddleOcrVlTask.SealRecognition:
                    return PaddleDocumentModule.SealTextDetection;
                default:
                    throw new ArgumentOutOfRangeException(nameof(task));
            }
        }
    }

    /// <summary>Describes one routed VL region, including an isolated failure when configured to continue. / 描述一个路由后的 VL 区域，并在允许继续时保留隔离失败。</summary>
    public sealed class PaddleDocumentVisionLanguageRegionExecution
    {
        /// <summary>Creates a successful routed region execution. / 创建成功的区域执行记录。</summary>
        public PaddleDocumentVisionLanguageRegionExecution(int regionIndex, PaddleDocumentRegion sourceRegion, PaddleDocumentVisionLanguageResult result)
        {
            if (regionIndex < 0) throw new ArgumentOutOfRangeException(nameof(regionIndex));
            SourceRegion = sourceRegion ?? throw new ArgumentNullException(nameof(sourceRegion));
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (result.RegionIndex != regionIndex) throw new ArgumentException("The VL result region index does not match the execution record.", nameof(result));
            if (result.SourceRegion != sourceRegion) throw new ArgumentException("The VL result source region does not match the execution record.", nameof(result));
            RegionIndex = regionIndex;
        }

        /// <summary>Creates an isolated failed region execution. / 创建一个被隔离的失败区域执行记录。</summary>
        public PaddleDocumentVisionLanguageRegionExecution(int regionIndex, PaddleDocumentRegion sourceRegion, string failureType, string failureMessage)
        {
            if (regionIndex < 0) throw new ArgumentOutOfRangeException(nameof(regionIndex));
            SourceRegion = sourceRegion ?? throw new ArgumentNullException(nameof(sourceRegion));
            if (string.IsNullOrWhiteSpace(failureType)) throw new ArgumentException("A failure type is required.", nameof(failureType));
            if (string.IsNullOrWhiteSpace(failureMessage)) throw new ArgumentException("A failure message is required.", nameof(failureMessage));
            RegionIndex = regionIndex;
            FailureType = failureType;
            FailureMessage = failureMessage;
        }

        /// <summary>Gets the stable page-region index. / 获取稳定的页面区域索引。</summary>
        public int RegionIndex { get; }
        /// <summary>Gets the original page-space region. / 获取原始页面坐标区域。</summary>
        public PaddleDocumentRegion SourceRegion { get; }
        /// <summary>Gets the successful raw VL result, or null when the region failed. / 获取成功的 VL 原始结果；失败时为空。</summary>
        public PaddleDocumentVisionLanguageResult? Result { get; }
        /// <summary>Gets the exception type recorded for an isolated failure. / 获取被隔离失败记录的异常类型。</summary>
        public string? FailureType { get; }
        /// <summary>Gets the diagnostic message recorded for an isolated failure. / 获取被隔离失败记录的诊断消息。</summary>
        public string? FailureMessage { get; }
        /// <summary>Gets whether this region completed successfully. / 获取该区域是否成功完成。</summary>
        public bool Succeeded => Result != null;
    }

    /// <summary>Aggregates VL outputs routed from one page's layout regions without inventing model-returned coordinates. / 聚合一页版面区域路由后的 VL 输出，不虚构模型返回的坐标。</summary>
    public sealed class PaddleDocumentVisionLanguagePageResult : PaddleDocumentModuleResult
    {
        /// <summary>Initializes an ordered page-level VL region result. / 初始化有序的页面级 VL 区域结果。</summary>
        public PaddleDocumentVisionLanguagePageResult(
            PaddleOcrVlTask task,
            PaddleDocumentResultMetadata metadata,
            IReadOnlyList<PaddleDocumentVisionLanguageRegionExecution> executions,
            IReadOnlyList<string>? warnings = null)
            : base(PaddleDocumentVisionLanguageResult.ModuleForTask(task), metadata, RegionsFor(executions), warnings)
        {
            if (executions == null) throw new ArgumentNullException(nameof(executions));
            if (!Enum.IsDefined(typeof(PaddleOcrVlTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            if (metadata.Model.Module != Module) throw new ArgumentException("The VL page result model module does not match the requested task.", nameof(metadata));
            var copied = new List<PaddleDocumentVisionLanguageRegionExecution>(executions.Count);
            for (int index = 0; index < executions.Count; index++)
            {
                PaddleDocumentVisionLanguageRegionExecution execution = executions[index] ?? throw new ArgumentException("A region execution cannot be null.", nameof(executions));
                if (execution.RegionIndex != index) throw new ArgumentException("Region executions must be ordered and indexed from zero.", nameof(executions));
                if (execution.Result != null)
                {
                    if (execution.Result.Task != task) throw new ArgumentException("A routed VL result task does not match the page result task.", nameof(executions));
                    if (execution.Result.Metadata.PageIndex != metadata.PageIndex) throw new ArgumentException("A routed VL result page index does not match the page result.", nameof(executions));
                    if (execution.Result.Metadata.Model.ModelId != metadata.Model.ModelId || execution.Result.Metadata.Backend != metadata.Backend)
                        throw new ArgumentException("A routed VL result provenance does not match the page result.", nameof(executions));
                }
                copied.Add(execution);
            }
            Task = task;
            Executions = copied.AsReadOnly();
            SucceededCount = copied.Count(value => value.Succeeded);
            FailedCount = copied.Count - SucceededCount;
        }

        /// <summary>Gets the routed task. / 获取路由任务。</summary>
        public PaddleOcrVlTask Task { get; }
        /// <summary>Gets ordered region executions, including isolated failures. / 获取按顺序排列的区域执行记录（含隔离失败）。</summary>
        public IReadOnlyList<PaddleDocumentVisionLanguageRegionExecution> Executions { get; }
        /// <summary>Gets the number of successful regions. / 获取成功区域数。</summary>
        public int SucceededCount { get; }
        /// <summary>Gets the number of failed regions. / 获取失败区域数。</summary>
        public int FailedCount { get; }
        /// <summary>Gets whether model-returned coordinates exist. Source regions are page provenance, not VL coordinates. / 获取模型是否返回坐标；源区域只是页面来源，不是 VL 模型坐标。</summary>
        public bool CoordinatesAvailable => false;
        /// <summary>Gets the stable spatial-output boundary. / 获取稳定的空间输出边界说明。</summary>
        public string SpatialOutputStatus => "source-regions-only;not-returned-by-paddleocr-vl-task-output";

        private static IReadOnlyList<PaddleDocumentRegion> RegionsFor(IReadOnlyList<PaddleDocumentVisionLanguageRegionExecution> executions)
        {
            if (executions == null) throw new ArgumentNullException(nameof(executions));
            var regions = new List<PaddleDocumentRegion>(executions.Count);
            for (int index = 0; index < executions.Count; index++)
            {
                PaddleDocumentVisionLanguageRegionExecution execution = executions[index] ?? throw new ArgumentException("A region execution cannot be null.", nameof(executions));
                if (execution.RegionIndex != index) throw new ArgumentException("Region executions must be ordered and indexed from zero.", nameof(executions));
                regions.Add(execution.SourceRegion);
            }
            return regions.AsReadOnly();
        }
    }
}
#endif
