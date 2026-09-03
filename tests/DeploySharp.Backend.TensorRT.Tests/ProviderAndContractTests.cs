using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.TensorRtSharp;
using JYPPX.TensorRtSharp.Shared.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Onnx;

namespace DeploySharp.Backend.TensorRT.Tests
{
    [TestClass]
    public sealed class ProviderAndContractTests
    {
        private static readonly ModelId ModelId = new ModelId("tests/tensorrt-plan");

        [TestMethod]
        public void DescriptorAndRegistryExposeOnlyManagedPlanContract()
        {
            using var registry = new BackendRegistry();
            registry.UseTensorRT();
            BackendDescriptor descriptor = registry.GetDescriptors().Single();
            Assert.AreEqual("tensorrt", descriptor.Id.Value);
            Assert.AreEqual("4.0.0", descriptor.Version);
            Assert.IsTrue(descriptor.Supports(BackendCapabilities.TensorInference | BackendCapabilities.DynamicShapes));
            Assert.IsFalse(descriptor.Supports(BackendCapabilities.AsynchronousExecution));
            CollectionAssert.AreEqual(new[] { "tensorrt-engine" }, descriptor.SupportedFormats.ToArray());
        }

        [TestMethod]
        public void ProviderAcceptsOnlyExternalPlanAndCudaRequests()
        {
            using var provider = new TensorRtBackendProvider();
            ModelArtifact plan = Artifact("model.plan");
            Assert.IsTrue(provider.CanCreate(plan, new BackendRequest(BackendCapabilities.TensorInference, device: "cuda")));
            Assert.IsTrue(provider.CanCreate(plan, new BackendRequest(BackendCapabilities.TensorInference)));
            Assert.IsFalse(provider.CanCreate(plan, new BackendRequest(BackendCapabilities.TensorInference, device: "cpu")));
            Assert.IsFalse(provider.CanCreate(new ModelArtifact(ModelId, "onnx", plan.Location), new BackendRequest(BackendCapabilities.TensorInference, device: "cuda")));
            Assert.IsFalse(provider.CanCreate(plan, new BackendRequest(BackendCapabilities.AsynchronousExecution, device: "cuda")));
        }

        [TestMethod]
        public void ArtifactValidatorRejectsWrongExtensionTruncationAndHashMismatch()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt-validation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string wrongExtension = Path.Combine(root, "model.bin");
                File.WriteAllBytes(wrongExtension, new byte[16]);
                Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtModelArtifactValidator.Validate(new ModelArtifact(ModelId, "tensorrt-engine", wrongExtension), 1024));

