using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeploySharpApp.Application;
using DeploySharpApp.BackendHost.Protocol;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Infrastructure
{
    public interface IBackendHostWorkerClient
    {
        Task<WorkerResponse> SendAsync(WorkerRequest request, TimeSpan timeout, CancellationToken cancellationToken);
        Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken);
        Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Starts one short-lived BackendHost process per operation. Native backends never load into the web or net48 host.
    /// </summary>
    public sealed class BackendHostWorkerClient : IBackendHostWorkerClient
    {
        public const string HostPathEnvironmentVariable = "DEPLOYSHARPAPP_BACKEND_HOST";
        private readonly string? _hostPath;
        private readonly string? _dotnetPath;

        public BackendHostWorkerClient(string? hostPath = null, string? dotnetPath = null)
        {
            _hostPath = string.IsNullOrWhiteSpace(hostPath) ? null : hostPath!.Trim();
            _dotnetPath = string.IsNullOrWhiteSpace(dotnetPath) ? null : dotnetPath!.Trim();
        }

        public async Task<WorkerResponse> SendAsync(WorkerRequest request, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            using Process process = StartProcess();

            try
            {
                await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, request, cancellationToken).ConfigureAwait(false);
                WorkerResponse? response = await ReadTerminalResponseAsync(process, request.RequestId, timeout, progress: null, diagnostics: null, cancellationToken).ConfigureAwait(false);
                if (response == null) throw new EndOfStreamException("BackendHost closed stdout before returning a response.");

                await SendShutdownAsync(process, request.RequestId).ConfigureAwait(false);
                return response;
            }
            finally
            {
                Kill(process);
            }
        }

        public async Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string requestId = "run-" + Guid.NewGuid().ToString("N");
            Process? process = null;
            var streamDiagnostics = new List<RuntimeDiagnostic>();
            DateTime deadline = DateTime.UtcNow + request.Timeout;
            try
            {
                process = StartProcess();
                progress?.Report(0.05);
                WorkerResponse handshake = await ReadRequestResponseAsync(process, CreateHandshake(requestId, request.BackendId), requestId + "-handshake", Remaining(deadline), progress, streamDiagnostics, cancellationToken).ConfigureAwait(false);
                if (!IsSuccessfulHandshake(handshake))
                    return Failure(request, AppErrorCode.WorkerRequired, "BackendHost Worker 未通过握手，未执行 native 推理。", "DSAPP-WORKER-HANDSHAKE-FAILED", handshake.Message, hostUnavailable: false);

                progress?.Report(0.2);
                WorkerResponse response = await ReadRequestResponseAsync(process, CreateInference(request, requestId), requestId, Remaining(deadline), progress, streamDiagnostics, cancellationToken).ConfigureAwait(false);
                progress?.Report(1);
                return MapRunResponse(request, response, streamDiagnostics);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await SendCancelAsync(process, requestId).ConfigureAwait(false);
                return Failure(request, AppErrorCode.Cancelled, "Worker 操作已取消。", "DSAPP-WORKER-CANCELLED", null, hostUnavailable: false);
            }
            catch (TimeoutException exception)
            {
                await SendCancelAsync(process, requestId).ConfigureAwait(false);
                return Failure(request, AppErrorCode.TimedOut, "Worker 操作超时，进程已终止。", "DSAPP-WORKER-TIMED-OUT", exception.Message, hostUnavailable: false);
            }
            catch (WorkerHostUnavailableException exception)
            {
                return Failure(request, AppErrorCode.WorkerRequired, "未配置可启动的 BackendHost Worker，native 后端没有在当前进程内加载。", "DSAPP-WORKER-HOST-NOT-CONFIGURED", exception.Message, hostUnavailable: true);
            }
            catch (Exception exception)
            {
                return Failure(request, AppErrorCode.WorkerFailed, "BackendHost Worker 启动或通信失败。", "DSAPP-WORKER-FAILED", exception.Message, hostUnavailable: false);
            }
            finally
            {
                if (process != null)
                {
                    await SendShutdownAsync(process, requestId).ConfigureAwait(false);
                    Kill(process);
                    process.Dispose();
                }
            }
        }

        public async Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string requestId = "bench-" + Guid.NewGuid().ToString("N");
            Process? process = null;
            var streamDiagnostics = new List<RuntimeDiagnostic>();
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromMinutes(5);
            try
            {
                process = StartProcess();
                progress?.Report(0.05);
                WorkerResponse handshake = await ReadRequestResponseAsync(process, CreateHandshake(requestId, request.BackendId), requestId + "-handshake", Remaining(deadline), progress, streamDiagnostics, cancellationToken).ConfigureAwait(false);
                if (!IsSuccessfulHandshake(handshake)) return BenchmarkFailure(request, "BackendHost Worker 未通过握手。", "DSAPP-WORKER-HANDSHAKE-FAILED", handshake.Message);
                progress?.Report(0.2);
                var payload = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["warmup"] = request.Warmup.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["iterations"] = request.Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["device"] = request.Device
                };
                WorkerResponse response = await ReadRequestResponseAsync(process, new WorkerRequest(WorkerMessageKind.Benchmark, requestId, request.BackendId, request.ModelId, payload), requestId, Remaining(deadline), progress, streamDiagnostics, cancellationToken).ConfigureAwait(false);
                progress?.Report(1);
                if (response.Succeeded && response.Kind == WorkerResponseKind.Result)
                    return new BenchmarkReport(request, true, response.Message ?? "Worker benchmark completed.", ParseDouble(response.Payload, "p50Ms"), ParseDouble(response.Payload, "p95Ms"), ParseDouble(response.Payload, "throughput"), AppExecutionMode.Worker.ToString(), streamDiagnostics);
                BenchmarkReport failure = BenchmarkFailure(request, response.Message ?? "Worker backend 尚未提供 benchmark adapter。", "DSAPP-WORKER-BENCHMARK-UNAVAILABLE", response.Message);
                return streamDiagnostics.Count == 0 ? failure : new BenchmarkReport(request, false, failure.Message, executionMode: failure.ExecutionMode, diagnostics: streamDiagnostics.Concat(failure.Diagnostics));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { await SendCancelAsync(process, requestId).ConfigureAwait(false); return BenchmarkFailure(request, "Worker benchmark 已取消。", "DSAPP-WORKER-CANCELLED", null); }
            catch (TimeoutException exception) { await SendCancelAsync(process, requestId).ConfigureAwait(false); return BenchmarkFailure(request, "Worker benchmark 超时。", "DSAPP-WORKER-TIMED-OUT", exception.Message); }
            catch (WorkerHostUnavailableException exception) { return BenchmarkFailure(request, "未配置可启动的 BackendHost Worker。", "DSAPP-WORKER-HOST-NOT-CONFIGURED", exception.Message); }
            catch (Exception exception) { return BenchmarkFailure(request, "BackendHost Worker benchmark 通信失败。", "DSAPP-WORKER-FAILED", exception.Message); }
            finally
            {
                if (process != null)
                {
                    await SendShutdownAsync(process, requestId).ConfigureAwait(false);
                    Kill(process);
                    process.Dispose();
                }
            }
        }

        private Process StartProcess()
        {
            ProcessStartInfo startInfo = CreateStartInfo() ?? throw new WorkerHostUnavailableException(ResolveHostPath());
            var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("BackendHost process could not be started.");
            }
            return process;
        }

        private static async Task<WorkerResponse> ReadRequestResponseAsync(Process process, WorkerRequest request, string expectedRequestId, TimeSpan timeout, IProgress<double>? progress, List<RuntimeDiagnostic> diagnostics, CancellationToken cancellationToken)
        {
            await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, request, cancellationToken).ConfigureAwait(false);
            WorkerResponse? response = await ReadTerminalResponseAsync(process, expectedRequestId, timeout, progress, diagnostics, cancellationToken).ConfigureAwait(false);
            if (response == null) throw new EndOfStreamException("BackendHost closed stdout before returning a response.");
            return response;
        }

        private static async Task<WorkerResponse?> ReadTerminalResponseAsync(Process process, string expectedRequestId, TimeSpan timeout, IProgress<double>? progress, List<RuntimeDiagnostic>? diagnostics, CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException("BackendHost response timed out.");
                WorkerResponse? response = await WaitForResponseAsync(WorkerProtocol.ReadResponseAsync(process.StandardOutput, cancellationToken), remaining, cancellationToken).ConfigureAwait(false);
                if (response == null) return null;
                if (!string.Equals(response.RequestId, expectedRequestId, StringComparison.Ordinal))
                {
                    diagnostics?.Add(new RuntimeDiagnostic("DSAPP-WORKER-CORRELATION-MISMATCH", DiagnosticSeverity.Warning, "Worker returned a response for an unexpected request id.", details: new Dictionary<string, string> { ["expected"] = expectedRequestId, ["actual"] = response.RequestId }));
                    continue;
                }
                if (response.Kind == WorkerResponseKind.Progress)
                {
                    if (TryGetProgress(response, out double value)) progress?.Report(Math.Max(0, Math.Min(1, value)));
                    continue;
                }
                if (response.Kind == WorkerResponseKind.Log)
                {
                    diagnostics?.Add(CreateLogDiagnostic(response));
                    continue;
                }
                return response;
            }
        }

        private static async Task SendShutdownAsync(Process process, string requestId)
        {
            if (process.HasExited) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try { await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Shutdown, "shutdown-" + requestId), timeout.Token).ConfigureAwait(false); }
            catch (Exception) { }
        }

        private static async Task SendCancelAsync(Process? process, string requestId)
        {
            if (process == null || process.HasExited) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            try { await WorkerProtocol.WriteRequestAsync(process.StandardInput.BaseStream, new WorkerRequest(WorkerMessageKind.Cancel, "cancel-" + requestId), timeout.Token).ConfigureAwait(false); }
            catch (Exception) { }
        }

        private static TimeSpan Remaining(DateTime deadline)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            return remaining <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining;
        }

        private static bool TryGetProgress(WorkerResponse response, out double value)
        {
            value = 0;
            string? text = response.Payload.TryGetValue("value", out string? valueText) ? valueText : response.Payload.TryGetValue("progress", out string? progressText) ? progressText : null;
            return text != null && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static RuntimeDiagnostic CreateLogDiagnostic(WorkerResponse response)
        {
            string severityText = response.Payload.TryGetValue("level", out string? level) ? level : "Information";
            DiagnosticSeverity severity = severityText.Equals("error", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Error : severityText.Equals("warning", StringComparison.OrdinalIgnoreCase) ? DiagnosticSeverity.Warning : DiagnosticSeverity.Information;
            string message = response.Message ?? (response.Payload.TryGetValue("message", out string? payloadMessage) ? payloadMessage : "Worker log event.");
            string code = response.Payload.TryGetValue("diagnosticCode", out string? diagnosticCode) && !string.IsNullOrWhiteSpace(diagnosticCode) ? diagnosticCode : "DSAPP-WORKER-LOG";
            return new RuntimeDiagnostic(code, severity, message, details: response.Payload);
        }

        private ProcessStartInfo? CreateStartInfo()
        {
            string? hostPath = ResolveHostPath();
            if (string.IsNullOrWhiteSpace(hostPath)) return null;
            string fullPath = hostPath!;
            bool looksLikePath = Path.IsPathRooted(fullPath) || fullPath.IndexOf(Path.DirectorySeparatorChar) >= 0 || fullPath.IndexOf(Path.AltDirectorySeparatorChar) >= 0 || !string.IsNullOrWhiteSpace(Path.GetExtension(fullPath));
            if (looksLikePath)
            {
                fullPath = Path.GetFullPath(fullPath);
                if (!File.Exists(fullPath)) return null;
            }
            bool managedHost = string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase);
            var info = new ProcessStartInfo
            {
                FileName = managedHost ? (_dotnetPath ?? "dotnet") : fullPath,
                Arguments = managedHost ? Quote(fullPath) : string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory
            };
            return info;
        }

        private string? ResolveHostPath()
        {
            string? configured = _hostPath ?? Environment.GetEnvironmentVariable(HostPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "DeploySharpApp.BackendHost.exe"),
                Path.Combine(AppContext.BaseDirectory, "DeploySharpApp.BackendHost.dll")
            };
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (var depth = 0; directory != null && depth < 7; depth++, directory = directory.Parent)
            {
                candidates.Add(Path.Combine(directory.FullName, "DeploySharpApp.BackendHost", "bin", "Debug", "net10.0", "DeploySharpApp.BackendHost.dll"));
                candidates.Add(Path.Combine(directory.FullName, "DeploySharpApp.BackendHost.dll"));
                candidates.Add(Path.Combine(directory.FullName, "src", "DeploySharpApp.BackendHost", "bin", "Debug", "net10.0", "DeploySharpApp.BackendHost.dll"));
            }
            return candidates.FirstOrDefault(File.Exists);
        }

        private static WorkerRequest CreateHandshake(string requestId, string backendId) => new(WorkerMessageKind.Handshake, requestId + "-handshake", backendId, payload: new Dictionary<string, string> { ["protocolVersion"] = WorkerProtocol.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ["client"] = "DeploySharpApp.Infrastructure" });

        private static WorkerRequest CreateInference(ModelRunRequest request, string requestId)
        {
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operation"] = request.Operation.ToString(),
                ["device"] = request.Device,
                ["outputFormat"] = request.OutputFormat,
                ["timeoutMs"] = request.Timeout.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["optionsJson"] = JsonSerializer.Serialize(request.Options),
                ["tensorInputsJson"] = JsonSerializer.Serialize(request.TensorInputs)
            };
            Add(payload, "inputPath", request.InputPath); Add(payload, "prompt", request.Prompt); Add(payload, "modelPath", request.ModelPath); Add(payload, "modelFormat", request.ModelFormat); Add(payload, "modelSha256", request.ModelSha256);
            return new WorkerRequest(WorkerMessageKind.Inference, requestId, request.BackendId, request.ModelId, payload);
        }

        private static void Add(IDictionary<string, string> payload, string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) payload[key] = value!; }

        private static bool IsSuccessfulHandshake(WorkerResponse response) => response.Kind == WorkerResponseKind.Handshake && response.Succeeded;

        private static ModelRunResult MapRunResponse(ModelRunRequest request, WorkerResponse response, IReadOnlyList<RuntimeDiagnostic> streamDiagnostics)
        {
            if (response.Succeeded && response.Kind == WorkerResponseKind.Result)
                return new ModelRunResult(true, AppErrorCode.None, response.Message ?? "Worker operation completed.", response.Payload.TryGetValue("output", out string? output) ? output : null, ParseDouble(response.Payload, "preprocessMs"), ParseDouble(response.Payload, "inferenceMs"), ParseDouble(response.Payload, "postprocessMs"), diagnostics: streamDiagnostics, runMode: ModelRunMode.Worker);
            bool adapterMissing = response.Message?.IndexOf("adapter", StringComparison.OrdinalIgnoreCase) >= 0 || response.Message?.IndexOf("minimal", StringComparison.OrdinalIgnoreCase) >= 0;
            ModelRunResult failure = Failure(request, adapterMissing ? AppErrorCode.WorkerRequired : AppErrorCode.WorkerFailed, response.Message ?? "Worker backend did not return a result.", adapterMissing ? "DSAPP-WORKER-ADAPTER-UNAVAILABLE" : "DSAPP-WORKER-RESPONSE-FAILED", response.Message, hostUnavailable: false);
            return streamDiagnostics.Count == 0 ? failure : WithAdditionalDiagnostics(failure, streamDiagnostics);
        }

        private static ModelRunResult WithAdditionalDiagnostics(ModelRunResult result, IEnumerable<RuntimeDiagnostic> additional)
        {
            RuntimeDiagnostic[] diagnostics = result.Diagnostics.Concat(additional).ToArray();
            return new ModelRunResult(result.Succeeded, result.ErrorCode, result.Message, result.Output, result.PreprocessMs, result.InferenceMs, result.PostprocessMs, result.CorrelationId, diagnostics, result.RunMode, result.RuntimeStatus);
        }

        private static ModelRunResult Failure(ModelRunRequest request, AppErrorCode code, string message, string diagnosticCode, string? technicalDetail, bool hostUnavailable)
        {
            var details = new Dictionary<string, string>(StringComparer.Ordinal) { ["workerHost"] = hostUnavailable ? "not-configured" : "configured-or-unresolved" };
            if (!string.IsNullOrWhiteSpace(technicalDetail)) details["technicalDetail"] = technicalDetail!;
            var diagnostic = new RuntimeDiagnostic(diagnosticCode, code == AppErrorCode.WorkerFailed ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning, message, request.BackendId, request.ModelId, details);
            var status = new BackendRuntimeStatus(request.BackendId, AppRuntimeState.Unavailable, message, suggestedAction: hostUnavailable ? "配置 DEPLOYSHARPAPP_BACKEND_HOST 指向 net10 BackendHost，再重试。" : "检查 BackendHost stderr、Worker 协议版本和后端 adapter。", details: details, diagnostics: new[] { diagnostic });
            return new ModelRunResult(false, code, message, diagnostics: new[] { diagnostic }, runMode: ModelRunMode.Worker, runtimeStatus: status);
        }

        private static BenchmarkReport BenchmarkFailure(BenchmarkRequest request, string message, string code, string? detail)
        {
            var details = string.IsNullOrWhiteSpace(detail) ? null : new Dictionary<string, string> { ["technicalDetail"] = detail! };
            var diagnostic = new RuntimeDiagnostic(code, DiagnosticSeverity.Warning, message, request.BackendId, request.ModelId, details);
            return new BenchmarkReport(request, false, message, executionMode: AppExecutionMode.Worker.ToString(), diagnostics: new[] { diagnostic });
        }

        private static double ParseDouble(IReadOnlyDictionary<string, string> payload, string key) => payload.TryGetValue(key, out string? value) && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result) ? result : 0;

        private static async Task<WorkerResponse?> WaitForResponseAsync(Task<WorkerResponse?> responseTask, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task timeoutTask = Task.Delay(timeout, timeoutCancellation.Token);
            Task completed = await Task.WhenAny(responseTask, timeoutTask).ConfigureAwait(false);
            if (completed != responseTask)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                throw new TimeoutException("BackendHost response timed out after " + timeout.TotalSeconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " seconds.");
            }
            timeoutCancellation.Cancel();
            return await responseTask.ConfigureAwait(false);
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private static void Kill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); } catch (Exception) { }
            try { process.WaitForExit(1000); } catch (Exception) { }
        }
    }

    public sealed class WorkerHostUnavailableException : InvalidOperationException
    {
        public WorkerHostUnavailableException(string? hostPath) : base(string.IsNullOrWhiteSpace(hostPath) ? "BackendHost path is not configured or discoverable." : "BackendHost was not found: " + hostPath) { }
    }
}
