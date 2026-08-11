using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage25GenerativeVisionLanguageExternalIntegrationTests
    {
        private const string ImageSha = "33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c";
        private const string OfficialText = "a group of people standing in front of a bus";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialBlipCaptionMatchesTokenizerOrtOpenVinoAndOpenCv()
        {
            RequireExternal();
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_BLIP_MODEL_ROOT") ?? @"E:\DeploySharp-Models\blip-caption-base";
            string converted = Path.Combine(root, "converted-opset17");
            string vision = Path.Combine(converted, "vision_encoder.onnx");
            string decoder = Path.Combine(converted, "text_decoder_full_prefix.onnx");
            string vocab = Path.Combine(root, "bert-base-uncased-vocab.txt");
            string pixelsPath = Path.Combine(converted, "pixel_values.f32");
            string encoderGoldenPath = Path.Combine(converted, "encoder_hidden_states.f32");
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_BLIP_IMAGE") ?? @"E:\Data\image\bus.jpg";
            foreach (string path in new[] { vision, decoder, vocab, pixelsPath, encoderGoldenPath, imagePath }) Require(path);

            GenerativeVisionLanguageProfile profile = GenerativeVisionLanguageProfiles.CreateBlipCaptionBase();
            var tokenizer = new BlipBertTokenizer(vocab, profile.Tokenizer);
            GenerativeTokenSequence prefix = tokenizer.EncodePrefix(profile, GenerativeVisionLanguageRequest.Caption());
            CollectionAssert.AreEqual(new long[] { 30522, 1037, 3861, 1997 }, prefix.CopyTokenIds());
            Assert.AreEqual("a group of people standing in front of a bus", tokenizer.DecodeCompletion(new[] { 1037, 2177, 1997, 2111, 3061, 1999, 2392, 1997, 1037, 3902, 102 }));

            float[] officialPixels = ReadFloat(pixelsPath);
            float[] officialEncoder = ReadFloat(encoderGoldenPath);
            PixelDifference ortEncoderDifference = CompareEncoder(profile, vision, officialPixels, officialEncoder, false);
            PixelDifference openVinoEncoderDifference = CompareEncoder(profile, vision, officialPixels, officialEncoder, true);
            Assert.IsTrue(ortEncoderDifference.Maximum <= .0005f && ortEncoderDifference.Mean <= .000005, "ORT/PyTorch encoder max=" + ortEncoderDifference.Maximum.ToString("R", CultureInfo.InvariantCulture) + ";mean=" + ortEncoderDifference.Mean.ToString("R", CultureInfo.InvariantCulture));
            Assert.IsTrue(openVinoEncoderDifference.Maximum <= .002f && openVinoEncoderDifference.Mean <= .00002, "OpenVINO/PyTorch encoder max=" + openVinoEncoderDifference.Maximum.ToString("R", CultureInfo.InvariantCulture) + ";mean=" + openVinoEncoderDifference.Mean.ToString("R", CultureInfo.InvariantCulture));
            Evidence ort = Run(profile, tokenizer, vision, decoder, officialPixels, false);
            Evidence openVino = Run(profile, tokenizer, vision, decoder, officialPixels, true);
            AssertResult(ort.Result);
            AssertResult(openVino.Result);
            AssertOfficialSelectedLogits(ort.Result, .0002f);
            AssertOfficialSelectedLogits(openVino.Result, .002f);
            CollectionAssert.AreEqual(ort.Result.Generation.TokenIds.ToArray(), openVino.Result.Generation.TokenIds.ToArray());
            Assert.AreEqual(ort.Result.Generation.Text, openVino.Result.Generation.Text);

            var preprocess = Stopwatch.StartNew();
            using PreparedVisualInput openCvInput = new OpenCvGenerativeVisionLanguageInputFactory().CreateFromFile(imagePath, profile);
            preprocess.Stop();
            Assert.AreEqual(ImageSha, openCvInput.InputId);
            PixelDifference difference = ComparePixels((float[])openCvInput.Tensor.Buffer, officialPixels);
            Assert.IsTrue(difference.Maximum <= .02f, "OpenCV/Pillow BLIP pixel max abs: " + difference.Maximum.ToString("R", CultureInfo.InvariantCulture));
            Assert.IsTrue(difference.Mean <= .000001, "OpenCV/Pillow BLIP pixel mean abs: " + difference.Mean.ToString("R", CultureInfo.InvariantCulture));
            Evidence openCv = RunPrepared(profile, tokenizer, vision, decoder, openCvInput, false);
            AssertResult(openCv.Result);

            Console.WriteLine(
                "STAGE25_BLIP_EVIDENCE text=" + ort.Result.Generation.Text +
                ";ortStateSha=" + ort.State.ValueSha256 +
                ";openVinoStateSha=" + openVino.State.ValueSha256 +
                ";ortEncoderMs=" + ort.State.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortDecoderMs=" + ort.Result.Timing.DecoderTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoEncoderMs=" + openVino.State.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoDecoderMs=" + openVino.Result.Timing.DecoderTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openCvPreprocessMs=" + preprocess.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openCvEncoderMs=" + openCv.State.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openCvDecoderMs=" + openCv.Result.Timing.DecoderTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortEncoderGoldenMaxAbs=" + ortEncoderDifference.Maximum.ToString("R", CultureInfo.InvariantCulture) +
                ";ortEncoderGoldenMeanAbs=" + ortEncoderDifference.Mean.ToString("R", CultureInfo.InvariantCulture) +
                ";openVinoEncoderGoldenMaxAbs=" + openVinoEncoderDifference.Maximum.ToString("R", CultureInfo.InvariantCulture) +
                ";openVinoEncoderGoldenMeanAbs=" + openVinoEncoderDifference.Mean.ToString("R", CultureInfo.InvariantCulture) +
                ";openCvPillowMaxAbs=" + difference.Maximum.ToString("R", CultureInfo.InvariantCulture) +
                ";openCvPillowMeanAbs=" + difference.Mean.ToString("R", CultureInfo.InvariantCulture));
        }

        private static PixelDifference CompareEncoder(GenerativeVisionLanguageProfile profile, string vision, float[] pixels, float[] expected, bool openVino)
        {
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            var backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            ModelArtifact artifact = profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, vision, backend);
            using IInferenceSession session = registry.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu"), new SessionOptions(1, false));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create("pixel_values", new Tensor<float>(new TensorShape(1, 3, 384, 384), (float[])pixels.Clone())), default);
            return ComparePixels((float[])outputs.GetRequired("encoder_hidden_states").Buffer, expected);
        }

        private static Evidence Run(GenerativeVisionLanguageProfile profile, IGenerativeVisionLanguageTokenizer tokenizer, string vision, string decoder, float[] pixels, bool openVino)
        {
            var source = new VisualSize(810, 1080);
            using var input = new PreparedVisualInput("pixel_values", new Tensor<float>(new TensorShape(1, 3, 384, 384), (float[])pixels.Clone()), source, profile.Processor.ImageSize, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, profile.Processor.ImageSize), inputId: ImageSha);
            return RunPrepared(profile, tokenizer, vision, decoder, input, openVino);
        }

        private static Evidence RunPrepared(GenerativeVisionLanguageProfile profile, IGenerativeVisionLanguageTokenizer tokenizer, string vision, string decoder, PreparedVisualInput input, bool openVino)
        {
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            var backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            var bundle = new GenerativeVisionLanguageArtifactBundle(profile, new[]
            {
                new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, vision, backend)),
                new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoder, backend))
            });
            using var session = new GenerativeVisionLanguageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu"));
            GenerativeVisionLanguageImageState state = session.SetImage(input);
            GenerativeVisionLanguageResult result = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
            session.ClearImage();
            VisualException reset = Assert.ThrowsExactly<VisualException>(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer));
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageStateInvalid, reset.ErrorCode);
            return new Evidence(state, result);
        }

        private static void AssertResult(GenerativeVisionLanguageResult result)
        {
            Assert.AreEqual(OfficialText, result.Generation.Text);
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.Generation.FinishReason);
            CollectionAssert.AreEqual(new[] { 1037, 2177, 1997, 2111, 3061, 1999, 2392, 1997, 1037, 3902, 102 }, result.Generation.TokenIds.ToArray());
            Assert.AreEqual(result.Generation.TokenIds.Count, result.TokenScores.Count);
            Assert.AreEqual("a picture of ", result.NormalizedPrompt);
        }

        private static void AssertOfficialSelectedLogits(GenerativeVisionLanguageResult result, float tolerance)
        {
            float[] expected = { 10.5699615f, 10.9010115f, 12.0279207f, 11.9441881f, 10.2663393f, 9.8283205f, 11.6924953f, 11.9117479f, 12.0055761f, 11.2998753f, 11.1993036f };
            Assert.AreEqual(expected.Length, result.TokenScores.Count);
            for (int index = 0; index < expected.Length; index++) Assert.AreEqual(expected[index], result.TokenScores[index].Logit, tolerance, "Selected logit mismatch at step " + index.ToString(CultureInfo.InvariantCulture));
        }

        private static PixelDifference ComparePixels(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            float maximum = 0;
            double sum = 0;
            for (int index = 0; index < actual.Length; index++) { float value = Math.Abs(actual[index] - expected[index]); maximum = Math.Max(maximum, value); sum += value; }
            return new PixelDifference(maximum, sum / actual.Length);
        }

        private static float[] ReadFloat(string path) { byte[] bytes = File.ReadAllBytes(path); var values = new float[bytes.Length / sizeof(float)]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values; }
        private static void RequireExternal() { if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_BLIP_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_BLIP_RUN_EXTERNAL=1 to run authorized BLIP external evidence."); }
        private static void Require(string path) { if (!File.Exists(path)) Assert.Inconclusive("Required external BLIP file is missing: " + path); }

        private sealed class Evidence
        {
            internal Evidence(GenerativeVisionLanguageImageState state, GenerativeVisionLanguageResult result) { State = state; Result = result; }
            internal GenerativeVisionLanguageImageState State { get; }
            internal GenerativeVisionLanguageResult Result { get; }
        }

        private readonly struct PixelDifference
        {
            internal PixelDifference(float maximum, double mean) { Maximum = maximum; Mean = mean; }
            internal float Maximum { get; }
            internal double Mean { get; }
        }
    }
}