                string truncated = Path.Combine(root, "truncated.plan");
                File.WriteAllBytes(truncated, new byte[3]);
                Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtModelArtifactValidator.Validate(new ModelArtifact(ModelId, "tensorrt-engine", truncated), 1024));

                string valid = Path.Combine(root, "valid.engine");
                byte[] contents = new byte[16];
                for (int index = 0; index < contents.Length; index++) contents[index] = (byte)index;
                File.WriteAllBytes(valid, contents);
                TensorRtBackendException mismatch = Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtModelArtifactValidator.Validate(new ModelArtifact(ModelId, "tensorrt-engine", valid, new string('0', 64)), 1024));
                Assert.AreEqual(TensorRtErrorCodes.ModelArtifactInvalid, mismatch.ErrorCode);
                Assert.AreEqual(Path.GetFullPath(valid), TensorRtModelArtifactValidator.Validate(new ModelArtifact(ModelId, "tensorrt-engine", valid, Sha256(contents)), 1024));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ConfigurationAllowsIndependentChannelsAndRejectsProfilingBeforeNativeLoad()
        {
            using var provider = new TensorRtBackendProvider();
            ModelArtifact artifact = Artifact("missing.plan");
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, device: "cuda");
            TensorRtBackendException concurrency = Assert.ThrowsExactly<TensorRtBackendException>(() => provider.CreateSession(artifact, request, new SessionOptions(maxConcurrency: 2)));
            Assert.AreNotEqual(TensorRtErrorCodes.ConfigurationInvalid, concurrency.ErrorCode, "MaxConcurrency is now implemented as independently-created TensorRT sessions; the missing artifact must be reported instead.");
            TensorRtBackendException profiling = Assert.ThrowsExactly<TensorRtBackendException>(() => provider.CreateSession(artifact, request, new SessionOptions(enableProfiling: true)));
            Assert.AreEqual(TensorRtErrorCodes.ConfigurationInvalid, profiling.ErrorCode);
        }

        [TestMethod]
        public void BindingContractRejectsUnsupportedLayoutsBeforeExecution()
        {
            TensorRtEngineTensorBinding host = Binding(location: TensorRtTensorLocation.Host);
            TensorRtBackendException hostError = Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateForSession(host, ModelId));
            Assert.AreEqual(TensorRtErrorCodes.TensorInvalid, hostError.ErrorCode);

            TensorRtEngineTensorBinding vectorized = Binding(format: TensorRtTensorFormat.Chw4, vectorizedDimension: 1, componentsPerElement: 4);
            Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateForSession(vectorized, ModelId));

            TensorRtEngineTensorBinding shapeIo = Binding(isShapeInferenceIo: true);
            Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateForSession(shapeIo, ModelId));
        }

        [TestMethod]
        public void BindingContractEnforcesStaticAndProfileShapesAndOutputBytes()
        {
            TensorRtEngineTensorBinding fixedBinding = Binding(shape: new[] { 1, 3, 4, 4 });
            TensorRtBindingContract.ValidateInputShape(fixedBinding, new TensorRtDims(new[] { 1, 3, 4, 4 }), ModelId);
            Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateInputShape(fixedBinding, new TensorRtDims(new[] { 1, 3, 8, 8 }), ModelId));

            TensorRtEngineTensorBinding dynamicBinding = Binding(
                shape: new[] { 1, 3, -1, -1 },
                profileMinShape: new TensorRtDims(new[] { 1, 3, 4, 4 }),
                profileMaxShape: new TensorRtDims(new[] { 1, 3, 16, 16 }));
            TensorRtBindingContract.ValidateInputShape(dynamicBinding, new TensorRtDims(new[] { 1, 3, 8, 8 }), ModelId);
            Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateInputShape(dynamicBinding, new TensorRtDims(new[] { 1, 3, 32, 32 }), ModelId));

            TensorRtBindingContract.ValidateOutputBuffer(fixedBinding, new TensorRtDims(new[] { 1, 3, 4, 4 }), 192, ModelId);
            Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateOutputBuffer(fixedBinding, new TensorRtDims(new[] { 1, 3, 4, 4 }), 188, ModelId));

            TensorRtEngineTensorBinding dataDependentOutput = Binding(shape: new[] { -1, 7 }, ioMode: TensorRtIOMode.Output);
            TensorRtBindingContract.ValidateOutputBuffer(dataDependentOutput, new TensorRtDims(new[] { 25216, 7 }), 25216 * sizeof(float) * 7, ModelId);
            Assert.ThrowsExactly<TensorRtBackendException>(() => TensorRtBindingContract.ValidateOutputBuffer(dataDependentOutput, new TensorRtDims(new[] { 25216, 7 }), 7, ModelId));
        }

        [TestMethod]
        public void ValidatedReadUsesTheVerifiedArtifactBytes()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt-read-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] expected = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
                string path = Path.Combine(root, "model.plan");
                File.WriteAllBytes(path, expected);
                var artifact = new ModelArtifact(ModelId, "tensorrt-engine", path, Sha256(expected));
                CollectionAssert.AreEqual(expected, TensorRtModelArtifactValidator.ReadValidatedBytes(artifact, 1024));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void OnnxBuildOptionsRequireOrderedUniqueStaticProfiles()
        {
            var profile = new TensorRtOnnxInputProfile(
                "images",
                new TensorShape(1, 3, 224, 224),
                new TensorShape(4, 3, 512, 512),
                new TensorShape(8, 3, 1024, 1024));
            var options = new TensorRtOnnxEngineBuildOptions(
                precision: TensorRtOnnxEnginePrecision.Float16,
                workspaceBytes: 512UL * 1024 * 1024,
                optimizationLevel: 4,
                inputProfiles: new[] { profile });
            Assert.AreEqual(TensorRtOnnxEnginePrecision.Float16, options.Precision);
            Assert.AreEqual(4, options.OptimizationLevel);
            Assert.AreEqual(1, options.InputProfiles.Count);
            Assert.IsFalse(options.StronglyTypedNetwork);

            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtOnnxInputProfile(
                "images",
                new TensorShape(8, 3, 224, 224),
                new TensorShape(4, 3, 512, 512),
                new TensorShape(1, 3, 1024, 1024)));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtOnnxEngineBuildOptions(inputProfiles: new[] { profile, profile }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TensorRtOnnxEngineBuildOptions(optimizationLevel: 6));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtOnnxEngineBuildOptions(
                apiVersion: TensorRtApiVersion.TensorRt11,
                precision: TensorRtOnnxEnginePrecision.Float16));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtOnnxEngineBuildOptions(
                apiVersion: TensorRtApiVersion.TensorRt11,
                precision: TensorRtOnnxEnginePrecision.Float32));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtOnnxEngineBuildOptions(
                precision: TensorRtOnnxEnginePrecision.Float16,
                stronglyTypedNetwork: true));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtOnnxEngineBuildOptions(
                apiVersion: TensorRtApiVersion.TensorRt8,
                stronglyTypedNetwork: true));
            Assert.IsTrue(new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt11).StronglyTypedNetwork);

            string sourceHash = new string('a', 64);
            string first = TensorRtOnnxEngineBuilder.GetBuildInputsSha256(sourceHash, options);
            string second = TensorRtOnnxEngineBuilder.GetBuildInputsSha256(sourceHash, new TensorRtOnnxEngineBuildOptions(
                precision: TensorRtOnnxEnginePrecision.Float16,
                workspaceBytes: 512UL * 1024 * 1024,
                optimizationLevel: 4,
                inputProfiles: new[] { profile }));
            Assert.AreEqual(64, first.Length);
            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, TensorRtOnnxEngineBuilder.GetBuildInputsSha256(new string('b', 64), options));
            Assert.ThrowsExactly<ArgumentException>(() => TensorRtOnnxEngineBuilder.GetBuildInputsSha256("invalid", options));
        }

        [TestMethod]
        public void OnnxParserBuilderConfigAttachmentIsTensorRt11Only()
        {
            Assert.IsFalse(TensorRtOnnxEngineBuilder.ShouldAttachParserBuilderConfig(TensorRtApiVersion.TensorRt8));
            Assert.IsFalse(TensorRtOnnxEngineBuilder.ShouldAttachParserBuilderConfig(TensorRtApiVersion.TensorRt10));
            Assert.IsTrue(TensorRtOnnxEngineBuilder.ShouldAttachParserBuilderConfig(TensorRtApiVersion.TensorRt11));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                TensorRtOnnxEngineBuilder.ShouldAttachParserBuilderConfig((TensorRtApiVersion)9));
        }

        [TestMethod]
        public void OnnxCompatibilityPassAddsPaddleGatherSqueezeAxes()
        {
            var model = new ModelProto
            {
                IrVersion = 8,
                Graph = new GraphProto()
            };
            var constant = new NodeProto { OpType = "Constant" };
            constant.Output.Add("index");
            constant.Attribute.Add(new AttributeProto
            {
                Name = "value",
                Type = AttributeProto.Types.AttributeType.Tensor,
                T = new TensorProto { DataType = (int)TensorProto.Types.DataType.Int64, Int64Data = { 1 }, Dims = { 1 } }
            });
            var gather = new NodeProto { OpType = "Gather" };
            gather.Input.Add("selected");
            gather.Input.Add("index");
            gather.Output.Add("gathered");
            gather.Attribute.Add(new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Int, I = 1 });
            var squeeze = new NodeProto { OpType = "Squeeze" };
            squeeze.Input.Add("gathered");
            squeeze.Output.Add("result");
            model.Graph.Node.Add(constant);
            model.Graph.Node.Add(gather);
            model.Graph.Node.Add(squeeze);

            ModelProto normalized = ModelProto.Parser.ParseFrom(TensorRtOnnxCompatibilityPasses.Normalize(model.ToByteArray()));
            AttributeProto axes = normalized.Graph.Node.Single(node => node.OpType == "Squeeze").Attribute.Single(attribute => attribute.Name == "axes");
            CollectionAssert.AreEqual(new long[] { 1 }, axes.Ints.ToArray());
        }

        [TestMethod]
        public void OnnxValidatorEnforcesFormatExtensionLengthAndHash()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt-onnx-validation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] contents = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
                string onnxPath = Path.Combine(root, "model.onnx");
                File.WriteAllBytes(onnxPath, contents);
                Assert.AreEqual(
                    Path.GetFullPath(onnxPath),
                    TensorRtOnnxModelArtifactValidator.Validate(new ModelArtifact(ModelId, "onnx", onnxPath, Sha256(contents)), 1024));

                TensorRtBackendException format = Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    TensorRtOnnxModelArtifactValidator.Validate(new ModelArtifact(ModelId, "tensorrt-engine", onnxPath), 1024));
                Assert.AreEqual(TensorRtErrorCodes.OnnxModelInvalid, format.ErrorCode);

                string wrongExtension = Path.Combine(root, "model.bin");
                File.WriteAllBytes(wrongExtension, contents);
                Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    TensorRtOnnxModelArtifactValidator.Validate(new ModelArtifact(ModelId, "onnx", wrongExtension), 1024));
                Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    TensorRtOnnxModelArtifactValidator.Validate(new ModelArtifact(ModelId, "onnx", onnxPath, new string('0', 64)), 1024));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void OnnxBuilderRejectsNonExternalOutputsBeforeNativeInitialization()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt-onnx-output-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string onnxPath = Path.Combine(root, "model.onnx");
                byte[] contents = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
                File.WriteAllBytes(onnxPath, contents);
                var artifact = new ModelArtifact(ModelId, "onnx", onnxPath, Sha256(contents));
                var builder = new TensorRtOnnxEngineBuilder();

                TensorRtBackendException extension = Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    builder.Build(artifact, Path.Combine(root, "model.bin")));
                Assert.AreEqual(TensorRtErrorCodes.EngineOutputInvalid, extension.ErrorCode);

                TensorRtBackendException directory = Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    builder.Build(artifact, Path.Combine(root, "missing", "model.engine")));
                Assert.AreEqual(TensorRtErrorCodes.EngineOutputInvalid, directory.ErrorCode);

                string directoryWithEngineExtension = Path.Combine(root, "folder.engine");
                Directory.CreateDirectory(directoryWithEngineExtension);
                TensorRtBackendException directoryOutput = Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    builder.Build(artifact, directoryWithEngineExtension));
                Assert.AreEqual(TensorRtErrorCodes.EngineOutputInvalid, directoryOutput.ErrorCode);

                string existing = Path.Combine(root, "existing.plan");
                File.WriteAllBytes(existing, new byte[16]);
                TensorRtBackendException overwrite = Assert.ThrowsExactly<TensorRtBackendException>(() =>
                    builder.Build(artifact, existing));
                Assert.AreEqual(TensorRtErrorCodes.EngineOutputInvalid, overwrite.ErrorCode);
                Assert.AreEqual(16L, new FileInfo(existing).Length);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CudaRtcDefinitionPinsSourceHeadersOptionsAndArchitectureWithoutNativeLoad()
        {
            var definition = new TensorRtCudaRtcKernelDefinition(
                TensorRtCudaKernelRole.Preprocessing,
                "#include \"scale.cuh\"\nextern \"C\" __global__ void scale(float* value) {}\n",
                "scale",
                "scale.cu",
                new[] { new TensorRtCudaRtcHeader("scale.cuh", "#define SCALE 1.0f\n") });
            var options = new TensorRtCudaRtcCompileOptions(
                "compute_86",
                TensorRtCudaRtcArtifactKind.Ptx,
                generateLineInfo: true,
                additionalOptions: new[] { "--std=c++17" });

            Assert.AreEqual(Sha256(Encoding.UTF8.GetBytes(definition.Source)), definition.SourceSha256);
            Assert.AreEqual(64, definition.HeadersSha256.Length);
            Assert.AreEqual(64, options.OptionsSha256.Length);
            Assert.AreEqual("--gpu-architecture=compute_86", options.Options[0]);
            Assert.AreEqual("--generate-line-info", options.Options[1]);
            Assert.AreEqual("--std=c++17", options.Options[2]);
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaRtcCompileOptions("compute_86", TensorRtCudaRtcArtifactKind.Cubin));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaRtcCompileOptions("", TensorRtCudaRtcArtifactKind.Ptx));
        }

        [TestMethod]
        public void BackendOptionsRequireExplicitCudaArchitectureForOptionalReduction()
        {
            Assert.IsNull(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11).CudaTargetArchitecture);
            Assert.IsFalse(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11).CacheImmutableHostInputsOnDevice);
            Assert.AreEqual("compute_75", new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cudaTargetArchitecture: " compute_75 ").CudaTargetArchitecture);
            Assert.IsTrue(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cacheImmutableHostInputsOnDevice: true).CacheImmutableHostInputsOnDevice);
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtBackendOptions(cudaTargetArchitecture: "75"));
        }

        [TestMethod]
        public void CudaRtcArtifactIsCopiedAndRequiresExactHash()
        {
            byte[] code = Encoding.ASCII.GetBytes("ptx-bytes\0");
            string codeHash = Sha256(code);
            var artifact = new TensorRtCudaRtcArtifact(
                code,
                TensorRtCudaRtcArtifactKind.Ptx,
                TensorRtCudaKernelRole.Postprocessing,
                new string('a', 64),
                new string('b', 64),
                new string('c', 64),
                "12.9",
                "compute_86",
                "post.cu",
                "post",
                expectedArtifactSha256: codeHash);

            code[0] = 0;
            Assert.AreEqual(codeHash, artifact.ArtifactSha256);
            Assert.AreEqual((byte)'p', artifact.ToArray()[0]);
            Assert.AreEqual(64, artifact.CompilationInputsSha256.Length);
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaRtcArtifact(
                new byte[] { 1 },
                TensorRtCudaRtcArtifactKind.Ptx,
                TensorRtCudaKernelRole.Preprocessing,
                new string('a', 64),
                new string('b', 64),
                new string('c', 64),
                "12.9",
                "compute_86",
                "pre.cu",
                "pre",
                expectedArtifactSha256: new string('0', 64)));
        }

        [TestMethod]
        public void CudaBufferContractPinsTypeShapeRangeAndAccessWithoutAllocatingNativeMemory()
        {
            var descriptor = new TensorRtCudaBufferDescriptor(
                "images",
                TensorElementType.Float32,
                new TensorShape(1, 3, 16, 16),
                TensorRtCudaBufferAccess.ReadWrite,
                byteOffset: 128);

            Assert.AreEqual(3072, descriptor.ByteLength);
            Assert.AreEqual(128, descriptor.ByteOffset);
            Assert.AreEqual(64, descriptor.IdentitySha256.Length);
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaBufferDescriptor(
                "images",
                TensorElementType.Float32,
                new TensorShape(1, 3, 16, 16),
                TensorRtCudaBufferAccess.Read,
                byteLength: 1024));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaBufferDescriptor(
                "images",
                TensorElementType.String,
                new TensorShape(1),
                TensorRtCudaBufferAccess.Read));
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaBufferDescriptor(
                "images",
                TensorElementType.Float32,
                new TensorShape(1, -1),
                TensorRtCudaBufferAccess.Read));
        }

        [TestMethod]
        public void CudaOcrKernelDefinitionsExposeFusedStagesAndStableLaunchContracts()
        {
            Assert.AreEqual(TensorRtCudaKernelRole.Preprocessing, TensorRtCudaOcrKernels.NormalizeLetterboxDefinition.Role);
            Assert.AreEqual("deploysharp_normalize_letterbox", TensorRtCudaOcrKernels.NormalizeLetterboxDefinition.KernelName);
            Assert.AreEqual(TensorRtCudaKernelRole.Preprocessing, TensorRtCudaOcrKernels.HomographyDefinition.Role);
            Assert.AreEqual("deploysharp_quad_to_homography", TensorRtCudaOcrKernels.HomographyDefinition.KernelName);
            Assert.AreEqual("deploysharp_perspective_crop", TensorRtCudaOcrKernels.PerspectiveCropDefinition.KernelName);
            Assert.AreEqual("deploysharp_perspective_crop_quad", TensorRtCudaOcrKernels.PerspectiveCropFromQuadrilateralDefinition.KernelName);
            Assert.IsTrue(TensorRtCudaOcrKernels.PerspectiveCropFromQuadrilateralDefinition.Source.Contains("quadrilaterals", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaOcrKernels.PerspectiveCropFromQuadrilateralDefinition.Source.Contains("__shared__ float homography[8]", StringComparison.Ordinal));
            Assert.AreEqual(TensorRtCudaKernelRole.Postprocessing, TensorRtCudaOcrKernels.CtcDecodeDefinition.Role);
            Assert.IsTrue(TensorRtCudaOcrKernels.CtcDecodeDefinition.Source.Contains("blankIndex", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaOcrKernels.CtcDecodeDefinition.Source.Contains("collapseRepeats", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaOcrKernels.CtcDecodeDefinition.Source.Contains("__shared__ float reductionValues[256]", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaOcrKernels.CtcDecodeDefinition.Source.Contains("int sequence = (int)blockIdx.x", StringComparison.Ordinal));
            Assert.AreEqual("deploysharp_ctc_trace", TensorRtCudaOcrKernels.CtcTraceDefinition.KernelName);
            Assert.IsTrue(TensorRtCudaOcrKernels.CtcTraceDefinition.Source.Contains("timeBatchClasses", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaOcrKernels.CtcTraceDefinition.Source.Contains("invalidOffsets", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaOcrKernels.NormalizeLetterboxDefinition.Source.Contains("destination[pixels + index]", StringComparison.Ordinal));
            Assert.AreEqual(TensorRtCudaKernelRole.Preprocessing, TensorRtCudaVisualKernels.NormalizeBgrNchwDefinition.Role);
            Assert.AreEqual("deploysharp_visual_normalize_bgr_nchw", TensorRtCudaVisualKernels.NormalizeBgrNchwDefinition.KernelName);
            Assert.IsTrue(TensorRtCudaVisualKernels.NormalizeBgrNchwDefinition.Source.Contains("inverseScaleX", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaVisualKernels.NormalizeBgrNchwDefinition.Source.Contains("scale2", StringComparison.Ordinal));
            Assert.AreEqual(TensorRtCudaKernelRole.Postprocessing, TensorRtCudaVisualKernels.RestoreSingleChannelMapDefinition.Role);
            Assert.AreEqual("deploysharp_visual_restore_single_channel_map", TensorRtCudaVisualKernels.RestoreSingleChannelMapDefinition.KernelName);
            Assert.IsTrue(TensorRtCudaVisualKernels.RestoreSingleChannelMapDefinition.Source.Contains("atomicAdd(positiveCount", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaVisualKernels.RestoreSingleChannelMapDefinition.Source.Contains("validateProbability", StringComparison.Ordinal));
            Assert.AreEqual(TensorRtCudaKernelRole.Postprocessing, TensorRtCudaVisualKernels.CombineYoloPrototypeMasksDefinition.Role);
            Assert.AreEqual("deploysharp_visual_combine_yolo_prototypes", TensorRtCudaVisualKernels.CombineYoloPrototypeMasksDefinition.KernelName);
            Assert.AreEqual(TensorRtCudaKernelRole.Postprocessing, TensorRtCudaVisualKernels.RestoreYoloPrototypeMasksDefinition.Role);
            Assert.AreEqual("deploysharp_visual_restore_yolo_masks", TensorRtCudaVisualKernels.RestoreYoloPrototypeMasksDefinition.KernelName);
            Assert.IsTrue(TensorRtCudaVisualKernels.CombineYoloPrototypeMasksDefinition.Source.Contains("const float* coefficients", StringComparison.Ordinal));
            Assert.IsTrue(TensorRtCudaVisualKernels.RestoreYoloPrototypeMasksDefinition.Source.Contains("blockPositive", StringComparison.Ordinal));
            Assert.AreEqual(TensorRtCudaKernelRole.Postprocessing, TensorRtCudaVisualKernels.FilterYoloCandidatesDefinition.Role);
            Assert.AreEqual("deploysharp_visual_filter_yolo_candidates", TensorRtCudaVisualKernels.FilterYoloCandidatesDefinition.KernelName);
            Assert.IsTrue(TensorRtCudaVisualKernels.FilterYoloCandidatesDefinition.Source.Contains("selectedFlags[candidate]", StringComparison.Ordinal));
        }


        [TestMethod]
        public void CudaLaunchOptionsRequireExplicitNonZeroGridBlockAndSynchronization()
        {
            var options = new TensorRtCudaKernelLaunchOptions(
                gridX: 12,
                blockX: 256,
                synchronizationMode: TensorRtCudaSynchronizationMode.CallerManaged,
                gridY: 2,
                dynamicSharedMemoryBytes: 512);
            Assert.AreEqual((uint)12, options.GridX);
            Assert.AreEqual((uint)2, options.GridY);
            Assert.AreEqual((uint)256, options.BlockX);
            Assert.AreEqual(512, options.DynamicSharedMemoryBytes);
            Assert.AreEqual(TensorRtCudaSynchronizationMode.CallerManaged, options.SynchronizationMode);
            Assert.AreNotEqual(
                TensorRtCudaKernelArgument.FromInt32(1).ScalarValueSha256,
                TensorRtCudaKernelArgument.FromInt32(2).ScalarValueSha256);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TensorRtCudaKernelLaunchOptions(0, 256, TensorRtCudaSynchronizationMode.CallerManaged));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TensorRtCudaKernelLaunchOptions(1, 0, TensorRtCudaSynchronizationMode.CallerManaged));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TensorRtCudaKernelLaunchOptions(1, 1, (TensorRtCudaSynchronizationMode)99));
        }

        [TestMethod]
        public void PreparedCudaLaunchFreezesArgumentsAndCachesIdentity()
        {
            var artifact = new TensorRtCudaRtcArtifact(
                Encoding.ASCII.GetBytes("ptx\0"),
                TensorRtCudaRtcArtifactKind.Ptx,
                TensorRtCudaKernelRole.Preprocessing,
                new string('a', 64),
                new string('b', 64),
                new string('c', 64),
                "12.9",
                "compute_86",
                "pre.cu",
                "pre");
            using var kernel = TensorRtCudaCompiledKernel.CreateManagedTestDouble(artifact, 0);
            var arguments = new List<TensorRtCudaKernelArgument> { TensorRtCudaKernelArgument.FromInt32(7) };
            var options = new TensorRtCudaKernelLaunchOptions(1, 32, TensorRtCudaSynchronizationMode.CallerManaged);

            TensorRtCudaPreparedLaunch prepared = kernel.PrepareLaunch(options, arguments);
            arguments.Add(TensorRtCudaKernelArgument.FromInt32(9));

            Assert.AreSame(kernel, prepared.Owner);
            Assert.AreSame(options, prepared.Options);
            Assert.HasCount(1, prepared.NativeArguments);
            Assert.AreEqual(64, prepared.Identity.LaunchInputsSha256.Length);
        }

        [TestMethod]
        public void CudaCacheIdentityBindsArtifactCompilerRuntimeDriverGpuAndBridge()
        {
            var artifact = new TensorRtCudaRtcArtifact(
                Encoding.ASCII.GetBytes("ptx\0"),
                TensorRtCudaRtcArtifactKind.Ptx,
                TensorRtCudaKernelRole.Preprocessing,
                new string('a', 64),
                new string('b', 64),
                new string('c', 64),
                "12.9",
                "compute_86",
                "pre.cu",
                "pre");
            var identity = new TensorRtCudaKernelCacheIdentity(
                artifact,
                "nvrtc64_129_0.dll+sha256:test",
                "12090",
                "cudart64_12.dll+sha256:test",
                "12090",
                "nvcuda.dll+driver-package:test",
                "sm_86",
                "Ampere-sm86-compatible",
                "JYPPX.CudaSharp/4.0.0+bridge-sha256:test");
            var changedDriver = new TensorRtCudaKernelCacheIdentity(
                artifact,
                "nvrtc64_129_0.dll+sha256:test",
                "12090",
                "cudart64_12.dll+sha256:test",
                "12080",
                "nvcuda.dll+driver-package:test",
                "sm_86",
                "Ampere-sm86-compatible",
                "JYPPX.CudaSharp/4.0.0+bridge-sha256:test");

            Assert.AreEqual(64, identity.CacheKeySha256.Length);
            Assert.AreNotEqual(identity.CacheKeySha256, changedDriver.CacheKeySha256);
            Assert.AreEqual(artifact.ArtifactSha256, identity.ArtifactSha256);
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtCudaKernelCacheIdentity(
                artifact,
                "nvrtc64_129_0.dll+sha256:test",
                "12090",
                "cudart64_12.dll+sha256:test",
                "12090",
                "nvcuda.dll+driver-package:test",
                "sm_86",
                "",
                "bridge"));
        }

        [TestMethod]
        public void CudaStreamDeviceFallbackIsLimitedToUnsupportedCudaQuery()
        {
            Assert.IsTrue(TensorRtCudaCompiledKernel.IsStreamDeviceQueryUnavailable(new CudaException(
                BridgeStatusCode.NotSupported,
                BridgeErrorCategory.Cuda,
                "cudaStreamGetDevice is unavailable.")));
            Assert.IsFalse(TensorRtCudaCompiledKernel.IsStreamDeviceQueryUnavailable(new CudaException(
                BridgeStatusCode.RuntimeError,
                BridgeErrorCategory.Cuda,
                "A real CUDA runtime error must propagate.")));
            Assert.IsFalse(TensorRtCudaCompiledKernel.IsStreamDeviceQueryUnavailable(new CudaException(
                BridgeStatusCode.NotSupported,
                BridgeErrorCategory.Common,
                "A non-CUDA unsupported error must propagate.")));
        }

        private static ModelArtifact Artifact(string fileName)
        {
            return new ModelArtifact(ModelId, "tensorrt-engine", Path.Combine(Path.GetTempPath(), fileName));
        }

        private static TensorRtEngineTensorBinding Binding(
            int[]? shape = null,
            TensorRtTensorLocation location = TensorRtTensorLocation.Device,
            bool isShapeInferenceIo = false,
            TensorRtIOMode ioMode = TensorRtIOMode.Input,
            TensorRtTensorFormat format = TensorRtTensorFormat.Linear,
            int vectorizedDimension = -1,
            int componentsPerElement = 1,
            TensorRtDims? profileMinShape = null,
            TensorRtDims? profileMaxShape = null)
        {
            return new TensorRtEngineTensorBinding(
                0,
                "images",
                TensorRtDataType.Float,
                ioMode,
                new TensorRtDims(shape ?? new[] { 1, 3, 4, 4 }),
                location,
                isShapeInferenceIo,
                sizeof(float),
                componentsPerElement,
                format,
                format.ToString(),
                vectorizedDimension,
                0,
                profileMinShape,
                null,
                profileMaxShape,
                Array.Empty<string>());
        }

        private static string Sha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
