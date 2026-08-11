using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class DocumentUnderstandingInputTests
    {
        [TestMethod]
        public void DonutFactoryDecodesFileAndBytesAcrossRgbGrayAndAlpha()
        {
            DocumentUnderstandingProfile profile = DocumentUnderstandingProfiles.CreateDonutCordV2Onnx(); var factory = new OpenCvDocumentUnderstandingInputFactory(); string rgbPath = Fixture("rgb.png");
            using PreparedDocumentPage rgb = factory.CreatePageFromFile(rgbPath, profile); using PreparedDocumentPage bytes = factory.CreatePageFromBytes(File.ReadAllBytes(rgbPath), profile); using PreparedDocumentPage gray = factory.CreatePageFromBytes(File.ReadAllBytes(Fixture("gray.png")), profile); using PreparedDocumentPage alpha = factory.CreatePageFromBytes(File.ReadAllBytes(Fixture("alpha.png")), profile);
            CollectionAssert.AreEqual(new long[] { 1, 3, 1280, 960 }, rgb.VisualInput.Tensor.Shape.ToArray()); Assert.AreEqual(rgb.VisualInput.InputId, bytes.VisualInput.InputId); Assert.AreEqual(0, rgb.PageIndex); Assert.IsNull(rgb.Layout); Assert.IsTrue(((float[])rgb.VisualInput.Tensor.Buffer).All(float.IsFinite)); Assert.IsTrue(((float[])gray.VisualInput.Tensor.Buffer).All(float.IsFinite)); Assert.IsTrue(((float[])alpha.VisualInput.Tensor.Buffer).All(float.IsFinite));
            Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingContractInvalid, Assert.ThrowsExactly<VisualException>(() => factory.CreatePageFromFile(rgbPath, profile, layout: new DocumentLayoutInput(new VisualSize(8, 8), new[] { new DocumentWord("x", new DocumentNormalizedBox(1, 1, 2, 2)) }, new[] { 0 }))).ErrorCode);
        }

        [TestMethod]
        public void DonutFactoryMatchesPinnedOfficialPixelGoldenAndManagedTokenizer()
        {
            string root = @"E:\DeploySharp-Models\donut-base-finetuned-cord-v2"; string image = Path.Combine(root, "evidence", "cord-test-0", "document.png"); string golden = Path.Combine(root, "evidence", "cord-test-0", "pixel-values.f32"); string checkpoint = Path.Combine(root, "checkpoint");
            if (!File.Exists(image) || !File.Exists(golden) || !Directory.Exists(checkpoint)) Assert.Inconclusive("External Stage 27 Donut evidence is missing.");
            DocumentUnderstandingProfile profile = DocumentUnderstandingProfiles.CreateDonutCordV2Onnx(); using PreparedDocumentPage prepared = new OpenCvDocumentUnderstandingInputFactory().CreatePageFromFile(image, profile);
            float[] actual = (float[])prepared.VisualInput.Tensor.Buffer; byte[] bytes = File.ReadAllBytes(golden); var expected = new float[bytes.Length / sizeof(float)]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length); Assert.AreEqual(expected.Length, actual.Length);
            double maximum = 0; double mean = 0; for (int index = 0; index < actual.Length; index++) { double difference = Math.Abs(actual[index] - expected[index]); maximum = Math.Max(maximum, difference); mean += difference; } mean /= actual.Length;
            Assert.IsTrue(maximum <= 0.016f, "max=" + maximum + ";mean=" + mean); Assert.IsTrue(mean <= 0.000011, "mean=" + mean);
            var tokenizer = new DonutDocumentTokenizer(checkpoint, profile.Tokenizer); DocumentTokenSequence prompt = tokenizer.Encode(profile, DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId)); CollectionAssert.AreEqual(new long[] { 57579 }, prompt.CopyTokenIds());
            int[] official = { 57526, 57528, 20220, 38946, 4107, 27587, 40242, 57527, 57543, 2 }; string decoded = tokenizer.Decode(official); StringAssert.Contains(decoded, "<s_menu>"); StringAssert.Contains(decoded, "- TICKET CP");
        }

        [TestMethod]
        public void DonutFactoryMapsCancellationAndPageCapacityToStableErrors()
        {
            DocumentUnderstandingProfile profile = DocumentUnderstandingProfiles.CreateDonutCordV2Onnx(); var factory = new OpenCvDocumentUnderstandingInputFactory();
            using (var cancellation = new System.Threading.CancellationTokenSource()) { cancellation.Cancel(); Assert.AreEqual(OpenCvErrorCodes.Cancelled, Assert.ThrowsExactly<OpenCvVisualException>(() => factory.CreatePageFromFile(Fixture("rgb.png"), profile, cancellationToken: cancellation.Token)).ErrorCode); }
            Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingLimitExceeded, Assert.ThrowsExactly<VisualException>(() => factory.CreatePageFromFile(Fixture("rgb.png"), profile, pageIndex: 1)).ErrorCode);
        }

        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
    }
}
