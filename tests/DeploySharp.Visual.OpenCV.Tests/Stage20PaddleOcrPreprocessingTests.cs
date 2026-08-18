using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage20PaddleOcrPreprocessingTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);

        [TestMethod]
        public void LegacyAndTextLineClassifiersKeepColorSizeAndNormalizationDistinct()
        {
            OpenCvPreprocessOptions legacy = OpenCvStage19Preprocessing.CreatePaddleOcrLegacyClassificationOptions();
            OpenCvPreprocessOptions textLine = OpenCvStage19Preprocessing.CreatePaddleOcrTextLineOrientationOptions();

            Assert.AreEqual(new VisualSize(192, 48), legacy.ModelSize);
            Assert.AreEqual(VisualColorOrder.Bgr, legacy.ColorOrder);
            Assert.AreEqual(127.5f, legacy.Means[0]);
            Assert.AreEqual(127.5f, legacy.StandardDeviations[0]);
            Assert.AreEqual(new VisualSize(160, 80), textLine.ModelSize);
            Assert.AreEqual(VisualColorOrder.Rgb, textLine.ColorOrder);
            Assert.AreEqual(123.675f, textLine.Means[0], .0001f);
            Assert.AreEqual(58.395f, textLine.StandardDeviations[0], .0001f);
        }

        [TestMethod]
        public void OfficialMobileDetectorUsesResizeLongAndStride128()
        {
            OpenCvPreprocessOptions options = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(new VisualSize(720, 1150));

            Assert.AreEqual(new VisualSize(640, 1024), options.ModelSize);
            Assert.AreEqual(VisualColorOrder.Bgr, options.ColorOrder);
            Assert.AreEqual(.485f, options.Means[0], .0001f);
            Assert.AreEqual(.229f, options.StandardDeviations[0], .0001f);
            Assert.AreEqual(255f, options.InputDivisors[0], .0001f);
        }

        [TestMethod]
        public void OfficialRecognitionAndOrientationUseArchiveNormalization()
        {
            OpenCvPreprocessOptions recognition = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceRecognitionOptions();
            Assert.AreEqual(new VisualSize(320, 48), recognition.ModelSize);
            Assert.AreEqual(VisualColorOrder.Bgr, recognition.ColorOrder);
            Assert.AreEqual(.5f, recognition.Means[0], .0001f);
            Assert.AreEqual(.5f, recognition.StandardDeviations[0], .0001f);
            Assert.AreEqual(255f, recognition.InputDivisors[0], .0001f);

            OpenCvPreprocessOptions orientation = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceTextLineOrientationOptions();
            Assert.AreEqual(new VisualSize(160, 80), orientation.ModelSize);
            Assert.AreEqual(VisualColorOrder.Rgb, orientation.ColorOrder);
            CollectionAssert.AreEqual(new[] { .485f, .456f, .406f }, orientation.Means.ToArray());
            CollectionAssert.AreEqual(new[] { .229f, .224f, .225f }, orientation.StandardDeviations.ToArray());
            CollectionAssert.AreEqual(new[] { 255f, 255f, 255f }, orientation.InputDivisors.ToArray());
        }

        [TestMethod]
        public void TextLineClassifierFileAndBytesSharePreparedGolden()
        {
            string path = Fixture("ocr-orientation-180.png");
            var factory = new OpenCvVisualInputFactory();
            OpenCvPreprocessOptions options = OpenCvStage19Preprocessing.CreatePaddleOcrTextLineOrientationOptions();
            using PreparedVisualInput fromFile = factory.CreateFromFile(path, "x", options);
            using PreparedVisualInput fromBytes = factory.Create(OpenCvImageSource.FromBytes(File.ReadAllBytes(path)), "x", options, "stage20-png");
            string fileSha = Sha(fromFile.Tensor);
            string bytesSha = Sha(fromBytes.Tensor);
            Assert.AreEqual(fileSha, bytesSha);
            Assert.AreEqual("47b84a19c734aed5ee428d58702a8457573d310749095fe35650c9d7b24c1dda", fileSha);
            Console.WriteLine("STAGE20_CLS_PREPARED_SHA textline=" + fileSha);
        }

        [TestMethod]
        public void LegacyClassifierPreparedGoldenIsStableAndDifferent()
        {
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(Fixture("ocr-orientation-180.png"), "x", OpenCvStage19Preprocessing.CreatePaddleOcrLegacyClassificationOptions());
            string sha = Sha(input.Tensor);
            Console.WriteLine("STAGE20_CLS_PREPARED_SHA legacy=" + sha);
            Assert.AreEqual("59d42e08e5689df6f6dc9a4e79adc0cbcfe2a5bd4fdbb5b1710e2d53d6891307", sha);
            Assert.AreEqual(new TensorShape(1, 3, 48, 192), input.Tensor.Shape);
        }

        [TestMethod]
        public void GrayAndAlphaSourcesProduceOwnedRgbClassifierTensors()
        {
            var factory = new OpenCvVisualInputFactory();
            OpenCvPreprocessOptions options = OpenCvStage19Preprocessing.CreatePaddleOcrTextLineOrientationOptions();
            using PreparedVisualInput gray = factory.CreateFromFile(Fixture("gray.png"), "x", options);
            using PreparedVisualInput alpha = factory.CreateFromFile(Fixture("alpha.png"), "x", options);
            Assert.AreEqual(new TensorShape(1, 3, 80, 160), gray.Tensor.Shape);
            Assert.AreEqual(new TensorShape(1, 3, 80, 160), alpha.Tensor.Shape);
            Assert.AreNotEqual(Sha(gray.Tensor), Sha(alpha.Tensor));
        }

        private static string Sha(ITensor tensor)
        {
            float[] values = tensor.Buffer as float[] ?? throw new AssertFailedException("PaddleOCRCls preprocessing must produce Float32.");
            var bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
