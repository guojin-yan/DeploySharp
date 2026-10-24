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
            IEnumerable<string>? warnings = null)
            : base(ModuleForTask(task), metadata, warnings: warnings == null ? null : new List<string>(warnings).AsReadOnly())
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (tokenIds == null) throw new ArgumentNullException(nameof(tokenIds));
            if (!Enum.IsDefined(typeof(GenerationFinishReason), finishReason)) throw new ArgumentOutOfRangeException(nameof(finishReason));
            if (metadata.Model.Module != Module) throw new ArgumentException("The VL result model module does not match the requested task.", nameof(metadata));
            var copied = tokenIds.ToList();
            if (copied.Any(value => value < 0)) throw new ArgumentException("Generated token IDs cannot be negative.", nameof(tokenIds));
            Task = task;
            Text = text;
            TokenIds = copied.AsReadOnly();
            FinishReason = finishReason;
        }

        /// <summary>Gets the original PaddleOCR-VL task. / 获取原始 PaddleOCR-VL 任务。</summary>
        public PaddleOcrVlTask Task { get; }
        /// <summary>Gets the raw generated text, including task-specific markup. / 获取包含任务标记的原始生成文本。</summary>
        public string Text { get; }
        /// <summary>Gets generated token IDs, including EOS when emitted. / 获取生成 Token ID，输出 EOS 时包含 EOS。</summary>
        public IReadOnlyList<int> TokenIds { get; }
        /// <summary>Gets the explicit generation termination reason. / 获取明确的生成结束原因。</summary>
        public GenerationFinishReason FinishReason { get; }
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
}
#endif
