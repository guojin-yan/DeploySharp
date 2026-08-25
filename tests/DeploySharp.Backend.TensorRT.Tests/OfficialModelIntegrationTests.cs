using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests
{
    [TestClass]
    public sealed class OfficialModelIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void YoloV8OnnxBuildsTensorRt11EngineAndRunsInference()
        {
            RequireExternal();
            const string expectedSha256 = "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4";
            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_MODEL") ?? @"E:\Model\yolo\yolov8\yolov8n.onnx";
            if (!File.Exists(onnxPath))
            {
                Assert.Inconclusive("Missing local model: " + onnxPath);
            }

            var modelId = new ModelId("yolo/v8/detect/n");
            var onnxArtifact = new ModelArtifact(modelId, "onnx", onnxPath, expectedSha256);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt11-yolov8-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string enginePath = Path.Combine(root, "yolov8n.engine");
                var buildOptions = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: TensorRtApiVersion.TensorRt11,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    overwrite: true);
                TensorRtOnnxEngineBuildResult build = new TensorRtOnnxEngineBuilder().Build(onnxArtifact, enginePath, buildOptions);

                Assert.IsTrue(File.Exists(build.EnginePath), "TensorRT engine was not written.");
                Assert.IsTrue(build.EngineBytes >= 8, "TensorRT engine was unexpectedly small.");
                using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId),
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                    SessionOptions.Default);

                Assert.AreEqual(1, session.Metadata.Inputs.Count, "Expected one YOLO input binding.");
                TensorDescriptor inputDescriptor = session.Metadata.Inputs[0];
                Assert.AreEqual(TensorElementType.Float32, inputDescriptor.ElementType, "YOLO input type changed.");
                long elementCount = inputDescriptor.Shape.GetElementCount();
                Assert.IsTrue(elementCount > 0 && elementCount <= int.MaxValue, "YOLO input shape must be static and bounded.");
                var input = new Tensor<float>(inputDescriptor.Shape, new float[(int)elementCount]);
                InferenceOutputs outputs = session.Run(
                    InferenceInputs.Create(inputDescriptor.Name, input),
                    CancellationToken.None);

                Assert.IsTrue(outputs.Count > 0, "TensorRT returned no YOLO output bindings.");
                Assert.IsTrue(outputs[0].Tensor.Length > 0, "TensorRT returned an empty YOLO output.");
                Console.WriteLine(
                    "TENSORRT_EXTERNAL_MODEL model=yolo/v8/detect/n;onnxSha256=" + expectedSha256 +
                    ";engineSha256=" + build.EngineSha256 +
                    ";engineBytes=" + build.EngineBytes +
                    ";input=" + inputDescriptor.Name + inputDescriptor.Shape +
                    ";outputs=" + outputs.Count +
                    ";firstOutputElements=" + outputs[0].Tensor.Length);
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void YoloV7OnnxBuildsTensorRt11EngineAndRunsInference()
        {
            RequireExternal();
            var model = new TensorRtImageCase("yolo/v7/detect/base", @"E:\Model\yolo\yolov7.onnx", "8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641", "images", 640, 640, false);
            if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
            BuildAndRun(model);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DynamicPaddleOcrOnnxModelsBuildTensorRt11EnginesAndRunInference()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtImageCase("paddleocr/ppocrv5/mobile-cls", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx", "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "x", 160, 80),
                new TensorRtImageCase("paddleocr/ppocrv5/mobile-det", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx", "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", "x", 32, 32),
                new TensorRtImageCase("paddleocr/ppocrv5/server-cls", @"E:\Model\ocr\ppocrv5-1\PP-OCRv5_server_cls_onnx.onnx", "d874cd926a8f9f66e886bbd8ad7747635802b6cc52d3b81b5892845fc84c616f", "x", 160, 80)
            };

            var failed = new List<string>();
            foreach (TensorRtImageCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                }
                catch (Exception exception)
                {
                    failed.Add(model.ModelId);
                    Console.WriteLine("TENSORRT_EXTERNAL_MODEL_FAILED model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void PadimAndRmbgOnnxModelsBuildTensorRt11EnginesAndRunInference()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtImageCase("anomalib/padim/mvtec-bottle", @"E:\Model\anomalib\Padim\model\padim.onnx", "bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff", "input", 256, 256),
                new TensorRtImageCase("bria/rmbg-1.4", @"E:\Model\RMBG\bria-rmbg-1.4.onnx", "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f", "input", 1024, 1024)
            };

            var failed = new List<string>();
            foreach (TensorRtImageCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                }
                catch (Exception exception)
                {
                    failed.Add(model.ModelId);
                    Console.WriteLine("TENSORRT_EXTERNAL_MODEL_FAILED model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AdditionalYoloOnnxModelsBuildTensorRt11EnginesAndRunInference()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtImageCase("yolo/v5/detect/n", @"E:\Model\yolo\yolov5\yolov5n.onnx", "1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v6/detect/s", @"E:\Model\yolo\yolov6s.onnx", "f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v7/detect/base", @"E:\Model\yolo\yolov7.onnx", "8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v9/detect/s", @"E:\Model\yolo\yolov9s.onnx", "e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v10/detect/n", @"E:\Model\yolo\yolov10\yolov10n.onnx", "908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v11/detect/n", @"E:\Model\yolo\yolov11\yolo11n.onnx", "7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v12/detect/n", @"E:\Model\yolo\yolov12\yolo12n.onnx", "9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v13/detect/n", @"E:\Model\yolo\yolov13n.onnx", "a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v26/detect/n", @"E:\Model\yolo\yolov26\yolo26n.onnx", "bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4", "images", 640, 640, false)
            };

            var failed = new List<string>();
            foreach (TensorRtImageCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                    if (!model.ExpectedToRun) failed.Add(model.ModelId + " (unexpectedly passed; update the expected result)");
                }
                catch (Exception exception)
                {
                    if (model.ExpectedToRun) failed.Add(model.ModelId);
                    Console.WriteLine((model.ExpectedToRun ? "TENSORRT_EXTERNAL_MODEL_FAILED" : "TENSORRT_EXTERNAL_MODEL_UNSUPPORTED") + " model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception + ";details=" + (exception as DeploySharpException)?.TechnicalDetails);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void YoloMultiTaskOnnxModelsBuildTensorRt11EnginesAndRunInference()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtImageCase("yolo/v8/classify/s", @"E:\Model\yolo\yolov8\yolov8s-cls.onnx", "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee", "images", 224, 224, false),
                new TensorRtImageCase("yolo/v5/segment/s", @"E:\Model\yolo\yolov5\yolov5s-seg.onnx", "ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v8/segment/n", @"E:\Model\yolo\yolov8\yolov8n-seg.onnx", "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v9/segment/c", @"E:\Model\yolo\yolov9-c-seg.onnx", "2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v11/segment/s", @"E:\Model\yolo\yolov11\yolo11s-seg.onnx", "0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v26/segment/s", @"E:\Model\yolo\yolov26\yolo26s-seg.onnx", "79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v8/pose/s", @"E:\Model\yolo\yolov8\yolov8s-pose.onnx", "253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v11/pose/s", @"E:\Model\yolo\yolov11\yolo11s-pose.onnx", "5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v26/pose/s", @"E:\Model\yolo\yolov26\yolo26s-pose.onnx", "55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc", "images", 640, 640, false),
                new TensorRtImageCase("yolo/v8/obb/s", @"E:\Model\yolo\yolov8\yolov8s-obb.onnx", "2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981", "images", 1024, 1024, false),
                new TensorRtImageCase("yolo/v11/obb/s", @"E:\Model\yolo\yolov11\yolo11s-obb.onnx", "50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0", "images", 1024, 1024, false),
                new TensorRtImageCase("yolo/v26/obb/s", @"E:\Model\yolo\yolov26\yolo26s-obb.onnx", "bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df", "images", 1024, 1024, false)
            };

            var failed = new List<string>();
            foreach (TensorRtImageCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                }
                catch (Exception exception)
                {
                    failed.Add(model.ModelId);
                    Console.WriteLine("TENSORRT_EXTERNAL_MODEL_FAILED model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AdditionalVisionOnnxModelsReportTensorRt11Compatibility()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtImageCase("rf-detr/detect", @"E:\Model\rf-detr\rf-detr.onnx", "b464822e768f5795f249a6bd08cf1c5299787806c740204ed8e46d3a369ab769", "input", 512, 512, false),
                new TensorRtImageCase("rf-detr/segment", @"E:\Model\rf-detr\rf-detr-seg.onnx", "6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35", "input", 432, 432, false),
                new TensorRtImageCase("rt-detr/r50vd-raw-query", @"E:\Model\RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx", "544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad", "image", 640, 640),
                new TensorRtImageCase("paddleocr/ppocrv5/mobile-rec", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx", "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "x", 320, 48),
                new TensorRtImageCase("paddleocr/ppocrv5/server-det", @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_det_onnx.onnx", "9a910baffbefb807ff2f7bfaa72910e3e470bd17014d798386d87bb46f442839", "x", 32, 32),
                new TensorRtImageCase("paddleocr/ppocrv5/server-rec", @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_rec_onnx.onnx", "5c4927aa0736ab598025a37b71daae061363642b1848a90a0cb1e02e2ce823d7", "x", 320, 48)
            };

            var failed = new List<string>();
            foreach (TensorRtImageCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                }
                catch (Exception exception)
                {
                    failed.Add(model.ModelId);
                    Console.WriteLine("TENSORRT_EXTERNAL_MODEL_FAILED model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void MultiInputDetectorOnnxModelsBuildTensorRt11EnginesAndRunInference()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtMultiInputCase(
                    "deim/v2/detect",
                    @"E:\Model\DEIMv2\DEIMv2.onnx",
                    "08a6a9052c83ccd356e91f8839dfe7b2e686639b577feb7f0b7b204f7f2969cc",
                    new[]
                    {
                        new TensorRtInputCase("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)),
                        new TensorRtInputCase("orig_target_sizes", TensorElementType.Int64, new TensorShape(1, 2))
                    }),
                new TensorRtMultiInputCase(
                    "pp-yoloe/plus-crn-l",
                    @"E:\Model\ppyoloe\ppyoloe_plus_crn_l_80e_coco.onnx",
                    "68866d9841e41f6637d4a1c13db6c70a42c9f0367c79870b0a8a9e9df32b8504",
                    new[]
                    {
                        new TensorRtInputCase("image", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)),
                        new TensorRtInputCase("scale_factor", TensorElementType.Float32, new TensorShape(1, 2))
                    },
                    true),
                new TensorRtMultiInputCase(
                    "rt-detr/r50vd-decoded-vector-onnx",
                    @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.onnx",
                    "a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db",
                    new[]
                    {
                        new TensorRtInputCase("im_shape", TensorElementType.Float32, new TensorShape(1, 2)),
                        new TensorRtInputCase("image", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)),
                        new TensorRtInputCase("scale_factor", TensorElementType.Float32, new TensorShape(1, 2))
                    })
            };

            var failed = new List<string>();
            foreach (TensorRtMultiInputCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                    if (!model.ExpectedToRun) failed.Add(model.ModelId + " (unexpectedly passed; update the expected result)");
                }
                catch (Exception exception)
                {
                    if (model.ExpectedToRun) failed.Add(model.ModelId);
                    Console.WriteLine((model.ExpectedToRun ? "TENSORRT_EXTERNAL_MODEL_FAILED" : "TENSORRT_EXTERNAL_MODEL_UNSUPPORTED") + " model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception + ";details=" + (exception as DeploySharpException)?.TechnicalDetails);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void Rmbg20OnnxModelsReportTensorRt11Compatibility()
        {
            RequireExternal();
            var cases = new[]
            {
                new TensorRtImageCase("bria/rmbg-2.0/fp32", @"E:\Model\RMBG\RMBG-2.0.onnx", "5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958", "pixel_values", 1024, 1024),
                new TensorRtImageCase("bria/rmbg-2.0/dynamic-int8", @"E:\Model\RMBG\RMBG-2.0_quantized.onnx", "fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420", "pixel_values", 1024, 1024, true, false)
            };

            var failed = new List<string>();
            foreach (TensorRtImageCase model in cases)
            {
                if (!File.Exists(model.Path)) Assert.Inconclusive("Missing local model: " + model.Path);
                try
                {
                    BuildAndRun(model);
                    if (!model.ExpectedToRun) failed.Add(model.ModelId + " (unexpectedly passed; update the expected result)");
                }
                catch (Exception exception)
                {
                    if (model.ExpectedToRun) failed.Add(model.ModelId);
                    Console.WriteLine((model.ExpectedToRun ? "TENSORRT_EXTERNAL_MODEL_FAILED" : "TENSORRT_EXTERNAL_MODEL_UNSUPPORTED") + " model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + exception + ";details=" + (exception as DeploySharpException)?.TechnicalDetails);
                }
            }

            if (failed.Count > 0) Assert.Fail("TensorRT ONNX conversions failed: " + string.Join(", ", failed));
        }

        private static void BuildAndRun(TensorRtImageCase model)
        {
            var modelId = new ModelId(model.ModelId);
            var inputShape = new TensorShape(1, 3, model.Height, model.Width);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt11-catalog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string enginePath = Path.Combine(root, "model.engine");
                var profiles = model.UseProfile
                    ? new[] { new TensorRtOnnxInputProfile(model.InputName, inputShape, inputShape, inputShape) }
                    : Array.Empty<TensorRtOnnxInputProfile>();
                var options = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: TensorRtApiVersion.TensorRt11,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 0,
                    overwrite: true,
                    inputProfiles: profiles);
                var artifact = new ModelArtifact(modelId, "onnx", model.Path, model.Sha256);
                TensorRtOnnxEngineBuildResult build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, options);

                using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId),
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                    SessionOptions.Default);
                var input = new Tensor<float>(inputShape, new float[checked(3 * model.Width * model.Height)]);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(model.InputName, input), CancellationToken.None);

                Assert.IsTrue(outputs.Count > 0, model.ModelId + " returned no outputs.");
                Assert.IsTrue(outputs[0].Tensor.Length > 0, model.ModelId + " returned an empty output.");
                Console.WriteLine(
                    "TENSORRT_EXTERNAL_MODEL model=" + model.ModelId +
                    ";onnxSha256=" + model.Sha256 +
                    ";engineSha256=" + build.EngineSha256 +
                    ";engineBytes=" + build.EngineBytes +
                    ";input=" + model.InputName + inputShape +
                    ";outputs=" + outputs.Count +
                    ";firstOutputElements=" + outputs[0].Tensor.Length);
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void BuildAndRun(TensorRtMultiInputCase model)
        {
            var modelId = new ModelId(model.ModelId);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt11-multi-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var profiles = new List<TensorRtOnnxInputProfile>();
                foreach (TensorRtInputCase input in model.Inputs)
                {
                    profiles.Add(new TensorRtOnnxInputProfile(input.Name, input.Shape, input.Shape, input.Shape));
                }

                var options = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: TensorRtApiVersion.TensorRt11,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 0,
                    overwrite: true,
                    inputProfiles: profiles);
                string enginePath = Path.Combine(root, "model.engine");
                var artifact = new ModelArtifact(modelId, "onnx", model.Path, model.Sha256);
                TensorRtOnnxEngineBuildResult build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, options);

                using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId),
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                    SessionOptions.Default);
                var tensors = new List<NamedTensor>();
                foreach (TensorRtInputCase input in model.Inputs)
                {
                    int count = checked((int)input.Shape.GetElementCount());
                    ITensor tensor = input.ElementType switch
                    {
                        TensorElementType.Float32 => new Tensor<float>(input.Shape, CreateFloatInput(input.Name, count)),
                        TensorElementType.Int64 => new Tensor<long>(input.Shape, CreateInt64Input(input.Name, count)),
                        _ => throw new NotSupportedException("Unsupported external test input type: " + input.ElementType)
                    };
                    tensors.Add(new NamedTensor(input.Name, tensor));
                }

                InferenceOutputs outputs = session.Run(new InferenceInputs(tensors), CancellationToken.None);
                Assert.IsTrue(outputs.Count > 0, model.ModelId + " returned no outputs.");
                Assert.IsTrue(outputs[0].Tensor.Length > 0, model.ModelId + " returned an empty output.");
                Console.WriteLine(
                    "TENSORRT_EXTERNAL_MODEL model=" + model.ModelId +
                    ";onnxSha256=" + model.Sha256 +
                    ";engineSha256=" + build.EngineSha256 +
                    ";engineBytes=" + build.EngineBytes +
                    ";inputs=" + model.Inputs.Count +
                    ";outputs=" + outputs.Count +
                    ";firstOutputElements=" + outputs[0].Tensor.Length);
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static float[] CreateFloatInput(string name, int count)
        {
            var values = new float[count];
            if (name == "im_shape" && count >= 2) { values[0] = 640f; values[1] = 640f; }
            if (name == "scale_factor") for (int index = 0; index < values.Length; index++) values[index] = 1f;
            if (name == "image") for (int index = 0; index < values.Length; index++) values[index] = 0.5f;
            return values;
        }

        private static long[] CreateInt64Input(string name, int count)
        {
            var values = new long[count];
            if (name == "orig_target_sizes" && count >= 2) { values[0] = 640; values[1] = 640; }
            return values;
        }

        private sealed record TensorRtImageCase(string ModelId, string Path, string Sha256, string InputName, int Width, int Height, bool UseProfile = true, bool ExpectedToRun = true);
        private sealed record TensorRtInputCase(string Name, TensorElementType ElementType, TensorShape Shape);
        private sealed record TensorRtMultiInputCase(string ModelId, string Path, string Sha256, IReadOnlyList<TensorRtInputCase> Inputs, bool ExpectedToRun = true);

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1 and configure the matching TensorRT bridge/runtime before running this test.");
            }
        }
    }
}
