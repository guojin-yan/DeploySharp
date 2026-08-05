using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.LlamaSharp
{
    /// <summary>Validates local GGUF artifacts before native loading. / 在原生加载之前验证本地 GGUF 工件。</summary>
    public static class GgufModelArtifactValidator
    {
        private static readonly BackendId Backend = new BackendId("llamasharp");

        /// <summary>Validates format, path, GGUF magic, and optional SHA256. / 验证格式、路径、GGUF 魔数和可选 SHA256。</summary>
        public static void Validate(ModelArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (!string.Equals(artifact.Format, "gguf", StringComparison.Ordinal)) ThrowInvalid(artifact, "The model format must be 'gguf'.");
            if (!string.Equals(Path.GetExtension(artifact.Location), ".gguf", StringComparison.OrdinalIgnoreCase)) ThrowInvalid(artifact, "The model file must use the .gguf extension.");
            if (!File.Exists(artifact.Location)) ThrowInvalid(artifact, $"The GGUF model file does not exist: '{artifact.Location}'.");

            try
            {
                using (var stream = new FileStream(artifact.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 8) ThrowInvalid(artifact, "The GGUF model file is truncated.");
                    var magic = new byte[4];
                    if (stream.Read(magic, 0, magic.Length) != magic.Length || magic[0] != (byte)'G' || magic[1] != (byte)'G' || magic[2] != (byte)'U' || magic[3] != (byte)'F')
                    {
                        ThrowInvalid(artifact, "The model file does not contain the GGUF magic header.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(artifact.Sha256)) ValidateSha256(artifact);
            }
            catch (DeploySharpException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new DeploySharpException(DeploySharpErrorCodes.ModelArtifactInvalid, "The GGUF model file could not be validated.", exception, Backend, artifact.ModelId, exception.ToString());
            }
        }

        private static void ValidateSha256(ModelArtifact artifact)
        {
            string expected = artifact.Sha256!;
            if (expected.Length != 64) ThrowInvalid(artifact, "The expected SHA256 value must contain 64 hexadecimal characters.");
            for (int index = 0; index < expected.Length; index++)
            {
                if (!Uri.IsHexDigit(expected[index])) ThrowInvalid(artifact, "The expected SHA256 value contains a non-hexadecimal character.");
            }

            string actual;
            using (var algorithm = SHA256.Create())
            using (var stream = new FileStream(artifact.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
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

            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) ThrowInvalid(artifact, "The GGUF model SHA256 value does not match the artifact.");
        }

        private static void ThrowInvalid(ModelArtifact artifact, string message)
        {
            throw new DeploySharpException(DeploySharpErrorCodes.ModelArtifactInvalid, message, backendId: Backend, modelId: artifact.ModelId, technicalDetails: artifact.Location);
        }
    }
}
