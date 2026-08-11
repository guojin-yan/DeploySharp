using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage19PreprocessingTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestMethod]
        public void PaddleDetectionUsesOfficialMaxSideStrideAndByteSpaceImageNetNormalization()
        {
            OpenCvPreprocessOptions options = OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(new VisualSize(1000, 500), 960, "max");
            Assert.AreEqual(new VisualSize(960, 480), options.ModelSize);
            Assert.AreEqual(VisualColorOrder.Bgr, options.ColorOrder);
            Assert.AreEqual(123.675f, options.Means[0], .0001f);
            Assert.AreEqual(58.395f, options.StandardDeviations[0], .0001f);

            Assert.AreEqual(new VisualSize(96, 64), OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(new VisualSize(99, 50)).ModelSize);
        }

        [TestMethod]
        public void PaddleRecognitionUsesOfficialBgrHalfRangeNormalization()
        {
            OpenCvPreprocessOptions options = OpenCvStage19Preprocessing.CreatePaddleOcrRecognitionOptions(new VisualSize(320, 48));
            Assert.AreEqual(VisualColorOrder.Bgr, options.ColorOrder);
            Assert.AreEqual(127.5f, options.Means[0]);
            Assert.AreEqual(127.5f, options.StandardDeviations[0]);
        }

        [TestMethod]
        public void AnomalibExportOnlyScalesBytesBecauseGraphOwnsImageNetNormalization()
        {
            AnomalibProfile profile = AnomalibProfiles.CreatePatchCore(new ModelId("tests/patchcore"), new AnomalibArtifactContract(14, Sha, "commit", "torch-2.7.1"));
            OpenCvPreprocessOptions options = OpenCvStage19Preprocessing.CreateAnomalibOptions(profile);
            Assert.AreEqual(new VisualSize(256, 256), options.ModelSize);
            Assert.AreEqual(0, options.Means.Count);
            Assert.AreEqual(255f, options.StandardDeviations[0]);
        }

        [TestMethod]
        public void BriaProfilesUseArtifactSpecificByteSpaceNormalization()
        {
            BriaRmbgProfile rmbg14 = BriaRmbgProfiles.CreateRmbg14(new ModelId("tests/rmbg14"), new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", Sha, "commit", "torch", "license"));
            OpenCvPreprocessOptions first = OpenCvStage19Preprocessing.CreateBriaRmbgOptions(rmbg14);
            Assert.AreEqual(127.5f, first.Means[0]);
            Assert.AreEqual(255f, first.StandardDeviations[0]);

            BriaRmbgProfile rmbg20 = BriaRmbgProfiles.CreateRmbg20(new ModelId("tests/rmbg20"), new BriaRmbgProfileOptions(14, new VisualSize(1024, 1024), "pixel_values", "alphas", Sha, "commit", "transformers", "gated"));
            OpenCvPreprocessOptions second = OpenCvStage19Preprocessing.CreateBriaRmbgOptions(rmbg20, new VisualSize(512, 512));
            Assert.AreEqual(127.5f, second.Means[0]);
            Assert.AreEqual(127.5f, second.StandardDeviations[0]);
        }
    }
}
