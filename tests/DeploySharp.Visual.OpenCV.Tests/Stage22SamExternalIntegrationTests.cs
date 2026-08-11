using System;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage22SamExternalIntegrationTests
    {
        private const string EncoderSha = "95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a";
        private const string DecoderSha = "b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a";
        private const string ImageSha = "bb6082ec3bb90dde8f7553f9bdfb7c09d438a74397df0b2ebabda55c6bcc0df3";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialSamV1PointBoxAndMaskFeedbackMatchOrtOpenVinoAndOfficialPredictor()
        {
            RequireExternal();
            string encoder = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM_V1_ENCODER_ONNX") ?? @"E:\DeploySharp-Models\sam-v1-vit-b\sam_vit_b_image_encoder_opset17.onnx";
            string decoder = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM_V1_DECODER_ONNX") ?? @"E:\DeploySharp-Models\sam-v1-vit-b\sam_vit_b_prompt_mask_decoder_opset17_legacy.onnx";
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM_IMAGE") ?? @"E:\Data\image\boy.jpg";
            string officialMasks = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM_OFFICIAL_MASKS") ?? @"E:\DeploySharp-Models\sam-v1-vit-b\official_sam_boy_masks_3plus1_u8.bin";
            RequireFile(encoder);
            RequireFile(decoder);
            RequireFile(image);
            RequireFile(officialMasks);
            Assert.AreEqual(EncoderSha, Sha256(encoder));
            Assert.AreEqual(DecoderSha, Sha256(decoder));
            Assert.AreEqual(ImageSha, Sha256(image));
            Assert.AreEqual("1a9acd85d97afba6126064b727f69a8c3400776069e53332885fe8260bb99ad4", Sha256(officialMasks));
            byte[] officialMaskBytes = File.ReadAllBytes(officialMasks);
            Assert.AreEqual(1971120, officialMaskBytes.Length);

            RunEvidence ort;
            try { ort = Run(false, encoder, decoder, image); }
            catch (Exception exception) { Console.WriteLine("SAM_ORT_FAILURE=" + exception + ";details=" + (exception as DeploySharpException)?.TechnicalDetails); throw; }
            RunEvidence openVino;
            try { openVino = Run(true, encoder, decoder, image); }
            catch (Exception exception) { Console.WriteLine("SAM_OPENVINO_FAILURE=" + exception + ";details=" + (exception as DeploySharpException)?.TechnicalDetails); throw; }
            Compare(ort.Multi, openVino.Multi, .0001f, .999f);
            Compare(ort.Refined, openVino.Refined, .0001f, .999f);
            Assert.AreEqual(ort.Embedding.Summaries[0].Mean, openVino.Embedding.Summaries[0].Mean, .00001);

            Assert.AreEqual(0.012284046038985252, ort.Embedding.Summaries[0].Mean, .01);
            Assert.AreEqual(-0.9135298728942871f, ort.Embedding.Summaries[0].Minimum, .15f);
            Assert.AreEqual(0.9342491626739502f, ort.Embedding.Summaries[0].Maximum, .15f);
            int[] officialForeground = { 32743, 30301, 27704 };
            float[] officialQuality = { 0.9742338061f, 0.9687876701f, 0.8400300741f };
            Assert.AreEqual(3, ort.Multi.Candidates.Count);
            Console.WriteLine("SAM_ORT_QUALITY=" + string.Join(",", ort.Multi.Candidates.Select(value => value.Quality.ToString("R", CultureInfo.InvariantCulture))) + ";foreground=" + string.Join(",", ort.Multi.Segmentation.Instances.Select(value => value.Mask.ForegroundPixelCount.ToString(CultureInfo.InvariantCulture))) + ";sources=" + string.Join(",", ort.Multi.Candidates.Select(value => value.SourceIndex.ToString(CultureInfo.InvariantCulture))) + ";embeddingMean=" + ort.Embedding.Summaries[0].Mean.ToString("R", CultureInfo.InvariantCulture));
            int sourcePixels = 860 * 573;
            var officialIous = new float[4];
            for (int index = 0; index < 3; index++)
            {
                Assert.AreEqual(officialQuality[index], ort.Multi.Candidates[index].Quality, .12f);
                Assert.IsTrue(MinimumOverMaximum(officialForeground[index], ort.Multi.Segmentation.Instances[index].Mask.ForegroundPixelCount) >= .94f);
                officialIous[index] = IoU(ort.Multi.Segmentation.Instances[index].Mask, officialMaskBytes, index * sourcePixels);
                Assert.IsTrue(officialIous[index] >= .93f, "Official predictor mask IoU was " + officialIous[index].ToString("R", CultureInfo.InvariantCulture));
            }
            Assert.AreEqual(0.9731657505f, ort.Refined.Candidates.Single().Quality, .12f);
            Assert.IsTrue(MinimumOverMaximum(31302, ort.Refined.Segmentation.Instances.Single().Mask.ForegroundPixelCount) >= .94f);
            officialIous[3] = IoU(ort.Refined.Segmentation.Instances.Single().Mask, officialMaskBytes, 3 * sourcePixels);
            Assert.IsTrue(officialIous[3] >= .93f, "Official predictor feedback-mask IoU was " + officialIous[3].ToString("R", CultureInfo.InvariantCulture));

            Console.WriteLine(
                "STAGE22_SAM_V1_EVIDENCE imageSha=" + ImageSha +
                ";encoderSha=" + EncoderSha +
                ";decoderSha=" + DecoderSha +
                ";ortEmbeddingSha=" + ort.Embedding.Summaries[0].Sha256 +
                ";openVinoEmbeddingSha=" + openVino.Embedding.Summaries[0].Sha256 +
                ";ortEncoderMs=" + ort.Embedding.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoEncoderMs=" + openVino.Embedding.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortPromptMs=" + ort.Multi.Timing.PromptDecode.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoPromptMs=" + openVino.Multi.Timing.PromptDecode.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortRestoreMs=" + ort.Multi.Timing.Restore.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoRestoreMs=" + openVino.Multi.Timing.Restore.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortRefineMs=" + ort.Refined.Timing.PromptDecode.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoRefineMs=" + openVino.Refined.Timing.PromptDecode.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";officialMaskIou=" + string.Join(",", officialIous.Select(value => value.ToString("F6", CultureInfo.InvariantCulture))));
        }

        private static RunEvidence Run(bool openVino, string encoderPath, string decoderPath, string image)
        {
            BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            PromptableSegmentationProfile profile = PromptableSegmentationProfiles.CreateSamV1(
                "external/sam-v1-vit-b-stage22",
                new ModelId("external/sam-v1-vit-b-encoder"),
                new ModelId("external/sam-v1-vit-b-prompt-mask-decoder"),
                EncoderSha,
                DecoderSha,
                "dca509fe793f601edb92606367a655c15ac00fdf",
                "torch-2.9.1+cpu legacy torchscript wrapper over official image_encoder; opset17",
                "official scripts/export_onnx_model.py plus dynamo=false compatibility; torch-2.9.1+cpu; opset17");
            var bundle = new PromptableSegmentationArtifactBundle(profile, new[]
            {
                new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).CreateArtifact(encoderPath, backend)),
                new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).CreateArtifact(decoderPath, backend))
            });
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            using var session = new PromptableSegmentationImageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu"));
            using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromFile(image);
            PromptableImageEmbedding embedding = session.SetImage(input);
            var points = new[] { new PromptPoint(430, 280, PromptPointLabel.Foreground), new PromptPoint(300, 150, PromptPointLabel.Background) };
            var box = new RectangleF(200, 80, 450, 480);
            PromptableSegmentationResult multi = session.Predict(new PromptableSegmentationPrompt(points, box, returnMultipleMasks: true, promptId: "official-golden"));
            PromptableMaskFeedback feedback = multi.Candidates[0].LowResolutionLogits.CreateFeedback();
            PromptableSegmentationResult refined = session.Predict(new PromptableSegmentationPrompt(points, box, feedback, returnMultipleMasks: false, promptId: "official-feedback"));
            return new RunEvidence(embedding, multi, refined);
        }

        private static void Compare(PromptableSegmentationResult first, PromptableSegmentationResult second, float scoreTolerance, float maskIou)
        {
            Assert.AreEqual(first.Candidates.Count, second.Candidates.Count);
            for (int index = 0; index < first.Candidates.Count; index++)
            {
                Assert.AreEqual(first.Candidates[index].SourceIndex, second.Candidates[index].SourceIndex);
                Assert.AreEqual(first.Candidates[index].Quality, second.Candidates[index].Quality, scoreTolerance);
                Assert.IsTrue(IoU(first.Segmentation.Instances[index].Mask, second.Segmentation.Instances[index].Mask) >= maskIou);
                Assert.AreEqual(first.Segmentation.Instances[index].BoundingBox, second.Segmentation.Instances[index].BoundingBox);
                Assert.AreEqual(first.Segmentation.Instances[index].Rle!.Runs.Count, second.Segmentation.Instances[index].Rle!.Runs.Count);
            }
        }

        private static float IoU(InstanceBinaryMask first, InstanceBinaryMask second)
        {
            byte[] a = first.ToArray();
            byte[] b = second.ToArray();
            int intersection = 0;
            int union = 0;
            for (int index = 0; index < a.Length; index++)
            {
                if (a[index] != 0 && b[index] != 0) intersection++;
                if (a[index] != 0 || b[index] != 0) union++;
            }
            return union == 0 ? 1f : (float)intersection / union;
        }

        private static float IoU(InstanceBinaryMask first, byte[] second, int offset)
        {
            byte[] a = first.ToArray();
            int intersection = 0;
            int union = 0;
            for (int index = 0; index < a.Length; index++)
            {
                byte b = second[offset + index];
                if (a[index] != 0 && b != 0) intersection++;
                if (a[index] != 0 || b != 0) union++;
            }
            return union == 0 ? 1f : (float)intersection / union;
        }

        private static float MinimumOverMaximum(int first, int second) => (float)Math.Min(first, second) / Math.Max(first, second);
        private static string Sha256(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        private static void RequireFile(string path) { if (!File.Exists(path)) Assert.Inconclusive("External SAM artifact is unavailable: " + path); }
        private static void RequireExternal() { if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_RUN_EXTERNAL_MODELS"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_RUN_EXTERNAL_MODELS=1 to run authorized external SAM models."); }

        private sealed class RunEvidence
        {
            public RunEvidence(PromptableImageEmbedding embedding, PromptableSegmentationResult multi, PromptableSegmentationResult refined) { Embedding = embedding; Multi = multi; Refined = refined; }
            public PromptableImageEmbedding Embedding { get; }
            public PromptableSegmentationResult Multi { get; }
            public PromptableSegmentationResult Refined { get; }
        }
    }
}
