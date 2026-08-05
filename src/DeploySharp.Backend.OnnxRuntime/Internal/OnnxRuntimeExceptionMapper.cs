using System;
using System.Threading;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using Microsoft.ML.OnnxRuntime;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime.Internal
{
    internal static class OnnxRuntimeExceptionMapper
    {
        public static Exception Map(Exception exception, ModelArtifact artifact, string operation, CancellationToken cancellationToken = default(CancellationToken), string? tensorName = null)
        {
            if (exception is OnnxRuntimeBackendException) return exception;
            if (cancellationToken.IsCancellationRequested || exception is OperationCanceledException || IsNativeTermination(exception))
            {
                return new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.Cancelled, "ONNX Runtime inference was cancelled.", exception, artifact.ModelId, tensorName, operation, exception.ToString());
            }
            if (ContainsNativeFailure(exception))
            {
                return new OnnxRuntimeBackendException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "No compatible ONNX Runtime native library is available. Install Microsoft.ML.OnnxRuntime 1.28.0 or another matching official runtime package for the current RID.", exception, artifact.ModelId, tensorName, operation, exception.ToString());
            }
            if (IsProviderFailure(exception))
            {
                return new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ExecutionProviderUnavailable, "The requested ONNX Runtime execution provider is unavailable or incompatible.", exception, artifact.ModelId, tensorName, operation, exception.ToString());
            }
            string code = string.Equals(operation, "load", StringComparison.Ordinal) ? OnnxRuntimeErrorCodes.ModelLoadFailed : OnnxRuntimeErrorCodes.InferenceFailed;
            string message = string.Equals(operation, "load", StringComparison.Ordinal) ? "ONNX Runtime could not load the ONNX model." : "ONNX Runtime inference failed.";
            return new OnnxRuntimeBackendException(code, message, exception, artifact.ModelId, tensorName, operation, exception.ToString());
        }

        private static bool ContainsNativeFailure(Exception exception)
        {
            Exception? current = exception;
            while (current != null)
            {
                if (current is DllNotFoundException || current is EntryPointNotFoundException || current is BadImageFormatException || current is TypeInitializationException && current.InnerException != null && ContainsNativeFailure(current.InnerException)) return true;
                current = current.InnerException;
            }
            return false;
        }

        private static bool IsNativeTermination(Exception exception)
        {
            if (!(exception is OnnxRuntimeException)) return false;
            string message = exception.Message ?? string.Empty;
            return message.IndexOf("terminate", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsProviderFailure(Exception exception)
        {
            string message = exception.Message ?? string.Empty;
            return message.IndexOf("execution provider", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("provider", StringComparison.OrdinalIgnoreCase) >= 0 && message.IndexOf("available", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
