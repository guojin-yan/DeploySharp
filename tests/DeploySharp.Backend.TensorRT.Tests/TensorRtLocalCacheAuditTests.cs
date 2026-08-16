using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
    public sealed class TensorRtLocalCacheAuditTests
    {
        [TestMethod]
        public void ExternalStoreRejectsRelativeRootWithoutCreatingIt()
        {
            string relative = "deploysharp-relative-cache-" + Guid.NewGuid().ToString("N");
            string resolved = Path.GetFullPath(relative);

            TensorRtBackendException exception = Assert.ThrowsExactly<TensorRtBackendException>(() => new TensorRtExternalCacheStore(relative));

            Assert.AreEqual(TensorRtErrorCodes.ExternalCacheConfigurationInvalid, exception.ErrorCode);
            Assert.IsFalse(Directory.Exists(resolved));
        }

        [TestMethod]
        public void EngineLookupKeyBindsEveryBuildRuntimeAbiPlatformAndHardwareDimension()
        {
            string baseline = EngineIdentity().LookupKeySha256;
            TensorRtEngineCacheIdentity[] changed =
            {
                EngineIdentity(onnxSha256: new string('c', 64)),
                EngineIdentity(apiVersion: TensorRtApiVersion.TensorRt8),
                EngineIdentity(precision: TensorRtOnnxEnginePrecision.Float16),
                EngineIdentity(workspaceBytes: 268435456),
                EngineIdentity(optimizationLevel: 5),
                EngineIdentity(stronglyTypedNetwork: true),
                EngineIdentity(profileMinimumBatch: 2),
                EngineIdentity(profileOptimumBatch: 5),
                EngineIdentity(profileMaximumBatch: 9),
                EngineIdentity(profileInputName: "tokens"),
                EngineIdentity(managedPackageIdentity: "JYPPX.TensorRT.CSharp.API/4.0.0+other"),
                EngineIdentity(managedApiContractSha256: new string('d', 64)),
                EngineIdentity(tensorRtVersion: "10.12"),
                EngineIdentity(tensorRtIdentity: "nvinfer+other"),
                EngineIdentity(cudaRuntimeVersion: "12.8"),
                EngineIdentity(cudaRuntimeIdentity: "cudart+other"),
                EngineIdentity(cudnnVersion: "9.3"),
                EngineIdentity(cudnnIdentity: "cudnn+other"),
                EngineIdentity(cudaDriverVersion: "577.00"),
                EngineIdentity(cudaDriverIdentity: "driver+other"),
                EngineIdentity(nativeBridgeIdentity: "bridge+other"),
                EngineIdentity(gpuCompatibilityIdentity: "Ada-compatible"),
                EngineIdentity(gpuComputeCapability: "8.9"),
                EngineIdentity(operatingSystem: "linux"),
                EngineIdentity(processArchitecture: "arm64"),
                EngineIdentity(artifactExtension: ".plan"),
                EngineIdentity(builderFlags: new[] { "FP16", "STRICT_TYPES" })
            };

            foreach (TensorRtEngineCacheIdentity identity in changed)
            {
                Assert.AreNotEqual(baseline, identity.LookupKeySha256);
            }
        }

        [TestMethod]
        public void EngineLookupKeyExcludesPhysicalUuidAndEnumerationOrder()
        {
            string firstPhysicalUuid = "GPU-11111111-1111-1111-1111-111111111111";
            string secondPhysicalUuid = "GPU-22222222-2222-2222-2222-222222222222";
            Assert.AreNotEqual(firstPhysicalUuid, secondPhysicalUuid);

            TensorRtOnnxInputProfile images = Profile("images", 1, 4, 8);
            TensorRtOnnxInputProfile tokens = Profile("tokens", 1, 8, 16);
            TensorRtEngineCacheIdentity first = EngineIdentity(
                gpuCompatibilityIdentity: "Ampere-sm86-compatible",
                profiles: new[] { images, tokens },
                builderFlags: new[] { "FP16", "PREFER_PRECISION_CONSTRAINTS" });
            TensorRtEngineCacheIdentity second = EngineIdentity(
                gpuCompatibilityIdentity: "Ampere-sm86-compatible",
                profiles: new[] { tokens, images },
                builderFlags: new[] { "PREFER_PRECISION_CONSTRAINTS", "FP16" });

            Assert.AreEqual(first.LookupKeySha256, second.LookupKeySha256);
            Assert.AreEqual("Ampere-sm86-compatible", first.GpuCompatibilityIdentity);
        }

        [TestMethod]
        public void CudaLookupKeyBindsSourceHeadersOptionsToolchainTargetArtifactAndHardware()
        {
            string baseline = CudaIdentity().LookupKeySha256;
            TensorRtCudaKernelLookupIdentity[] changed =
            {
                CudaIdentity(source: "extern \"C\" __global__ void kernel() { int x = 1; }\n"),
                CudaIdentity(headers: new[] { new TensorRtCudaRtcHeader("a.cuh", "#define A 2\n") }),
                CudaIdentity(additionalOptions: new[] { "--std=c++20" }),
                CudaIdentity(targetArchitecture: "compute_89", gpuArchitecture: "sm_89"),
                CudaIdentity(targetArchitecture: "sm_86", artifactKind: TensorRtCudaRtcArtifactKind.Cubin),
                CudaIdentity(compilerVersion: "12.8"),
                CudaIdentity(compilerIdentity: "nvrtc+other"),
                CudaIdentity(cudaRuntimeVersion: "12.8"),
                CudaIdentity(cudaRuntimeIdentity: "cudart+other"),
                CudaIdentity(cudaDriverVersion: "577.00"),
                CudaIdentity(cudaDriverIdentity: "driver+other"),
                CudaIdentity(gpuArchitecture: "sm_89"),
                CudaIdentity(gpuCompatibilityIdentity: "Ada-compatible"),
                CudaIdentity(nativeBridgeIdentity: "bridge+other")
            };

            foreach (TensorRtCudaKernelLookupIdentity identity in changed)
            {
                Assert.AreNotEqual(baseline, identity.LookupKeySha256);
            }
        }

        [TestMethod]
        public void CudaHeadersUseDeterministicNameOrderAndPhysicalUuidIsExcluded()
        {
            TensorRtCudaRtcHeader first = new TensorRtCudaRtcHeader("a.cuh", "#define A 1\n");
            TensorRtCudaRtcHeader second = new TensorRtCudaRtcHeader("z.cuh", "#define Z 1\n");
            TensorRtCudaKernelLookupIdentity ordered = CudaIdentity(headers: new[] { first, second });
            TensorRtCudaKernelLookupIdentity reversed = CudaIdentity(headers: new[] { second, first });
            string physicalUuidA = "GPU-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            string physicalUuidB = "GPU-bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

            Assert.AreNotEqual(physicalUuidA, physicalUuidB);
            Assert.AreEqual(ordered.LookupKeySha256, reversed.LookupKeySha256);
            Assert.AreEqual("a.cuh", reversedDefinition(headers: new[] { second, first }).Headers[0].IncludeName);
            Assert.AreEqual("Ampere-sm86-compatible", ordered.GpuCompatibilityIdentity);
            Assert.AreNotEqual(
                CudaIdentity(headers: new[] { new TensorRtCudaRtcHeader("a.cuh", "#define VALUE 1\n") }).LookupKeySha256,
                CudaIdentity(headers: new[] { new TensorRtCudaRtcHeader("b.cuh", "#define VALUE 1\n") }).LookupKeySha256);
            Assert.AreNotEqual(
                CudaIdentity(headers: new[] { new TensorRtCudaRtcHeader("a.cuh", "#define VALUE 1\n") }).LookupKeySha256,
                CudaIdentity(headers: new[] { new TensorRtCudaRtcHeader("a.cuh", "#define VALUE 2\n") }).LookupKeySha256);
        }

        [TestMethod]
        public async Task ConcurrentFactoryFailureExecutesOnceAndIsSharedWithWaiters()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();
                var expected = new InvalidOperationException("stable synthetic factory failure");
                int factoryCalls = 0;
                using var ready = new CountdownEvent(8);
                using var start = new ManualResetEventSlim(false);
                using var factoryEntered = new ManualResetEventSlim(false);
                using var releaseFactory = new ManualResetEventSlim(false);
                Task<InvalidOperationException>[] tasks = Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(() =>
                {
                    ready.Signal();
                    start.Wait();
                    return Assert.ThrowsExactly<InvalidOperationException>(() => store.GetOrCompileCuda(identity, _ =>
                    {
                        Interlocked.Increment(ref factoryCalls);
                        factoryEntered.Set();
                        releaseFactory.Wait();
                        throw expected;
                    }));
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

                Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(5)));
                start.Set();
                Assert.IsTrue(factoryEntered.Wait(TimeSpan.FromSeconds(5)));
                Thread.Sleep(100);
                releaseFactory.Set();
                InvalidOperationException[] failures = await Task.WhenAll(tasks);

                Assert.AreEqual(1, factoryCalls);
                Assert.IsTrue(failures.All(exception => ReferenceEquals(expected, exception)));
                Assert.AreEqual(TensorRtExternalCacheStatus.Miss, store.LookupCuda(identity).Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public async Task DifferentKeysInOneRootDoNotBlockEachOther()
        {
            string root = NewRoot();
            try
            {
                var store = new TensorRtExternalCacheStore(root);
                TensorRtCudaKernelLookupIdentity firstIdentity = CudaIdentity();
                TensorRtCudaKernelLookupIdentity secondIdentity = CudaIdentity(source: "extern \"C\" __global__ void kernel() { int x = 2; }\n");
                TensorRtCudaRtcArtifact firstArtifact = CudaArtifact(firstIdentity, "first-ptx\0");
                TensorRtCudaRtcArtifact secondArtifact = CudaArtifact(secondIdentity, "second-ptx\0");
                using var firstEntered = new ManualResetEventSlim(false);
                using var releaseFirst = new ManualResetEventSlim(false);
                using var secondEntered = new ManualResetEventSlim(false);

                Task<TensorRtCudaCacheResult> first = Task.Run(() => store.GetOrCompileCuda(firstIdentity, _ =>
                {
                    firstEntered.Set();
                    releaseFirst.Wait();
                    return firstArtifact;
                }));
                Assert.IsTrue(firstEntered.Wait(TimeSpan.FromSeconds(5)));
                Task<TensorRtCudaCacheResult> second = Task.Run(() => store.GetOrCompileCuda(secondIdentity, _ =>
                {
                    secondEntered.Set();
                    return secondArtifact;
                }));

                Assert.IsTrue(secondEntered.Wait(TimeSpan.FromSeconds(5)));
                releaseFirst.Set();
                await Task.WhenAll(first, second);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void HardLinkedPayloadIsRejectedWhenPlatformSupportsHardLinks()
        {
            string root = NewRoot();
            string external = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity identity = EngineIdentity();
                var store = new TensorRtExternalCacheStore(root);
                using (var payload = new MemoryStream(Encoding.ASCII.GetBytes("engine-payload"))) store.StoreEngine(identity, payload).Dispose();
                string payloadPath = FindPayload(root, "engine", identity.LookupKeySha256);
                string externalPath = Path.Combine(external, "external.engine");
                File.WriteAllBytes(externalPath, File.ReadAllBytes(payloadPath));
                File.Delete(payloadPath);
                if (!OperatingSystem.IsWindows() || !CreateHardLink(payloadPath, externalPath, IntPtr.Zero)) return;

                using TensorRtEngineCacheResult result = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, result.Status);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.UnsafePath, result.RejectionReason);
            }
            finally
            {
                DeleteRoot(root);
                DeleteRoot(external);
            }
        }

        [TestMethod]
        public void ReparsePointGenerationDirectoryIsRejectedWhenPlatformAllowsCreation()
        {
            string root = NewRoot();
            string external = NewRoot();
            try
            {
                TensorRtEngineCacheIdentity identity = EngineIdentity();
                var store = new TensorRtExternalCacheStore(root);
                using (var payload = new MemoryStream(Encoding.ASCII.GetBytes("engine-payload"))) store.StoreEngine(identity, payload).Dispose();
                string payloadPath = FindPayload(root, "engine", identity.LookupKeySha256);
                string generationPath = Directory.GetParent(payloadPath)!.FullName;
                string externalGeneration = Path.Combine(external, "generation");
                Directory.Move(generationPath, externalGeneration);
                try { Directory.CreateSymbolicLink(generationPath, externalGeneration); }
                catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException) { return; }

                using TensorRtEngineCacheResult result = store.OpenEngine(identity);
                Assert.AreEqual(TensorRtExternalCacheStatus.Rejected, result.Status);
                Assert.AreEqual(TensorRtExternalCacheRejectionReason.UnsafePath, result.RejectionReason);
            }
            finally
            {
                DeleteRoot(root);
                DeleteRoot(external);
            }
        }

        [TestMethod]
        public void CudaFacadeCompilesOnceThenHitsWithoutCompilerExecution()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();
                TensorRtCudaRtcArtifact artifact = CudaArtifact(identity, "facade-ptx\0");
                int compilerCalls = 0;
                int loaderCalls = 0;
                using var factory = CreateCudaFactory(root, (loaded, ordinal) =>
                {
                    loaderCalls++;
                    return TensorRtCudaCompiledKernel.CreateManagedTestDouble(loaded, ordinal);
                });

                using (TensorRtLocalCudaKernelResult compiled = factory.ResolveOrCompileCudaKernel(identity, _ =>
                {
                    compilerCalls++;
                    return artifact;
                }))
                {
                    Assert.AreEqual(TensorRtLocalCacheResolutionStatus.Compiled, compiled.Status);
                }
                using TensorRtLocalCudaKernelResult hit = factory.ResolveOrCompileCudaKernel(identity, _ => throw new AssertFailedException("NVRTC compiler ran on a facade cache hit."));

                Assert.AreEqual(TensorRtLocalCacheResolutionStatus.CacheHit, hit.Status);
                Assert.AreEqual(1, compilerCalls);
                Assert.AreEqual(2, loaderCalls);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void CudaNativeLoadFailureInvalidatesExactKeyAndRecompilesOnlyOnce()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity invalidIdentity = CudaIdentity();
                TensorRtCudaKernelLookupIdentity otherIdentity = CudaIdentity(source: "extern \"C\" __global__ void kernel() { int y = 3; }\n");
                TensorRtCudaRtcArtifact invalidArtifact = CudaArtifact(invalidIdentity, "invalid-cached-ptx\0");
                TensorRtCudaRtcArtifact otherArtifact = CudaArtifact(otherIdentity, "other-cached-ptx\0");
                var store = new TensorRtExternalCacheStore(root);
                store.StoreCuda(invalidIdentity, invalidArtifact);
                store.StoreCuda(otherIdentity, otherArtifact);
                int compilerCalls = 0;
                int loaderCalls = 0;
                using var factory = CreateCudaFactory(root, (loaded, ordinal) =>
                {
                    loaderCalls++;
                    if (loaderCalls == 1) throw CudaModuleLoadFailure("first native load failed");
                    return TensorRtCudaCompiledKernel.CreateManagedTestDouble(loaded, ordinal);
                });

                using TensorRtLocalCudaKernelResult result = factory.ResolveOrCompileCudaKernel(invalidIdentity, _ =>
                {
                    compilerCalls++;
                    return CudaArtifact(invalidIdentity, "rebuilt-ptx\0");
                });

                Assert.AreEqual(TensorRtLocalCacheResolutionStatus.RebuiltAfterInvalidCache, result.Status);
                Assert.AreEqual(1, compilerCalls);
                Assert.AreEqual(2, loaderCalls);
                Assert.AreEqual(TensorRtExternalCacheStatus.Hit, store.LookupCuda(otherIdentity).Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void SecondCudaNativeLoadFailureEscapesStableDiagnosticWithoutLoop()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();
                TensorRtCudaRtcArtifact artifact = CudaArtifact(identity, "cached-ptx\0");
                new TensorRtExternalCacheStore(root).StoreCuda(identity, artifact);
                int compilerCalls = 0;
                int loaderCalls = 0;
                using var factory = CreateCudaFactory(root, (_, _) =>
                {
                    loaderCalls++;
                    throw CudaModuleLoadFailure("repeated native load failure");
                });

                TensorRtBackendException exception = Assert.ThrowsExactly<TensorRtBackendException>(() => factory.ResolveOrCompileCudaKernel(identity, _ =>
                {
                    compilerCalls++;
                    return CudaArtifact(identity, "rebuilt-ptx\0");
                }));

                Assert.AreEqual(TensorRtErrorCodes.CudaCompilationFailed, exception.ErrorCode);
                Assert.AreEqual("cuda-module-load", exception.Operation);
                Assert.AreEqual(1, compilerCalls);
                Assert.AreEqual(2, loaderCalls);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void CudaNativeLoadCancellationDoesNotInvalidateOrRecompile()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaKernelLookupIdentity identity = CudaIdentity();
                TensorRtCudaRtcArtifact artifact = CudaArtifact(identity, "cached-ptx\0");
                var store = new TensorRtExternalCacheStore(root);
                store.StoreCuda(identity, artifact);
                int compilerCalls = 0;
                using var factory = CreateCudaFactory(root, (_, _) => throw new OperationCanceledException("synthetic native cancellation"));

                Assert.ThrowsExactly<OperationCanceledException>(() => factory.ResolveOrCompileCudaKernel(identity, _ =>
                {
                    compilerCalls++;
                    return artifact;
                }));

                Assert.AreEqual(0, compilerCalls);
                Assert.AreEqual(TensorRtExternalCacheStatus.Hit, store.LookupCuda(identity).Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void EngineCacheHitDoesNotCreateBuilderFactory()
        {
            string root = NewRoot();
            try
            {
                byte[] onnxBytes = new byte[] { 8, 1, 18, 7, 116, 101, 115, 116, 45, 111, 110, 110, 120 };
                string onnxPath = Path.Combine(root, "model.onnx");
                File.WriteAllBytes(onnxPath, onnxBytes);
                string onnxSha256 = TensorRtOnnxModelArtifactValidator.ComputeSha256(onnxBytes);
                var artifact = new ModelArtifact(new ModelId("tests/builder-hit"), "onnx", onnxPath, onnxSha256);
                TensorRtEngineCacheIdentity identity = EngineIdentity(onnxSha256: onnxSha256);
                string cacheRoot = Path.Combine(root, "cache");
                var store = new TensorRtExternalCacheStore(cacheRoot);
                using (var payload = new MemoryStream(Encoding.ASCII.GetBytes("validated-engine"))) store.StoreEngine(identity, payload).Dispose();
                int builderFactoryCalls = 0;
                using var factory = new TensorRtLocalSessionFactory(
                    new TensorRtLocalCacheOptions(cacheRoot),
                    () =>
                    {
                        builderFactoryCalls++;
                        return new TensorRtOnnxEngineBuilder();
                    },
                    () => new TensorRtBackendProvider(),
                    TensorRtCudaCompiledKernel.Load,
                    sessionLoader: null);

                using TensorRtLocalEngineResult result = factory.ResolveOrBuildEngine(artifact, identity.BuildOptions, identity);

                Assert.AreEqual(TensorRtLocalCacheResolutionStatus.CacheHit, result.Status);
                Assert.AreEqual(0, builderFactoryCalls);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void CorruptEngineIsValidatedBeforeNativeSessionLoader()
        {
            string root = NewRoot();
            try
            {
                byte[] onnxBytes = new byte[] { 8, 1, 18, 7, 116, 101, 115, 116, 45, 111, 110, 110, 120 };
                string onnxPath = Path.Combine(root, "model.onnx");
                File.WriteAllBytes(onnxPath, onnxBytes);
                string onnxSha256 = TensorRtOnnxModelArtifactValidator.ComputeSha256(onnxBytes);
                var artifact = new ModelArtifact(new ModelId("tests/validate-before-native"), "onnx", onnxPath, onnxSha256);
                TensorRtEngineCacheIdentity identity = EngineIdentity(onnxSha256: onnxSha256);
                string cacheRoot = Path.Combine(root, "cache");
                var store = new TensorRtExternalCacheStore(cacheRoot);
                using (var payload = new MemoryStream(Encoding.ASCII.GetBytes("cached-engine"))) store.StoreEngine(identity, payload).Dispose();
                File.WriteAllText(FindPayload(cacheRoot, "engine", identity.LookupKeySha256), "corrupt");
                int builds = 0;
                int nativeLoads = 0;
                using var factory = new TensorRtLocalSessionFactory(
                    new TensorRtLocalCacheOptions(cacheRoot),
                    () => new TensorRtOnnxEngineBuilder(),
                    () => new TensorRtBackendProvider(),
                    TensorRtCudaCompiledKernel.Load,
                    (_, _, _, _, _, _) =>
                    {
                        nativeLoads++;
                        throw new AssertFailedException("Native loader ran before managed cache validation completed.");
                    });

                Assert.ThrowsExactly<InvalidOperationException>(() => factory.CreateSessionFromOnnx(
                    artifact,
                    identity.BuildOptions,
                    identity,
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                    SessionOptions.Default,
                    _ =>
                    {
                        builds++;
                        throw new InvalidOperationException("managed rebuild stopped before native load");
                    },
                    CancellationToken.None));

                Assert.AreEqual(1, builds);
                Assert.AreEqual(0, nativeLoads);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void PublicCudaFacadeHitDoesNotInvokeNvrtcCompiler()
        {
            string root = NewRoot();
            try
            {
                TensorRtCudaRtcKernelDefinition definition = reversedDefinition();
                var options = new TensorRtCudaRtcCompileOptions("compute_86");
                var identity = new TensorRtCudaKernelLookupIdentity(
                    definition,
                    options,
                    "12.9",
                    "nvrtc+test",
                    "12.9",
                    "cudart+test",
                    "576.02",
                    "driver+test",
                    "sm_86",
                    "Ampere-sm86-compatible",
                    "bridge+test");
                TensorRtCudaRtcArtifact artifact = CudaArtifact(identity, "cached-public-ptx\0");
                new TensorRtExternalCacheStore(root).StoreCuda(identity, artifact);
                using var factory = CreateCudaFactory(root, TensorRtCudaCompiledKernel.CreateManagedTestDouble);

                using TensorRtLocalCudaKernelResult result = factory.ResolveOrCompileCudaKernel(
                    definition,
                    options,
                    "12.9",
                    "nvrtc+test",
                    "12.9",
                    "cudart+test",
                    "576.02",
                    "driver+test",
                    "sm_86",
                    "Ampere-sm86-compatible",
                    "bridge+test");

                Assert.AreEqual(TensorRtLocalCacheResolutionStatus.CacheHit, result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static TensorRtEngineCacheIdentity EngineIdentity(
            string? onnxSha256 = null,
            TensorRtApiVersion apiVersion = TensorRtApiVersion.TensorRt10,
            TensorRtOnnxEnginePrecision precision = TensorRtOnnxEnginePrecision.RuntimeDefault,
            ulong workspaceBytes = 536870912,
            int optimizationLevel = 4,
            bool stronglyTypedNetwork = false,
            long profileMinimumBatch = 1,
            long profileOptimumBatch = 4,
            long profileMaximumBatch = 8,
            string profileInputName = "images",
            string managedPackageIdentity = "JYPPX.TensorRT.CSharp.API/4.0.0+contentHash:test",
            string? managedApiContractSha256 = null,
            string tensorRtVersion = "10.11",
            string tensorRtIdentity = "nvinfer+test",
            string cudaRuntimeVersion = "12.9",
            string cudaRuntimeIdentity = "cudart+test",
            string cudnnVersion = "9.22",
            string cudnnIdentity = "cudnn+test",
            string cudaDriverVersion = "576.02",
            string cudaDriverIdentity = "driver+test",
            string nativeBridgeIdentity = "bridge+test",
            string gpuCompatibilityIdentity = "Ampere-sm86-compatible",
            string gpuComputeCapability = "8.6",
            string operatingSystem = "windows",
            string processArchitecture = "x64",
            string artifactExtension = ".engine",
            IEnumerable<string>? builderFlags = null,
            IEnumerable<TensorRtOnnxInputProfile>? profiles = null)
        {
            TensorRtOnnxInputProfile[] selectedProfiles = profiles?.ToArray() ?? new[]
            {
                Profile(profileInputName, profileMinimumBatch, profileOptimumBatch, profileMaximumBatch)
            };
            var options = new TensorRtOnnxEngineBuildOptions(
                apiVersion,
                precision,
                workspaceBytes: workspaceBytes,
                optimizationLevel: optimizationLevel,
                stronglyTypedNetwork: stronglyTypedNetwork,
                inputProfiles: selectedProfiles);
            return new TensorRtEngineCacheIdentity(
                onnxSha256 ?? new string('a', 64),
                options,
                managedPackageIdentity,
                managedApiContractSha256 ?? new string('b', 64),
                tensorRtVersion,
                tensorRtIdentity,
                cudaRuntimeVersion,
                cudaRuntimeIdentity,
                cudnnVersion,
                cudnnIdentity,
                cudaDriverVersion,
                cudaDriverIdentity,
                nativeBridgeIdentity,
                gpuCompatibilityIdentity,
                gpuComputeCapability,
                operatingSystem,
                processArchitecture,
                artifactExtension,
                builderFlags ?? new[] { "FP16", "PREFER_PRECISION_CONSTRAINTS" });
        }

        private static TensorRtOnnxInputProfile Profile(string name, long minimumBatch, long optimumBatch, long maximumBatch)
        {
            return new TensorRtOnnxInputProfile(
                name,
                new TensorShape(minimumBatch, 3, 16, 16),
                new TensorShape(optimumBatch, 3, 16, 16),
                new TensorShape(maximumBatch, 3, 16, 16));
        }

        private static TensorRtCudaKernelLookupIdentity CudaIdentity(
            string source = "extern \"C\" __global__ void kernel() {}\n",
            IEnumerable<TensorRtCudaRtcHeader>? headers = null,
            IEnumerable<string>? additionalOptions = null,
            string targetArchitecture = "compute_86",
            TensorRtCudaRtcArtifactKind artifactKind = TensorRtCudaRtcArtifactKind.Ptx,
            string compilerVersion = "12.9",
            string compilerIdentity = "nvrtc+test",
            string cudaRuntimeVersion = "12.9",
            string cudaRuntimeIdentity = "cudart+test",
            string cudaDriverVersion = "576.02",
            string cudaDriverIdentity = "driver+test",
            string gpuArchitecture = "sm_86",
            string gpuCompatibilityIdentity = "Ampere-sm86-compatible",
            string nativeBridgeIdentity = "bridge+test")
        {
            TensorRtCudaRtcKernelDefinition definition = reversedDefinition(source, headers);
            var options = new TensorRtCudaRtcCompileOptions(targetArchitecture, artifactKind, additionalOptions: additionalOptions);
            return new TensorRtCudaKernelLookupIdentity(
                definition,
                options,
                compilerVersion,
                compilerIdentity,
                cudaRuntimeVersion,
                cudaRuntimeIdentity,
                cudaDriverVersion,
                cudaDriverIdentity,
                gpuArchitecture,
                gpuCompatibilityIdentity,
                nativeBridgeIdentity);
        }

        private static TensorRtCudaRtcKernelDefinition reversedDefinition(
            string source = "extern \"C\" __global__ void kernel() {}\n",
            IEnumerable<TensorRtCudaRtcHeader>? headers = null)
        {
            return new TensorRtCudaRtcKernelDefinition(
                TensorRtCudaKernelRole.Preprocessing,
                source,
                "kernel",
                "kernel.cu",
                headers);
        }

        private static TensorRtCudaRtcArtifact CudaArtifact(TensorRtCudaKernelLookupIdentity identity, string payload)
        {
            return new TensorRtCudaRtcArtifact(
                Encoding.ASCII.GetBytes(payload),
                identity.ArtifactKind,
                identity.Role,
                identity.SourceSha256,
                identity.HeadersSha256,
                identity.OptionsSha256,
                identity.CompilerVersion,
                identity.TargetArchitecture,
                identity.ProgramName,
                identity.KernelName,
                identity.KernelNameExpression);
        }

        private static TensorRtLocalSessionFactory CreateCudaFactory(
            string root,
            Func<TensorRtCudaRtcArtifact, int, TensorRtCudaCompiledKernel> loader)
        {
            return new TensorRtLocalSessionFactory(
                new TensorRtLocalCacheOptions(root),
                () => new TensorRtOnnxEngineBuilder(),
                () => new TensorRtBackendProvider(),
                loader,
                sessionLoader: null);
        }

        private static TensorRtBackendException CudaModuleLoadFailure(string message)
        {
            return new TensorRtBackendException(
                TensorRtErrorCodes.CudaCompilationFailed,
                message,
                operation: "cuda-module-load");
        }

        private static string FindPayload(string root, string category, string key)
        {
            string entry = Path.Combine(root, "deploysharp-tensorrt-cache-v1", category, key.Substring(0, 2), key);
            string generation = Directory.EnumerateDirectories(entry, "g-*").Single();
            return Directory.EnumerateFiles(generation, "artifact.*").Single();
        }

        private static string NewRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-trt-audit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (!Directory.Exists(root)) return;
            try { Directory.Delete(root, recursive: true); }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
    }
}
