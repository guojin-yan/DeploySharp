using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the outcome of a cross-field consistency rule. / 标识跨字段一致性规则的结果。</summary>
    public enum OcrFieldConsistencyStatus
    {
        /// <summary>All required values satisfy the rule. / 所有必需值均满足规则。</summary>
        Consistent = 0,
        /// <summary>All required values exist but violate the rule. / 所有必需值存在但违反规则。</summary>
        Inconsistent = 1,
        /// <summary>At least one required field is missing. / 至少一个必需字段缺失。</summary>
        Missing = 2,
        /// <summary>A required field exists but its format validation did not pass. / 必需字段存在但格式校验未通过。</summary>
        InvalidDependency = 3
    }

    /// <summary>Associates a stable application field name with one OCR validation result. / 将稳定的应用字段名与一次 OCR 校验结果关联。</summary>
    public sealed class OcrFieldBinding
    {
        /// <summary>Initializes a named field binding. / 初始化命名字段绑定。</summary>
        public OcrFieldBinding(string name, OcrFieldValidationResult validation)
        {
            Name = VisualGuard.Identifier(name, nameof(name));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        }

        /// <summary>Gets the normalized stable field name. / 获取规范化后的稳定字段名。</summary>
        public string Name { get; }
        /// <summary>Gets the immutable field validation result. / 获取不可变字段校验结果。</summary>
        public OcrFieldValidationResult Validation { get; }
    }

    /// <summary>Provides an immutable, bounded lookup context for cross-field rules. / 为跨字段规则提供不可变且有界的查找上下文。</summary>
    public sealed class OcrFieldConsistencyContext
    {
        private readonly IReadOnlyList<OcrFieldBinding> _fields;
        private readonly Dictionary<string, OcrFieldValidationResult> _lookup;

        /// <summary>Initializes a field context. / 初始化字段上下文。</summary>
        public OcrFieldConsistencyContext(IEnumerable<OcrFieldBinding> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            var copy = new List<OcrFieldBinding>();
            _lookup = new Dictionary<string, OcrFieldValidationResult>(StringComparer.Ordinal);
            foreach (OcrFieldBinding field in fields)
            {
                if (field == null) throw new ArgumentException("Field bindings cannot contain null.", nameof(fields));
                if (copy.Count >= 128) throw new ArgumentOutOfRangeException(nameof(fields));
                if (_lookup.ContainsKey(field.Name)) throw new ArgumentException("Field names must be unique.", nameof(fields));
                _lookup.Add(field.Name, field.Validation);
                copy.Add(field);
            }
            _fields = new ReadOnlyCollection<OcrFieldBinding>(copy);
        }

        /// <summary>Gets fields in declaration order. / 获取按声明顺序排列的字段。</summary>
        public IReadOnlyList<OcrFieldBinding> Fields => _fields;

        /// <summary>Looks up a field by its stable name. / 按稳定名称查找字段。</summary>
        public bool TryGet(string name, out OcrFieldValidationResult? validation)
        {
            string normalized = VisualGuard.Identifier(name, nameof(name));
            return _lookup.TryGetValue(normalized, out validation);
        }

        /// <summary>Computes a stable hash over field names and their validation provenance. / 对字段名及其校验来源计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            foreach (OcrFieldBinding field in _fields)
            {
                Append(builder, field.Name);
                Append(builder, field.Validation.ComputeSha256());
            }
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Evaluates one bounded relation over named OCR fields. / 在命名 OCR 字段上评估一个有界关系。</summary>
    public interface IOcrFieldConsistencyRule
    {
        /// <summary>Gets the stable rule identifier and version. / 获取稳定的规则标识与版本。</summary>
        public string Id { get; }
        /// <summary>Gets fields read by this rule in deterministic order. / 获取此规则按确定性顺序读取的字段。</summary>
        public IReadOnlyList<string> FieldNames { get; }
        /// <summary>Evaluates the rule without mutating the context. / 评估规则且不修改上下文。</summary>
        public OcrFieldConsistencyResult Evaluate(OcrFieldConsistencyContext context, CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>Provides bounded result creation for custom consistency rules. / 为自定义一致性规则提供有界结果创建支持。</summary>
    public abstract class OcrFieldConsistencyRuleBase : IOcrFieldConsistencyRule
    {
        private readonly IReadOnlyList<string> _fieldNames;

        /// <summary>Initializes a consistency rule. / 初始化一致性规则。</summary>
        protected OcrFieldConsistencyRuleBase(string id, IEnumerable<string> fieldNames)
        {
            Id = VisualGuard.Identifier(id, nameof(id));
            if (fieldNames == null) throw new ArgumentNullException(nameof(fieldNames));
            var copy = new List<string>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string fieldName in fieldNames)
            {
                if (copy.Count >= 64) throw new ArgumentOutOfRangeException(nameof(fieldNames));
                string normalized = VisualGuard.Identifier(fieldName, nameof(fieldNames));
                if (!names.Add(normalized)) throw new ArgumentException("Rule field names must be unique.", nameof(fieldNames));
                copy.Add(normalized);
            }
            if (copy.Count == 0) throw new ArgumentException("A consistency rule must reference at least one field.", nameof(fieldNames));
            _fieldNames = new ReadOnlyCollection<string>(copy);
        }

        /// <inheritdoc />
        public string Id { get; }
        /// <inheritdoc />
        public IReadOnlyList<string> FieldNames => _fieldNames;

        /// <inheritdoc />
        public OcrFieldConsistencyResult Evaluate(OcrFieldConsistencyContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            return EvaluateCore(context, cancellationToken);
        }

        /// <summary>Evaluates the rule after common argument and cancellation checks. / 在完成通用参数与取消检查后评估规则。</summary>
        protected abstract OcrFieldConsistencyResult EvaluateCore(OcrFieldConsistencyContext context, CancellationToken cancellationToken);

        /// <summary>Creates a consistent result. / 创建一致结果。</summary>
        protected OcrFieldConsistencyResult Consistent(OcrFieldConsistencyContext context, string message, IEnumerable<string>? observedValues = null)
            => Create(context, OcrFieldConsistencyStatus.Consistent, "ok", message, observedValues);

        /// <summary>Creates an inconsistent result. / 创建不一致结果。</summary>
        protected OcrFieldConsistencyResult Inconsistent(OcrFieldConsistencyContext context, string code, string message, IEnumerable<string>? observedValues = null)
            => Create(context, OcrFieldConsistencyStatus.Inconsistent, code, message, observedValues);

        /// <summary>Creates a missing-field result. / 创建字段缺失结果。</summary>
        protected OcrFieldConsistencyResult Missing(OcrFieldConsistencyContext context, string message, IEnumerable<string>? observedValues = null)
            => Create(context, OcrFieldConsistencyStatus.Missing, "missing-field", message, observedValues);

        /// <summary>Creates a dependency-invalid result. / 创建依赖字段无效结果。</summary>
        protected OcrFieldConsistencyResult InvalidDependency(OcrFieldConsistencyContext context, string message, IEnumerable<string>? observedValues = null)
            => Create(context, OcrFieldConsistencyStatus.InvalidDependency, "dependency-invalid", message, observedValues);

        private OcrFieldConsistencyResult Create(OcrFieldConsistencyContext context, OcrFieldConsistencyStatus status, string code, string message, IEnumerable<string>? observedValues)
            => new OcrFieldConsistencyResult(this, context, status, code, message, observedValues);
    }

    /// <summary>Contains one cross-field consistency outcome and its provenance. / 包含一次跨字段一致性结果及其来源。</summary>
    public sealed class OcrFieldConsistencyResult
    {
        private readonly IReadOnlyList<string> _observedValues;

        internal OcrFieldConsistencyResult(IOcrFieldConsistencyRule rule, OcrFieldConsistencyContext context, OcrFieldConsistencyStatus status, string code, string message, IEnumerable<string>? observedValues)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            RuleId = rule.Id;
            FieldNames = rule.FieldNames;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(OcrFieldConsistencyStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A consistency code is required.", nameof(code));
            if (message == null) throw new ArgumentNullException(nameof(message));
            var copy = new List<string>();
            if (observedValues != null)
            {
                foreach (string value in observedValues)
                {
                    if (value == null) throw new ArgumentException("Observed values cannot contain null.", nameof(observedValues));
                    if (copy.Count >= 64) throw new ArgumentOutOfRangeException(nameof(observedValues));
                    copy.Add(value);
                }
            }
            _observedValues = new ReadOnlyCollection<string>(copy);
            Status = status;
            Code = code;
            Message = message;
        }

        /// <summary>Gets the stable rule identifier. / 获取稳定的规则标识。</summary>
        public string RuleId { get; }
        /// <summary>Gets the fields read by the rule. / 获取规则读取的字段。</summary>
        public IReadOnlyList<string> FieldNames { get; }
        /// <summary>Gets the immutable input context. / 获取不可变输入上下文。</summary>
        public OcrFieldConsistencyContext Context { get; }
        /// <summary>Gets values observed in rule-defined order. / 获取按规则顺序观察到的值。</summary>
        public IReadOnlyList<string> ObservedValues => _observedValues;
        /// <summary>Gets the consistency status. / 获取一致性状态。</summary>
        public OcrFieldConsistencyStatus Status { get; }
        /// <summary>Gets the stable machine-readable reason code. / 获取稳定的机器可读原因码。</summary>
        public string Code { get; }
        /// <summary>Gets a human-readable diagnostic message. / 获取人类可读诊断消息。</summary>
        public string Message { get; }
        /// <summary>Gets whether the rule passed. / 获取规则是否通过。</summary>
        public bool IsConsistent => Status == OcrFieldConsistencyStatus.Consistent;

        /// <summary>Computes a stable hash over rule, values, status, and context provenance. / 对规则、值、状态及上下文来源计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            Append(builder, RuleId);
            foreach (string fieldName in FieldNames) Append(builder, fieldName);
            Append(builder, Context.ComputeSha256());
            builder.Append((int)Status).Append(';');
            Append(builder, Code);
            Append(builder, Message);
            foreach (string value in _observedValues) Append(builder, value);
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Contains ordered outcomes from a bounded consistency rule set. / 包含有界一致性规则集的有序结果。</summary>
    public sealed class OcrFieldConsistencyReport
    {
        private readonly IReadOnlyList<OcrFieldConsistencyResult> _results;

        internal OcrFieldConsistencyReport(IEnumerable<OcrFieldConsistencyResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var copy = new List<OcrFieldConsistencyResult>();
            foreach (OcrFieldConsistencyResult result in results)
            {
                if (result == null) throw new ArgumentException("Consistency results cannot contain null.", nameof(results));
                if (copy.Count >= 64) throw new ArgumentOutOfRangeException(nameof(results));
                copy.Add(result);
            }
            _results = new ReadOnlyCollection<OcrFieldConsistencyResult>(copy);
        }

        /// <summary>Gets results in rule declaration order. / 获取按规则声明顺序排列的结果。</summary>
        public IReadOnlyList<OcrFieldConsistencyResult> Results => _results;
        /// <summary>Gets whether every declared rule passed. / 获取是否所有声明规则均通过。</summary>
        public bool IsConsistent => _results.Count != 0 && ConsistentCount == _results.Count;
        /// <summary>Gets the number of consistent rules. / 获取一致规则数量。</summary>
        public int ConsistentCount => Count(OcrFieldConsistencyStatus.Consistent);
        /// <summary>Gets the number of inconsistent rules. / 获取不一致规则数量。</summary>
        public int InconsistentCount => Count(OcrFieldConsistencyStatus.Inconsistent);
        /// <summary>Gets the number of missing-field rules. / 获取字段缺失规则数量。</summary>
        public int MissingCount => Count(OcrFieldConsistencyStatus.Missing);
        /// <summary>Gets the number of invalid-dependency rules. / 获取依赖字段无效规则数量。</summary>
        public int InvalidDependencyCount => Count(OcrFieldConsistencyStatus.InvalidDependency);

        /// <summary>Computes a stable hash over ordered outcomes. / 对有序一致性结果计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            foreach (OcrFieldConsistencyResult result in _results) Append(builder, result.ComputeSha256());
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private int Count(OcrFieldConsistencyStatus status)
        {
            int count = 0;
            foreach (OcrFieldConsistencyResult result in _results) if (result.Status == status) count++;
            return count;
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Runs a bounded, uniquely identified consistency rule set. / 执行有界且规则标识唯一的一致性规则集。</summary>
    public static class OcrFieldConsistency
    {
        /// <summary>Evaluates all declared rules in order. / 按声明顺序评估所有规则。</summary>
        public static OcrFieldConsistencyReport EvaluateAll(OcrFieldConsistencyContext context, IEnumerable<IOcrFieldConsistencyRule> rules, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var results = new List<OcrFieldConsistencyResult>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (IOcrFieldConsistencyRule rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rule == null) throw new ArgumentException("Consistency rules cannot contain null.", nameof(rules));
                if (!ids.Add(rule.Id)) throw new ArgumentException("Consistency rule identifiers must be unique.", nameof(rules));
                if (results.Count >= 64) throw new ArgumentOutOfRangeException(nameof(rules));
                results.Add(rule.Evaluate(context, cancellationToken));
            }
            return new OcrFieldConsistencyReport(results);
        }
    }

    /// <summary>Compares two canonical field values using ordinal equality. / 使用序号相等比较两个规范字段值。</summary>
    public sealed class EqualOcrFieldConsistencyRule : OcrFieldConsistencyRuleBase
    {
        /// <summary>Initializes an equality rule. / 初始化相等规则。</summary>
        public EqualOcrFieldConsistencyRule(string leftFieldName, string rightFieldName, string? id = null)
            : base(id ?? "ocr/consistency/equal/" + Normalize(leftFieldName) + "/" + Normalize(rightFieldName), new[] { Normalize(leftFieldName), Normalize(rightFieldName) })
        {
            if (string.Equals(FieldNames[0], FieldNames[1], StringComparison.Ordinal)) throw new ArgumentException("Equality fields must be different.", nameof(rightFieldName));
        }

        /// <summary>Gets the left field name. / 获取左字段名。</summary>
        public string LeftFieldName => FieldNames[0];
        /// <summary>Gets the right field name. / 获取右字段名。</summary>
        public string RightFieldName => FieldNames[1];

        /// <inheritdoc />
        protected override OcrFieldConsistencyResult EvaluateCore(OcrFieldConsistencyContext context, CancellationToken cancellationToken)
        {
            OcrFieldValidationResult? left = null;
            OcrFieldValidationResult? right = null;
            if (!context.TryGet(LeftFieldName, out left) || !context.TryGet(RightFieldName, out right)) return Missing(context, "One or more equality fields are missing.", Values(left, right));
            if (!left!.IsValid || !right!.IsValid) return InvalidDependency(context, "An equality dependency did not pass field validation.", Values(left, right));
            string leftValue = Value(left);
            string rightValue = Value(right);
            return string.Equals(leftValue, rightValue, StringComparison.Ordinal)
                ? Consistent(context, "The two field values are equal.", new[] { leftValue, rightValue })
                : Inconsistent(context, "not-equal", "The two field values are different.", new[] { leftValue, rightValue });
        }

        private static string Normalize(string name) => VisualGuard.Identifier(name, nameof(name));
        private static string Value(OcrFieldValidationResult result) => result.CanonicalValue ?? result.NormalizedText.Trim();
        private static IEnumerable<string> Values(OcrFieldValidationResult? left, OcrFieldValidationResult? right)
        {
            if (left != null) yield return Value(left);
            if (right != null) yield return Value(right);
        }
    }

    /// <summary>Defines the direction of a date range relation. / 定义日期范围关系的方向。</summary>
    public enum OcrDateOrder
    {
        /// <summary>The first date must be on or before the second. / 第一个日期必须早于或等于第二个日期。</summary>
        Ascending = 0,
        /// <summary>The first date must be on or after the second. / 第一个日期必须晚于或等于第二个日期。</summary>
        Descending = 1
    }

    /// <summary>Checks that two validated date fields are ordered. / 检查两个已校验日期字段的先后顺序。</summary>
    public sealed class DateOrderOcrFieldConsistencyRule : OcrFieldConsistencyRuleBase
    {
        /// <summary>Initializes a date-order rule. / 初始化日期顺序规则。</summary>
        public DateOrderOcrFieldConsistencyRule(string firstFieldName, string secondFieldName, OcrDateOrder order = OcrDateOrder.Ascending, string? id = null)
            : base(id ?? "ocr/consistency/date-order/" + Normalize(firstFieldName) + "/" + Normalize(secondFieldName), new[] { Normalize(firstFieldName), Normalize(secondFieldName) })
        {
            if (!Enum.IsDefined(typeof(OcrDateOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            if (string.Equals(FieldNames[0], FieldNames[1], StringComparison.Ordinal)) throw new ArgumentException("Date-order fields must be different.", nameof(secondFieldName));
            Order = order;
        }

        /// <summary>Gets the first field name. / 获取第一个字段名。</summary>
        public string FirstFieldName => FieldNames[0];
        /// <summary>Gets the second field name. / 获取第二个字段名。</summary>
        public string SecondFieldName => FieldNames[1];
        /// <summary>Gets the required order. / 获取要求的顺序。</summary>
        public OcrDateOrder Order { get; }

        /// <inheritdoc />
        protected override OcrFieldConsistencyResult EvaluateCore(OcrFieldConsistencyContext context, CancellationToken cancellationToken)
        {
            OcrFieldValidationResult? first = null;
            OcrFieldValidationResult? second = null;
            if (!context.TryGet(FirstFieldName, out first) || !context.TryGet(SecondFieldName, out second)) return Missing(context, "One or more date-order fields are missing.", Values(first, second));
            if (!first!.IsValid || !second!.IsValid) return InvalidDependency(context, "A date-order dependency did not pass field validation.", Values(first, second));
            DateTime firstDate;
            DateTime secondDate;
            if (!TryDate(first, out firstDate) || !TryDate(second, out secondDate)) return InvalidDependency(context, "A date-order dependency did not expose an ISO date canonical value.", Values(first, second));
            bool ordered = Order == OcrDateOrder.Ascending ? firstDate <= secondDate : firstDate >= secondDate;
            return ordered
                ? Consistent(context, "The date fields satisfy the configured order.", new[] { firstDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), secondDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })
                : Inconsistent(context, "date-order", "The date fields violate the configured order.", new[] { firstDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), secondDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        private static string Normalize(string name) => VisualGuard.Identifier(name, nameof(name));
        private static bool TryDate(OcrFieldValidationResult result, out DateTime date) => DateTime.TryParseExact(result.CanonicalValue ?? result.NormalizedText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        private static IEnumerable<string> Values(OcrFieldValidationResult? first, OcrFieldValidationResult? second)
        {
            if (first != null) yield return first.CanonicalValue ?? first.NormalizedText.Trim();
            if (second != null) yield return second.CanonicalValue ?? second.NormalizedText.Trim();
        }
    }

    /// <summary>Checks that a bounded set of amount fields equals a total field within a tolerance. / 检查有界金额字段集合在误差范围内等于总额字段。</summary>
    public sealed class AmountTotalOcrFieldConsistencyRule : OcrFieldConsistencyRuleBase
    {
        /// <summary>Initializes an amount-total rule. / 初始化金额总额规则。</summary>
        public AmountTotalOcrFieldConsistencyRule(IEnumerable<string> lineFieldNames, string totalFieldName, decimal tolerance = 0m, string? id = null)
            : base(id ?? "ocr/consistency/amount-total.v1", BuildNames(lineFieldNames, totalFieldName))
        {
            if (tolerance < 0m || tolerance > 1000000000m) throw new ArgumentOutOfRangeException(nameof(tolerance));
            Tolerance = tolerance;
            var lines = new List<string>();
            for (int index = 0; index < FieldNames.Count - 1; index++) lines.Add(FieldNames[index]);
            LineFieldNames = new ReadOnlyCollection<string>(lines);
            TotalFieldName = FieldNames[FieldNames.Count - 1];
        }

        /// <summary>Gets line amount fields in declaration order. / 获取按声明顺序排列的明细金额字段。</summary>
        public IReadOnlyList<string> LineFieldNames { get; }
        /// <summary>Gets the total amount field. / 获取总金额字段。</summary>
        public string TotalFieldName { get; }
        /// <summary>Gets the absolute allowed decimal difference. / 获取允许的十进制绝对误差。</summary>
        public decimal Tolerance { get; }

        /// <inheritdoc />
        protected override OcrFieldConsistencyResult EvaluateCore(OcrFieldConsistencyContext context, CancellationToken cancellationToken)
        {
            decimal sum = 0m;
            var observed = new List<string>();
            for (int index = 0; index < LineFieldNames.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OcrFieldValidationResult? line;
                if (!context.TryGet(LineFieldNames[index], out line)) return Missing(context, "One or more amount fields are missing.", observed);
                if (!line!.IsValid) return InvalidDependency(context, "An amount dependency did not pass field validation.", observed);
                string value = Value(line);
                decimal amount;
                if (!decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount)) return InvalidDependency(context, "An amount dependency did not expose a decimal canonical value.", observed);
                try { sum = checked(sum + amount); }
                catch (OverflowException) { return InvalidDependency(context, "The amount sum exceeds the supported decimal range.", observed); }
                observed.Add(value);
            }

            OcrFieldValidationResult? total;
            if (!context.TryGet(TotalFieldName, out total)) return Missing(context, "The total amount field is missing.", observed);
            if (!total!.IsValid) return InvalidDependency(context, "The total amount dependency did not pass field validation.", observed);
            string totalValue = Value(total);
            decimal expected;
            if (!decimal.TryParse(totalValue, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out expected)) return InvalidDependency(context, "The total amount dependency did not expose a decimal canonical value.", observed);
            observed.Add(totalValue);
            decimal difference;
            try { difference = Math.Abs(sum - expected); }
            catch (OverflowException) { return InvalidDependency(context, "The amount difference exceeds the supported decimal range.", observed); }
            bool equal = difference <= Tolerance;
            string details = "sum=" + sum.ToString("0.##", CultureInfo.InvariantCulture) + ";total=" + expected.ToString("0.##", CultureInfo.InvariantCulture) + ";difference=" + difference.ToString("0.##", CultureInfo.InvariantCulture) + ";tolerance=" + Tolerance.ToString("0.##", CultureInfo.InvariantCulture);
            return equal
                ? Consistent(context, "The amount fields satisfy the configured total. " + details, observed)
                : Inconsistent(context, "amount-total", "The amount fields do not satisfy the configured total. " + details, observed);
        }

        private static string[] BuildNames(IEnumerable<string> lineFieldNames, string totalFieldName)
        {
            if (lineFieldNames == null) throw new ArgumentNullException(nameof(lineFieldNames));
            var names = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string lineFieldName in lineFieldNames)
            {
                if (names.Count >= 63) throw new ArgumentOutOfRangeException(nameof(lineFieldNames));
                string normalized = VisualGuard.Identifier(lineFieldName, nameof(lineFieldNames));
                if (!unique.Add(normalized)) throw new ArgumentException("Amount field names must be unique.", nameof(lineFieldNames));
                names.Add(normalized);
            }
            if (names.Count == 0) throw new ArgumentException("At least one line amount field is required.", nameof(lineFieldNames));
            string total = VisualGuard.Identifier(totalFieldName, nameof(totalFieldName));
            if (!unique.Add(total)) throw new ArgumentException("The total field must differ from line amount fields.", nameof(totalFieldName));
            names.Add(total);
            return names.ToArray();
        }

        private static string Value(OcrFieldValidationResult result) => result.CanonicalValue ?? result.NormalizedText.Trim();
    }
}
