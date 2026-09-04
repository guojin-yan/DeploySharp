using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DeploySharpApp.Contracts
{
    public enum AppBackendCapability { None = 0, Vision = 1, TextGeneration = 2, Embedding = 4, Multimodal = 8, Benchmark = 16 }
    public enum AppRuntimeState { Available, MissingPackage, MissingNative, Incompatible, Unavailable, ProbeFailed, Unsupported, Installing }
    public enum AppExecutionMode { InProcess, Worker, InProcessOrWorker }
    public enum AppOperationKind { Vision, TextGeneration, Embedding, Multimodal, Benchmark, Doctor }
    public enum AppErrorCode { None, InvalidRequest, BackendUnavailable, ModelUnavailable, NativeDependencyMissing, WorkerFailed, Cancelled, TimedOut, Unknown, WorkerRequired }
    public enum ModelRunMode { Unspecified, Demo, RealOnnxRuntime, Worker }
    public enum WorkerMessageKind { Handshake, Capability, Probe, Inference, Benchmark, Cancel, Shutdown, Error, Log, Progress }
    public enum WorkerResponseKind { Handshake, Capability, Probe, Result, Error, Log, Progress, Shutdown }

    public sealed class AppBackendInfo
    {
        public AppBackendInfo(string id, string displayName, string version, AppBackendCapability capabilities, IEnumerable<string>? formats = null, AppRuntimeState state = AppRuntimeState.Unavailable, AppExecutionMode executionMode = AppExecutionMode.InProcessOrWorker, IEnumerable<string>? devices = null, string? detail = null, string? probeId = null, string? providerPackageId = null, string? providerPackageVersion = null)
        {
            Id = ContractGuard.Id(id, nameof(id)); DisplayName = ContractGuard.Text(displayName, nameof(displayName)); Version = ContractGuard.Text(version, nameof(version)); Capabilities = capabilities; State = state; ExecutionMode = executionMode;
            Formats = ContractGuard.List(formats); Devices = ContractGuard.List(devices); Detail = ContractGuard.Optional(detail); ProbeId = ContractGuard.Optional(probeId); ProviderPackageId = ContractGuard.Optional(providerPackageId); ProviderPackageVersion = ContractGuard.Optional(providerPackageVersion);
        }
        public string Id { get; }
        public string DisplayName { get; }
        public string Version { get; }
        public AppBackendCapability Capabilities { get; }
        public IReadOnlyList<string> Formats { get; }
        public AppRuntimeState State { get; }
        public AppExecutionMode ExecutionMode { get; }
        public IReadOnlyList<string> Devices { get; }
        public string? Detail { get; }
        public string? ProbeId { get; }
        public string? ProviderPackageId { get; }
        public string? ProviderPackageVersion { get; }
    }

    public sealed class AppModelInfo
    {
        public AppModelInfo(string id, string displayName, string task, string format, string size, IEnumerable<string>? recommendedBackends = null, string? license = null, bool cached = false, string? location = null, string? sha256 = null, bool externalArtifact = false)
        {
            Id = ContractGuard.Id(id, nameof(id)); DisplayName = ContractGuard.Text(displayName, nameof(displayName)); Task = ContractGuard.Text(task, nameof(task)); Format = ContractGuard.Id(format, nameof(format)); Size = ContractGuard.Text(size, nameof(size)); RecommendedBackends = ContractGuard.List(recommendedBackends); License = ContractGuard.Optional(license); Cached = cached; Location = ContractGuard.Optional(location); Sha256 = ContractGuard.Optional(sha256); ExternalArtifact = externalArtifact;
        }
        public string Id { get; }
        public string DisplayName { get; }
        public string Task { get; }
        public string Format { get; }
        public string Size { get; }
        public IReadOnlyList<string> RecommendedBackends { get; }
        public string? License { get; }
        public bool Cached { get; }
        public string? Location { get; }
        public string? Sha256 { get; }
        public bool ExternalArtifact { get; }
    }

    public sealed class BackendRuntimeStatus
    {
        public BackendRuntimeStatus(string backendId, AppRuntimeState state, string message, string? loadedPath = null, string? version = null, string? apiLine = null, string? rid = null, string? processArchitecture = null, IEnumerable<string>? devices = null, IEnumerable<string>? missingItems = null, string? suggestedAction = null, IReadOnlyDictionary<string, string>? details = null, IEnumerable<RuntimeDiagnostic>? diagnostics = null)
        {
            BackendId = ContractGuard.Id(backendId, nameof(backendId)); State = state; Message = ContractGuard.Text(message, nameof(message)); LoadedPath = loadedPath; Version = version; ApiLine = apiLine; Rid = rid; ProcessArchitecture = processArchitecture; Devices = ContractGuard.List(devices); MissingItems = ContractGuard.List(missingItems); SuggestedAction = suggestedAction; Details = ContractGuard.Dictionary(details); Diagnostics = ContractGuard.List(diagnostics);
        }
        public string BackendId { get; }
        public AppRuntimeState State { get; }
        public string Message { get; }
        public string? LoadedPath { get; }
        public string? Version { get; }
        public string? ApiLine { get; }
        public string? Rid { get; }
        public string? ProcessArchitecture { get; }
        public IReadOnlyList<string> Devices { get; }
        public IReadOnlyList<string> MissingItems { get; }
        public string? SuggestedAction { get; }
        public IReadOnlyDictionary<string, string> Details { get; }
        public IReadOnlyList<RuntimeDiagnostic> Diagnostics { get; }
    }

    public enum DiagnosticSeverity { Information, Warning, Error, Critical }

    public sealed class RuntimeDiagnostic
    {
        public RuntimeDiagnostic(string code, DiagnosticSeverity severity, string message, string? backendId = null, string? modelId = null, IReadOnlyDictionary<string, string>? details = null)
        {
            Code = ContractGuard.Id(code, nameof(code)); Severity = severity; Message = ContractGuard.Text(message, nameof(message)); BackendId = backendId; ModelId = modelId; Details = ContractGuard.Dictionary(details);
        }
        public string Code { get; }
        public DiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string? BackendId { get; }
        public string? ModelId { get; }
        public IReadOnlyDictionary<string, string> Details { get; }
    }

    public sealed class ModelTensorInput
    {
        public ModelTensorInput(string name, string elementType, IEnumerable<long> shape, string? valuesJson = null, string? valuesFilePath = null)
        {
            Name = ContractGuard.Text(name, nameof(name));
            ElementType = ContractGuard.Id(elementType, nameof(elementType)).ToLowerInvariant();
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            var dimensions = new List<long>();
            foreach (var dimension in shape)
            {
                if (dimension < 0) throw new ArgumentOutOfRangeException(nameof(shape), "Runtime tensor dimensions must be non-negative.");
                dimensions.Add(dimension);
            }
            Shape = new ReadOnlyCollection<long>(dimensions);
            ValuesJson = ContractGuard.Optional(valuesJson);
            ValuesFilePath = ContractGuard.Optional(valuesFilePath);
            if ((ValuesJson == null) == (ValuesFilePath == null)) throw new ArgumentException("Exactly one tensor value source is required.");
        }
        public string Name { get; }
        public string ElementType { get; }
        public IReadOnlyList<long> Shape { get; }
        public string? ValuesJson { get; }
        public string? ValuesFilePath { get; }
    }

    public sealed class ModelRunRequest
    {
        public ModelRunRequest(AppOperationKind operation, string modelId, string backendId, string device = "cpu", string? inputPath = null, string? prompt = null, IReadOnlyDictionary<string, string>? options = null, TimeSpan? timeout = null, string? modelPath = null, string? modelFormat = null, string? modelSha256 = null, IEnumerable<ModelTensorInput>? tensorInputs = null, string outputFormat = "json")
        {
            Operation = operation; ModelId = ContractGuard.Id(modelId, nameof(modelId)); BackendId = ContractGuard.Id(backendId, nameof(backendId)); Device = ContractGuard.Text(device, nameof(device)); InputPath = ContractGuard.Optional(inputPath); Prompt = ContractGuard.Optional(prompt); Options = ContractGuard.Dictionary(options); Timeout = timeout ?? TimeSpan.FromMinutes(2); if (Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            ModelPath = ContractGuard.Optional(modelPath); ModelFormat = ContractGuard.Optional(modelFormat)?.ToLowerInvariant(); ModelSha256 = ContractGuard.Optional(modelSha256); TensorInputs = ContractGuard.List(tensorInputs); OutputFormat = ContractGuard.Id(outputFormat, nameof(outputFormat)).ToLowerInvariant();
        }
        public AppOperationKind Operation { get; }
        public string ModelId { get; }
        public string BackendId { get; }
        public string Device { get; }
        public string? InputPath { get; }
        public string? Prompt { get; }
        public IReadOnlyDictionary<string, string> Options { get; }
        public IReadOnlyDictionary<string, string> BackendOptions => Options;
        public TimeSpan Timeout { get; }
        public string? ModelPath { get; }
        public string? ModelFormat { get; }
        public string? ModelSha256 { get; }
        public IReadOnlyList<ModelTensorInput> TensorInputs { get; }
        public string OutputFormat { get; }
    }

    public sealed class ModelRunResult
    {
        public ModelRunResult(bool succeeded, AppErrorCode errorCode, string message, string? output = null, double preprocessMs = 0, double inferenceMs = 0, double postprocessMs = 0, string? correlationId = null, IEnumerable<RuntimeDiagnostic>? diagnostics = null, ModelRunMode runMode = ModelRunMode.Unspecified, BackendRuntimeStatus? runtimeStatus = null)
        { Succeeded = succeeded; ErrorCode = errorCode; Message = ContractGuard.Text(message, nameof(message)); Output = output; PreprocessMs = preprocessMs; InferenceMs = inferenceMs; PostprocessMs = postprocessMs; CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"); Diagnostics = ContractGuard.List(diagnostics); RunMode = runMode; RuntimeStatus = runtimeStatus; }
        public bool Succeeded { get; }
        public AppErrorCode ErrorCode { get; }
        public string Message { get; }
        public string? Output { get; }
        public double PreprocessMs { get; }
        public double InferenceMs { get; }
        public double PostprocessMs { get; }
        public double TotalMs => PreprocessMs + InferenceMs + PostprocessMs;
        public string CorrelationId { get; }
        public IReadOnlyList<RuntimeDiagnostic> Diagnostics { get; }
        public ModelRunMode RunMode { get; }
        public BackendRuntimeStatus? RuntimeStatus { get; }
    }

    public sealed class BenchmarkRequest
    {
        public BenchmarkRequest(string modelId, string backendId, int warmup = 3, int iterations = 20, string device = "cpu")
        { ModelId = ContractGuard.Id(modelId, nameof(modelId)); BackendId = ContractGuard.Id(backendId, nameof(backendId)); Device = ContractGuard.Text(device, nameof(device)); if (warmup < 0) throw new ArgumentOutOfRangeException(nameof(warmup)); if (iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations)); Warmup = warmup; Iterations = iterations; }
        public string ModelId { get; }
        public string BackendId { get; }
        public string Device { get; }
        public int Warmup { get; }
        public int Iterations { get; }
    }

    public sealed class BenchmarkReport
    {
        public BenchmarkReport(BenchmarkRequest request, bool available, string message, double p50Ms = 0, double p95Ms = 0, double throughput = 0, string? executionMode = null, IEnumerable<RuntimeDiagnostic>? diagnostics = null)
        { Request = request ?? throw new ArgumentNullException(nameof(request)); Available = available; Message = ContractGuard.Text(message, nameof(message)); P50Ms = p50Ms; P95Ms = p95Ms; Throughput = throughput; ExecutionMode = executionMode; Diagnostics = ContractGuard.List(diagnostics); }
        public BenchmarkRequest Request { get; }
        public bool Available { get; }
        public string Message { get; }
        public double P50Ms { get; }
        public double P95Ms { get; }
        public double Throughput { get; }
        public string? ExecutionMode { get; }
        public IReadOnlyList<RuntimeDiagnostic> Diagnostics { get; }
    }

    public sealed class PluginInstallState
    {
        public PluginInstallState(string pluginId, AppRuntimeState state, double progress = 0, string? message = null)
        { PluginId = ContractGuard.Id(pluginId, nameof(pluginId)); State = state; if (progress < 0 || progress > 1) throw new ArgumentOutOfRangeException(nameof(progress)); Progress = progress; Message = message; UpdatedUtc = DateTimeOffset.UtcNow; }
        public string PluginId { get; }
        public AppRuntimeState State { get; }
        public double Progress { get; }
        public string? Message { get; }
        public DateTimeOffset UpdatedUtc { get; }
    }

    public sealed class WorkerRequest
    {
        public WorkerRequest(WorkerMessageKind kind, string requestId, string? backendId = null, string? modelId = null, IReadOnlyDictionary<string, string>? payload = null)
        { Kind = kind; RequestId = ContractGuard.Id(requestId, nameof(requestId)); BackendId = backendId; ModelId = modelId; Payload = ContractGuard.Dictionary(payload); }
        public WorkerMessageKind Kind { get; }
        public string RequestId { get; }
        public string? BackendId { get; }
        public string? ModelId { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }
    }

    public sealed class WorkerResponse
    {
        public WorkerResponse(WorkerResponseKind kind, string requestId, bool succeeded = true, string? message = null, IReadOnlyDictionary<string, string>? payload = null)
        { Kind = kind; RequestId = ContractGuard.Id(requestId, nameof(requestId)); Succeeded = succeeded; Message = message; Payload = ContractGuard.Dictionary(payload); }
        public WorkerResponseKind Kind { get; }
        public string RequestId { get; }
        public bool Succeeded { get; }
        public string? Message { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }
    }

    public sealed class AppCapabilityDescriptor
    {
        public AppCapabilityDescriptor(string id, string displayName, AppBackendCapability capability, string description, bool available = false)
        { Id = ContractGuard.Id(id, nameof(id)); DisplayName = ContractGuard.Text(displayName, nameof(displayName)); Capability = capability; Description = ContractGuard.Text(description, nameof(description)); Available = available; }
        public string Id { get; }
        public string DisplayName { get; }
        public AppBackendCapability Capability { get; }
        public string Description { get; }
        public bool Available { get; }
    }

    internal static class ContractGuard
    {
        public static string Text(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", name); return value!.Trim(); }
        public static string? Optional(string? value) { return string.IsNullOrWhiteSpace(value) ? null : value!.Trim(); }
        public static string Id(string? value, string name) { var text = Text(value, name); foreach (var c in text) if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '/')) throw new ArgumentException("Invalid identifier.", name); return text; }
        public static IReadOnlyList<string> List(IEnumerable<string>? values) { var result = new List<string>(); if (values != null) foreach (var value in values) result.Add(Text(value, nameof(values))); return new ReadOnlyCollection<string>(result); }
        public static IReadOnlyList<T> List<T>(IEnumerable<T>? values) { var result = new List<T>(); if (values != null) foreach (var value in values) { if (value == null) throw new ArgumentException("Collection cannot contain null.", nameof(values)); result.Add(value); } return new ReadOnlyCollection<T>(result); }
        public static IReadOnlyDictionary<string, string> Dictionary(IReadOnlyDictionary<string, string>? values) { var result = new Dictionary<string, string>(StringComparer.Ordinal); if (values != null) foreach (var pair in values) result.Add(Id(pair.Key, nameof(values)), pair.Value ?? throw new ArgumentException("Dictionary values cannot be null.", nameof(values))); return new ReadOnlyDictionary<string, string>(result); }
    }
}
