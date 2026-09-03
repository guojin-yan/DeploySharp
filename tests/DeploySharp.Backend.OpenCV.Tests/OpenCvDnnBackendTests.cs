using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Onnx;

namespace DeploySharp.Backend.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvDnnBackendTests
    {
        private static readonly ModelId Model = new ModelId("fixture/opencv-classification");
        private const string Sha256 = "05a885298cca6e04b83732a46ff340f48203cc62e5fa89af74fe3eeab259de2a";

        [TestMethod]
        public void RealOpenCvDnnMatchesPinnedOnnxGolden()
        {
            string path = Fixture();
            ModelArtifact artifact = new ModelArtifact(Model, "onnx", path, Sha256);
            OpenCvDnnModelContract contract = Contract();
            using var openCvProvider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession openCv = openCvProvider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
            InferenceInputs inputs = Inputs();
            float[] expected = { 2.5f, 6.5f, 10.5f };
            float[] actual = (float[])openCv.Run(inputs, CancellationToken.None).Single().Tensor.Buffer;
            Assert.AreEqual(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++) Assert.AreEqual(expected[index], actual[index], 0.00001f, "Output drift at index " + index);
            CollectionAssert.AreEqual(new long[] { 1, 3 }, openCv.Metadata.Outputs.Single().Shape.ToArray());
        }

        [TestMethod]
        public async Task MultipleConcurrencyCreatesIndependentOpenCvNetsWithStableResults()
        {
            var artifact = new ModelArtifact(Model, "onnx", Fixture(), Sha256);
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(Contract(), enableFusion: false, enableWinograd: false));
            using IInferenceSession openCv = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), new SessionOptions(maxConcurrency: 2));
            Task<float[]>[] calls = Enumerable.Range(0, 4).Select(_ => Task.Run(() => (float[])openCv.Run(Inputs(), CancellationToken.None).Single().Tensor.Buffer)).ToArray();
            float[][] results = await Task.WhenAll(calls);
            float[] expected = { 2.5f, 6.5f, 10.5f };
            foreach (float[] actual in results) CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task AsyncPoolCallsReturnWithoutSerializingIndependentNets()
        {
            var artifact = new ModelArtifact(Model, "onnx", Fixture(), Sha256);
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(Contract(), enableFusion: false, enableWinograd: false));
            using IInferenceSession openCv = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), new SessionOptions(maxConcurrency: 2));
            Task<InferenceOutputs>[] calls = Enumerable.Range(0, 4).Select(_ => openCv.RunAsync(Inputs(), CancellationToken.None)).ToArray();
            InferenceOutputs[] results = await Task.WhenAll(calls);
            foreach (InferenceOutputs result in results) CollectionAssert.AreEqual(new[] { 2.5f, 6.5f, 10.5f }, (float[])result.Single().Tensor.Buffer);
        }

        [TestMethod]
        public void ContractAndArtifactDriftFailClosed()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(Model, new[] { new TensorDescriptor("images", TensorElementType.Int64, new TensorShape(1, 3, 2, 2)) }, Contract().Outputs));
            OpenCvDnnModelContract dynamicImageContract = new OpenCvDnnModelContract(Model, new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2)) }, Contract().Outputs);
            Assert.IsTrue(dynamicImageContract.Inputs.Single().Shape.IsDynamic);
            var bad = new ModelArtifact(Model, "onnx", Fixture(), new string('a', 64));
            Assert.AreEqual(DeploySharpErrorCodes.ModelArtifactInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => OpenCvDnnModelArtifactValidator.Validate(bad)).ErrorCode);
        }

        [TestMethod]
        public void DynamicOnnxInputsAreSpecializedWithoutChangingTheArtifact()
        {
            byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "dynamic-identity.onnx"));
            var inputs = new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(3, 2)) };

            byte[] specialized = OpenCvDnnOnnxInputSpecializer.Specialize(original, inputs, out bool changed);
            Assert.IsTrue(changed);
            Assert.AreNotSame(original, specialized);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "dynamic-identity.onnx")));

            byte[] unchanged = OpenCvDnnOnnxInputSpecializer.Specialize(specialized, inputs, out bool changedAgain);
            Assert.IsFalse(changedAgain);
            Assert.AreSame(specialized, unchanged);
            Assert.ThrowsExactly<InvalidDataException>(() => OpenCvDnnOnnxInputSpecializer.Specialize(specialized, new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(4, 2)) }, out _));
        }

        [TestMethod]
        public void ConstantInputOperatorFormsAreNormalizedForOpenCvImporter()
        {
            byte[] source = CreateUnsqueezeFixture();
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(source, out bool changed);
            Assert.IsTrue(changed);
            ModelProto model = ModelProto.Parser.ParseFrom(normalized);
            NodeProto node = model.Graph.Node.Single(value => value.OpType == "Unsqueeze");
            Assert.AreEqual(1, node.Input.Count);
            AttributeProto axes = node.Attribute.Single(value => value.Name == "axes");
            CollectionAssert.AreEqual(new long[] { 1 }, axes.Ints.ToArray());
        }

        [TestMethod]
        public void ShapeOnlyConcatInputsAreNormalizedToInt32()
        {
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(CreateShapeConcatFixture(), out bool changed);
            Assert.IsTrue(changed);
            ModelProto model = ModelProto.Parser.ParseFrom(normalized);
            NodeProto concat = model.Graph.Node.Single(node => node.OpType == "Concat");
            Assert.AreEqual(2, concat.Input.Count);
            foreach (string input in concat.Input)
            {
                NodeProto cast = model.Graph.Node.Single(node => node.OpType == "Cast" && node.Output.Contains(input));
                Assert.AreEqual((long)TensorProto.Types.DataType.Int32, cast.Attribute.Single(attribute => attribute.Name == "to").I);
            }
        }

        [TestMethod]
        public void DynamicTransformerGraphIsMarkedNativeImporterHazard()
        {
            var graph = new GraphProto { Name = "dynamic-transformer" };
            graph.Input.Add(DynamicBatchValueInfo("input", TensorProto.Types.DataType.Float, 3, 8, 8));
            graph.Node.Add(Node("Shape", new[] { "input" }, new[] { "shape" }));
            NodeProto split = Node("Split", new[] { "shape" }, new[] { "shape_part" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 });
            split.Name = "/transformer/Split";
            graph.Node.Add(split);
            var model = new ModelProto { IrVersion = 7, Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });

            Assert.IsTrue(OpenCvDnnOnnxCompatibilityPasses.IsNativeImporterHazard(model.ToByteArray()));
        }

        [TestMethod]
        public void IntegerShapeComparisonsNormalizeAllInt64Inputs()
        {
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(CreateMixedIntegerShapeComparisonFixture(), out bool changed);
            Assert.IsTrue(changed);
            ModelProto model = ModelProto.Parser.ParseFrom(normalized);
            NodeProto equal = model.Graph.Node.Single(node => node.OpType == "Equal");
            Assert.AreEqual(2, equal.Input.Count);
            foreach (string input in equal.Input) Assert.IsTrue(model.Graph.Node.Any(node => node.OpType == "Cast" && node.Output.Contains(input)));
        }

        [TestMethod]
        public void IntegerShapeWhereBranchesNormalizeToInt64WithoutCastingCondition()
        {
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(CreateMixedIntegerShapeWhereFixture(), out bool changed);
            Assert.IsTrue(changed);
            ModelProto model = ModelProto.Parser.ParseFrom(normalized);
            NodeProto where = model.Graph.Node.Single(node => node.OpType == "Where");
            Assert.AreEqual("condition", where.Input[0]);
            foreach (string input in where.Input.Skip(1))
            {
                NodeProto cast = model.Graph.Node.Single(node => node.OpType == "Cast" && node.Output.Contains(input));
                Assert.AreEqual((long)TensorProto.Types.DataType.Int64, cast.Attribute.Single(attribute => attribute.Name == "to").I);
            }
        }

        [TestMethod]
        public void ScalarConstantExpandIsRewrittenToConstantOfShape()
        {
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(CreateScalarConstantExpandFixture(), out bool changed);
            Assert.IsTrue(changed);
            ModelProto model = ModelProto.Parser.ParseFrom(normalized);
            NodeProto fill = model.Graph.Node.Single(node => node.Output.Contains("output"));
            Assert.AreEqual("ConstantOfShape", fill.OpType);
            CollectionAssert.AreEqual(new[] { "shape" }, fill.Input.ToArray());
            TensorProto value = fill.Attribute.Single(attribute => attribute.Name == "value").T;
            CollectionAssert.AreEqual(new long[] { 1 }, value.Dims.ToArray());
            CollectionAssert.AreEqual(new[] { 2.5f }, value.FloatData.ToArray());
        }

        [TestMethod]
        public void CommonConstantOperatorInputsAreNormalizedWithoutChangingSourceGraph()
        {
            byte[] source = CreateConstantOperatorFixture();
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(source, out bool changed);
            Assert.IsTrue(changed);
            ModelProto model = ModelProto.Parser.ParseFrom(normalized);

            NodeProto reduce = model.Graph.Node.Single(value => value.OpType == "ReduceSum");
            Assert.AreEqual(1, reduce.Input.Count);
            CollectionAssert.AreEqual(new long[] { 1 }, reduce.Attribute.Single(value => value.Name == "axes").Ints.ToArray());
            Assert.IsFalse(reduce.Attribute.Any(value => value.Name == "keepdims"));
            NodeProto reduceNoKeepDims = model.Graph.Node.Single(value => value.OpType == "ReduceMax");
            Assert.IsFalse(reduceNoKeepDims.Attribute.Any(value => value.Name == "keepdims"));
            Assert.AreNotEqual("max_reduced", reduceNoKeepDims.Output[0]);
            NodeProto squeeze = model.Graph.Node.Single(value => value.OpType == "Squeeze" && value.Output.Contains("max_reduced"));
            CollectionAssert.AreEqual(new long[] { 1 }, squeeze.Attribute.Single(value => value.Name == "axes").Ints.ToArray());

            NodeProto topK = model.Graph.Node.Single(value => value.OpType == "TopK");
            Assert.AreEqual(1, topK.Input.Count);
            Assert.AreEqual(2, topK.Attribute.Single(value => value.Name == "k").I);

            NodeProto slice = model.Graph.Node.Single(value => value.OpType == "Slice" && value.Output.Contains("output"));
            Assert.AreEqual(1, slice.Input.Count);
            CollectionAssert.AreEqual(new long[] { 0 }, slice.Attribute.Single(value => value.Name == "starts").Ints.ToArray());
            CollectionAssert.AreEqual(new long[] { 2 }, slice.Attribute.Single(value => value.Name == "ends").Ints.ToArray());
            CollectionAssert.AreEqual(new long[] { 1 }, slice.Attribute.Single(value => value.Name == "axes").Ints.ToArray());
            CollectionAssert.AreEqual(new long[] { 1 }, slice.Attribute.Single(value => value.Name == "steps").Ints.ToArray());

            NodeProto pool = model.Graph.Node.Single(value => value.OpType == "MaxPool");
            Assert.IsFalse(pool.Attribute.Any(value => value.Name == "ceil_mode"));
            Assert.IsFalse(pool.Attribute.Any(value => value.Name == "storage_order"));
            NodeProto averagePool = model.Graph.Node.Single(value => value.OpType == "AveragePool");
            Assert.IsFalse(averagePool.Attribute.Any(value => value.Name == "ceil_mode"));
            Assert.IsFalse(averagePool.Attribute.Any(value => value.Name == "count_include_pad"));
            NodeProto concat = model.Graph.Node.Single(value => value.OpType == "Concat");
            Assert.AreEqual(AttributeProto.Types.AttributeType.Ints, concat.Attribute.Single(value => value.Name == "axis").Type);
            CollectionAssert.AreEqual(new long[] { 0 }, concat.Attribute.Single(value => value.Name == "axis").Ints.ToArray());
            NodeProto gather = model.Graph.Node.Single(value => value.OpType == "Gather");
            Assert.AreEqual(AttributeProto.Types.AttributeType.Ints, gather.Attribute.Single(value => value.Name == "axis").Type);
            CollectionAssert.AreEqual(new long[] { 0 }, gather.Attribute.Single(value => value.Name == "axis").Ints.ToArray());
            NodeProto split = model.Graph.Node.Single(value => value.OpType == "Split");
            Assert.AreEqual(1, split.Input.Count);
            CollectionAssert.AreEqual(new long[] { 2, 2 }, split.Attribute.Single(value => value.Name == "split").Ints.ToArray());
            CollectionAssert.AreEqual(new long[] { 1 }, split.Attribute.Single(value => value.Name == "axis").Ints.ToArray());
            NodeProto mod = model.Graph.Node.Single(value => value.OpType == "Mod");
            Assert.IsFalse(mod.Attribute.Any(value => value.Name == "fmod"));
            NodeProto gridSample = model.Graph.Node.Single(value => value.OpType == "GridSample");
            Assert.IsFalse(gridSample.Attribute.Any(value => value.Name == "align_corners"));
            NodeProto[] unsqueeze = model.Graph.Node.Where(value => value.OpType == "Unsqueeze").ToArray();
            Assert.AreEqual(2, unsqueeze.Length);
            Assert.AreEqual(1, unsqueeze[0].Attribute.Single(value => value.Name == "axes").Ints.Count);
            Assert.AreEqual(1, unsqueeze[1].Attribute.Single(value => value.Name == "axes").Ints.Count);
            Assert.AreEqual("multi_unsqueeze", unsqueeze[1].Output[0]);
        }

        [TestMethod]
        public void ConstantInputUnsqueezeRunsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-unsqueeze-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateUnsqueezeFixture();
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-unsqueeze");
                var contract = new OpenCvDnnModelContract(modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(1, 2)) },
                    new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(1, 1, 2)) },
                    imageInputNames: Array.Empty<string>());
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false, specializeDynamicInputShapes: false));
                using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
                InferenceOutputs output = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(1, 2), new[] { 2f, 5f })), CancellationToken.None);
                CollectionAssert.AreEqual(new[] { 2f, 5f }, (float[])output.GetRequired("output").Buffer);
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void ConstantInputSliceRunsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-slice-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateSliceFixture();
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-slice");
                var contract = new OpenCvDnnModelContract(modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(1, 4)) },
                    new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(1, 2)) },
                    imageInputNames: Array.Empty<string>());
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
                using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
                InferenceOutputs output = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(1, 4), new[] { 2f, 5f, 7f, 11f })), CancellationToken.None);
                CollectionAssert.AreEqual(new[] { 5f, 7f }, (float[])output.GetRequired("output").Buffer);
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void ConstantInputSplitRunsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-split-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateSplitFixture();
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-split");
                var contract = new OpenCvDnnModelContract(modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(1, 4)) },
                    new[]
                    {
                        new TensorDescriptor("first", TensorElementType.Float32, new TensorShape(1, 2)),
                        new TensorDescriptor("second", TensorElementType.Float32, new TensorShape(1, 2))
                    },
                    imageInputNames: Array.Empty<string>());
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false, specializeDynamicInputShapes: false));
                using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
                InferenceOutputs output = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(1, 4), new[] { 2f, 5f, 7f, 11f })), CancellationToken.None);
                CollectionAssert.AreEqual(new[] { 2f, 5f }, (float[])output.GetRequired("first").Buffer);
                CollectionAssert.AreEqual(new[] { 7f, 11f }, (float[])output.GetRequired("second").Buffer);
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void SingleOutputSplitIsFoldedToIdentityAndRunsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-single-split-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateSingleOutputSplitFixture();
            byte[] normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(model, out bool changed);
            Assert.IsTrue(changed);
            Assert.AreEqual("Identity", ModelProto.Parser.ParseFrom(normalized).Graph.Node.Single().OpType);
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-single-split");
                var contract = new OpenCvDnnModelContract(modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(1, 4)) },
                    new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(1, 4)) },
                    imageInputNames: Array.Empty<string>());
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false, specializeDynamicInputShapes: false));
                using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
                float[] values = { 2f, 5f, 7f, 11f };
                InferenceOutputs output = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(1, 4), values)), CancellationToken.None);
                CollectionAssert.AreEqual(values, (float[])output.GetRequired("output").Buffer);
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void StaticImageContractsAdmitPositiveBatchSizes()
        {
            var contract = new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(4, 3, 48, 320)) },
                new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(4, 40, 10)) });

            CollectionAssert.AreEqual(new long[] { 4, 3, 48, 320 }, contract.Inputs.Single().Shape.ToArray());
            var options = new OpenCvDnnOptions(contract, numThreads: 4);
            Assert.AreEqual(4, options.NumThreads);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OpenCvDnnOptions(contract, numThreads: 0));
        }

        [TestMethod]
        public void ExplicitAuxiliaryInputsUseTheirDeclaredNumericContract()
        {
            var contract = new OpenCvDnnModelContract(
                Model,
                new[]
                {
                    new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)),
                    new TensorDescriptor("orig_target_sizes", TensorElementType.Int32, new TensorShape(1, 2))
                },
                new[] { new TensorDescriptor("labels", TensorElementType.Int32, new TensorShape(1, 2)) },
                imageInputNames: new[] { "images" });

            Assert.IsTrue(contract.IsImageInput("images"));
            Assert.IsFalse(contract.IsImageInput("orig_target_sizes"));
            CollectionAssert.AreEqual(new[] { "images" }, contract.ImageInputNames.ToArray());
            Assert.AreEqual(TensorElementType.Int32, contract.Inputs[1].ElementType);
            OpenCvDnnModelContract dynamicAuxiliaryContract = new OpenCvDnnModelContract(
                Model,
                new[]
                {
                    new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)),
                    new TensorDescriptor("orig_target_sizes", TensorElementType.Int32, new TensorShape(-1, 2))
                },
                contract.Outputs,
                imageInputNames: new[] { "images" });
            Assert.IsTrue(dynamicAuxiliaryContract.Inputs[1].Shape.IsDynamic);
        }

        [TestMethod]
        public void Int64AuxiliaryInputsAreAdmittedForGeometryContracts()
        {
            var contract = new OpenCvDnnModelContract(
                Model,
                new[]
                {
                    new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)),
                    new TensorDescriptor("orig_target_sizes", TensorElementType.Int64, new TensorShape(1, 2))
                },
                new[] { new TensorDescriptor("labels", TensorElementType.Int64, new TensorShape(1, 2)) },
                imageInputNames: new[] { "images" });

            Assert.IsFalse(contract.IsImageInput("orig_target_sizes"));
            Assert.AreEqual(TensorElementType.Int64, contract.Inputs[1].ElementType);
            Assert.AreEqual(TensorElementType.Int64, contract.Outputs.Single().ElementType);
        }

        [TestMethod]
        public void Int64GeometryAuxiliaryInputRunsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-int64-aux-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateInt64AuxiliaryFixture();
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-int64-auxiliary");
                var contract = new OpenCvDnnModelContract(
                    modelId,
                    new[]
                    {
                        new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)),
                        new TensorDescriptor("orig_target_sizes", TensorElementType.Int64, new TensorShape(1, 2))
                    },
                    new[] { new TensorDescriptor("score", TensorElementType.Float32, new TensorShape(1, 1)) },
                    imageInputNames: new[] { "images" });
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)),
                    new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                    SessionOptions.Default);
                var inputs = new InferenceInputs(new[]
                {
                    new NamedTensor("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), Enumerable.Range(1, 12).Select(value => (float)value).ToArray())),
                    new NamedTensor("orig_target_sizes", new Tensor<long>(new TensorShape(1, 2), new long[] { 6, 8 }))
                });

                float[] output = (float[])session.Run(inputs, CancellationToken.None).Single().Tensor.Buffer;
                Assert.AreEqual(92f, output.Single(), .00001f);

                var overflow = new InferenceInputs(new[]
                {
                    new NamedTensor("images", inputs.GetRequired("images")),
                    new NamedTensor("orig_target_sizes", new Tensor<long>(new TensorShape(1, 2), new[] { (long)int.MaxValue + 1L, 8L }))
                });
                OpenCvDnnBackendException error = Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(overflow, CancellationToken.None));
                Assert.AreEqual(OpenCvDnnErrorCodes.TensorInvalid, error.ErrorCode);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DynamicInputAndSingleDynamicOutputContractsAreAdmitted()
        {
            var contract = new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 48, 320)) },
                new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, -1, 6)) });

            Assert.IsTrue(contract.Outputs.Single().Shape.IsDynamic);
            OpenCvDnnModelContract dynamicInputContract = new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(-1, 3, 48, 320)) },
                contract.Outputs);
            Assert.IsTrue(dynamicInputContract.Inputs.Single().Shape.IsDynamic);
            Assert.ThrowsExactly<ArgumentException>(() => new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 48, 320)) },
                new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(-1, -1, 6)) }));

            var int32OutputContract = new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 48, 320)) },
                new[] { new TensorDescriptor("count", TensorElementType.Int32, new TensorShape(1)) });
            Assert.AreEqual(TensorElementType.Int32, int32OutputContract.Outputs.Single().ElementType);
            var int64OutputContract = new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 48, 320)) },
                new[] { new TensorDescriptor("labels", TensorElementType.Int64, new TensorShape(1, 4)) });
            Assert.AreEqual(TensorElementType.Int64, int64OutputContract.Outputs.Single().ElementType);
        }

        [TestMethod]
        public void DynamicAuxiliaryInputReloadsSpecializedNetworkForEachRuntimeShape()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "dynamic-identity.onnx");
            byte[] model = File.ReadAllBytes(path);
            var modelId = new ModelId("fixture/opencv-dynamic-identity");
            var contract = new OpenCvDnnModelContract(
                modelId,
                new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(-1, 2)) },
                new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(-1, 2)) },
                imageInputNames: Array.Empty<string>());
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)),
                new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                SessionOptions.Default);

            float[] first = { 1f, 2f, 3f, 4f, 5f, 6f };
            InferenceOutputs firstResult = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(3, 2), first)), CancellationToken.None);
            CollectionAssert.AreEqual(first, (float[])firstResult.GetRequired("output").Buffer);
            CollectionAssert.AreEqual(new long[] { 3, 2 }, firstResult.GetRequired("output").Shape.ToArray());

            float[] second = { 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f };
            InferenceOutputs secondResult = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(4, 2), second)), CancellationToken.None);
            CollectionAssert.AreEqual(second, (float[])secondResult.GetRequired("output").Buffer);
            CollectionAssert.AreEqual(new long[] { 4, 2 }, secondResult.GetRequired("output").Shape.ToArray());
        }

        [TestMethod]
        public void DynamicImageBatchIsSpecializedAndReloadedForRuntimeBatchSize()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-dynamic-image-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateDynamicImageIdentityFixture();
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-dynamic-image");
                var contract = new OpenCvDnnModelContract(modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2)) },
                    new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2)) });
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
                using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);

                float[] first = Enumerable.Range(1, 12).Select(value => (float)value).ToArray();
                InferenceOutputs firstResult = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(1, 3, 2, 2), first)), CancellationToken.None);
                CollectionAssert.AreEqual(first, (float[])firstResult.GetRequired("output").Buffer);
                CollectionAssert.AreEqual(new long[] { 1, 3, 2, 2 }, firstResult.GetRequired("output").Shape.ToArray());

                float[] second = Enumerable.Range(13, 24).Select(value => (float)value).ToArray();
                InferenceOutputs secondResult = session.Run(InferenceInputs.Create("input", new Tensor<float>(new TensorShape(2, 3, 2, 2), second)), CancellationToken.None);
                CollectionAssert.AreEqual(second, (float[])secondResult.GetRequired("output").Buffer);
                CollectionAssert.AreEqual(new long[] { 2, 3, 2, 2 }, secondResult.GetRequired("output").Shape.ToArray());
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void QuantizedUInt8AuxiliaryTensorRoundTripsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-uint8-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateIdentityFixture(TensorProto.Types.DataType.Uint8, 1, 2);
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-uint8-identity");
                var contract = new OpenCvDnnModelContract(
                    modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.UInt8, new TensorShape(1, 2)) },
                    new[] { new TensorDescriptor("output", TensorElementType.UInt8, new TensorShape(1, 2)) },
                    imageInputNames: Array.Empty<string>());
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)),
                    new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                    SessionOptions.Default);
                InferenceOutputs result = session.Run(InferenceInputs.Create("input", new Tensor<byte>(new TensorShape(1, 2), new byte[] { 7, 201 })), CancellationToken.None);
                CollectionAssert.AreEqual(new byte[] { 7, 201 }, (byte[])result.GetRequired("output").Buffer);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Float64AuxiliaryTensorRoundTripsThroughOpenCvDnn()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-f64-" + Guid.NewGuid().ToString("N") + ".onnx");
            byte[] model = CreateIdentityFixture(TensorProto.Types.DataType.Double, 1, 2);
            File.WriteAllBytes(path, model);
            try
            {
                var modelId = new ModelId("fixture/opencv-f64-identity");
                var contract = new OpenCvDnnModelContract(
                    modelId,
                    new[] { new TensorDescriptor("input", TensorElementType.Float64, new TensorShape(1, 2)) },
                    new[] { new TensorDescriptor("output", TensorElementType.Float64, new TensorShape(1, 2)) },
                    imageInputNames: Array.Empty<string>());
                using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
                using IInferenceSession session = provider.CreateSession(
                    new ModelArtifact(modelId, "onnx", path, Sha256Hex(model)),
                    new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                    SessionOptions.Default);
                InferenceOutputs result = session.Run(InferenceInputs.Create("input", new Tensor<double>(new TensorShape(1, 2), new[] { 1.25d, -2.5d })), CancellationToken.None);
                CollectionAssert.AreEqual(new[] { 1.25d, -2.5d }, (double[])result.GetRequired("output").Buffer);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ScalarAuxiliaryContractsAreAdmitted()
        {
            var contract = new OpenCvDnnModelContract(
                Model,
                new[] { new TensorDescriptor("value", TensorElementType.Float32, TensorShape.Scalar) },
                new[] { new TensorDescriptor("score", TensorElementType.Float32, TensorShape.Scalar) },
                imageInputNames: Array.Empty<string>());
            Assert.AreEqual(0, contract.Inputs.Single().Shape.Rank);
            Assert.AreEqual(0, contract.Outputs.Single().Shape.Rank);
        }

        [TestMethod]
        public void WrongShapeNameDeviceAndDisposedSessionAreRejected()
        {
            var artifact = new ModelArtifact(Model, "onnx", Fixture(), Sha256);
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(Contract()));
            Assert.IsFalse(provider.CanCreate(artifact, new BackendRequest(BackendCapabilities.TensorInference, device: "cuda")));
            Assert.AreEqual(OpenCvDnnErrorCodes.ConfigurationInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, device: "cuda"), SessionOptions.Default)).ErrorCode);
            var session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference), SessionOptions.Default);
            Assert.AreEqual(OpenCvDnnErrorCodes.TensorInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(new InferenceInputs(new[] { new NamedTensor("wrong", Inputs().Single().Tensor) }), CancellationToken.None)).ErrorCode);
            Assert.AreEqual(OpenCvDnnErrorCodes.TensorInvalid, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(new InferenceInputs(new[] { new NamedTensor("images", new Tensor<float>(new TensorShape(1, 1, 2, 2), new float[4])) }), CancellationToken.None)).ErrorCode);
            session.Dispose();
            Assert.AreEqual(OpenCvDnnErrorCodes.ObjectDisposed, Assert.ThrowsExactly<OpenCvDnnBackendException>(() => session.Run(Inputs(), CancellationToken.None)).ErrorCode);
        }

        [TestMethod]
        public void CancellationAndRegistryBoundaryAreStable()
        {
            var artifact = new ModelArtifact(Model, "onnx", Fixture(), Sha256);
            using var registry = new JYPPX.DeploySharp.Registry.BackendRegistry();
            registry.UseOpenCvDnn(new OpenCvDnnOptions(Contract()));
            using IInferenceSession session = registry.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            // The registry-owned pooled wrapper rejects pre-cancelled work before
            // leasing a native channel; backend-specific mapping is intentionally
            // not entered in this boundary case.
            Assert.ThrowsExactly<OperationCanceledException>(() => session.Run(Inputs(), cancellation.Token));
        }

        private static OpenCvDnnModelContract Contract() => new OpenCvDnnModelContract(Model, new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) }, new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });
        private static InferenceInputs Inputs() => new InferenceInputs(new[] { new NamedTensor("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f })) });
        private static string Fixture() => Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");

        private static byte[] CreateInt64AuxiliaryFixture()
        {
            var graph = new GraphProto { Name = "opencv-int64-auxiliary" };
            graph.Input.Add(ValueInfo("images", TensorProto.Types.DataType.Float, 1, 3, 2, 2));
            graph.Input.Add(ValueInfo("orig_target_sizes", TensorProto.Types.DataType.Int64, 1, 2));
            graph.Output.Add(ValueInfo("score", TensorProto.Types.DataType.Float, 1, 1));
            graph.Node.Add(Node("Flatten", new[] { "images" }, new[] { "image_flat" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            graph.Node.Add(Node("ReduceSum", new[] { "image_flat" }, new[] { "image_sum" }, new AttributeProto { Name = "axes", Type = AttributeProto.Types.AttributeType.Ints, Ints = { 1 } }, new AttributeProto { Name = "keepdims", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            graph.Node.Add(Node("Cast", new[] { "orig_target_sizes" }, new[] { "size_float" }, new AttributeProto { Name = "to", Type = AttributeProto.Types.AttributeType.Int, I = (int)TensorProto.Types.DataType.Float }));
            graph.Node.Add(Node("ReduceSum", new[] { "size_float" }, new[] { "size_sum" }, new AttributeProto { Name = "axes", Type = AttributeProto.Types.AttributeType.Ints, Ints = { 1 } }, new AttributeProto { Name = "keepdims", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            graph.Node.Add(Node("Add", new[] { "image_sum", "size_sum" }, new[] { "score" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 11 });
            return model.ToByteArray();
        }

        private static byte[] CreateIdentityFixture(TensorProto.Types.DataType elementType, params long[] dimensions)
        {
            var graph = new GraphProto { Name = "opencv-identity" };
            graph.Input.Add(ValueInfo("input", elementType, dimensions));
            graph.Output.Add(ValueInfo("output", elementType, dimensions));
            graph.Node.Add(Node("Identity", new[] { "input" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 11 });
            return model.ToByteArray();
        }

        private static byte[] CreateUnsqueezeFixture()
        {
            var graph = new GraphProto { Name = "opencv-unsqueeze" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 2));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Float, 1, 1, 2));
            var axes = new TensorProto { Name = "axes", DataType = (int)TensorProto.Types.DataType.Int64 };
            axes.Dims.Add(1);
            axes.Int64Data.Add(1);
            graph.Initializer.Add(axes);
            graph.Node.Add(Node("Unsqueeze", new[] { "input", "axes" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateShapeConcatFixture()
        {
            var graph = new GraphProto { Name = "opencv-shape-concat" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Float, 1, 4));
            graph.Initializer.Add(IntegerTensor("tail", 4));
            graph.Initializer.Add(IntegerTensor("reshape", 1, 4));
            graph.Node.Add(Node("Shape", new[] { "input" }, new[] { "shape" }));
            graph.Node.Add(Node("Gather", new[] { "shape", "tail" }, new[] { "dimension" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Unsqueeze", new[] { "dimension" }, new[] { "dimension_vector" }, new AttributeProto { Name = "axes", Type = AttributeProto.Types.AttributeType.Ints, Ints = { 0 } }));
            graph.Node.Add(Node("Concat", new[] { "dimension_vector", "tail" }, new[] { "target_shape" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Reshape", new[] { "input", "target_shape" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateMixedIntegerShapeComparisonFixture()
        {
            var graph = new GraphProto { Name = "opencv-mixed-integer-shape" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Bool, 1, 1));
            graph.Initializer.Add(IntegerTensor("expected", 4));
            graph.Node.Add(Node("Shape", new[] { "input" }, new[] { "input_shape" }));
            graph.Node.Add(Node("Gather", new[] { "input_shape", "expected" }, new[] { "actual" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Equal", new[] { "actual", "expected" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateMixedIntegerShapeWhereFixture()
        {
            var graph = new GraphProto { Name = "opencv-mixed-integer-where" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Input.Add(ValueInfo("condition", TensorProto.Types.DataType.Bool, 1));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Int64, 1));
            graph.Initializer.Add(IntegerTensor("index", 1));
            graph.Initializer.Add(IntegerTensor("fallback", 4));
            graph.Node.Add(Node("Shape", new[] { "input" }, new[] { "input_shape" }));
            graph.Node.Add(Node("Gather", new[] { "input_shape", "index" }, new[] { "actual" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Where", new[] { "condition", "actual", "fallback" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateScalarConstantExpandFixture()
        {
            var graph = new GraphProto { Name = "opencv-scalar-expand" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Float, 1, 4));
            var scalar = new TensorProto { DataType = (int)TensorProto.Types.DataType.Float };
            scalar.Dims.Add(1);
            scalar.FloatData.Add(2.5f);
            graph.Node.Add(Node("Constant", Array.Empty<string>(), new[] { "scalar" }, new AttributeProto { Name = "value", Type = AttributeProto.Types.AttributeType.Tensor, T = scalar }));
            graph.Node.Add(Node("Shape", new[] { "input" }, new[] { "shape" }));
            graph.Node.Add(Node("Expand", new[] { "scalar", "shape" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateConstantOperatorFixture()
        {
            var graph = new GraphProto { Name = "opencv-constant-operator-forms" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Float, 1, 2));
            graph.Initializer.Add(IntegerTensor("reduce_axes", 1));
            graph.Initializer.Add(IntegerTensor("topk_k", 2));
            graph.Initializer.Add(IntegerTensor("slice_starts", 0));
            graph.Initializer.Add(IntegerTensor("slice_ends", 2));
            graph.Initializer.Add(IntegerTensor("slice_axes", 1));
            graph.Initializer.Add(IntegerTensor("slice_steps", 1));
            graph.Initializer.Add(IntegerTensor("split_sizes", 2, 2));
            graph.Initializer.Add(IntegerTensor("unsqueeze_axes", 0, 2));
            graph.Node.Add(Node("ReduceSum", new[] { "input", "reduce_axes" }, new[] { "reduced" }, new AttributeProto { Name = "keepdims", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            graph.Node.Add(Node("ReduceMax", new[] { "input", "reduce_axes" }, new[] { "max_reduced" }, new AttributeProto { Name = "keepdims", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("TopK", new[] { "input", "topk_k" }, new[] { "top_values", "top_indices" }, new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            graph.Node.Add(Node("Slice", new[] { "input", "slice_starts", "slice_ends", "slice_axes", "slice_steps" }, new[] { "output" }));
            graph.Node.Add(Node("MaxPool", new[] { "input" }, new[] { "pool" },
                new AttributeProto { Name = "kernel_shape", Type = AttributeProto.Types.AttributeType.Ints, Ints = { 2 } },
                new AttributeProto { Name = "ceil_mode", Type = AttributeProto.Types.AttributeType.Int, I = 0 },
                new AttributeProto { Name = "storage_order", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("AveragePool", new[] { "input" }, new[] { "average_pool" },
                new AttributeProto { Name = "kernel_shape", Type = AttributeProto.Types.AttributeType.Ints, Ints = { 2 } },
                new AttributeProto { Name = "ceil_mode", Type = AttributeProto.Types.AttributeType.Int, I = 0 },
                new AttributeProto { Name = "count_include_pad", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Concat", new[] { "input", "input" }, new[] { "concat" },
                new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Gather", new[] { "input", "input" }, new[] { "gather" },
                new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Mod", new[] { "input", "input" }, new[] { "mod" },
                new AttributeProto { Name = "fmod", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("GridSample", new[] { "input", "input" }, new[] { "grid" },
                new AttributeProto { Name = "mode", Type = AttributeProto.Types.AttributeType.String, S = Google.Protobuf.ByteString.CopyFromUtf8("bilinear") },
                new AttributeProto { Name = "padding_mode", Type = AttributeProto.Types.AttributeType.String, S = Google.Protobuf.ByteString.CopyFromUtf8("zeros") },
                new AttributeProto { Name = "align_corners", Type = AttributeProto.Types.AttributeType.Int, I = 0 }));
            graph.Node.Add(Node("Split", new[] { "input", "split_sizes" }, new[] { "split0", "split1" },
                new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            graph.Node.Add(Node("Unsqueeze", new[] { "input", "unsqueeze_axes" }, new[] { "multi_unsqueeze" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateSliceFixture()
        {
            var graph = new GraphProto { Name = "opencv-slice" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Float, 1, 2));
            graph.Initializer.Add(IntegerTensor("starts", 1));
            graph.Initializer.Add(IntegerTensor("ends", 3));
            graph.Initializer.Add(IntegerTensor("axes", 1));
            graph.Initializer.Add(IntegerTensor("steps", 1));
            graph.Node.Add(Node("Slice", new[] { "input", "starts", "ends", "axes", "steps" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateSplitFixture()
        {
            var graph = new GraphProto { Name = "opencv-split" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("first", TensorProto.Types.DataType.Float, 1, 2));
            graph.Output.Add(ValueInfo("second", TensorProto.Types.DataType.Float, 1, 2));
            graph.Initializer.Add(IntegerTensor("split_sizes", 2, 2));
            graph.Node.Add(Node("Split", new[] { "input", "split_sizes" }, new[] { "first", "second" },
                new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateSingleOutputSplitFixture()
        {
            var graph = new GraphProto { Name = "opencv-single-split" };
            graph.Input.Add(ValueInfo("input", TensorProto.Types.DataType.Float, 1, 4));
            graph.Output.Add(ValueInfo("output", TensorProto.Types.DataType.Float, 1, 4));
            graph.Initializer.Add(IntegerTensor("split_sizes", 4));
            graph.Node.Add(Node("Split", new[] { "input", "split_sizes" }, new[] { "output" },
                new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 1 }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 13 });
            return model.ToByteArray();
        }

        private static byte[] CreateDynamicImageIdentityFixture()
        {
            var graph = new GraphProto { Name = "opencv-dynamic-image" };
            graph.Input.Add(DynamicBatchValueInfo("input", TensorProto.Types.DataType.Float, 3, 2, 2));
            graph.Output.Add(DynamicBatchValueInfo("output", TensorProto.Types.DataType.Float, 3, 2, 2));
            graph.Node.Add(Node("Identity", new[] { "input" }, new[] { "output" }));
            var model = new ModelProto { IrVersion = 7, ProducerName = "DeploySharp.Tests", Graph = graph };
            model.OpsetImport.Add(new OperatorSetIdProto { Version = 11 });
            return model.ToByteArray();
        }

        private static TensorProto IntegerTensor(string name, params long[] values)
        {
            var tensor = new TensorProto { Name = name, DataType = (int)TensorProto.Types.DataType.Int64 };
            tensor.Dims.Add(values.Length);
            tensor.Int64Data.Add(values);
            return tensor;
        }

        private static NodeProto Node(string type, string[] inputs, string[] outputs, params AttributeProto[] attributes)
        {
            var node = new NodeProto { OpType = type };
            node.Input.Add(inputs);
            node.Output.Add(outputs);
            node.Attribute.Add(attributes);
            return node;
        }

        private static ValueInfoProto ValueInfo(string name, TensorProto.Types.DataType elementType, params long[] dimensions)
        {
            var shape = new TensorShapeProto();
            foreach (long dimension in dimensions) shape.Dim.Add(new TensorShapeProto.Types.Dimension { DimValue = dimension });
            return new ValueInfoProto
            {
                Name = name,
                Type = new TypeProto { TensorType = new TypeProto.Types.Tensor { ElemType = (int)elementType, Shape = shape } }
            };
        }

        private static ValueInfoProto DynamicBatchValueInfo(string name, TensorProto.Types.DataType elementType, params long[] dimensions)
        {
            var shape = new TensorShapeProto();
            shape.Dim.Add(new TensorShapeProto.Types.Dimension { DimParam = "batch" });
            foreach (long dimension in dimensions) shape.Dim.Add(new TensorShapeProto.Types.Dimension { DimValue = dimension });
            return new ValueInfoProto
            {
                Name = name,
                Type = new TypeProto { TensorType = new TypeProto.Types.Tensor { ElemType = (int)elementType, Shape = shape } }
            };
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }
    }
}
