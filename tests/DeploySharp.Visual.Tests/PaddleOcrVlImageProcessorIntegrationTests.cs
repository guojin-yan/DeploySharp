using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlImageProcessorIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalAssets")]
    public void RealRgbInputsMatchPinnedUpstreamPatchGeometryAndPixels()
    {
        string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        if (string.IsNullOrWhiteSpace(root)) Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES to the exported upstream fixtures.");
        if (!BitConverter.IsLittleEndian) Assert.Inconclusive("The pinned float32 fixtures are little-endian.");
        string manifestPath = Path.Combine(root!, "fixtures.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        Assert.AreEqual("deploysharp-paddleocr-vl-processor-fixtures-v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, manifest.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual("exported", manifest.RootElement.GetProperty("status").GetString());
        const double tolerance = 0.000001;
        var rows = new List<object>();
        var failures = new List<string>();
        var processor = new PaddleOcrVlImageProcessor();
        foreach (JsonElement fixture in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = fixture.GetProperty("id").GetString()!;
            byte[] rgb = ReadVerified(root!, fixture, "rgbFile", "rgbSha256");
            byte[] expectedBytes = ReadVerified(root!, fixture, "patchFile", "patchSha256");
            int[] dimensions = fixture.GetProperty("sourceSize").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            PaddleOcrVlTask task = Enum.Parse<PaddleOcrVlTask>(fixture.GetProperty("task").GetString()!);
            PaddleOcrVlPreparedInput input = processor.Create(rgb, new VisualSize(dimensions[0], dimensions[1]), task,
                sourceSha256: fixture.GetProperty("inputSha256").GetString());
            var expectedShape = new TensorShape(fixture.GetProperty("patchShape").EnumerateArray().Select(value => value.GetInt64()).ToArray());
            Assert.AreEqual(expectedShape, input.PixelPatches.Shape, id);
            float[] actual = ((Tensor<float>)input.PixelPatches).ToArray();
            Assert.AreEqual(actual.Length * sizeof(float), expectedBytes.Length, id);
            var expected = new float[actual.Length];
            Buffer.BlockCopy(expectedBytes, 0, expected, 0, expectedBytes.Length);
            double maximum = 0, sum = 0;
            int different = 0, beyondTolerance = 0;
            for (int index = 0; index < actual.Length; index++)
            {
                double delta = Math.Abs((double)actual[index] - expected[index]);
                if (!float.IsFinite(actual[index]) || !float.IsFinite(expected[index])) delta = double.PositiveInfinity;
                if (BitConverter.SingleToInt32Bits(actual[index]) != BitConverter.SingleToInt32Bits(expected[index])) different++;
                if (delta > tolerance) beyondTolerance++;
                maximum = Math.Max(maximum, delta);
                sum += delta;
            }
            rows.Add(new { id, task = task.ToString(), inputSha256 = input.SourceSha256,
                rgbSha256 = fixture.GetProperty("rgbSha256").GetString(), patchSha256 = fixture.GetProperty("patchSha256").GetString(),
                shape = expectedShape.ToString(), values = actual.Length, bitDifferences = different,
                beyondTolerance, maximumAbsoluteDifference = maximum, meanAbsoluteDifference = sum / actual.Length });
            if (different > 0) failures.Add(id + ": " + different + " patch values differ from upstream; max=" + maximum);
        }
        Assert.IsTrue(rows.Count > 0);
        string? reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_REPORT");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            string fullPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var assembly = typeof(PaddleOcrVlImageProcessor).Assembly;
            File.WriteAllText(fullPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow,
                status = failures.Count == 0 ? "all-bit-exact" : "pixel-differences-recorded",
                fixtureManifestSha256 = Hash(File.ReadAllBytes(manifestPath)), tolerance,
                assembly = new { name = assembly.GetName().Name, sha256 = Hash(File.ReadAllBytes(assembly.Location)) },
                environment = new { framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription },
                boundary = "Managed preprocessing of pinned upstream-decoded RGB. Does not verify an encoded-image decoder, model generation, page routing, labeled quality or performance.",
                cases = rows
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static byte[] ReadVerified(string root, JsonElement fixture, string fileProperty, string hashProperty)
    {
        string name = fixture.GetProperty(fileProperty).GetString()!;
        Assert.AreEqual(name, Path.GetFileName(name), "Fixture paths must be file names.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, name));
        Assert.AreEqual(fixture.GetProperty(hashProperty).GetString(), Hash(bytes));
        return bytes;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
