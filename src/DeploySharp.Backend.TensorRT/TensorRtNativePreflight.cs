using System;
using System.Diagnostics;
using System.Text;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Performs a non-invasive driver health check before loading the TensorRT bridge. / 在加载 TensorRT bridge 前执行无侵入的驱动健康检查。</summary>
    internal static class TensorRtNativePreflight
    {
        private static readonly object Sync = new object();
        private static bool _checked;
        private static TensorRtBackendException? _failure;

        public static void Validate(ModelId modelId)
        {
            lock (Sync)
            {
                if (!_checked)
                {
                    _failure = ProbeDriver();
                    _checked = true;
                }
            }
            if (_failure != null)
                throw new TensorRtBackendException(_failure.ErrorCode, _failure.Message, _failure, modelId, operation: "native-preflight", technicalDetails: _failure.TechnicalDetails);
        }

        private static TensorRtBackendException? ProbeDriver()
        {
            string? skip = Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_SKIP_DRIVER_PREFLIGHT");
            if (string.Equals(skip, "1", StringComparison.Ordinal) || string.Equals(skip, "true", StringComparison.OrdinalIgnoreCase)) return null;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,driver_version --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using Process? process = Process.Start(startInfo);
                if (process == null) return null;
                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); } catch { }
                    return Failure("nvidia-smi did not exit within 5 seconds; the NVIDIA driver may be unresponsive.", "probe=nvidia-smi;status=timeout");
                }

                string output = (process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim();
                string compact = output.Replace((char)13, ' ').Replace((char)10, ' ');
                string lower = compact.ToLowerInvariant();
                if (process.ExitCode != 0 || lower.Contains("driver/library version mismatch") || lower.Contains("failed to initialize nvml") || lower.Contains("no devices were found") || lower.Contains("not found"))
                    return Failure("The NVIDIA driver is not healthy, so TensorRT was not loaded.", "probe=nvidia-smi;exitCode=" + process.ExitCode + ";output=" + Truncate(compact));
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Minimal containers may not include nvidia-smi even when
                // libcuda is mounted. Let the bridge perform its own probe.
                return null;
            }
            catch (Exception exception)
            {
                return Failure("The NVIDIA driver health check could not be completed; TensorRT was not loaded.", "probe=nvidia-smi;exception=" + exception.GetType().Name + ":" + exception.Message);
            }
        }

        private static TensorRtBackendException Failure(string message, string details) => new TensorRtBackendException(TensorRtErrorCodes.NativeRuntimeUnavailable, message, modelId: null, operation: "native-preflight", technicalDetails: details);

        private static string Truncate(string value) => value.Length <= 512 ? value : value.Substring(0, 512) + "...";
    }
}
