using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlOnnxIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public void SixRealTaskPromptsGenerateThroughTheProductionManagedDynamicKvSession()
        => RunSixTasks(useManagedProcessor: false);

    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public void SixRealTaskPromptsGenerateThroughManagedRgbProcessingAndDynamicKvSession()
        => RunSixTasks(useManagedProcessor: true);

    private static void RunSixTasks(bool useManagedProcessor)
    {
        string bundleRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string modelRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string? tensors = useManagedProcessor ? null : Required("DEPLOYSHARP_PADDLEOCR_VL_TENSOR_ROOT");
        string? fixtureRoot = useManagedProcessor ? Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES") : null;
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE");
        string? output = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REPORT");
        if (!BitConverter.IsLittleEndian) Assert.Inconclusive("The saved official tensor fixture is little-endian.");
        using JsonDocument reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        Assert.AreEqual("reference-generation-complete", reference.RootElement.GetProperty("status").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
        using JsonDocument? processorFixtures = useManagedProcessor
            ? JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot!, "fixtures.json"))) : null;
        if (processorFixtures != null)
        {
            Assert.AreEqual("deploysharp-paddleocr-vl-processor-fixtures-v1", processorFixtures.RootElement.GetProperty("schemaVersion").GetString());
            Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, processorFixtures.RootElement.GetProperty("checkpointRevision").GetString());
        }
        var tokenizer = new PaddleOcrVlTokenizer(modelRoot);
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1,
            enableMemoryPattern: false, enableCpuMemoryArena: false));
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1, ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["status"] = "running", ["referenceSha256"] = Hash(referencePath),
            ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision,
            ["inputPreparation"] = useManagedProcessor ? "managed-rgb-processor" : "saved-official-patches",
            ["processorFixtureManifestSha256"] = useManagedProcessor ? Hash(Path.Combine(fixtureRoot!, "fixtures.json")) : null,
            ["environment"] = new { os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                processorCount = Environment.ProcessorCount, provider = "CPUExecutionProvider", threads = 4 },
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OnnxRuntimeOptions).Assembly, typeof(TensorShape).Assembly, typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), sha256 = Hash(assembly.Location) }).ToArray(),
            ["graphs"] = new[] { "vision", "embedding", "decoder", "head" }.Select(role => new { role, sha256 = Hash(Path.Combine(bundleRoot, role + ".onnx")) }).ToArray(),
            ["boundary"] = useManagedProcessor
                ? "Production C# RGB resize/normalization/patch packing and generation on one CPU host. RGB decoding is supplied by upstream Pillow. Does not verify OpenCV decoding, layout/crop/multipage routing, independent labeled quality, GPU/multi-backend execution or formal performance."
                : "Production C# generation from saved official pixels on one CPU host. Does not verify a C# image resizer, independent labeled quality, multi-backend/GPU execution or formal performance. Failed feature precision is not promoted by matching output tokens."
        };
        var rows = new List<Dictionary<string, object?>>();
        report["cases"] = rows;
        void Save()
        {
            if (string.IsNullOrWhiteSpace(output)) return;
            string full = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Save();
        try
        {
            using PaddleOcrVlOnnxSession session = PaddleOcrVlOnnxSession.Create(registry, bundleRoot,
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
            foreach (JsonElement image in reference.RootElement.GetProperty("cases").EnumerateArray())
            {
                string id = image.GetProperty("id").GetString()!;
                string task = image.GetProperty("task").GetString()!;
                PaddleOcrVlTask taskId = task switch
                {
                    "ocr" => PaddleOcrVlTask.TextRecognition, "table" => PaddleOcrVlTask.TableRecognition,
                    "formula" => PaddleOcrVlTask.FormulaRecognition, "chart" => PaddleOcrVlTask.ChartRecognition,
                    "spotting" => PaddleOcrVlTask.TextSpotting, "seal" => PaddleOcrVlTask.SealRecognition,
                    _ => throw new InvalidDataException("Unknown official task")
                };
                JsonElement size = image.GetProperty("sourceSize");
                PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(size[0].GetInt32(), size[1].GetInt32()), taskId);
                PaddleOcrVlPreparedInput prepared;
                byte[] bytes;
                if (processorFixtures != null)
                {
                    JsonElement fixture = processorFixtures.RootElement.GetProperty("cases").EnumerateArray()
                        .Single(value => value.GetProperty("id").GetString() == id);
                    Assert.AreEqual(image.GetProperty("inputSha256").GetString(), fixture.GetProperty("inputSha256").GetString());
                    string rgbName = fixture.GetProperty("rgbFile").GetString()!;
                    Assert.AreEqual(rgbName, Path.GetFileName(rgbName));
                    string rgbPath = Path.Combine(fixtureRoot!, rgbName);
                    Assert.AreEqual(fixture.GetProperty("rgbSha256").GetString(), Hash(rgbPath));
                    prepared = new PaddleOcrVlImageProcessor().Create(File.ReadAllBytes(rgbPath), plan.SourceSize, taskId,
                        sourceSha256: image.GetProperty("inputSha256").GetString());
                    float[] actualPatches = prepared.PixelPatches.ToArray();
                    bytes = new byte[actualPatches.Length * sizeof(float)];
                    Buffer.BlockCopy(actualPatches, 0, bytes, 0, bytes.Length);
                }
                else
                {
                    string pixelsPath = Path.Combine(tensors!, id + "-pixel_values.bin");
                    bytes = File.ReadAllBytes(pixelsPath);
                    var values = new float[plan.PatchCount * 3 * 14 * 14];
                    Assert.AreEqual(values.Length * sizeof(float), bytes.Length);
                    Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                    prepared = new PaddleOcrVlPreparedInput(plan, new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14), values, TensorBufferOwnership.Transfer), image.GetProperty("inputSha256").GetString());
                }
                string pixelSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                Assert.AreEqual(image.GetProperty("tensors").GetProperty("pixel_values").GetProperty("sha256").GetString(), pixelSha);
                var row = new Dictionary<string, object?> { ["id"] = id, ["task"] = task, ["inputSha256"] = prepared.SourceSha256, ["pixelSha256"] = pixelSha, ["status"] = "running" };
                rows.Add(row); Save();
                PaddleOcrVlGenerationResult actual = session.Generate(prepared, tokenizer, new PaddleOcrVlGenerationOptions(timeout: TimeSpan.FromMinutes(15)));
                int[] expectedIds = image.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                bool tokenExact = actual.TokenIds.SequenceEqual(expectedIds);
                bool textExact = actual.Text == image.GetProperty("decoded").GetString();
                row["generatedTokenIds"] = actual.TokenIds; row["text"] = actual.Text;
                row["finishReason"] = actual.FinishReason.ToString(); row["promptTokens"] = actual.PromptTokenCount;
                row["ropeDelta"] = actual.RopeDelta; row["elapsedMilliseconds"] = actual.Elapsed.TotalMilliseconds;
                row["tokenExact"] = tokenExact; row["textExact"] = textExact; row["status"] = "generated";
                Save();
                Console.WriteLine("MANAGED_VL_GENERATED " + id + " tokens=" + actual.TokenIds.Count + " exact=" + tokenExact);
                Assert.AreEqual(GenerationFinishReason.EndOfSequence, actual.FinishReason, id);
                Assert.IsTrue(tokenExact, "Generated tokens diverged for " + id);
                Assert.IsTrue(textExact, "Decoded text diverged for " + id);
            }
            Assert.AreEqual(6, rows.Count);
            report["status"] = useManagedProcessor ? "six-task-managed-rgb-generation-exact" : "six-task-managed-generation-exact";
            Save();
        }
        catch (Exception exception)
        {
            report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw;
        }
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + name + " for the external PaddleOCR-VL test.");
        return value!;
    }
    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
