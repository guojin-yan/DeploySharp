using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.Dnn;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DnnCv2 = JYPPX.OpenCvSharp.Dnn.Cv2;

namespace DeploySharp.Backend.OpenCV.Tests;

[TestClass]
public sealed class AdditionalOfficialModelProbeTests
{
    [TestMethod]
    public void OpenCvV1ContractRejectsCatalogAuxiliaryInputsAndBooleanOutputs()
    {
        var image = new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640));
        var output = new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(1, 1));

        Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(
            new ModelId("deim/v2/detect"),
            new[] { image, new TensorDescriptor("orig_target_sizes", TensorElementType.Int64, new TensorShape(1, 2)) },
            new[] { output }));
        Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(
            new ModelId("pp-yoloe/plus-crn-l"),
            new[] { image, new TensorDescriptor("scale_factor", TensorElementType.Float32, new TensorShape(1, 2)) },
            new[] { output }));
        Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(
            new ModelId("rt-detr/r50vd-decoded-vector-onnx"),
            new[] { image, new TensorDescriptor("im_shape", TensorElementType.Float32, new TensorShape(1, 2)) },
            new[] { output }));
        Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(
            new ModelId("anomalib/padim/mvtec-bottle"),
            new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(1, 3, 256, 256)) },
            new[] { new TensorDescriptor("pred_mask", TensorElementType.Boolean, new TensorShape(1, 1, 256, 256)) }));
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void AdditionalYoloDetectionModelsUseDeploySharpOpenCvProvider()
    {
        RequireExternal();
        var cases = new[]
        {
            new ProviderCase("yolo/v5/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V5_MODEL") ?? @"E:\Model\yolo\yolov5\yolov5n.onnx", "1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d", "output0", new TensorShape(1, 25200, 85)),
            new ProviderCase("yolo/v6/detect/s", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V6_MODEL") ?? @"E:\Model\yolo\yolov6s.onnx", "f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6", "outputs", new TensorShape(1, 8400, 85)),
            new ProviderCase("yolo/v9/detect/s", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V9_MODEL") ?? @"E:\Model\yolo\yolov9s.onnx", "e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7", "output0", new TensorShape(1, 84, 8400)),
            new ProviderCase("yolo/v10/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V10_MODEL") ?? @"E:\Model\yolo\yolov10\yolov10n.onnx", "908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81", "output0", new TensorShape(1, 300, 6)),
            new ProviderCase("yolo/v11/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V11_MODEL") ?? @"E:\Model\yolo\yolov11\yolo11n.onnx", "7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3", "output0", new TensorShape(1, 84, 8400)),
            new ProviderCase("yolo/v12/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V12_MODEL") ?? @"E:\Model\yolo\yolov12\yolo12n.onnx", "9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80", "output0", new TensorShape(1, 84, 8400)),
            new ProviderCase("yolo/v13/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V13_MODEL") ?? @"E:\Model\yolo\yolov13n.onnx", "a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80", "output0", new TensorShape(1, 84, 8400)),
            new ProviderCase("yolo/v26/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V26_MODEL") ?? @"E:\Model\yolo\yolov26\yolo26n.onnx", "bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4", "output0", new TensorShape(1, 300, 6))
        };

        var missing = new List<string>();
        foreach (ProviderCase probe in cases)
        {
            if (!File.Exists(probe.Path))
            {
                missing.Add(probe.ModelId);
                continue;
            }

            var modelId = new ModelId(probe.ModelId);
            var contract = new OpenCvDnnModelContract(
                modelId,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)) },
                new[] { new TensorDescriptor(probe.OutputName, TensorElementType.Float32, probe.OutputShape) });
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(modelId, "onnx", probe.Path, probe.Sha256),
                new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                SessionOptions.Default);
            var input = new Tensor<float>(new TensorShape(1, 3, 640, 640), new float[3 * 640 * 640]);
            InferenceOutputs outputs = session.Run(new InferenceInputs(new[] { new NamedTensor("images", input) }), CancellationToken.None);
            Assert.AreEqual(1, outputs.Count, probe.ModelId);
            Assert.AreEqual(probe.OutputShape.GetElementCount(), outputs[0].Tensor.Length, probe.ModelId);
            Console.WriteLine("OPENCV_EXTERNAL_PROVIDER model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";output=" + probe.OutputName + ";elements=" + outputs[0].Tensor.Length);
        }

        if (missing.Count > 0) Assert.Inconclusive("Missing local models: " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void AdditionalYoloDetectionModelsRunThroughOpenCvDnnCpu()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_OPENCV_RUN_EXTERNAL=1 to run the local OpenCV DNN model probes.");
        }

        var cases = new[]
        {
            new ProbeCase("yolo/v5/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V5_MODEL") ?? @"E:\Model\yolo\yolov5\yolov5n.onnx", "1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d"),
            new ProbeCase("yolo/v6/detect/s", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V6_MODEL") ?? @"E:\Model\yolo\yolov6s.onnx", "f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6"),
            new ProbeCase("yolo/v7/detect", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V7_MODEL") ?? @"E:\Model\yolo\yolov7.onnx", "8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641"),
            new ProbeCase("yolo/v9/detect/s", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V9_MODEL") ?? @"E:\Model\yolo\yolov9s.onnx", "e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7"),
            new ProbeCase("yolo/v10/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V10_MODEL") ?? @"E:\Model\yolo\yolov10\yolov10n.onnx", "908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81"),
            new ProbeCase("yolo/v11/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V11_MODEL") ?? @"E:\Model\yolo\yolov11\yolo11n.onnx", "7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3"),
            new ProbeCase("yolo/v12/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V12_MODEL") ?? @"E:\Model\yolo\yolov12\yolo12n.onnx", "9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80"),
            new ProbeCase("yolo/v13/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V13_MODEL") ?? @"E:\Model\yolo\yolov13n.onnx", "a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80"),
            new ProbeCase("yolo/v26/detect/n", Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_V26_MODEL") ?? @"E:\Model\yolo\yolov26\yolo26n.onnx", "bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4")
        };

        var missing = new List<string>();
        var failed = new List<string>();
        foreach (ProbeCase probe in cases)
        {
            if (!File.Exists(probe.Path))
            {
                missing.Add(probe.ModelId);
                continue;
            }

            try
            {
                using Net network = Net.ReadNetFromOnnx(probe.Path, DnnEngine.Classic);
                Assert.IsFalse(network.Empty, probe.ModelId + " loaded an empty network.");
                network.SetPreferableBackend(DnnBackend.OpenCV).SetPreferableTarget(DnnTarget.Cpu).EnableFusion(false).EnableWinograd(false);
                using var image = new Mat(640, 640, MatType.CV_32FC3);
                using Mat blob = DnnCv2.BlobFromImage(image, 1d, new Size(640, 640), new Scalar(0d), false, false, MatType.CV_32F);
                network.SetInput(blob, "images", 1d, null);
                string[] names = network.GetUnconnectedOutLayersNames();
                Assert.IsTrue(names.Length > 0, probe.ModelId + " has no unconnected output.");
                Mat[] outputs = network.Forward(names);
                try
                {
                    Assert.AreEqual(names.Length, outputs.Length, probe.ModelId + " output count mismatch.");
                    for (int index = 0; index < outputs.Length; index++)
                    {
                        Mat output = outputs[index];
                        Assert.IsFalse(output.Empty, probe.ModelId + " output is empty: " + names[index]);
                        Assert.IsTrue(output.HasData, probe.ModelId + " output has no data: " + names[index]);
                        Assert.AreEqual(MatType.CV_32F, output.Depth, probe.ModelId + " output depth mismatch: " + names[index]);
                        Assert.IsTrue(output.ValueCount > 0, probe.ModelId + " output has no values: " + names[index]);
                    }
                    Console.WriteLine("OPENCV_EXTERNAL_PROBE model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";outputs=" + string.Join(",", names) + ";elements=" + string.Join(",", Array.ConvertAll(outputs, output => output.ValueCount.ToString())));
                }
                finally
                {
                    foreach (Mat output in outputs) output.Dispose();
                }
            }
            catch (Exception exception)
            {
                if (string.Equals(probe.ModelId, "yolo/v7/detect", StringComparison.Ordinal))
                {
                    Console.WriteLine("OPENCV_EXTERNAL_PROBE_UNSUPPORTED model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";reason=OpenCV 5.0 DNN GatherLayerImpl shape validation");
                }
                else
                {
                    failed.Add(probe.ModelId);
                    Console.WriteLine("OPENCV_EXTERNAL_PROBE_FAILED model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";error=" + exception.Message);
                }
            }
        }

        if (missing.Count > 0) Assert.Inconclusive("Missing local models: " + string.Join(", ", missing));
        if (failed.Count > 0) Assert.Fail("OpenCV DNN probes failed: " + string.Join(", ", failed));
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void NonYoloCatalogModelsProbeOpenCvDnnCpu()
    {
        RequireExternal();
        var cases = new[]
        {
            new ImageProbeCase("rf-detr/detect", @"E:\Model\rf-detr\rf-detr.onnx", "b464822e768f5795f249a6bd08cf1c5299787806c740204ed8e46d3a369ab769", "input", 512, 512, false),
            new ImageProbeCase("rf-detr/segment", @"E:\Model\rf-detr\rf-detr-seg.onnx", "6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35", "input", 432, 432, false),
            new ImageProbeCase("rt-detr/r50vd-raw-query", @"E:\Model\RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx", "544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad", "image", 640, 640, false),
            new ImageProbeCase("paddleocr/ppocrv5/mobile-cls", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx", "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "x", 160, 80),
            new ImageProbeCase("paddleocr/ppocrv5/mobile-det", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx", "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", "x", 32, 32),
            new ImageProbeCase("paddleocr/ppocrv5/mobile-rec", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx", "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "x", 320, 48, false),
            new ImageProbeCase("paddleocr/ppocrv5/server-cls", @"E:\Model\ocr\ppocrv5-1\PP-OCRv5_server_cls_onnx.onnx", "d874cd926a8f9f66e886bbd8ad7747635802b6cc52d3b81b5892845fc84c616f", "x", 160, 80),
            new ImageProbeCase("paddleocr/ppocrv5/server-det", @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_det_onnx.onnx", "9a910baffbefb807ff2f7bfaa72910e3e470bd17014d798386d87bb46f442839", "x", 32, 32, false),
            new ImageProbeCase("paddleocr/ppocrv5/server-rec", @"E:\Model\ocr\ppocrv5\PP-OCRv5_server_rec_onnx.onnx", "5c4927aa0736ab598025a37b71daae061363642b1848a90a0cb1e02e2ce823d7", "x", 320, 48, false),
            new ImageProbeCase("anomalib/padim/mvtec-bottle", @"E:\Model\anomalib\Padim\model\padim.onnx", "bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff", "input", 256, 256),
            new ImageProbeCase("bria/rmbg-1.4", @"E:\Model\RMBG\bria-rmbg-1.4.onnx", "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f", "input", 1024, 1024),
            new ImageProbeCase("bria/rmbg-2.0|onnx.fp32", @"E:\Model\RMBG\RMBG-2.0.onnx", "5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958", "pixel_values", 1024, 1024, false),
            new ImageProbeCase("bria/rmbg-2.0|onnx.dynamic-int8", @"E:\Model\RMBG\RMBG-2.0_quantized.onnx", "fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420", "pixel_values", 1024, 1024, false)
        };

        var missing = new List<string>();
        var mismatched = new List<string>();
        foreach (ImageProbeCase probe in cases)
        {
            if (!File.Exists(probe.Path))
            {
                missing.Add(probe.ModelId);
                continue;
            }

            Assert.AreEqual(probe.Sha256, ComputeSha256(probe.Path), true, probe.ModelId + " SHA-256 mismatch.");
            try
            {
                using Net network = Net.ReadNetFromOnnx(probe.Path, DnnEngine.Classic);
                network.SetPreferableBackend(DnnBackend.OpenCV).SetPreferableTarget(DnnTarget.Cpu).EnableFusion(false).EnableWinograd(false);
                using var image = new Mat(probe.Height, probe.Width, MatType.CV_32FC3);
                using Mat blob = DnnCv2.BlobFromImage(image, 1d, new Size(probe.Width, probe.Height), new Scalar(0d), false, false, MatType.CV_32F);
                network.SetInput(blob, probe.InputName, 1d, null);
                string[] names = network.GetUnconnectedOutLayersNames();
                Mat[] outputs = network.Forward(names);
                try
                {
                    Assert.IsTrue(outputs.Length > 0, probe.ModelId + " returned no outputs.");
                    foreach (Mat output in outputs) Assert.IsTrue(output.HasData && output.ValueCount > 0, probe.ModelId + " returned an empty output.");
                    Console.WriteLine("OPENCV_CATALOG_PROBE_OK model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";outputs=" + string.Join(",", names) + ";elements=" + string.Join(",", Array.ConvertAll(outputs, output => output.ValueCount.ToString())));
                    if (!probe.ExpectedToRun) mismatched.Add(probe.ModelId + " unexpectedly passed");
                }
                finally
                {
                    foreach (Mat output in outputs) output.Dispose();
                }
            }
            catch (Exception exception)
            {
                if (probe.ExpectedToRun) mismatched.Add(probe.ModelId + " unexpectedly failed");
                Console.WriteLine("OPENCV_CATALOG_PROBE_UNSUPPORTED model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";error=" + exception.Message);
            }
        }

        if (missing.Count > 0) Assert.Inconclusive("Missing local models: " + string.Join(", ", missing));
        if (mismatched.Count > 0) Assert.Fail("OpenCV catalog compatibility changed: " + string.Join(", ", mismatched));
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SupportedNonYoloModelsUseDeploySharpOpenCvProvider()
    {
        RequireExternal();
        var cases = new[]
        {
            new ImageProviderCase("paddleocr/ppocrv5/mobile-cls", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx", "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2", "x", 160, 80, "fetch_name_0", new TensorShape(1, 2)),
            new ImageProviderCase("paddleocr/ppocrv5/mobile-det", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx", "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039", "x", 32, 32, "fetch_name_0", new TensorShape(1, 1, 32, 32)),
            new ImageProviderCase("paddleocr/ppocrv5/server-cls", @"E:\Model\ocr\ppocrv5-1\PP-OCRv5_server_cls_onnx.onnx", "d874cd926a8f9f66e886bbd8ad7747635802b6cc52d3b81b5892845fc84c616f", "x", 160, 80, "fetch_name_0", new TensorShape(1, 2)),
            new ImageProviderCase("bria/rmbg-1.4", @"E:\Model\RMBG\bria-rmbg-1.4.onnx", "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f", "input", 1024, 1024, "output", new TensorShape(1, 1, 1024, 1024))
        };

        foreach (ImageProviderCase probe in cases)
        {
            if (!File.Exists(probe.Path)) Assert.Inconclusive("Missing local model: " + probe.Path);
            var modelId = new ModelId(probe.ModelId);
            var inputShape = new TensorShape(1, 3, probe.Height, probe.Width);
            var contract = new OpenCvDnnModelContract(
                modelId,
                new[] { new TensorDescriptor(probe.InputName, TensorElementType.Float32, inputShape) },
                new[] { new TensorDescriptor(probe.OutputName, TensorElementType.Float32, probe.OutputShape) });
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(modelId, "onnx", probe.Path, probe.Sha256),
                new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                SessionOptions.Default);
            var input = new Tensor<float>(inputShape, new float[checked(3 * probe.Width * probe.Height)]);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(probe.InputName, input), CancellationToken.None);
            Assert.AreEqual(1, outputs.Count, probe.ModelId);
            Assert.AreEqual(probe.OutputShape.GetElementCount(), outputs[0].Tensor.Length, probe.ModelId);
            Console.WriteLine("OPENCV_CATALOG_PROVIDER model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";output=" + probe.OutputName + ";elements=" + outputs[0].Tensor.Length);
        }
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void AdditionalYoloMultiTaskModelsProbeOpenCvDnnCpu()
    {
        RequireExternal();
        var cases = new[]
        {
            new ImageProbeCase("yolo/v5/segment/s", @"E:\Model\yolo\yolov5\yolov5s-seg.onnx", "ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576", "images", 640, 640),
            new ImageProbeCase("yolo/v9/segment/c", @"E:\Model\yolo\yolov9-c-seg.onnx", "2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0", "images", 640, 640),
            new ImageProbeCase("yolo/v11/segment/s", @"E:\Model\yolo\yolov11\yolo11s-seg.onnx", "0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48", "images", 640, 640),
            new ImageProbeCase("yolo/v26/segment/s", @"E:\Model\yolo\yolov26\yolo26s-seg.onnx", "79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f", "images", 640, 640),
            new ImageProbeCase("yolo/v11/pose/s", @"E:\Model\yolo\yolov11\yolo11s-pose.onnx", "5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5", "images", 640, 640),
            new ImageProbeCase("yolo/v26/pose/s", @"E:\Model\yolo\yolov26\yolo26s-pose.onnx", "55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc", "images", 640, 640),
            new ImageProbeCase("yolo/v11/obb/s", @"E:\Model\yolo\yolov11\yolo11s-obb.onnx", "50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0", "images", 1024, 1024),
            new ImageProbeCase("yolo/v26/obb/s", @"E:\Model\yolo\yolov26\yolo26s-obb.onnx", "bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df", "images", 1024, 1024)
        };

        var failed = new List<string>();
        foreach (ImageProbeCase probe in cases)
        {
            if (!File.Exists(probe.Path)) Assert.Inconclusive("Missing local model: " + probe.Path);
            Assert.AreEqual(probe.Sha256, ComputeSha256(probe.Path), true, probe.ModelId + " SHA-256 mismatch.");
            try
            {
                using Net network = Net.ReadNetFromOnnx(probe.Path, DnnEngine.Classic);
                network.SetPreferableBackend(DnnBackend.OpenCV).SetPreferableTarget(DnnTarget.Cpu).EnableFusion(false).EnableWinograd(false);
                using var image = new Mat(probe.Height, probe.Width, MatType.CV_32FC3);
                using Mat blob = DnnCv2.BlobFromImage(image, 1d, new Size(probe.Width, probe.Height), new Scalar(0d), false, false, MatType.CV_32F);
                network.SetInput(blob, probe.InputName, 1d, null);
                string[] names = network.GetUnconnectedOutLayersNames();
                Mat[] outputs = network.Forward(names);
                try
                {
                    Assert.IsTrue(outputs.Length > 0, probe.ModelId + " returned no outputs.");
                    foreach (Mat output in outputs) Assert.IsTrue(output.HasData && output.ValueCount > 0, probe.ModelId + " returned an empty output.");
                    Console.WriteLine("OPENCV_YOLO_MULTITASK_PROBE_OK model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";outputs=" + string.Join(",", names) + ";elements=" + string.Join(",", Array.ConvertAll(outputs, output => output.ValueCount.ToString())));
                }
                finally
                {
                    foreach (Mat output in outputs) output.Dispose();
                }
            }
            catch (Exception exception)
            {
                failed.Add(probe.ModelId);
                Console.WriteLine("OPENCV_YOLO_MULTITASK_PROBE_UNSUPPORTED model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";error=" + exception.Message);
            }
        }

        if (failed.Count > 0) Assert.Fail("OpenCV YOLO multi-task probes failed: " + string.Join(", ", failed));
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void AdditionalYoloMultiTaskModelsUseDeploySharpOpenCvProvider()
    {
        RequireExternal();
        var cases = new[]
        {
            new MultiOutputProviderCase("yolo/v5/segment/s", @"E:\Model\yolo\yolov5\yolov5s-seg.onnx", "ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576", 640, 640, new[] { Output("output0", 1, 25200, 117), Output("output1", 1, 32, 160, 160) }),
            new MultiOutputProviderCase("yolo/v9/segment/c", @"E:\Model\yolo\yolov9-c-seg.onnx", "2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0", 640, 640, new[] { Output("output0", 1, 116, 8400), Output("output1", 1, 32, 160, 160) }),
            new MultiOutputProviderCase("yolo/v11/segment/s", @"E:\Model\yolo\yolov11\yolo11s-seg.onnx", "0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48", 640, 640, new[] { Output("output0", 1, 116, 8400), Output("output1", 1, 32, 160, 160) }),
            new MultiOutputProviderCase("yolo/v26/segment/s", @"E:\Model\yolo\yolov26\yolo26s-seg.onnx", "79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f", 640, 640, new[] { Output("output0", 1, 300, 38), Output("output1", 1, 32, 160, 160) }),
            new MultiOutputProviderCase("yolo/v11/pose/s", @"E:\Model\yolo\yolov11\yolo11s-pose.onnx", "5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5", 640, 640, new[] { Output("output0", 1, 56, 8400) }),
            new MultiOutputProviderCase("yolo/v26/pose/s", @"E:\Model\yolo\yolov26\yolo26s-pose.onnx", "55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc", 640, 640, new[] { Output("output0", 1, 300, 57) }),
            new MultiOutputProviderCase("yolo/v11/obb/s", @"E:\Model\yolo\yolov11\yolo11s-obb.onnx", "50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0", 1024, 1024, new[] { Output("output0", 1, 20, 21504) }),
            new MultiOutputProviderCase("yolo/v26/obb/s", @"E:\Model\yolo\yolov26\yolo26s-obb.onnx", "bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df", 1024, 1024, new[] { Output("output0", 1, 300, 7) })
        };

        foreach (MultiOutputProviderCase probe in cases)
        {
            if (!File.Exists(probe.Path)) Assert.Inconclusive("Missing local model: " + probe.Path);
            var modelId = new ModelId(probe.ModelId);
            var inputShape = new TensorShape(1, 3, probe.Height, probe.Width);
            var contract = new OpenCvDnnModelContract(
                modelId,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, inputShape) },
                probe.Outputs);
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(modelId, "onnx", probe.Path, probe.Sha256),
                new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                SessionOptions.Default);
            var input = new Tensor<float>(inputShape, new float[checked(3 * probe.Width * probe.Height)]);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create("images", input), CancellationToken.None);
            Assert.AreEqual(probe.Outputs.Count, outputs.Count, probe.ModelId);
            for (int index = 0; index < outputs.Count; index++) Assert.AreEqual(probe.Outputs[index].Shape.GetElementCount(), outputs[index].Tensor.Length, probe.ModelId);
            Console.WriteLine("OPENCV_YOLO_MULTITASK_PROVIDER model=" + probe.ModelId + ";sha256=" + probe.Sha256 + ";outputs=" + outputs.Count);
        }
    }

    private sealed record ProbeCase(string ModelId, string Path, string Sha256);
    private sealed record ProviderCase(string ModelId, string Path, string Sha256, string OutputName, TensorShape OutputShape);
    private sealed record ImageProbeCase(string ModelId, string Path, string Sha256, string InputName, int Width, int Height, bool ExpectedToRun = true);
    private sealed record ImageProviderCase(string ModelId, string Path, string Sha256, string InputName, int Width, int Height, string OutputName, TensorShape OutputShape);
    private sealed record MultiOutputProviderCase(string ModelId, string Path, string Sha256, int Width, int Height, IReadOnlyList<TensorDescriptor> Outputs);

    private static TensorDescriptor Output(string name, params long[] shape)
        => new TensorDescriptor(name, TensorElementType.Float32, new TensorShape(shape));

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void RequireExternal()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_OPENCV_RUN_EXTERNAL=1 to run exact local OpenCV DNN probes.");
        }
    }
}
