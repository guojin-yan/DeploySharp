using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Validates an external, device-bound TensorRT engine before native deserialization.</summary>
    public static class TensorRtModelArtifactValidator
    {
        /// <summary>Validates a regular .engine or .plan file and its optional SHA256.</summary>
        public static string Validate(ModelArtifact artifact, long maximumEngineBytes)
        {
            string fullPath = ResolvePath(artifact);
            using (FileStream stream = OpenValidatedStream(artifact, fullPath, maximumEngineBytes))
            {
                ValidateSha256(artifact, stream);
            }

            return fullPath;
        }

        internal static byte[] ReadValidatedBytes(ModelArtifact artifact, long maximumEngineBytes)
        {
            string fullPath = ResolvePath(artifact);
            using (FileStream stream = OpenValidatedStream(artifact, fullPath, maximumEngineBytes))
            {
                var bytes = new byte[checked((int)stream.Length)];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) throw Invalid(artifact, "The TensorRT engine changed or became truncated while it was being read.");
                    offset += read;
                }
                ValidateSha256Bytes(artifact, bytes);
                return bytes;
            }
        }

        private static string ResolvePath(ModelArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (!string.Equals(artifact.Format, "tensorrt-engine", StringComparison.Ordinal))
            {
                throw Invalid(artifact, "The model format must be 'tensorrt-engine'.");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(artifact.Location);
            }
            catch (Exception exception)
            {
                throw Invalid(artifact, "The TensorRT engine path is invalid.", exception);
            }

            string extension = Path.GetExtension(fullPath);
            if (!string.Equals(extension, ".engine", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".plan", StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(artifact, "The TensorRT artifact must use the .engine or .plan extension.");
            }

            return fullPath;
        }

        private static FileStream OpenValidatedStream(ModelArtifact artifact, string fullPath, long maximumEngineBytes)
        {
            FileStream? stream = null;
            try
            {
                if (!File.Exists(fullPath)) throw Invalid(artifact, "The TensorRT engine file does not exist.");
                FileAttributes attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw Invalid(artifact, "The TensorRT engine must be a regular file and cannot be a reparse point.");
                }

                stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
                long length = stream.Length;
                if (length < 8) throw Invalid(artifact, "The TensorRT engine file is empty or truncated.");
                if (length > maximumEngineBytes)
                {
                    throw Invalid(artifact, "The TensorRT engine exceeds the configured managed loader size limit.");
                }
                FileStream result = stream;
                stream = null;
                return result;
            }
            catch (TensorRtBackendException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Invalid(artifact, "The TensorRT engine file could not be validated.", exception);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private static void ValidateSha256(ModelArtifact artifact, FileStream stream)
        {
            if (string.IsNullOrWhiteSpace(artifact.Sha256)) return;
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                stream.Position = 0;
                hash = algorithm.ComputeHash(stream);
            }
            ValidateSha256Hash(artifact, hash);
        }

        private static void ValidateSha256Bytes(ModelArtifact artifact, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(artifact.Sha256)) return;
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }
            ValidateSha256Hash(artifact, hash);
        }

        private static void ValidateSha256Hash(ModelArtifact artifact, byte[] hash)
        {
            string expected = artifact.Sha256!;
            if (expected.Length != 64) throw Invalid(artifact, "The expected SHA256 value must contain 64 hexadecimal characters.");
            for (int index = 0; index < expected.Length; index++)
            {
                if (!Uri.IsHexDigit(expected[index])) throw Invalid(artifact, "The expected SHA256 value contains a non-hexadecimal character.");
            }

            var characters = new char[hash.Length * 2];
            for (int index = 0; index < hash.Length; index++)
            {
                string pair = hash[index].ToString("x2", CultureInfo.InvariantCulture);
                characters[index * 2] = pair[0];
                characters[(index * 2) + 1] = pair[1];
            }

            if (!string.Equals(expected, new string(characters), StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(artifact, "The TensorRT engine SHA256 value does not match the artifact.");
            }
        }

        private static TensorRtBackendException Invalid(ModelArtifact artifact, string message, Exception? inner = null)
        {
            return new TensorRtBackendException(
                TensorRtErrorCodes.ModelArtifactInvalid,
                message,
                inner,
                artifact.ModelId,
                operation: "validate",
                technicalDetails: artifact.Location);
        }
    }
}
