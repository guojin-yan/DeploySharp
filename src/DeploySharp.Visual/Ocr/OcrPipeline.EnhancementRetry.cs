using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    public sealed partial class OcrPipeline
    {
        private async Task<RetryWork> RunEnhancementRetryAsync(IOcrImageInput input, List<OcrRegionResult> results, long initialBytes,
            OcrExecutionOptions execution, CtcConfidenceAggregation aggregation, CancellationToken token, CropWorkBudget cropBudget)
        {
            var watch = Stopwatch.StartNew();
            var work = new RetryWork();
            OcrEnhancementRetryOptions options = _cropProfile.EnhancementRetry!;
            TextCropProfile candidateProfile = _cropProfile.ForEnhancementCandidate(options.Enhancement);
            var budget = new RetryBudget(initialBytes, _options.MaximumResultBytes);
            var requests = new List<IndexedRequest>();
            var plans = new List<EnhancementPlan>();
            for (int position = 0; position < results.Count; position++)
            {
                token.ThrowIfCancellationRequested();
                OcrRegionResult original = results[position];
                if (!options.NeedsRetry(original.Recognition)) continue;
                budget.Reserve(256);
                bool eligible = false;
                foreach (OcrCropDiagnostics evidence in original.CropDiagnostics)
                {
                    token.ThrowIfCancellationRequested();
                    if (OcrCropEnhancementPolicy.Decide(evidence.Rectified, options.Enhancement) == OcrCropEnhancementDecision.Applied) eligible = true;
                }
                if (!eligible || plans.Count >= options.MaximumRegionsPerImage)
                {
                    results[position] = original.WithEnhancementRetry(new OcrEnhancementRetryResult(options, new OcrEnhancementAttempt(original), null,
                        eligible ? OcrEnhancementRetryDecision.RegionLimit : OcrEnhancementRetryDecision.NoEligibleCrop));
                    continue;
                }
                IReadOnlyList<OcrRecognitionWindow> windows = OcrRecognitionWindowPlanner.Plan(original.Region, candidateProfile, token);
                if (windows.Count != original.CropDiagnostics.Count) throw Failure("Enhancement retry cannot match the original crop evidence.", OcrPipelineStage.Recognition);
                if (requests.Count + windows.Count > options.MaximumCropsPerImage ||
                    (candidateProfile.OverflowMode == RecognitionOverflowMode.SlidingWindow && requests.Count + windows.Count > candidateProfile.RecognitionWindows.MaximumWindowsPerImage))
                    throw Limit("Enhancement retry windows exceed their image limit.", OcrPipelineStage.Recognition, regionIndex: original.Region.SourceIndex);
                plans.Add(new EnhancementPlan(position, original, windows, requests.Count));
                foreach (OcrRecognitionWindow window in windows)
                {
                    // Retain the original physical width: regrouping a single line must not silently
                    // change padding/receptive-field context and attribute the change to enhancement.
                    // 保留原物理宽度：单行重新分组不得改变补边上下文并把变化归因于增强。
                    int originalWidth = original.CropDiagnostics[window.Index].TensorSize.Width;
                    requests.Add(new IndexedRequest(requests.Count, new TextCropRequest(window.Crop.Region, candidateProfile, originalWidth)));
                }
            }
            if (requests.Count != 0)
            {
                List<OcrBatchDescriptor> batches = CreateBatches(requests, RecognitionSelection.Profile.Input.MinimumBatch,
                    EffectiveMaximumBatch(RecognitionSelection), 1.0, token);
                int physical = 0; foreach (OcrBatchDescriptor batch in batches) physical = checked(physical + batch.Crops.Count);
                if (physical > options.MaximumCropsPerImage) throw Limit("Enhancement retry physical rows including padding exceed their image limit.", OcrPipelineStage.Recognition);
                budget.Reserve(cropBudget.Reserve(batches));
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
                var diagnostics = new OcrCropDiagnostics[requests.Count];
                foreach (BatchExecution<VisualInferenceResult> batch in executed)
                {
                    work.Preparation += batch.Preparation; work.Inference += batch.Result.Timing.Inference; work.Postprocessing += batch.Result.Timing.Postprocessing;
                    work.BatchCount++;
                    TextRecognitionBatchResult decoded = batch.Result.GetValue<TextRecognitionBatchResult>();
                    if (decoded.Items.Count != batch.Batch.Requests.Count) throw Failure("Enhancement recognizer result count mismatch.", OcrPipelineStage.Recognition);
                    for (int index = 0; index < batch.Batch.ActualCount; index++)
                    {
                        IndexedRequest request = batch.Batch.Requests[index];
                        texts[request.Position] = decoded.Items[index].WithSourceRegionIndex(request.Request.Region.SourceIndex);
                        widths[request.Position] = batch.Batch.Crops[index].WidthInfo;
                        diagnostics[request.Position] = batch.Diagnostics[index];
                    }
                }
                foreach (EnhancementPlan plan in plans)
                {
                    token.ThrowIfCancellationRequested();
                    OcrRegionResult candidate;
                    if (plan.Windows.Count == 1)
                        candidate = new OcrRegionResult(plan.Original.Region, texts[plan.Offset], widths[plan.Offset], System.Array.Empty<OcrRecognitionWindowResult>(), plan.Original.Geometry);
                    else
                    {
                        var windows = new List<OcrRecognitionWindowResult>(plan.Windows.Count);
                        long mergedBytes = 0;
                        for (int index = 0; index < plan.Windows.Count; index++)
                        {
                            RecognizedText text = texts[plan.Offset + index]; mergedBytes = checked(mergedBytes + RetryTextBytes(text));
                            windows.Add(new OcrRecognitionWindowResult(plan.Windows[index], text, widths[plan.Offset + index]));
                        }
                        budget.Reserve(mergedBytes);
                        candidate = OcrRecognitionWindowMerger.Merge(plan.Original.Region, candidateProfile, windows, aggregation, token);
                        if (plan.Original.Geometry != null) candidate = candidate.WithGeometry(plan.Original.Geometry);
                    }
                    budget.Reserve(128L + plan.Windows.Count * 48L);
                    candidate = candidate.WithCropDiagnostics(CropEvidenceRange(diagnostics, plan.Offset, plan.Windows.Count));
                    bool selected = options.Prefer(candidate.Recognition, plan.Original.Recognition);
                    OcrEnhancementRetryDecision decision = selected ? OcrEnhancementRetryDecision.CandidateSelected :
                        options.SelectionPolicy == OcrEnhancementSelectionPolicy.PreserveOriginal ? OcrEnhancementRetryDecision.PreservedByPolicy : OcrEnhancementRetryDecision.InsufficientGain;
                    OcrRegionResult chosen = selected ? candidate : plan.Original;
                    if (selected && plan.Original.OrientationRetry != null) chosen = chosen.WithOrientationRetry(plan.Original.OrientationRetry);
                    results[plan.Position] = chosen.WithEnhancementRetry(new OcrEnhancementRetryResult(options, new OcrEnhancementAttempt(plan.Original), new OcrEnhancementAttempt(candidate), decision));
                }
            }
            token.ThrowIfCancellationRequested();
            watch.Stop(); work.Elapsed = watch.Elapsed; work.RetainedBytes = budget.Used;
            return work;
        }

        private sealed class EnhancementPlan
        {
            internal EnhancementPlan(int position, OcrRegionResult original, IReadOnlyList<OcrRecognitionWindow> windows, int offset)
            { Position = position; Original = original; Windows = windows; Offset = offset; }
            internal int Position { get; }
            internal OcrRegionResult Original { get; }
            internal IReadOnlyList<OcrRecognitionWindow> Windows { get; }
            internal int Offset { get; }
        }
    }
}
