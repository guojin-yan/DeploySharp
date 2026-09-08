using System;
using System.Runtime.InteropServices;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OpenVINO.Internal
{
    internal static class OpenVinoNativePreflight
    {
        private const string RequiredVersionPrefix = "2026.2";

        public static string Validate(ModelArtifact artifact)
        {
            IntPtr buffer = Marshal.AllocHGlobal(IntPtr.Size * 2);
            bool nativeAllocated = false;
            try
            {
                Marshal.WriteIntPtr(buffer, 0, IntPtr.Zero);
                Marshal.WriteIntPtr(buffer, IntPtr.Size, IntPtr.Zero);
                int status = OvGetOpenVinoVersion(buffer);
                if (status != 0) throw new InvalidOperationException("ov_get_openvino_version returned status " + status + ".");
                nativeAllocated = true;
                string build = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(buffer, 0)) ?? string.Empty;
                if (!build.StartsWith(RequiredVersionPrefix, StringComparison.Ordinal))
                {
                    throw new OpenVinoBackendException(
                        DeploySharpErrorCodes.NativeRuntimeUnavailable,
                        "The loaded OpenVINO native runtime is incompatible. Install an OpenVINO 2026.2.x runtime package that matches JYPPX.OpenVINO.CSharp.API 3.3.1.",
                        modelId: artifact.ModelId,
                        operation: "native-preflight",
                        technicalDetails: "required=" + RequiredVersionPrefix + ";loaded=" + build);
                }
                return build;
            }
            catch (OpenVinoBackendException) { throw; }
            catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException || exception is MarshalDirectiveException || exception is InvalidOperationException)
            {
                throw new OpenVinoBackendException(
                    DeploySharpErrorCodes.NativeRuntimeUnavailable,
                    "No compatible OpenVINO native runtime is available. Install OpenVINO.runtime.win 2026.2.1 on Windows x64 or the matching platform runtime package.",
                    exception,
                    artifact.ModelId,
                    operation: "native-preflight",
                    technicalDetails: exception.ToString());
            }
            finally
            {
                if (nativeAllocated)
                {
                    try { OvVersionFree(buffer); }
                    catch (Exception) { }
                }
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("openvino_c", EntryPoint = "ov_get_openvino_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern int OvGetOpenVinoVersion(IntPtr version);

        [DllImport("openvino_c", EntryPoint = "ov_version_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void OvVersionFree(IntPtr version);
    }
}
