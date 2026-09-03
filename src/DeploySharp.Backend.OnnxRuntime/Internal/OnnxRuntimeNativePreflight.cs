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
        private const string RequiredVersion = "1.28.0";
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
                        "The loaded ONNX Runtime native library does not match the managed adapter. Install Microsoft.ML.OnnxRuntime 1.28.0 for the current RID and remove older machine-wide copies from native search paths.",
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
                    "No compatible ONNX Runtime native library is available. Install Microsoft.ML.OnnxRuntime 1.28.0 for the current RID.",
                    exception,
                    artifact.ModelId,
                    operation: "native-preflight",
                    technicalDetails: exception.ToString());
            }
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

                if (NativeLibrary.TryLoad("onnxruntime", typeof(OnnxRuntimeNativePreflight).Assembly, DllImportSearchPath.SafeDirectories, out IntPtr fallback))
                {
                    _nativeHandle = fallback;
                    _nativePath = "default-search";
                    return;
                }
                throw new DllNotFoundException("onnxruntime.dll was not found in the application-local runtime directories or safe native search paths.");
            }
        }
#endif

        private static IEnumerable<string> CandidatePaths()
        {
            string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                string value = configured.Trim();
                yield return Path.GetFullPath(Directory.Exists(value) ? Path.Combine(value, "onnxruntime.dll") : value);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in SearchRoots())
            {
                string direct = Path.Combine(root, "onnxruntime.dll");
                if (seen.Add(direct)) yield return direct;
                string runtime = Path.Combine(root, "runtimes", "win-x64", "native", "onnxruntime.dll");
                if (seen.Add(runtime)) yield return runtime;
            }
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
