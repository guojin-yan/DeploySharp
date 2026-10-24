#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Adapts one PaddleOCR-VL task to the page/multi-page PP-Structure pipeline. / 将一项 PaddleOCR-VL 任务适配到页面和多页 PP-Structure Pipeline。</summary>
    /// <remarks>The stage preserves raw text, Token IDs, EOS/truncation and page provenance. Table tasks also expose validated logical OTSL cells/HTML; no pixel bounds, polygons or spotting coordinates are invented. / 阶段保留原文、Token、EOS/截断和页面来源；表格任务另提供经验证的 OTSL 逻辑单元格/HTML，不虚构像素框、Polygon 或 Spotting 坐标。</remarks>
    public sealed class PaddleOcrVlDocumentStage : IPaddleDocumentPipelineStage
    {
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;
        private readonly PaddleOcrVlTask _task;
        private readonly Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleOcrVlPreparedInput>> _prepare;
        private readonly Func<PaddleDocumentPipelineContext, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> _generate;
        private readonly Func<PaddleDocumentPipelineContext, PaddleOcrVlPreparedInput, string>? _inputIdentity;

        /// <summary>Creates a VL stage around caller-owned preparation and generation delegates. / 使用调用方拥有的预处理和生成委托创建 VL 阶段。</summary>
        public PaddleOcrVlDocumentStage(
            PaddleDocumentModelDescriptor model,
            string backend,
            PaddleOcrVlTask task,
            Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            Func<PaddleDocumentPipelineContext, PaddleOcrVlPreparedInput, string>? inputIdentity = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            if (!Enum.IsDefined(typeof(PaddleOcrVlTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            if (model.Module != PaddleDocumentVisionLanguageResult.ModuleForTask(task))
                throw new ArgumentException("The model module does not match the PaddleOCR-VL task.", nameof(model));
            _backend = backend;
            _task = task;
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            _generate = generate ?? throw new ArgumentNullException(nameof(generate));
            _inputIdentity = inputIdentity;
            Module = PaddleDocumentVisionLanguageResult.ModuleForTask(task);
        }

        /// <summary>Gets the existing document module used by this task. / 获取本任务使用的现有文档模块。</summary>
        public PaddleDocumentModule Module { get; }

        /// <summary>Creates a stage using the production four-graph VL Session and exact tokenizer. / 使用生产四图 VL Session 和精确 Tokenizer 创建阶段。</summary>
        public static PaddleOcrVlDocumentStage FromSession(
            PaddleDocumentModelDescriptor model,
            string backend,
            PaddleOcrVlTask task,
            PaddleOcrVlOnnxSession session,
            PaddleOcrVlTokenizer tokenizer,
            Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleOcrVlPreparedInput, string>? inputIdentity = null,
            PaddleOcrVlGenerationOptions? generationOptions = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions options = generationOptions ?? new PaddleOcrVlGenerationOptions();
            return new PaddleOcrVlDocumentStage(model, backend, task, prepare,
                (context, input, cancellationToken) => session.GenerateAsync(input, tokenizer, options, cancellationToken), inputIdentity);
        }

        /// <summary>Runs preparation, generation and page-result mapping exactly once. / 执行一次预处理、生成和页面结果映射。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch watch = Stopwatch.StartNew();
            PaddleOcrVlPreparedInput? input = await _prepare(context, cancellationToken).ConfigureAwait(false);
            if (input == null) throw new InvalidOperationException("The PaddleOCR-VL document preparation callback returned null.");
            PaddleOcrVlGenerationResult generation = await _generate(context, input, cancellationToken).ConfigureAwait(false);
            if (generation == null) throw new InvalidOperationException("The PaddleOCR-VL document generation callback returned null.");
            if (generation.Task != _task)
                throw new InvalidOperationException("The PaddleOCR-VL generation task does not match the document stage. expected=" + _task + ";actual=" + generation.Task);
            string? inputSha = _inputIdentity?.Invoke(context, input) ?? input.SourceSha256;
            if (string.IsNullOrWhiteSpace(inputSha))
                throw new InvalidOperationException("PaddleOCR-VL document results require a source/input SHA-256. Provide inputIdentity or set PreparedInput.SourceSha256.");
            watch.Stop();
            var warnings = new List<string> { "vl-raw-output-no-coordinates" };
            if (generation.FinishReason == GenerationFinishReason.MaxTokens)
                warnings.Add("missing-eos:sequence-may-be-truncated");
            return new PaddleDocumentVisionLanguageResult(_task,
                new PaddleDocumentResultMetadata(_model, _backend, watch.Elapsed, inputSha, context.Page.PageIndex),
                generation.Text, generation.TokenIds, generation.FinishReason, warnings);
        }
    }
}
#endif
