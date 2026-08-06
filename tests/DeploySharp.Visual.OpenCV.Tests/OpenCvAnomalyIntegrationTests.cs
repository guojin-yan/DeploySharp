using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvAnomalyIntegrationTests
    {
        private static readonly ModelId ModelId = new ModelId("tests/opencv-anomaly-detection");
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);
        private static string Onnx(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "onnx", name);
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void RealOddPngOpenCvAndOnnxRuntimeExecuteAnomalyPipeline()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using AnomalyPipeline pipeline = CreatePipeline(registry);
            var preprocess = new OpenCvPreprocessOptions(
                new VisualSize(5,3), OpenCvResizeMode.Resize, VisualColorOrder.Rgb,
                standardDeviations: new[] { 255f,255f,255f }, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.Float32);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(Fixture("anomaly.png"), "images", preprocess);

            float[] pixels = ((Tensor<float>)input.Tensor).ToArray();
            Assert.AreEqual(20f / 255f, pixels[0], .000001f);
            Assert.AreEqual(15f / 255f, pixels[15], .000001f);
            Assert.AreEqual(200f / 255f, pixels[30], .000001f);
            AnomalyDetectionResult result = pipeline.Run(input);

            Assert.AreEqual(.875f, result.ImageScore, .000001f);
            CollectionAssert.AreEqual(new byte[] { 0,0,0,0,0, 0,1,1,1,1, 1,0,0,1,1 }, result.Mask.ToArray());
            Assert.AreEqual("f418bc5e06bb64863b38860375335aa9fdde1c6cd706ac3776457dbf53dbf7da", result.ComputeSha256());
        }

        [TestMethod]
        public void ByteInputGrayOddResizeCancellationAndDisposalRemainBackendNeutral()
        {
            byte[] encoded = File.ReadAllBytes(Fixture("anomaly.png"));
            var options = new OpenCvPreprocessOptions(new VisualSize(7,5), OpenCvResizeMode.Letterbox, VisualColorOrder.Gray, layout: VisualTensorLayout.Nhwc, outputType: OpenCvOutputType.UInt8);
            using PreparedVisualInput gray = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromBytes(encoded), "images", options);
            Assert.AreEqual(new TensorShape(1,5,7,1), gray.Tensor.Shape);
            Assert.AreEqual(new VisualSize(5,3), gray.SourceSize);
            Assert.AreEqual(new VisualSize(7,5), gray.ModelSize);
            Assert.IsTrue(((Tensor<byte>)gray.Tensor).ToArray().Any(value => value != 0));

            using var cancelled = new System.Threading.CancellationTokenSource();
            cancelled.Cancel();
            OpenCvVisualException exception = Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromBytes(encoded), "images", options, cancellationToken: cancelled.Token));
            Assert.AreEqual(OpenCvErrorCodes.Cancelled, exception.ErrorCode);
            gray.Dispose();
            gray.Dispose();
        }

        [TestMethod]
        public void PerformanceEntryRecordsPreprocessExecutePostprocessEndToEndP50P95AndAllocation()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            using AnomalyPipeline pipeline = CreatePipeline(registry);
            var preprocess = new OpenCvPreprocessOptions(new VisualSize(5,3), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, standardDeviations: new[] { 255f }, outputType: OpenCvOutputType.Float32);
            using (PreparedVisualInput warmup = new OpenCvVisualInputFactory().CreateFromFile(Fixture("anomaly.png"), "images", preprocess))
            {
                pipeline.Run(warmup);
            }
            var preprocessing = new List<TimeSpan>();
            var inference = new List<TimeSpan>();
            var postprocessing = new List<TimeSpan>();
            var endToEnd = new List<TimeSpan>();
            string? golden = null;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 12; iteration++)
            {
                var totalWatch = Stopwatch.StartNew();
                var preprocessWatch = Stopwatch.StartNew();
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(Fixture("anomaly.png"), "images", preprocess);
                preprocessWatch.Stop();
                AnomalyDetectionResult result = pipeline.Run(input);
                totalWatch.Stop();
                golden ??= result.ComputeSha256();
                Assert.AreEqual(golden, result.ComputeSha256());
                preprocessing.Add(preprocessWatch.Elapsed);
                inference.Add(result.Timing.Inference);
                postprocessing.Add(result.Timing.Postprocessing);
                endToEnd.Add(totalWatch.Elapsed);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext?.WriteLine(
                $"ANOMALY_E2E_PERF iterations=12 preprocessP50Ms={Percentile(preprocessing,.50).TotalMilliseconds:F3} preprocessP95Ms={Percentile(preprocessing,.95).TotalMilliseconds:F3} " +
                $"executeP50Ms={Percentile(inference,.50).TotalMilliseconds:F3} executeP95Ms={Percentile(inference,.95).TotalMilliseconds:F3} " +
                $"postprocessP50Ms={Percentile(postprocessing,.50).TotalMilliseconds:F3} postprocessP95Ms={Percentile(postprocessing,.95).TotalMilliseconds:F3} " +
                $"endToEndP50Ms={Percentile(endToEnd,.50).TotalMilliseconds:F3} endToEndP95Ms={Percentile(endToEnd,.95).TotalMilliseconds:F3} " +
                $"pixels=15 allocatedBytes={allocated} sha256={golden}");
            Assert.IsTrue(Percentile(endToEnd, .95) >= Percentile(endToEnd, .50));
        }

        private static AnomalyPipeline CreatePipeline(BackendRegistry registry)
        {
            var profiles = new VisualProfileRegistry(); profiles.Register(Profile()); profiles.Freeze();
            var artifact = new ModelArtifact(ModelId, "onnx", Onnx("anomaly-detection.onnx"), preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            return new AnomalyPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.AnomalyDetection), request);
        }

        private static VisualModelProfile Profile()
        {
            var decoder = new AnomalyDecoder(
                new AnomalyMapSchema("image_score", "anomaly_map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 2),
                new AnomalyDecoderOptions(normalization: AnomalyNormalizationMode.FixedRange, threshold: .6f, channelAggregation: AnomalyChannelAggregation.Maximum));
            return new VisualModelProfile(
                "tests/opencv-anomaly.v1", ModelId, VisualTaskId.AnomalyDetection, "1.0", "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,3,5), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("image_score", TensorElementType.Float32, new TensorShape(1)), new VisualOutputBinding("anomaly_map", TensorElementType.Float32, new TensorShape(1,2,3,5)) },
                Array.Empty<VisualLabel>(), decoder);
        }

        private static TimeSpan Percentile(IReadOnlyList<TimeSpan> values, double percentile)
        {
            long[] ordered = values.Select(value => value.Ticks).OrderBy(value => value).ToArray();
            int index = Math.Max(0, Math.Min(ordered.Length - 1, (int)Math.Ceiling(percentile * ordered.Length) - 1));
            return TimeSpan.FromTicks(ordered[index]);
        }
    }
}
