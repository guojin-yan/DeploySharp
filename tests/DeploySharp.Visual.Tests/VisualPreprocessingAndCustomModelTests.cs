using System;
using System.Threading;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class VisualPreprocessingAndCustomModelTests
    {
        [TestMethod]
        public void PreprocessingOptionsAreComposableAndValidateAgainstProfile()
        {
            VisualPreprocessingOptions options = new VisualPreprocessingOptions(
                new VisualSize(640, 640),
                VisualResizeMode.Letterbox,
                VisualColorOrder.Rgb,
                VisualNormalizationOptions.ImageNet,
                VisualTensorLayout.Nchw,
                1,
                paddingColor: new VisualRgbColor(114, 114, 114))
                .WithResizeMode(VisualResizeMode.Resize)
                .WithNormalization(VisualNormalizationOptions.MeanStandardDeviation(new[] { 0f }, new[] { 127.5f }))
                .WithScaleUp(false);

            VisualModelProfile profile = new VisualModelProfile(
                "tests/preprocess.v1",
                new ModelId("tests/preprocess"),
                VisualTaskId.ImageClassification,
                "1",
                "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 2)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one") },
                new ClassificationDecoder("scores"),
                preprocessing: options);

            Assert.AreEqual(VisualResizeMode.Resize, profile.Preprocessing!.ResizeMode);
            Assert.AreEqual(VisualNormalizationMode.MeanStandardDeviation, profile.Preprocessing.Normalization.Mode);
            Assert.IsFalse(profile.Preprocessing.ScaleUp);
        }

        [TestMethod]
        public void CustomModelBuilderCreatesTypedDecoderAndPreprocessorDefinition()
        {
            var task = new VisualTaskId("custom-classification");
            var definition = new VisualModelDefinitionBuilder<string, CustomResult>("custom/model.v1", new ModelId("custom/model"), task, "1", "onnx")
                .WithInput("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw)
                .AddOutput("scores", TensorElementType.Float32, new TensorShape(1, 2))
                .WithPreprocessing(new VisualPreprocessingOptions(new VisualSize(2, 2), VisualResizeMode.Resize, VisualColorOrder.Rgb, VisualNormalizationOptions.Scale(255f)))
                .WithInputPreprocessor((_, profile, _) =>
                    new PreparedVisualInput(profile.Input.Name, new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), new VisualSize(2, 2), new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(2, 2), new VisualSize(2, 2))))
                .WithDecoder(context => new CustomResult(((float[])context.GetOutput<float>("scores").Buffer)[1].ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Build();

            Assert.AreEqual(task, definition.Profile.Task);
            var prepared = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), new VisualSize(2, 2), new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(2, 2), new VisualSize(2, 2)));
            try
            {
                object decoded = definition.Profile.Decoder.Decode(new VisualDecodeContext(
                    prepared,
                    definition.Profile,
                    InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 2), new[] { 0f, 1f })),
                    CancellationToken.None));
                Assert.AreEqual("1", ((CustomResult)decoded).Label);
                VisualException mismatch = Assert.ThrowsExactly<VisualException>(() =>
                    new VisualDecodeContext(prepared, definition.Profile, InferenceOutputs.Create("scores", new Tensor<long>(new TensorShape(1, 2), new long[2])), CancellationToken.None)
                        .GetOutput<float>("scores"));
                Assert.AreEqual(VisualErrorCodes.TensorInvalid, mismatch.ErrorCode);
            }
            finally
            {
                prepared.Dispose();
            }
        }

        [TestMethod]
        public void BuiltInAnomalyAndRmbgProfilesAcceptValidatedOverrides()
        {
            var size = new VisualSize(256, 256);
            var preprocessing = new VisualPreprocessingOptions(
                size,
                VisualResizeMode.CenterCrop,
                VisualColorOrder.Rgb,
                VisualNormalizationOptions.ImageNet,
                VisualTensorLayout.Nchw);
            AnomalibProfile anomaly = AnomalibProfiles.CreatePadim(
                new ModelId("tests/custom-padim"),
                new AnomalibArtifactContract(14, new string('a', 64), "commit", "exporter"),
                size,
                preprocessing: preprocessing);
            BriaRmbgProfile rmbg = BriaRmbgProfiles.CreateRmbg14(
                new ModelId("tests/custom-rmbg"),
                new BriaRmbgProfileOptions(17, size, "input", "output", new string('b', 64), "commit", "exporter", "license", preprocessing: preprocessing));

            Assert.AreEqual(VisualResizeMode.CenterCrop, anomaly.VisualProfile.Preprocessing!.ResizeMode);
            Assert.AreSame(preprocessing, rmbg.VisualProfile.Preprocessing);
        }

        private sealed class CustomResult
        {
            public CustomResult(string label) { Label = label; }
            public string Label { get; }
        }
    }
}
