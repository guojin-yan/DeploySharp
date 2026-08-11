using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage23OpenVocabularyExternalIntegrationTests
    {
        private const string ModelSha = "42f9d408c0ba8f941fa5efd503c8d4faa175fff1705686174684ae5e6de29bdd";
        private const string ImageSha = "33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialFixedVocabularyYoloWorldMatchesOrtOpenVinoAndOfficialOnnxPredictor()
        {
            RequireExternal();
            string model = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLOWORLD_ONNX") ?? @"E:\Model\yolo\yolo-world\yolov8s-worldv2-person-bus.onnx";
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_OPEN_VOCAB_IMAGE") ?? @"E:\Data\image\bus.jpg";
            RequireFile(model);
            RequireFile(image);
            Assert.AreEqual(ModelSha, Sha256(model));
            Assert.AreEqual(ImageSha, Sha256(image));

            RunEvidence ort = Run(false, model, image);
            RunEvidence openVino = Run(true, model, image);
            AssertOfficialGolden(ort.Result);
            Compare(ort.Result, openVino.Result, .002f, .5f);
            Assert.AreEqual(ort.Result.VocabularySha256, openVino.Result.VocabularySha256);
            Console.WriteLine(
                "STAGE23_YOLOWORLD_EVIDENCE modelSha=" + ModelSha +
                ";imageSha=" + ImageSha +
                ";vocabularySha=" + ort.Result.VocabularySha256 +
                ";count=" + ort.Result.Detections.Detections.Count.ToString(CultureInfo.InvariantCulture) +
                ";ortMs=" + ort.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoMs=" + openVino.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortInferenceMs=" + ort.Inference.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoInferenceMs=" + openVino.Inference.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void GroundedSamBoxesMatchOfficialSamPredictorAcrossOrtOpenVinoAndReset()
        {
            RequireExternal();
            string detector = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLOWORLD_ONNX") ?? @"E:\Model\yolo\yolo-world\yolov8s-worldv2-person-bus.onnx";
            string encoder = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM_V1_ENCODER_ONNX") ?? @"E:\DeploySharp-Models\sam-v1-vit-b\sam_vit_b_image_encoder_opset17.onnx";
            string decoder = Environment.GetEnvironmentVariable("DEPLOYSHARP_SAM_V1_DECODER_ONNX") ?? @"E:\DeploySharp-Models\sam-v1-vit-b\sam_vit_b_prompt_mask_decoder_opset17_legacy.onnx";
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_OPEN_VOCAB_IMAGE") ?? @"E:\Data\image\bus.jpg";
            string officialMasks = Environment.GetEnvironmentVariable("DEPLOYSHARP_GROUNDED_SAM_OFFICIAL_MASKS") ?? @"E:\DeploySharp-Models\grounded-sam-yoloworldv2-person-bus\official_grounded_sam_bus_5_u8.bin";
            foreach (string path in new[] { detector, encoder, decoder, image, officialMasks }) RequireFile(path);
            Assert.AreEqual("95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a", Sha256(encoder));
            Assert.AreEqual("b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a", Sha256(decoder));
            Assert.AreEqual("2f4ebf145d27b48ff4f5175d886ac22bddbfc95e801c3d749b4e4e3f5efcab4e", Sha256(officialMasks));
            byte[] golden = File.ReadAllBytes(officialMasks);

            GroundedEvidence ort = RunGrounded(false, detector, encoder, decoder, image);
            GroundedEvidence openVino = RunGrounded(true, detector, encoder, decoder, image);
            Assert.AreEqual(5, ort.Result.Instances.Count);
            Assert.AreEqual(ort.Result.Instances.Count, openVino.Result.Instances.Count);
            int[] foreground = { 20859, 31976, 46970, 257087, 11234 };
            float[] quality = { .9748985767f, .9466376305f, .9719399810f, .9881566763f, .9449980259f };
            var officialIou = new float[5];
            for (int index = 0; index < ort.Result.Instances.Count; index++)
            {
                var ortMask = ort.Result.Instances[index].Segmentation.Segmentation.Instances.Single().Mask;
                var openVinoMask = openVino.Result.Instances[index].Segmentation.Segmentation.Instances.Single().Mask;
                officialIou[index] = IoU(ortMask.ToArray(), golden, index * 810 * 1080);
                Assert.IsTrue(officialIou[index] >= .92f, "Official SAM predictor mask IoU was " + officialIou[index].ToString("R", CultureInfo.InvariantCulture));
                Assert.IsTrue(MinimumOverMaximum(foreground[index], ortMask.ForegroundPixelCount) >= .92f);
                Assert.AreEqual(quality[index], ort.Result.Instances[index].Segmentation.Candidates.Single().Quality, .12f);
                Assert.IsTrue(IoU(ortMask.ToArray(), openVinoMask.ToArray(), 0) >= .999f);
                Assert.AreEqual(ort.Result.Instances[index].DetectionIndex, ort.Result.Instances[index].Segmentation.Prompt.PromptId == "grounded-detection-" + index.ToString(CultureInfo.InvariantCulture) ? index : -1);
            }
            Assert.AreEqual(ort.ResetMaskSha256, openVino.ResetMaskSha256, "Reset must reinstall deterministic state across backends.");
            Console.WriteLine(
                "STAGE23_GROUNDED_SAM_EVIDENCE officialMaskSha=2f4ebf145d27b48ff4f5175d886ac22bddbfc95e801c3d749b4e4e3f5efcab4e" +
                ";officialMaskIou=" + string.Join(",", officialIou.Select(value => value.ToString("F6", CultureInfo.InvariantCulture))) +
                ";ortDetectorMs=" + ort.State.DetectorTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoDetectorMs=" + openVino.State.DetectorTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortEncoderMs=" + ort.State.Embedding.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoEncoderMs=" + openVino.State.Embedding.EncoderTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";ortCompose5Ms=" + ort.Result.CompositionTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";openVinoCompose5Ms=" + openVino.Result.CompositionTime.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                ";resetMaskSha=" + ort.ResetMaskSha256);
        }

        private static RunEvidence Run(bool openVino, string model, string image)
        {
            OpenVocabularyDetectionProfile profile = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
            BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu");
            using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(model, backend), registry, request, VisualTaskId.ObjectDetection), request);
            using PreparedVisualInput input = new OpenCvOpenVocabularyInputFactory().CreateFromFile(image, profile);
            var watch = Stopwatch.StartNew();
            VisualInferenceResult result = pipeline.Run(input);
            watch.Stop();
            return new RunEvidence(result.GetValue<OpenVocabularyDetectionResult>(), watch.Elapsed, result.Timing.Inference);
        }

        private static GroundedEvidence RunGrounded(bool openVino, string detectorPath, string encoderPath, string decoderPath, string image)
        {
            OpenVocabularyDetectionProfile detector = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
            PromptableSegmentationProfile sam = PromptableSegmentationProfiles.CreateSamV1(
                "external/sam-v1-vit-b-stage23-grounded",
                new ModelId("external/sam-v1-vit-b-encoder"),
                new ModelId("external/sam-v1-vit-b-prompt-mask-decoder"),
                "95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a",
                "b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a",
                "dca509fe793f601edb92606367a655c15ac00fdf",
                "torch-2.9.1+cpu legacy torchscript wrapper over official image_encoder; opset17",
                "official scripts/export_onnx_model.py plus dynamo=false compatibility; torch-2.9.1+cpu; opset17");
            BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            var bundle = new PromptableSegmentationArtifactBundle(sam, new[]
            {
                new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, sam.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).CreateArtifact(encoderPath, backend)),
                new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, sam.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).CreateArtifact(decoderPath, backend))
            });
            using var registry = new BackendRegistry();
            if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu");
            using var session = new GroundedSamImageSession(registry, detector, detector.CreateArtifact(detectorPath, backend), request, bundle, request);
            var factory = new OpenCvOpenVocabularyInputFactory();
            using GroundedSamPreparedInput input = factory.CreateGroundedSamFromFile(image, detector, sam);
            GroundedSamImageState state = session.SetImage(input);
            GroundedSamResult result = session.SegmentDetections(5, .25f);
            session.ClearImage();
            Assert.IsNull(session.CurrentImage);
            VisualException noState = Assert.ThrowsExactly<VisualException>(() => session.SegmentDetections(1, .25f));
            Assert.AreEqual(VisualErrorCodes.OpenVocabularyStateInvalid, noState.ErrorCode);
            using GroundedSamPreparedInput resetInput = factory.CreateGroundedSamFromFile(image, detector, sam);
            session.SetImage(resetInput);
            GroundedSamResult reset = session.SegmentDetections(1, .25f);
            string resetSha = reset.Instances.Single().Segmentation.Segmentation.Instances.Single().Mask.ComputeSha256();
            return new GroundedEvidence(state, result, resetSha);
        }

        private static void AssertOfficialGolden(OpenVocabularyDetectionResult result)
        {
            Assert.IsTrue(result.Detections.Detections.Count >= 5);
            int[] labels = { 0, 0, 0, 1, 0 };
            float[] scores = { .9133244157f, .8983264565f, .8956039548f, .8942797184f, .6997551322f };
            float[,] boxes =
            {
                { 668.8041992f, 392.4747925f, 809.9835205f, 877.4118042f },
                { 221.6071930f, 406.6691589f, 345.2045288f, 858.4848022f },
                { 50.3109741f, 397.9634705f, 246.3343201f, 902.2511597f },
                { 23.3081551f, 231.8772736f, 802.8672485f, 743.2628784f },
                { .5068216f, 545.8725586f, 79.6457748f, 873.5904541f }
            };
            for (int index = 0; index < 5; index++)
            {
                Detection detection = result.Detections.Detections[index];
                Assert.AreEqual(labels[index], detection.Label.Index, "Official class/order mismatch at " + index);
                Assert.AreEqual(scores[index], detection.Label.Score, .003f, "Official score mismatch at " + index);
                Assert.AreEqual(boxes[index, 0], detection.Box.X, 1f, "Official X1 mismatch at " + index);
                Assert.AreEqual(boxes[index, 1], detection.Box.Y, 1f, "Official Y1 mismatch at " + index);
                Assert.AreEqual(boxes[index, 2] - boxes[index, 0], detection.Box.Width, 1f, "Official width mismatch at " + index);
                Assert.AreEqual(boxes[index, 3] - boxes[index, 1], detection.Box.Height, 1f, "Official height mismatch at " + index);
                Assert.AreEqual(detection.Label.Label, result.Matches[index].Phrase);
            }
        }

        private static void Compare(OpenVocabularyDetectionResult first, OpenVocabularyDetectionResult second, float scoreTolerance, float boxTolerance)
        {
            Assert.AreEqual(first.Detections.Detections.Count, second.Detections.Detections.Count, "Threshold and NMS decisions differ.");
            for (int index = 0; index < first.Detections.Detections.Count; index++)
            {
                Detection left = first.Detections.Detections[index];
                Detection right = second.Detections.Detections[index];
                Assert.AreEqual(left.Label.Index, right.Label.Index);
                Assert.AreEqual(left.Label.Label, right.Label.Label);
                Assert.AreEqual(left.Label.Score, right.Label.Score, scoreTolerance);
                Assert.AreEqual(left.Box.X, right.Box.X, boxTolerance);
                Assert.AreEqual(left.Box.Y, right.Box.Y, boxTolerance);
                Assert.AreEqual(left.Box.Width, right.Box.Width, boxTolerance);
                Assert.AreEqual(left.Box.Height, right.Box.Height, boxTolerance);
                CollectionAssert.AreEqual(left.Label.Index == 0 ? new[] { 49406, 2533, 49407 } : new[] { 49406, 2840, 49407 }, second.Matches[index].TokenIds.Take(3).ToArray());
            }
        }

        private static float IoU(byte[] first, byte[] second, int offset)
        {
            int intersection = 0;
            int union = 0;
            for (int index = 0; index < first.Length; index++)
            {
                byte right = second[offset + index];
                if (first[index] != 0 && right != 0) intersection++;
                if (first[index] != 0 || right != 0) union++;
            }
            return union == 0 ? 1f : (float)intersection / union;
        }

        private static float MinimumOverMaximum(int first, int second) => (float)Math.Min(first, second) / Math.Max(first, second);

        private static string Sha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void RequireExternal() { if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_OPEN_VOCAB_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE23_OPEN_VOCAB_EXTERNAL=1 to run authorized local open-vocabulary artifacts."); }
        private static void RequireFile(string path) { if (!File.Exists(path)) Assert.Inconclusive("External stage 23 file is unavailable: " + path); }

        private sealed class RunEvidence
        {
            internal RunEvidence(OpenVocabularyDetectionResult result, TimeSpan elapsed, TimeSpan inference) { Result = result; Elapsed = elapsed; Inference = inference; }
            internal OpenVocabularyDetectionResult Result { get; }
            internal TimeSpan Elapsed { get; }
            internal TimeSpan Inference { get; }
        }

        private sealed class GroundedEvidence
        {
            internal GroundedEvidence(GroundedSamImageState state, GroundedSamResult result, string resetMaskSha256) { State = state; Result = result; ResetMaskSha256 = resetMaskSha256; }
            internal GroundedSamImageState State { get; }
            internal GroundedSamResult Result { get; }
            internal string ResetMaskSha256 { get; }
        }
    }
}
