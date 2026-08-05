using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    internal static class OpenVinoModelArtifactValidator
    {
        public static string Validate(ModelArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            string expectedExtension;
            if (string.Equals(artifact.Format, "onnx", StringComparison.OrdinalIgnoreCase)) expectedExtension = ".onnx";
            else if (string.Equals(artifact.Format, "openvino-ir", StringComparison.OrdinalIgnoreCase)) expectedExtension = ".xml";
            else throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "Only ONNX and OpenVINO IR artifacts are supported.", artifact, "validate-artifact");

            string path;
            try { path = Path.GetFullPath(artifact.Location); }
            catch (Exception exception) { throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "The model path is invalid.", artifact, "validate-artifact", exception); }
            if (!Path.IsPathRooted(path) || !File.Exists(path)) throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "The model file does not exist.", artifact, "validate-artifact");
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.Directory) != 0 || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "The model must be a regular local file and cannot be a reparse point.", artifact, "validate-artifact");
            if (!string.Equals(info.Extension, expectedExtension, StringComparison.OrdinalIgnoreCase)) throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "The model extension does not match its declared format.", artifact, "validate-artifact");
            if (info.Length == 0) throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "The model file is empty.", artifact, "validate-artifact");

            if (expectedExtension == ".xml")
            {
                string bin = Path.ChangeExtension(path, ".bin");
                if (!File.Exists(bin) || (File.GetAttributes(bin) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw Failure(OpenVinoErrorCodes.IrSidecarInvalid, "The OpenVINO IR .bin sidecar is missing or unsafe.", artifact, "validate-ir-sidecar");
            }

            if (!string.IsNullOrWhiteSpace(artifact.Sha256))
            {
                string actual = ComputeSha256(path);
                if (!string.Equals(actual, artifact.Sha256, StringComparison.OrdinalIgnoreCase)) throw Failure(OpenVinoErrorCodes.ModelLoadFailed, "The model SHA256 does not match the artifact declaration.", artifact, "validate-integrity");
            }
            return path;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                var text = new StringBuilder(64);
                foreach (byte value in algorithm.ComputeHash(stream)) text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static OpenVinoBackendException Failure(string code, string message, ModelArtifact artifact, string operation, Exception? inner = null)
        {
            return new OpenVinoBackendException(code, message, inner, artifact.ModelId, operation: operation, technicalDetails: artifact.Location);
        }
    }
}
