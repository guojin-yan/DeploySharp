using System;
using System.IO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenVocabularyInputTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);

        [TestMethod]
        [DataRow("rgb.png")]
        [DataRow("gray.png")]
        [DataRow("alpha.png")]
        public void FixedVocabularyDetectorCoversEncodedSourcesAndChannels(string fixture)
        {
            OpenVocabularyDetectionProfile profile = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
            byte[] bytes = File.ReadAllBytes(Fixture(fixture));
            OpenCvImageSource source = OpenCvImageSource.FromBytes(bytes);
            using PreparedVisualInput input = new OpenCvOpenVocabularyInputFactory().Create(source, profile);
            Assert.AreEqual(source.Sha256, input.InputId);
            Assert.AreEqual(new TensorShape(1, 3, 640, 640), input.Tensor.Shape);
            Assert.AreEqual(VisualColorOrder.Rgb, input.Preprocessing.ColorOrder);
            Assert.AreEqual(ImageTransformKind.Letterbox, input.Transform.Kind);
            float[] values = ((Tensor<float>)input.Tensor).ToArray();
            Assert.IsTrue(Array.TrueForAll(values, value => value >= 0f && value <= 1f));
        }

        [TestMethod]
        public void GroundedSamUsesOneSourceIdentityForTwoDifferentTransforms()
        {
            OpenVocabularyDetectionProfile detector = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
            PromptableSegmentationProfile sam = PromptableSegmentationProfiles.CreateSamV1("tests/stage23-sam", new ModelId("tests/sam-encoder"), new ModelId("tests/sam-decoder"), Sha, Sha, "dca509fe793f601edb92606367a655c15ac00fdf", "test", "test", imageSize: 8, embeddingSize: 1, lowResolutionMaskSize: 2);
            OpenCvImageSource source = OpenCvImageSource.FromFile(Fixture("rgb.png"));
            using GroundedSamPreparedInput input = new OpenCvOpenVocabularyInputFactory().CreateGroundedSam(source, detector, sam);
            Assert.AreEqual(source.Sha256, input.SourceSha256);
            Assert.AreEqual(input.DetectorInput.InputId, input.SegmentationInput.InputId);
            Assert.AreEqual(input.DetectorInput.SourceSize, input.SegmentationInput.SourceSize);
            Assert.AreEqual(new TensorShape(1, 3, 640, 640), input.DetectorInput.Tensor.Shape);
            Assert.AreEqual(new TensorShape(1, 3, 8, 8), input.SegmentationInput.Tensor.Shape);
            Assert.AreEqual(ImageTransformKind.Letterbox, input.DetectorInput.Transform.Kind);
            Assert.AreEqual(ImageTransformKind.Letterbox, input.SegmentationInput.Transform.Kind);
            Assert.IsTrue(input.DetectorInput.Transform.OffsetY > 0f);
            Assert.AreEqual(0f, input.SegmentationInput.Transform.OffsetX);
            Assert.AreEqual(0f, input.SegmentationInput.Transform.OffsetY);
        }
    }
}
