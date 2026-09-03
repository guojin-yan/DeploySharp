using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class Stage19ModelFamilyTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestMethod]
        public void PaddleProfilesBindOfficialNamesShapesAndCtcClasses()
        {
            PaddleOcrArtifactContract detectionArtifact = PaddleArtifact(11);
            PaddleOcrProfile detection = PaddleOcrProfiles.CreateDetection(new ModelId("tests/paddle-det"), detectionArtifact);
            Assert.AreEqual(PaddleOcrFamily.PaddleOcrDet, detection.Family);
            Assert.AreEqual(VisualTaskId.TextDetection, detection.VisualProfile.Task);
            Assert.AreEqual("x", detection.VisualProfile.Input.Name);
            Assert.AreEqual("fetch_name_0", detection.VisualProfile.Outputs[0].Name);
            Assert.AreEqual(new TensorShape(1, 3, -1, -1), detection.VisualProfile.Input.ShapePattern);
            Assert.AreEqual(4000, ((PaddleDbTextDetectionDecoder)detection.VisualProfile.Decoder).MaximumSide);

            var characters = new OcrCharacterSet("tests.ppocrv5", "1", "A ");
            PaddleOcrProfile recognition = PaddleOcrProfiles.CreateRecognition(new ModelId("tests/paddle-rec"), PaddleArtifact(7), characters);
            Assert.AreEqual(PaddleOcrFamily.PaddleOcrRec, recognition.Family);
            Assert.AreEqual(VisualTaskId.TextRecognition, recognition.VisualProfile.Task);
            Assert.AreEqual(new TensorShape(-1, 3, 48, -1), recognition.VisualProfile.Input.ShapePattern);
            Assert.AreEqual(3L, recognition.VisualProfile.Outputs[0].ShapePattern[2]);
            Assert.AreEqual(48, recognition.CropProfile!.TargetHeight);
            Assert.AreSame(characters, recognition.CharacterSet);
            Assert.AreEqual(Sha, recognition.CreateArtifact("model.onnx").Sha256);
        }

        [TestMethod]
        public void PaddleDbDecoderThresholdsExpandsAndRestoresSourceGeometry()
        {
            PaddleOcrProfile family = PaddleOcrProfiles.CreateDetection(
                new ModelId("tests/paddle-det"), PaddleArtifact(11),
                postprocess: new PaddleDbPostprocessOptions(.3f, .6f, 1.5f, minimumSide: 3, maximumCandidates: 8, maximumRegions: 8));
            VisualModelProfile profile = family.VisualProfile;
            var source = new VisualSize(40, 20);
            var model = new VisualSize(20, 10);
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 10, 20), new float[600]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, model));
            var map = new float[200];
            for (int y = 2; y <= 5; y++) for (int x = 4; x <= 8; x++) map[(y * 20) + x] = .9f;
            var result = (TextDetectionResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 1, 10, 20), map)), CancellationToken.None));
            Assert.AreEqual(1, result.Regions.Count);
            Assert.AreEqual(.9f, result.Regions[0].Score, .0001f);
            Assert.IsTrue(result.Regions[0].AxisAlignedBounds.Width > 8f);
            Assert.IsTrue(result.Regions[0].AxisAlignedBounds.Height > 6f);
            Assert.AreEqual("Fast", result.Regions[0].Metadata["paddle.db.scoreMode"]);
        }

        [TestMethod]
        public void PaddleDbDecoderPreservesRotatedComponentGeometry()
        {
            PaddleOcrProfile family = PaddleOcrProfiles.CreateDetection(new ModelId("tests/paddle-det"), PaddleArtifact(11),
                postprocess: new PaddleDbPostprocessOptions(.3f, .6f, 1.5f, minimumSide: 3, maximumCandidates: 8, maximumRegions: 8));
            VisualModelProfile profile = family.VisualProfile;
            var source = new VisualSize(32, 32);
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 16, 16), new float[768]), source, new VisualSize(16, 16), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, new VisualSize(16, 16)));
            var map = new float[256];
            for (int y = 3; y < 13; y++)
                for (int x = 2; x < 14; x++)
                    if (Math.Abs((x - 8) * 0.5f + (y - 8) * 1.0f) < 3.5f && Math.Abs((x - 8) * 1.0f - (y - 8) * 0.5f) < 5.5f) map[(y * 16) + x] = .95f;

            var result = (TextDetectionResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile,
                InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 1, 16, 16), map)), CancellationToken.None));
            Assert.AreEqual(1, result.Regions.Count);
            Assert.IsTrue(result.Regions[0].Polygon.Vertices.Select(point => point.X).Distinct().Count() > 2);
            Assert.IsTrue(result.Regions[0].Polygon.Vertices.Select(point => point.Y).Distinct().Count() > 2);
            Assert.IsTrue(result.Regions[0].AxisAlignedBounds.Width > result.Regions[0].AxisAlignedBounds.Height);
        }

        [TestMethod]
        public void PaddleDbDecoderRejectsProbabilityMapBeyondConfiguredSide()
        {
            PaddleOcrProfile family = PaddleOcrProfiles.CreateDetection(new ModelId("tests/paddle-det"), PaddleArtifact(11), maximumSide: 4);
            VisualModelProfile profile = family.VisualProfile;
            var size = new VisualSize(8, 8);
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 8, 8), new float[192]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            VisualException exception = Assert.ThrowsExactly<VisualException>(() => profile.Decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 1, 8, 8), new float[64])), CancellationToken.None)));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, exception.ErrorCode);
        }

        [TestMethod]
        public void PaddleDbDecoderAcceptsFloatingPointBoundaryNoise()
        {
            PaddleOcrProfile family = PaddleOcrProfiles.CreateDetection(
                new ModelId("tests/paddle-det"), PaddleArtifact(11),
                postprocess: new PaddleDbPostprocessOptions(.3f, .6f, 1.5f, minimumSide: 1, maximumCandidates: 8, maximumRegions: 8));
            VisualModelProfile profile = family.VisualProfile;
            var source = new VisualSize(8, 8);
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 8, 8), new float[192]), source, source, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, source));
            var map = new float[64];
            for (int y = 2; y <= 5; y++) for (int x = 2; x <= 5; x++) map[(y * 8) + x] = 1.0000001f;
            var result = (TextDetectionResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile,
                InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 1, 8, 8), map)), CancellationToken.None));
            Assert.AreEqual(1, result.Regions.Count);
            Assert.IsTrue(result.Regions[0].Score <= 1f);

            map[0] = float.NaN;
            VisualException nonFinite = Assert.ThrowsExactly<VisualException>(() => profile.Decoder.Decode(new VisualDecodeContext(input, profile,
                InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 1, 8, 8), map)), CancellationToken.None)));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, nonFinite.ErrorCode);
        }

        [TestMethod]
        public void PaddleRecognitionConsumesProbabilityCtcBlankRepeatAndSpace()
        {
            PaddleOcrProfile family = PaddleOcrProfiles.CreateRecognition(new ModelId("tests/paddle-rec"), PaddleArtifact(7), new OcrCharacterSet("tests.ppocrv5", "1", "A "));
            VisualModelProfile profile = family.VisualProfile;
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 48, 16), new float[2304]), new VisualSize(16, 48), new VisualSize(16, 48), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(16, 48), new VisualSize(16, 48)));
            float[] probabilities =
            {
                .9f, .05f, .05f,
                .05f, .9f, .05f,
                .05f, .9f, .05f,
                .05f, .05f, .9f
            };
            var result = (TextRecognitionBatchResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 4, 3), probabilities)), CancellationToken.None));
            Assert.AreEqual("A ", result.Items[0].Text);
            Assert.AreEqual(.9f, result.Items[0].Confidence, .0001f);
        }

        [TestMethod]
        public void AnomalibFourOutputContractProducesOwnedSourceMap()
        {
            AnomalibProfile family = AnomalibProfiles.CreatePadim(new ModelId("tests/padim"), new AnomalibArtifactContract(14, Sha, "commit", "torch-2.7.1"), new VisualSize(2, 2));
            VisualModelProfile profile = family.VisualProfile;
            using var input = new PreparedVisualInput("input", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), new VisualSize(4, 4), new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(4, 4), new VisualSize(2, 2)));
            float[] supplied = { 0f, .25f, .75f, 1f };
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("pred_score", new Tensor<float>(new TensorShape(1, 1), new[] { .75f })),
                new NamedTensor("pred_label", new Tensor<bool>(new TensorShape(1, 1), new[] { true })),
                new NamedTensor("anomaly_map", new Tensor<float>(new TensorShape(1, 1, 2, 2), supplied)),
                new NamedTensor("pred_mask", new Tensor<bool>(new TensorShape(1, 1, 2, 2), new[] { false, false, true, true }))
            });
            var result = (AnomalyDetectionResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            supplied[0] = 1f;
            Assert.AreEqual(.75f, result.ImageScore, .0001f);
            Assert.AreEqual(4, result.NormalizedMap.Width);
            Assert.AreEqual(4, result.NormalizedMap.Height);
            Assert.AreEqual(0f, result.RawMap!.ToArray()[0], .0001f);
            Assert.AreEqual(4, profile.Outputs.Count);
            Assert.AreEqual(1L, profile.Input.ShapePattern[0]);
        }

        [TestMethod]
        public void AnomalibDynamicBatchDecodesEachMapWithItsOwnSourceGeometry()
        {
            AnomalibProfile family = AnomalibProfiles.CreatePadim(new ModelId("tests/padim-batch"), new AnomalibArtifactContract(14, Sha, "commit", "torch-2.7.1"), new VisualSize(2, 2), maximumBatch: 2);
            VisualModelProfile profile = family.VisualProfile;
            var firstSource = new VisualSize(4, 4);
            var secondSource = new VisualSize(2, 2);
            var firstTransform = ImageTransform.Resize(firstSource, new VisualSize(2, 2));
            var secondTransform = ImageTransform.Resize(secondSource, new VisualSize(2, 2));
            using var input = new PreparedVisualInput("input", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), firstSource, new VisualSize(2, 2), 2, VisualTensorLayout.Nchw, firstTransform,
                batchFrames: new[] { new VisualInputFrame(firstSource, new VisualSize(2, 2), firstTransform, "first"), new VisualInputFrame(secondSource, new VisualSize(2, 2), secondTransform, "second") });
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("pred_score", new Tensor<float>(new TensorShape(2, 1), new[] { .75f, .25f })),
                new NamedTensor("pred_label", new Tensor<bool>(new TensorShape(2, 1), new[] { true, false })),
                new NamedTensor("anomaly_map", new Tensor<float>(new TensorShape(2, 1, 2, 2), new[] { 0f, .25f, .75f, 1f, 1f, .5f, .25f, 0f })),
                new NamedTensor("pred_mask", new Tensor<bool>(new TensorShape(2, 1, 2, 2), new[] { false, false, true, true, true, false, false, false }))
            });

            object decoded = profile.Decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            var batch = (AnomalyDetectionBatchResult)decoded;
            Assert.AreEqual(2, batch.Count);
            Assert.AreEqual(.75f, batch[0].ImageScore, .0001f);
            Assert.AreEqual(.25f, batch[1].ImageScore, .0001f);
            Assert.AreEqual(4, batch[0].NormalizedMap.Width);
            Assert.AreEqual(2, batch[1].NormalizedMap.Width);
            Assert.AreEqual("second", input.BatchFrames[1].InputId);
            Assert.AreEqual(-1L, profile.Input.ShapePattern[0]);
        }

        [TestMethod]
        public void BriaAlphaDecoderRestoresOwnsAndComposites()
        {
            BriaRmbgProfile family = BriaRmbgProfiles.CreateRmbg14(new ModelId("tests/rmbg-1.4"), new BriaRmbgProfileOptions(11, new VisualSize(2, 2), "input", "output", Sha, "2ceba5a5", "torch-2.1.0", "bria-rmbg-1.4"));
            VisualModelProfile profile = family.VisualProfile;
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("input", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            float[] supplied = { 0f, .25f, .75f, 1f };
            var result = (BackgroundRemovalResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("output", new Tensor<float>(new TensorShape(1, 1, 2, 2), supplied)), CancellationToken.None));
            CollectionAssert.AreEqual(new[] { 0f, .25f, .75f, 1f }, supplied);
            supplied[3] = 0f;
            CollectionAssert.AreEqual(new[] { 0f, .25f, .75f, 1f }, result.Alpha.ToArray());
            Assert.AreEqual(64, result.Alpha.ComputeSha256().Length);
            byte[] composite = result.Alpha.CompositeRgb(new byte[] { 255, 0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0 }, 0, 0, 255);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 64, 0, 191, 191, 0, 64, 255, 0, 0 }, composite);

            InferenceOutputs invalid = InferenceOutputs.Create("output", new Tensor<float>(new TensorShape(1, 1, 2, 2), new[] { 0f, .25f, .75f, 1.1f }));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => profile.Decoder.Decode(new VisualDecodeContext(input, profile, invalid, CancellationToken.None))).ErrorCode);
        }

        [TestMethod]
        public void BriaAlphaDecoderSupportsDynamicImageBatch()
        {
            var options = new BriaRmbgProfileOptions(11, new VisualSize(2, 2), "input", "output", Sha, "2ceba5a5", "torch-2.1.0", "bria-rmbg-1.4", maximumBatch: 2);
            BriaRmbgProfile family = BriaRmbgProfiles.CreateRmbg14(new ModelId("tests/rmbg-batch"), options);
            VisualModelProfile profile = family.VisualProfile;
            var firstSource = new VisualSize(4, 4);
            var modelSize = new VisualSize(2, 2);
            var firstTransform = ImageTransform.Resize(firstSource, modelSize);
            var secondTransform = ImageTransform.Resize(modelSize, modelSize);
            using var input = new PreparedVisualInput("input", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), firstSource, modelSize, 2, VisualTensorLayout.Nchw, firstTransform,
                batchFrames: new[] { new VisualInputFrame(firstSource, modelSize, firstTransform, "first"), new VisualInputFrame(modelSize, modelSize, secondTransform, "second") });
            var output = new float[] { 0f, .25f, .75f, 1f, 1f, .5f, .25f, 0f };
            object decoded = profile.Decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create("output", new Tensor<float>(new TensorShape(2, 1, 2, 2), output)), CancellationToken.None));
            var batch = (BackgroundRemovalBatchResult)decoded;
            Assert.AreEqual(2, batch.Count);
            Assert.AreEqual(4, batch[0].Alpha.Width);
            Assert.AreEqual(2, batch[1].Alpha.Width);
            Assert.AreEqual("second", input.BatchFrames[1].InputId);
            Assert.AreEqual(-1L, profile.Input.ShapePattern[0]);
        }

        [TestMethod]
        public void AlphaDecoderConvertsFloat64LogitsWithoutOutOfRangeValues()
        {
            var decoder = new AlphaMattingDecoder(new AlphaOutputSchema("output", AlphaTensorLayout.Nchw, outputIsProbability: false));
            var profile = new VisualModelProfile(
                "tests/alpha-logits",
                new ModelId("tests/alpha-logits"),
                VisualTaskId.ForegroundMatting,
                "1",
                "onnx",
                new VisualInputBinding("input", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("output", TensorElementType.Float64, new TensorShape(1, 1, 2, 2)) },
                Array.Empty<VisualLabel>(),
                decoder);
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("input", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));

            var invalid = InferenceOutputs.Create("output", new Tensor<double>(new TensorShape(1, 1, 2, 2), new[] { double.NegativeInfinity, -2d, 0d, 2d }));
            VisualException exception = Assert.ThrowsExactly<VisualException>(() => profile.Decoder.Decode(new VisualDecodeContext(input, profile, invalid, CancellationToken.None)));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, exception.ErrorCode);

            var outputs = InferenceOutputs.Create("output", new Tensor<double>(new TensorShape(1, 1, 2, 2), new[] { -2d, 0d, 2d, 4d }));
            var result = (BackgroundRemovalResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            float[] alpha = result.Alpha.ToArray();
            Assert.AreEqual((float)(1d / (1d + Math.Exp(2d))), alpha[0], 0.000001f);
            Assert.AreEqual(.5f, alpha[1], 0.000001f);
            Assert.AreEqual((float)(1d / (1d + Math.Exp(-2d))), alpha[2], 0.000001f);
            Assert.AreEqual((float)(1d / (1d + Math.Exp(-4d))), alpha[3], 0.000001f);
        }

        private static PaddleOcrArtifactContract PaddleArtifact(int opset)
            => new PaddleOcrArtifactContract(opset, Sha, "2661c7c0", "paddle2onnx", "Apache-2.0", "paddle-det-resize-v1", "paddle-db-ctc-v1");
    }
}
