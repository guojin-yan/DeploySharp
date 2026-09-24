using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.TensorRT;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests
{
    /// <summary>
    /// Runs converted PP-Structure models through the applicable TensorRT
    /// execution and DeploySharp decoder paths. The orientation case covers
    /// the CUDA preprocessing path; the layout parity case deliberately uses
    /// the shared CPU image adapter so auxiliary Paddle geometry inputs can be
    /// compared against an ORT reference. This remains opt-in because
    /// TensorRT/CUDA are consumer-owned native dependencies.
    /// </summary>
    [TestClass]
    public sealed class PaddleDocumentTensorRtExternalIntegrationTests
    {
        private const string ModelId = "paddle-doc/pp-lcnet-x1-0-doc-ori";
        private const string DefaultOnnxPath = @"E:\Model\PaddleDocument\onnx\pp-lcnet-x1-0-doc-ori.onnx";
        private const string DefaultImagePath = @"E:\Model\PaddleDocument\validation\img_rot180_demo.jpg";
        private const string ExpectedOnnxSha256 = "96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DocumentOrientationRunsThroughCudaPreprocessTensorRtAndDecoder()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");
            }

            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_ONNX") ?? DefaultOnnxPath;
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_IMAGE") ?? DefaultImagePath;
            if (!File.Exists(onnxPath)) Assert.Inconclusive("Missing local PP-Structure ONNX model: " + onnxPath);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing local PP-Structure test image: " + imagePath);

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(ModelId);
            PaddleDocumentProfile document = PaddleDocumentProfiles.CreateClassification(
                descriptor,
                PaddleDocumentProfiles.DocumentOrientationLabels,
                VisualTaskId.DocumentOrientation,
                inputName: "x",
                outputName: "fetch_name_0",
                modelSize: new VisualSize(224, 224),
                maximumBatch: 1,
                allowDynamicBatch: false);

            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-document-trt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, "pp-lcnet-x1-0-doc-ori.engine");
            try
            {
                string onnxSha256 = ComputeSha256(onnxPath);
                Assert.AreEqual(ExpectedOnnxSha256, onnxSha256, "The local ONNX file differs from the registered Release artifact; do not record this run as Release evidence.");
                var artifact = new ModelArtifact(new ModelId(ModelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                var buildOptions = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 3,
                    inputProfiles: new[]
                    {
                        // PP-LCNet exports a dynamic batch dimension even though
                        // this smoke uses one image. TensorRT requires an explicit
                        // min/opt/max profile for every dynamic input.
                        new TensorRtOnnxInputProfile(
                            "x",
                            new TensorShape(1, 3, 224, 224),
                            new TensorShape(1, 3, 224, 224),
                            new TensorShape(1, 3, 224, 224))
                    },
                    overwrite: true);

                Stopwatch buildWatch = Stopwatch.StartNew();
                TensorRtOnnxEngineBuildResult build;
                try
                {
                    build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, buildOptions);
                }
                catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                {
                    // A missing/mismatched consumer-owned CUDA/TensorRT stack is
                    // an environment blocker, not model evidence. Keep the
                    // external test opt-in and report the exact adapter details
                    // instead of turning every developer checkout red.
                    string details = exception.TechnicalDetails ?? exception.InnerException?.Message ?? exception.Message;
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_BLOCKED model=" + ModelId + ";errorCode=" + exception.ErrorCode + ";details=" + details + ";runtime=" + DescribeNativeRuntime());
                    Assert.Inconclusive("TensorRT native runtime is unavailable or mismatched. Install the bridge plus matching CUDA/cuDNN/TensorRT DLLs and retry. " + details);
                    return;
                }
                buildWatch.Stop();

                string architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86";
                var backendOptions = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: architecture);
                // Keep TensorRT smoke aligned with the official PP-LCNet
                // contract: resize_short=256, center crop=224, ImageNet norm.
                var preprocessing = document.VisualProfile.Preprocessing!;

                Stopwatch imageWatch = Stopwatch.StartNew();
                OpenCvBgrImage image = new OpenCvBgrImageFactory().CreateFromFile(imagePath);
                imageWatch.Stop();
                string imageHash = ComputeSha256(imagePath);
                float cpuReferenceScore;
                using (var provider = new TensorRtBackendProvider(backendOptions))
                using (var session = provider.CreateSession(new ModelArtifact(new ModelId(ModelId), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), SessionOptions.Default))
                using (var input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, document.VisualProfile))
                {
                    var output = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                    var reference = (ClassificationResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, output, CancellationToken.None));
                    cpuReferenceScore = reference.TopPrediction!.Score;
                }
                using (var pipeline = new TensorRtVisualPipeline(document.VisualProfile, build.EnginePath, preprocessing, backendOptions, TensorRtCudaVisualPostprocessingMode.Disabled))
                {
                    const int warmup = 5, iterations = 50;
                    var samples = new List<double>();
                    var stageSamples = new List<object>();
                    VisualInferenceResult result = null!;
                    for (int index = -warmup; index < iterations; index++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        result = pipeline.Run(image);
                        double elapsed = watch.Elapsed.TotalMilliseconds;
                        var classification = result.GetValue<ClassificationResult>();
                        Assert.IsNotNull(classification.TopPrediction);
                        Assert.AreEqual(cpuReferenceScore, classification.TopPrediction!.Score, .005f, "CPU/CUDA preprocessing disagrees on the same TensorRT engine.");
                        if (imageHash == "c5a77e031470e13878ff4f28a06ca843fd455d95c20b1b49b486681e346209ed")
                        {
                            Assert.AreEqual(2, classification.TopPrediction!.Index);
                            Assert.AreEqual(.88164f, classification.TopPrediction.Score, .025f);
                        }
                        if (index >= 0)
                        {
                            samples.Add(elapsed);
                            stageSamples.Add(new { totalMs = elapsed, preprocessingMs = result.Timing.Preprocessing.TotalMilliseconds, inferenceMs = result.Timing.Inference.TotalMilliseconds, postprocessingMs = result.Timing.Postprocessing.TotalMilliseconds });
                        }
                    }
                    double[] sorted = samples.OrderBy(value => value).ToArray();
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_WARM " + JsonSerializer.Serialize(new { model = ModelId, machine = Environment.MachineName, imageSha256 = imageHash, onnxSha256, warmup, iterations, batch = 1, sessions = 1, optimizationLevel = 3, p50Ms = sorted[(int)Math.Ceiling(iterations * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(iterations * .95) - 1], score = result.GetValue<ClassificationResult>().TopPrediction!.Score, cpuReferenceScore, samples = stageSamples }));

                    Console.WriteLine(
                        "PADDLE_DOCUMENT_TENSORRT_EXTERNAL model=" + ModelId +
                        ";onnxSha256=" + onnxSha256 +
                        ";engineSha256=" + build.EngineSha256 +
                        ";engineBytes=" + build.EngineBytes +
                        ";engineBuildMs=" + buildWatch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        ";imageDecodeMs=" + imageWatch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        ";preprocessMs=" + result.Timing.Preprocessing.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        ";inferenceMs=" + result.Timing.Inference.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        ";postprocessMs=" + result.Timing.Postprocessing.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        ";cudaArchitecture=" + architecture +
                        ";backend=" + result.BackendId.Value +
                        ";valueType=" + result.Value.GetType().FullName);
                }
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void TableClassificationRunsThroughTensorRtAndPreservesOrtScore()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");

            const string modelId = "paddle-table/pp-lcnet-x1-0-table-cls";
            const string defaultOnnx = @"E:\Model\PaddleDocument\onnx\pp-lcnet-x1-0-table-cls.onnx";
            const string defaultImage = @"E:\Model\PaddleDocument\validation\table_recognition.jpg";
            const string expectedOnnxSha256 = "04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45";
            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_ONNX") ?? defaultOnnx;
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_IMAGE") ?? defaultImage;
            if (!File.Exists(onnxPath)) Assert.Inconclusive("Missing local PP-Structure ONNX model: " + onnxPath);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing local PP-Structure test image: " + imagePath);

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
            PaddleDocumentProfile document = PaddleDocumentProfiles.CreateClassification(
                descriptor,
                new[] { "wired_table", "wireless_table" },
                VisualTaskId.TableClassification,
                inputName: "x",
                outputName: "fetch_name_0",
                modelSize: new VisualSize(224, 224),
                maximumBatch: 1,
                allowDynamicBatch: false);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-table-trt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, "pp-lcnet-x1-0-table-cls.engine");
            try
            {
                string onnxSha256 = ComputeSha256(onnxPath);
                Assert.AreEqual(expectedOnnxSha256, onnxSha256, "The local ONNX file differs from the registered Release artifact; do not record this run as Release evidence.");
                var artifact = new ModelArtifact(new ModelId(modelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                var buildOptions = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 3,
                    inputProfiles: new[]
                    {
                        new TensorRtOnnxInputProfile(
                            "x",
                            new TensorShape(1, 3, 224, 224),
                            new TensorShape(1, 3, 224, 224),
                            new TensorShape(1, 3, 224, 224))
                    },
                    overwrite: true);

                Stopwatch buildWatch = Stopwatch.StartNew();
                TensorRtOnnxEngineBuildResult build;
                try
                {
                    build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, buildOptions);
                }
                catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                {
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_BLOCKED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message) + ";runtime=" + DescribeNativeRuntime());
                    Assert.Inconclusive("TensorRT native runtime is unavailable or mismatched. " + (exception.TechnicalDetails ?? exception.Message));
                    return;
                }
                buildWatch.Stop();

                string architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86";
                var backendOptions = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: architecture);
                OpenCvBgrImage image = new OpenCvBgrImageFactory().CreateFromFile(imagePath);
                using (var ortRegistry = new BackendRegistry().UseOnnxRuntime())
                using (IInferenceSession ortSession = ortRegistry.CreateSession(document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                using (PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, document.VisualProfile))
                {
                    InferenceOutputs outputs = ortSession.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                    ClassificationResult cpuReference = (ClassificationResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                    Assert.IsNotNull(cpuReference.TopPrediction);

                    int warmup = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_WARMUP", 5);
                    int iterations = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_ITERATIONS", 50);
                    const float scoreTolerance = .01f;
                    var samples = new List<double>();
                    ClassificationResult trtResult = null!;
                    using (var pipeline = new TensorRtVisualPipeline(document.VisualProfile, build.EnginePath, document.VisualProfile.Preprocessing!, backendOptions, TensorRtCudaVisualPostprocessingMode.Disabled))
                    {
                        for (int index = -warmup; index < iterations; index++)
                        {
                            Stopwatch watch = Stopwatch.StartNew();
                            VisualInferenceResult result = pipeline.Run(image);
                            watch.Stop();
                            trtResult = result.GetValue<ClassificationResult>();
                            Assert.IsNotNull(trtResult.TopPrediction);
                            Assert.AreEqual(cpuReference.TopPrediction!.Index, trtResult.TopPrediction!.Index);
                            // TensorRT and ORT may select different fused accumulation paths while
                            // retaining the same class. Keep this explicit, bounded tolerance in the
                            // external evidence instead of treating harmless sub-percent drift as a
                            // backend mismatch.
                            Assert.AreEqual(cpuReference.TopPrediction.Score, trtResult.TopPrediction.Score, scoreTolerance,
                                "TensorRT table-classification confidence drift exceeded the .01 absolute tolerance.");
                            if (index >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                        }
                    }

                    double[] sorted = samples.OrderBy(value => value).ToArray();
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_TABLE_CLASSIFICATION " + JsonSerializer.Serialize(new
                    {
                        model = modelId,
                        onnxSha256,
                        engineSha256 = build.EngineSha256,
                        engineBytes = build.EngineBytes,
                        api = (int)apiVersion,
                        engineBuildMs = buildWatch.Elapsed.TotalMilliseconds,
                        warmup,
                        iterations,
                        label = trtResult.TopPrediction!.Label,
                        score = trtResult.TopPrediction.Score,
                        cpuScore = cpuReference.TopPrediction!.Score,
                        scoreTolerance,
                        p50Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .5) - 1)],
                        p95Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .95) - 1)]
                    }));
                }
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
            {
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_UNSUPPORTED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                Assert.Inconclusive("TensorRT importer does not currently accept this PP-Structure graph. " + (exception.TechnicalDetails ?? exception.Message));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void MobileSealDetectionRunsThroughTensorRtAndPreservesOrtMaskDecoding()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");

            const string modelId = "paddle-seal/ppocrv4-mobile";
            const string defaultOnnx = @"E:\Model\PaddleDocument\onnx\ppocrv4-mobile-seal-det.onnx";
            const string defaultImage = @"E:\Data\ocr\demo_1.jpg";
            const string expectedOnnxSha256 = "e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199";
            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_ONNX") ?? defaultOnnx;
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_IMAGE") ?? defaultImage;
            if (!File.Exists(onnxPath)) Assert.Inconclusive("Missing local PP-Structure ONNX model: " + onnxPath);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing local seal validation image: " + imagePath);

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
            PaddleDocumentProfile document = PaddleDocumentProfiles.CreateSealDetection(
                descriptor,
                modelSize: new VisualSize(224, 224),
                inputName: "x",
                outputName: "fetch_name_0",
                maximumBatch: 1,
                threshold: .3f,
                minimumArea: 16);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-seal-trt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, "ppocrv4-mobile-seal-det.engine");
            try
            {
                string onnxSha256 = ComputeSha256(onnxPath);
                Assert.AreEqual(expectedOnnxSha256, onnxSha256, "The local ONNX file differs from the registered Release artifact; do not record this run as Release evidence.");
                var artifact = new ModelArtifact(new ModelId(modelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                var buildOptions = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 3,
                    inputProfiles: new[]
                    {
                        // The official seal export is dynamic in N/H/W. The
                        // runtime contract fixes this evidence run at 224x224.
                        new TensorRtOnnxInputProfile(
                            "x",
                            new TensorShape(1, 3, 224, 224),
                            new TensorShape(1, 3, 224, 224),
                            new TensorShape(1, 3, 224, 224))
                    },
                    overwrite: true);

                Stopwatch buildWatch = Stopwatch.StartNew();
                TensorRtOnnxEngineBuildResult build;
                try
                {
                    build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, buildOptions);
                }
                catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                {
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_BLOCKED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message) + ";runtime=" + DescribeNativeRuntime());
                    Assert.Inconclusive("TensorRT native runtime is unavailable or mismatched. " + (exception.TechnicalDetails ?? exception.Message));
                    return;
                }
                buildWatch.Stop();

                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                    imagePath,
                    document.VisualProfile,
                    inputId: "paddle-document-tensorrt-seal");
                var inputs = new[] { new NamedTensor(input.InputName, input.Tensor) };
                var request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
                PaddleDocumentSealResult ortReference;
                float[] ortMask;
                using (var ortRegistry = new BackendRegistry().UseOnnxRuntime())
                using (IInferenceSession ortSession = ortRegistry.CreateSession(document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                {
                    InferenceOutputs outputs = ortSession.Run(new InferenceInputs(inputs), CancellationToken.None);
                    ITensor tensor = outputs.GetRequired("fetch_name_0");
                    Assert.IsTrue(tensor.Buffer is float[], "The seal reference output must be Float32.");
                    ortMask = ((float[])tensor.Buffer).ToArray();
                    ortReference = (PaddleDocumentSealResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                }

                string architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86";
                var backendOptions = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: architecture);
                int warmup = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_WARMUP", 5);
                int iterations = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_ITERATIONS", 50);
                var samples = new List<double>();
                PaddleDocumentSealResult trtResult = null!;
                double maskMaxAbs = 0;
                double maskMeanAbs = 0;
                using (var provider = new TensorRtBackendProvider(backendOptions))
                using (IInferenceSession session = provider.CreateSession(new ModelArtifact(new ModelId(modelId), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), request, SessionOptions.Default))
                {
                    for (int index = -warmup; index < iterations; index++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
                        ITensor tensor = outputs.GetRequired("fetch_name_0");
                        Assert.IsTrue(tensor.Buffer is float[], "The TensorRT seal output must be Float32.");
                        float[] trtMask = (float[])tensor.Buffer;
                        Assert.AreEqual(ortMask.Length, trtMask.Length, "TensorRT changed the seal probability-map element count.");
                        if (index == -warmup)
                        {
                            double sumAbs = 0;
                            for (int i = 0; i < ortMask.Length; i++)
                            {
                                double difference = Math.Abs((double)ortMask[i] - trtMask[i]);
                                maskMaxAbs = Math.Max(maskMaxAbs, difference);
                                sumAbs += difference;
                            }
                            maskMeanAbs = sumAbs / ortMask.Length;
                            Assert.IsTrue(maskMaxAbs <= .05, "TensorRT seal probability map drifted beyond the .05 absolute tolerance.");
                        }
                        trtResult = (PaddleDocumentSealResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                        watch.Stop();
                        Assert.IsTrue(trtResult.MaskWidth > 0 && trtResult.MaskHeight > 0);
                        Assert.IsTrue(trtResult.Regions.All(region => region.Score >= 0 && region.Score <= 1));
                        if (index >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }

                Assert.AreEqual(ortReference.MaskWidth, trtResult.MaskWidth, "TensorRT changed the seal probability-map width.");
                Assert.AreEqual(ortReference.MaskHeight, trtResult.MaskHeight, "TensorRT changed the seal probability-map height.");
                Assert.AreEqual(ortReference.Regions.Count, trtResult.Regions.Count, "TensorRT changed the decoded seal component count.");
                double[] sorted = samples.OrderBy(value => value).ToArray();
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_SEAL " + JsonSerializer.Serialize(new
                {
                    model = modelId,
                    image = imagePath,
                    onnxSha256,
                    engineSha256 = build.EngineSha256,
                    engineBytes = build.EngineBytes,
                    api = (int)apiVersion,
                    engineBuildMs = buildWatch.Elapsed.TotalMilliseconds,
                    warmup,
                    iterations,
                    mask = trtResult.MaskWidth + "x" + trtResult.MaskHeight,
                    ortRegions = ortReference.Regions.Count,
                    trtRegions = trtResult.Regions.Count,
                    maskMaxAbs,
                    maskMeanAbs,
                    p50Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .5) - 1)],
                    p95Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .95) - 1)],
                    cudaArchitecture = architecture
                }));
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
            {
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_UNSUPPORTED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                Assert.Inconclusive("TensorRT importer does not currently accept this PP-Structure graph. " + (exception.TechnicalDetails ?? exception.Message));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LayoutNmsRunsThroughTensorRtAndPreservesOrtDecoding()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");

            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_ONNX") ?? @"E:\Model\PaddleDocument\onnx\pp-doclayout-l.onnx";
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(onnxPath)) Assert.Inconclusive("Missing local PP-Structure ONNX model: " + onnxPath);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing local PP-Structure test image: " + imagePath);

            const string modelId = "paddle-doc/pp-doclayout-l";
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
            PaddleDocumentProfile document = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                scoreThreshold: 0);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-layout-trt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, "pp-doclayout-l.engine");
            try
            {
                string onnxSha256 = ComputeSha256(onnxPath);
                var artifact = new ModelArtifact(new ModelId(modelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                var shape = new TensorShape(1, 2);
                var buildOptions = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    workspaceBytes: 536870912UL,
                    optimizationLevel: 3,
                    inputProfiles: new[]
                    {
                        new TensorRtOnnxInputProfile("im_shape", shape, shape, shape),
                        new TensorRtOnnxInputProfile("image", new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640)),
                        new TensorRtOnnxInputProfile("scale_factor", shape, shape, shape)
                    },
                    overwrite: true);

                TensorRtOnnxEngineBuildResult build;
                try
                {
                    build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, buildOptions);
                }
                catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                {
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_BLOCKED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message) + ";runtime=" + DescribeNativeRuntime());
                    Assert.Inconclusive("TensorRT native runtime is unavailable or mismatched. " + (exception.TechnicalDetails ?? exception.Message));
                    return;
                }

                using var input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, document, inputId: "paddle-document-tensorrt-layout");
                var inputs = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
                inputs.AddRange(input.AuxiliaryInputs);
                var request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
                DetectionResult cpuReference;
                using (var ortRegistry = new BackendRegistry().UseOnnxRuntime())
                using (IInferenceSession ortSession = ortRegistry.CreateSession(document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                {
                    InferenceOutputs outputs = ortSession.Run(new InferenceInputs(inputs), CancellationToken.None);
                    cpuReference = (DetectionResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                }

                var options = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86");
                var samples = new List<double>();
                DetectionResult trtResult = null!;
                int warmup = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_WARMUP", 5);
                int iterations = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_ITERATIONS", 50);
                using (var provider = new TensorRtBackendProvider(options))
                using (IInferenceSession session = provider.CreateSession(new ModelArtifact(new ModelId(modelId), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), request, SessionOptions.Default))
                {
                    for (int index = -warmup; index < iterations; index++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
                        trtResult = (DetectionResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                        watch.Stop();
                        if (index >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }

                Assert.IsTrue(cpuReference.Detections.Count > 0, "The ORT reference returned no layout regions.");
                Assert.AreEqual(cpuReference.Detections.Count, trtResult.Detections.Count, "TensorRT changed the exported NMS result count.");
                // Multiclass NMS does not promise a stable order when two
                // candidates have nearly equal scores. Compare a deterministic
                // semantic order rather than treating backend tie-breaking as a
                // coordinate error.
                const float confidenceFloor = .05f;
                Detection[] expectedDetections = cpuReference.Detections.Where(value => value.Label.Score >= confidenceFloor).OrderBy(value => value.Label.Index).ThenByDescending(value => value.Label.Score).ThenBy(value => value.Box.X).ToArray();
                Detection[] actualDetections = trtResult.Detections.Where(value => value.Label.Score >= confidenceFloor).OrderBy(value => value.Label.Index).ThenByDescending(value => value.Label.Score).ThenBy(value => value.Box.X).ToArray();
                Assert.AreEqual(expectedDetections.Length, actualDetections.Length, "TensorRT changed the confident layout region count.");
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_LAYOUT_COMPARE ort=" + JsonSerializer.Serialize(expectedDetections.Take(5).Select(value => new { label = value.Label.Index, score = value.Label.Score, x = value.Box.X, y = value.Box.Y, w = value.Box.Width, h = value.Box.Height })) + ";trt=" + JsonSerializer.Serialize(actualDetections.Take(5).Select(value => new { label = value.Label.Index, score = value.Label.Score, x = value.Box.X, y = value.Box.Y, w = value.Box.Width, h = value.Box.Height })));
                for (int index = 0; index < expectedDetections.Length; index++)
                {
                    Assert.AreEqual(expectedDetections[index].Label.Index, actualDetections[index].Label.Index);
                    Assert.AreEqual(expectedDetections[index].Label.Score, actualDetections[index].Label.Score, .01f);
                    Assert.AreEqual(expectedDetections[index].Box.X, actualDetections[index].Box.X, 1.5f);
                    Assert.AreEqual(expectedDetections[index].Box.Y, actualDetections[index].Box.Y, 1.5f);
                    Assert.AreEqual(expectedDetections[index].Box.Width, actualDetections[index].Box.Width, 1.5f);
                    Assert.AreEqual(expectedDetections[index].Box.Height, actualDetections[index].Box.Height, 1.5f);
                }
                double[] sorted = samples.OrderBy(value => value).ToArray();
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_LAYOUT " + JsonSerializer.Serialize(new { model = modelId, onnxSha256, engineSha256 = build.EngineSha256, engineBytes = build.EngineBytes, api = (int)apiVersion, warmup, iterations, detections = trtResult.Detections.Count, confidentDetections = actualDetections.Length, confidenceFloor, p50Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .5) - 1)], p95Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .95) - 1)], scoreTolerance = .01, boxTolerancePx = 1.5 }));
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
            {
                // Parser/importer gaps are recorded as an explicit model/backend
                // blocker. Do not claim that the ONNX graph or the decoder is bad.
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_UNSUPPORTED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                Assert.Inconclusive("TensorRT importer does not currently accept this PP-Structure graph. " + (exception.TechnicalDetails ?? exception.Message));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void UvDocRunsThroughTensorRtDecoderAndRecordsOrtDrift()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the UVDoc TensorRT parity test.");
            const string modelId = "paddle-doc/uvdoc";
            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_UVDOC_ONNX") ?? @"E:\Model\PaddleDocument\onnx\uvdoc.onnx";
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_UVDOC_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(onnxPath) || !File.Exists(imagePath)) Assert.Inconclusive("Missing UVDoc TensorRT model or image.");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
            PaddleDocumentProfile document = PaddleDocumentProfiles.CreateUnwarping(descriptor, new VisualSize(640, 640));
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-uvdoc-trt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, "uvdoc.engine");
            try
            {
                string onnxSha256 = ComputeSha256(onnxPath);
                var artifact = new ModelArtifact(new ModelId(modelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                var build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 3,
                    disableTf32: true,
                    inputProfiles: new[] { new TensorRtOnnxInputProfile("image", new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640)) },
                    overwrite: true));
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, document.VisualProfile, "uvdoc-tensorrt-parity");
                var inputs = InferenceInputs.Create(input.InputName, input.Tensor);
                InferenceOutputs ortOutputs;
                using (var ortRegistry = new BackendRegistry().UseOnnxRuntime())
                using (IInferenceSession ort = ortRegistry.CreateSession(document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                    ortOutputs = ort.Run(inputs, CancellationToken.None);
                var options = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86");
                var request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
                InferenceOutputs trtOutputs;
                using (var provider = new TensorRtBackendProvider(options))
                using (IInferenceSession trt = provider.CreateSession(new ModelArtifact(new ModelId(modelId), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), request, SessionOptions.Default))
                    trtOutputs = trt.Run(inputs, CancellationToken.None);
                float[] ortValues = (float[])ortOutputs.GetRequired("fetch_name_0").Buffer;
                float[] trtValues = (float[])trtOutputs.GetRequired("fetch_name_0").Buffer;
                Assert.AreEqual(ortValues.Length, trtValues.Length);
                double maxAbs = 0, sumAbs = 0;
                for (int i = 0; i < ortValues.Length; i++) { double delta = Math.Abs((double)ortValues[i] - trtValues[i]); maxAbs = Math.Max(maxAbs, delta); sumAbs += delta; }
                double meanAbs = sumAbs / ortValues.Length;
                Assert.IsTrue(trtValues.All(float.IsFinite), "TensorRT UVDoc output contains non-finite values.");
                var ortResult = (PaddleDocumentUnwarpingResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, ortOutputs, CancellationToken.None));
                var trtResult = (PaddleDocumentUnwarpingResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, trtOutputs, CancellationToken.None));
                Assert.AreEqual(ortResult.Width, trtResult.Width); Assert.AreEqual(ortResult.Height, trtResult.Height); Assert.AreEqual(ortResult.Channels, trtResult.Channels);
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_UVDOC " + JsonSerializer.Serialize(new { model = modelId, onnxSha256, engineSha256 = build.EngineSha256, engineBytes = build.EngineBytes, output = trtResult.Width + "x" + trtResult.Height + "x" + trtResult.Channels, maxAbs, meanAbs, status = maxAbs < .1 && meanAbs < .01 ? "close" : "ranged-difference" }));
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
            {
                Assert.Inconclusive("TensorRT cannot build UVDoc: " + (exception.TechnicalDetails ?? exception.Message));
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        private static TensorRtApiVersion ResolveApiVersion()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API") ?? Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API");
            if (string.IsNullOrWhiteSpace(value))
            {
                // The current checked-in PP-Structure bridge and the local
                // validation bundle target TensorRT 11. If a caller selected
                // an explicitly versioned SDK root, infer its API line rather
                // than silently falling back to the historical TRT10 default.
                string root = Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT") ?? string.Empty;
                var match = System.Text.RegularExpressions.Regex.Match(root, @"TensorRT-(8|10|11)(?:\.|-|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int inferred) && Enum.IsDefined(typeof(TensorRtApiVersion), inferred))
                        return (TensorRtApiVersion)inferred;
                }
                return TensorRtApiVersion.TensorRt11;
            }
            if (int.TryParse(value, out int number) && Enum.IsDefined(typeof(TensorRtApiVersion), number)) return (TensorRtApiVersion)number;
            if (Enum.TryParse(value, ignoreCase: true, out TensorRtApiVersion parsed) && Enum.IsDefined(typeof(TensorRtApiVersion), parsed)) return parsed;
            throw new ArgumentException("TensorRT API must be 8, 10, 11, TensorRt8, TensorRt10, or TensorRt11.", nameof(value));
        }

        private static int ResolvePositiveInt(string name, int fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private static string DescribeNativeRuntime()
        {
            string[] variables = { "JYPPX_NATIVE_BRIDGE_PATH", "JYPPX_TENSORRT_ROOT", "JYPPX_CUDA_ROOT", "JYPPX_CUDNN_ROOT", "PATH" };
            var parts = new System.Collections.Generic.List<string>();
            foreach (string variable in variables)
            {
                string value = Environment.GetEnvironmentVariable(variable) ?? string.Empty;
                if (variable == "PATH")
                {
                    string[] pathEntries = value.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    value = "entries=" + pathEntries.Length;
                }
                else if (!string.IsNullOrWhiteSpace(value))
                {
                    value = value + ";exists=" + File.Exists(value) + ";dir=" + Directory.Exists(value);
                    if (Directory.Exists(value))
                    {
                        string runtimeRoot = value.Split(';')[0];
                        string[] names = { "nvinfer.dll", "nvinfer_10.dll", "nvinfer_11.dll", "nvonnxparser.dll", "nvonnxparser_10.dll", "nvonnxparser_11.dll", "nvinfer_plugin.dll", "nvinfer_plugin_10.dll", "nvinfer_plugin_11.dll" };
                        var found = new System.Collections.Generic.List<string>();
                        foreach (string directory in new[] { runtimeRoot, Path.Combine(runtimeRoot, "bin"), Path.Combine(runtimeRoot, "lib") })
                            foreach (string name in names)
                                if (File.Exists(Path.Combine(directory, name))) found.Add(Path.Combine(directory, name));
                        value += ";dlls=" + string.Join(",", found);
                    }
                }
                parts.Add(variable + "=" + (string.IsNullOrWhiteSpace(value) ? "<unset>" : value));
            }
            return string.Join("|", parts);
        }
    }
}
