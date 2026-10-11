using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Performs a non-invasive driver health check before loading the TensorRT bridge. / 在加载 TensorRT bridge 前执行无侵入的驱动健康检查。</summary>
    internal static class TensorRtNativePreflight
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<string> RegisteredDependencyDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<IntPtr> DependencyDirectoryCookies = new List<IntPtr>();
        private static bool _checked;
        private static TensorRtBackendException? _failure;

        public static void Validate(ModelId modelId)
        {
            try
            {
                RegisterWindowsDependencyDirectories();
            }
            catch (Exception exception) when (exception is Win32Exception || exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.NativeRuntimeUnavailable,
                    "TensorRT native dependency directories could not be registered with the Windows loader.",
                    exception,
                    modelId,
                    operation: "native-preflight",
                    technicalDetails: exception.GetType().FullName + ": " + exception.Message);
            }
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

        private static void RegisterWindowsDependencyDirectories()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            lock (Sync)
            {
                AddRootAndSubdirectory(Environment.GetEnvironmentVariable("JYPPX_NATIVE_BRIDGE_PATH"), includeRoot: true, "");
                AddRootAndSubdirectory(Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT"), includeRoot: false, "bin", "lib");
                AddRootAndSubdirectory(Environment.GetEnvironmentVariable("JYPPX_CUDA_ROOT"), includeRoot: false, "bin", Path.Combine("bin", "x64"));
                AddRootAndSubdirectory(Environment.GetEnvironmentVariable("CUDA_PATH"), includeRoot: false, "bin", Path.Combine("bin", "x64"));
                AddRootAndSubdirectory(Environment.GetEnvironmentVariable("JYPPX_CUDNN_ROOT"), includeRoot: true, "bin");

                // cuDNN commonly lives beside, rather than beneath, CUDA_PATH.
                // Register only PATH entries that visibly contain cuDNN DLLs.
                string? processPath = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrWhiteSpace(processPath)) return;
                foreach (string entry in processPath.Split(Path.PathSeparator))
                {
                    string directory = entry.Trim().Trim('"');
                    if (directory.Length == 0 ||
                        (!File.Exists(Path.Combine(directory, "cudnn64_9.dll")) && !File.Exists(Path.Combine(directory, "cudnn64_8.dll")))) continue;
                    AddDirectory(directory);
                }
            }
        }

        private static void AddRootAndSubdirectory(string? configuredPath, bool includeRoot, params string[] subdirectories)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return;
            string root = configuredPath.Trim().Trim('"');
            if (File.Exists(root)) root = Path.GetDirectoryName(root) ?? string.Empty;
            if (root.Length == 0) return;
            if (includeRoot) AddDirectory(root);
            foreach (string subdirectory in subdirectories)
                AddDirectory(subdirectory.Length == 0 ? root : Path.Combine(root, subdirectory));
        }

        private static void AddDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            string fullPath = Path.GetFullPath(directory);
            if (!RegisteredDependencyDirectories.Add(fullPath)) return;
            IntPtr cookie = AddDllDirectory(fullPath);
            if (cookie == IntPtr.Zero)
            {
                RegisteredDependencyDirectories.Remove(fullPath);
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register a TensorRT native dependency directory with the Windows DLL loader: " + fullPath);
            }

            // TensorRT and CUDA load some transitive libraries lazily. Keep the
            // directory cookies alive for the process lifetime.
            DependencyDirectoryCookies.Add(cookie);
            PrependProcessSearchPath(fullPath);
        }

        private static void PrependProcessSearchPath(string directory)
        {
            string current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] entries = current.Split(Path.PathSeparator);
            var updated = new List<string> { directory };
            foreach (string entry in entries)
            {
                string candidate = entry.Trim().Trim('"');
                if (candidate.Length == 0 || string.Equals(candidate, directory, StringComparison.OrdinalIgnoreCase)) continue;
                bool alreadyPresent = false;
                try { alreadyPresent = string.Equals(Path.GetFullPath(candidate), directory, StringComparison.OrdinalIgnoreCase); }
                catch (Exception) { }
                if (!alreadyPresent) updated.Add(candidate);
            }
            Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator.ToString(), updated));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "AddDllDirectory")]
        private static extern IntPtr AddDllDirectory(string newDirectory);

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
