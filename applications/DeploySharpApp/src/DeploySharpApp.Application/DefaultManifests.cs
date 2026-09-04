using System.Collections.Generic;
using DeploySharpApp.Plugin.Abstractions;

namespace DeploySharpApp.Application
{
    public static class DefaultManifests
    {
        public static IReadOnlyList<PluginManifest> Create()
        {
            var manifests = new List<PluginManifest>();
            manifests.Add(CreateManifest("deploysharp.backend.onnxruntime", "ONNX Runtime", "1.28.0", "JYPPX.DeploySharp.Backend.OnnxRuntime", new[] { "tensor-inference", "dynamic-shapes" }, new[] { "onnx" }, "inprocess-or-worker", "onnxruntime.default", "Microsoft.ML.OnnxRuntime.Managed", "1.28.0", runtimeDependencies: new List<ManifestRuntimeDependency>
            {
                new ManifestRuntimeDependency { Kind = "managed-package", PackageId = "Microsoft.ML.OnnxRuntime.Managed", Version = "1.28.0", Downloadable = true, License = "MIT" },
                new ManifestRuntimeDependency { Kind = "native-package", PackageId = "Microsoft.ML.OnnxRuntime", Version = "1.28.0", Rid = "win-x64", Downloadable = true, License = "MIT", Condition = "device == cpu" }
            }, nativeRequirements: new[]
            {
                new ManifestNativeRequirement { Kind = "onnxruntime-native", RootSelection = "package-or-user", EnvironmentVariables = new List<string> { "DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH", "DEPLOYSHARP_ORT_ROOT" }, MinimumVersion = "1.28.0" }
            }));
            manifests.Add(CreateManifest("deploysharp.backend.opencv", "OpenCV DNN", "5.0.0-preview.1", "JYPPX.DeploySharp.Backend.OpenCV", new[] { "tensor-inference" }, new[] { "onnx" }, "inprocess-or-worker", "opencv.default", "JYPPX.OpenCV.CSharp.API", "5.0.0-preview.1", new List<ManifestRuntimeDependency> { new ManifestRuntimeDependency { Kind = "native-package", PackageId = "JYPPX.OpenCV.runtime.win-x64", Version = "5.0.0-preview.1", Downloadable = true, License = "Apache-2.0" } }, new[] { new ManifestNativeRequirement { Kind = "opencv", RootSelection = "package-or-user", EnvironmentVariables = new List<string> { "DEPLOYSHARP_OPENCV_ROOT" } } }));
            manifests.Add(CreateManifest("deploysharp.backend.openvino", "OpenVINO", "3.3.0", "JYPPX.DeploySharp.Backend.OpenVINO", new[] { "tensor-inference" }, new[] { "onnx", "ir" }, "inprocess-or-worker", "openvino.default", "JYPPX.OpenVINO.CSharp.API", "3.3.0", new List<ManifestRuntimeDependency> { new ManifestRuntimeDependency { Kind = "native-package", PackageId = "OpenVINO.runtime.win", Version = "2026.2.1", Downloadable = true, License = "Apache-2.0" } }, new[] { new ManifestNativeRequirement { Kind = "openvino", RootSelection = "package-or-user", EnvironmentVariables = new List<string> { "DEPLOYSHARP_OPENVINO_ROOT" } } }));
            manifests.Add(CreateManifest("deploysharp.backend.llamasharp", "LLamaSharp", "0.27.0", "JYPPX.DeploySharp.Backend.LlamaSharp", new[] { "text-generation", "embedding" }, new[] { "gguf" }, "worker", "llamasharp.default", "LLamaSharp", "0.27.0", new List<ManifestRuntimeDependency> { new ManifestRuntimeDependency { Kind = "managed-package", PackageId = "LLamaSharp.Backend.Cpu", Version = "0.27.0", Downloadable = true, License = "MIT" } }, new[] { new ManifestNativeRequirement { Kind = "llamasharp-native", RootSelection = "package-or-user", EnvironmentVariables = new List<string> { "DEPLOYSHARP_LLAMASHARP_ROOT" } } }));
            manifests.Add(CreateManifest("deploysharp.backend.tensorrt", "TensorRT", "4.0.0", "JYPPX.DeploySharp.Backend.TensorRT", new[] { "tensor-inference" }, new[] { "onnx", "engine" }, "worker", "tensorrt.default", "JYPPX.TensorRT.CSharp.API", "4.0.0", nativeRequirements: new[]
            {
                new ManifestNativeRequirement { Kind = "cuda", RootSelection = "user", EnvironmentVariables = new List<string> { "JYPPX_CUDA_ROOT", "CUDA_PATH" } },
                new ManifestNativeRequirement { Kind = "cudnn", RootSelection = "user", EnvironmentVariables = new List<string> { "JYPPX_CUDNN_ROOT" } },
                new ManifestNativeRequirement { Kind = "tensorrt", RootSelection = "user", EnvironmentVariables = new List<string> { "JYPPX_TENSORRT_ROOT" }, ApiLines = new List<string> { "8", "10", "11" } },
                new ManifestNativeRequirement { Kind = "bridge", RootSelection = "package-or-user", EnvironmentVariables = new List<string> { "JYPPX_NATIVE_BRIDGE_PATH" } }
            }));
            return manifests.AsReadOnly();
        }

        private static PluginManifest CreateManifest(string id, string name, string version, string packageId, string[] capabilities, string[] formats, string execution, string probeId, string providerId, string providerVersion, List<ManifestRuntimeDependency>? runtimeDependencies = null, ManifestNativeRequirement[]? nativeRequirements = null)
        {
            return new PluginManifest
            {
                SchemaVersion = 1,
                PluginId = id,
                DisplayName = name,
                Description = name + " DeploySharp adapter.",
                Version = version,
                PackageId = packageId,
                TargetFrameworks = new List<string> { "netstandard2.0", "net8.0", "net10.0" },
                RuntimeIdentifiers = new List<string> { "win-x64" },
                Execution = execution,
                Capabilities = new List<string>(capabilities),
                Formats = new List<string>(formats),
                ProviderPackageId = providerId,
                ProviderPackageVersion = providerVersion,
                RuntimeDependencies = runtimeDependencies,
                NativeRequirements = nativeRequirements == null ? null : new List<ManifestNativeRequirement>(nativeRequirements),
                ProbeId = probeId,
                License = "Apache-2.0"
            };
        }
    }
}
