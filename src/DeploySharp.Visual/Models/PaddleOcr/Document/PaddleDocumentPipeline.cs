using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Describes one caller-owned page passed through the PP-Structure orchestration layer. / 描述交给 PP-Structure 编排层的一页调用方数据。</summary>
    public sealed class PaddleDocumentPage
    {
        public PaddleDocumentPage(object source, VisualSize sourceSize, int pageIndex = 0)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(sourceSize));
            if (pageIndex < 0) throw new ArgumentOutOfRangeException(nameof(pageIndex));
            SourceSize = sourceSize;
            PageIndex = pageIndex;
        }

        /// <summary>Gets the caller-owned source, such as an image, prepared input, or decoded page handle. / 获取调用方拥有的源对象。</summary>
        public object Source { get; }
        /// <summary>Gets the source pixel size used by geometry-aware stages. / 获取供几何阶段使用的源像素尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the zero-based page index. / 获取从零开始的页码。</summary>
        public int PageIndex { get; }
    }

    /// <summary>Provides a stage with the page and all preceding results without owning the source or child pipelines. / 向阶段提供页面和前序结果，但不拥有源数据或子 Pipeline。</summary>
    public sealed class PaddleDocumentPipelineContext
    {
        private readonly IReadOnlyList<PaddleDocumentModuleResult> _results;

        internal PaddleDocumentPipelineContext(PaddleDocumentPage page, IReadOnlyList<PaddleDocumentModuleResult> results)
        {
            Page = page ?? throw new ArgumentNullException(nameof(page));
            _results = results ?? throw new ArgumentNullException(nameof(results));
        }

        public PaddleDocumentPage Page { get; }
        public IReadOnlyList<PaddleDocumentModuleResult> Results => _results;

        /// <summary>Gets the latest result for a module, or null when the optional stage has not run. / 获取指定模块最近一次结果。</summary>
        public T? TryGet<T>(PaddleDocumentModule module) where T : PaddleDocumentModuleResult
        {
            for (int index = _results.Count - 1; index >= 0; index--)
            {
                PaddleDocumentModuleResult result = _results[index];
                if (result.Module == module && result is T typed) return typed;
            }
            return null;
        }

        /// <summary>Gets all regions emitted by preceding stages in page coordinates. / 获取前序阶段以页面坐标输出的全部区域。</summary>
        public IReadOnlyList<PaddleDocumentRegion> GetRegions()
        {
            var regions = new List<PaddleDocumentRegion>();
            foreach (PaddleDocumentModuleResult result in _results) regions.AddRange(result.Regions);
            return regions.AsReadOnly();
        }
    }

    /// <summary>Defines one independently executable PP-Structure stage adapter. / 定义一个可独立执行的 PP-Structure 阶段适配器。</summary>
    public interface IPaddleDocumentPipelineStage
    {
        public PaddleDocumentModule Module { get; }
        public Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>Stores wall-clock timing for one stage. / 保存一个阶段的墙钟耗时。</summary>
    public sealed class PaddleDocumentPipelineStageTiming
    {
        internal PaddleDocumentPipelineStageTiming(PaddleDocumentModule module, TimeSpan elapsed)
        {
            Module = module;
            Elapsed = elapsed;
        }

        public PaddleDocumentModule Module { get; }
        public TimeSpan Elapsed { get; }
    }

    /// <summary>Contains one complete page result assembled from the stages that actually ran. / 保存由实际运行阶段组装的一页完整结果。</summary>
    public sealed class PaddleDocumentPipelineResult
    {
        private readonly IReadOnlyList<PaddleDocumentModuleResult> _results;
        private readonly IReadOnlyList<PaddleDocumentPipelineStageTiming> _timings;

        internal PaddleDocumentPipelineResult(PaddleDocumentPage page, IReadOnlyList<PaddleDocumentModuleResult> results, IReadOnlyList<PaddleDocumentPipelineStageTiming> timings, TimeSpan elapsed)
        {
            Page = page;
            _results = results;
            _timings = timings;
            Elapsed = elapsed;
        }

        public PaddleDocumentPage Page { get; }
        public IReadOnlyList<PaddleDocumentModuleResult> Results => _results;
        public IReadOnlyList<PaddleDocumentPipelineStageTiming> Timings => _timings;
        public TimeSpan Elapsed { get; }

        public bool TryGet<T>(PaddleDocumentModule module, out T? result) where T : PaddleDocumentModuleResult
        {
            for (int index = _results.Count - 1; index >= 0; index--)
            {
                if (_results[index].Module == module && _results[index] is T typed)
                {
                    result = typed;
                    return true;
                }
            }
            result = null;
            return false;
        }

        public T GetRequired<T>(PaddleDocumentModule module) where T : PaddleDocumentModuleResult
        {
            if (TryGet(module, out T? result) && result != null) return result;
            throw new KeyNotFoundException("The PP-Structure pipeline did not produce a result for module: " + module);
        }
    }

    /// <summary>Runs caller-owned PP-Structure stage adapters in a deterministic page order. It orchestrates stages but does not claim a backend or model is available. / 按确定页面顺序运行调用方拥有的 PP-Structure 阶段适配器；只负责编排，不伪称后端或模型已可用。</summary>
    public sealed class PaddleDocumentPipeline
    {
        private static readonly IReadOnlyList<PaddleDocumentModule> DefaultOrder = new[]
        {
            PaddleDocumentModule.DocumentOrientation,
            PaddleDocumentModule.TextImageUnwarping,
            PaddleDocumentModule.LayoutDetection,
            PaddleDocumentModule.TableClassification,
            PaddleDocumentModule.TableCellDetection,
            PaddleDocumentModule.TableStructureRecognition,
            PaddleDocumentModule.FormulaRecognition,
            PaddleDocumentModule.SealTextDetection,
            PaddleDocumentModule.ChartParsing
        };

        private readonly IReadOnlyList<IPaddleDocumentPipelineStage> _stages;

        public PaddleDocumentPipeline(IEnumerable<IPaddleDocumentPipelineStage> stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var values = stages.ToArray();
            if (values.Length == 0) throw new ArgumentException("At least one PP-Structure stage is required.", nameof(stages));
            var seen = new HashSet<PaddleDocumentModule>();
            foreach (IPaddleDocumentPipelineStage stage in values)
            {
                if (stage == null) throw new ArgumentException("A PP-Structure stage cannot be null.", nameof(stages));
                if (!Enum.IsDefined(typeof(PaddleDocumentModule), stage.Module) || stage.Module == PaddleDocumentModule.StructurePipeline) throw new ArgumentException("StructurePipeline is an aggregate task and cannot be registered as a child stage.", nameof(stages));
                if (!seen.Add(stage.Module)) throw new ArgumentException("A PP-Structure module can only be registered once: " + stage.Module, nameof(stages));
            }
            _stages = values.OrderBy(stage => OrderOf(stage.Module)).ThenBy(stage => stage.Module).ToArray();
        }

        public IReadOnlyList<IPaddleDocumentPipelineStage> Stages => _stages;

        /// <summary>Runs each selected stage once, passing prior results to later stages and preserving page provenance. / 依次运行所选阶段，将前序结果传给后续阶段并保持页码来源。</summary>
        public PaddleDocumentPipelineResult Run(PaddleDocumentPage page, CancellationToken cancellationToken = default(CancellationToken))
            => RunAsync(page, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Runs pages in caller order and preserves each page's provenance. / 按调用方顺序运行多页并保留每页来源。</summary>
        public IReadOnlyList<PaddleDocumentPipelineResult> RunMany(IEnumerable<PaddleDocumentPage> pages, CancellationToken cancellationToken = default(CancellationToken))
            => RunManyAsync(pages, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Runs pages sequentially so caller-owned stage/session lifetimes remain deterministic. / 顺序运行多页以保持调用方阶段和会话生命周期确定。</summary>
        public async Task<IReadOnlyList<PaddleDocumentPipelineResult>> RunManyAsync(IEnumerable<PaddleDocumentPage> pages, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            var results = new List<PaddleDocumentPipelineResult>();
            foreach (PaddleDocumentPage page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (page == null) throw new ArgumentException("A document page cannot be null.", nameof(pages));
                results.Add(await RunAsync(page, cancellationToken).ConfigureAwait(false));
            }
            return results.AsReadOnly();
        }

        public async Task<PaddleDocumentPipelineResult> RunAsync(PaddleDocumentPage page, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            var results = new List<PaddleDocumentModuleResult>(_stages.Count);
            var timings = new List<PaddleDocumentPipelineStageTiming>(_stages.Count);
            Stopwatch total = Stopwatch.StartNew();
            foreach (IPaddleDocumentPipelineStage stage in _stages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new PaddleDocumentPipelineContext(page, results.AsReadOnly());
                Stopwatch watch = Stopwatch.StartNew();
                PaddleDocumentModuleResult result = await stage.RunAsync(context, cancellationToken).ConfigureAwait(false);
                watch.Stop();
                if (result == null) throw new InvalidOperationException("PP-Structure stage returned null: " + stage.Module);
                if (result.Module != stage.Module) throw new InvalidOperationException("PP-Structure stage returned a mismatched module. expected=" + stage.Module + ";actual=" + result.Module);
                if (result.Metadata.PageIndex != page.PageIndex) throw new InvalidOperationException("PP-Structure stage returned mismatched page provenance. expected=" + page.PageIndex + ";actual=" + result.Metadata.PageIndex);
                results.Add(result);
                timings.Add(new PaddleDocumentPipelineStageTiming(stage.Module, watch.Elapsed));
                cancellationToken.ThrowIfCancellationRequested();
            }
            total.Stop();
            return new PaddleDocumentPipelineResult(page, results.AsReadOnly(), timings.AsReadOnly(), total.Elapsed);
        }

        private static int OrderOf(PaddleDocumentModule module)
        {
            for (int index = 0; index < DefaultOrder.Count; index++) if (DefaultOrder[index] == module) return index;
            return int.MaxValue;
        }
    }

    /// <summary>Adapts an asynchronous delegate to a PP-Structure stage without forcing an image-library dependency into the core package. / 将异步委托适配为 PP-Structure 阶段且不把图像库依赖引入核心包。</summary>
    public sealed class PaddleDocumentPipelineStage : IPaddleDocumentPipelineStage
    {
        private readonly Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleDocumentModuleResult>> _handler;

        public PaddleDocumentPipelineStage(PaddleDocumentModule module, Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleDocumentModuleResult>> handler)
        {
            if (!Enum.IsDefined(typeof(PaddleDocumentModule), module) || module == PaddleDocumentModule.StructurePipeline) throw new ArgumentOutOfRangeException(nameof(module));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Module = module;
        }

        public PaddleDocumentModule Module { get; }
        public Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken)) => _handler(context ?? throw new ArgumentNullException(nameof(context)), cancellationToken);
    }
}

#pragma warning restore CS1591
