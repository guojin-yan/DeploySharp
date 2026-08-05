using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.ModelPack.Json;

namespace DeploySharp.ModelPack.Json.Tests
{
    internal static class ModelPackageTestFactory
    {
        public static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return ToLowerHex(sha.ComputeHash(bytes));
        }

        public static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        public static ModelPackageDocument Document(params ModelArtifactDocument[] artifacts)
        {
            return new ModelPackageDocument(
                "2.0", "tests/model-pack", "Test model pack", "test-family", "inference", "1.0.0",
                new ModelExporterDocument("DeploySharp.Tests", "2.0.0"),
                new ModelSourceDocument("https://example.com/source", "https://example.com/project", "abc123", "DeploySharp", null, "Apache-2.0", null, true),
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "test-profile",
                new[] { new ModelTensorSignatureDocument("input", "float32", new long[] { 1, 3, 224, 224 }) },
                new[] { new ModelTensorSignatureDocument("output", "float32", new long[] { 1, 1000 }) },
                artifacts,
                new[] { new KeyValuePair<string, string>("z-extension", "last"), new KeyValuePair<string, string>("a-extension", "first") });
        }

        public static ModelArtifactDocument FileArtifact(string id, string path, byte[] bytes, string format = "onnx", string backend = "onnxruntime", ModelFileRole role = ModelFileRole.Model)
        {
            return new ModelArtifactDocument(id, format, ModelArtifactLocationKind.File, path, new[] { backend }, new[] { new ModelFileDocument(path, Hash(bytes), bytes.LongLength, "application/octet-stream", role) }, precision: "fp32", opset: 17, portable: true);
        }

        public static ModelArtifactDocument DirectoryArtifact(string id, string directory, string fileName, byte[] bytes, string format, string backend)
        {
            string relative = directory + "/" + fileName;
            return new ModelArtifactDocument(id, format, ModelArtifactLocationKind.Directory, directory, new[] { backend }, new[] { new ModelFileDocument(relative, Hash(bytes), bytes.LongLength, "application/octet-stream", ModelFileRole.Model) }, portable: true);
        }

        public static string CreatePackage(string root, params Tuple<string, byte[]>[] files)
        {
            Directory.CreateDirectory(root);
            foreach (Tuple<string, byte[]> file in files)
            {
                string path = Path.Combine(root, file.Item1.Replace('/', Path.DirectorySeparatorChar));
                string? parent = Path.GetDirectoryName(path);
                if (parent != null) Directory.CreateDirectory(parent);
                System.IO.File.WriteAllBytes(path, file.Item2);
            }

            return root;
        }

        public static Tuple<string, byte[]> File(string name, string content)
        {
            return Tuple.Create(name, Encoding.UTF8.GetBytes(content));
        }
    }
}
