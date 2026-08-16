using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

internal static class Program
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "deploysharp-tensorrt-local-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new TensorRtExternalCacheStore(root);
            TensorRtCudaKernelLookupIdentity cudaIdentity = CreateCudaIdentity();
            TensorRtCudaRtcArtifact cudaArtifact = CreateCudaArtifact(cudaIdentity);
            if (store.StoreCuda(cudaIdentity, cudaArtifact).Status != TensorRtExternalCacheStatus.Stored) return 2;
            TensorRtCudaCacheResult cudaHit = store.LookupCuda(cudaIdentity);
            if (cudaHit.Status != TensorRtExternalCacheStatus.Hit || cudaHit.Artifact?.ArtifactSha256 != cudaArtifact.ArtifactSha256) return 3;

            TensorRtEngineCacheIdentity engineIdentity = CreateEngineIdentity();
            using (var payload = new MemoryStream(Encoding.ASCII.GetBytes("consumer-engine"), writable: false))
            using (TensorRtEngineCacheResult stored = store.StoreEngine(engineIdentity, payload))
            {
                if (stored.Status != TensorRtExternalCacheStatus.Stored) return 4;
            }
            using (TensorRtEngineCacheResult hit = store.OpenEngine(engineIdentity))
            {
                if (hit.Status != TensorRtExternalCacheStatus.Hit || hit.Stream == null || hit.Stream.Length != 15) return 5;
            }

            int compileCount = 0;
            TensorRtCudaCacheResult compiled = store.GetOrCompileCuda(
                CreateCudaIdentity("postprocess"),
                _ =>
                {
                    compileCount++;
                    TensorRtCudaKernelLookupIdentity identity = CreateCudaIdentity("postprocess");
                    return CreateCudaArtifact(identity);
                });
            TensorRtCudaCacheResult compiledHit = store.GetOrCompileCuda(
                CreateCudaIdentity("postprocess"),
                _ => throw new InvalidOperationException("cache hit invoked compiler"));
            if (compiled.Status != TensorRtExternalCacheStatus.Stored || compiledHit.Status != TensorRtExternalCacheStatus.Hit || compileCount != 1) return 6;

            byte[] onnxBytes = Encoding.ASCII.GetBytes("managed-onnx-consumer");
            string onnxPath = Path.Combine(root, "consumer.onnx");
            File.WriteAllBytes(onnxPath, onnxBytes);
            string onnxSha256 = Sha256(onnxBytes);
            var onnxArtifact = new ModelArtifact(new ModelId("consumer/local-cache"), "onnx", onnxPath, onnxSha256);
            var buildOptions = new TensorRtOnnxEngineBuildOptions(maximumOnnxBytes: 1024, maximumEngineBytes: 1024);
            TensorRtEngineCacheIdentity facadeIdentity = CreateEngineIdentity(onnxSha256, buildOptions);
            int buildCount = 0;
            using (var factory = new TensorRtLocalSessionFactory(new TensorRtLocalCacheOptions(Path.Combine(root, "facade-cache"))))
            {
                using (TensorRtLocalEngineResult built = factory.ResolveOrBuildEngine(
                    onnxArtifact,
                    buildOptions,
                    facadeIdentity,
                    _ =>
                    {
                        buildCount++;
                        return new MemoryStream(Encoding.ASCII.GetBytes("facade-engine-one"), writable: false);
                    }))
                {
                    if (built.Status != TensorRtLocalCacheResolutionStatus.Built) return 7;
                }
                using TensorRtLocalEngineResult facadeHit = factory.ResolveOrBuildEngine(
                    onnxArtifact,
                    buildOptions,
                    facadeIdentity,
                    _ => throw new InvalidOperationException("cache hit invoked builder"));
                if (facadeHit.Status != TensorRtLocalCacheResolutionStatus.CacheHit || buildCount != 1) return 8;
            }

            if (store.InvalidateCuda(cudaIdentity).Status != TensorRtExternalCacheStatus.Deleted ||
                store.InvalidateEngine(engineIdentity).Status != TensorRtExternalCacheStatus.Deleted) return 9;
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        Console.WriteLine("DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine-facade=miss-hit cuda-store=miss-hit local-cache=engine-ptx-cubin gpu=not-run");
        return 0;
    }

    private static TensorRtCudaKernelLookupIdentity CreateCudaIdentity(string kernel = "prepare")
    {
        var definition = new TensorRtCudaRtcKernelDefinition(
            TensorRtCudaKernelRole.Preprocessing,
            "extern \"C\" __global__ void " + kernel + "(float* values) {}\n",
            kernel);
        var options = new TensorRtCudaRtcCompileOptions("compute_86");
        return new TensorRtCudaKernelLookupIdentity(definition, options, "12.9", "nvrtc+sha256:consumer", "12.9", "cudart+sha256:consumer", "576.02", "driver+sha256:consumer", "sm_86", "RTX-compatible", "bridge+sha256:consumer");
    }

    private static TensorRtCudaRtcArtifact CreateCudaArtifact(TensorRtCudaKernelLookupIdentity identity)
    {
        return new TensorRtCudaRtcArtifact(Encoding.ASCII.GetBytes("consumer-ptx\0"), identity.ArtifactKind, identity.Role, identity.SourceSha256, identity.HeadersSha256, identity.OptionsSha256, identity.CompilerVersion, identity.TargetArchitecture, identity.ProgramName, identity.KernelName);
    }

    private static TensorRtEngineCacheIdentity CreateEngineIdentity()
    {
        var options = new TensorRtOnnxEngineBuildOptions();
        return CreateEngineIdentity(new string('a', 64), options);
    }

    private static TensorRtEngineCacheIdentity CreateEngineIdentity(string onnxSha256, TensorRtOnnxEngineBuildOptions options)
    {
        return new TensorRtEngineCacheIdentity(onnxSha256, options, "JYPPX.TensorRT.CSharp.API/4.0.0+consumer", new string('b', 64), "10.11", "nvinfer+consumer", "12.9", "cudart+consumer", "9.22", "cudnn+consumer", "576.02", "driver+consumer", "bridge+consumer", "RTX-compatible", "8.6", "windows", "x64", ".plan", new[] { "consumer-contract" });
    }

    private static string Sha256(byte[] bytes)
    {
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(bytes);
        var text = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash) text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return text.ToString();
    }
}
