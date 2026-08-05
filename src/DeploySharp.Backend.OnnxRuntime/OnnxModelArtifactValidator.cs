using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Validates a local ONNX model before native session creation. / 在创建原生会话前验证本地 ONNX 模型。</summary>
    public static class OnnxModelArtifactValidator
    {
        /// <summary>Validates format, regular-file path, minimum size, and optional SHA256, then returns the absolute path. / 验证格式、普通文件路径、最小大小及可选 SHA256，并返回绝对路径。</summary>
        public static string Validate(ModelArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (!string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)) ThrowInvalid(artifact, "The model format must be 'onnx'.");
            string fullPath;
            try { fullPath = Path.GetFullPath(artifact.Location); }
            catch (Exception exception) { throw Invalid(artifact, "The ONNX model path is invalid.", exception); }
            if (!string.Equals(Path.GetExtension(fullPath), ".onnx", StringComparison.OrdinalIgnoreCase)) ThrowInvalid(artifact, "The model file must use the .onnx extension.");
            if (!File.Exists(fullPath)) ThrowInvalid(artifact, "The ONNX model file does not exist.");
            try
            {
                FileAttributes attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0) ThrowInvalid(artifact, "The ONNX model must be a regular file and cannot be a symbolic link or reparse point.");
                if (new FileInfo(fullPath).Length < 8) ThrowInvalid(artifact, "The ONNX model file is empty or truncated.");
                if (!string.IsNullOrWhiteSpace(artifact.Sha256)) ValidateSha256(artifact, fullPath);
                return fullPath;
            }
            catch (DeploySharpException) { throw; }
            catch (Exception exception) { throw Invalid(artifact, "The ONNX model file could not be validated.", exception); }
        }

        private static void ValidateSha256(ModelArtifact artifact, string path)
        {
            string expected = artifact.Sha256!;
            if (expected.Length != 64) ThrowInvalid(artifact, "The expected SHA256 value must contain 64 hexadecimal characters.");
            for (int index = 0; index < expected.Length; index++) if (!Uri.IsHexDigit(expected[index])) ThrowInvalid(artifact, "The expected SHA256 value contains a non-hexadecimal character.");
            string actual;
            using (SHA256 algorithm = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                var characters = new char[hash.Length * 2];
                for (int index = 0; index < hash.Length; index++)
                {
                    string pair = hash[index].ToString("x2", CultureInfo.InvariantCulture);
                    characters[index * 2] = pair[0];
                    characters[(index * 2) + 1] = pair[1];
                }
                actual = new string(characters);
            }
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) ThrowInvalid(artifact, "The ONNX model SHA256 value does not match the artifact.");
        }

        private static void ThrowInvalid(ModelArtifact artifact, string message) { throw Invalid(artifact, message); }

        private static OnnxRuntimeBackendException Invalid(ModelArtifact artifact, string message, Exception? inner = null)
        {
            return new OnnxRuntimeBackendException(DeploySharpErrorCodes.ModelArtifactInvalid, message, inner, artifact.ModelId, operation: "validate", technicalDetails: artifact.Location);
        }
    }
}
