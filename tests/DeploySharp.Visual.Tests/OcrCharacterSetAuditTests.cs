using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    public sealed partial class OcrTests
    {
        [TestMethod]
        public void AuditedDuplicateDictionaryClassesEmitTheirOriginalTextAndUnknownRemainsExplicit()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "A\nA\n", new UTF8Encoding(false));
                OcrCharacterSet set = PaddleOcrProfiles.LoadCharacterSet(path, "tests/duplicate", "1", false);
                var auditor = new OcrCharacterSetAuditor(set);
                auditor.Audit("A").EnsureCovered();
                Assert.IsFalse(auditor.Audit("?").IsCovered);
                var decoder = new GreedyCtcDecoder(new CtcOutputSchema("logits", CtcTensorLayout.BatchTimeClasses), set,
                    new CtcDecoderOptions(0, applySoftmax: false, unknownClassIndex: 3, unknownBehavior: CtcUnknownTokenBehavior.Replace, unknownReplacement: "?"));
                VisualModelProfile profile = RecognitionProfile(decoder, TensorElementType.Float32, 1, 5, 4);
                using PreparedVisualInput input = RecognitionInput(1, 16);
                var tensor = new Tensor<float>(new TensorShape(1, 5, 4), Probabilities(1, 5, 4, 1, 2, 2, 0, 3));
                var result = (TextRecognitionBatchResult)decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("logits", tensor), CancellationToken.None));
                Assert.AreEqual("AA?", result.Items[0].Text, "Different classes with the same visible token are not collapsed together or renamed.");
                Assert.IsTrue(result.Items[0].Tokens[2].IsCollapsedRepeat);
                Assert.IsTrue(result.Items[0].Tokens[4].IsUnknown);
                Assert.AreEqual(4, decoder.ExpectedClassCount);
            }
            finally { File.Delete(path); }
        }
    }

    [TestClass]
    public sealed class OcrCharacterSetAuditTests
    {
        [TestMethod]
        public void ReportsUnicodeScalarsOffsetsCountsAndImmutableEvidence()
        {
            var characters = new OcrCharacterSet("tests/audit", "v1", "中A1， 😀");
            var auditor = new OcrCharacterSetAuditor(characters);
            OcrCharacterCoverageReport result = auditor.Audit("A𠀀中𠀀Ω😀");
            Assert.AreEqual(6, result.RequiredScalarCount);
            Assert.AreEqual(5, result.DistinctRequiredScalarCount);
            Assert.AreEqual(.6, result.Coverage!.Value, 1e-9);
            CollectionAssert.AreEqual(new[] { "U+03A9", "U+20000" }, result.MissingCharacters.Select(x => x.CodePoint).ToArray());
            OcrMissingCharacter supplementary = result.MissingCharacters[1];
            Assert.AreEqual("𠀀", supplementary.Text);
            Assert.AreEqual(1, supplementary.FirstUtf16Offset);
            Assert.AreEqual(2, supplementary.Occurrences);
            Assert.IsFalse(supplementary.AppearsInCompoundToken);
            Assert.AreEqual(characters.Sha256, result.CharacterSetSha256);
            Assert.AreEqual("v1", result.CharacterSetVersion);
            Assert.AreEqual(VisualErrorCodes.OcrCharacterCoverageMissing, Assert.ThrowsExactly<VisualException>(() => result.EnsureCovered()).ErrorCode);
            Assert.ThrowsExactly<NotSupportedException>(() => ((System.Collections.Generic.IList<OcrMissingCharacter>)result.MissingCharacters).Clear());
        }

        [TestMethod]
        public void DoesNotConfuseCompoundTokensWithIndependentCharactersOrNormalizeRequirements()
        {
            var auditor = new OcrCharacterSetAuditor(new OcrCharacterSet("tests/audit", "1", new[] { "AB", "中", "é", "😀", " " }));
            OcrCharacterCoverageReport result = auditor.Audit("ABＡe\u0301\t");
            Assert.AreEqual(6, result.MissingCharacters.Count);
            Assert.IsTrue(result.MissingCharacters.Single(x => x.Text == "A").AppearsInCompoundToken);
            Assert.IsTrue(result.MissingCharacters.Single(x => x.Text == "B").AppearsInCompoundToken);
            Assert.IsFalse(result.MissingCharacters.Single(x => x.Text == "Ａ").AppearsInCompoundToken);
            Assert.AreEqual(1, auditor.CompoundTokenCount);
            Assert.AreEqual(4, auditor.IndependentScalarCount);
            auditor.Audit("中é😀 ").EnsureCovered();
        }

        [TestMethod]
        public void EmptyRequirementsAreNotCertifiedAndInvalidUtf16IsRejected()
        {
            var auditor = new OcrCharacterSetAuditor(new OcrCharacterSet("tests/audit", "1", "A"));
            OcrCharacterCoverageReport empty = auditor.Audit("");
            Assert.IsFalse(empty.IsCovered);
            Assert.IsNull(empty.Coverage);
            Assert.ThrowsExactly<VisualException>(() => empty.EnsureCovered());
            Assert.ThrowsExactly<ArgumentNullException>(() => auditor.Audit(null!));
            foreach (string invalid in new[] { "\uD800", "\uDC00", "A\uD800B" })
                Assert.ThrowsExactly<ArgumentException>(() => auditor.Audit(invalid));
        }

        [TestMethod]
        public void MappingHashDistinguishesTokenBoundariesOrderAndDuplicatesWithoutChangingLegacyHash()
        {
            var first = new OcrCharacterSet("tests/audit", "1", new[] { "AB", "C" });
            var second = new OcrCharacterSet("tests/audit", "1", new[] { "A", "BC" });
            Assert.AreEqual(first.Sha256, second.Sha256, "Existing result hash is preserved for compatibility.");
            string hash = new OcrCharacterSetAuditor(first).TokenMappingSha256;
            Assert.AreNotEqual(hash, new OcrCharacterSetAuditor(second).TokenMappingSha256);
            Assert.AreNotEqual(hash, new OcrCharacterSetAuditor(new OcrCharacterSet("tests/audit", "1", new[] { "C", "AB" })).TokenMappingSha256);
            Assert.AreEqual(hash, new OcrCharacterSetAuditor(new OcrCharacterSet("other/id", "2", new[] { "AB", "C" })).TokenMappingSha256);
        }

        [TestMethod]
        public void AuditBoundsCancellationAndParallelReuseDoNotChangeCharacterSet()
        {
            var set = new OcrCharacterSet("tests/audit", "1", "A😀");
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCharacterSetAuditor(set, maximumDictionaryUtf16Length: 2));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrCharacterSetAuditor(set, maximumRequiredUtf16Length: 0));
            var auditor = new OcrCharacterSetAuditor(set, maximumRequiredUtf16Length: 3);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => auditor.Audit("AAAA"));
            var token = new CancellationToken(true);
            Assert.ThrowsExactly<OperationCanceledException>(() => new OcrCharacterSetAuditor(set, cancellationToken: token));
            Assert.ThrowsExactly<OperationCanceledException>(() => auditor.Audit("A", token));
            Parallel.For(0, 32, i => { Assert.IsTrue(auditor.Audit("A😀").IsCovered); Assert.AreEqual("B", auditor.Audit("B").MissingCharacters[0].Text); });
            Assert.AreEqual(2, set.Count);
        }

        [TestMethod]
        public void PaddleLoaderPreservesDuplicatesAndSpacesAndTreatsBomAsAFileMarker()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "A\r\nA\r\nAB\r\n \r\n😀\r\n", new UTF8Encoding(true));
                OcrCharacterSet set = PaddleOcrProfiles.LoadCharacterSet(path, "tests/audit", "1", true);
                CollectionAssert.AreEqual(new[] { "A", "A", "AB", " ", "😀", " " }, set.Characters.ToArray());
                var auditor = new OcrCharacterSetAuditor(set);
                Assert.AreEqual(6, auditor.TokenCount);
                Assert.AreEqual(2, auditor.DuplicateTokens.Count);
                Assert.AreEqual(0, auditor.DuplicateTokens[0].FirstCharacterIndex);
                Assert.AreEqual(1, auditor.DuplicateTokens[0].DuplicateCharacterIndex);
                Assert.AreEqual(3, auditor.DuplicateTokens[1].FirstCharacterIndex);
                Assert.AreEqual(5, auditor.DuplicateTokens[1].DuplicateCharacterIndex);
                Assert.IsTrue(auditor.Audit("A 😀").IsCovered);
                Assert.ThrowsExactly<NotSupportedException>(() => ((System.Collections.Generic.IList<OcrDuplicateToken>)auditor.DuplicateTokens).Clear());
                string expectedFileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                Assert.AreEqual(set.Sha256, PaddleOcrProfiles.LoadCharacterSet(path, "tests/audit", "1", true, expectedFileHash).Sha256);
                Assert.ThrowsExactly<VisualException>(() => PaddleOcrProfiles.LoadCharacterSet(path, "tests/audit", "1", true, new string('0', 64)));
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void PaddleLoaderRejectsInvalidUtf8AndInteriorEmptyRowsWithoutRenumberingClasses()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, new byte[] { 0x41, 0x0A, 0xC3, 0x28 });
                Assert.AreEqual(VisualErrorCodes.ProfileInvalid, Assert.ThrowsExactly<VisualException>(() => PaddleOcrProfiles.LoadCharacterSet(path, "tests/audit", "1", false)).ErrorCode);
                File.WriteAllText(path, "A\n\nB\n", new UTF8Encoding(false));
                Assert.ThrowsExactly<VisualException>(() => PaddleOcrProfiles.LoadCharacterSet(path, "tests/audit", "1", false));
            }
            finally { File.Delete(path); }
        }
    }
}
