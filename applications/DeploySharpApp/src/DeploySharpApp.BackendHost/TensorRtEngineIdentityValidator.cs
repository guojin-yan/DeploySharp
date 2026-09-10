using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using JYPPX.DeploySharp.Backends.TensorRT;

namespace DeploySharpApp.BackendHost;

internal sealed class TensorRtValidationResult
{
    public TensorRtValidationResult(bool succeeded, string code, string message, IReadOnlyDictionary<string, string> details)
    {
        Succeeded = succeeded;
        Code = code;
        Message = message;
        Details = details;
    }

    public bool Succeeded { get; }
    public string Code { get; }
    public string Message { get; }
    public IReadOnlyDictionary<string, string> Details { get; }
}

internal static class TensorRtEngineIdentityValidator
{
    public static TensorRtValidationResult ValidateRuntime(IReadOnlyDictionary<string, string> payload)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["runtimeIdentifier"] = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        };
        string? cudaRoot = First(payload, "cudaRoot", "JYPPX_CUDA_ROOT", "CUDA_PATH");
        string? cudnnRoot = First(payload, "cudnnRoot", "JYPPX_CUDNN_ROOT");
        string? tensorRtRoot = First(payload, "tensorRtRoot", "JYPPX_TENSORRT_ROOT");
        string? bridgePath = ResolveBridgePath(payload);
        bool rootsValid = CheckRoot(details, "cuda", cudaRoot, "cudart64_*.dll", "libcudart.so")
            && CheckRoot(details, "cudnn", cudnnRoot, "cudnn*.dll", "libcudnn.so")
            && CheckRoot(details, "tensorrt", tensorRtRoot, "nvinfer*.dll", "libnvinfer.so");
        if (!string.IsNullOrWhiteSpace(bridgePath))
        {
            string fullBridge = Path.GetFullPath(bridgePath!);
            details["bridgePath"] = fullBridge;
            if (!File.Exists(fullBridge)) rootsValid = false;
        }
        else
        {
            details["bridgePath"] = string.Empty;
            rootsValid = false;
        }

        string? driver = FindDriverLibrary();
        details["driverPath"] = driver ?? string.Empty;
        if (driver == null) rootsValid = false;
        string? gpu = FindGpuIdentity(out string? driverVersion, out string? gpuCompatibility);
        details["gpuIdentity"] = gpu ?? string.Empty;
        details["driverVersion"] = driverVersion ?? string.Empty;
        details["gpuCompatibility"] = gpuCompatibility ?? string.Empty;
        if (gpu == null || driverVersion == null || gpuCompatibility == null) rootsValid = false;
        if (!rootsValid)
            return Failure("DSAPP-TENSORRT-NATIVE-MATCH-FAILED", "TensorRT requires verified CUDA, cuDNN, TensorRT, bridge, driver and GPU assets from explicit paths.", details);

        try
        {
            string tensorRtVersion = RuntimeVersion(details["tensorrtLibrary"], tensorRtRoot!, "TensorRT-") ?? throw new ArgumentException("TensorRT version cannot be derived from the selected runtime.");
            string cudaVersion = RuntimeVersion(details["cudaLibrary"], cudaRoot!, "v") ?? throw new ArgumentException("CUDA version cannot be derived from the selected runtime.");
            string cudnnVersion = RuntimeVersion(details["cudnnLibrary"], cudnnRoot!, "cuDNN-") ?? throw new ArgumentException("cuDNN version cannot be derived from the selected runtime.");
            details["runtime.tensorRtVersion"] = tensorRtVersion;
            details["runtime.cudaVersion"] = cudaVersion;
            details["runtime.cudnnVersion"] = cudnnVersion;
            details["runtime.driverVersion"] = driverVersion!;
            details["runtime.gpuCompatibility"] = gpuCompatibility!;
            details["runtime.bridgeIdentity"] = HashFile(details["bridgePath"]);
            details["runtime.cudaIdentity"] = HashFile(details["cudaLibrary"]);
            details["runtime.cudnnIdentity"] = HashFile(details["cudnnLibrary"]);
            details["runtime.tensorRtIdentity"] = HashFile(details["tensorrtLibrary"]);
            details["runtime.driverIdentity"] = HashFile(details["driverPath"]);
            return new TensorRtValidationResult(true, "DSAPP-TENSORRT-RUNTIME-MATCH-PASSED", "TensorRT native runtime, driver, bridge and GPU identity validation passed.", details);
        }
        catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
        {
            details["runtimeIdentityError"] = exception.Message;
            return Failure("DSAPP-TENSORRT-RUNTIME-IDENTITY-INVALID", "TensorRT runtime identity could not be derived from the selected native assets.", details);
        }
    }

    public static TensorRtValidationResult Validate(string enginePath, IReadOnlyDictionary<string, string> payload)
    {
        enginePath = Path.GetFullPath(enginePath);
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["enginePath"] = enginePath,
            ["runtimeIdentifier"] = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        };
        string? cudaRoot = First(payload, "cudaRoot", "JYPPX_CUDA_ROOT", "CUDA_PATH");
        string? cudnnRoot = First(payload, "cudnnRoot", "JYPPX_CUDNN_ROOT");
        string? tensorRtRoot = First(payload, "tensorRtRoot", "JYPPX_TENSORRT_ROOT");
        string? bridgePath = ResolveBridgePath(payload);
        if (!File.Exists(enginePath) || new FileInfo(enginePath).Length == 0)
            return Failure("DSAPP-TENSORRT-ENGINE-MISSING", "The TensorRT engine/plan is missing or empty.", details);
        string extension = Path.GetExtension(enginePath);
        if (!string.Equals(extension, ".engine", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".plan", StringComparison.OrdinalIgnoreCase))
            return Failure("DSAPP-TENSORRT-ENGINE-FORMAT-INVALID", "TensorRT accepts only an explicit .engine or .plan device-bound artifact.", details);
        bool rootsValid = CheckRoot(details, "cuda", cudaRoot, "cudart64_*.dll", "libcudart.so")
            && CheckRoot(details, "cudnn", cudnnRoot, "cudnn*.dll", "libcudnn.so")
            && CheckRoot(details, "tensorrt", tensorRtRoot, "nvinfer*.dll", "libnvinfer.so");
        if (!string.IsNullOrWhiteSpace(bridgePath))
        {
            string fullBridge = Path.GetFullPath(bridgePath!);
            details["bridgePath"] = fullBridge;
            if (!File.Exists(fullBridge)) rootsValid = false;
        }
        else rootsValid = false;
        string? driver = FindDriverLibrary();
        details["driverPath"] = driver ?? string.Empty;
        if (driver == null) rootsValid = false;
        string? gpu = FindGpuIdentity(out string? driverVersion, out string? gpuCompatibility);
        details["gpuIdentity"] = gpu ?? string.Empty;
        details["driverVersion"] = driverVersion ?? string.Empty;
        details["gpuCompatibility"] = gpuCompatibility ?? string.Empty;
        if (gpu == null || driverVersion == null || gpuCompatibility == null) rootsValid = false;
        if (!rootsValid)
            return Failure("DSAPP-TENSORRT-NATIVE-MATCH-FAILED", "TensorRT requires verified CUDA, cuDNN, TensorRT, bridge, driver and GPU assets from explicit paths.", details);

        string? identityPath = First(payload, "engineIdentityPath");
        if (string.IsNullOrWhiteSpace(identityPath))
            return Failure("DSAPP-TENSORRT-ENGINE-IDENTITY-REQUIRED", "The device-bound TensorRT engine identity sidecar is required before loading an external engine.", details);
        identityPath = Path.GetFullPath(identityPath!);
        details["engineIdentityPath"] = identityPath;
        if (!File.Exists(identityPath)) return Failure("DSAPP-TENSORRT-ENGINE-IDENTITY-MISSING", "The TensorRT engine identity sidecar does not exist.", details);
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(identityPath));
            var actual = ReadIdentity(document.RootElement);
            string expectedEngineSha256 = Required(actual, "engineSha256");
            string engineSha256 = HashFile(enginePath);
            details["engine.engineSha256"] = expectedEngineSha256;
            details["runtime.engineSha256"] = engineSha256;
            if (!string.Equals(expectedEngineSha256, engineSha256, StringComparison.OrdinalIgnoreCase))
                return Failure("DSAPP-TENSORRT-ENGINE-SHA256-MISMATCH", "The TensorRT engine file SHA256 does not match its identity sidecar.", details);
            var expected = new TensorRtEngineCompatibility(
                Required(actual, "onnxSha256"), Required(actual, "engineSerializationVersion"), Required(actual, "tensorRtVersion"),
                Required(actual, "cudaVersion"), Required(actual, "cudnnVersion"), Required(actual, "driverVersion"),
                Required(actual, "gpuCompatibility"), Required(actual, "bridgeIdentity"));
            foreach (string key in new[] { "onnxSha256", "engineSerializationVersion", "tensorRtVersion", "cudaVersion", "cudnnVersion", "driverVersion", "gpuCompatibility", "bridgeIdentity" })
                details["engine." + key] = actual[key];
            string tensorRtVersion = RuntimeVersion(details["tensorrtLibrary"], tensorRtRoot!, "TensorRT-") ?? throw new ArgumentException("TensorRT version cannot be derived from the selected runtime.");
            string cudaVersion = RuntimeVersion(details["cudaLibrary"], cudaRoot!, "v") ?? throw new ArgumentException("CUDA version cannot be derived from the selected runtime.");
            string cudnnVersion = RuntimeVersion(details["cudnnLibrary"], cudnnRoot!, "cuDNN-") ?? throw new ArgumentException("cuDNN version cannot be derived from the selected runtime.");
            string bridgeIdentity = HashFile(details["bridgePath"]);
            details["runtime.tensorRtVersion"] = tensorRtVersion;
            details["runtime.cudaVersion"] = cudaVersion;
            details["runtime.cudnnVersion"] = cudnnVersion;
            details["runtime.driverVersion"] = driverVersion!;
            details["runtime.gpuCompatibility"] = gpuCompatibility!;
            details["runtime.bridgeIdentity"] = bridgeIdentity;
            var runtime = new TensorRtEngineCompatibility(expected.OnnxSha256, expected.EngineSerializationVersion, tensorRtVersion, cudaVersion, cudnnVersion, driverVersion!, gpuCompatibility!, bridgeIdentity);
            if (!expected.Matches(runtime))
                return Failure("DSAPP-TENSORRT-ENGINE-IDENTITY-MISMATCH", "The TensorRT engine identity sidecar does not match the selected CUDA, cuDNN, TensorRT, driver, GPU or bridge.", details);
            if (actual.TryGetValue("gpuIdentity", out string? expectedGpuIdentity) && !string.Equals(expectedGpuIdentity, gpu, StringComparison.Ordinal))
                return Failure("DSAPP-TENSORRT-ENGINE-GPU-IDENTITY-MISMATCH", "The TensorRT engine was built for a different GPU identity.", details);
            string serializationMajor = expected.EngineSerializationVersion.Split('.')[0];
            string tensorRtMajor = tensorRtVersion.Split('.')[0];
            if (!string.Equals(serializationMajor, tensorRtMajor, StringComparison.Ordinal))
                return Failure("DSAPP-TENSORRT-ENGINE-SERIALIZATION-MISMATCH", "The engine serialization line does not match the selected TensorRT major version.", details);
            string? apiLine = First(payload, "apiVersion", "tensorRtApiVersion", "DEPLOYSHARP_TENSORRT_API_VERSION");
            if (!string.IsNullOrWhiteSpace(apiLine) && !string.Equals(apiLine, tensorRtMajor, StringComparison.Ordinal))
                return Failure("DSAPP-TENSORRT-API-LINE-MISMATCH", "The selected TensorRT API line does not match the detected TensorRT runtime major version.", details);
            if (First(payload, "sourceOnnxPath") is string sourceOnnxPath)
            {
                sourceOnnxPath = Path.GetFullPath(sourceOnnxPath);
                details["sourceOnnxPath"] = sourceOnnxPath;
                if (!File.Exists(sourceOnnxPath) || !string.Equals(HashFile(sourceOnnxPath), expected.OnnxSha256, StringComparison.Ordinal))
                    return Failure("DSAPP-TENSORRT-SOURCE-ONNX-MISMATCH", "The source ONNX file does not match the SHA256 recorded by the engine identity.", details);
            }
            details["engineIdentity"] = expected.OnnxSha256 + "/" + expected.TensorRtVersion + "/" + expected.GpuCompatibility;
            return new TensorRtValidationResult(true, "DSAPP-TENSORRT-NATIVE-MATCH-PASSED", "TensorRT native and device-bound engine identity validation passed.", details);
        }
        catch (Exception exception) when (exception is JsonException || exception is IOException || exception is ArgumentException)
        {
            details["identityError"] = exception.Message;
            return Failure("DSAPP-TENSORRT-ENGINE-IDENTITY-INVALID", "The TensorRT engine identity sidecar is invalid.", details);
        }
    }

    private static Dictionary<string, string> ReadIdentity(JsonElement root)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in new[] { "engineSha256", "onnxSha256", "engineSerializationVersion", "tensorRtVersion", "cudaVersion", "cudnnVersion", "driverVersion", "gpuCompatibility", "gpuIdentity", "bridgeIdentity" })
            if (root.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String) values[key] = value.GetString() ?? string.Empty;
        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing identity field: " + key);

    private static bool CheckRoot(Dictionary<string, string> details, string name, string? root, params string[] patterns)
    {
        if (string.IsNullOrWhiteSpace(root)) { details[name + "Root"] = string.Empty; details[name + "Library"] = string.Empty; return false; }
        string fullRoot = Path.GetFullPath(root!);
        details[name + "Root"] = fullRoot;
        if (!Directory.Exists(fullRoot)) { details[name + "Library"] = string.Empty; return false; }
        string? match = patterns.SelectMany(pattern => Directory.EnumerateFiles(fullRoot, pattern, SearchOption.AllDirectories)).FirstOrDefault();
        details[name + "Library"] = match ?? string.Empty;
        return match != null;
    }

    private static string? FindDriverLibrary()
    {
        string[] candidates = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(Environment.SystemDirectory, "nvcuda.dll") }
            : new[] { "/usr/lib/x86_64-linux-gnu/libcuda.so.1", "/usr/lib/wsl/lib/libcuda.so.1" };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindGpuIdentity(out string? driverVersion, out string? gpuCompatibility)
    {
        driverVersion = null;
        gpuCompatibility = null;
        string[] candidates = OperatingSystem.IsWindows()
            ? new[] { @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe", @"C:\Windows\System32\nvidia-smi.exe" }
            : new[] { "/usr/bin/nvidia-smi", "/usr/local/bin/nvidia-smi" };
        string? executable = candidates.FirstOrDefault(File.Exists);
        if (executable == null) return null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = executable, Arguments = "--query-gpu=name,compute_cap,driver_version --format=csv,noheader", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            if (process == null || !process.WaitForExit(3000)) return null;
            string output = process.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(output)) return null;
            string first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            string[] parts = first.Split(',').Select(value => value.Trim()).ToArray();
            if (parts.Length < 3) return null;
            gpuCompatibility = parts[1];
            driverVersion = parts[2];
            return parts[0] + ", compute capability " + gpuCompatibility;
        }
        catch { return null; }
    }

    private static string? VersionFromPath(string path, string marker)
    {
        Match match = Regex.Match(path, Regex.Escape(marker) + @"(?<version>\d+(?:\.\d+){1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["version"].Value : null;
    }

    private static string? FileVersion(string path)
    {
        if (!File.Exists(path)) return null;
        string? version = FileVersionInfo.GetVersionInfo(path).FileVersion;
        return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    private static string? RuntimeVersion(string libraryPath, string root, string marker)
        => VersionFromPath(root, marker) ?? FileVersion(libraryPath);

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string? First(IReadOnlyDictionary<string, string> payload, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (payload.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
            string? environment = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(environment)) return environment.Trim();
        }
        return null;
    }

    private static string? ResolveBridgePath(IReadOnlyDictionary<string, string> payload)
    {
        string? bridgePath = First(payload, "bridgePath", "JYPPX_NATIVE_BRIDGE_PATH");
        if (!string.IsNullOrWhiteSpace(bridgePath)) return bridgePath;
        string packagedBridge = Path.Combine(AppContext.BaseDirectory, "jyppxtrtbridge.dll");
        return File.Exists(packagedBridge) ? packagedBridge : null;
    }

    private static TensorRtValidationResult Failure(string code, string message, IReadOnlyDictionary<string, string> details)
        => new(false, code, message, details);
}
