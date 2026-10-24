using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlMixedPageIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public async Task MixedTasksCropTwoPagesAndMatchEveryOfficialToken()
    {
        string bundleRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string modelRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtureRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE");
        string output = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_MIXED_PAGE_OUTPUT"));
        Directory.CreateDirectory(output);
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json")));
        using var reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, fixtures.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
        Assert.AreEqual("reference-generation-complete", reference.RootElement.GetProperty("status").GetString());
        var definitions = new[]
        {
            ("text", "ocr", PaddleOcrVlTask.TextRecognition), ("table", "table", PaddleOcrVlTask.TableRecognition),
            ("formula", "formula", PaddleOcrVlTask.FormulaRecognition), ("chart", "chart", PaddleOcrVlTask.ChartRecognition),
            ("spotting", "spotting", PaddleOcrVlTask.TextSpotting), ("seal", "seal", PaddleOcrVlTask.SealRecognition)
        };
        var cases = new Dictionary<string, CaseSpec>();
        foreach (var (category, referenceTask, task) in definitions)
        {
            JsonElement fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("task").GetString() == task.ToString());
            JsonElement expected = reference.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("task").GetString() == referenceTask);
            Assert.AreEqual(expected.GetProperty("inputSha256").GetString(), fixture.GetProperty("inputSha256").GetString());
            string rgbName = fixture.GetProperty("rgbFile").GetString()!;
            Assert.AreEqual(rgbName, Path.GetFileName(rgbName));
            byte[] rgb = File.ReadAllBytes(Path.Combine(fixtureRoot, rgbName));
            Assert.AreEqual(fixture.GetProperty("rgbSha256").GetString(), Hash(rgb));
            int width = fixture.GetProperty("sourceSize")[0].GetInt32(), height = fixture.GetProperty("sourceSize")[1].GetInt32();
            Assert.AreEqual(checked(width * height * 3), rgb.Length);
            cases.Add(category, new CaseSpec(category, task, width, height, rgb, fixture, expected));
        }
        PageSpec[] specifications =
        {
            Assemble("mixed-page-7.png", 7, new[] { cases["table"], cases["formula"], cases["chart"] }, skipCategory: "figure"),
            Assemble("mixed-page-3.png", 3, new[] { cases["seal"], cases["spotting"], cases["text"] }, skipCategory: "unmapped")
        };
        foreach (PageSpec spec in specifications) WritePage(output, spec);
        File.WriteAllText(Path.Combine(output, "pages.json"), JsonSerializer.Serialize(new
        {
            task = "Auto", maximumNewTokens = 512, taskByCategory = new { spotting = "TextSpotting" },
            pages = specifications.Select(page => new { image = page.Name, width = page.Width, height = page.Height, pageIndex = page.Index,
                regions = page.Regions.Select(region => new { category = region.Category, x = region.Bounds.X, y = region.Bounds.Y, width = region.Bounds.Width, height = region.Bounds.Height }) })
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        PaddleDocumentPage[] pages = specifications.Select(spec => new PaddleDocumentPage(OpenCvImageSource.FromFile(Path.Combine(output, spec.Name)),
            new VisualSize(spec.Width, spec.Height), spec.Index)).ToArray();
        int sourceCalls = 0;
        var rows = new List<object>();
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-mixed-pages-v1", ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision, ["status"] = "running",
            ["referenceSha256"] = HashFile(referencePath), ["processorFixtureManifestSha256"] = HashFile(Path.Combine(fixtureRoot, "fixtures.json")),
            ["pagesManifestSha256"] = HashFile(Path.Combine(output, "pages.json")),
            ["environment"] = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), provider = "CPUExecutionProvider", threads = 4,
                openCv = OpenCvRuntimePreflight.Check().NativeVersion, pageParallelism = 2, sessionCount = 1 },
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OpenCvPaddleOcrVlLayoutStage).Assembly,
                typeof(OnnxRuntimeOptions).Assembly, typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly, typeof(PaddleOcrVlMixedPageIntegrationTests).Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, sha256 = HashFile(assembly.Location) }).ToArray(),
            ["graphs"] = new[] { "vision", "embedding", "decoder", "head" }.Select(role => new { role, sha256 = HashFile(Path.Combine(bundleRoot, role + ".onnx")) }).ToArray(),
            ["pages"] = specifications.Select((spec, index) => new { pageIndex = spec.Index, image = spec.Name, width = spec.Width, height = spec.Height,
                encodedSha256 = ((OpenCvImageSource)pages[index].Source).Sha256, regionCount = spec.Regions.Length }).ToArray(),
            ["regions"] = rows,
            ["boundary"] = "Two lossless mixed canvases assembled from six fixed real task RGB fixtures; manual layout with category-based routing and two explicitly skipped regions. Not independent pages, automatic layout detection, labeled quality or formal performance."
        };
        void Save() => File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        try
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1, enableMemoryPattern: false, enableCpuMemoryArena: false));
            using var session = PaddleOcrVlOnnxSession.Create(registry, bundleRoot,
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
            var tokenizer = new PaddleOcrVlTokenizer(modelRoot);
            var router = PaddleOcrVlTaskRouter.CreatePpStructure(new Dictionary<string, PaddleOcrVlTask?> { ["spotting"] = PaddleOcrVlTask.TextSpotting });
            var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(
                new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                    new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "manual-layout", TimeSpan.Zero, ((OpenCvImageSource)context.Page.Source).Sha256, context.Page.PageIndex),
                    specifications.Single(spec => spec.Index == context.Page.PageIndex).Regions.Select(region => new PaddleDocumentRegion(region.Category, 1f, region.Bounds)))));
            var stage = new OpenCvPaddleOcrVlLayoutStage(Model(PaddleDocumentModule.VisionLanguageParsing), "onnxruntime-cpu", router,
                page => { Interlocked.Increment(ref sourceCalls); return (OpenCvImageSource)page.Source; },
                async (context, region, index, input, token) =>
                {
                    RegionSpec spec = specifications.Single(page => page.Index == context.Page.PageIndex).Regions[index];
                    CaseSpec expected = spec.Case!;
                    Assert.IsNotNull(expected, "An unmapped region must never reach generation.");
                    Assert.AreEqual(expected.Task, input.ImagePlan.Task);
                    Assert.AreEqual(new VisualSize(expected.Width, expected.Height), input.ImagePlan.SourceSize);
                    float[] patches = input.PixelPatches.ToArray();
                    var bytes = new byte[checked(patches.Length * sizeof(float))];
                    Buffer.BlockCopy(patches, 0, bytes, 0, bytes.Length);
                    string patchSha = Hash(bytes);
                    Assert.AreEqual(expected.Fixture.GetProperty("patchSha256").GetString(), patchSha);
                    var generation = await session.GenerateAsync(input, tokenizer, new PaddleOcrVlGenerationOptions(timeout: TimeSpan.FromMinutes(15)), token);
                    int[] expectedIds = expected.Reference.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    bool tokensExact = generation.TokenIds.SequenceEqual(expectedIds), textExact = generation.Text == expected.Reference.GetProperty("decoded").GetString();
                    lock (rows)
                    {
                        rows.Add(new { pageIndex = context.Page.PageIndex, regionIndex = index, category = region.Category, task = generation.Task.ToString(),
                            bounds = new { x = region.Bounds.X, y = region.Bounds.Y, width = region.Bounds.Width, height = region.Bounds.Height },
                            originalEncodedInputSha256 = expected.Fixture.GetProperty("inputSha256").GetString(), cropInputSha256 = input.SourceSha256,
                            patchSha256 = patchSha, patchFloatCount = patches.Length, generatedTokenIds = generation.TokenIds, tokenCount = generation.TokenIds.Count,
                            text = generation.Text, tokenExact = tokensExact, textExact, finishReason = generation.FinishReason.ToString(), elapsedMilliseconds = generation.Elapsed.TotalMilliseconds });
                        Save();
                    }
                    Assert.AreEqual(GenerationFinishReason.EndOfSequence, generation.FinishReason);
                    Assert.IsTrue(tokensExact); Assert.IsTrue(textExact);
                    return generation;
                });
            var actual = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { stage, layout }).RunManyConcurrentAsync(pages, 2);
            Assert.AreEqual(2, sourceCalls);
            for (int index = 0; index < actual.Count; index++)
            {
                var result = actual[index].GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(PaddleDocumentModule.VisionLanguageParsing);
                Assert.AreEqual(specifications[index].Index, result.Metadata.PageIndex);
                Assert.AreEqual(((OpenCvImageSource)pages[index].Source).Sha256, result.Metadata.InputSha256);
                Assert.AreEqual(3, result.SucceededCount); Assert.AreEqual(1, result.SkippedCount); Assert.AreEqual(0, result.FailedCount);
                for (int regionIndex = 0; regionIndex < result.Executions.Count; regionIndex++)
                {
                    RegionSpec spec = specifications[index].Regions[regionIndex];
                    var execution = result.Executions[regionIndex];
                    Assert.AreEqual(regionIndex, execution.RegionIndex);
                    Assert.AreEqual(spec.Bounds, execution.SourceRegion.Bounds);
                    Assert.AreEqual(spec.Case == null, execution.Skipped);
                    if (spec.Case != null)
                    {
                        Assert.AreEqual(spec.Case.Task, execution.Result!.Task);
                        Assert.AreEqual(PaddleDocumentVisionLanguageResult.ModuleForTask(spec.Case.Task), execution.Result.Module);
                    }
                }
                File.WriteAllText(Path.Combine(output, "result-page-" + result.Metadata.PageIndex + ".json"), PaddleDocumentPipelineExport.ToJson(actual[index]));
                File.WriteAllText(Path.Combine(output, "result-page-" + result.Metadata.PageIndex + ".md"), PaddleDocumentPipelineExport.ToMarkdown(actual[index]));
            }
            report["sourceFactoryCalls"] = sourceCalls; report["successfulRegions"] = 6; report["skippedRegions"] = 2;
            report["totalGeneratedTokens"] = cases.Values.Sum(spec => spec.Reference.GetProperty("generatedTokenIds").GetArrayLength());
            report["status"] = "six-tasks-two-mixed-pages-exact";
            Save();
        }
        catch (Exception exception) { report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw; }
    }

    private static PageSpec Assemble(string name, int index, CaseSpec[] cases, string skipCategory)
    {
        var regions = new List<RegionSpec>();
        int y = 19;
        foreach (CaseSpec spec in cases)
        {
            regions.Add(new RegionSpec(spec.Category, new RectangleF(23, y, spec.Width, spec.Height), spec));
            y += spec.Height + 23;
        }
        regions.Insert(1, new RegionSpec(skipCategory, new RectangleF(1, 1, 8, 8), null));
        return new PageSpec(name, index, cases.Max(spec => spec.Width) + 61, y + 13, regions.ToArray());
    }
    private static void WritePage(string output, PageSpec spec)
    {
        using var page = new Mat(spec.Height, spec.Width, MatType.CV_8UC3, new Scalar(255, 255, 255));
        foreach (RegionSpec region in spec.Regions)
        {
            if (region.Case == null) continue;
            CaseSpec input = region.Case;
            var row = new byte[input.Width * 3];
            for (int y = 0; y < input.Height; y++)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    int source = ((y * input.Width) + x) * 3;
                    row[x * 3] = input.Rgb[source + 2]; row[(x * 3) + 1] = input.Rgb[source + 1]; row[(x * 3) + 2] = input.Rgb[source];
                }
                Marshal.Copy(row, 0, IntPtr.Add(page.Data, checked(((int)region.Bounds.Y + y) * (int)page.Step.ToUInt64() + ((int)region.Bounds.X * 3))), row.Length);
            }
        }
        File.WriteAllBytes(Path.Combine(output, spec.Name), ImageCodecs.ImEncode(".png", page));
    }
    private sealed record CaseSpec(string Category, PaddleOcrVlTask Task, int Width, int Height, byte[] Rgb, JsonElement Fixture, JsonElement Reference);
    private sealed record RegionSpec(string Category, RectangleF Bounds, CaseSpec? Case);
    private sealed record PageSpec(string Name, int Index, int Width, int Height, RegionSpec[] Regions);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string Required(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + variable + " for the external mixed-page test.");
        return value!;
    }
    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("paddleocr-vl/1.6", "PaddleOCR-VL-1.6", module, "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
}
