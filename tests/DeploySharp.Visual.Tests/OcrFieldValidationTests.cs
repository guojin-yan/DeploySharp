using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrFieldValidationTests
    {
        [TestMethod]
        public void BuiltInValidatorsAcceptCanonicalExamples()
        {
            AssertValid(OcrFieldKind.Date, "2026年9月18日", "2026-09-18");
            AssertValid(OcrFieldKind.Amount, "￥1,234.50", "1234.5");
            AssertValid(OcrFieldKind.Phone, "+86 13800138000", "+86 13800138000");
            AssertValid(OcrFieldKind.ChineseIdCard, "11010519491231002x", "11010519491231002X");
            AssertValid(OcrFieldKind.Url, "https://example.com/a?id=1", "https://example.com/a?id=1");
            AssertValid(OcrFieldKind.Email, "user@example.com", "user@example.com");
            AssertValid(OcrFieldKind.Identifier, "MOTOR-01/A", "MOTOR-01/A");
            AssertValid(OcrFieldKind.Address, "江苏省徐州市泉山区", "江苏省徐州市泉山区");
            AssertValid(OcrFieldKind.InvoiceNumber, "12345678", "12345678");
            AssertValid(OcrFieldKind.DeviceSerialNumber, "SN-2026-001", "SN-2026-001");
        }

        [TestMethod]
        public void BuiltInValidatorsRejectMalformedValuesWithReasons()
        {
            AssertInvalid(OcrFieldKind.Date, "2026-02-31", "format");
            AssertInvalid(OcrFieldKind.Amount, "12.345", "format");
            AssertInvalid(OcrFieldKind.Phone, "12345", "format");
            AssertInvalid(OcrFieldKind.ChineseIdCard, "11010519491231002A", "format");
            AssertInvalid(OcrFieldKind.ChineseIdCard, "110105194912310021", "checksum");
            AssertInvalid(OcrFieldKind.Url, "ftp://example.com", "format");
            AssertInvalid(OcrFieldKind.Email, "user@example", "format");
            AssertInvalid(OcrFieldKind.Identifier, "---", "format");
            AssertInvalid(OcrFieldKind.Address, "---", "format");
            AssertInvalid(OcrFieldKind.InvoiceNumber, "1234", "length");
            AssertInvalid(OcrFieldKind.DeviceSerialNumber, "SN", "length");
        }

        [TestMethod]
        public void EmptyValuesAreExplicitAndNormalizationProvenanceIsRetained()
        {
            OcrTextNormalizationResult text = OcrTextNormalizer.Normalize("  ＳＮ-01  ", new OcrTextNormalizationOptions(convertFullWidthAscii: true, trimWhitespace: true));
            OcrFieldValidationResult result = new DeviceSerialNumberOcrFieldValidator(minimumLength: 4).Validate(text);
            Assert.AreEqual(OcrFieldValidationStatus.Valid, result.Status);
            Assert.AreEqual("  ＳＮ-01  ", result.RawText);
            Assert.AreEqual("SN-01", result.NormalizedText);
            Assert.AreEqual("SN-01", result.CanonicalValue);
            Assert.AreEqual(text.ComputeSha256(), result.Text.ComputeSha256());

            OcrFieldValidationResult empty = new EmailOcrFieldValidator().Validate(OcrTextNormalizer.Normalize(" \t", new OcrTextNormalizationOptions()));
            Assert.AreEqual(OcrFieldValidationStatus.Empty, empty.Status);
            Assert.AreEqual("empty", empty.Code);
            Assert.IsFalse(empty.IsValid);
        }

        [TestMethod]
        public void FactoryProducesUniqueCompleteSetAndSetHashIsStable()
        {
            IReadOnlyList<IOcrFieldValidator> validators = OcrFieldValidators.CreateDefault();
            Assert.AreEqual(10, validators.Count);
            var kinds = new HashSet<OcrFieldKind>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (IOcrFieldValidator validator in validators)
            {
                Assert.IsTrue(kinds.Add(validator.Kind));
                Assert.IsTrue(ids.Add(validator.Id));
            }
            OcrTextNormalizationResult text = OcrTextNormalizer.Normalize("2026-09-18", new OcrTextNormalizationOptions());
            OcrFieldValidationReport report = OcrFieldValidators.ValidateAll(text, new[] { OcrFieldValidators.Create(OcrFieldKind.Date) });
            Assert.IsTrue(report.IsValid);
            Assert.AreEqual(1, report.ValidCount);
            Assert.AreEqual(0, report.InvalidCount);
            Assert.AreEqual(64, report.ComputeSha256().Length);
        }

        [TestMethod]
        public void CustomValidatorCanUseTheSameContract()
        {
            OcrTextNormalizationResult text = OcrTextNormalizer.Normalize("DS-001", new OcrTextNormalizationOptions());
            OcrFieldValidationResult result = new PrefixValidator().Validate(text);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("custom/prefix.v1", result.ValidatorId);
            Assert.AreEqual(OcrFieldKind.Identifier, result.Kind);
            Assert.AreEqual("DS-001", result.CanonicalValue);
        }

        [TestMethod]
        public void CancellationAndDuplicateValidatorIdsAreBounded()
        {
            OcrTextNormalizationResult text = OcrTextNormalizer.Normalize("2026-09-18", new OcrTextNormalizationOptions());
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() => OcrFieldValidators.ValidateAll(text, new[] { OcrFieldValidators.Create(OcrFieldKind.Date) }, cancellation.Token));
            }
            IOcrFieldValidator first = new DateOcrFieldValidator("custom/date.v1");
            IOcrFieldValidator second = new DateOcrFieldValidator("custom/date.v1");
            Assert.ThrowsExactly<ArgumentException>(() => OcrFieldValidators.ValidateAll(text, new[] { first, second }));
        }

        private static void AssertValid(OcrFieldKind kind, string value, string canonical)
        {
            OcrFieldValidationResult result = OcrFieldValidators.Create(kind).Validate(OcrTextNormalizer.Normalize(value, new OcrTextNormalizationOptions()));
            Assert.IsTrue(result.IsValid, kind + ": " + result.Message);
            Assert.AreEqual(canonical, result.CanonicalValue, kind.ToString());
        }

        private static void AssertInvalid(OcrFieldKind kind, string value, string code)
        {
            OcrFieldValidationResult result = OcrFieldValidators.Create(kind).Validate(OcrTextNormalizer.Normalize(value, new OcrTextNormalizationOptions()));
            Assert.AreEqual(OcrFieldValidationStatus.Invalid, result.Status, kind.ToString());
            Assert.AreEqual(code, result.Code, kind.ToString());
        }

        private sealed class PrefixValidator : OcrFieldValidatorBase
        {
            public PrefixValidator() : base("custom/prefix.v1", OcrFieldKind.Identifier) { }

            protected override OcrFieldValidationResult ValidateCore(OcrTextNormalizationResult text, string value, CancellationToken cancellationToken)
                => value.StartsWith("DS-", StringComparison.Ordinal) ? Valid(text) : Invalid(text, "prefix", "The value must start with DS-.");
        }
    }
}
