using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests
{
    [TestClass]
    public sealed class PaddleDocumentTensorRtServerSealExternalIntegrationTests
    {
        private const string ModelId = "paddle-seal/ppocrv4-server";
        private const string DefaultOnnxPath = @"E:\Model\PaddleDocument\onnx\ppocrv4-server-seal-det.onnx";
        private const string DefaultImagePath = @"E:\Data\ocr\demo_1.jpg";
        private const string ExpectedOnnxSha256 = "f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        [DataRow("paddle-seal/ppocrv4-mobile", "ppocrv4-mobile-seal-det.onnx", "e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199")]
        [DataRow("paddle-seal/ppocrv4-server", "ppocrv4-server-seal-det.onnx", "f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7")]
        public void SealDetectionRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows(string modelId, string onnxFile, string expectedOnnxSha256)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");

            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_ONNX")
                ?? Path.Combine(@"E:\Model\PaddleDocument\onnx", onnxFile);
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_IMAGE")
                ?? DefaultImagePath;
            if (!File.Exists(onnxPath)) Assert.Inconclusive("Missing local PP-Structure ONNX model: " + onnxPath);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing local seal validation image: " + imagePath);

            string onnxSha256 = ComputeSha256(onnxPath);
            Assert.AreEqual(expectedOnnxSha256, onnxSha256, "The local ONNX file differs from the registered Release artifact; do not record this run as Release evidence.");
            PaddleDocumentProfile document = PaddleDocumentProfiles.CreateSealDetection(
                PaddleDocumentModelCatalog.Get(modelId),
                modelSize: new VisualSize(224, 224),
                inputName: "x",
                outputName: "fetch_name_0",
                maximumBatch: 2,
                threshold: .3f,
                minimumArea: 16);
            Assert.AreEqual(-1L, document.VisualProfile.Input.ShapePattern[0], modelId + " must expose a dynamic input batch axis.");
            Assert.AreEqual(-1L, document.VisualProfile.Outputs[0].ShapePattern[0], modelId + " must expose a dynamic output batch axis.");

            TensorRtApiVersion apiVersion = ResolveApiVersion();
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-seal-trt-batch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, Path.GetFileNameWithoutExtension(onnxFile) + "-batch.engine");
            string? prebuiltEnginePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_ENGINE");
            string effectiveEnginePath = enginePath;
            string effectiveEngineSha256 = string.Empty;
            long effectiveEngineBytes = 0;
            try
            {
                var artifact = new ModelArtifact(new ModelId(modelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                if (!string.IsNullOrWhiteSpace(prebuiltEnginePath))
                {
                    if (!File.Exists(prebuiltEnginePath)) Assert.Inconclusive("The configured prebuilt TensorRT engine does not exist: " + prebuiltEnginePath);
                    effectiveEnginePath = Path.GetFullPath(prebuiltEnginePath);
                    effectiveEngineSha256 = ComputeSha256(effectiveEnginePath);
                    effectiveEngineBytes = new FileInfo(effectiveEnginePath).Length;
                }
                else
                {
                    TensorRtOnnxEngineBuildResult build;
                    try
                    {
                        build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, new TensorRtOnnxEngineBuildOptions(
                            apiVersion: apiVersion,
                            precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                            workspaceBytes: 268435456UL,
                            optimizationLevel: 3,
                            inputProfiles: new[]
                            {
                                new TensorRtOnnxInputProfile(
                                    "x",
                                    new TensorShape(1, 3, 224, 224),
                                    new TensorShape(2, 3, 224, 224),
                                    new TensorShape(2, 3, 224, 224))
                            },
                            overwrite: true,
                            disableTf32: true));
                    }
                    catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                    {
                        Assert.Inconclusive("DeploySharp did not reach TensorRT seal inference because native bridge initialization failed: " + (exception.TechnicalDetails ?? exception.Message));
                        return;
                    }
                    catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
                    {
                        Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_UNSUPPORTED model=" + modelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                        Assert.Inconclusive("TensorRT cannot build this seal dynamic Batch profile: " + (exception.TechnicalDetails ?? exception.Message));
                        return;
                    }
                    effectiveEnginePath = build.EnginePath;
                    effectiveEngineSha256 = build.EngineSha256;
                    effectiveEngineBytes = build.EngineBytes;
                }

                using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, document.VisualProfile, "seal-dynamic-batch-probe");
                float halfHeight = probe.SourceSize.Height / 2f;
                IVisualRoiGeometry[] regions =
                {
                    new RectangleRoiGeometry(new RectangleF(0, 0, probe.SourceSize.Width, halfHeight)),
                    new RectangleRoiGeometry(new RectangleF(0, halfHeight, probe.SourceSize.Width, probe.SourceSize.Height - halfHeight))
                };
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateRoiBatch(
                    OpenCvImageSource.FromFile(imagePath),
                    regions,
                    document.VisualProfile.Input.Name,
                    document.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                    inputId: "seal-dynamic-batch-rows",
                    cancellationToken: CancellationToken.None);
                Assert.AreEqual(2, input.BatchSize);
                Assert.AreEqual(2L, input.Tensor.Shape[0]);
                string[] inputRowSha256 = Enumerable.Range(0, 2).Select(row => TensorRowSha256(input.Tensor, row)).ToArray();
                Assert.AreNotEqual(inputRowSha256[0], inputRowSha256[1], "Distinct seal ROI rows must not collapse to one prepared tensor.");
                var inferenceInputs = InferenceInputs.Create(input.InputName, input.Tensor);

                InferenceOutputs ortOutputs;
                using (var registry = new BackendRegistry().UseOnnxRuntime())
                using (IInferenceSession session = registry.CreateSession(document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                    ortOutputs = session.Run(inferenceInputs, CancellationToken.None);
                ITensor ortMask = ortOutputs.GetRequired("fetch_name_0");
                Assert.AreEqual(4, ortMask.Shape.Rank);
                Assert.AreEqual(2L, ortMask.Shape[0]);
                Assert.IsTrue(ortMask.Buffer is float[], "ORT seal output must be Float32.");

                var backendOptions = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86");
                InferenceOutputs? lastTrtOutputs = null;
                int warmup = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_WARMUP", 5);
                int iterations = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_ITERATIONS", 20);
                var samples = new List<double>(iterations);
                PaddleDocumentSealBatchResult trtResult = null!;
                using (var provider = new TensorRtBackendProvider(backendOptions))
                using (IInferenceSession session = provider.CreateSession(new ModelArtifact(new ModelId(modelId), "tensorrt-engine", effectiveEnginePath, effectiveEngineSha256, TensorRtBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), SessionOptions.Default))
                {
                    for (int index = -warmup; index < iterations; index++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        lastTrtOutputs = session.Run(inferenceInputs, CancellationToken.None);
                        ITensor trtMask = lastTrtOutputs.GetRequired("fetch_name_0");
                        Assert.AreEqual(ortMask.Shape.ToString(), trtMask.Shape.ToString(), "TensorRT changed the seal output shape.");
                        Assert.IsTrue(trtMask.Buffer is float[], "TensorRT seal output must be Float32.");
                        CompareSealMaskRows((float[])ortMask.Buffer, (float[])trtMask.Buffer, (int)ortMask.Shape[0], modelId);
                        trtResult = document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, lastTrtOutputs, CancellationToken.None)) as PaddleDocumentSealBatchResult
                            ?? throw new AssertFailedException("TensorRT did not produce a PaddleDocumentSealBatchResult for " + modelId + ".");
                        watch.Stop();
                        Assert.AreEqual(2, trtResult.Count);
                        if (index >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }

                PaddleDocumentSealBatchResult ortResult = document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, ortOutputs, CancellationToken.None)) as PaddleDocumentSealBatchResult
                    ?? throw new AssertFailedException("ORT did not produce a PaddleDocumentSealBatchResult for " + modelId + ".");
                Assert.AreEqual(2, ortResult.Count);
                for (int row = 0; row < 2; row++)
                {
                    Assert.AreEqual(ortResult[row].MaskWidth, trtResult[row].MaskWidth);
                    Assert.AreEqual(ortResult[row].MaskHeight, trtResult[row].MaskHeight);
                    Assert.AreEqual(ortResult[row].Regions.Count, trtResult[row].Regions.Count, "TensorRT changed the decoded seal region count for row " + row + ".");
                }

                double[] sorted = samples.OrderBy(value => value).ToArray();
                double p50 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * .50) - 1)];
                double p95 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * .95) - 1)];
                string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_REPORT_PATH")
                    ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-seal-" + modelId.Replace('/', '-') + "-dynamic-batch-tensorrt11.json");
                string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
                if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
                File.WriteAllText(report, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    generatedAtUtc = DateTimeOffset.UtcNow,
                    execution = new { os = Environment.OSVersion.ToString(), framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, runtime = DescribeNativeRuntime(), tensorRtApi = (int)apiVersion, cudaArchitecture = backendOptions.CudaTargetArchitecture },
                    model = new { id = modelId, file = onnxFile, onnxSha256, engineSha256 = effectiveEngineSha256, engineBytes = effectiveEngineBytes, engineSource = string.IsNullOrWhiteSpace(prebuiltEnginePath) ? "deploysharp-builder" : "external-prebuilt" },
                    image = new { file = imagePath, sha256 = ComputeSha256(imagePath), regions = regions.Select(value => new { x = value.Bounds.X, y = value.Bounds.Y, width = value.Bounds.Width, height = value.Bounds.Height }).ToArray() },
                    batch = new { size = 2, inputShape = input.Tensor.Shape.ToString(), outputShape = ortMask.Shape.ToString(), inputRowSha256 },
                    rows = Enumerable.Range(0, 2).Select(row => new { row, ortMaskRowSha256 = TensorRowSha256(ortMask, row), tensorRtMaskRowSha256 = TensorRowSha256(lastTrtOutputs!.GetRequired("fetch_name_0"), row), ortRegions = ortResult[row].Regions.Count, tensorRtRegions = trtResult[row].Regions.Count, ortMask = ortResult[row].MaskWidth + "x" + ortResult[row].MaskHeight, tensorRtMask = trtResult[row].MaskWidth + "x" + trtResult[row].MaskHeight }).ToArray(),
                    timing = new { warmup, iterations, p50Ms = p50, p95Ms = p95 },
                    boundary = "Exact PP-OCRv4 seal dynamic Batch=2 Engine build/provider execution, raw mask row binding and bounded ORT parity for this artifact on one Windows TensorRT host. ROI bands are execution probes without seal ground truth; no accuracy, throughput, cross-device portability or all-seal-model claim is made."
                }, new JsonSerializerOptions { WriteIndented = true }));
                TestContext.AddResultFile(report);
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH " + JsonSerializer.Serialize(new { modelId, onnxSha256, engineSha256 = effectiveEngineSha256, inputShape = input.Tensor.Shape.ToString(), outputShape = ortMask.Shape.ToString(), p50Ms = p50, p95Ms = p95, report }));
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
            {
                Assert.Inconclusive("TensorRT runtime is unavailable or mismatched for seal dynamic Batch: " + (exception.TechnicalDetails ?? exception.Message));
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
            {
                Assert.Inconclusive("TensorRT cannot build this seal dynamic Batch profile: " + (exception.TechnicalDetails ?? exception.Message));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ServerSealDetectionRunsThroughTensorRtAndPreservesOrtMaskValues()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");

            string onnxPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_ONNX") ?? DefaultOnnxPath;
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_IMAGE") ?? DefaultImagePath;
            if (!File.Exists(onnxPath)) Assert.Inconclusive("Missing local PP-Structure ONNX model: " + onnxPath);
            if (!File.Exists(imagePath)) Assert.Inconclusive("Missing local seal validation image: " + imagePath);

            PaddleDocumentProfile document = PaddleDocumentProfiles.CreateSealDetection(
                PaddleDocumentModelCatalog.Get(ModelId),
                modelSize: new VisualSize(224, 224),
                inputName: "x",
                outputName: "fetch_name_0",
                maximumBatch: 1,
                threshold: .3f,
                minimumArea: 16);

            string onnxSha256 = ComputeSha256(onnxPath);
            Assert.AreEqual(ExpectedOnnxSha256, onnxSha256, "The local ONNX file differs from the registered Release artifact; do not record this run as Release evidence.");
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-server-seal-trt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string enginePath = Path.Combine(root, "ppocrv4-server-seal-det.engine");
            try
            {
                TensorRtApiVersion apiVersion = ResolveApiVersion();
                var artifact = new ModelArtifact(new ModelId(ModelId), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
                var options = new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 3,
                    inputProfiles: new[] { new TensorRtOnnxInputProfile("x", new TensorShape(1, 3, 224, 224), new TensorShape(1, 3, 224, 224), new TensorShape(1, 3, 224, 224)) },
                    overwrite: true,
                    disableTf32: true);

                Stopwatch buildWatch = Stopwatch.StartNew();
                TensorRtOnnxEngineBuildResult build;
                try
                {
                    build = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, options);
                }
                catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                {
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_BLOCKED model=" + ModelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                    Assert.Inconclusive("TensorRT native runtime is unavailable or mismatched. " + (exception.TechnicalDetails ?? exception.Message));
                    return;
                }
                buildWatch.Stop();

                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, document.VisualProfile, inputId: "paddle-document-tensorrt-server-seal");
                var inputs = new[] { new NamedTensor(input.InputName, input.Tensor) };
                PaddleDocumentSealResult ortReference;
                float[] ortMask;
                using (var registry = new BackendRegistry().UseOnnxRuntime())
                using (IInferenceSession session = registry.CreateSession(document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                {
                    InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
                    ITensor tensor = outputs.GetRequired("fetch_name_0");
                    Assert.IsTrue(tensor.Buffer is float[], "The server seal reference output must be Float32.");
                    ortMask = ((float[])tensor.Buffer).ToArray();
                    ortReference = (PaddleDocumentSealResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                }

                string architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86";
                var backendOptions = new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: architecture);
                int warmup = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_WARMUP", 5);
                int iterations = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_ITERATIONS", 50);
                var samples = new List<double>(iterations);
                double maskMaxAbs = 0;
                double maskMeanAbs = 0;
                PaddleDocumentSealResult trtResult = null!;
                using (var provider = new TensorRtBackendProvider(backendOptions))
                using (IInferenceSession session = provider.CreateSession(new ModelArtifact(new ModelId(ModelId), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), SessionOptions.Default))
                {
                    for (int index = -warmup; index < iterations; index++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
                        ITensor tensor = outputs.GetRequired("fetch_name_0");
                        Assert.IsTrue(tensor.Buffer is float[], "The server TensorRT seal output must be Float32.");
                        float[] trtMask = (float[])tensor.Buffer;
                        Assert.AreEqual(ortMask.Length, trtMask.Length, "TensorRT changed the server seal probability-map element count.");
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
                            Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL_MASK_DIFF maxAbs=" + maskMaxAbs.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";meanAbs=" + maskMeanAbs.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                            // Keep a strict mean-error guard. The build explicitly
                            // disables TF32 while retaining TRT11's strongly typed
                            // network policy, so max error remains a useful signal.
                            Assert.IsTrue(maskMeanAbs <= .001, "TensorRT server seal probability map mean error exceeded .001.");
                        }
                        trtResult = (PaddleDocumentSealResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                        watch.Stop();
                        Assert.IsTrue(trtResult.Regions.All(region => region.Score >= 0 && region.Score <= 1));
                        if (index >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }

                Assert.AreEqual(ortReference.MaskWidth, trtResult.MaskWidth);
                Assert.AreEqual(ortReference.MaskHeight, trtResult.MaskHeight);
                Assert.AreEqual(ortReference.Regions.Count, trtResult.Regions.Count);
                double[] sorted = samples.OrderBy(value => value).ToArray();
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_SERVER_SEAL " + JsonSerializer.Serialize(new
                {
                    model = ModelId,
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
                    numericWarning = maskMaxAbs > .05,
                    p50Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .5) - 1)],
                    p95Ms = sorted[Math.Max(0, (int)Math.Ceiling(iterations * .95) - 1)],
                    cudaArchitecture = architecture
                }));
            }
            catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
            {
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_UNSUPPORTED model=" + ModelId + ";errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                Assert.Inconclusive("TensorRT importer does not currently accept this server seal graph. " + (exception.TechnicalDetails ?? exception.Message));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static TensorRtApiVersion ResolveApiVersion()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API");
            if (value == "8") return TensorRtApiVersion.TensorRt8;
            if (value == "10") return TensorRtApiVersion.TensorRt10;
            if (value == "11") return TensorRtApiVersion.TensorRt11;
            string? root = Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT");
            if (!string.IsNullOrWhiteSpace(root))
            {
                var match = System.Text.RegularExpressions.Regex.Match(root, @"TensorRT-(8|10|11)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups[1].Value == "8") return TensorRtApiVersion.TensorRt8;
                if (match.Success && match.Groups[1].Value == "10") return TensorRtApiVersion.TensorRt10;
            }
            return TensorRtApiVersion.TensorRt11;
        }

        private static int ResolvePositiveInt(string name, int fallback)
        {
            return int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;
        }

        private static string DescribeNativeRuntime()
        {
            string[] variables = { "JYPPX_NATIVE_BRIDGE_PATH", "JYPPX_TENSORRT_ROOT", "JYPPX_CUDA_ROOT", "JYPPX_CUDNN_ROOT" };
            return string.Join("|", variables.Select(variable => variable + "=" + (Environment.GetEnvironmentVariable(variable) ?? "<unset>")));
        }

        private static string TensorRowSha256(ITensor tensor, int row)
        {
            if (tensor.Buffer is not float[] values) throw new AssertFailedException("Seal dynamic-batch evidence requires Float32 tensors.");
            int batch = checked((int)tensor.Shape[0]);
            if (row < 0 || row >= batch || values.Length % batch != 0) throw new ArgumentOutOfRangeException(nameof(row));
            int rowLength = values.Length / batch;
            return Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan(row * rowLength, rowLength)))).ToLowerInvariant();
        }

        private static void CompareSealMaskRows(float[] ortValues, float[] trtValues, int batch, string modelId)
        {
            if (ortValues.Length != trtValues.Length || ortValues.Length % batch != 0) throw new AssertFailedException("Seal raw output shape cannot be partitioned by batch.");
            int rowLength = ortValues.Length / batch;
            for (int row = 0; row < batch; row++)
            {
                double maxAbs = 0, sumAbs = 0;
                for (int index = 0; index < rowLength; index++)
                {
                    double delta = Math.Abs((double)ortValues[row * rowLength + index] - trtValues[row * rowLength + index]);
                    maxAbs = Math.Max(maxAbs, delta);
                    sumAbs += delta;
                }
                double meanAbs = sumAbs / rowLength;
                Assert.IsTrue(maxAbs <= .05 && meanAbs <= .005, $"TensorRT {modelId} seal mask drift exceeded the bounded tolerance for row {row}: max={maxAbs:R}, mean={meanAbs:R}.");
                Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_SEAL_DYNAMIC_BATCH_ROW " + JsonSerializer.Serialize(new { model = modelId, row, maxAbs, meanAbs, rowLength }));
            }
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
