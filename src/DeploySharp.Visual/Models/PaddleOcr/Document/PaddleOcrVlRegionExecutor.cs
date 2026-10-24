#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    internal static class PaddleOcrVlRegionExecutor
    {
        internal static async Task<PaddleDocumentVisionLanguageResult> RunAsync(
            PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTask task,
            PaddleDocumentPipelineContext context, PaddleDocumentRegion region, int index,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRegion(region, context.Page.SourceSize, index);
            PaddleOcrVlPreparedInput? prepared = await prepare(context, region, index, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (prepared == null) throw new InvalidOperationException("The PaddleOCR-VL region preparation callback returned null.");
            if (prepared.ImagePlan.Task != task)
                throw new InvalidOperationException("The prepared PaddleOCR-VL task does not match the region stage.");
            PaddleOcrVlGenerationResult generation;
            bool usedFallback = false;
            try
            {
                generation = await generate(context, region, index, prepared, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (generation == null) throw new InvalidOperationException("The PaddleOCR-VL region generation callback returned null.");
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && fallback != null)
            {
                generation = await fallback(context, region, index, exception, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (generation == null) throw new InvalidOperationException("The PaddleOCR-VL region fallback callback returned null.");
                usedFallback = true;
            }
            if (generation.Task != task)
                throw new InvalidOperationException("The PaddleOCR-VL generation task does not match the region stage. expected=" + task + ";actual=" + generation.Task);
            if (prepared.SourceSha256 != null && generation.SourceSha256 != null
                && !string.Equals(prepared.SourceSha256, generation.SourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The PaddleOCR-VL generation source does not match the prepared region.");
            string? inputSha = inputIdentity?.Invoke(context, region, index, prepared) ?? prepared.SourceSha256 ?? generation.SourceSha256;
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(inputSha)) throw new InvalidOperationException("PaddleOCR-VL region results require a source/input SHA-256.");
            var warnings = new List<string> { "vl-regional-output-no-coordinates" };
            if (generation.FinishReason == GenerationFinishReason.MaxTokens) warnings.Add("missing-eos:sequence-may-be-truncated");
            if (usedFallback) warnings.Add("vl-region-fallback-used");
            var metadata = new PaddleDocumentResultMetadata(model, backend, generation.Elapsed, inputSha, context.Page.PageIndex);
            return new PaddleDocumentVisionLanguageResult(task, metadata, generation.Text, generation.TokenIds, generation.FinishReason,
                warnings, regionIndex: index, sourceRegion: region);
        }

        private static void ValidateRegion(PaddleDocumentRegion region, VisualSize sourceSize, int index)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (float.IsNaN(region.Bounds.X) || float.IsInfinity(region.Bounds.X) || float.IsNaN(region.Bounds.Y) || float.IsInfinity(region.Bounds.Y)
                || float.IsNaN(region.Bounds.Width) || float.IsInfinity(region.Bounds.Width) || float.IsNaN(region.Bounds.Height) || float.IsInfinity(region.Bounds.Height)
                || region.Bounds.Width <= 0 || region.Bounds.Height <= 0)
                throw new InvalidOperationException("The layout region is not a finite positive rectangle. index=" + index);
            if (region.Bounds.Right <= 0 || region.Bounds.Bottom <= 0 || region.Bounds.X >= sourceSize.Width || region.Bounds.Y >= sourceSize.Height)
                throw new InvalidOperationException("The layout region does not intersect the page. index=" + index);
        }
    }
}
#endif
