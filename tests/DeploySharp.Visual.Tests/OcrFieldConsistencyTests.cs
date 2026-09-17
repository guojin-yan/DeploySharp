using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrFieldConsistencyTests
    {
        [TestMethod]
        public void EqualityRulePreservesValuesAndReportsStableHash()
        {
            OcrFieldConsistencyContext context = Context(
                Field("serial_a", OcrFieldKind.Identifier, "SN-001"),
                Field("serial_b", OcrFieldKind.Identifier, "SN-001"));
            OcrFieldConsistencyReport report = OcrFieldConsistency.EvaluateAll(context, new[] { new EqualOcrFieldConsistencyRule("serial_a", "serial_b") });

            Assert.IsTrue(report.IsConsistent);
            Assert.AreEqual(OcrFieldConsistencyStatus.Consistent, report.Results[0].Status);
            CollectionAssert.AreEqual(new[] { "SN-001", "SN-001" }, ToArray(report.Results[0].ObservedValues));
            Assert.AreEqual(64, context.ComputeSha256().Length);
            Assert.AreEqual(64, report.ComputeSha256().Length);

            OcrFieldConsistencyContext different = Context(
                Field("serial_a", OcrFieldKind.Identifier, "SN-001"),
                Field("serial_b", OcrFieldKind.Identifier, "SN-002"));
            OcrFieldConsistencyResult result = OcrFieldConsistency.EvaluateAll(different, new[] { new EqualOcrFieldConsistencyRule("serial_a", "serial_b", "custom/equal.v1") }).Results[0];
            Assert.AreEqual(OcrFieldConsistencyStatus.Inconsistent, result.Status);
            Assert.AreEqual("not-equal", result.Code);
        }

        [TestMethod]
        public void DateOrderAndAmountTotalRulesValidateBusinessRelations()
        {
            OcrFieldConsistencyContext dates = Context(
                Field("start", OcrFieldKind.Date, "2026-09-01"),
                Field("end", OcrFieldKind.Date, "2026-09-18"));
            OcrFieldConsistencyResult dateResult = OcrFieldConsistency.EvaluateAll(dates, new[] { new DateOrderOcrFieldConsistencyRule("start", "end") }).Results[0];
            Assert.AreEqual(OcrFieldConsistencyStatus.Consistent, dateResult.Status);

            OcrFieldConsistencyContext amounts = Context(
                Field("line_1", OcrFieldKind.Amount, "10.25"),
                Field("line_2", OcrFieldKind.Amount, "2.50"),
                Field("total", OcrFieldKind.Amount, "12.75"));
            var amountRule = new AmountTotalOcrFieldConsistencyRule(new[] { "line_1", "line_2" }, "total", .001m);
            OcrFieldConsistencyResult amountResult = OcrFieldConsistency.EvaluateAll(amounts, new[] { amountRule }).Results[0];
            Assert.AreEqual(OcrFieldConsistencyStatus.Consistent, amountResult.Status);
            Assert.AreEqual(3, amountResult.ObservedValues.Count);
            OcrFieldConsistencyContext mismatch = Context(
                Field("line_1", OcrFieldKind.Amount, "10.25"),
                Field("line_2", OcrFieldKind.Amount, "2.50"),
                Field("total", OcrFieldKind.Amount, "13.00"));
            OcrFieldConsistencyResult mismatchResult = OcrFieldConsistency.EvaluateAll(mismatch, new[] { new AmountTotalOcrFieldConsistencyRule(new[] { "line_1", "line_2" }, "total", 0m, "custom/amount-total.v1") }).Results[0];
            Assert.AreEqual(OcrFieldConsistencyStatus.Inconsistent, mismatchResult.Status);
            Assert.AreEqual("amount-total", mismatchResult.Code);
        }

        [TestMethod]
        public void MissingAndInvalidDependenciesAreNotReportedAsInferenceFailures()
        {
            OcrFieldConsistencyContext missing = Context(Field("start", OcrFieldKind.Date, "2026-09-01"));
            OcrFieldConsistencyResult missingResult = OcrFieldConsistency.EvaluateAll(missing, new[] { new DateOrderOcrFieldConsistencyRule("start", "end") }).Results[0];
            Assert.AreEqual(OcrFieldConsistencyStatus.Missing, missingResult.Status);
            Assert.AreEqual("missing-field", missingResult.Code);

            OcrFieldConsistencyContext invalid = Context(
                Field("start", OcrFieldKind.Date, "2026-02-31"),
                Field("end", OcrFieldKind.Date, "2026-09-01"));
            OcrFieldConsistencyResult invalidResult = OcrFieldConsistency.EvaluateAll(invalid, new[] { new DateOrderOcrFieldConsistencyRule("start", "end", id: "custom/date-order.v1") }).Results[0];
            Assert.AreEqual(OcrFieldConsistencyStatus.InvalidDependency, invalidResult.Status);
            Assert.AreEqual("dependency-invalid", invalidResult.Code);
        }

        [TestMethod]
        public void CustomRuleUsesBaseContractAndCancellationIsBounded()
        {
            OcrFieldConsistencyContext context = Context(Field("value", OcrFieldKind.Identifier, "OK"));
            OcrFieldConsistencyResult custom = OcrFieldConsistency.EvaluateAll(context, new[] { new PrefixConsistencyRule() }).Results[0];
            Assert.IsTrue(custom.IsConsistent);
            Assert.AreEqual("custom/prefix.v1", custom.RuleId);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() => OcrFieldConsistency.EvaluateAll(context, new[] { new PrefixConsistencyRule() }, cancellation.Token));
            }

            Assert.ThrowsExactly<ArgumentException>(() => OcrFieldConsistency.EvaluateAll(context, new IOcrFieldConsistencyRule[] { new PrefixConsistencyRule(), new PrefixConsistencyRule() }));
            Assert.ThrowsExactly<ArgumentException>(() => new OcrFieldConsistencyContext(new[] { Field("value", OcrFieldKind.Identifier, "OK"), Field("VALUE", OcrFieldKind.Identifier, "OK") }));
        }

        [TestMethod]
        public void RuleAndContextNamesAreNormalizedAndInvalidDefinitionsRejected()
        {
            OcrFieldConsistencyContext context = Context(Field("start", OcrFieldKind.Date, "2026-09-18"), Field("end", OcrFieldKind.Date, "2026-09-18"));
            DateOrderOcrFieldConsistencyRule rule = new DateOrderOcrFieldConsistencyRule("START", "END", OcrDateOrder.Descending);
            Assert.AreEqual("start", rule.FirstFieldName);
            Assert.AreEqual("end", rule.SecondFieldName);
            Assert.IsTrue(OcrFieldConsistency.EvaluateAll(context, new[] { rule }).IsConsistent);
            Assert.ThrowsExactly<ArgumentException>(() => new EqualOcrFieldConsistencyRule("same", "SAME"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AmountTotalOcrFieldConsistencyRule(new[] { "a" }, "b", -1m));
        }

        private static OcrFieldBinding Field(string name, OcrFieldKind kind, string value)
        {
            OcrTextNormalizationResult text = OcrTextNormalizer.Normalize(value, new OcrTextNormalizationOptions());
            return new OcrFieldBinding(name, OcrFieldValidators.Create(kind).Validate(text));
        }

        private static OcrFieldConsistencyContext Context(params OcrFieldBinding[] fields) => new OcrFieldConsistencyContext(fields);

        private static string[] ToArray(IReadOnlyList<string> values)
        {
            var copy = new string[values.Count];
            for (int index = 0; index < values.Count; index++) copy[index] = values[index];
            return copy;
        }

        private sealed class PrefixConsistencyRule : OcrFieldConsistencyRuleBase
        {
            public PrefixConsistencyRule() : base("custom/prefix.v1", new[] { "value" }) { }

            protected override OcrFieldConsistencyResult EvaluateCore(OcrFieldConsistencyContext context, CancellationToken cancellationToken)
            {
                OcrFieldValidationResult? value;
                if (!context.TryGet("value", out value)) return Missing(context, "The value field is missing.");
                if (!value!.IsValid) return InvalidDependency(context, "The value field is invalid.");
                string actual = value.CanonicalValue ?? value.NormalizedText;
                return actual.StartsWith("OK", StringComparison.Ordinal) ? Consistent(context, "The value has the expected prefix.", new[] { actual }) : Inconsistent(context, "prefix", "The value does not have the expected prefix.", new[] { actual });
            }
        }
    }
}
