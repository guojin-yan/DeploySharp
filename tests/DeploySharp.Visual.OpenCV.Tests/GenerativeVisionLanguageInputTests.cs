using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class GenerativeVisionLanguageInputTests
    {
        [TestMethod]
        public void FactoryCoversFileBytesRgbGrayAlphaAndStableIdentity()
        {
            GenerativeVisionLanguageProfile profile = Profile();
            var factory = new OpenCvGenerativeVisionLanguageInputFactory();
            string rgbPath = Fixture("rgb.png");
            using PreparedVisualInput rgb = factory.CreateFromFile(rgbPath, profile);
            using PreparedVisualInput bytes = factory.CreateFromBytes(File.ReadAllBytes(rgbPath), profile);
            using PreparedVisualInput gray = factory.CreateFromBytes(File.ReadAllBytes(Fixture("gray.png")), profile);
            using PreparedVisualInput alpha = factory.CreateFromBytes(File.ReadAllBytes(Fixture("alpha.png")), profile);
            Assert.AreEqual(new TensorShape(1, 3, 4, 4), rgb.Tensor.Shape);
            Assert.AreEqual(ImageTransformKind.Resize, rgb.Transform.Kind);
            Assert.AreEqual(rgb.InputId, bytes.InputId);
            Assert.IsTrue(((float[])gray.Tensor.Buffer).All(float.IsFinite));
            Assert.IsTrue(((float[])alpha.Tensor.Buffer).All(float.IsFinite));
            Assert.AreEqual("pixel_values", alpha.InputName);
        }

        [TestMethod]
        public void FactoryRejectsBlockerAndEncodedCapacity()
        {
            GenerativeVisionLanguageProfile blocker = Blocker();
            var factory = new OpenCvGenerativeVisionLanguageInputFactory();
            Assert.AreEqual(VisualErrorCodes.CapabilityUnavailable, Assert.ThrowsExactly<VisualException>(() => factory.CreateFromFile(Fixture("rgb.png"), blocker)).ErrorCode);
            GenerativeVisionLanguageProfile small = Profile(maximumImageBytes: 1);
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageLimitExceeded, Assert.ThrowsExactly<VisualException>(() => factory.Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), small)).ErrorCode);
        }

        private static GenerativeVisionLanguageProfile Profile(int maximumImageBytes = 1024 * 1024)
        {
            var vision = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.VisionEncoder, new ModelId("external/blip/opencv/vision"), "onnx", new string('a', 64), 1, 17,
                new[] { Tensor("pixel_values", TensorElementType.Float32, 1, 3, 4, 4) }, new[] { Tensor("encoder_hidden_states", TensorElementType.Float32, 1, 2, 4) }, "commit", "exporter", "BSD-3-Clause", "https://example.invalid/vision");
            var decoder = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.LanguageDecoder, new ModelId("external/blip/opencv/decoder"), "onnx", new string('b', 64), 1, 17,
                new[] { Tensor("input_ids", TensorElementType.Int64, 1, -1), Tensor("attention_mask", TensorElementType.Int64, 1, -1), Tensor("encoder_hidden_states", TensorElementType.Float32, 1, 2, 4), Tensor("encoder_attention_mask", TensorElementType.Int64, 1, 2) }, new[] { Tensor("logits", TensorElementType.Float32, 1, -1, 10) }, "commit", "exporter", "BSD-3-Clause", "https://example.invalid/decoder");
            return new GenerativeVisionLanguageProfile("generative-vlm.blip.opencv", GenerativeVisionLanguageFamily.Blip, "opencv", GenerativeVisionLanguageTask.ImageCaptioning,
                new GenerativeVisionLanguageProcessorContract("processor", new string('c', 64), new VisualSize(4, 4), new[] { 122.7709383f, 116.7460125f, 104.09373615f }, new[] { 68.5005327f, 66.6321579f, 70.32316305f }, "bicubic", "official", maximumImageBytes),
                new GenerativeVisionLanguageTokenizerContract("fake", new string('d', 64), "fake", 10, 1, 2, 0, 3, 4, "exact"), new GenerativeVisionLanguageGenerationContract("generation", new string('e', 64), GenerativeVisionLanguageGenerationMode.Greedy, GenerativeVisionLanguageCacheMode.NoneFullPrefix, 3, 4), "caption", new[] { vision, decoder }, "test", true);
        }

        private static GenerativeVisionLanguageProfile Blocker()
        {
            GenerativeVisionLanguageProfile executable = Profile();
            return new GenerativeVisionLanguageProfile("generative-vlm.blip2.blocker", GenerativeVisionLanguageFamily.Blip2, "opt-2.7b", GenerativeVisionLanguageTask.ImageCaptioning, executable.Processor, executable.Tokenizer, executable.Generation, "", Array.Empty<GenerativeVisionLanguageArtifactContract>(), "external", false, "No official ONNX/OpenVINO Q-Former and language decoder bundle was audited.");
        }

        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), 1_000_000);
        private static string Fixture(string fileName) => Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
