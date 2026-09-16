using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime.Internal
{
    internal static class OnnxRuntimeNativePreflight
    {
        private static readonly object LoadSync = new object();
        private static IntPtr _nativeHandle;
        private static string? _nativePath;

        public static void Validate(ModelArtifact artifact)
        {
            try
            {
                // Querying the native C ABI before any ORT managed static initializer prevents an incompatible machine-wide library from causing an uncatchable access violation. / 在任何 ORT 托管静态初始化器之前查询原生 C ABI，可防止不兼容的系统级库造成无法捕获的访问冲突。
                IntPtr apiBase = GetApiBase();
                if (apiBase == IntPtr.Zero) throw new InvalidOperationException("OrtGetApiBase returned null.");
                IntPtr versionFunction = Marshal.ReadIntPtr(apiBase, IntPtr.Size);
                if (versionFunction == IntPtr.Zero) throw new InvalidOperationException("OrtApiBase.GetVersionString is unavailable.");
                var getVersion = (GetVersionStringDelegate)Marshal.GetDelegateForFunctionPointer(versionFunction, typeof(GetVersionStringDelegate));
                string? version = Marshal.PtrToStringAnsi(getVersion());
                if (!string.Equals(version, RequiredVersion, StringComparison.Ordinal))
                {
                    throw new OnnxRuntimeBackendException(
                        DeploySharpErrorCodes.NativeRuntimeUnavailable,
                        "The loaded ONNX Runtime native library does not match the managed adapter. Install the matching Microsoft.ML.OnnxRuntime package for the current RID and remove older machine-wide copies from native search paths.",
                        modelId: artifact.ModelId,
                        operation: "native-preflight",
                        technicalDetails: "required=" + RequiredVersion + ";loaded=" + (version ?? "unknown") + ";nativePath=" + (_nativePath ?? "default-search"));
                }
            }
            catch (OnnxRuntimeBackendException) { throw; }
            catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException || exception is MarshalDirectiveException || exception is InvalidOperationException)
            {
                throw new OnnxRuntimeBackendException(
                    DeploySharpErrorCodes.NativeRuntimeUnavailable,
                    "No compatible ONNX Runtime native library is available. Install the matching Microsoft.ML.OnnxRuntime package for the current RID.",
                    exception,
                    artifact.ModelId,
                    operation: "native-preflight",
                    technicalDetails: exception.ToString());
            }
        }

        /// <summary>Validates that the CUDA execution-provider binary is present. ORT initializes the provider host and loads CUDA dependencies during session creation. / 验证 CUDA 执行提供程序文件存在，由 ORT 在创建会话时初始化插件宿主并加载 CUDA 依赖。</summary>
        public static void ValidateCudaProvider(ModelArtifact artifact)
        {
            string providerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "onnxruntime_providers_cuda.dll" : "libonnxruntime_providers_cuda.so";
            string? providerPath = null;
            foreach (string candidate in CandidateProviderPaths(providerName))
            {
                if (File.Exists(candidate)) { providerPath = candidate; break; }
            }

            if (providerPath == null)
            {
                throw new OnnxRuntimeBackendException(
                    DeploySharpErrorCodes.NativeRuntimeUnavailable,
                    "The ONNX Runtime CUDA execution-provider binary is missing. Install the matching Microsoft.ML.OnnxRuntime.Gpu package for the current RID.",
                    modelId: artifact.ModelId,
                    operation: "cuda-provider-preflight",
                    technicalDetails: "required=" + providerName + ";searched=" + string.Join("|", CandidateProviderPaths(providerName)));
            }

            // The provider is an ORT plugin, not an independently loadable
            // library. ORT loads its shared provider host and initializes the
            // host API before binding CUDA. Loading/unloading it here can
            // fail with a false dependency/initialization error (including
            // Win32 error 1114), or duplicate expensive CUDA initialization.
            // Check presence here; session creation performs the authoritative
            // dependency check and preserves ORT's native error details.
        }

#if NETSTANDARD2_0
        // NativeLibrary was introduced after netstandard2.0. Keep the same ABI
        // preflight on that target through a direct, platform loader-resolved
        // import; the managed package still leaves native runtime ownership to the
        // application. DllNotFound/EntryPoint/BadImageFormat are mapped by Validate.
        private static IntPtr GetApiBase() => OrtGetApiBaseNative();

        private static void EnsureNativeLoaded()
        {
            if (_nativeHandle != IntPtr.Zero) return;
            lock (LoadSync)
            {
                if (_nativeHandle != IntPtr.Zero) return;
                _nativeHandle = new IntPtr(1);
                _nativePath = "default-import";
            }
        }

        [DllImport("onnxruntime", CallingConvention = CallingConvention.Cdecl, EntryPoint = "OrtGetApiBase")]
        private static extern IntPtr OrtGetApiBaseNative();
#else
        private static IntPtr GetApiBase()
        {
            EnsureNativeLoaded();
            IntPtr export = NativeLibrary.GetExport(_nativeHandle, "OrtGetApiBase");
            var getApiBase = (OrtGetApiBaseDelegate)Marshal.GetDelegateForFunctionPointer(export, typeof(OrtGetApiBaseDelegate));
            return getApiBase();
        }

        private static void EnsureNativeLoaded()
        {
            if (_nativeHandle != IntPtr.Zero) return;
            lock (LoadSync)
            {
                if (_nativeHandle != IntPtr.Zero) return;
                foreach (string path in CandidatePaths())
                {
                    if (!File.Exists(path)) continue;
                    if (!NativeLibrary.TryLoad(path, out IntPtr handle)) continue;
                    _nativeHandle = handle;
                    _nativePath = path;
                    return;
                }

                string libraryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "onnxruntime" : "libonnxruntime.so";
                if (NativeLibrary.TryLoad(libraryName, typeof(OnnxRuntimeNativePreflight).Assembly, DllImportSearchPath.SafeDirectories, out IntPtr fallback))
                {
                    _nativeHandle = fallback;
                    _nativePath = "default-search";
                    return;
                }
                throw new DllNotFoundException(NativeFileName() + " was not found in the application-local runtime directories or safe native search paths.");
            }
        }
#endif

        internal static string ManagedVersion
        {
            get
            {
                Version? version = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly.GetName().Version;
                return version == null ? "unknown" : version.Major + "." + version.Minor + "." + version.Build;
            }
        }

        private static string RequiredVersion => ManagedVersion;

        private static IEnumerable<string> CandidatePaths()
        {
            string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                string value = configured.Trim();
                yield return Path.GetFullPath(Directory.Exists(value) ? Path.Combine(value, NativeFileName()) : value);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in SearchRoots())
            {
                string direct = Path.Combine(root, NativeFileName());
                if (seen.Add(direct)) yield return direct;
                foreach (string rid in RuntimeIdentifiers())
                {
                    string runtime = Path.Combine(root, "runtimes", rid, "native", NativeFileName());
                    if (seen.Add(runtime)) yield return runtime;
                }
            }
        }

        private static IEnumerable<string> CandidateProviderPaths(string providerName)
        {
            string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                string value = configured.Trim();
                yield return Path.GetFullPath(Directory.Exists(value) ? Path.Combine(value, providerName) : value);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in SearchRoots())
            {
                string direct = Path.Combine(root, providerName);
                if (seen.Add(direct)) yield return direct;
                foreach (string rid in RuntimeIdentifiers())
                {
                    string runtime = Path.Combine(root, "runtimes", rid, "native", providerName);
                    if (seen.Add(runtime)) yield return runtime;
                }
            }
        }

        private static string NativeFileName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "onnxruntime.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "libonnxruntime.dylib";
            return "libonnxruntime.so";
        }

        private static IEnumerable<string> RuntimeIdentifiers()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield return "win-x64";
                yield return "win-arm64";
                yield break;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return "osx-x64";
                yield return "osx-arm64";
                yield break;
            }
            yield return "linux-x64";
            // Self-contained Linux publishes may use a distro-specific RID
            // even though the ORT package stores its native assets under a
            // generic linux-x64 folder. Include both forms so preflight works
            // before the managed loader performs its own RID probing.
            yield return "ubuntu.22.04-x64";
            yield return "ubuntu.22-x64";
            yield return "linux-arm64";
            yield return "ubuntu.22.04-arm64";
            yield return "ubuntu.22-arm64";
        }

        private static IEnumerable<string> SearchRoots()
        {
            string current = Path.GetFullPath(AppContext.BaseDirectory);
            for (int depth = 0; depth < 5; depth++)
            {
                yield return current;
                DirectoryInfo? parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            string? assemblyDirectory = Path.GetDirectoryName(typeof(OnnxRuntimeNativePreflight).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory)) yield return Path.GetFullPath(assemblyDirectory);
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr OrtGetApiBaseDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr GetVersionStringDelegate();
    }
}
