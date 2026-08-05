using System;
using System.Threading;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OpenVINO.Internal
{
    internal static class OpenVinoExceptionMapper
    {
        public static Exception Map(Exception exception, ModelArtifact artifact, string operation, string device, CancellationToken cancellationToken = default(CancellationToken), string? tensorName = null)
        {
            if (cancellationToken.IsCancellationRequested || exception is OperationCanceledException || ContainsText(exception, "cancel"))
                return new OpenVinoBackendException(OpenVinoErrorCodes.Cancelled, "OpenVINO inference was cancelled.", exception, artifact.ModelId, tensorName, operation, device, exception.ToString());
            if (exception is OpenVinoBackendException) return exception;
            if (ContainsNativeFailure(exception))
                return new OpenVinoBackendException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "No compatible OpenVINO native runtime is available for the current process architecture.", exception, artifact.ModelId, tensorName, operation, device, exception.ToString());
            if (ContainsText(exception, "device") || ContainsText(exception, "plugin"))
                return new OpenVinoBackendException(OpenVinoErrorCodes.DeviceUnavailable, "The requested OpenVINO device or plug-in is unavailable.", exception, artifact.ModelId, tensorName, operation, device, exception.ToString());
            string code = operation.StartsWith("load", StringComparison.Ordinal) || operation.StartsWith("compile", StringComparison.Ordinal) ? OpenVinoErrorCodes.ModelLoadFailed : OpenVinoErrorCodes.InferenceFailed;
            string message = code == OpenVinoErrorCodes.ModelLoadFailed ? "OpenVINO could not read or compile the model." : "OpenVINO inference failed.";
            return new OpenVinoBackendException(code, message, exception, artifact.ModelId, tensorName, operation, device, exception.ToString());
        }

        private static bool ContainsNativeFailure(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is DllNotFoundException || current is EntryPointNotFoundException || current is BadImageFormatException) return true;
            }
            return false;
        }

        private static bool ContainsText(Exception exception, string value)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if ((current.Message ?? string.Empty).IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
