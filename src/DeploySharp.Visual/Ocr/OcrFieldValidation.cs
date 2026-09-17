using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies a built-in OCR field validation family. / 标识内置 OCR 字段校验类型。</summary>
    public enum OcrFieldKind
    {
        /// <summary>Calendar date without a time component. / 不含时间的日历日期。</summary>
        Date = 0,
        /// <summary>Signed decimal amount with an optional currency symbol. / 可带货币符号的有符号十进制金额。</summary>
        Amount = 1,
        /// <summary>Chinese mainland mobile or landline telephone number. / 中国大陆手机或座机号码。</summary>
        Phone = 2,
        /// <summary>Mainland Chinese resident identity card number. / 中国大陆居民身份证号码。</summary>
        ChineseIdCard = 3,
        /// <summary>HTTP or HTTPS URL. / HTTP 或 HTTPS URL。</summary>
        Url = 4,
        /// <summary>Internet email address. / 互联网电子邮箱地址。</summary>
        Email = 5,
        /// <summary>Configurable industrial identifier. / 可配置工业编号。</summary>
        Identifier = 6,
        /// <summary>Human-readable postal or site address heuristic. / 人类可读的邮寄或现场地址启发式校验。</summary>
        Address = 7,
        /// <summary>Common mainland Chinese invoice number formats. / 常见中国大陆发票号码格式。</summary>
        InvoiceNumber = 8,
        /// <summary>Configurable device serial number. / 可配置设备序列号。</summary>
        DeviceSerialNumber = 9
    }

    /// <summary>Identifies the outcome of a field validation. / 标识字段校验结果。</summary>
    public enum OcrFieldValidationStatus
    {
        /// <summary>The value passed the validator. / 值通过校验。</summary>
        Valid = 0,
        /// <summary>The value is present but violates the validator contract. / 值存在但违反校验合同。</summary>
        Invalid = 1,
        /// <summary>The normalized value is empty or whitespace-only. / 规范化值为空或仅含空白。</summary>
        Empty = 2,
        /// <summary>The validator cannot evaluate the requested field family. / 校验器无法评估请求的字段类型。</summary>
        Unsupported = 3
    }

    /// <summary>Accepts a normalized OCR text value and returns a bounded, traceable field result. / 接收规范化 OCR 文本并返回有界、可追溯的字段结果。</summary>
    public interface IOcrFieldValidator
    {
        /// <summary>Gets a stable validator identifier and version. / 获取稳定的校验器标识与版本。</summary>
        public string Id { get; }
        /// <summary>Gets the field family handled by this validator. / 获取此校验器处理的字段类型。</summary>
        public OcrFieldKind Kind { get; }
        /// <summary>Validates one normalized OCR value without mutating the input. / 校验一个规范化 OCR 值且不修改输入。</summary>
        public OcrFieldValidationResult Validate(OcrTextNormalizationResult text, CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>Contains one field validation outcome and its text provenance. / 包含一次字段校验结果及文本来源信息。</summary>
    public sealed class OcrFieldValidationResult
    {
        internal OcrFieldValidationResult(IOcrFieldValidator validator, OcrTextNormalizationResult text, OcrFieldValidationStatus status, string code, string message, string? canonicalValue)
        {
            ValidatorId = validator?.Id ?? throw new ArgumentNullException(nameof(validator));
            Kind = validator.Kind;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (!Enum.IsDefined(typeof(OcrFieldValidationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A field validation code is required.", nameof(code));
            if (message == null) throw new ArgumentNullException(nameof(message));
            Status = status;
            Code = code;
            Message = message;
            CanonicalValue = canonicalValue;
        }

        /// <summary>Gets the validator identity. / 获取校验器标识。</summary>
        public string ValidatorId { get; }
        /// <summary>Gets the validated field family. / 获取校验字段类型。</summary>
        public OcrFieldKind Kind { get; }
        /// <summary>Gets the full normalization result used as input. / 获取作为输入的完整规范化结果。</summary>
        public OcrTextNormalizationResult Text { get; }
        /// <summary>Gets the original decoder text. / 获取解码器原始文本。</summary>
        public string RawText => Text.RawText;
        /// <summary>Gets the normalized text checked by the validator. / 获取校验器检查的规范化文本。</summary>
        public string NormalizedText => Text.NormalizedText;
        /// <summary>Gets the source OCR region index, when available. / 获取可用时的源 OCR 区域索引。</summary>
        public int? SourceRegionIndex => Text.SourceRegionIndex;
        /// <summary>Gets the validation status. / 获取校验状态。</summary>
        public OcrFieldValidationStatus Status { get; }
        /// <summary>Gets a stable machine-readable reason code. / 获取稳定的机器可读原因码。</summary>
        public string Code { get; }
        /// <summary>Gets a human-readable diagnostic message. / 获取人类可读诊断消息。</summary>
        public string Message { get; }
        /// <summary>Gets an optional canonical representation. / 获取可选规范表示。</summary>
        public string? CanonicalValue { get; }
        /// <summary>Gets whether the value passed. / 获取值是否通过。</summary>
        public bool IsValid => Status == OcrFieldValidationStatus.Valid;

        /// <summary>Computes a stable hash over validator identity, text provenance, status, and canonical value. / 对校验器身份、文本来源、状态和规范值计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            Append(builder, ValidatorId);
            builder.Append((int)Kind).Append(';').Append((int)Status).Append(';');
            Append(builder, Code);
            Append(builder, Message);
            Append(builder, CanonicalValue ?? string.Empty);
            Append(builder, Text.ComputeSha256());
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Contains ordered outcomes from a bounded validator set. / 包含有界校验器集合的有序结果。</summary>
    public sealed class OcrFieldValidationReport
    {
        private readonly IReadOnlyList<OcrFieldValidationResult> _results;

        internal OcrFieldValidationReport(IEnumerable<OcrFieldValidationResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var copy = new List<OcrFieldValidationResult>();
            foreach (OcrFieldValidationResult result in results)
            {
                if (result == null) throw new ArgumentException("Field validation results cannot contain null.", nameof(results));
                if (copy.Count >= 64) throw new ArgumentOutOfRangeException(nameof(results));
                copy.Add(result);
            }
            _results = new ReadOnlyCollection<OcrFieldValidationResult>(copy);
        }

        /// <summary>Gets results in validator declaration order. / 获取按校验器声明顺序排列的结果。</summary>
        public IReadOnlyList<OcrFieldValidationResult> Results => _results;
        /// <summary>Gets whether every declared validator passed. / 获取是否所有声明的校验器均通过。</summary>
        public bool IsValid => _results.Count != 0 && ValidCount == _results.Count;
        /// <summary>Gets the number of valid values. / 获取通过数量。</summary>
        public int ValidCount => Count(OcrFieldValidationStatus.Valid);
        /// <summary>Gets the number of invalid values. / 获取无效数量。</summary>
        public int InvalidCount => Count(OcrFieldValidationStatus.Invalid);
        /// <summary>Gets the number of empty values. / 获取空值数量。</summary>
        public int EmptyCount => Count(OcrFieldValidationStatus.Empty);

        /// <summary>Computes a stable hash over ordered validation outcomes. / 对有序校验结果计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            foreach (OcrFieldValidationResult result in _results) { builder.Append(result.Kind).Append(';'); Append(builder, result.ComputeSha256()); }
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private int Count(OcrFieldValidationStatus status)
        {
            int count = 0;
            foreach (OcrFieldValidationResult result in _results) if (result.Status == status) count++;
            return count;
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Provides factory and bounded set execution for built-in OCR validators. / 为内置 OCR 校验器提供工厂与有界集合执行。</summary>
    public static class OcrFieldValidators
    {
        /// <summary>Creates the default validators for all supported field families. / 创建所有已支持字段类型的默认校验器。</summary>
        public static IReadOnlyList<IOcrFieldValidator> CreateDefault()
        {
            return new ReadOnlyCollection<IOcrFieldValidator>(new List<IOcrFieldValidator>
            {
                new DateOcrFieldValidator(), new AmountOcrFieldValidator(), new PhoneOcrFieldValidator(),
                new ChineseIdCardOcrFieldValidator(), new UrlOcrFieldValidator(), new EmailOcrFieldValidator(),
                new IdentifierOcrFieldValidator(), new AddressOcrFieldValidator(), new InvoiceNumberOcrFieldValidator(),
                new DeviceSerialNumberOcrFieldValidator()
            });
        }

        /// <summary>Creates one built-in validator. / 创建一个内置校验器。</summary>
        public static IOcrFieldValidator Create(OcrFieldKind kind)
        {
            switch (kind)
            {
                case OcrFieldKind.Date: return new DateOcrFieldValidator();
                case OcrFieldKind.Amount: return new AmountOcrFieldValidator();
                case OcrFieldKind.Phone: return new PhoneOcrFieldValidator();
                case OcrFieldKind.ChineseIdCard: return new ChineseIdCardOcrFieldValidator();
                case OcrFieldKind.Url: return new UrlOcrFieldValidator();
                case OcrFieldKind.Email: return new EmailOcrFieldValidator();
                case OcrFieldKind.Identifier: return new IdentifierOcrFieldValidator();
                case OcrFieldKind.Address: return new AddressOcrFieldValidator();
                case OcrFieldKind.InvoiceNumber: return new InvoiceNumberOcrFieldValidator();
                case OcrFieldKind.DeviceSerialNumber: return new DeviceSerialNumberOcrFieldValidator();
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>Runs a declared validator set against one normalized value. / 使用声明的校验器集合检查一个规范化值。</summary>
        public static OcrFieldValidationReport ValidateAll(OcrTextNormalizationResult text, IEnumerable<IOcrFieldValidator> validators, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (validators == null) throw new ArgumentNullException(nameof(validators));
            var results = new List<OcrFieldValidationResult>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (IOcrFieldValidator validator in validators)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (validator == null) throw new ArgumentException("Field validators cannot contain null.", nameof(validators));
                if (!ids.Add(validator.Id)) throw new ArgumentException("Field validator identifiers must be unique.", nameof(validators));
                if (results.Count >= 64) throw new ArgumentOutOfRangeException(nameof(validators));
                results.Add(validator.Validate(text, cancellationToken));
            }
            return new OcrFieldValidationReport(results);
        }
    }

    /// <summary>Provides the shared contract and diagnostics for built-in validators and custom extensions. / 为内置校验器与自定义扩展提供共享合同与诊断。</summary>
    public abstract class OcrFieldValidatorBase : IOcrFieldValidator
    {
        /// <summary>Initializes a validator identity. / 初始化校验器身份。</summary>
        protected OcrFieldValidatorBase(string id, OcrFieldKind kind)
        {
            Id = VisualGuard.Identifier(id, nameof(id));
            if (!Enum.IsDefined(typeof(OcrFieldKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
        }

        /// <inheritdoc />
        public string Id { get; }
        /// <inheritdoc />
        public OcrFieldKind Kind { get; }

        /// <inheritdoc />
        public OcrFieldValidationResult Validate(OcrTextNormalizationResult text, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text.NormalizedText)) return Create(text, OcrFieldValidationStatus.Empty, "empty", "The normalized OCR value is empty.", null);
            return ValidateCore(text, text.NormalizedText.Trim(), cancellationToken);
        }

        /// <summary>Validates a prepared value in its normalized form. / 以规范化形式校验准备好的值。</summary>
        protected abstract OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken);

        /// <summary>Creates a valid result with an optional canonical value. / 创建带可选规范值的通过结果。</summary>
        protected OcrFieldValidationResult Valid(OcrTextNormalizationResult text, string? canonicalValue = null, string message = "The value passed validation.")
            => Create(text, OcrFieldValidationStatus.Valid, "ok", message, canonicalValue ?? text.NormalizedText.Trim());

        /// <summary>Creates an invalid result with a stable reason code. / 使用稳定原因码创建无效结果。</summary>
        protected OcrFieldValidationResult Invalid(OcrTextNormalizationResult text, string code, string message)
            => Create(text, OcrFieldValidationStatus.Invalid, code, message, null);

        private OcrFieldValidationResult Create(OcrTextNormalizationResult text, OcrFieldValidationStatus status, string code, string message, string? canonicalValue)
            => new OcrFieldValidationResult(this, text, status, code, message, canonicalValue);
    }

    /// <summary>Validates common date-only formats and returns ISO-8601 date text. / 校验常见纯日期格式并返回 ISO-8601 日期文本。</summary>
    public sealed class DateOcrFieldValidator : OcrFieldValidatorBase
    {
        private static readonly string[] Formats = { "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d", "yyyy.MM.dd", "yyyy.M.d", "yyyy年M月d日", "yyyyMMdd" };

        /// <summary>Initializes the versioned date validator. / 初始化带版本的日期校验器。</summary>
        public DateOcrFieldValidator(string id = "ocr/date.v1") : base(id, OcrFieldKind.Date) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            DateTime parsed;
            foreach (string format in Formats)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                    return Valid(text, parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            return Invalid(text, "format", "The value is not a supported date-only format.");
        }
    }

    /// <summary>Validates bounded decimal amounts with optional currency and grouping separators. / 校验可带货币符号和分组分隔符的有界十进制金额。</summary>
    public sealed class AmountOcrFieldValidator : OcrFieldValidatorBase
    {
        private static readonly Regex Shape = new Regex(@"^[+-]?(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d{1,2})?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Initializes the versioned amount validator. / 初始化带版本的金额校验器。</summary>
        public AmountOcrFieldValidator(string id = "ocr/amount.v1") : base(id, OcrFieldKind.Amount) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string candidate = value;
            if (candidate.Length != 0 && IsCurrency(candidate[0])) candidate = candidate.Substring(1).Trim();
            if (candidate.Length != 0 && IsCurrency(candidate[candidate.Length - 1])) candidate = candidate.Substring(0, candidate.Length - 1).Trim();
            candidate = candidate.Replace(" ", string.Empty).Replace("\u00a0", string.Empty);
            if (!Shape.IsMatch(candidate)) return Invalid(text, "format", "The value is not a bounded decimal amount.");
            decimal amount;
            if (!decimal.TryParse(candidate, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out amount)) return Invalid(text, "range", "The amount is outside the supported decimal range.");
            return Valid(text, amount.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private static bool IsCurrency(char value) => value == '¥' || value == '￥' || value == '$' || value == '€' || value == '£';
    }

    /// <summary>Validates Chinese mainland mobile and landline phone formats. / 校验中国大陆手机和座机号码格式。</summary>
    public sealed class PhoneOcrFieldValidator : OcrFieldValidatorBase
    {
        private static readonly Regex Pattern = new Regex(@"^(?:(?:\+?86)[ -]?)?1[3-9]\d{9}|(?:(?:\+?86)[ -]?)?0\d{2,3}[ -]?\d{7,8}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Initializes the versioned phone validator. / 初始化带版本的电话校验器。</summary>
        public PhoneOcrFieldValidator(string id = "ocr/phone-cn.v1") : base(id, OcrFieldKind.Phone) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string candidate = value.Replace("(", string.Empty).Replace(")", string.Empty);
            return Pattern.IsMatch(candidate) ? Valid(text, candidate) : Invalid(text, "format", "The value is not a supported mainland China phone format.");
        }
    }

    /// <summary>Validates 18- or legacy 15-digit mainland Chinese identity cards, including checksum. / 校验含校验码的中国大陆 18 位或旧 15 位身份证号码。</summary>
    public sealed class ChineseIdCardOcrFieldValidator : OcrFieldValidatorBase
    {
        private static readonly int[] Weights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
        private static readonly char[] Checks = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };

        /// <summary>Initializes the versioned identity-card validator. / 初始化带版本的身份证校验器。</summary>
        public ChineseIdCardOcrFieldValidator(string id = "ocr/chinese-id.v1") : base(id, OcrFieldKind.ChineseIdCard) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string candidate = value.Replace(" ", string.Empty).Replace("\u00a0", string.Empty).ToUpperInvariant();
            if (candidate.Length == 15)
            {
                for (int index = 0; index < candidate.Length; index++) if (!char.IsDigit(candidate[index])) return Invalid(text, "format", "A legacy identity card must contain 15 digits.");
                candidate = candidate.Substring(0, 6) + "19" + candidate.Substring(6);
            }
            if (candidate.Length != 18) return Invalid(text, "format", "A Chinese identity card must contain 18 digits and a checksum character.");
            for (int index = 0; index < 17; index++) if (!char.IsDigit(candidate[index])) return Invalid(text, "format", "The identity card body contains a non-digit character.");
            if (!(char.IsDigit(candidate[17]) || candidate[17] == 'X')) return Invalid(text, "format", "The identity card checksum character is invalid.");
            DateTime birth;
            if (!DateTime.TryParseExact(candidate.Substring(6, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out birth)) return Invalid(text, "date", "The identity card birth date is invalid.");
            int sum = 0;
            for (int index = 0; index < 17; index++) sum += (candidate[index] - '0') * Weights[index];
            if (Checks[sum % 11] != candidate[17]) return Invalid(text, "checksum", "The identity card checksum is invalid.");
            return Valid(text, candidate);
        }
    }

    /// <summary>Validates absolute HTTP and HTTPS URLs with a non-empty host. / 校验具有非空主机的绝对 HTTP/HTTPS URL。</summary>
    public sealed class UrlOcrFieldValidator : OcrFieldValidatorBase
    {
        /// <summary>Initializes the versioned URL validator. / 初始化带版本的 URL 校验器。</summary>
        public UrlOcrFieldValidator(string id = "ocr/url.v1") : base(id, OcrFieldKind.Url) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Length > 2048 || OcrFieldValidationHelpers.ContainsWhitespaceOrControl(value)) return Invalid(text, "format", "The URL contains whitespace, control characters, or exceeds 2048 characters.");
            Uri? uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri == null || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || string.IsNullOrEmpty(uri.Host)) return Invalid(text, "format", "The value is not an absolute HTTP or HTTPS URL.");
            return Valid(text, uri.AbsoluteUri);
        }
    }

    /// <summary>Validates a bounded conventional email address shape. / 校验有界的常规电子邮箱格式。</summary>
    public sealed class EmailOcrFieldValidator : OcrFieldValidatorBase
    {
        private static readonly Regex Pattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Initializes the versioned email validator. / 初始化带版本的邮箱校验器。</summary>
        public EmailOcrFieldValidator(string id = "ocr/email.v1") : base(id, OcrFieldKind.Email) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Length > 254 || value.IndexOf("..", StringComparison.Ordinal) >= 0 || !Pattern.IsMatch(value)) return Invalid(text, "format", "The value is not a bounded conventional email address.");
            return Valid(text, value);
        }
    }

    /// <summary>Validates an industrial identifier made from letters, digits, and safe separators. / 校验由字母、数字和安全分隔符组成的工业编号。</summary>
    public class IdentifierOcrFieldValidator : OcrFieldValidatorBase
    {
        /// <summary>Initializes an identifier validator. / 初始化编号校验器。</summary>
        public IdentifierOcrFieldValidator(int minimumLength = 1, int maximumLength = 128, bool allowUnicodeLetters = false, string id = "ocr/identifier.v1")
            : this(OcrFieldKind.Identifier, minimumLength, maximumLength, allowUnicodeLetters, id)
        {
        }

        /// <summary>Initializes an identifier validator for a derived field family. / 为派生字段类型初始化编号校验器。</summary>
        protected IdentifierOcrFieldValidator(OcrFieldKind kind, int minimumLength, int maximumLength, bool allowUnicodeLetters, string id)
            : base(id, kind)
        {
            if (minimumLength <= 0 || maximumLength < minimumLength || maximumLength > 4096) throw new ArgumentOutOfRangeException(nameof(minimumLength));
            MinimumLength = minimumLength;
            MaximumLength = maximumLength;
            AllowUnicodeLetters = allowUnicodeLetters;
        }

        /// <summary>Gets the minimum scalar length. / 获取最小标量长度。</summary>
        public int MinimumLength { get; }
        /// <summary>Gets the maximum scalar length. / 获取最大标量长度。</summary>
        public int MaximumLength { get; }
        /// <summary>Gets whether Unicode letters are accepted. / 获取是否接受 Unicode 字母。</summary>
        public bool AllowUnicodeLetters { get; }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = 0;
            bool alphanumeric = false;
            for (int index = 0; index < value.Length; index++, length++)
            {
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) { index++; continue; }
                char current = value[index];
                bool letterOrDigit = AllowUnicodeLetters ? char.IsLetterOrDigit(current) : IsAsciiLetterOrDigit(current);
                if (!letterOrDigit && current != '_' && current != '-' && current != '.' && current != '/') return Invalid(text, "characters", "The identifier contains an unsupported character.");
                if (letterOrDigit) alphanumeric = true;
            }
            if (length < MinimumLength || length > MaximumLength) return Invalid(text, "length", "The identifier length is outside the configured bounds.");
            return alphanumeric ? Valid(text, value) : Invalid(text, "format", "The identifier must contain at least one letter or digit.");
        }

        private static bool IsAsciiLetterOrDigit(char value) => (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9');
    }

    /// <summary>Validates an address using bounded content and control-character checks; it is not postal-address verification. / 使用有界内容与控制字符检查地址；这不是邮政地址真实性校验。</summary>
    public sealed class AddressOcrFieldValidator : OcrFieldValidatorBase
    {
        /// <summary>Initializes an address heuristic. / 初始化地址启发式校验器。</summary>
        public AddressOcrFieldValidator(int minimumLength = 2, int maximumLength = 200, string id = "ocr/address.v1") : base(id, OcrFieldKind.Address)
        {
            if (minimumLength <= 0 || maximumLength < minimumLength || maximumLength > 4096) throw new ArgumentOutOfRangeException(nameof(minimumLength));
            MinimumLength = minimumLength;
            MaximumLength = maximumLength;
        }

        /// <summary>Gets the minimum scalar length. / 获取最小标量长度。</summary>
        public int MinimumLength { get; }
        /// <summary>Gets the maximum scalar length. / 获取最大标量长度。</summary>
        public int MaximumLength { get; }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (OcrFieldValidationHelpers.ContainsControlExceptWhitespace(value)) return Invalid(text, "characters", "The address contains a control character.");
            int length = OcrFieldValidationHelpers.CountScalars(value);
            bool content = false;
            for (int index = 0; index < value.Length; index++) if (char.IsLetterOrDigit(value[index])) { content = true; break; }
            if (!content || length < MinimumLength || length > MaximumLength) return Invalid(text, "format", "The address does not meet its bounded content and length rules.");
            return Valid(text, value);
        }
    }

    /// <summary>Validates common eight- or twenty-digit mainland Chinese invoice numbers. / 校验常见八位或二十位中国大陆发票号码。</summary>
    public sealed class InvoiceNumberOcrFieldValidator : OcrFieldValidatorBase
    {
        /// <summary>Initializes the versioned invoice-number validator. / 初始化带版本的发票号码校验器。</summary>
        public InvoiceNumberOcrFieldValidator(string id = "ocr/invoice-number.v1") : base(id, OcrFieldKind.InvoiceNumber) { }

        /// <inheritdoc />
        protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string candidate = value.Replace(" ", string.Empty).Replace("\u00a0", string.Empty).ToUpperInvariant();
            if (candidate.Length != 8 && candidate.Length != 20) return Invalid(text, "length", "The invoice number must contain 8 or 20 characters.");
            for (int index = 0; index < candidate.Length; index++) if (!IsAsciiLetterOrDigit(candidate[index])) return Invalid(text, "characters", "The invoice number contains an unsupported character.");
            return Valid(text, candidate);
        }

        private static bool IsAsciiLetterOrDigit(char value) => (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9');
    }

    /// <summary>Validates device serial numbers using the industrial identifier rules with a safer minimum length. / 使用工业编号规则并提高最小长度校验设备序列号。</summary>
    public sealed class DeviceSerialNumberOcrFieldValidator : IdentifierOcrFieldValidator
    {
        /// <summary>Initializes a device serial-number validator. / 初始化设备序列号校验器。</summary>
        public DeviceSerialNumberOcrFieldValidator(int minimumLength = 4, int maximumLength = 128, bool allowUnicodeLetters = false, string id = "ocr/device-serial.v1")
            : base(OcrFieldKind.DeviceSerialNumber, minimumLength, maximumLength, allowUnicodeLetters, id)
        {
        }
    }

    internal static class OcrFieldValidationHelpers
    {
        internal static bool ContainsWhitespaceOrControl(string value)
        {
            for (int index = 0; index < value.Length; index++) if (char.IsWhiteSpace(value[index]) || char.IsControl(value[index])) return true;
            return false;
        }

        internal static bool ContainsControlExceptWhitespace(string value)
        {
            for (int index = 0; index < value.Length; index++) if (char.IsControl(value[index]) && !char.IsWhiteSpace(value[index])) return true;
            return false;
        }

        internal static int CountScalars(string value)
        {
            int count = 0;
            for (int index = 0; index < value.Length; index++, count++) if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) index++;
            return count;
        }
    }

}
