using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage20PaddleOcrThreeModelParityTests
    {
        private const string DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";
        private const string OfficialGoldenImageSha = "3ac37804e4e292f68c8960d553485147516cdc2e4154afeec6ca742a70e71dca";
        private const string OfficialGoldenDetectorTensorSha = "615fabec288f5c03f3cc7e241a0b0d2905c22cc20ae31940699456ec6f1f6db7";
        private const string OfficialGoldenServerDetectorOutputSha = "a0d29a098e5884e1a64e8f87c45a93ffcd549fa892e56e88bb16b34efd0c04aa";
        private const string OfficialGoldenRecognitionImageSha = "5362ba97741413494c507237b5096ef09ed575a501c4d9e68bfeffe17528a6ad";
        private const string OfficialGoldenRecognitionTensorSha = "11a118dd4318209a6d382f4d7cfe68f096602a6f27b37f35054b90487a757b5d";
        private const string OfficialGoldenRecognitionOutputSha = "4d77378bf1a9e4fa99b81ac1013758d3928986ab4a0b4191cab88d022f71d70f";
        private const string OfficialGoldenServerRecognitionOutputSha = "15a94deb04566181186a091e9b950d43c89adf181af848c314aadb3c3a3b7876";
        private const string OfficialGoldenRecognitionText = "绿洲仕格维花园公寓";
        private const string OfficialGoldenOrientationImageSha = "872200f57a1408e7aab2856d5f2c687b3a937805e0c4ff74bd7de21df1f742b9";
        private const string OfficialGoldenOrientationTensorSha = "7cda055c7450b2e6f52d5993a827dbd1c202ae8044d3fdbb132453b602c2d340";
        private const string OfficialGoldenOrientationOutputSha = "7b2495af2f5a8bcc459041a65440f7a3900c43e022601aa9e49e912b96ea0dd5";
        private const string OfficialGoldenServerOrientationOutputSha = "ed22b72274726fa5eb31cf9178227e46c4cedc562e68fb670067902ac4272c7f";
        private static readonly int[] OfficialGoldenRecognitionTokenIndexes = { 2498, 1680, 3542, 1845, 2492, 666, 727, 149, 2782 };
        private static readonly float[][] OfficialGoldenServerBoxes =
        {
            new[] { 32f, 407f, 488f, 383f, 490f, 433f, 34f, 457f },
            new[] { 192f, 453f, 400f, 443f, 402f, 481f, 194f, 491f },
            new[] { 13f, 505f, 518f, 483f, 520f, 532f, 15f, 553f },
            new[] { 74f, 550f, 398f, 538f, 400f, 576f, 75f, 588f }
        };
        private static readonly float[][] OfficialGoldenBoxes =
        {
            new[] { 36f, 408f, 486f, 387f, 488f, 433f, 39f, 454f },
            new[] { 189f, 452f, 402f, 443f, 403f, 481f, 190f, 490f },
            new[] { 14f, 504f, 518f, 485f, 520f, 534f, 16f, 553f },
            new[] { 75f, 551f, 414f, 539f, 416f, 576f, 76f, 588f }
        };

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DetectorClassifierRecognizerMatchAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string image = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_IMAGE") ?? @"E:\Data\image\bus.jpg");
            OcrResult ort = Timed("ort", () => Run(false, image));
            OcrResult openVino = Timed("openvino", () => Run(true, image));

            Assert.AreEqual(ort.Regions.Count, openVino.Regions.Count);
            Assert.IsTrue(ort.Regions.Count > 1);
            for (int index = 0; index < ort.Regions.Count; index++)
            {
                OcrRegionResult left = ort.Regions[index];
                OcrRegionResult right = openVino.Regions[index];
                Assert.AreEqual(left.Region.SourceIndex, right.Region.SourceIndex);
                Assert.AreEqual(left.Region.Orientation, right.Region.Orientation);
                Assert.AreEqual(left.Region.Metadata["ocr.orientation.classIndex"], right.Region.Metadata["ocr.orientation.classIndex"]);
                Assert.AreEqual(left.Region.Metadata["ocr.orientation.rejected"], right.Region.Metadata["ocr.orientation.rejected"]);
                float leftConfidence = float.Parse(left.Region.Metadata["ocr.orientation.confidence"], CultureInfo.InvariantCulture);
                float rightConfidence = float.Parse(right.Region.Metadata["ocr.orientation.confidence"], CultureInfo.InvariantCulture);
                Assert.AreEqual(leftConfidence, rightConfidence, .0001f);
                Assert.AreEqual(left.Recognition.Text, right.Recognition.Text);
                Assert.AreEqual(left.Recognition.Confidence, right.Recognition.Confidence, .001f);
                for (int vertex = 0; vertex < left.Region.Polygon.Vertices.Count; vertex++)
                {
                    PointF a = left.Region.Polygon.Vertices[vertex];
                    PointF b = right.Region.Polygon.Vertices[vertex];
                    Assert.AreEqual(a.X, b.X, .25f);
                    Assert.AreEqual(a.Y, b.Y, .25f);
                }
            }

            Console.WriteLine("STAGE20_OCR3_PARITY inputSha=" + FileSha256(image) + ";regions=" + ort.Regions.Count + ";ortSha=" + ort.ComputeSha256() + ";openvinoSha=" + openVino.ComputeSha256());
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RecognitionAndOrientationMatchOfficialGoldensAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string recognitionImage = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_REC_GOLDEN_IMAGE") ?? DefaultArtifact("artifacts\\paddleocr-reference\\images\\rec.png"));
            string orientationImage = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_CLS_GOLDEN_IMAGE") ?? DefaultArtifact("artifacts\\paddleocr-reference\\images\\cls.jpg"));
            Assert.AreEqual(OfficialGoldenRecognitionImageSha, FileSha256(recognitionImage));
            Assert.AreEqual(OfficialGoldenOrientationImageSha, FileSha256(orientationImage));

            (string InputSha, string OutputSha, TextRecognitionBatchResult Result) ortRecognition = Timed("ort-recognition-golden", () => RunRecognitionGolden(false, recognitionImage));
            (string InputSha, string OutputSha, TextRecognitionBatchResult Result) openVinoRecognition = Timed("openvino-recognition-golden", () => RunRecognitionGolden(true, recognitionImage));
            Assert.AreEqual(OfficialGoldenRecognitionTensorSha, ortRecognition.InputSha);
            Assert.AreEqual(ortRecognition.InputSha, openVinoRecognition.InputSha);
            Assert.AreEqual(ortRecognition.Result.Items.Count, openVinoRecognition.Result.Items.Count);
            Assert.AreEqual(OfficialGoldenRecognitionText, ortRecognition.Result.Items[0].Text);
            Assert.AreEqual(ortRecognition.Result.Items[0].Text, openVinoRecognition.Result.Items[0].Text);
            Assert.AreEqual(ortRecognition.Result.Items[0].Confidence, openVinoRecognition.Result.Items[0].Confidence, .0001f);
            CollectionAssert.AreEqual(OfficialGoldenRecognitionTokenIndexes, ortRecognition.Result.Items[0].Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray());
            CollectionAssert.AreEqual(ortRecognition.Result.Items[0].Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray(), openVinoRecognition.Result.Items[0].Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray());
            Assert.AreEqual(.9935501f, ortRecognition.Result.Items[0].Confidence, .00001f);

            (string InputSha, string OutputSha, OcrOrientationResult Result) ortOrientation = Timed("ort-orientation-golden", () => RunOrientationGolden(false, orientationImage));
            (string InputSha, string OutputSha, OcrOrientationResult Result) openVinoOrientation = Timed("openvino-orientation-golden", () => RunOrientationGolden(true, orientationImage));
            Assert.AreEqual(OfficialGoldenOrientationTensorSha, ortOrientation.InputSha);
            Assert.AreEqual(ortOrientation.InputSha, openVinoOrientation.InputSha);
            Assert.AreEqual(1, ortOrientation.Result.ClassIndex);
            Assert.AreEqual(TextOrientation.Degrees180, ortOrientation.Result.Orientation);
            Assert.AreEqual(ortOrientation.Result.ClassIndex, openVinoOrientation.Result.ClassIndex);
            Assert.AreEqual(ortOrientation.Result.Orientation, openVinoOrientation.Result.Orientation);
            Assert.AreEqual(ortOrientation.Result.Confidence, openVinoOrientation.Result.Confidence, .0001f);
            Assert.AreEqual(.9986027f, ortOrientation.Result.Confidence, .00001f);
            Console.WriteLine("STAGE20_OCR3_OFFICIAL_GOLDEN recognitionText=" + ortRecognition.Result.Items[0].Text + ";recognitionConfidence=" + ortRecognition.Result.Items[0].Confidence.ToString("R", CultureInfo.InvariantCulture) + ";recognitionReferenceOutputSha=" + OfficialGoldenRecognitionOutputSha + ";recognitionOrtOutputSha=" + ortRecognition.OutputSha + ";orientationClass=" + ortOrientation.Result.ClassIndex + ";orientationConfidence=" + ortOrientation.Result.Confidence.ToString("R", CultureInfo.InvariantCulture) + ";orientationReferenceOutputSha=" + OfficialGoldenOrientationOutputSha + ";orientationOrtOutputSha=" + ortOrientation.OutputSha);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ServerDetectorMatchesOfficialGoldenAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string image = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_IMAGE") ?? DefaultArtifact("artifacts\\paddleocr-reference\\images\\det.png"));
            Assert.AreEqual(OfficialGoldenImageSha, FileSha256(image));
            (string InputSha, string OutputSha, TextDetectionResult Result) ort = Timed("ort-server-detector-golden", () => RunServerDetectorGolden(false, image));
            (string InputSha, string OutputSha, TextDetectionResult Result) openVino = Timed("openvino-server-detector-golden", () => RunServerDetectorGolden(true, image));
            Assert.AreEqual(OfficialGoldenDetectorTensorSha, ort.InputSha);
            Assert.AreEqual(ort.InputSha, openVino.InputSha);
            Assert.AreEqual(OfficialGoldenServerBoxes.Length, ort.Result.Regions.Count);
            Assert.AreEqual(ort.Result.Regions.Count, openVino.Result.Regions.Count);
            for (int index = 0; index < OfficialGoldenServerBoxes.Length; index++)
            {
                Assert.IsTrue(CyclicPolygonDistance(ort.Result.Regions[index].Polygon.Vertices, OfficialGoldenServerBoxes[index]) <= 4f, "Official server DB golden polygon mismatch at index " + index + ".");
                Assert.IsTrue(CyclicPolygonDistance(openVino.Result.Regions[index].Polygon.Vertices, OfficialGoldenServerBoxes[index]) <= 4f, "OpenVINO server DB golden polygon mismatch at index " + index + ".");
                Assert.AreEqual(ort.Result.Regions[index].Polygon.Vertices.Count, openVino.Result.Regions[index].Polygon.Vertices.Count);
                Assert.AreEqual(ort.Result.Regions[index].Score, openVino.Result.Regions[index].Score, .001f);
            }
            Console.WriteLine("STAGE20_OCR3_SERVER_GOLDEN referenceOutputSha=" + OfficialGoldenServerDetectorOutputSha + ";ortOutputSha=" + ort.OutputSha + ";openvinoOutputSha=" + openVino.OutputSha + ";regions=" + ort.Result.Regions.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ServerRecognitionAndOrientationMatchOfficialGoldensAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string recognitionImage = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_REC_GOLDEN_IMAGE") ?? DefaultArtifact("artifacts\\paddleocr-reference\\images\\rec.png"));
            string orientationImage = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_CLS_GOLDEN_IMAGE") ?? DefaultArtifact("artifacts\\paddleocr-reference\\images\\cls.jpg"));
            Assert.AreEqual(OfficialGoldenRecognitionImageSha, FileSha256(recognitionImage));
            Assert.AreEqual(OfficialGoldenOrientationImageSha, FileSha256(orientationImage));

            (string InputSha, string OutputSha, TextRecognitionBatchResult Result) ortRecognition = Timed("ort-server-recognition-golden", () => RunServerRecognitionGolden(false, recognitionImage));
            (string InputSha, string OutputSha, TextRecognitionBatchResult Result) openVinoRecognition = Timed("openvino-server-recognition-golden", () => RunServerRecognitionGolden(true, recognitionImage));
            Assert.AreEqual(OfficialGoldenRecognitionTensorSha, ortRecognition.InputSha);
            Assert.AreEqual(ortRecognition.InputSha, openVinoRecognition.InputSha);
            Assert.AreEqual(OfficialGoldenRecognitionText, ortRecognition.Result.Items[0].Text);
            Assert.AreEqual(ortRecognition.Result.Items[0].Text, openVinoRecognition.Result.Items[0].Text);
            CollectionAssert.AreEqual(OfficialGoldenRecognitionTokenIndexes, ortRecognition.Result.Items[0].Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray());
            CollectionAssert.AreEqual(ortRecognition.Result.Items[0].Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray(), openVinoRecognition.Result.Items[0].Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray());
            Assert.AreEqual(.989775f, ortRecognition.Result.Items[0].Confidence, .00001f);
            Assert.AreEqual(ortRecognition.Result.Items[0].Confidence, openVinoRecognition.Result.Items[0].Confidence, .0001f);

            (string InputSha, string OutputSha, OcrOrientationResult Result) ortOrientation = Timed("ort-server-orientation-golden", () => RunServerOrientationGolden(false, orientationImage));
            (string InputSha, string OutputSha, OcrOrientationResult Result) openVinoOrientation = Timed("openvino-server-orientation-golden", () => RunServerOrientationGolden(true, orientationImage));
            Assert.AreEqual(OfficialGoldenOrientationTensorSha, ortOrientation.InputSha);
            Assert.AreEqual(ortOrientation.InputSha, openVinoOrientation.InputSha);
            Assert.AreEqual(1, ortOrientation.Result.ClassIndex);
            Assert.IsTrue(ortOrientation.Result.Rejected);
            Assert.AreEqual(TextOrientation.Degrees0, ortOrientation.Result.Orientation);
            Assert.IsNull(ortOrientation.Result.AcceptedOrientation);
            Assert.AreEqual(.8986771f, ortOrientation.Result.Confidence, .00001f);
            Assert.AreEqual(ortOrientation.Result.ClassIndex, openVinoOrientation.Result.ClassIndex);
            Assert.AreEqual(ortOrientation.Result.Rejected, openVinoOrientation.Result.Rejected);
            Assert.AreEqual(ortOrientation.Result.Orientation, openVinoOrientation.Result.Orientation);
            Assert.AreEqual(ortOrientation.Result.Confidence, openVinoOrientation.Result.Confidence, .0001f);
            Console.WriteLine("STAGE20_OCR3_SERVER_REC_CLS_GOLDEN recognitionReferenceOutputSha=" + OfficialGoldenServerRecognitionOutputSha + ";recognitionOrtOutputSha=" + ortRecognition.OutputSha + ";orientationReferenceOutputSha=" + OfficialGoldenServerOrientationOutputSha + ";orientationOrtOutputSha=" + ortOrientation.OutputSha + ";orientationRejected=" + ortOrientation.Result.Rejected);
        }

        private static OcrResult Run(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/stage20-detector"), Artifact(11, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", "ppocr-det-resize-long960-stride128-f32-v2", "ppocr-db-contour-minarea-unclip-v2"));
            PaddleOcrProfile classifier = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/stage20-classifier"), Artifact(7, "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_DICT") ?? @"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt"), "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/stage20-recognizer"), Artifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha), characters);

            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(classifier.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using var pipeline = new OcrPipeline(backends,
                profiles.Select(detector.CreateArtifact(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_DET_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx"), backendId), backends, request, VisualTaskId.TextDetection), request,
                profiles.Select(classifier.CreateArtifact(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx"), backendId), backends, request, VisualTaskId.TextOrientationClassification), request,
                classifier.CropProfile ?? throw new InvalidOperationException("The classifier crop profile is missing."),
                profiles.Select(recognizer.CreateArtifact(RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_REC_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx"), backendId), backends, request, VisualTaskId.TextRecognition), request,
                recognizer.CropProfile ?? throw new InvalidOperationException("The recognition crop profile is missing."),
                new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: 16),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, detector.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
            string detectorSha = TensorSha(input.DetectionInput.Tensor);
            Console.WriteLine("STAGE20_OCR3_PREPARED detectorSha=" + detectorSha + ";first=" + FirstTensorValues(input.DetectionInput.Tensor));
            OcrResult result = pipeline.Run(input);
            if (string.Equals(FileSha256(imagePath), OfficialGoldenImageSha, StringComparison.Ordinal)) AssertOfficialGolden(detectorSha, result);
            Assert.AreEqual(OcrOrientationStrategy.PerTextRegion, pipeline.OrientationStrategy);
            foreach (OcrRegionResult region in result.Regions) Assert.AreEqual("per-text-region", region.Region.Metadata["ocr.orientation.strategy"]);
            for (int index = 0; index < result.Regions.Count; index++)
            {
                OcrRegionResult region = result.Regions[index];
                Console.WriteLine("STAGE20_OCR3_REGION backend=" + backendId + ";index=" + index + ";score=" + region.Region.Score.ToString("F6", CultureInfo.InvariantCulture) + ";points=" + string.Join("|", region.Region.Polygon.Vertices.Select(point => point.X.ToString("F3", CultureInfo.InvariantCulture) + "," + point.Y.ToString("F3", CultureInfo.InvariantCulture))));
            }
            return result;
        }

        private static PaddleOcrArtifactContract Artifact(int opset, string sha, string preprocessing, string postprocessing, string? dictionarySha = null)
            => new PaddleOcrArtifactContract(opset, sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", preprocessing, postprocessing, dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? "" : "official-repository-file-separate-review-required");

        private static (string InputSha, string OutputSha, TextRecognitionBatchResult Result) RunRecognitionGolden(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            string modelPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_REC_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx");
            string dictionaryPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_DICT") ?? @"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt");
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile profile = PaddleOcrProfiles.CreateRecognition(new ModelId("external/stage20-recognizer-golden"), Artifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha), characters);
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceRecognitionOptions());
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
            TextRecognitionBatchResult result = (TextRecognitionBatchResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            return (TensorSha(input.Tensor), TensorSha(outputs.GetRequired("fetch_name_0")), result);
        }

        private static (string InputSha, string OutputSha, TextDetectionResult Result) RunServerDetectorGolden(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            string modelPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_SERVER_DET_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_det_onnx.onnx");
            PaddleOcrProfile profile = PaddleOcrProfiles.CreateDetection(new ModelId("external/stage20-server-detector-golden"), Artifact(11, "9a910baffbefb807ff2f7bfaa72910e3e470bd17014d798386d87bb46f442839", "ppocr-det-resize-long960-stride128-f32-v2", "ppocr-db-contour-minarea-unclip-v2"));
            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
            TextDetectionResult result = (TextDetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            return (TensorSha(input.Tensor), TensorSha(outputs.GetRequired("fetch_name_0")), result);
        }

        private static (string InputSha, string OutputSha, TextRecognitionBatchResult Result) RunServerRecognitionGolden(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            string modelPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_SERVER_REC_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_rec_onnx.onnx");
            string dictionaryPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_OCR_DICT") ?? @"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt");
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile profile = PaddleOcrProfiles.CreateRecognition(new ModelId("external/stage20-server-recognizer-golden"), Artifact(10, "5c4927aa0736ab598025a37b71daae061363642b1848a90a0cb1e02e2ce823d7", "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha), characters);
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceRecognitionOptions());
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
            TextRecognitionBatchResult result = (TextRecognitionBatchResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            return (TensorSha(input.Tensor), TensorSha(outputs.GetRequired("fetch_name_0")), result);
        }

        private static (string InputSha, string OutputSha, OcrOrientationResult Result) RunOrientationGolden(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            string modelPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx");
            PaddleOcrProfile profile = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/stage20-classifier-golden"), Artifact(7, "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceTextLineOrientationOptions());
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
            OcrOrientationResult result = (OcrOrientationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            return (TensorSha(input.Tensor), TensorSha(outputs.GetRequired("fetch_name_0")), result);
        }

        private static (string InputSha, string OutputSha, OcrOrientationResult Result) RunServerOrientationGolden(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            string modelPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_SERVER_CLS_MODEL") ?? @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_cls_onnx.onnx");
            PaddleOcrProfile profile = PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/stage20-server-classifier-golden"), Artifact(7, "d874cd926a8f9f66e886bbd8ad7747635802b6cc52d3b81b5892845fc84c616f", "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceTextLineOrientationOptions());
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
            OcrOrientationResult result = (OcrOrientationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            return (TensorSha(input.Tensor), TensorSha(outputs.GetRequired("fetch_name_0")), result);
        }

        private static string DefaultArtifact(string relativePath)
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DeploySharp.sln"))) directory = directory.Parent;
            return Path.Combine((directory ?? new DirectoryInfo(Directory.GetCurrentDirectory())).FullName, relativePath);
        }

        private static T Timed<T>(string backend, Func<T> action)
        {
            var watch = Stopwatch.StartNew(); T value = action(); watch.Stop();
            Console.WriteLine("STAGE20_OCR3_TIMING backend=" + backend + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            return value;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE20_RUN_EXTERNAL=1 to run the authorized local three-model OCR parity test.");
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured stage-20 validation file does not exist: " + path);
            return path;
        }

        private static string FileSha256(string path)
        {
            using FileStream stream = File.OpenRead(path); using SHA256 sha = SHA256.Create(); byte[] bytes = sha.ComputeHash(stream);
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string TensorSha(ITensor tensor)
        {
            if (!(tensor.Buffer is float[] values)) throw new InvalidOperationException("The detector input must be Float32.");
            byte[] bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string FirstTensorValues(ITensor tensor)
        {
            float[] values = tensor.Buffer as float[] ?? throw new InvalidOperationException("The detector input must be Float32.");
            int count = Math.Min(12, values.Length);
            var parts = new string[count];
            for (int index = 0; index < count; index++) parts[index] = values[index].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(",", parts);
        }

        private static void AssertOfficialGolden(string detectorSha, OcrResult result)
        {
            Assert.AreEqual(OfficialGoldenDetectorTensorSha, detectorSha);
            Assert.AreEqual(OfficialGoldenBoxes.Length, result.Regions.Count);
            for (int index = 0; index < OfficialGoldenBoxes.Length; index++)
                Assert.IsTrue(CyclicPolygonDistance(result.Regions[index].Region.Polygon.Vertices, OfficialGoldenBoxes[index]) <= 3f, "Official DB golden polygon mismatch at index " + index + ".");
        }

        private static float CyclicPolygonDistance(IReadOnlyList<PointF> actual, float[] expected)
        {
            float best = float.PositiveInfinity;
            for (int reverse = 0; reverse < 2; reverse++)
                for (int shift = 0; shift < 4; shift++)
                {
                    float maximum = 0f;
                    for (int index = 0; index < 4; index++)
                    {
                        int expectedIndex = reverse == 0 ? (index + shift) % 4 : (shift - index + 400) % 4;
                        float dx = actual[index].X - expected[expectedIndex * 2];
                        float dy = actual[index].Y - expected[(expectedIndex * 2) + 1];
                        maximum = Math.Max(maximum, (float)Math.Sqrt((dx * dx) + (dy * dy)));
                    }
                    best = Math.Min(best, maximum);
                }
            return best;
        }
    }
}
