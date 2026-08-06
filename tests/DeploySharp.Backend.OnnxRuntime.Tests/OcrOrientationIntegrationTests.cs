using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class OcrOrientationIntegrationTests
    {
        private static string ModelPath => Path.Combine(AppContext.BaseDirectory, "fixtures", "text-orientation.onnx");

        [TestMethod]
        public void RealCpuOnnxMapsAllFourExplicitOrientations()
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var artifact = new ModelArtifact(new ModelId("tests/text-orientation"), "onnx", ModelPath, preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var profiles = new VisualProfileRegistry();
            profiles.Register(Profile()); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new OcrOrientationPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.TextOrientationClassification), request);
            var values = new[]
            {
                new[] { 9f, 1f, 2f, 3f },
                new[] { 2f, 9f, 3f, 1f },
                new[] { 1f, 3f, 9f, 2f },
                new[] { 3f, 2f, 1f, 9f }
            };
            var expected = new[] { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Clockwise90, TextOrientation.Degrees180 };
            for (int index = 0; index < values.Length; index++)
            {
                using PreparedVisualInput input = Input(values[index]);
                OcrOrientationResult result = pipeline.Run(input);
                Assert.AreEqual(expected[index], result.AcceptedOrientation);
                Assert.AreEqual(64, result.CanonicalSha256.Length);
                Assert.AreEqual(OnnxRuntimeBackendProvider.BackendId, result.BackendId);
            }
        }

        [TestMethod]
        public void PreCancelledOrientationDoesNotPoisonSession()
        {
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var artifact = new ModelArtifact(new ModelId("tests/text-orientation"), "onnx", ModelPath, preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            var profiles = new VisualProfileRegistry(); profiles.Register(Profile()); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new OcrOrientationPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.TextOrientationClassification), request);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            using PreparedVisualInput input = Input(new[] { 9f, 1f, 2f, 3f });
            Assert.ThrowsExactly<VisualException>(() => pipeline.Run(input, cancellationToken: cancelled.Token));
            Assert.AreEqual(TextOrientation.Degrees0, pipeline.Run(input).AcceptedOrientation);
        }

        private static VisualModelProfile Profile()
        {
            var decoder = new OcrOrientationDecoder(new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.CounterClockwise90, TextOrientation.Clockwise90, TextOrientation.Degrees180 }));
            return new VisualModelProfile("tests/text-orientation.v1", new ModelId("tests/text-orientation"), VisualTaskId.TextOrientationClassification, "1.0", "onnx", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 1, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("orientation_scores", TensorElementType.Float32, new TensorShape(1, 4)) }, Array.Empty<VisualLabel>(), decoder);
        }

        private static PreparedVisualInput Input(float[] values)
        {
            var size = new VisualSize(2, 2);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 1, 2, 2), values), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }
    }
}
