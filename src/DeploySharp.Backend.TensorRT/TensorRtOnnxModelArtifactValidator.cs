using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Validates a caller-owned ONNX artifact before native parsing. / 验证 ONNX 模型输入。</summary>
    public static class TensorRtOnnxModelArtifactValidator
    {
        /// <summary>Validates an ONNX regular file, its size, and its optional SHA256. / 验证 ONNX 模型输入。</summary>
        public static string Validate(ModelArtifact artifact, long maximumOnnxBytes)
        {
            ReadResult result = ReadValidated(artifact, maximumOnnxBytes);
            return result.Path;
        }

        internal static ReadResult ReadValidated(ModelArtifact artifact, long maximumOnnxBytes)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (maximumOnnxBytes < 8 || maximumOnnxBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumOnnxBytes));
            if (!string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)) throw Invalid(artifact, "The source model format must be 'onnx'.");

            string fullPath;
            try { fullPath = Path.GetFullPath(artifact.Location); }
            catch (Exception exception) { throw Invalid(artifact, "The ONNX model path is invalid.", exception); }
            if (!string.Equals(Path.GetExtension(fullPath), ".onnx", StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(artifact, "The ONNX source artifact must use the .onnx extension.");
            }

            try
            {
                if (!File.Exists(fullPath)) throw Invalid(artifact, "The ONNX model file does not exist.");
                FileAttributes attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw Invalid(artifact, "The ONNX model must be a regular file and cannot be a reparse point.");
                }

                byte[] bytes;
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
                {
                    if (stream.Length < 8) throw Invalid(artifact, "The ONNX model is empty or truncated.");
                    if (stream.Length > maximumOnnxBytes) throw Invalid(artifact, "The ONNX model exceeds the configured managed input size limit.");
                    bytes = new byte[checked((int)stream.Length)];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) throw Invalid(artifact, "The ONNX model changed or became truncated while it was being read.");
                        offset += read;
                    }
                }

                string sha256 = ComputeSha256(bytes);
                ValidateExpectedSha256(artifact, sha256);
                return new ReadResult(fullPath, bytes, sha256);
            }
            catch (TensorRtBackendException) { throw; }
            catch (Exception exception) { throw Invalid(artifact, "The ONNX model could not be validated.", exception); }
        }

        internal static string ComputeSha256(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(bytes);
            var characters = new char[hash.Length * 2];
            for (int index = 0; index < hash.Length; index++)
            {
                string pair = hash[index].ToString("x2", CultureInfo.InvariantCulture);
                characters[index * 2] = pair[0];
                characters[(index * 2) + 1] = pair[1];
            }
            return new string(characters);
        }

        private static void ValidateExpectedSha256(ModelArtifact artifact, string actual)
        {
            if (string.IsNullOrWhiteSpace(artifact.Sha256)) return;
            string expected = artifact.Sha256!;
            if (expected.Length != 64) throw Invalid(artifact, "The expected SHA256 value must contain 64 hexadecimal characters.");
            for (int index = 0; index < expected.Length; index++)
            {
                if (!Uri.IsHexDigit(expected[index])) throw Invalid(artifact, "The expected SHA256 value contains a non-hexadecimal character.");
            }
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) throw Invalid(artifact, "The ONNX model SHA256 value does not match the artifact.");
        }

        private static TensorRtBackendException Invalid(ModelArtifact artifact, string message, Exception? inner = null)
        {
            return new TensorRtBackendException(
                TensorRtErrorCodes.OnnxModelInvalid,
                message,
                inner,
                artifact.ModelId,
                operation: "validate-onnx",
                technicalDetails: artifact.Location);
        }

        internal sealed class ReadResult
        {
            public ReadResult(string path, byte[] bytes, string sha256)
            {
                Path = path;
                Bytes = bytes;
                Sha256 = sha256;
            }

            public string Path { get; }
            public byte[] Bytes { get; }
            public string Sha256 { get; }
        }
    }
}
