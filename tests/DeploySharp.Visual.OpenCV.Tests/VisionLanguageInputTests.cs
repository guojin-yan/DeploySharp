using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class VisionLanguageInputTests
    {
        private const string ImageSha = "33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c";
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);

        [TestMethod]
        public void ClipAndSigLipFactoriesCoverFileBytesGrayAlphaGeometryAndBatch()
        {
            var factory = new OpenCvVisionLanguageInputFactory();
            VisionLanguageEmbeddingProfile clip = VisionLanguageProfiles.CreateClipVitB32();
            VisionLanguageEmbeddingProfile siglip = VisionLanguageProfiles.CreateSigLipBase();
            using PreparedVisualInput clipFile = factory.CreateFromFile(Fixture("rgb.png"), clip, 2);
            using PreparedVisualInput clipBytes = factory.CreateFromBytes(File.ReadAllBytes(Fixture("alpha.png")), clip);
            using PreparedVisualInput siglipGray = factory.CreateFromBytes(File.ReadAllBytes(Fixture("gray.png")), siglip);
            Assert.AreEqual(new TensorShape(2, 3, 224, 224), clipFile.Tensor.Shape);
            Assert.AreEqual(ImageTransformKind.Crop, clipFile.Transform.Kind);
            Assert.AreEqual(OpenCvImageSource.FromFile(Fixture("rgb.png")).Sha256, clipFile.InputId);
            Assert.AreEqual(new TensorShape(1, 3, 224, 224), clipBytes.Tensor.Shape);
            Assert.AreEqual(new TensorShape(1, 3, 224, 224), siglipGray.Tensor.Shape);
            Assert.AreEqual(ImageTransformKind.Resize, siglipGray.Transform.Kind);
            Assert.IsTrue(((float[])clipBytes.Tensor.Buffer).All(float.IsFinite));
            Assert.IsTrue(((float[])siglipGray.Tensor.Buffer).All(float.IsFinite));
            VisualException limit = Assert.ThrowsExactly<VisualException>(() => factory.CreateFromFile(Fixture("rgb.png"), clip, clip.MaximumImageBatch + 1));
            Assert.AreEqual(VisualErrorCodes.VisionLanguageLimitExceeded, limit.ErrorCode);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialBusPreprocessingRecordsAuditedOpenCvPillowDifference()
        {
            RequireExternal();
            string root = Root();
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_VLM_IMAGE") ?? @"E:\Data\image\bus.jpg";
            Require(image);
            Assert.AreEqual(ImageSha, OpenCvImageSource.FromFile(image).Sha256);
            // OpenCV 5's native INTER_CUBIC has a different kernel/coordinate convention from Pillow BICUBIC.
            // OpenCV 5 原生 INTER_CUBIC 与 Pillow BICUBIC 的内核/坐标约定不同；这里保留实测门控并在文档中记录该限制。
            ComparePixels(new OpenCvVisionLanguageInputFactory().CreateFromFile(image, VisionLanguageProfiles.CreateClipVitB32()), Path.Combine(root, "clip-vit-base-patch32", "clip-pixel-values.f32"), 2.50f, .20f, "clip");
            ComparePixels(new OpenCvVisionLanguageInputFactory().CreateFromFile(image, VisionLanguageProfiles.CreateSigLipBase()), Path.Combine(root, "siglip-base-patch16-224", "siglip-pixel-values.f32"), 2.50f, .20f, "siglip");
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDualEncodersMatchGoldensAcrossOrtAndOpenVinoCpu()
        {
            RequireExternal();
            string root = Root();
            RunFamily(VisionLanguageProfiles.CreateClipVitB32(), Path.Combine(root, "clip-vit-base-patch32"), "clip", new[] { 26.5425701141f, 19.4810085297f, 17.3104190826f });
            RunFamily(VisionLanguageProfiles.CreateSigLipBase(), Path.Combine(root, "siglip-base-patch16-224"), "siglip", new[] { -1.7420225143f, -13.3275032043f, -15.3673458099f });
        }

        private static void RunFamily(VisionLanguageEmbeddingProfile profile, string directory, string prefix, float[] officialLogits)
        {
            string imageModel = Path.Combine(directory, prefix + "-image-encoder-opset17.onnx");
            string textModel = Path.Combine(directory, prefix + "-text-encoder-opset17.onnx");
            string pixelPath = Path.Combine(directory, prefix + "-pixel-values.f32");
            string idsPath = Path.Combine(directory, prefix + "-input-ids.i64");
            string imageEmbeddingPath = Path.Combine(directory, prefix + "-image-embedding.f32");
            string textEmbeddingPath = Path.Combine(directory, prefix + "-text-embeddings.f32");
            foreach (string path in new[] { imageModel, textModel, pixelPath, idsPath, imageEmbeddingPath, textEmbeddingPath }) Require(path);
            long[]? mask = profile.Tokenizer.AttentionMaskRequired ? ReadInt64(Path.Combine(directory, prefix + "-attention-mask.i64")) : null;
            var tokens = new TextTokenBatch(new[] { "a photo of a bus", "a photo of a person", "a photo of a dog" }, ReadInt64(idsPath), 3, profile.Tokenizer.MaximumTokens, profile.Tokenizer.TokenizerId, profile.Tokenizer.Sha256, mask);
            float[] pixels = ReadFloat(pixelPath);
            float[] officialImage = ReadFloat(imageEmbeddingPath);
            float[] officialText = ReadFloat(textEmbeddingPath);
            Evidence ort = Run(profile, imageModel, textModel, pixels, tokens, OnnxRuntimeBackendProvider.BackendId, false);
            Evidence ov = Run(profile, imageModel, textModel, pixels, tokens, OpenVinoBackendProvider.BackendId, true);
            Assert.IsTrue(MaxAbs(ort.Image.CopyValues(), officialImage) <= .00001f);
            Assert.IsTrue(MaxAbs(ort.Text.CopyValues(), officialText) <= .00001f);
            Assert.IsTrue(MaxAbs(ov.Image.CopyValues(), officialImage) <= .005f);
            Assert.IsTrue(MaxAbs(ov.Text.CopyValues(), officialText) <= .005f);
            Assert.IsTrue(MaxAbs(ort.Scores.CopyLogits(), officialLogits) <= .0001f);
            Assert.IsTrue(MaxAbs(ov.Scores.CopyLogits(), officialLogits) <= .05f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, VisionLanguageScorer.RetrieveTexts(profile, ort.Image, ort.Text, 3).Select(value => value.Index).ToArray());
            Assert.AreEqual(0, VisionLanguageScorer.RetrieveTexts(profile, ov.Image, ov.Text, 3)[0].Index);
            string sourcePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_VLM_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (File.Exists(sourcePath))
            {
                Evidence opencv = RunOpenCv(profile, imageModel, textModel, sourcePath, tokens);
                Assert.AreEqual(0, VisionLanguageScorer.RetrieveTexts(profile, opencv.Image, opencv.Text, 3)[0].Index);
                Console.WriteLine("STAGE24_VLM_OPENCV_IMAGE family=" + profile.Family + ";embeddingGoldenMax=" + MaxAbs(opencv.Image.CopyValues(), officialImage).ToString("R", CultureInfo.InvariantCulture) + ";logitGoldenMax=" + MaxAbs(opencv.Scores.CopyLogits(), officialLogits).ToString("R", CultureInfo.InvariantCulture) + ";preprocessMs=" + opencv.PreprocessTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";imageMs=" + opencv.Image.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";textMs=" + opencv.Text.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";scoreMs=" + opencv.ScoreTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";retrievalMs=" + opencv.RetrievalTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            }
            Console.WriteLine("STAGE24_VLM_EVIDENCE family=" + profile.Family + ";imageSha=" + ImageSha + ";ortImageMs=" + ort.Image.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";ortTextMs=" + ort.Text.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoImageMs=" + ov.Image.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoTextMs=" + ov.Text.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";ortImageGoldenMax=" + MaxAbs(ort.Image.CopyValues(), officialImage).ToString("R", CultureInfo.InvariantCulture) + ";openVinoImageGoldenMax=" + MaxAbs(ov.Image.CopyValues(), officialImage).ToString("R", CultureInfo.InvariantCulture));
        }

        private static Evidence Run(VisionLanguageEmbeddingProfile profile, string imageModel, string textModel, float[] pixels, TextTokenBatch tokens, BackendId backend, bool openVino)
        {
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            string device = openVino ? "CPU" : "cpu";
            var bundle = new VisionLanguageArtifactBundle(profile, profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, imageModel, backend), profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, textModel, backend));
            var request = new BackendRequest(BackendCapabilities.TensorInference, backend, device);
            using var session = new VisionLanguageEmbeddingSession(registry, bundle, request);
            var size = profile.ImageSize;
            using var input = new PreparedVisualInput("pixel_values", new Tensor<float>(new TensorShape(1, 3, 224, 224), pixels), new VisualSize(1080, 810), size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(1080, 810), size), inputId: ImageSha);
            VisionLanguageImageEmbedding image = session.EncodeImage(input);
            VisionLanguageTextEmbedding text = session.EncodeText(tokens);
            var scoring = Stopwatch.StartNew();
            VisionLanguageScoreMatrix scores = VisionLanguageScorer.Score(profile, image, text);
            scoring.Stop();
            var retrieval = Stopwatch.StartNew();
            VisionLanguageScorer.RetrieveTexts(profile, image, text, Math.Min(3, text.BatchSize));
            retrieval.Stop();
            return new Evidence(image, text, scores, TimeSpan.Zero, scoring.Elapsed, retrieval.Elapsed);
        }

        private static Evidence RunOpenCv(VisionLanguageEmbeddingProfile profile, string imageModel, string textModel, string imagePath, TextTokenBatch tokens)
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var backend = OnnxRuntimeBackendProvider.BackendId;
            var bundle = new VisionLanguageArtifactBundle(profile, profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, imageModel, backend), profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, textModel, backend));
            using var session = new VisionLanguageEmbeddingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
            var preprocessing = Stopwatch.StartNew();
            using PreparedVisualInput input = new OpenCvVisionLanguageInputFactory().CreateFromFile(imagePath, profile);
            preprocessing.Stop();
            VisionLanguageImageEmbedding image = session.EncodeImage(input);
            VisionLanguageTextEmbedding text = session.EncodeText(tokens);
            var scoring = Stopwatch.StartNew();
            VisionLanguageScoreMatrix scores = VisionLanguageScorer.Score(profile, image, text);
            scoring.Stop();
            var retrieval = Stopwatch.StartNew();
            VisionLanguageScorer.RetrieveTexts(profile, image, text, Math.Min(3, text.BatchSize));
            retrieval.Stop();
            return new Evidence(image, text, scores, preprocessing.Elapsed, scoring.Elapsed, retrieval.Elapsed);
        }

        private static void ComparePixels(PreparedVisualInput input, string goldenPath, float maximumLimit, float meanLimit, string family)
        {
            using (input)
            {
                Require(goldenPath);
                float[] actual = (float[])input.Tensor.Buffer;
                float[] golden = ReadFloat(goldenPath);
                double sum = 0;
                float maximum = 0;
                for (int index = 0; index < actual.Length; index++) { float difference = Math.Abs(actual[index] - golden[index]); maximum = Math.Max(maximum, difference); sum += difference; }
                double mean = sum / actual.Length;
                Console.WriteLine("STAGE24_OPENCV_GOLDEN family=" + family + ";maxAbs=" + maximum.ToString("R", CultureInfo.InvariantCulture) + ";meanAbs=" + mean.ToString("R", CultureInfo.InvariantCulture) + ";actualFirst=" + string.Join(",", actual.Take(8).Select(value => value.ToString("R", CultureInfo.InvariantCulture))) + ";goldenFirst=" + string.Join(",", golden.Take(8).Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
                Assert.IsTrue(maximum <= maximumLimit, family + " preprocessing max abs " + maximum.ToString("R", CultureInfo.InvariantCulture));
                Assert.IsTrue(mean <= meanLimit, family + " preprocessing mean abs " + mean.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static float[] ReadFloat(string path) { byte[] bytes = File.ReadAllBytes(path); var result = new float[bytes.Length / sizeof(float)]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result; }
        private static long[] ReadInt64(string path) { Require(path); byte[] bytes = File.ReadAllBytes(path); var result = new long[bytes.Length / sizeof(long)]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result; }
        private static float MaxAbs(float[] left, float[] right) { Assert.AreEqual(left.Length, right.Length); float result = 0; for (int index = 0; index < left.Length; index++) result = Math.Max(result, Math.Abs(left[index] - right[index])); return result; }
        private static void RequireExternal() { if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_VLM_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_VLM_RUN_EXTERNAL=1 to run authorized stage-24 external evidence."); }
        private static string Root() => Environment.GetEnvironmentVariable("DEPLOYSHARP_VLM_STAGE24_ROOT") ?? @"E:\DeploySharp-Models";
        private static void Require(string path) { if (!File.Exists(path)) Assert.Inconclusive("Required external stage-24 file is missing: " + path); }

        private sealed class Evidence
        {
            public Evidence(VisionLanguageImageEmbedding image, VisionLanguageTextEmbedding text, VisionLanguageScoreMatrix scores, TimeSpan preprocessTime, TimeSpan scoreTime, TimeSpan retrievalTime) { Image = image; Text = text; Scores = scores; PreprocessTime = preprocessTime; ScoreTime = scoreTime; RetrievalTime = retrievalTime; }
            public VisionLanguageImageEmbedding Image { get; }
            public VisionLanguageTextEmbedding Text { get; }
            public VisionLanguageScoreMatrix Scores { get; }
            public TimeSpan PreprocessTime { get; }
            public TimeSpan ScoreTime { get; }
            public TimeSpan RetrievalTime { get; }
        }
    }
}
