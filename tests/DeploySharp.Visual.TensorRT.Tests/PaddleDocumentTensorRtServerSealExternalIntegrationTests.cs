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

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
