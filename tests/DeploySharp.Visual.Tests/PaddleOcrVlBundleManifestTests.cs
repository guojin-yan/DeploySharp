using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlBundleManifestTests
{
    [TestMethod]
    public void ParsesAndVerifiesCompleteEightFileBundle()
    {
        using var directory = new TemporaryDirectory();
        string json = WriteBundle(directory.Path);

        PaddleOcrVlBundleManifest manifest = PaddleOcrVlBundleManifest.Parse(json);
        Assert.AreEqual(PaddleOcrVlBundleManifest.OfficialBundleId, manifest.BundleId);
        Assert.AreEqual(8, manifest.Artifacts.Count);
        IReadOnlyDictionary<string, string> paths = manifest.VerifyFiles(directory.Path);
        Assert.AreEqual(Path.Combine(directory.Path, "vision.onnx"), paths["vision"]);
        Assert.AreEqual(Path.Combine(directory.Path, "tokenizer.model"), manifest.GetVerifiedPath(directory.Path, "tokenizer-model"));
        Assert.AreEqual(PaddleOcrVlBundleManifest.OfficialCheckpointRevision, manifest.CheckpointRevision);
    }

    [TestMethod]
    public void RejectsTamperedGraphBeforeAFactoryCanUseIt()
    {
        using var directory = new TemporaryDirectory();
        WriteBundle(directory.Path);
        PaddleOcrVlBundleManifest manifest = PaddleOcrVlBundleManifest.LoadFromDirectory(directory.Path);
        File.AppendAllText(Path.Combine(directory.Path, "decoder.onnx"), "tampered");

        VisualException exception = Assert.ThrowsExactly<VisualException>(() => manifest.VerifyFiles(directory.Path));
        Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, exception.ErrorCode);
        StringAssert.Contains(exception.Message, "decoder.onnx");
    }

    [TestMethod]
    public void RejectsMissingRoleAndWrongFormat()
    {
        using var directory = new TemporaryDirectory();
        string json = WriteBundle(directory.Path).Replace("\"format\": \"onnx\"", "\"format\": \"json\"", StringComparison.Ordinal);
        VisualException exception = Assert.ThrowsExactly<VisualException>(() => PaddleOcrVlBundleManifest.Parse(json));
        StringAssert.Contains(exception.Message, "vision");
    }

    private static string WriteBundle(string root)
    {
        var files = new (string Role, string Name, string Format)[]
        {
            ("vision", "vision.onnx", "onnx"),
            ("embedding", "embedding.onnx", "onnx"),
            ("decoder", "decoder.onnx", "onnx"),
            ("head", "head.onnx", "onnx"),
            ("tokenizer-model", "tokenizer.model", "sentencepiece"),
            ("tokenizer-json", "tokenizer.json", "json"),
            ("tokenizer-config", "tokenizer_config.json", "json"),
            ("chat-template", "chat_template.jinja", "jinja")
        };
        var rows = new List<object>();
        foreach ((string role, string name, string format) in files)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(role + "-payload");
            string path = Path.Combine(root, name);
            File.WriteAllBytes(path, bytes);
            rows.Add(new { role, fileName = name, format, size = bytes.Length, sha256 = Hash(bytes) });
        }
        string manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = PaddleOcrVlBundleManifest.OfficialSchemaVersion,
            bundleId = PaddleOcrVlBundleManifest.OfficialBundleId,
            modelId = PaddleOcrVlBundleManifest.OfficialBundleId,
            checkpointRevision = PaddleOcrVlBundleManifest.OfficialCheckpointRevision,
            artifacts = rows
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(System.IO.Path.Combine(root, "paddleocr-vl-bundle.json"), manifest);
        return manifest;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "deploysharp-paddleocr-vl-manifest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        internal string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
