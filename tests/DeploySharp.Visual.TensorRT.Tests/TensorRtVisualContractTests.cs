using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.TensorRT;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests
{
    [TestClass]
    public sealed class TensorRtVisualContractTests
    {
        [TestMethod]
        public void StaticBatchOneFloat32NchwContractIsAccepted()
        {
            VisualModelProfile profile = Profile(new TensorShape(1, 3, 224, 224));
            var options = new OpenCvPreprocessOptions(new VisualSize(224, 224), OpenCvResizeMode.Letterbox);

            TensorRtVisualContracts.ValidatePreprocessing(profile, options);
            TensorRtVisualContracts.ValidateMetadata(Metadata(profile), profile);
        }

        [TestMethod]
        public void DynamicOrBatchTwoPreprocessingIsRejectedBeforeNativeStartup()
        {
            VisualModelProfile dynamicProfile = Profile(new TensorShape(1, 3, -1, 224));
            var options = new OpenCvPreprocessOptions(new VisualSize(224, 224));
            Assert.ThrowsExactly<NotSupportedException>(() => TensorRtVisualContracts.ValidatePreprocessing(dynamicProfile, options));

            VisualModelProfile batchProfile = new VisualModelProfile(
                "trt.visual.batch",
                new ModelId("visual/test"),
                VisualTaskId.ImageClassification,
                "test",
                "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(2, 3, 224, 224), VisualTensorLayout.Nchw, 2, 2),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(2, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                new Decoder());
            Assert.ThrowsExactly<NotSupportedException>(() => TensorRtVisualContracts.ValidatePreprocessing(batchProfile, options));
        }

        [TestMethod]
        public void MetadataOutputNamesTypesAndShapesAreValidated()
        {
            VisualModelProfile profile = Profile(new TensorShape(1, 3, 224, 224));
            ModelMetadata wrongName = new ModelMetadata(profile.ModelId, "tensorrt-engine", MetadataInput(profile), new[] { new TensorDescriptor("wrong", TensorElementType.Float32, new TensorShape(1, 3)) });
            Assert.ThrowsExactly<InvalidOperationException>(() => TensorRtVisualContracts.ValidateMetadata(wrongName, profile));

            ModelMetadata dynamicOutput = new ModelMetadata(profile.ModelId, "tensorrt-engine", MetadataInput(profile), new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, -1)) });
            Assert.ThrowsExactly<InvalidOperationException>(() => TensorRtVisualContracts.ValidateMetadata(dynamicOutput, profile));
        }

        [TestMethod]
        public async Task ContractValidationIsDeterministicAcrossConcurrentCalls()
        {
            VisualModelProfile profile = Profile(new TensorShape(1, 3, 224, 224));
            ModelMetadata metadata = Metadata(profile);
            var options = new OpenCvPreprocessOptions(new VisualSize(224, 224));
            Task[] calls = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                TensorRtVisualContracts.ValidatePreprocessing(profile, options);
                TensorRtVisualContracts.ValidateMetadata(metadata, profile);
            })).ToArray();
            await Task.WhenAll(calls);
        }

        [TestMethod]
        public async Task PipelineGateSerializesSharedDeviceBuffers()
        {
            var gate = new TensorRtVisualExecutionGate();
            int active = 0;
            int maximum = 0;
            Task[] calls = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                lock (gate.SyncRoot)
                {
                    int current = Interlocked.Increment(ref active);
                    SpinWait spin = new SpinWait();
                    while (spin.NextSpinWillYield == false) spin.SpinOnce();
                    if (current > maximum) Interlocked.Exchange(ref maximum, current);
                    Interlocked.Decrement(ref active);
                }
            })).ToArray();
            await Task.WhenAll(calls);
            Assert.AreEqual(1, maximum, "One TensorRT visual pipeline owns one set of device buffers and must serialize calls.");
        }

        private static ModelMetadata Metadata(VisualModelProfile profile) => new ModelMetadata(profile.ModelId, "tensorrt-engine", MetadataInput(profile), new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });

        private static IReadOnlyList<TensorDescriptor> MetadataInput(VisualModelProfile profile) => new[] { new TensorDescriptor(profile.Input.Name, profile.Input.ElementType, profile.Input.ShapePattern) };

        private static VisualModelProfile Profile(TensorShape shape) => new VisualModelProfile(
            "trt.visual.test",
            new ModelId("visual/test"),
            VisualTaskId.ImageClassification,
            "test",
            "onnx",
            new VisualInputBinding("images", TensorElementType.Float32, shape, VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
            new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
            new Decoder());

        private sealed class Decoder : IVisualDecoder
        {
            public VisualTaskId Task => VisualTaskId.ImageClassification;
            public object Decode(VisualDecodeContext context) => new object();
        }
    }
}
