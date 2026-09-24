using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains bounded edit-distance metrics for one reference/hypothesis text pair. / 保存一对参考文本与识别文本的有界编辑距离指标。</summary>
    public sealed class OcrTextAccuracyMetrics
    {
        internal OcrTextAccuracyMetrics(int referenceCharacters, int hypothesisCharacters, int characterDistance, int referenceWords, int hypothesisWords, int wordDistance)
        {
            ReferenceCharacters = referenceCharacters; HypothesisCharacters = hypothesisCharacters; CharacterEditDistance = characterDistance;
            ReferenceWords = referenceWords; HypothesisWords = hypothesisWords; WordEditDistance = wordDistance;
        }

        /// <summary>Gets reference Unicode scalar count. / 获取参考文本 Unicode 标量数量。</summary>
        public int ReferenceCharacters { get; }
        /// <summary>Gets hypothesis Unicode scalar count. / 获取识别文本 Unicode 标量数量。</summary>
        public int HypothesisCharacters { get; }
        /// <summary>Gets character edit distance. / 获取字符编辑距离。</summary>
        public int CharacterEditDistance { get; }
        /// <summary>Gets reference word count. / 获取参考文本词数。</summary>
        public int ReferenceWords { get; }
        /// <summary>Gets hypothesis word count. / 获取识别文本词数。</summary>
        public int HypothesisWords { get; }
        /// <summary>Gets word edit distance. / 获取词编辑距离。</summary>
        public int WordEditDistance { get; }
        /// <summary>Gets character error rate. / 获取字符错误率。</summary>
        public double CharacterErrorRate => ReferenceCharacters == 0 ? (HypothesisCharacters == 0 ? 0d : 1d) : (double)CharacterEditDistance / ReferenceCharacters;
        /// <summary>Gets word error rate. / 获取词错误率。</summary>
        public double WordErrorRate => ReferenceWords == 0 ? (HypothesisWords == 0 ? 0d : 1d) : (double)WordEditDistance / ReferenceWords;
    }

    /// <summary>Computes deterministic CER/WER without changing OCR text or applying normalization implicitly. / 计算确定性的 CER/WER，不修改 OCR 文本且不隐式规范化。</summary>
    public static class OcrTextAccuracy
    {
        /// <summary>Compares reference and hypothesis using Unicode scalars and whitespace-delimited words. / 使用 Unicode 标量和空白分隔词比较参考文本与识别文本。</summary>
        public static OcrTextAccuracyMetrics Compare(string reference, string hypothesis)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (hypothesis == null) throw new ArgumentNullException(nameof(hypothesis));
            IReadOnlyList<string> referenceCharacters = Scalars(reference);
            IReadOnlyList<string> hypothesisCharacters = Scalars(hypothesis);
            IReadOnlyList<string> referenceWords = Words(reference);
            IReadOnlyList<string> hypothesisWords = Words(hypothesis);
            return new OcrTextAccuracyMetrics(referenceCharacters.Count, hypothesisCharacters.Count,
                Distance(referenceCharacters, hypothesisCharacters), referenceWords.Count, hypothesisWords.Count,
                Distance(referenceWords, hypothesisWords));
        }

        private static IReadOnlyList<string> Scalars(string value)
        {
            var result = new List<string>();
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    result.Add(value.Substring(index++, 2));
                else result.Add(value[index].ToString());
            }
            return result;
        }

        private static IReadOnlyList<string> Words(string value)
        {
            var result = new List<string>();
            int start = -1;
            for (int index = 0; index <= value.Length; index++)
            {
                bool separator = index == value.Length || char.IsWhiteSpace(value[index]);
                if (!separator && start < 0) start = index;
                if (separator && start >= 0) { result.Add(value.Substring(start, index - start)); start = -1; }
            }
            return result;
        }

        private static int Distance(IReadOnlyList<string> reference, IReadOnlyList<string> hypothesis)
        {
            var previous = new int[hypothesis.Count + 1];
            var current = new int[hypothesis.Count + 1];
            for (int column = 0; column <= hypothesis.Count; column++) previous[column] = column;
            for (int row = 1; row <= reference.Count; row++)
            {
                current[0] = row;
                for (int column = 1; column <= hypothesis.Count; column++)
                {
                    int substitution = previous[column - 1] + (reference[row - 1] == hypothesis[column - 1] ? 0 : 1);
                    current[column] = Math.Min(Math.Min(previous[column] + 1, current[column - 1] + 1), substitution);
                }
                int[] swap = previous; previous = current; current = swap;
            }
            return previous[hypothesis.Count];
        }
    }
}
