using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlDetectedDocumentIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public Task PrepareActualDetectorCropsForIndependentOfficialReference() => Run(generate: false);

    [TestMethod]
    [TestCategory("ExternalModels")]
    public Task ActualLayoutDetectorAndVlMatchEveryOfficialCropToken() => Run(generate: true);

    private static async Task Run(bool generate)
    {
        string manifestPath = Required("DEPLOYSHARP_PADDLEOCR_VL_DETECTED_MANIFEST");
        string output = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_DETECTED_OUTPUT"));
        Directory.CreateDirectory(output);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        JsonElement specification = manifest.RootElement;
        Assert.AreEqual("Auto", specification.GetProperty("task").GetString());
        JsonElement layoutSpec = specification.GetProperty("layoutDetector");
        string layoutPath = Path.GetFullPath(Path.Combine(root, layoutSpec.GetProperty("path").GetString()!));
        Assert.AreEqual(layoutSpec.GetProperty("sha256").GetString(), HashFile(layoutPath));
        var descriptor = PaddleDocumentModelCatalog.Get(layoutSpec.GetProperty("modelId").GetString()!);
        var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(descriptor, PaddleDocumentProfiles.Layout23Labels,
            new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: layoutSpec.GetProperty("scoreThreshold").GetSingle());
        var pages = specification.GetProperty("pages").EnumerateArray().Select(page =>
        {
            string path = Path.GetFullPath(Path.Combine(root, page.GetProperty("image").GetString()!));
            OpenCvImageSource source = OpenCvImageSource.FromFile(path);
            Assert.AreEqual(page.GetProperty("sha256").GetString(), source.Sha256);
            return new PaddleDocumentPage(source, new VisualSize(page.GetProperty("width").GetInt32(), page.GetProperty("height").GetInt32()), page.GetProperty("pageIndex").GetInt32());
        }).ToArray();
        var rows = new List<object>();
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-detected-pages-v1", ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision, ["status"] = "running",
            ["mode"] = generate ? "real-detector-and-vl" : "real-detector-and-patch-preparation-only",
            ["manifestSha256"] = HashFile(manifestPath), ["layoutModelSha256"] = HashFile(layoutPath),
            ["environment"] = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), provider = "CPUExecutionProvider", threads = 4,
                openCv = OpenCvRuntimePreflight.Check().NativeVersion, pageParallelism = 1, vlSessionCount = generate ? 1 : 0 },
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OpenCvPaddleOcrVlDocumentPipeline).Assembly,
                typeof(OnnxRuntimeOptions).Assembly, typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly, typeof(PaddleOcrVlDetectedDocumentIntegrationTests).Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, sha256 = HashFile(assembly.Location) }).ToArray(),
            ["regions"] = rows,
            ["boundary"] = "Fixed independent FUNSD document pages, all detector regions at the declared threshold in detector order. Official comparisons use exact crops; not labeled document quality, reading order, structure normalization, other backends or formal performance. Preparation-only mode never runs or validates a VL model."
        };
        void Save() => File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        try
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1, enableMemoryPattern: false, enableCpuMemoryArena: false));
            var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var detector = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(layoutPath), registry, request, VisualTaskId.LayoutDetection), request);
            string? bundleRoot = generate ? Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT") : null;
            using var session = generate ? PaddleOcrVlOnnxSession.Create(registry, bundleRoot!, request) : null;
            var tokenizer = generate ? new PaddleOcrVlTokenizer(Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT")) : null;
            string? referencePath = generate ? Required("DEPLOYSHARP_PADDLEOCR_VL_DETECTED_REFERENCE") : null;
            using var reference = generate ? JsonDocument.Parse(File.ReadAllBytes(referencePath!)) : null;
            if (generate)
            {
                Assert.AreEqual("reference-generation-complete", reference!.RootElement.GetProperty("status").GetString());
                Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
                report["referenceSha256"] = HashFile(referencePath!);
                report["graphs"] = new[] { "vision", "embedding", "decoder", "head" }.Select(role => new { role, sha256 = HashFile(Path.Combine(bundleRoot!, role + ".onnx")) }).ToArray();
            }
            var pipeline = new OpenCvPaddleOcrVlDocumentPipeline(detector, descriptor,
                new PaddleDocumentModelDescriptor("paddleocr-vl/1.6", "PaddleOCR-VL-1.6", PaddleDocumentModule.VisionLanguageParsing,
                    "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx"), "onnxruntime-cpu",
                async (context, region, index, input, token) =>
                {
                    string id = "page-" + context.Page.PageIndex + "-region-" + index;
                    float[] patches = input.PixelPatches.ToArray();
                    byte[] bytes = new byte[checked(patches.Length * sizeof(float))]; Buffer.BlockCopy(patches, 0, bytes, 0, bytes.Length);
                    string patchSha = Hash(bytes);
                    var row = new Dictionary<string, object?>
                    {
                        ["id"] = id, ["pageIndex"] = context.Page.PageIndex, ["regionIndex"] = index, ["category"] = region.Category,
                        ["task"] = input.ImagePlan.Task.ToString(), ["sourceBounds"] = new { x = region.Bounds.X, y = region.Bounds.Y, width = region.Bounds.Width, height = region.Bounds.Height },
                        ["sourceSize"] = new[] { input.ImagePlan.SourceSize.Width, input.ImagePlan.SourceSize.Height },
                        ["cropRgbSha256"] = input.SourceSha256, ["patchSha256"] = patchSha, ["patchFloatCount"] = patches.Length
                    };
                    rows.Add(row); Save();
                    if (!generate)
                        return new PaddleOcrVlGenerationResult(input.ImagePlan.Task, "preparation-only", Array.Empty<int>(), GenerationFinishReason.MaxTokens, 0, 0, input.SourceSha256, TimeSpan.Zero);
                    JsonElement expected = reference!.RootElement.GetProperty("cases").EnumerateArray().Single(item => item.GetProperty("id").GetString() == id);
                    Assert.AreEqual(expected.GetProperty("tensors").GetProperty("pixel_values").GetProperty("sha256").GetString(), patchSha, id + " patch parity");
                    var generated = await session!.GenerateAsync(input, tokenizer!,
                        new PaddleOcrVlGenerationOptions(maximumNewTokens: specification.GetProperty("maximumNewTokens").GetInt32(), timeout: TimeSpan.FromMinutes(15)), token);
                    int[] expectedIds = expected.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    bool tokenExact = generated.TokenIds.SequenceEqual(expectedIds), textExact = generated.Text == expected.GetProperty("decoded").GetString();
                    row["tokenIds"] = generated.TokenIds; row["tokenCount"] = generated.TokenIds.Count; row["text"] = generated.Text;
                    row["tokenExact"] = tokenExact; row["textExact"] = textExact; row["finishReason"] = generated.FinishReason.ToString();
                    row["elapsedMilliseconds"] = generated.Elapsed.TotalMilliseconds; Save();
                    Assert.IsTrue(tokenExact, id + " tokens"); Assert.IsTrue(textExact, id + " text");
                    Assert.AreEqual(GenerationFinishReason.EndOfSequence, generated.FinishReason, id + " EOS");
                    return generated;
                }, maximumRegions: layoutSpec.GetProperty("maximumRegions").GetInt32());
            var results = await pipeline.RunManyAsync(pages);
            Assert.AreEqual(pages.Length, results.Count);
            var layouts = new List<object>();
            for (int pageIndex = 0; pageIndex < results.Count; pageIndex++)
            {
                var result = results[pageIndex];
                Assert.AreSame(pages[pageIndex].Source, result.Page.Source);
                var detected = result.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection);
                var generated = result.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
                Assert.AreEqual(detected.Regions.Count, generated.Executions.Count);
                Assert.AreEqual(0, generated.FailedCount);
                layouts.Add(new { pageIndex = result.Page.PageIndex, encodedSha256 = ((OpenCvImageSource)result.Page.Source).Sha256,
                    detectionCount = detected.Regions.Count, succeeded = generate ? generated.SucceededCount : (int?)null, skipped = generated.SkippedCount,
                    regions = generated.Executions.Select(execution => new { regionIndex = execution.RegionIndex, category = execution.SourceRegion.Category,
                        score = execution.SourceRegion.Score, bounds = new { x = execution.SourceRegion.Bounds.X, y = execution.SourceRegion.Bounds.Y,
                            width = execution.SourceRegion.Bounds.Width, height = execution.SourceRegion.Bounds.Height },
                        task = execution.Task?.ToString(), skipped = execution.Skipped, skipReason = execution.SkipReason }) });
                if (generate)
                {
                    File.WriteAllText(Path.Combine(output, "page-" + result.Page.PageIndex + ".json"), PaddleDocumentPipelineExport.ToJson(result));
                    File.WriteAllText(Path.Combine(output, "page-" + result.Page.PageIndex + ".md"), PaddleDocumentPipelineExport.ToMarkdown(result));
                }
            }
            if (generate)
            {
                string[] expectedIds = reference!.RootElement.GetProperty("cases").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] actualIds = rows.Cast<Dictionary<string, object?>>().Select(row => (string)row["id"]!).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expectedIds, actualIds, "Every reference crop must execute, without omissions or duplicates.");
            }
            report["pages"] = layouts; report["status"] = generate ? "real-layout-crop-patches-and-generation-exact" : "real-layout-crops-prepared";
            Save();
        }
        catch (Exception exception) { report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw; }
    }

    private static string Required(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + variable + " for the external detector/VL test.");
        return value!;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
}
