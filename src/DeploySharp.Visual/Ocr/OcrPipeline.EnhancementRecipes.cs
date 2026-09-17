using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    public sealed partial class OcrPipeline
    {
        private async Task<RetryWork> RunEnhancementRecipeRetryAsync(IOcrImageInput input, List<OcrRegionResult> results, long initialBytes,
            OcrExecutionOptions execution, CtcConfidenceAggregation aggregation, CancellationToken token, CropWorkBudget cropBudget)
        {
            var watch = Stopwatch.StartNew();
            var work = new RetryWork();
            OcrEnhancementRetryOptions options = _cropProfile.EnhancementRetry!;
            var budget = new RetryBudget(initialBytes, _options.MaximumResultBytes);
            var admitted = new List<int>();
            for (int position = 0; position < results.Count; position++)
            {
                token.ThrowIfCancellationRequested();
                OcrRegionResult original = results[position];
                if (!options.NeedsRetry(original.Recognition)) continue;
                budget.Reserve(256);
                bool eligible = false;
                foreach (OcrCropEnhancementOptions recipe in options.Enhancements)
                {
                    if (HasEligibleCrop(original, recipe, token)) { eligible = true; break; }
                }
                if (!eligible)
                {
                    results[position] = original.WithEnhancementRetry(new OcrEnhancementRetryResult(options, new OcrEnhancementAttempt(original), (OcrEnhancementAttempt?)null, OcrEnhancementRetryDecision.NoEligibleCrop));
                }
                else if (admitted.Count >= options.MaximumRegionsPerImage)
                {
                    results[position] = original.WithEnhancementRetry(new OcrEnhancementRetryResult(options, new OcrEnhancementAttempt(original), (OcrEnhancementAttempt?)null, OcrEnhancementRetryDecision.RegionLimit));
                }
                else admitted.Add(position);
            }

            var candidateLists = new List<RecipeCandidate>[results.Count];
            int retryStartCrops = cropBudget.CropsUsed;
            for (int recipeIndex = 0; recipeIndex < options.Enhancements.Count; recipeIndex++)
            {
                token.ThrowIfCancellationRequested();
                OcrCropEnhancementOptions recipe = options.Enhancements[recipeIndex];
                var recipePositions = new List<int>();
                var subset = new List<OcrRegionResult>();
                foreach (int position in admitted)
                {
                    OcrRegionResult original = results[position];
                    if (!HasEligibleCrop(original, recipe, token)) continue;
                    recipePositions.Add(position);
                    subset.Add(original);
                }
                if (subset.Count == 0) continue;
                int usedRetryCrops = cropBudget.CropsUsed - retryStartCrops;
                int remainingCrops = options.MaximumCropsPerImage - usedRetryCrops;
                if (remainingCrops <= 0) throw Limit("Enhancement recipe candidates exceed their shared image crop limit.", OcrPipelineStage.Recognition);
                var recipeOptions = OcrEnhancementRetryOptions.CreateMany(new[] { recipe }, options.ConfidenceThreshold,
                    options.SelectionPolicy, options.MinimumConfidenceGain, recipePositions.Count, remainingCrops);
                RetryWork recipeWork = await RunEnhancementRetryAsync(input, subset, initialBytes, execution, aggregation, token, cropBudget, recipeOptions).ConfigureAwait(false);
                work.Preparation += recipeWork.Preparation;
                work.Inference += recipeWork.Inference;
                work.Postprocessing += recipeWork.Postprocessing;
                work.BatchCount += recipeWork.BatchCount;
                for (int index = 0; index < subset.Count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    OcrEnhancementRetryResult? retry = subset[index].EnhancementRetry;
                    if (retry?.Candidate == null) continue;
                    OcrRegionResult candidate = RehydrateEnhancementAttempt(subset[index], retry.Candidate);
                    int position = recipePositions[index];
                    List<RecipeCandidate>? list = candidateLists[position];
                    if (list == null) candidateLists[position] = list = new List<RecipeCandidate>(options.Enhancements.Count);
                    list.Add(new RecipeCandidate(recipeIndex, candidate));
                    budget.Reserve(RetryTextBytes(candidate.Recognition) + 128L + candidate.CropDiagnostics.Count * 48L);
                }
            }

            foreach (int position in admitted)
            {
                token.ThrowIfCancellationRequested();
                OcrRegionResult original = results[position];
                List<RecipeCandidate>? candidates = candidateLists[position];
                if (candidates == null || candidates.Count == 0)
                {
                    results[position] = original.WithEnhancementRetry(new OcrEnhancementRetryResult(options, new OcrEnhancementAttempt(original), (OcrEnhancementAttempt?)null, OcrEnhancementRetryDecision.NoEligibleCrop));
                    continue;
                }
                var candidateAttempts = new List<OcrEnhancementAttempt>(candidates.Count);
                var candidateRecipeIndices = new List<int>(candidates.Count);
                OcrRegionResult? selectedCandidate = null;
                int? selectedIndex = null;
                for (int index = 0; index < candidates.Count; index++)
                {
                    OcrRegionResult candidate = candidates[index].Result;
                    candidateAttempts.Add(new OcrEnhancementAttempt(candidate));
                    candidateRecipeIndices.Add(candidates[index].RecipeIndex);
                    if (!options.Prefer(candidate.Recognition, original.Recognition)) continue;
                    if (selectedCandidate == null || candidate.Recognition.Confidence > selectedCandidate.Recognition.Confidence)
                    {
                        selectedCandidate = candidate;
                        selectedIndex = index;
                    }
                }
                OcrEnhancementRetryDecision decision = selectedCandidate != null ? OcrEnhancementRetryDecision.CandidateSelected :
                    options.SelectionPolicy == OcrEnhancementSelectionPolicy.PreserveOriginal ? OcrEnhancementRetryDecision.PreservedByPolicy : OcrEnhancementRetryDecision.InsufficientGain;
                OcrRegionResult chosen = selectedCandidate ?? original;
                if (selectedCandidate != null && original.OrientationRetry != null) chosen = chosen.WithOrientationRetry(original.OrientationRetry);
                results[position] = chosen.WithEnhancementRetry(new OcrEnhancementRetryResult(options, new OcrEnhancementAttempt(original), candidateAttempts, candidateRecipeIndices, selectedIndex, decision));
            }

            token.ThrowIfCancellationRequested();
            watch.Stop();
            // Recipes execute sequentially, so the outer wall clock already includes
            // every candidate. Summing candidate elapsed values would double-count
            // scheduling gaps and make Recognition exceed the real wall clock.
            work.Elapsed = watch.Elapsed;
            work.RetainedBytes = budget.Used;
            return work;
        }

        private static bool HasEligibleCrop(OcrRegionResult result, OcrCropEnhancementOptions recipe, CancellationToken token)
        {
            foreach (OcrCropDiagnostics evidence in result.CropDiagnostics)
            {
                token.ThrowIfCancellationRequested();
                if (OcrCropEnhancementPolicy.Decide(evidence.Rectified, recipe) == OcrCropEnhancementDecision.Applied) return true;
            }
            return false;
        }

        private static OcrRegionResult RehydrateEnhancementAttempt(OcrRegionResult original, OcrEnhancementAttempt attempt)
        {
            var candidate = new OcrRegionResult(original.Region, attempt.Recognition, attempt.RecognitionWidth, attempt.RecognitionWindows, original.Geometry, original.OrientationRetry)
                .WithPixelQuality(original.PixelQuality).WithWidthRetry(original.WidthRetry);
            return attempt.CropDiagnostics.Count == 0 ? candidate : candidate.WithCropDiagnostics(attempt.CropDiagnostics);
        }

        private sealed class RecipeCandidate
        {
            internal RecipeCandidate(int recipeIndex, OcrRegionResult result) { RecipeIndex = recipeIndex; Result = result; }
            internal int RecipeIndex { get; }
            internal OcrRegionResult Result { get; }
        }
    }
}
