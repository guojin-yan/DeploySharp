using System;
using System.Collections.Generic;
using System.Text;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Stores conservative, deterministic formula text diagnostics. / 保存保守且确定性的公式文本诊断指标。</summary>
    public sealed class PaddleDocumentFormulaQualityMetrics
    {
        internal PaddleDocumentFormulaQualityMetrics(
            bool exactTextMatch,
            bool normalizedTextMatch,
            int expectedCommandCount,
            int actualCommandCount,
            int matchedCommandCount,
            bool expectedBalancedDelimiters,
            bool actualBalancedDelimiters,
            OcrTextAccuracyMetrics textAccuracy,
            OcrTextAccuracyMetrics normalizedTextAccuracy)
        {
            ExactTextMatch = exactTextMatch;
            NormalizedTextMatch = normalizedTextMatch;
            ExpectedCommandCount = expectedCommandCount;
            ActualCommandCount = actualCommandCount;
            MatchedCommandCount = matchedCommandCount;
            ExpectedBalancedDelimiters = expectedBalancedDelimiters;
            ActualBalancedDelimiters = actualBalancedDelimiters;
            TextAccuracy = textAccuracy;
            NormalizedTextAccuracy = normalizedTextAccuracy;
        }

        /// <summary>Gets whether trimmed raw strings are equal. / 获取去首尾空白后的原始字符串是否相等。</summary>
        public bool ExactTextMatch { get; }
        /// <summary>Gets whether conservative whitespace-normalized strings are equal. / 获取保守空白规范化后的字符串是否相等。</summary>
        public bool NormalizedTextMatch { get; }
        /// <summary>Gets the number of LaTeX command tokens in the reference. / 获取参考文本中的 LaTeX 命令 token 数。</summary>
        public int ExpectedCommandCount { get; }
        /// <summary>Gets the number of LaTeX command tokens in the prediction. / 获取预测文本中的 LaTeX 命令 token 数。</summary>
        public int ActualCommandCount { get; }
        /// <summary>Gets multiset command-token overlap. / 获取命令 token 多重集合交集数量。</summary>
        public int MatchedCommandCount { get; }
        /// <summary>Gets whether braces and left/right command pairs are balanced in the reference. / 获取参考文本的大括号及 left/right 命令是否平衡。</summary>
        public bool ExpectedBalancedDelimiters { get; }
        /// <summary>Gets whether braces and left/right command pairs are balanced in the prediction. / 获取预测文本的大括号及 left/right 命令是否平衡。</summary>
        public bool ActualBalancedDelimiters { get; }
        /// <summary>Gets raw Unicode-scalar CER diagnostics. / 获取原始 Unicode 标量 CER 诊断。</summary>
        public OcrTextAccuracyMetrics TextAccuracy { get; }
        /// <summary>Gets CER diagnostics after conservative whitespace normalization. / 获取保守空白规范化后的 CER 诊断。</summary>
        public OcrTextAccuracyMetrics NormalizedTextAccuracy { get; }
        /// <summary>Gets command-token precision. / 获取命令 token 精确率。</summary>
        public double CommandPrecision => ActualCommandCount == 0 ? (ExpectedCommandCount == 0 ? 0d : 0d) : (double)MatchedCommandCount / ActualCommandCount;
        /// <summary>Gets command-token recall. / 获取命令 token 召回率。</summary>
        public double CommandRecall => ExpectedCommandCount == 0 ? (ActualCommandCount == 0 ? 1d : 0d) : (double)MatchedCommandCount / ExpectedCommandCount;
        /// <summary>Gets command-token F1. / 获取命令 token F1。</summary>
        public double CommandF1
        {
            get
            {
                double precision = CommandPrecision;
                double recall = CommandRecall;
                return precision + recall == 0d ? 0d : 2d * precision * recall / (precision + recall);
            }
        }
    }

    /// <summary>Computes formula diagnostics without claiming rendered mathematical equivalence. / 计算公式诊断，但不宣称渲染后的数学等价性。</summary>
    public static class PaddleDocumentFormulaQualityEvaluator
    {
        /// <summary>Compares two formula strings using raw/normalized CER, command overlap and delimiter checks. / 使用原始及规范化 CER、命令重叠和分隔符检查比较两个公式字符串。</summary>
        public static PaddleDocumentFormulaQualityMetrics Compare(string expected, string actual)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (actual == null) throw new ArgumentNullException(nameof(actual));
            string normalizedExpected = NormalizeLatex(expected);
            string normalizedActual = NormalizeLatex(actual);
            IReadOnlyDictionary<string, int> expectedCommands = Commands(expected);
            IReadOnlyDictionary<string, int> actualCommands = Commands(actual);
            int matched = 0;
            foreach (KeyValuePair<string, int> pair in expectedCommands)
            {
                int actualCount;
                if (actualCommands.TryGetValue(pair.Key, out actualCount)) matched += Math.Min(pair.Value, actualCount);
            }
            return new PaddleDocumentFormulaQualityMetrics(
                string.Equals(expected.Trim(), actual.Trim(), StringComparison.Ordinal),
                string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal),
                Count(expectedCommands), Count(actualCommands), matched,
                BalancedDelimiters(expected), BalancedDelimiters(actual),
                OcrTextAccuracy.Compare(expected, actual),
                OcrTextAccuracy.Compare(normalizedExpected, normalizedActual));
        }

        /// <summary>Collapses Unicode whitespace and normalizes line endings only; it does not rewrite LaTeX commands. / 仅折叠 Unicode 空白并规范换行，不重写 LaTeX 命令。</summary>
        public static string NormalizeLatex(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var builder = new StringBuilder(value.Length);
            bool pendingWhitespace = false;
            foreach (char character in value.Replace("\r\n", "\n").Replace('\r', '\n').Trim())
            {
                if (char.IsWhiteSpace(character)) { pendingWhitespace = builder.Length > 0; continue; }
                if (pendingWhitespace) { builder.Append(' '); pendingWhitespace = false; }
                builder.Append(character);
            }
            return builder.ToString();
        }

        private static int Count(IReadOnlyDictionary<string, int> values)
        {
            int count = 0;
            foreach (int value in values.Values) count += value;
            return count;
        }

        private static IReadOnlyDictionary<string, int> Commands(string value)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '\\') continue;
                int start = index++;
                if (index >= value.Length) Add(result, value.Substring(start, 1));
                else if (char.IsLetter(value[index]))
                {
                    while (index + 1 < value.Length && char.IsLetter(value[index + 1])) index++;
                    Add(result, value.Substring(start, index - start + 1));
                }
                else Add(result, value.Substring(start, 1) + value[index]);
            }
            return result;
        }

        private static void Add(Dictionary<string, int> values, string token)
        {
            int count;
            values.TryGetValue(token, out count);
            values[token] = count + 1;
        }

        private static bool BalancedDelimiters(string value)
        {
            int braces = 0;
            int left = 0;
            int right = 0;
            IReadOnlyDictionary<string, int> commands = Commands(value);
            foreach (KeyValuePair<string, int> command in commands)
            {
                if (command.Key == "\\left") left += command.Value;
                else if (command.Key == "\\right") right += command.Value;
            }
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] == '\\') { index++; continue; }
                if (value[index] == '{') braces++;
                else if (value[index] == '}') { braces--; if (braces < 0) return false; }
            }
            return braces == 0 && left == right;
        }
    }
}

#pragma warning restore CS1591
