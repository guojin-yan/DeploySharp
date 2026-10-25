using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Dnn;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenCV.Tests;

/// <summary>Records native importer evidence only; run each graph in a separate process.</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlImporterProbeTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SelectedGraphRecordsNativeImporterBoundary()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_BACKEND_PROBE") != "1")
            Assert.Inconclusive("Run Invoke-PaddleOcrVlBackendProbe.ps1 to isolate native failures.");
        string graph = Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_GRAPH");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vision"] = "940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f",
            ["embedding"] = "1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86",
            ["decoder"] = "823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad",
            ["head"] = "8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148"
        };
        Assert.IsTrue(hashes.ContainsKey(graph), "Unknown graph role.");
        string path = Path.Combine(Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT"), graph + ".onnx");
        string reportPath = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_PROBE_REPORT"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        using var source = File.OpenRead(path);
        string hash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        Assert.AreEqual(hashes[graph], hash, "This probe is limited to the fixed 1.6 export.");
        source.Close();
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-opencv-importer-v1",
            ["generatedUtc"] = DateTimeOffset.UtcNow, ["graph"] = graph,
            ["sourceSha256"] = hash, ["sourceBytes"] = new FileInfo(path).Length,
            ["sourceCommit"] = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT"),
            ["testAssemblySha256"] = Hash(GetType().Assembly.Location),
            ["os"] = RuntimeInformation.OSDescription, ["framework"] = RuntimeInformation.FrameworkDescription,
            ["backend"] = "opencv-dnn", ["engine"] = "Classic", ["graphPolicy"] = "original-unmodified",
            ["status"] = "probing", ["phase"] = "native-preflight",
            ["managedContractBoundary"] = graph == "embedding" ? "rank-2 Int64 input can be described; forward unverified"
                : "production adapter currently admits rank-4 images or rank<=2 auxiliary inputs; this graph needs rank-3/4/5 non-image inputs",
            ["boundary"] = "Native original-graph importer probe, bypassing managed tensor-rank restrictions. Import success is not inference, dynamic-shape, empty-KV, token parity or full Bundle support. A green probe test means its diagnostic was recorded, not backend support."
        };
        void Save() => File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        var watch = Stopwatch.StartNew();
        try
        {
            report["nativeVersion"] = OpenCvSharpBuildInfo.GetNativeOpenCvVersion();
            Assert.IsTrue(OpenCvSharpBuildInfo.IsNativeOpenCvVersionCompatible(), "Managed/native OpenCV mismatch.");
            report["phase"] = "native-import"; Save();
            using Net network = Net.ReadNetFromOnnx(path, DnnEngine.Classic);
            report["status"] = "imported-no-inference";
        }
        catch (Exception exception)
        {
            report["status"] = (string)report["phase"]! == "native-import" && exception is OpenCvException
                ? "unsupported-import" : "blocked";
            report["exceptionType"] = exception.GetType().FullName;
            report["diagnostic"] = exception.ToString();
        }
        finally
        {
            report["elapsedMilliseconds"] = watch.Elapsed.TotalMilliseconds;
            report["nativeModules"] = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Where(module => module.FileName.Contains("opencv", StringComparison.OrdinalIgnoreCase))
                .Select(module => new { path = module.FileName, version = module.FileVersionInfo.FileVersion }).ToArray();
            Save();
        }
        Assert.AreNotEqual("blocked", report["status"], "Probe infrastructure failed; inspect the report.");
        Console.WriteLine("VL_OPENCV_PROBE graph=" + graph + ";status=" + report["status"]);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException("Missing " + name);
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
