using System;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp.ModelPack.Json;
using JYPPX.DeploySharp.ModelPack.Json.Serialization;

namespace DeploySharp.ModelPack.Json.CleanConsumer
{
    internal static class Program
    {
        private static void Main()
        {
            byte[] modelBytes = { 1, 2, 3, 4 };
            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(modelBytes);
                hash = BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }

            var document = new ModelPackageDocument(
                "2.0", "clean/consumer", "Clean Consumer", "test", "inference", "1.0",
                new ModelExporterDocument("clean-consumer", "1.0"),
                new ModelSourceDocument("https://example.com/source", null, "main", "DeploySharp", null, "Apache-2.0", null, true),
                DateTimeOffset.UtcNow, null, Array.Empty<ModelTensorSignatureDocument>(), Array.Empty<ModelTensorSignatureDocument>(),
                new[] { new ModelArtifactDocument("onnx.cpu", "onnx", ModelArtifactLocationKind.File, "model.onnx", new[] { "onnxruntime" }, new[] { new ModelFileDocument("model.onnx", hash, modelBytes.Length, "application/onnx", ModelFileRole.Model) }) });
            ValidatedModelPackage validated = ModelPackageValidator.Validate(document);
            string root = Path.Combine(Path.GetTempPath(), "deploysharp-clean-consumer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "model.onnx"), modelBytes);
                string manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, ModelPackageJsonSerializer.Serialize(validated));
                LocalModelPackage package = ModelPackageLoader.Load(manifestPath);
                Console.WriteLine("ModelPack.Json package-only consumer passed: " + package.ToCoreArtifacts()[0].Format);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
