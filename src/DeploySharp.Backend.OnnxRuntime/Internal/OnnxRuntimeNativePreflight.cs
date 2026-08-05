using System;
using System.Runtime.InteropServices;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime.Internal
{
    internal static class OnnxRuntimeNativePreflight
    {
        private const string RequiredVersion = "1.28.0";

        public static void Validate(ModelArtifact artifact)
        {
            try
            {
                // Querying the native C ABI before any ORT managed static initializer prevents an incompatible machine-wide library from causing an uncatchable access violation. / 在任何 ORT 托管静态初始化器之前查询原生 C ABI，可防止不兼容的系统级库造成无法捕获的访问冲突。
                IntPtr apiBase = OrtGetApiBase();
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
                        technicalDetails: "required=" + RequiredVersion + ";loaded=" + (version ?? "unknown"));
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

        [DllImport("onnxruntime", EntryPoint = "OrtGetApiBase", CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr OrtGetApiBase();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr GetVersionStringDelegate();
    }
}
