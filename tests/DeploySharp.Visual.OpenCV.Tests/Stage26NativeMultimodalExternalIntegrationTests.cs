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
    public sealed class Stage26NativeMultimodalExternalIntegrationTests
    {
        private static readonly int[] OfficialVqaTokens = { 785, 17438, 2383, 374, 304, 8453, 13, 151645 };
        private static readonly int[] Ort128OfficialPixelTokens = { 785, 17438, 2383, 304, 279, 2168, 18689, 1467, 304, 2176, 6364, 323, 8453, 13, 576, 6364 };
        private static readonly int[] OpenVinoAndOpenCvTokens = { 785, 17438, 2383, 374, 304, 6364, 323, 8453, 13, 151645 };

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialLlavaOneVisionMatchesPromptPixelsOrtAndOpenVino()
        {
            RequireExternal();
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_NATIVE_VLM_MODEL_ROOT") ?? @"E:\DeploySharp-Models\llava-onevision-qwen2-0.5b-ov-hf";
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_NATIVE_VLM_IMAGE") ?? @"E:\Data\ocr\demo_2.jpg";
            string evidence = Path.Combine(root, "evidence", "ocr-demo2");
            string vision = Path.Combine(root, "official-onnx-int8", "vision_encoder.onnx");
            string embedding = Path.Combine(root, "official-onnx-int8", "embed_tokens_int8.onnx");
            string decoder = Path.Combine(root, "official-onnx-int8", "decoder_model_merged_int8.onnx");
            string newline = Path.Combine(evidence, "image_newline.f32");
            string officialPixelsPath = Path.Combine(evidence, "pixel_values.f32");
            foreach (string path in new[] { root, imagePath, vision, embedding, decoder, newline, officialPixelsPath }) Require(path);

            NativeMultimodalProfile profile = NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
            var tokenizer = new Qwen2NativeMultimodalTokenizer(root, profile.Tokenizer);
            NativeMultimodalTokenSequence captionPrompt = tokenizer.Encode(profile, GenerativeVisionLanguageRequest.Caption(), 1485);
            NativeMultimodalTokenSequence vqaPrompt = tokenizer.Encode(profile, GenerativeVisionLanguageRequest.Question("What languages are visible on this clothing label?"), 1485);
            CollectionAssert.AreEqual(ReadLong(Path.Combine(evidence, "caption_input_ids.i64")), captionPrompt.CopyTokenIds());
            CollectionAssert.AreEqual(ReadLong(Path.Combine(evidence, "vqa_input_ids.i64")), vqaPrompt.CopyTokenIds());

            var preprocessWatch = Stopwatch.StartNew();
            using NativeMultimodalPreparedImage prepared = new OpenCvNativeMultimodalInputFactory().CreateFromFile(imagePath, profile);
            preprocessWatch.Stop();
            Assert.AreEqual("957a9cc15da49312277796126be225e0ee653f3316578c12d626fa43fbe9561b", prepared.Input.InputId);
            Assert.AreEqual(new NativeMultimodalImageGrid(1, 1), prepared.Grid);
            Assert.AreEqual(1485, prepared.PackedImageTokens);
            Difference pixelDifference = Compare((float[])prepared.Input.Tensor.Buffer, ReadFloat(officialPixelsPath));
            Assert.IsTrue(pixelDifference.Maximum <= .02f, "OpenCV/Pillow max=" + pixelDifference.Maximum.ToString("R", CultureInfo.InvariantCulture));
            Assert.IsTrue(pixelDifference.Mean <= .000002, "OpenCV/Pillow mean=" + pixelDifference.Mean.ToString("R", CultureInfo.InvariantCulture));

            using NativeMultimodalPreparedImage officialPrepared = CreateOfficialPrepared(profile, ReadFloat(officialPixelsPath));
            Evidence ort = Run(profile, tokenizer, officialPrepared, newline, vision, embedding, decoder, false);
            Console.WriteLine("STAGE26_ORT_OFFICIAL_PACKED_SHA=" + ort.Image.FeatureState.ValueSha256 + ";text=" + ort.Result.Generation.Generation.Text + ";tokens=" + string.Join(",", ort.Result.Generation.Generation.TokenIds));
            Evidence openVino = Run(profile, tokenizer, officialPrepared, newline, vision, embedding, decoder, true);
            Console.WriteLine("STAGE26_OPENVINO_OFFICIAL_PACKED_SHA=" + openVino.Image.FeatureState.ValueSha256 + ";text=" + openVino.Result.Generation.Generation.Text + ";tokens=" + string.Join(",", openVino.Result.Generation.Generation.TokenIds));
            Evidence openCv = Run(profile, tokenizer, prepared, newline, vision, embedding, decoder, false);
            Console.WriteLine("STAGE26_ORT_OPENCV_PACKED_SHA=" + openCv.Image.FeatureState.ValueSha256 + ";text=" + openCv.Result.Generation.Generation.Text + ";tokens=" + string.Join(",", openCv.Result.Generation.Generation.TokenIds));
            AssertRuntimeEvidence(ort, vqaPrompt.TokenIds.Count);
            AssertRuntimeEvidence(openVino, vqaPrompt.TokenIds.Count);
            AssertRuntimeEvidence(openCv, vqaPrompt.TokenIds.Count);
            Assert.AreEqual("41e00ddc3d807a8413c31537c9e12be186182504ac72e27180e60f196f675c93", ort.Image.FeatureState.ValueSha256);
            CollectionAssert.AreEqual(Ort128OfficialPixelTokens, ort.Result.Generation.Generation.TokenIds.ToArray());
            Assert.AreEqual(GenerationFinishReason.MaxTokens, ort.Result.Generation.Generation.FinishReason);
            Assert.AreEqual("576879596bdf8459f49a5f36658accdadd7acbdad894685b601bf434060dd163", openVino.Image.FeatureState.ValueSha256);
            CollectionAssert.AreEqual(OpenVinoAndOpenCvTokens, openVino.Result.Generation.Generation.TokenIds.ToArray());
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, openVino.Result.Generation.Generation.FinishReason);
            Assert.AreEqual("662c4f269a9b17050ac82a089e23ae4cf0d6a89cb24610d565c713c1819600f7", openCv.Image.FeatureState.ValueSha256);
            CollectionAssert.AreEqual(OpenVinoAndOpenCvTokens, openCv.Result.Generation.Generation.TokenIds.ToArray());
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, openCv.Result.Generation.Generation.FinishReason);
            Assert.IsFalse(OfficialVqaTokens.SequenceEqual(ort.Result.Generation.Generation.TokenIds));
            Assert.IsFalse(OfficialVqaTokens.SequenceEqual(openVino.Result.Generation.Generation.TokenIds));
            Console.WriteLine(
                "STAGE26_NATIVE_VLM_EVIDENCE text=" + ort.Result.Generation.Generation.Text +
                ";openCvPreprocessMs=" + preprocessWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";pixelMaxAbs=" + pixelDifference.Maximum.ToString("R", CultureInfo.InvariantCulture) +
                ";pixelMeanAbs=" + pixelDifference.Mean.ToString("R", CultureInfo.InvariantCulture) +
                ";ortVisionPackMs=" + ort.Image.FeatureState.EncoderTime.Add(ort.Image.PackingTime).TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortPrefillMs=" + ort.Result.Timing.Prefill.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortDecodeMs=" + ort.Result.Timing.DecodeTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoVisionPackMs=" + openVino.Image.FeatureState.EncoderTime.Add(openVino.Image.PackingTime).TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoPrefillMs=" + openVino.Result.Timing.Prefill.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoDecodeMs=" + openVino.Result.Timing.DecodeTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openCvText=" + openCv.Result.Generation.Generation.Text +
                ";openCvTokens=" + string.Join(",", openCv.Result.Generation.Generation.TokenIds));
        }

        private static Evidence Run(NativeMultimodalProfile profile, INativeMultimodalTokenizer tokenizer, NativeMultimodalPreparedImage prepared, string newline, string vision, string embedding, string decoder, bool openVino)
        {
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            var bundle = new NativeMultimodalArtifactBundle(profile, new[]
            {
                Bind(profile, GenerativeVisionLanguageArtifactRole.VisionEncoder, vision, backend),
                Bind(profile, GenerativeVisionLanguageArtifactRole.TokenEmbedding, embedding, backend),
                Bind(profile, GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoder, backend)
            });
            using var session = new NativeMultimodalSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu"), newline);
            NativeMultimodalImageState image = session.SetImage(prepared);
            NativeMultimodalResult result = session.Generate(GenerativeVisionLanguageRequest.Question("What languages are visible on this clothing label?"), tokenizer);
            session.Clear();
            Assert.AreEqual(VisualErrorCodes.NativeMultimodalStateInvalid, Assert.ThrowsExactly<VisualException>(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer)).ErrorCode);
            return new Evidence(image, result);
        }

        private static GenerativeVisionLanguageArtifactBinding Bind(NativeMultimodalProfile profile, GenerativeVisionLanguageArtifactRole role, string path, BackendId backend) => new GenerativeVisionLanguageArtifactBinding(role, profile.CreateArtifact(role, path, backend));

        private static NativeMultimodalPreparedImage CreateOfficialPrepared(NativeMultimodalProfile profile, float[] pixels)
        {
            var source = new VisualSize(350, 350);
            var input = new PreparedVisualInput("pixel_values", new Tensor<float>(new TensorShape(2, 3, 384, 384), pixels), source, new VisualSize(384, 384), 2, VisualTensorLayout.Nchw, ImageTransform.Resize(source, new VisualSize(384, 384)), inputId: "957a9cc15da49312277796126be225e0ee653f3316578c12d626fa43fbe9561b");
            return new NativeMultimodalPreparedImage(profile.ProfileId, input, new NativeMultimodalImageGrid(1, 1), 1485);
        }

        private static void AssertRuntimeEvidence(Evidence evidence, int promptTokens)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(evidence.Result.Generation.Generation.Text));
            Assert.IsTrue(evidence.Result.Generation.Generation.TokenIds.Count > 0 && evidence.Result.Generation.Generation.TokenIds.Count <= 16);
            Assert.AreEqual(promptTokens + evidence.Result.Generation.Generation.TokenIds.Count - 1, evidence.Result.KvState.PastTokens);
            Assert.AreEqual(24, evidence.Result.KvState.Layers);
            Assert.AreEqual(2, evidence.Result.KvState.KeyValueHeads);
            Assert.AreEqual(64, evidence.Result.KvState.HeadDimension);
        }

        private static Difference Compare(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            float maximum = 0;
            double total = 0;
            for (int index = 0; index < actual.Length; index++) { float value = Math.Abs(actual[index] - expected[index]); maximum = Math.Max(maximum, value); total += value; }
            return new Difference(maximum, total / actual.Length);
        }

        private static float[] ReadFloat(string path) { byte[] bytes = File.ReadAllBytes(path); var values = new float[bytes.Length / sizeof(float)]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values; }
        private static long[] ReadLong(string path) { byte[] bytes = File.ReadAllBytes(path); var values = new long[bytes.Length / sizeof(long)]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values; }
        private static void RequireExternal() { if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_NATIVE_VLM_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_NATIVE_VLM_RUN_EXTERNAL=1 to run the external native multimodal gate."); }
        private static void Require(string path) { if (!File.Exists(path) && !Directory.Exists(path)) Assert.Inconclusive("External Stage 26 asset is missing: " + path); }

        private readonly struct Difference { internal Difference(float maximum, double mean) { Maximum = maximum; Mean = mean; } internal float Maximum { get; } internal double Mean { get; } }
        private sealed class Evidence { internal Evidence(NativeMultimodalImageState image, NativeMultimodalResult result) { Image = image; Result = result; } internal NativeMultimodalImageState Image { get; } internal NativeMultimodalResult Result { get; } }
    }
}
