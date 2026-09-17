using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    public sealed partial class OcrPipeline
    {
        private async Task<RetryWork> RunOrientationRetriesAsync(IOcrImageInput input, List<OcrRegionResult> results, long initialBytes,
            OcrExecutionOptions execution, CtcConfidenceAggregation aggregation, CancellationToken token, CropWorkBudget? cropBudget)
        {
            var watch = Stopwatch.StartNew();
            var work = new RetryWork();
            OcrOrientationRetryOptions options = _cropProfile.OrientationRetry!;
            var budget = new RetryBudget(initialBytes, _options.MaximumResultBytes);
            var states = new List<RetryState>();
            int admitted = 0, totalCrops = 0;
            for (int index = 0; index < results.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                if (!options.NeedsRetry(results[index].Recognition)) continue;
                budget.Reserve(128);
                states.Add(new RetryState(index, results[index], admitted++ >= options.MaximumRegionsPerImage));
            }
            if (states.Count == 0) { watch.Stop(); work.Elapsed = watch.Elapsed; return work; }
            foreach (TextOrientation relative in options.Rotations)
            {
                token.ThrowIfCancellationRequested();
                var requests = new List<IndexedRequest>();
                var plans = new List<RetryPlan>();
                foreach (RetryState state in states)
                {
                    if (state.Skipped || !options.NeedsRetry(state.Best.Recognition)) continue;
                    TextRegion source = state.Initial.Region;
                    var region = new TextRegion(source.SourceIndex, source.Score, source.Polygon, source.CropQuadrilateral,
                        OcrOrientationRetryOptions.Rotate(source.Orientation, relative), source.AngleRadians, source.Language, source.Script, source.ExternalId, source.Metadata);
                    IReadOnlyList<OcrRecognitionWindow> windows = OcrRecognitionWindowPlanner.Plan(region, _cropProfile, token);
                    totalCrops = checked(totalCrops + windows.Count);
                    if (totalCrops > options.MaximumCropsPerImage || (_cropProfile.OverflowMode == RecognitionOverflowMode.SlidingWindow && requests.Count + windows.Count > _cropProfile.RecognitionWindows.MaximumWindowsPerImage))
                        throw Limit("OCR orientation retry crops exceed their image limit.", OcrPipelineStage.Recognition, regionIndex: source.SourceIndex);
                    plans.Add(new RetryPlan(state, region, windows, requests.Count));
                    foreach (OcrRecognitionWindow window in windows) requests.Add(new IndexedRequest(requests.Count, window.Crop));
                }
                if (requests.Count == 0) break;
                List<OcrBatchDescriptor> batches = CreateBatches(requests, RecognitionSelection.Profile.Input.MinimumBatch,
                    EffectiveMaximumBatch(RecognitionSelection), _options.MaximumRecognitionPaddingRatio, token);
                if (cropBudget != null) budget.Reserve(cropBudget.Reserve(batches));
                BatchExecution<VisualInferenceResult>[] executed = await RunBatchesAsync(input, RecognitionSelection.Profile.Input.Name, batches,
                    _recognizer.MaximumConcurrency, async (prepared, cancellation) =>
                    {
                        budget.Reserve(0);
                        VisualInferenceResult inference = await _recognizer.RunAsync(prepared, new VisualExecutionOptions(correlationId: execution.CorrelationId), cancellation).ConfigureAwait(false);
                        long bytes = 0;
                        foreach (RecognizedText row in inference.GetValue<TextRecognitionBatchResult>().Items) bytes = checked(bytes + RetryTextBytes(row));
                        // Conservatively include padded duplicate rows and all original/retry traces.
                        // 保守计入填充的重复行，以及所有原始/重试追踪。
                        budget.Reserve(bytes);
                        return inference;
                    }, token).ConfigureAwait(false);
                var texts = new RecognizedText[requests.Count];
                var widths = new OcrRecognitionWidthInfo[requests.Count];
                OcrCropDiagnostics[]? diagnostics = cropBudget == null ? null : new OcrCropDiagnostics[requests.Count];
                foreach (BatchExecution<VisualInferenceResult> batch in executed)
                {
                    work.Preparation += batch.Preparation; work.Inference += batch.Result.Timing.Inference; work.Postprocessing += batch.Result.Timing.Postprocessing;
                    work.BatchCount++;
                    TextRecognitionBatchResult decoded = batch.Result.GetValue<TextRecognitionBatchResult>();
                    if (decoded.Items.Count != batch.Batch.Requests.Count) throw Failure("Retry recognizer result count does not match submitted batch.", OcrPipelineStage.Recognition);
                    for (int index = 0; index < batch.Batch.ActualCount; index++)
                    {
                        IndexedRequest request = batch.Batch.Requests[index];
                        texts[request.Position] = decoded.Items[index].WithSourceRegionIndex(request.Request.Region.SourceIndex);
                        widths[request.Position] = batch.Batch.Crops[index].WidthInfo;
                        if (diagnostics != null) diagnostics[request.Position] = batch.Diagnostics[index];
                    }
                }
                foreach (RetryPlan plan in plans)
                {
                    token.ThrowIfCancellationRequested();
                    OcrRegionResult candidate;
                    if (plan.Windows.Count == 1)
                        candidate = new OcrRegionResult(plan.Region, texts[plan.Offset], widths[plan.Offset], Array.Empty<OcrRecognitionWindowResult>(), plan.State.Initial.Geometry);
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
                        candidate = OcrRecognitionWindowMerger.Merge(plan.Region, _cropProfile, windows, aggregation, token);
                        if (plan.State.Initial.Geometry != null) candidate = candidate.WithGeometry(plan.State.Initial.Geometry);
                    }
                    budget.Reserve(64L + plan.Windows.Count * 48L);
                    if (diagnostics != null) candidate = candidate.WithCropDiagnostics(CropEvidenceRange(diagnostics, plan.Offset, plan.Windows.Count));
                    plan.State.Attempts.Add(new OcrOrientationAttempt(candidate));
                    if (options.Prefer(candidate.Recognition, plan.State.Best.Recognition))
                    { plan.State.Best = candidate; plan.State.Selected = plan.State.Attempts.Count - 1; }
                }
            }
            foreach (RetryState state in states)
                results[state.Position] = state.Best.WithOrientationRetry(new OcrOrientationRetryResult(state.Attempts, state.Selected, state.Skipped));
            token.ThrowIfCancellationRequested();
            watch.Stop(); work.Elapsed = watch.Elapsed;
            return work;
        }

        private static long RetryTextBytes(RecognizedText text) => checked(40L + EncodingBytes(text.Text) + (long)text.Tokens.Count * 40);

        private sealed class RetryBudget
        {
            private long _used;
            private readonly long _maximum;
            internal RetryBudget(long used, long maximum) { _used = used; _maximum = maximum; }
            internal void Reserve(long bytes)
            {
                if (Interlocked.Add(ref _used, bytes) > _maximum) throw Limit("OCR original and retry results exceed their retained byte budget.", OcrPipelineStage.Recognition);
            }
        }

        private sealed class RetryState
        {
            internal RetryState(int position, OcrRegionResult initial, bool skipped)
            { Position = position; Initial = initial; Best = initial; Skipped = skipped; Attempts.Add(new OcrOrientationAttempt(initial)); }
            internal int Position { get; }
            internal OcrRegionResult Initial { get; }
            internal OcrRegionResult Best { get; set; }
            internal bool Skipped { get; }
            internal List<OcrOrientationAttempt> Attempts { get; } = new List<OcrOrientationAttempt>(4);
            internal int Selected { get; set; }
        }

        private sealed class RetryPlan
        {
            internal RetryPlan(RetryState state, TextRegion region, IReadOnlyList<OcrRecognitionWindow> windows, int offset)
            { State = state; Region = region; Windows = windows; Offset = offset; }
            internal RetryState State { get; }
            internal TextRegion Region { get; }
            internal IReadOnlyList<OcrRecognitionWindow> Windows { get; }
            internal int Offset { get; }
        }

        private sealed class RetryWork
        {
            internal TimeSpan Elapsed { get; set; }
            internal TimeSpan Preparation { get; set; }
            internal TimeSpan Inference { get; set; }
            internal TimeSpan Postprocessing { get; set; }
            internal int BatchCount { get; set; }
        }
    }
}
