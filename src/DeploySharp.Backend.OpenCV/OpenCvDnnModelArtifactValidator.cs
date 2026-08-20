using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Validates one regular, immutable ONNX model before native initialization. / 在原生初始化前校验一个普通且不可变的 ONNX 模型。</summary>
    public static class OpenCvDnnModelArtifactValidator
    {
        /// <summary>Returns the full validated model path. / 返回完整且已校验的模型路径。</summary>
        public static string Validate(ModelArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (!string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)) throw Invalid(artifact, "OpenCV DNN v1 accepts only ONNX artifacts.");
            string path;
            try { path = Path.GetFullPath(artifact.Location); }
            catch (Exception exception) { throw Invalid(artifact, "The model path is invalid.", exception); }
            if (!File.Exists(path)) throw Invalid(artifact, "The ONNX model file does not exist.");
            var info = new FileInfo(path);
            if (info.Length <= 0) throw Invalid(artifact, "The ONNX model file is empty.");
            if ((info.Attributes & FileAttributes.Directory) != 0 || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw Invalid(artifact, "The ONNX artifact must be a regular file and cannot be a reparse point.");
            if (artifact.Sha256 != null)
            {
                string actual = Sha256(path);
                if (!string.Equals(actual, artifact.Sha256, StringComparison.OrdinalIgnoreCase)) throw Invalid(artifact, "The ONNX model SHA-256 does not match the artifact identity.");
            }
            return path;
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static OpenCvDnnBackendException Invalid(ModelArtifact artifact, string message, Exception? inner = null)
            => new OpenCvDnnBackendException(DeploySharpErrorCodes.ModelArtifactInvalid, message, inner, artifact.ModelId, operation: "validate", technicalDetails: artifact.Location);
    }
}
