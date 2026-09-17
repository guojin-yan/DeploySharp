using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Records one repeated dictionary entry without modifying its index. / 记录一个重复字典项，不修改其索引。</summary>
    public sealed class OcrDuplicateToken
    {
        internal OcrDuplicateToken(string token, int firstIndex, int duplicateIndex)
        { Token = token; FirstCharacterIndex = firstIndex; DuplicateCharacterIndex = duplicateIndex; }

        /// <summary>Gets the exact token text. / 获取精确 token 文本。</summary>
        public string Token { get; }
        /// <summary>Gets the first zero-based dictionary index, excluding reserved CTC classes. / 获取首个零起始字典索引，不含 CTC 保留类别。</summary>
        public int FirstCharacterIndex { get; }
        /// <summary>Gets the repeated zero-based dictionary index. / 获取重复项的零起始字典索引。</summary>
        public int DuplicateCharacterIndex { get; }
    }

    /// <summary>Describes a required Unicode scalar absent as an independent dictionary token. / 描述不能作为独立字典 token 输出的需求 Unicode 标量。</summary>
    public sealed class OcrMissingCharacter
    {
        internal OcrMissingCharacter(int scalar, int firstOffset, int occurrences, bool inCompoundToken)
        { Scalar = scalar; FirstUtf16Offset = firstOffset; Occurrences = occurrences; AppearsInCompoundToken = inCompoundToken; }

        /// <summary>Gets the Unicode scalar value. / 获取 Unicode 标量值。</summary>
        public int Scalar { get; }
        /// <summary>Gets the U+ hexadecimal label. / 获取 U+ 十六进制标记。</summary>
        public string CodePoint => "U+" + Scalar.ToString("X4", CultureInfo.InvariantCulture);
        /// <summary>Gets the original scalar text without normalization. / 获取未规范化的原始标量文本。</summary>
        public string Text => char.ConvertFromUtf32(Scalar);
        /// <summary>Gets the first UTF-16 offset in the required text. / 获取需求文本中首次出现的 UTF-16 偏移。</summary>
        public int FirstUtf16Offset { get; }
        /// <summary>Gets the number of occurrences in the required text. / 获取需求文本中的出现次数。</summary>
        public int Occurrences { get; }
        /// <summary>Gets whether the scalar occurs inside a multi-scalar token only. / 获取标量是否仅出现在多标量 token 内。</summary>
        public bool AppearsInCompoundToken { get; }
    }

    /// <summary>Contains immutable independent-character coverage evidence, not OCR accuracy. / 包含不可变的独立字符覆盖证据，不表示 OCR 准确率。</summary>
    public sealed class OcrCharacterCoverageReport
    {
        internal OcrCharacterCoverageReport(OcrCharacterSet characterSet, string mappingSha, string requiredSha, int scalarCount, int distinctCount, List<OcrMissingCharacter> missing)
        {
            CharacterSetId = characterSet.Id; CharacterSetVersion = characterSet.Version; CharacterSetSha256 = characterSet.Sha256;
            TokenMappingSha256 = mappingSha; RequiredTextSha256 = requiredSha; RequiredScalarCount = scalarCount;
            DistinctRequiredScalarCount = distinctCount; MissingCharacters = missing.AsReadOnly();
        }

        /// <summary>Gets character-set identity. / 获取字符表标识。</summary>
        public string CharacterSetId { get; }
        /// <summary>Gets character-set version. / 获取字符表版本。</summary>
        public string CharacterSetVersion { get; }
        /// <summary>Gets the existing compatibility hash. / 获取既有兼容哈希。</summary>
        public string CharacterSetSha256 { get; }
        /// <summary>Gets the length-framed ordered-token mapping hash. / 获取带长度边界的有序 token 映射哈希。</summary>
        public string TokenMappingSha256 { get; }
        /// <summary>Gets SHA256 of the exact UTF-8 requirement text. / 获取精确 UTF-8 需求文本的 SHA256。</summary>
        public string RequiredTextSha256 { get; }
        /// <summary>Gets total required Unicode scalars including repetitions. / 获取包含重复项的需求 Unicode 标量总数。</summary>
        public int RequiredScalarCount { get; }
        /// <summary>Gets distinct required scalar count. / 获取需求中不同标量的数量。</summary>
        public int DistinctRequiredScalarCount { get; }
        /// <summary>Gets missing independent characters sorted by scalar value. / 获取按标量值排序的缺失独立字符。</summary>
        public IReadOnlyList<OcrMissingCharacter> MissingCharacters { get; }
        /// <summary>Gets whether a nonempty requirement is fully covered. / 获取非空需求是否被完整覆盖。</summary>
        public bool IsCovered => RequiredScalarCount > 0 && MissingCharacters.Count == 0;
        /// <summary>Gets distinct-scalar coverage; null means no requirements. / 获取不同标量的覆盖率；null 表示没有需求。</summary>
        public double? Coverage => DistinctRequiredScalarCount == 0 ? (double?)null : (double)(DistinctRequiredScalarCount - MissingCharacters.Count) / DistinctRequiredScalarCount;

        /// <summary>Rejects missing or empty requirements before creating inference sessions. / 在创建推理会话前拒绝缺失或空需求。</summary>
        public void EnsureCovered()
        {
            if (!IsCovered) throw new VisualException(VisualErrorCodes.OcrCharacterCoverageMissing,
                "The dictionary does not cover the nonempty independent-character requirement.",
                technicalDetails: "characterSet=" + CharacterSetId + ";requiredDistinct=" + DistinctRequiredScalarCount.ToString(CultureInfo.InvariantCulture)
                    + ";missingDistinct=" + MissingCharacters.Count.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Builds a reusable, thread-safe character coverage index outside inference. / 在推理之外构建可复用、线程安全的字符覆盖索引。</summary>
    public sealed class OcrCharacterSetAuditor
    {
        private readonly OcrCharacterSet _characterSet;
        private readonly HashSet<int> _independent = new HashSet<int>();
        private readonly HashSet<int> _compound = new HashSet<int>();
        private readonly int _maximumRequiredUtf16Length;

        /// <summary>Indexes the unchanged dictionary within UTF-16 work bounds; blank/unknown are not dictionary characters. / 在 UTF-16 工作量限制内索引原字典；blank/unknown 不属于字典字符。</summary>
        public OcrCharacterSetAuditor(OcrCharacterSet characterSet, int maximumDictionaryUtf16Length = 4 * 1024 * 1024,
            int maximumRequiredUtf16Length = 65536, CancellationToken cancellationToken = default(CancellationToken))
        {
            _characterSet = characterSet ?? throw new ArgumentNullException(nameof(characterSet));
            if (maximumDictionaryUtf16Length <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDictionaryUtf16Length));
            if (maximumRequiredUtf16Length <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRequiredUtf16Length));
            _maximumRequiredUtf16Length = maximumRequiredUtf16Length;
            var firstIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var duplicates = new List<OcrDuplicateToken>();
            long length = 0;
            foreach (string token in characterSet.Characters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                length += token.Length;
                if (length > maximumDictionaryUtf16Length) throw new ArgumentOutOfRangeException(nameof(characterSet), "Dictionary exceeds the audit UTF-16 work bound.");
            }
            for (int index = 0; index < characterSet.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string token = characterSet.Characters[index];
                if (firstIndexes.TryGetValue(token, out int first)) duplicates.Add(new OcrDuplicateToken(token, first, index));
                else firstIndexes.Add(token, index);
                bool single = token.Length == (char.IsHighSurrogate(token[0]) ? 2 : 1);
                if (!single) CompoundTokenCount++;
                for (int offset = 0; offset < token.Length;)
                {
                    if ((offset & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    int scalar = ReadScalar(token, ref offset);
                    if (single) _independent.Add(scalar); else _compound.Add(scalar);
                }
            }
            DuplicateTokens = duplicates.AsReadOnly();
            TokenCount = characterSet.Count;
            TokenMappingSha256 = HashMapping(characterSet, cancellationToken);
        }

        /// <summary>Gets dictionary token count including duplicate entries. / 获取包含重复项的字典 token 数。</summary>
        public int TokenCount { get; }
        /// <summary>Gets multi-scalar token count including duplicates. / 获取包含重复项的多标量 token 数。</summary>
        public int CompoundTokenCount { get; }
        /// <summary>Gets distinct scalars available as independent tokens. / 获取可独立输出的不同标量数量。</summary>
        public int IndependentScalarCount => _independent.Count;
        /// <summary>Gets repeated entries in original dictionary-index order. / 获取按原字典索引排序的重复项。</summary>
        public IReadOnlyList<OcrDuplicateToken> DuplicateTokens { get; }
        /// <summary>Gets SHA256 over a version prefix, count and length-framed UTF-8 ordered tokens. / 获取版本前缀、数量及带长度边界的有序 UTF-8 token 的 SHA256。</summary>
        public string TokenMappingSha256 { get; }

        /// <summary>Audits independent scalar coverage without trimming, case folding or normalization. / 审计独立标量覆盖，不裁剪空白、不转换大小写、不规范化。</summary>
        public OcrCharacterCoverageReport Audit(string requiredCharacters, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (requiredCharacters == null) throw new ArgumentNullException(nameof(requiredCharacters));
            if (requiredCharacters.Length > _maximumRequiredUtf16Length) throw new ArgumentOutOfRangeException(nameof(requiredCharacters), "Requirements exceed the audit UTF-16 work bound.");
            cancellationToken.ThrowIfCancellationRequested();
            var unique = new HashSet<int>();
            var missing = new Dictionary<int, MissingCount>();
            int count = 0;
            for (int offset = 0; offset < requiredCharacters.Length;)
            {
                if ((count & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                int firstOffset = offset;
                int scalar = ReadScalar(requiredCharacters, ref offset);
                count++;
                unique.Add(scalar);
                if (!_independent.Contains(scalar))
                {
                    if (!missing.TryGetValue(scalar, out MissingCount? value)) missing.Add(scalar, value = new MissingCount(firstOffset));
                    value.Occurrences++;
                }
            }
            var sorted = new List<int>(missing.Keys);
            sorted.Sort();
            var items = new List<OcrMissingCharacter>(sorted.Count);
            foreach (int scalar in sorted)
                items.Add(new OcrMissingCharacter(scalar, missing[scalar].Offset, missing[scalar].Occurrences, _compound.Contains(scalar)));
            cancellationToken.ThrowIfCancellationRequested();
            using (SHA256 sha = SHA256.Create())
                return new OcrCharacterCoverageReport(_characterSet, TokenMappingSha256, OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(requiredCharacters))), count, unique.Count, items);
        }

        private static int ReadScalar(string text, ref int offset)
        {
            char first = text[offset++];
            if (char.IsHighSurrogate(first))
            {
                if (offset >= text.Length || !char.IsLowSurrogate(text[offset])) throw new ArgumentException("Audit text contains an unpaired high surrogate.", nameof(text));
                return char.ConvertToUtf32(first, text[offset++]);
            }
            if (char.IsLowSurrogate(first)) throw new ArgumentException("Audit text contains an unpaired low surrogate.", nameof(text));
            return first;
        }

        private static string HashMapping(OcrCharacterSet characters, CancellationToken cancellationToken)
        {
            using (SHA256 sha = SHA256.Create())
            {
                using (var stream = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
                {
                    writer.Write("DeploySharp.OcrTokenMapping/v1");
                    writer.Write(characters.Count);
                    foreach (string token in characters.Characters) { cancellationToken.ThrowIfCancellationRequested(); writer.Write(token); }
                    writer.Flush();
                    stream.FlushFinalBlock();
                    return OcrCharacterSet.Hex(sha.Hash!);
                }
            }
        }

        private sealed class MissingCount
        {
            internal MissingCount(int offset) { Offset = offset; }
            internal int Offset { get; }
            internal int Occurrences { get; set; }
        }
    }
}
