using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the Unicode normalization form applied to OCR text. / 标识 OCR 文本使用的 Unicode 规范化形式。</summary>
    public enum OcrUnicodeNormalizationForm
    {
        /// <summary>Do not normalize Unicode composition. / 不进行 Unicode 组合规范化。</summary>
        None = 0,
        /// <summary>Use canonical composition (NFC). / 使用规范组合（NFC）。</summary>
        Nfc = 1,
        /// <summary>Use compatibility composition (NFKC). / 使用兼容组合（NFKC）。</summary>
        Nfkc = 2
    }

    /// <summary>Defines the action taken when normalized text exceeds its scalar limit. / 定义规范化文本超过标量长度限制时的处理方式。</summary>
    public enum OcrTextNormalizationOverflowMode
    {
        /// <summary>Reject the operation with a diagnosable Visual exception. / 以可诊断的 Visual 异常拒绝操作。</summary>
        Reject = 0,
        /// <summary>Truncate at a Unicode scalar boundary and record the rule. / 在 Unicode 标量边界截断并记录规则。</summary>
        Truncate = 1
    }

    /// <summary>Describes one deterministic exact replacement applied after built-in normalization. / 描述一个在内置规范化之后执行的确定性精确替换。</summary>
    public sealed class OcrTextReplacementRule
    {
        /// <summary>Initializes a replacement rule. / 初始化替换规则。</summary>
        public OcrTextReplacementRule(string id, string source, string replacement)
        {
            Id = VisualGuard.Identifier(id, nameof(id));
            if (string.IsNullOrEmpty(source)) throw new ArgumentException("A replacement source is required.", nameof(source));
            if (source.Length > 256) throw new ArgumentOutOfRangeException(nameof(source));
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (replacement.Length > 1024) throw new ArgumentOutOfRangeException(nameof(replacement));
            Source = source;
            Replacement = replacement;
        }

        /// <summary>Gets the stable rule identifier. / 获取稳定规则标识符。</summary>
        public string Id { get; }
        /// <summary>Gets the exact source sequence. / 获取精确源序列。</summary>
        public string Source { get; }
        /// <summary>Gets the replacement sequence, which may be empty. / 获取可为空的替换序列。</summary>
        public string Replacement { get; }
    }

    /// <summary>Defines an opt-in, deterministic OCR text normalization policy. / 定义可选且确定性的 OCR 文本规范化策略。</summary>
    public sealed class OcrTextNormalizationOptions
    {
        private readonly IReadOnlyList<OcrTextReplacementRule> _replacements;

        /// <summary>Initializes text normalization options. Existing OCR results remain unchanged unless this policy is explicitly used. / 初始化文本规范化选项；除非显式使用此策略，否则既有 OCR 结果保持不变。</summary>
        public OcrTextNormalizationOptions(
            OcrUnicodeNormalizationForm unicodeForm = OcrUnicodeNormalizationForm.None,
            bool convertFullWidthAscii = false,
            bool normalizeLineEndings = false,
            bool trimWhitespace = false,
            bool collapseWhitespace = false,
            bool removeControlCharacters = false,
            IEnumerable<OcrTextReplacementRule>? replacements = null,
            int maximumLength = 0,
            OcrTextNormalizationOverflowMode overflowMode = OcrTextNormalizationOverflowMode.Reject)
        {
            if (!Enum.IsDefined(typeof(OcrUnicodeNormalizationForm), unicodeForm)) throw new ArgumentOutOfRangeException(nameof(unicodeForm));
            if (!Enum.IsDefined(typeof(OcrTextNormalizationOverflowMode), overflowMode)) throw new ArgumentOutOfRangeException(nameof(overflowMode));
            if (maximumLength < 0 || maximumLength > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumLength));
            UnicodeForm = unicodeForm;
            ConvertFullWidthAscii = convertFullWidthAscii;
            NormalizeLineEndings = normalizeLineEndings;
            TrimWhitespace = trimWhitespace;
            CollapseWhitespace = collapseWhitespace;
            RemoveControlCharacters = removeControlCharacters;
            MaximumLength = maximumLength;
            OverflowMode = overflowMode;

            var copy = new List<OcrTextReplacementRule>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (replacements != null)
            {
                foreach (OcrTextReplacementRule rule in replacements)
                {
                    if (rule == null) throw new ArgumentException("Replacement rules cannot contain null.", nameof(replacements));
                    if (copy.Count >= 64) throw new ArgumentOutOfRangeException(nameof(replacements));
                    if (!ids.Add(rule.Id)) throw new ArgumentException("Replacement rule identifiers must be unique.", nameof(replacements));
                    copy.Add(rule);
                }
            }
            _replacements = new ReadOnlyCollection<OcrTextReplacementRule>(copy);
            ConfigurationSha256 = ComputeConfigurationSha256();
        }

        /// <summary>Gets the Unicode normalization form. / 获取 Unicode 规范化形式。</summary>
        public OcrUnicodeNormalizationForm UnicodeForm { get; }
        /// <summary>Gets whether full-width ASCII and U+3000 are mapped to their narrow equivalents. / 获取是否将全角 ASCII 和 U+3000 映射为半角等价字符。</summary>
        public bool ConvertFullWidthAscii { get; }
        /// <summary>Gets whether CRLF and CR are converted to LF. / 获取是否将 CRLF 和 CR 转换为 LF。</summary>
        public bool NormalizeLineEndings { get; }
        /// <summary>Gets whether leading and trailing Unicode whitespace is removed. / 获取是否移除首尾 Unicode 空白。</summary>
        public bool TrimWhitespace { get; }
        /// <summary>Gets whether every Unicode whitespace run is replaced by one ASCII space. / 获取是否将每段 Unicode 空白替换为一个 ASCII 空格。</summary>
        public bool CollapseWhitespace { get; }
        /// <summary>Gets whether control characters other than tab and line endings are removed. / 获取是否移除除制表符和换行外的控制字符。</summary>
        public bool RemoveControlCharacters { get; }
        /// <summary>Gets exact replacements applied in declaration order. / 获取按声明顺序执行的精确替换。</summary>
        public IReadOnlyList<OcrTextReplacementRule> Replacements => _replacements;
        /// <summary>Gets the maximum number of Unicode scalars; zero means unlimited. / 获取 Unicode 标量最大数量；零表示不限制。</summary>
        public int MaximumLength { get; }
        /// <summary>Gets the over-limit policy. / 获取超限策略。</summary>
        public OcrTextNormalizationOverflowMode OverflowMode { get; }
        /// <summary>Gets a stable hash of every configured normalization option and replacement. / 获取所有规范化选项与替换配置的稳定哈希。</summary>
        public string ConfigurationSha256 { get; }

        private string ComputeConfigurationSha256()
        {
            var builder = new StringBuilder();
            builder.Append("unicode=").Append((int)UnicodeForm).Append(';');
            builder.Append("width=").Append(ConvertFullWidthAscii ? '1' : '0').Append(';');
            builder.Append("lines=").Append(NormalizeLineEndings ? '1' : '0').Append(';');
            builder.Append("trim=").Append(TrimWhitespace ? '1' : '0').Append(';');
            builder.Append("collapse=").Append(CollapseWhitespace ? '1' : '0').Append(';');
            builder.Append("controls=").Append(RemoveControlCharacters ? '1' : '0').Append(';');
            builder.Append("max=").Append(MaximumLength).Append(';');
            builder.Append("overflow=").Append((int)OverflowMode).Append(';');
            foreach (OcrTextReplacementRule rule in _replacements)
            {
                AppendLengthPrefixed(builder, rule.Id);
                AppendLengthPrefixed(builder, rule.Source);
                AppendLengthPrefixed(builder, rule.Replacement);
            }
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void AppendLengthPrefixed(StringBuilder builder, string value)
            => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Contains raw and normalized OCR text with deterministic provenance. / 包含带确定性来源信息的原始与规范化 OCR 文本。</summary>
    public sealed class OcrTextNormalizationResult
    {
        private readonly IReadOnlyList<string> _appliedRules;

        internal OcrTextNormalizationResult(string rawText, string normalizedText, IEnumerable<string> appliedRules, bool truncated, string configurationSha256, int? sourceRegionIndex)
        {
            RawText = rawText ?? throw new ArgumentNullException(nameof(rawText));
            NormalizedText = normalizedText ?? throw new ArgumentNullException(nameof(normalizedText));
            if (appliedRules == null) throw new ArgumentNullException(nameof(appliedRules));
            var copy = new List<string>();
            foreach (string rule in appliedRules) copy.Add(string.IsNullOrEmpty(rule) ? throw new ArgumentException("Applied rule identifiers cannot be empty.", nameof(appliedRules)) : rule);
            _appliedRules = new ReadOnlyCollection<string>(copy);
            Truncated = truncated;
            ConfigurationSha256 = configurationSha256 ?? throw new ArgumentNullException(nameof(configurationSha256));
            SourceRegionIndex = sourceRegionIndex;
            OriginalLength = CountUnicodeScalars(RawText);
            NormalizedLength = CountUnicodeScalars(NormalizedText);
        }

        /// <summary>Gets the original decoder text. / 获取解码器原始文本。</summary>
        public string RawText { get; }
        /// <summary>Gets the normalized text consumed by the caller. / 获取供调用方使用的规范化文本。</summary>
        public string NormalizedText { get; }
        /// <summary>Gets whether the normalized value differs from the raw value. / 获取规范化值是否不同于原始值。</summary>
        public bool Changed => !string.Equals(RawText, NormalizedText, StringComparison.Ordinal);
        /// <summary>Gets whether the output was truncated at a scalar boundary. / 获取输出是否在标量边界被截断。</summary>
        public bool Truncated { get; }
        /// <summary>Gets the ordered built-in or custom rules that changed the value. / 获取实际改变文本的内置或自定义规则顺序。</summary>
        public IReadOnlyList<string> AppliedRules => _appliedRules;
        /// <summary>Gets the source OCR region index when normalized from a recognition result. / 从识别结果规范化时获取源 OCR 区域索引。</summary>
        public int? SourceRegionIndex { get; }
        /// <summary>Gets the original Unicode scalar count. / 获取原始 Unicode 标量数量。</summary>
        public int OriginalLength { get; }
        /// <summary>Gets the normalized Unicode scalar count. / 获取规范化 Unicode 标量数量。</summary>
        public int NormalizedLength { get; }
        /// <summary>Gets the hash of the configured policy, including rules that did not match. / 获取配置策略哈希，包括未匹配的规则。</summary>
        public string ConfigurationSha256 { get; }

        /// <summary>Computes a stable hash over raw text, normalized text, policy, and applied rules. / 对原始文本、规范化文本、策略和实际规则计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            AppendLengthPrefixed(builder, RawText);
            AppendLengthPrefixed(builder, NormalizedText);
            AppendLengthPrefixed(builder, ConfigurationSha256);
            builder.Append(Truncated ? '1' : '0').Append(';').Append(SourceRegionIndex.GetValueOrDefault(-1)).Append(';');
            foreach (string rule in _appliedRules) AppendLengthPrefixed(builder, rule);
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void AppendLengthPrefixed(StringBuilder builder, string value)
            => builder.Append(value.Length).Append(':').Append(value).Append('|');

        private static int CountUnicodeScalars(string value)
        {
            int count = 0;
            for (int index = 0; index < value.Length; index++, count++)
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index++;
            return count;
        }
    }

    /// <summary>Pairs one source OCR region with its non-mutating normalized text. / 将一个源 OCR 区域与不修改源结果的规范化文本配对。</summary>
    public sealed class OcrNormalizedRegionResult
    {
        internal OcrNormalizedRegionResult(OcrRegionResult region, OcrTextNormalizationResult text)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>Gets the unchanged source region/result pair. / 获取未修改的源区域/结果对。</summary>
        public OcrRegionResult Region { get; }
        /// <summary>Gets raw and normalized text provenance for this region. / 获取此区域的原始与规范化文本来源信息。</summary>
        public OcrTextNormalizationResult Text { get; }
    }

    /// <summary>Contains a normalized view of an OCR result without changing the canonical OCR result or its hash. / 包含不改变规范 OCR 结果及其哈希的规范化视图。</summary>
    public sealed class OcrNormalizedResult
    {
        private readonly IReadOnlyList<OcrNormalizedRegionResult> _regions;

        internal OcrNormalizedResult(OcrResult source, IEnumerable<OcrNormalizedRegionResult> regions, OcrTextNormalizationOptions options)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            var copy = new List<OcrNormalizedRegionResult>();
            foreach (OcrNormalizedRegionResult region in regions) copy.Add(region ?? throw new ArgumentException("Normalized regions cannot contain null.", nameof(regions)));
            _regions = new ReadOnlyCollection<OcrNormalizedRegionResult>(copy);
            ConfigurationSha256 = Options.ConfigurationSha256;
        }

        /// <summary>Gets the original immutable OCR result. / 获取原始不可变 OCR 结果。</summary>
        public OcrResult Source { get; }
        /// <summary>Gets normalized regions in the original reading order. / 获取保持原阅读顺序的规范化区域。</summary>
        public IReadOnlyList<OcrNormalizedRegionResult> Regions => _regions;
        /// <summary>Gets the policy configuration hash. / 获取策略配置哈希。</summary>
        public string ConfigurationSha256 { get; }
        /// <summary>Gets the immutable policy used to create this view. / 获取创建此视图所使用的不可变策略。</summary>
        public OcrTextNormalizationOptions Options { get; }

        /// <summary>Computes a stable hash over the source OCR result and normalized view. / 对源 OCR 结果与规范化视图计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            AppendLengthPrefixed(builder, Source.ComputeSha256());
            AppendLengthPrefixed(builder, ConfigurationSha256);
            foreach (OcrNormalizedRegionResult region in _regions)
            {
                builder.Append(region.Region.Region.SourceIndex).Append(';');
                AppendLengthPrefixed(builder, region.Text.ComputeSha256());
            }
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void AppendLengthPrefixed(StringBuilder builder, string value)
            => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Applies the bounded OCR text normalization policy without mutating decoder results. / 在不修改解码结果的前提下应用有界 OCR 文本规范化策略。</summary>
    public static class OcrTextNormalizer
    {
        /// <summary>Normalizes a standalone string. / 规范化独立字符串。</summary>
        public static OcrTextNormalizationResult Normalize(string rawText, OcrTextNormalizationOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => NormalizeCore(rawText ?? throw new ArgumentNullException(nameof(rawText)), options ?? throw new ArgumentNullException(nameof(options)), null, cancellationToken);

        /// <summary>Normalizes one recognition result while retaining its source region index. / 规范化单个识别结果并保留其源区域索引。</summary>
        public static OcrTextNormalizationResult Normalize(RecognizedText recognition, OcrTextNormalizationOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (recognition == null) throw new ArgumentNullException(nameof(recognition));
            return NormalizeCore(recognition.Text, options ?? throw new ArgumentNullException(nameof(options)), recognition.SourceRegionIndex, cancellationToken);
        }

        /// <summary>Creates a normalized view of every OCR region in reading order; the source result remains unchanged. / 按阅读顺序创建全部 OCR 区域的规范化视图；源结果保持不变。</summary>
        public static OcrNormalizedResult Normalize(OcrResult result, OcrTextNormalizationOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var regions = new List<OcrNormalizedRegionResult>(result.Regions.Count);
            foreach (OcrRegionResult region in result.Regions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                regions.Add(new OcrNormalizedRegionResult(region, Normalize(region.Recognition, options, cancellationToken)));
            }
            return new OcrNormalizedResult(result, regions, options);
        }

        private static OcrTextNormalizationResult NormalizeCore(string rawText, OcrTextNormalizationOptions options, int? sourceRegionIndex, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = rawText;
            var applied = new List<string>();

            if (options.UnicodeForm != OcrUnicodeNormalizationForm.None)
            {
                string normalized = current.Normalize(options.UnicodeForm == OcrUnicodeNormalizationForm.Nfc ? NormalizationForm.FormC : NormalizationForm.FormKC);
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add(options.UnicodeForm == OcrUnicodeNormalizationForm.Nfc ? "unicode:nfc" : "unicode:nfkc"); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (options.ConvertFullWidthAscii)
            {
                string normalized = ConvertFullWidthAscii(current);
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add("width:fullwidth-ascii"); }
            }
            if (options.NormalizeLineEndings)
            {
                string normalized = NormalizeLineEndings(current);
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add("line-endings:lf"); }
            }
            if (options.RemoveControlCharacters)
            {
                string normalized = RemoveControlCharacters(current);
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add("controls:remove"); }
            }
            if (options.CollapseWhitespace)
            {
                string normalized = CollapseWhitespace(current);
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add("whitespace:collapse"); }
            }
            if (options.TrimWhitespace)
            {
                string normalized = current.Trim();
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add("whitespace:trim"); }
            }
            foreach (OcrTextReplacementRule replacement in options.Replacements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string normalized = current.Replace(replacement.Source, replacement.Replacement);
                if (!string.Equals(normalized, current, StringComparison.Ordinal)) { current = normalized; applied.Add("replacement:" + replacement.Id); }
            }

            bool truncated = false;
            if (options.MaximumLength > 0)
            {
                int length = CountUnicodeScalars(current);
                if (length > options.MaximumLength)
                {
                    if (options.OverflowMode == OcrTextNormalizationOverflowMode.Reject)
                        throw new VisualException(VisualErrorCodes.OcrTextNormalizationLimitExceeded, "Normalized OCR text exceeds its configured scalar limit.", technicalDetails: "length=" + length + ";maximum=" + options.MaximumLength + ";configurationSha256=" + options.ConfigurationSha256);
                    current = TruncateUnicodeScalars(current, options.MaximumLength);
                    truncated = true;
                    applied.Add("length:truncate");
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new OcrTextNormalizationResult(rawText, current, applied, truncated, options.ConfigurationSha256, sourceRegionIndex);
        }

        private static string ConvertFullWidthAscii(string value)
        {
            StringBuilder? builder = null;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                char mapped = current >= '\uff01' && current <= '\uff5e' ? (char)(current - 0xfee0) : current == '\u3000' ? ' ' : current;
                if (mapped != current && builder == null)
                {
                    builder = new StringBuilder(value.Length);
                    if (index != 0) builder.Append(value, 0, index);
                }
                if (builder != null) builder.Append(mapped);
            }
            return builder == null ? value : builder.ToString();
        }

        private static string NormalizeLineEndings(string value)
        {
            StringBuilder? builder = null;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current != '\r') { if (builder != null) builder.Append(current); continue; }
                if (builder == null)
                {
                    builder = new StringBuilder(value.Length);
                    if (index != 0) builder.Append(value, 0, index);
                }
                if (index + 1 < value.Length && value[index + 1] == '\n') index++;
                builder.Append('\n');
            }
            return builder == null ? value : builder.ToString();
        }

        private static string RemoveControlCharacters(string value)
        {
            StringBuilder? builder = null;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool remove = char.IsControl(current) && current != '\r' && current != '\n' && current != '\t';
                if (remove && builder == null)
                {
                    builder = new StringBuilder(value.Length);
                    if (index != 0) builder.Append(value, 0, index);
                }
                if (!remove && builder != null) builder.Append(current);
            }
            return builder == null ? value : builder.ToString();
        }

        private static string CollapseWhitespace(string value)
        {
            bool changed = false;
            bool whitespace = false;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (char.IsWhiteSpace(current))
                {
                    if (whitespace || current != ' ') { changed = true; break; }
                    whitespace = true;
                }
                else
                {
                    whitespace = false;
                }
            }
            if (!changed) return value;
            var builder = new StringBuilder(value.Length);
            whitespace = false;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (char.IsWhiteSpace(current))
                {
                    if (!whitespace) builder.Append(' ');
                    whitespace = true;
                }
                else
                {
                    builder.Append(current);
                    whitespace = false;
                }
            }
            return builder.ToString();
        }

        private static string TruncateUnicodeScalars(string value, int maximumLength)
        {
            if (maximumLength <= 0) return string.Empty;
            int scalars = 0;
            int end = 0;
            for (int index = 0; index < value.Length && scalars < maximumLength; index++, scalars++)
            {
                end = index + 1;
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    index++;
                    end = index + 1;
                }
            }
            if (scalars < maximumLength) return value;
            return value.Substring(0, end);
        }

        private static int CountUnicodeScalars(string value)
        {
            int count = 0;
            for (int index = 0; index < value.Length; index++, count++)
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index++;
            return count;
        }
    }
}
