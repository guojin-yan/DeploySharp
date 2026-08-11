using System;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using LLama.Exceptions;

namespace JYPPX.DeploySharp.Backends.LlamaSharp.Internal
{
    internal static class LlamaSharpExceptionMapper
    {
        private static readonly BackendId Backend = new BackendId("llamasharp");

        public static Exception Map(Exception exception, ModelArtifact artifact, string operation, bool loading = false)
        {
            if (exception is DeploySharpException || exception is OperationCanceledException) return exception;
            if (exception is ContextOverflowException || ContainsContextLimit(exception))
            {
                return new DeploySharpException(DeploySharpErrorCodes.ContextLimitExceeded, "The GGUF model context window cannot satisfy the request.", exception, Backend, artifact.ModelId, exception.ToString());
            }

            if (ContainsNativeFailure(exception))
            {
                return new DeploySharpException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "No compatible LLamaSharp native backend could be loaded. Install one matching LLamaSharp 0.27.0 and the current RID.", exception, Backend, artifact.ModelId, exception.ToString());
            }

            return new DeploySharpException(
                loading ? DeploySharpErrorCodes.ModelArtifactInvalid : DeploySharpErrorCodes.LanguageModelFailed,
                loading ? "LLamaSharp could not load the GGUF model." : $"LLamaSharp failed while performing '{operation}'.",
                exception,
                Backend,
                artifact.ModelId,
                exception.ToString());
        }

        private static bool ContainsNativeFailure(Exception exception)
        {
            Exception? current = exception;
            while (current != null)
            {
                if (current is DllNotFoundException || current is EntryPointNotFoundException || current is BadImageFormatException) return true;
                if (current is RuntimeError && current.Message.IndexOf("native library", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                current = current.InnerException;
            }

            return false;
        }

        private static bool ContainsContextLimit(Exception exception)
        {
            string text = exception.Message ?? string.Empty;
            return text.IndexOf("context window", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("ContextSize", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("context size", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
