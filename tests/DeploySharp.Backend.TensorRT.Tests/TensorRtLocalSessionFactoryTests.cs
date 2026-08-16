using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests
{
    [TestClass]
    public sealed class TensorRtLocalSessionFactoryTests
    {
        [TestMethod]
        public void CacheOptionsUseStablePerUserDefaultAndRequireAbsoluteOverrides()
        {
            string expected = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JYPPX",
                "DeploySharp",
                "TensorRT"));

            Assert.AreEqual(expected, new TensorRtLocalCacheOptions().CacheRootPath);
            Assert.ThrowsExactly<ArgumentException>(() => new TensorRtLocalCacheOptions("relative-cache"));

            string custom = NewRoot();
            try
            {
                var options = new TensorRtLocalCacheOptions(custom);
                Assert.AreEqual(Path.GetFullPath(custom), options.CacheRootPath);
                Assert.IsFalse(Directory.Exists(custom));
            }
            finally
            {
                DeleteRoot(custom);
            }
        }

        [TestMethod]
        public void ResolveEngineBuildsOnceThenReturnsValidatedCacheHit()
        {
            string root = NewRoot();
            try
            {
                (ModelArtifact artifact, TensorRtOnnxEngineBuildOptions options, TensorRtEngineCacheIdentity identity) = CreateInputs(root);
                string cacheRoot = Path.Combine(root, "cache");
                int builds = 0;
                using var factory = CreateFactory(cacheRoot, (_, _, _, _, _, _) => new FakeSession(artifact.ModelId));
                Assert.IsTrue(factory.CacheRootCreated);

                using (TensorRtLocalEngineResult built = factory.ResolveOrBuildEngine(
                    artifact,
                    options,
                    identity,
                    _ =>
                    {
                        builds++;
                        return new MemoryStream(EngineBytes(), writable: false);
                    }))
                {
                    Assert.AreEqual(TensorRtLocalCacheResolutionStatus.Built, built.Status);
                    CollectionAssert.AreEqual(EngineBytes(), ReadAll(built.Stream));
                }

                using TensorRtLocalEngineResult hit = factory.ResolveOrBuildEngine(
                    artifact,
                    options,
                    identity,
                    _ => throw new AssertFailedException("The engine builder ran after a valid local cache hit."));
                Assert.AreEqual(TensorRtLocalCacheResolutionStatus.CacheHit, hit.Status);
                CollectionAssert.AreEqual(EngineBytes(), ReadAll(hit.Stream));
                Assert.AreEqual(1, builds);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void SessionLoadFailureInvalidatesAndRebuildsOnlyOnce()
        {
            string root = NewRoot();
            try
            {
                (ModelArtifact artifact, TensorRtOnnxEngineBuildOptions options, TensorRtEngineCacheIdentity identity) = CreateInputs(root);
                string cacheRoot = Path.Combine(root, "cache");
                int builds = 0;
                int loads = 0;
                using var factory = CreateFactory(cacheRoot, (_, _, _, _, _, _) =>
                {
                    loads++;
                    if (loads == 1)
                    {
                        throw new TensorRtBackendException(
                            TensorRtErrorCodes.NativeRuntimeUnavailable,
                            "Synthetic native load failure.");
                    }
                    return new FakeSession(artifact.ModelId);
                });

                using TensorRtLocalSessionResult session = factory.CreateSessionFromOnnx(
                    artifact,
                    options,
                    identity,
                    Request(),
                    SessionOptions.Default,
                    _ =>
                    {
                        builds++;
                        return new MemoryStream(EngineBytes(), writable: false);
                    },
                    CancellationToken.None);

                Assert.AreEqual(TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache, session.Status);
                Assert.AreEqual(2, builds);
                Assert.AreEqual(2, loads);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void SecondSessionLoadFailureEscapesWithoutThirdBuild()
        {
            string root = NewRoot();
            try
            {
                (ModelArtifact artifact, TensorRtOnnxEngineBuildOptions options, TensorRtEngineCacheIdentity identity) = CreateInputs(root);
                string cacheRoot = Path.Combine(root, "cache");
                int builds = 0;
                int loads = 0;
                using var factory = CreateFactory(cacheRoot, (_, _, _, _, _, _) =>
                {
                    loads++;
                    throw new TensorRtBackendException(
                        TensorRtErrorCodes.NativeRuntimeUnavailable,
                        "Synthetic repeated native load failure.");
                });

                Assert.ThrowsExactly<TensorRtBackendException>(() => factory.CreateSessionFromOnnx(
                    artifact,
                    options,
                    identity,
                    Request(),
                    SessionOptions.Default,
                    _ =>
                    {
                        builds++;
                        return new MemoryStream(EngineBytes(), writable: false);
                    },
                    CancellationToken.None));

                Assert.AreEqual(2, builds);
                Assert.AreEqual(2, loads);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static TensorRtLocalSessionFactory CreateFactory(
            string root,
            Func<ModelArtifact, Stream, string, BackendRequest, SessionOptions, CancellationToken, IInferenceSession> sessionLoader)
        {
            return new TensorRtLocalSessionFactory(
                new TensorRtLocalCacheOptions(root),
                () => new TensorRtOnnxEngineBuilder(),
                () => new TensorRtBackendProvider(),
                TensorRtCudaCompiledKernel.Load,
                sessionLoader);
        }

        private static (ModelArtifact Artifact, TensorRtOnnxEngineBuildOptions Options, TensorRtEngineCacheIdentity Identity) CreateInputs(string root)
        {
            Directory.CreateDirectory(root);
            byte[] onnxBytes = new byte[] { 8, 1, 18, 7, 116, 101, 115, 116, 45, 111, 110, 110, 120 };
            string path = Path.Combine(root, "model.onnx");
            File.WriteAllBytes(path, onnxBytes);
            string onnxSha256 = TensorRtOnnxModelArtifactValidator.ComputeSha256(onnxBytes);
            var artifact = new ModelArtifact(new ModelId("tests/local-cache"), "onnx", path, onnxSha256);
            var options = new TensorRtOnnxEngineBuildOptions(
                precision: TensorRtOnnxEnginePrecision.Float16,
                maximumOnnxBytes: 1024,
                maximumEngineBytes: 1024,
                workspaceBytes: 64 * 1024 * 1024,
                optimizationLevel: 3);
            var identity = new TensorRtEngineCacheIdentity(
                onnxSha256,
                options,
                "JYPPX.TensorRT.CSharp.API/4.0.0+contentHash:test",
                new string('b', 64),
                "10.11.0.33",
                "nvinfer+sha256:nvinfer",
                "12.9",
                "cudart+sha256:cudart",
                "9.22",
                "cudnn+sha256:cudnn",
                "576.02",
                "nvcuda+sha256:driver",
                "bridge+sha256:bridge",
                "NVIDIA RTX 3060 Laptop GPU",
                "8.6",
                "windows",
                "x64");
            return (artifact, options, identity);
        }

        private static BackendRequest Request()
        {
            return new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
        }

        private static byte[] EngineBytes()
        {
            return new byte[] { 84, 82, 84, 45, 69, 78, 71, 73, 78, 69, 45, 84, 69, 83, 84, 49 };
        }

        private static byte[] ReadAll(Stream stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static string NewRoot()
        {
            return Path.Combine(Path.GetTempPath(), "deploysharp-trt-local-" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        private sealed class FakeSession : IInferenceSession
        {
            public FakeSession(ModelId modelId)
            {
                Metadata = new ModelMetadata(modelId, "tensorrt-engine", Array.Empty<TensorDescriptor>(), Array.Empty<TensorDescriptor>());
            }

            public ModelMetadata Metadata { get; }

            public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public void Dispose() { }
        }
    }
}
