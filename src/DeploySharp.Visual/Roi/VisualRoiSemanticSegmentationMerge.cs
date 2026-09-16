using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines deterministic semantic-label ownership for overlapping ROI masks. / 定义重叠 ROI 掩码的确定性语义标签所有权。</summary>
    public enum RoiSemanticLabelMergeMode
    {
        /// <summary>Keep the first candidate in caller order. / 保留调用顺序中的第一个候选。</summary>
        KeepFirst = 0,
        /// <summary>Keep the last candidate in caller order. / 保留调用顺序中的最后一个候选。</summary>
        KeepLast = 1,
        /// <summary>Choose the candidate with the highest ROI priority. / 选择 ROI 优先级最高的候选。</summary>
        RoiPriority = 2
    }

    /// <summary>Defines deterministic probability fusion for overlapping semantic ROI results. / 定义重叠语义 ROI 结果的确定性概率融合策略。</summary>
    public enum RoiSemanticProbabilityMergeMode
    {
        /// <summary>Keep the first candidate's probabilities. / 保留第一个候选的概率。</summary>
        KeepFirst = 0,
        /// <summary>Keep the last candidate's probabilities. / 保留最后一个候选的概率。</summary>
        KeepLast = 1,
        /// <summary>Average probabilities from all candidates. / 平均所有候选的概率。</summary>
        Mean = 2,
        /// <summary>Take the maximum probability independently per class and pixel. / 按类别和像素独立取最大概率。</summary>
        Max = 3,
        /// <summary>Choose probabilities from the highest-priority ROI. / 选择最高优先级 ROI 的概率。</summary>
        RoiPriority = 4
    }

    /// <summary>Contains a fused semantic mask and deterministic ROI ownership provenance. / 包含融合后的语义掩码及确定性的 ROI 所有权来源。</summary>
    public sealed class RoiMergedSemanticSegmentationResult
    {
        private readonly IReadOnlyList<string> _pixelOwnerRoiIds;

        internal RoiMergedSemanticSegmentationResult(SemanticSegmentationResult result, IEnumerable<string> pixelOwnerRoiIds)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (pixelOwnerRoiIds == null) throw new ArgumentNullException(nameof(pixelOwnerRoiIds));
            _pixelOwnerRoiIds = new ReadOnlyCollection<string>(pixelOwnerRoiIds.ToList());
            if (_pixelOwnerRoiIds.Count != checked(result.Mask.Width * result.Mask.Height)) throw new ArgumentException("Pixel owner count must match the fused mask.", nameof(pixelOwnerRoiIds));
        }

        /// <summary>Gets the fused source-space result. / 获取融合后的源图结果。</summary>
        public SemanticSegmentationResult Result { get; }
        /// <summary>Gets the ROI id owning each row-major pixel, or an empty string outside all candidates. / 获取每个行优先像素的所有权 ROI ID；不属于候选时为空字符串。</summary>
        public IReadOnlyList<string> PixelOwnerRoiIds => _pixelOwnerRoiIds;
        /// <summary>Gets source dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize => new VisualSize(Result.Mask.Width, Result.Mask.Height);
    }

    /// <summary>Fuses full-source semantic label masks from ROI crops using explicit pixel ownership rules. / 使用显式逐像素所有权规则融合 ROI 裁剪得到的完整源图语义标签掩码。</summary>
    public sealed class RoiSemanticSegmentationResultMerger
    {
        /// <summary>Merges projected semantic results. Probability maps are intentionally not synthesized. / 合并已投影语义结果；不会虚构概率图。</summary>
        public RoiMergedSemanticSegmentationResult Merge(IReadOnlyList<RoiProjectedResult<SemanticSegmentationResult>> results, RoiSemanticLabelMergeMode mode = RoiSemanticLabelMergeMode.RoiPriority)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiSemanticLabelMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (results.Count == 0) throw new ArgumentException("At least one semantic-segmentation result is required.", nameof(results));
            RoiProjectedResult<SemanticSegmentationResult> firstCandidate = results[0] ?? throw new ArgumentException("Semantic candidates cannot contain null values.", nameof(results));
            SemanticSegmentationResult first = firstCandidate.Result;
            int width = first.Mask.Width;
            int height = first.Mask.Height;
            var classes = first.Classes;
            var classMap = classes.ToDictionary(value => value.Index);
            for (int index = 0; index < results.Count; index++)
            {
                RoiProjectedResult<SemanticSegmentationResult> candidate = results[index] ?? throw new ArgumentException("Semantic candidates cannot contain null values.", nameof(results));
                SemanticSegmentationResult result = candidate.Result;
                if (result.Mask.Width != width || result.Mask.Height != height) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI semantic-segmentation results must use the same source-image dimensions.");
                if (result.ProbabilityMap != null) throw new NotSupportedException("Semantic probability-map fusion is not implemented; pass label-only results or fuse probabilities in an application-specific merger.");
                if (result.Classes.Count != classes.Count || result.Classes.Any(value => !classMap.TryGetValue(value.Index, out SemanticSegmentationClass? expected) || expected.Label != value.Label || expected.IsBackground != value.IsBackground || expected.IsIgnored != value.IsIgnored))
                {
                    throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI semantic-segmentation results must use the same class contract.");
                }
            }

            int pixelCount = checked(width * height);
            var output = new ushort[pixelCount];
            var owners = new string[pixelCount];
            for (int pixel = 0; pixel < pixelCount; pixel++)
            {
                bool selected = false;
                int selectedPriority = int.MinValue;
                for (int candidateIndex = 0; candidateIndex < results.Count; candidateIndex++)
                {
                    RoiProjectedResult<SemanticSegmentationResult> candidate = results[candidateIndex]!;
                    bool replace = mode == RoiSemanticLabelMergeMode.KeepLast || !selected;
                    if (mode == RoiSemanticLabelMergeMode.RoiPriority) replace = !selected || candidate.Priority > selectedPriority;
                    if (!replace) continue;
                    output[pixel] = candidate.Result.Mask.DangerousGetReadOnlyBuffer()[pixel];
                    owners[pixel] = candidate.RoiId;
                    selectedPriority = candidate.Priority;
                    selected = true;
                    if (mode == RoiSemanticLabelMergeMode.KeepFirst) break;
                }
            }

            var mask = new SemanticSegmentationMask(width, height, output, true);
            var counts = new long[classes.Count];
            var classIndexes = classes.Select(value => value.Index).ToArray();
            var indexToCount = new Dictionary<int, int>();
            for (int index = 0; index < classIndexes.Length; index++) indexToCount[classIndexes[index]] = index;
            for (int pixel = 0; pixel < output.Length; pixel++) if (indexToCount.TryGetValue(output[pixel], out int classOffset)) counts[classOffset]++;
            var statistics = new List<SegmentationClassStatistics>(classes.Count);
            for (int index = 0; index < classes.Count; index++) statistics.Add(new SegmentationClassStatistics(classes[index].Index, counts[index], pixelCount == 0 ? 0 : (double)counts[index] / pixelCount));
            var fused = new SemanticSegmentationResult(mask, classes, statistics, SegmentationRle.Encode(mask));
            return new RoiMergedSemanticSegmentationResult(fused, owners);
        }

        /// <summary>Merges projected semantic probability maps and rebuilds labels from the fused class probabilities. / 合并已投影语义概率图，并根据融合后的类别概率重建标签。</summary>
        /// <remarks>All candidates must carry source-resolution HWC probability maps with one channel per class. This method is separate from <see cref="Merge"/> so label-only callers retain their previous explicit semantics. / 所有候选必须携带源分辨率 HWC 概率图且每个类别一个通道；该方法与 <see cref="Merge"/> 分开，保持仅标签调用方原有的显式语义。</remarks>
        public RoiMergedSemanticSegmentationResult MergeWithProbabilities(IReadOnlyList<RoiProjectedResult<SemanticSegmentationResult>> results, RoiSemanticProbabilityMergeMode mode = RoiSemanticProbabilityMergeMode.Mean)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (!Enum.IsDefined(typeof(RoiSemanticProbabilityMergeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (results.Count == 0) throw new ArgumentException("At least one semantic-segmentation result is required.", nameof(results));
            RoiProjectedResult<SemanticSegmentationResult> firstCandidate = results[0] ?? throw new ArgumentException("Semantic candidates cannot contain null values.", nameof(results));
            SemanticSegmentationResult first = firstCandidate.Result;
            SegmentationProbabilityMap firstMap = first.ProbabilityMap ?? throw new VisualException(VisualErrorCodes.InputInvalid, "Probability fusion requires every semantic result to carry a probability map.");
            int width = first.Mask.Width;
            int height = first.Mask.Height;
            int classCount = firstMap.ClassCount;
            ValidateProbabilityContract(results, first, firstMap, width, height, classCount);

            int pixelCount = checked(width * height);
            var fused = new float[checked(pixelCount * classCount)];
            var owners = new string[pixelCount];
            var ownerPriority = Enumerable.Repeat(int.MinValue, pixelCount).ToArray();
            for (int candidateIndex = 0; candidateIndex < results.Count; candidateIndex++)
            {
                RoiProjectedResult<SemanticSegmentationResult> candidate = results[candidateIndex]!;
                float[] values = candidate.Result.ProbabilityMap!.DangerousGetReadOnlyBuffer();
                if (mode == RoiSemanticProbabilityMergeMode.Mean)
                {
                    for (int offset = 0; offset < fused.Length; offset++) fused[offset] += values[offset] / results.Count;
                    if (candidateIndex == 0) for (int pixel = 0; pixel < pixelCount; pixel++) owners[pixel] = candidate.RoiId;
                    continue;
                }
                for (int pixel = 0; pixel < pixelCount; pixel++)
                {
                    int sourceOffset = pixel * classCount;
                    if (mode == RoiSemanticProbabilityMergeMode.KeepFirst && candidateIndex != 0) continue;
                    if (mode == RoiSemanticProbabilityMergeMode.KeepFirst || (mode == RoiSemanticProbabilityMergeMode.Max && candidateIndex == 0))
                    {
                        Array.Copy(values, sourceOffset, fused, sourceOffset, classCount);
                        owners[pixel] = candidate.RoiId;
                        ownerPriority[pixel] = candidate.Priority;
                        continue;
                    }
                    if (mode == RoiSemanticProbabilityMergeMode.KeepLast)
                    {
                        Array.Copy(values, sourceOffset, fused, sourceOffset, classCount);
                        owners[pixel] = candidate.RoiId;
                        ownerPriority[pixel] = candidate.Priority;
                        continue;
                    }
                    if (mode == RoiSemanticProbabilityMergeMode.RoiPriority && candidate.Priority < ownerPriority[pixel]) continue;
                    if (mode == RoiSemanticProbabilityMergeMode.RoiPriority)
                    {
                        Array.Copy(values, sourceOffset, fused, sourceOffset, classCount);
                        owners[pixel] = candidate.RoiId;
                        ownerPriority[pixel] = candidate.Priority;
                        continue;
                    }
                    for (int classIndex = 0; classIndex < classCount; classIndex++)
                    {
                        int offset = sourceOffset + classIndex;
                        if (values[offset] > fused[offset])
                        {
                            fused[offset] = values[offset];
                            owners[pixel] = candidate.RoiId;
                        }
                    }
                }
            }

            var labels = new ushort[pixelCount];
            for (int pixel = 0; pixel < pixelCount; pixel++)
            {
                int offset = pixel * classCount;
                int bestClass = 0;
                float bestValue = fused[offset];
                for (int classIndex = 1; classIndex < classCount; classIndex++) if (fused[offset + classIndex] > bestValue) { bestClass = classIndex; bestValue = fused[offset + classIndex]; }
                labels[pixel] = checked((ushort)bestClass);
                if (mode == RoiSemanticProbabilityMergeMode.Mean && string.IsNullOrEmpty(owners[pixel])) owners[pixel] = firstCandidate.RoiId;
            }
            var counts = new long[classCount];
            for (int pixel = 0; pixel < labels.Length; pixel++) counts[labels[pixel]]++;
            var statistics = new List<SegmentationClassStatistics>(first.Classes.Count);
            for (int classIndex = 0; classIndex < first.Classes.Count; classIndex++) statistics.Add(new SegmentationClassStatistics(first.Classes[classIndex].Index, counts[classIndex], (double)counts[classIndex] / pixelCount));
            var probabilityMap = new SegmentationProbabilityMap(width, height, classCount, fused, true);
            var result = new SemanticSegmentationResult(new SemanticSegmentationMask(width, height, labels, true), first.Classes, statistics, probabilityMap: probabilityMap);
            return new RoiMergedSemanticSegmentationResult(result, owners);
        }

        private static void ValidateProbabilityContract(IReadOnlyList<RoiProjectedResult<SemanticSegmentationResult>> results, SemanticSegmentationResult first, SegmentationProbabilityMap firstMap, int width, int height, int classCount)
        {
            if (classCount != first.Classes.Count) throw new VisualException(VisualErrorCodes.InputInvalid, "Semantic probability-map channel count must equal the class contract count.");
            for (int classIndex = 0; classIndex < first.Classes.Count; classIndex++) if (first.Classes[classIndex].Index != classIndex) throw new VisualException(VisualErrorCodes.InputInvalid, "Semantic probability fusion requires zero-based contiguous class indexes.");
            for (int index = 0; index < results.Count; index++)
            {
                RoiProjectedResult<SemanticSegmentationResult> candidate = results[index] ?? throw new ArgumentException("Semantic candidates cannot contain null values.", nameof(results));
                SemanticSegmentationResult result = candidate.Result;
                SegmentationProbabilityMap map = result.ProbabilityMap ?? throw new VisualException(VisualErrorCodes.InputInvalid, "Probability fusion requires every semantic result to carry a probability map.");
                if (result.Mask.Width != width || result.Mask.Height != height || map.Width != width || map.Height != height || map.ClassCount != classCount) throw new VisualException(VisualErrorCodes.InputInvalid, "All semantic probability maps must use the same source dimensions and class count.");
                if (result.Classes.Count != first.Classes.Count || result.Classes.Any(value => !first.Classes.Any(expected => expected.Index == value.Index && expected.Label == value.Label && expected.IsBackground == value.IsBackground && expected.IsIgnored == value.IsIgnored))) throw new VisualException(VisualErrorCodes.InputInvalid, "All ROI semantic probability results must use the same class contract.");
            }
        }
    }
}
