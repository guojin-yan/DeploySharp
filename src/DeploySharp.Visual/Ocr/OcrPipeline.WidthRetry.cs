using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    public sealed partial class OcrPipeline
    {
        private async Task<RetryWork> RunWidthRetryAsync(IOcrImageInput input, List<OcrRegionResult> results, long initialBytes,
            OcrExecutionOptions execution, CtcConfidenceAggregation aggregation, CancellationToken token, CropWorkBudget? cropBudget)
        {
            var watch = Stopwatch.StartNew();
            var work = new RetryWork();
            OcrWidthRetryOptions options = _cropProfile.WidthRetry!;
            TextCropProfile candidateProfile = _cropProfile.ForWidthRetryCandidate(options.CandidateMaximumWidth);
            var budget = new RetryBudget(initialBytes, _options.MaximumResultBytes);
            var requests = new List<IndexedRequest>();
            var plans = new List<WidthPlan>();
            int admitted = 0;
            for (int position = 0; position < results.Count; position++)
            {
                token.ThrowIfCancellationRequested();
                OcrRegionResult original = results[position];
                if (!options.NeedsRetry(original.Recognition)) continue;
                budget.Reserve(256);
                if (admitted++ >= options.MaximumRegionsPerImage)
                {
                    results[position] = original.WithWidthRetry(new OcrWidthRetryResult(options, new OcrWidthRetryAttempt(original), null, OcrWidthRetryDecision.RegionLimit));
                    continue;
                }
                IReadOnlyList<OcrRecognitionWindow> windows = OcrRecognitionWindowPlanner.Plan(original.Region, candidateProfile, token);
                if (requests.Count + windows.Count > options.MaximumCropsPerImage ||
                    (candidateProfile.OverflowMode == RecognitionOverflowMode.SlidingWindow && requests.Count + windows.Count > candidateProfile.RecognitionWindows.MaximumWindowsPerImage))
                    throw Limit("Wider OCR retry windows exceed their image limit.", OcrPipelineStage.Recognition, regionIndex: original.Region.SourceIndex);
                plans.Add(new WidthPlan(position, original, windows, requests.Count));
                foreach (OcrRecognitionWindow window in windows) requests.Add(new IndexedRequest(requests.Count, window.Crop));
            }

            if (requests.Count != 0)
            {
                List<OcrBatchDescriptor> batches = CreateBatches(requests, RecognitionSelection.Profile.Input.MinimumBatch,
                    EffectiveMaximumBatch(RecognitionSelection), _options.MaximumRecognitionPaddingRatio, token);
                int physical = 0;
                foreach (OcrBatchDescriptor batch in batches) physical = checked(physical + batch.Crops.Count);
                if (physical > options.MaximumCropsPerImage) throw Limit("Wider OCR retry physical rows including padding exceed their image limit.", OcrPipelineStage.Recognition);
                if (cropBudget != null) budget.Reserve(cropBudget.Reserve(batches));
                BatchExecution<VisualInferenceResult>[] executed = await RunBatchesAsync(input, RecognitionSelection.Profile.Input.Name, batches,
                    _recognizer.MaximumConcurrency, async (prepared, cancellation) =>
                    {
                        budget.Reserve(0);
                        VisualInferenceResult inference = await _recognizer.RunAsync(prepared, new VisualExecutionOptions(correlationId: execution.CorrelationId), cancellation).ConfigureAwait(false);
                        long bytes = 0;
                        foreach (RecognizedText row in inference.GetValue<TextRecognitionBatchResult>().Items) bytes = checked(bytes + RetryTextBytes(row));
                        budget.Reserve(bytes);
                        return inference;
                    }, token).ConfigureAwait(false);
                var texts = new RecognizedText[requests.Count];
                var widths = new OcrRecognitionWidthInfo[requests.Count];
                OcrCropDiagnostics[]? diagnostics = cropBudget == null ? null : new OcrCropDiagnostics[requests.Count];
                foreach (BatchExecution<VisualInferenceResult> batch in executed)
                {
                    work.Preparation += batch.Preparation;
                    work.Inference += batch.Result.Timing.Inference;
                    work.Postprocessing += batch.Result.Timing.Postprocessing;
                    work.BatchCount++;
                    TextRecognitionBatchResult decoded = batch.Result.GetValue<TextRecognitionBatchResult>();
                    if (decoded.Items.Count != batch.Batch.Requests.Count) throw Failure("Wider retry recognizer result count mismatch.", OcrPipelineStage.Recognition);
                    for (int index = 0; index < batch.Batch.ActualCount; index++)
                    {
                        IndexedRequest request = batch.Batch.Requests[index];
                        texts[request.Position] = decoded.Items[index].WithSourceRegionIndex(request.Request.Region.SourceIndex);
                        widths[request.Position] = batch.Batch.Crops[index].WidthInfo;
                        if (diagnostics != null) diagnostics[request.Position] = batch.Diagnostics[index];
                    }
                }

                foreach (WidthPlan plan in plans)
                {
                    token.ThrowIfCancellationRequested();
                    OcrRegionResult candidate;
                    if (plan.Windows.Count == 1)
                        candidate = new OcrRegionResult(plan.Original.Region, texts[plan.Offset], widths[plan.Offset], Array.Empty<OcrRecognitionWindowResult>(), plan.Original.Geometry);
                    else
                    {
                        var windows = new List<OcrRecognitionWindowResult>(plan.Windows.Count);
                        long mergedBytes = 0;
                        for (int index = 0; index < plan.Windows.Count; index++)
                        {
                            RecognizedText text = texts[plan.Offset + index];
                            mergedBytes = checked(mergedBytes + RetryTextBytes(text));
                            windows.Add(new OcrRecognitionWindowResult(plan.Windows[index], text, widths[plan.Offset + index]));
                        }
                        budget.Reserve(mergedBytes);
                        candidate = OcrRecognitionWindowMerger.Merge(plan.Original.Region, candidateProfile, windows, aggregation, token);
                        if (plan.Original.Geometry != null) candidate = candidate.WithGeometry(plan.Original.Geometry);
                    }
                    budget.Reserve(128L + plan.Windows.Count * 48L);
                    if (diagnostics != null) candidate = candidate.WithCropDiagnostics(CropEvidenceRange(diagnostics, plan.Offset, plan.Windows.Count));
                    bool selected = options.Prefer(candidate.Recognition, plan.Original.Recognition);
                    OcrWidthRetryDecision decision = selected ? OcrWidthRetryDecision.CandidateSelected :
                        options.SelectionPolicy == OcrWidthRetrySelectionPolicy.PreserveOriginal ? OcrWidthRetryDecision.PreservedByPolicy : OcrWidthRetryDecision.InsufficientGain;
                    OcrRegionResult chosen = selected ? candidate : plan.Original;
                    if (selected && plan.Original.OrientationRetry != null) chosen = chosen.WithOrientationRetry(plan.Original.OrientationRetry);
                    results[plan.Position] = chosen.WithWidthRetry(new OcrWidthRetryResult(options,
                        new OcrWidthRetryAttempt(plan.Original), new OcrWidthRetryAttempt(candidate), decision));
                }
            }

            token.ThrowIfCancellationRequested();
            watch.Stop();
            work.Elapsed = watch.Elapsed;
            work.RetainedBytes = budget.Used;
            return work;
        }

        private sealed class WidthPlan
        {
            internal WidthPlan(int position, OcrRegionResult original, IReadOnlyList<OcrRecognitionWindow> windows, int offset)
            { Position = position; Original = original; Windows = windows; Offset = offset; }
            internal int Position { get; }
            internal OcrRegionResult Original { get; }
            internal IReadOnlyList<OcrRecognitionWindow> Windows { get; }
            internal int Offset { get; }
        }
    }
}
