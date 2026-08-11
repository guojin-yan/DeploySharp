using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PortableDetectorTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestMethod]
        public void DeimProfileRequiresTargetSizesAndDoesNotRepeatGraphNms()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateDEIMv2(new ModelId("tests/deim"), Options(16, new VisualSize(10, 10), new[] { "zero", "one" }));
            Assert.AreEqual(1, profile.VisualProfile.AuxiliaryInputs.Count);
            Assert.AreEqual("orig_target_sizes", profile.VisualProfile.AuxiliaryInputs[0].Name);
            using var input = Input("images", 10, new NamedTensor("orig_target_sizes", new Tensor<long>(new TensorShape(1, 2), new long[] { 10, 10 })));
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("labels", new Tensor<long>(new TensorShape(1, 2), new long[] { 1, 0 })),
                new NamedTensor("boxes", new Tensor<float>(new TensorShape(1, 2, 4), new[] { 1f, 1f, 9f, 9f, 2f, 2f, 8f, 8f })),
                new NamedTensor("scores", new Tensor<float>(new TensorShape(1, 2), new[] { .9f, .4f }))
            });
            var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual("one", result.Detections[0].Label.Label);
        }

        [TestMethod]
        public void RfDetrUsesGlobalSigmoidTopKAndIgnoresNoObjectColumn()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRFDETR(new ModelId("tests/rfdetr"), Options(17, new VisualSize(10, 10), new[] { "zero", "one" }, scoreThreshold: .2f, topK: 2, rfDetrIncludesNoObjectClass: true, rfDetrQueryCount: 2));
            using var input = Input("input", 10);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 2, 4), new[] { .5f, .5f, .4f, .4f, .25f, .25f, .2f, .2f })),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 2, 3), new[] { 0f, 2f, 20f, 3f, -5f, 20f }))
            });
            var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(2, result.Detections.Count);
            Assert.AreEqual(0, result.Detections[0].Label.Index);
            Assert.AreEqual(1, result.Detections[1].Label.Index);
            Assert.IsTrue(result.Detections[0].Label.Score > result.Detections[1].Label.Score);
        }

        [TestMethod]
        public void PaddleDecodedRowsUseSourceCoordinatesAndDeclaredCount()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreatePPYOLOE(new ModelId("tests/ppyoloe"), Options(11, new VisualSize(10, 10), new[] { "zero", "one" }, scoreThreshold: .4f));
            using var input = Input("input", 20, new NamedTensor("scale_factor", new Tensor<float>(new TensorShape(1, 2), new[] { .5f, .5f })));
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("save_infer_model/scale_0.tmp_0", new Tensor<float>(new TensorShape(2, 6), new[] { 1f, .8f, 2f, 3f, 12f, 13f, 0f, .99f, 0f, 0f, 20f, 20f })),
                new NamedTensor("save_infer_model/scale_1.tmp_0", new Tensor<int>(new TensorShape(1), new[] { 1 }))
            });
            var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(2f, result.Detections[0].Box.X, .0001f);
            Assert.AreEqual(10f, result.Detections[0].Box.Width, .0001f);
        }

        [TestMethod]
        public void RfDetrSegmentationRestoresOwnedSourceMask()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRFDETRSeg(new ModelId("tests/rfdetr-seg"), Options(17, new VisualSize(4, 4), new[] { "object" }, scoreThreshold: .2f, topK: 1, masksOutputName: "4245", rfDetrIncludesNoObjectClass: true, rfDetrQueryCount: 1));
            using var input = Input("input", 4);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 1, 4), new[] { .5f, .5f, 1f, 1f })),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 1, 2), new[] { 2f, 20f })),
                new NamedTensor("4245", new Tensor<float>(new TensorShape(1, 1, 2, 2), new[] { 4f, 4f, 4f, 4f }))
            });
            var result = (InstanceSegmentationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(16, result.Instances[0].Mask.ForegroundPixelCount);
            Assert.AreEqual(16, result.Instances[0].Mask.ToArray().Length);
        }

        [TestMethod]
        public void RfDetrOfficialProfileUsesEveryClassColumnAndStableLowercaseId()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRFDETR(new ModelId("tests/rfdetr-official"), Options(17, new VisualSize(2, 2), new[] { "zero", "one" }, topK: 1));
            Assert.IsFalse(profile.Output.IncludesNoObjectClass);
            Assert.AreEqual(new TensorShape(1, -1, 2), profile.VisualProfile.Outputs[1].ShapePattern);
            StringAssert.StartsWith(profile.VisualProfile.ProfileId, "portable.rfdetrdet.");
        }

        [TestMethod]
        public void RfDetrRejectsUndeclaredExtraClassColumnWithStableDiagnostic()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRFDETR(new ModelId("tests/rfdetr-contract"), Options(17, new VisualSize(2, 2), new[] { "zero", "one" }, topK: 1));
            using var input = Input("input", 2);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 1, 4), new[] { .5f, .5f, .5f, .5f })),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 1, 3), new[] { 1f, 0f, 0f }))
            });
            VisualException exception = Assert.ThrowsExactly<VisualException>(() => profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, exception.ErrorCode);
            StringAssert.Contains(exception.Message, "artifact-bound");
        }

        [TestMethod]
        public void RfDetrNaNLogitMapsToStableDecodeDiagnostic()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRFDETR(new ModelId("tests/rfdetr-nan"), Options(17, new VisualSize(2, 2), new[] { "zero" }, topK: 1));
            using var input = Input("input", 2);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 1, 4), new[] { .5f, .5f, .5f, .5f })),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 1, 1), new[] { float.NaN }))
            });
            VisualException exception = Assert.ThrowsExactly<VisualException>(() => profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, exception.ErrorCode);
            Assert.AreEqual("labels", exception.TensorName);
        }

        [TestMethod]
        public void RfDetrSegmentationUsesStrictZeroMaskThreshold()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRFDETRSeg(new ModelId("tests/rfdetr-seg-strict"), Options(17, new VisualSize(2, 2), new[] { "object" }, scoreThreshold: .2f, topK: 1, masksOutputName: "masks", rfDetrQueryCount: 1));
            using var input = Input("input", 2);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 1, 4), new[] { .5f, .5f, 1f, 1f })),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 1, 1), new[] { 20f })),
                new NamedTensor("masks", new Tensor<float>(new TensorShape(1, 1, 1, 1), new[] { 0f }))
            });
            var result = (InstanceSegmentationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(0, result.Instances[0].Mask.ForegroundPixelCount);
        }

        [TestMethod]
        public void RtDetrDecodedVectorCountBindsDynamicMetadataAndTypedPaddleGeometry()
        {
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(10, 10),
                new[] { "zero", "one" },
                inputName: "image",
                artifactSha256: Sha,
                upstreamRepository: "https://github.com/PaddlePaddle/PaddleDetection",
                upstreamCommit: "commit",
                exporterVersion: "paddle2onnx-1.0.5",
                license: "Apache-2.0",
                scoreThreshold: .4f,
                boxesOutputName: "bbox",
                countOutputName: "bbox_num",
                hasDynamicBatchAxis: true,
                paddleCountShape: PortableDetectorCountShape.BatchVector);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETR(new ModelId("tests/rtdetr-decoded"), options);
            Assert.AreEqual(new TensorShape(-1, 3, 10, 10), profile.VisualProfile.Input.ShapePattern);
            Assert.AreEqual(new TensorShape(-1, 2), profile.VisualProfile.AuxiliaryInputs[0].ShapePattern);
            Assert.AreEqual(new TensorShape(-1), profile.VisualProfile.Outputs[1].ShapePattern);
            Assert.AreEqual(PortableDetectorNmsOwnership.ExportedGraph, profile.Output.NmsOwnership);

            var source = new VisualSize(20, 20);
            var model = new VisualSize(10, 10);
            using var prepared = new PreparedVisualInput("image", new Tensor<float>(new TensorShape(1, 3, 10, 10), new float[300]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, model));
            IReadOnlyList<NamedTensor> auxiliary = profile.CreateAuxiliaryInputs(prepared);
            CollectionAssert.AreEqual(new[] { 10f, 10f }, ((Tensor<float>)auxiliary[0].Tensor).ToArray());
            CollectionAssert.AreEqual(new[] { .5f, .5f }, ((Tensor<float>)auxiliary[1].Tensor).ToArray());
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("bbox", new Tensor<float>(new TensorShape(1, 6), new[] { 1f, .9f, 2f, 3f, 12f, 13f })),
                new NamedTensor("bbox_num", new Tensor<int>(new TensorShape(1), new[] { 1 }))
            });
            var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(prepared, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(2f, result.Detections[0].Box.X, .0001f);
            Assert.AreEqual(10f, result.Detections[0].Box.Width, .0001f);
        }

        [TestMethod]
        public void RtDetrRawUsesOfficialSigmoidGlobalTopKAndNormalizedCxcywh()
        {
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(10, 10),
                new[] { "zero", "one" },
                inputName: "image",
                artifactSha256: Sha,
                scoreThreshold: .5f,
                maximumCandidates: 2,
                maximumResults: 1,
                topK: 1,
                boxesOutputName: "raw_boxes",
                labelsOutputName: "raw_logits",
                rfDetrQueryCount: 2,
                hasDynamicBatchAxis: true);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETRRaw(new ModelId("tests/rtdetr-raw"), options);
            using var input = Input("image", 10);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("raw_boxes", new Tensor<float>(new TensorShape(1, 2, 4), new[] { .5f, .5f, .4f, .2f, .2f, .2f, .1f, .1f })),
                new NamedTensor("raw_logits", new Tensor<float>(new TensorShape(1, 2, 2), new[] { -5f, 4f, 3f, -5f }))
            });
            var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual("one", result.Detections[0].Label.Label);
            Assert.AreEqual(3f, result.Detections[0].Box.X, .0001f);
            Assert.AreEqual(4f, result.Detections[0].Box.Width, .0001f);
            Assert.AreEqual(PortableDetectorNmsOwnership.None, profile.Output.NmsOwnership);
        }

        [TestMethod]
        public void RtDetrV2UsesSourceWidthHeightAndDoesNotRestoreSourceBoxesTwice()
        {
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(4, 4),
                new[] { "zero", "one" },
                inputName: "images",
                artifactSha256: Sha,
                upstreamRepository: "https://github.com/lyuwenyu/RT-DETR",
                upstreamCommit: "commit",
                exporterVersion: "torch-2.7.1",
                license: "Apache-2.0",
                scoreThreshold: .4f,
                maximumCandidates: 2,
                maximumResults: 2,
                topK: 2,
                rfDetrQueryCount: 2,
                hasDynamicBatchAxis: true);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETRv2(new ModelId("tests/rtdetrv2"), options);
            var source = new VisualSize(20, 10);
            var model = new VisualSize(4, 4);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 4, 4), new float[48]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, model));
            NamedTensor targetSizes = profile.CreateAuxiliaryInputs(input).Single();
            Assert.AreEqual(PortableDetectorSizeOrder.WidthHeight, profile.AuxiliaryInputs[0].SizeOrder);
            CollectionAssert.AreEqual(new long[] { 20, 10 }, ((Tensor<long>)targetSizes.Tensor).ToArray());
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("labels", new Tensor<long>(new TensorShape(1, 2), new long[] { 1, 0 })),
                new NamedTensor("boxes", new Tensor<float>(new TensorShape(1, 2, 4), new[] { 2f, 1f, 18f, 9f, 0f, 0f, 20f, 10f })),
                new NamedTensor("scores", new Tensor<float>(new TensorShape(1, 2), new[] { .9f, .4f }))
            });
            var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(2f, result.Detections[0].Box.X, .0001f);
            Assert.AreEqual(16f, result.Detections[0].Box.Width, .0001f);
            Assert.AreEqual(PortableDetectorCoordinateSpace.SourcePixels, profile.Output.CoordinateSpace);
        }

        [TestMethod]
        public void VisualPipelinePassesDeclaredAuxiliaryInputsByExactName()
        {
            var profile = new VisualModelProfile(
                "tests/auxiliary.v1", new ModelId("tests/auxiliary"), VisualTaskId.ImageClassification, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 2)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Probabilities),
                auxiliaryInputs: new[] { new VisualAuxiliaryInputBinding("scale_factor", TensorElementType.Float32, new TensorShape(1, 2)) });
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 2), inputs =>
            {
                CollectionAssert.AreEqual(new[] { .5f, .5f }, ((Tensor<float>)inputs.GetRequired("scale_factor")).ToArray());
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 2), new[] { .1f, .9f }));
            });
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), auxiliaryInputs: new[] { new NamedTensor("scale_factor", new Tensor<float>(new TensorShape(1, 2), new[] { .5f, .5f })) });
            var result = fixture.Pipeline.Run(input).GetValue<ClassificationResult>();
            Assert.AreEqual("one", result.TopPrediction!.Label);
        }

        private static PortableDetectorProfileOptions Options(int opset, VisualSize modelSize, string[] labels, float scoreThreshold = .4f, int topK = 300, string? masksOutputName = null, bool rfDetrIncludesNoObjectClass = false, int rfDetrQueryCount = -1, long maximumMaskPixels = 64L * 1024 * 1024)
        {
            return new PortableDetectorProfileOptions(opset, modelSize, labels, inputName: "input", artifactSha256: Sha, upstreamRepository: "https://example.invalid/source", upstreamCommit: "commit", exporterVersion: "exporter", license: "Apache-2.0", scoreThreshold: scoreThreshold, topK: topK, maximumResults: Math.Min(300, topK), maximumMaskPixels: maximumMaskPixels, masksOutputName: masksOutputName, rfDetrIncludesNoObjectClass: rfDetrIncludesNoObjectClass, rfDetrQueryCount: rfDetrQueryCount);
        }

        private static PreparedVisualInput Input(string name, int size, params NamedTensor[] auxiliary)
        {
            var visualSize = new VisualSize(size, size);
            return new PreparedVisualInput(name, new Tensor<float>(new TensorShape(1, 3, size, size), new float[size * size * 3]), visualSize, visualSize, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(visualSize, visualSize), auxiliaryInputs: auxiliary);
        }
    }
}
