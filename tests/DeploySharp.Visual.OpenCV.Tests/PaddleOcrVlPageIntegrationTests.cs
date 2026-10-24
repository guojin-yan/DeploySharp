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
public sealed class PaddleOcrVlPageIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public async Task TwoPagesCropRealTableRegionsAndGenerateExactTokens()
    {
        string bundleRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string modelRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtureRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_REFERENCE");
        string output = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_PAGE_OUTPUT"));
        Directory.CreateDirectory(output);
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json")));
        using JsonDocument reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, fixtures.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpoint").GetProperty("revision").GetString());
        Assert.AreEqual("reference-generation-complete", reference.RootElement.GetProperty("status").GetString());
        JsonElement fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("task").GetString() == "TableRecognition");
        JsonElement expected = reference.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("task").GetString() == "table");
        Assert.AreEqual(expected.GetProperty("inputSha256").GetString(), fixture.GetProperty("inputSha256").GetString());
        JsonElement size = fixture.GetProperty("sourceSize");
        int width = size[0].GetInt32(), height = size[1].GetInt32();
        string rgbName = fixture.GetProperty("rgbFile").GetString()!;
        Assert.AreEqual(rgbName, Path.GetFileName(rgbName));
        byte[] rgb = File.ReadAllBytes(Path.Combine(fixtureRoot, rgbName));
        Assert.AreEqual(fixture.GetProperty("rgbSha256").GetString(), Hash(rgb));
        Assert.AreEqual(width * height * 3, rgb.Length);
        int[] expectedIds = expected.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        string expectedText = expected.GetProperty("decoded").GetString()!;
        var specifications = new[]
        {
            new PageSpec("page-7.png", width + 48, (height * 2) + 66, 7,
                new[] { new RectangleF(29, height + 49, width, height), new RectangleF(17, 19, width, height) }),
            new PageSpec("page-3.png", width + 149, height + 88, 3,
                new[] { new RectangleF(60, 31, width, height) })
        };
        foreach (PageSpec spec in specifications) WritePage(output, spec, width, height, rgb);
        File.WriteAllText(Path.Combine(output, "pages.json"), JsonSerializer.Serialize(new
        {
            task = "TableRecognition", maximumNewTokens = 512,
            pages = specifications.Select(page => new { image = page.Name, width = page.Width, height = page.Height, pageIndex = page.Index,
                regions = page.Regions.Select(region => new { category = "table", x = region.X, y = region.Y, width = region.Width, height = region.Height }) })
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        PaddleDocumentPage[] pages = specifications.Select(spec => new PaddleDocumentPage(
            OpenCvImageSource.FromFile(Path.Combine(output, spec.Name)), new VisualSize(spec.Width, spec.Height), spec.Index)).ToArray();
        var rows = new List<object>();
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-regional-pages-v1", ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision, ["status"] = "running",
            ["referenceSha256"] = HashFile(referencePath),
            ["processorFixtureManifestSha256"] = HashFile(Path.Combine(fixtureRoot, "fixtures.json")),
            ["pagesManifestSha256"] = HashFile(Path.Combine(output, "pages.json")),
            ["regionRgbSha256"] = Hash(rgb), ["originalEncodedInputSha256"] = fixture.GetProperty("inputSha256").GetString(),
            ["environment"] = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), provider = "CPUExecutionProvider", threads = 4,
                openCv = OpenCvRuntimePreflight.Check().NativeVersion, pageParallelism = 2, sessionCount = 1 },
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OpenCvPaddleOcrVlRegionStage).Assembly,
                typeof(OnnxRuntimeOptions).Assembly, typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly,
                typeof(PaddleOcrVlPageIntegrationTests).Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, sha256 = HashFile(assembly.Location) }).ToArray(),
            ["graphs"] = new[] { "vision", "embedding", "decoder", "head" }.Select(role => new { role, sha256 = HashFile(Path.Combine(bundleRoot, role + ".onnx")) }).ToArray(),
            ["pages"] = specifications.Select((spec, index) => new { pageIndex = spec.Index, image = spec.Name,
                width = spec.Width, height = spec.Height, encodedSha256 = ((OpenCvImageSource)pages[index].Source).Sha256, regionCount = spec.Regions.Length }).ToArray(),
            ["regions"] = rows,
            ["boundary"] = "Two lossless canvases assembled from one pinned real table RGB fixture; three manual layout regions (same content), not independent pages or automatic layout detection. Verifies native PNG decoding, noncontiguous crop/RGB/patch parity, managed generation and page ordering/provenance. No labeled quality, backend comparison, throughput or formal P50/P95 claim."
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
            var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(
                new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                    new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "manual-layout", TimeSpan.Zero, ((OpenCvImageSource)context.Page.Source).Sha256, context.Page.PageIndex),
                    specifications.Single(spec => spec.Index == context.Page.PageIndex).Regions.Select(rect => new PaddleDocumentRegion("table", 1f, rect)))));
            var stage = new OpenCvPaddleOcrVlRegionStage(Model(PaddleDocumentModule.TableStructureRecognition), "onnxruntime-cpu", PaddleOcrVlTask.TableRecognition,
                page => (OpenCvImageSource)page.Source, async (context, region, index, input, token) =>
                {
                    Assert.AreEqual(new VisualSize(width, height), input.ImagePlan.SourceSize);
                    float[] patches = input.PixelPatches.ToArray();
                    var bytes = new byte[patches.Length * sizeof(float)];
                    Buffer.BlockCopy(patches, 0, bytes, 0, bytes.Length);
                    string patchSha = Hash(bytes);
                    Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), patchSha);
                    PaddleOcrVlGenerationResult generation = await session.GenerateAsync(input, tokenizer,
                        new PaddleOcrVlGenerationOptions(timeout: TimeSpan.FromMinutes(15)), token);
                    bool tokensExact = generation.TokenIds.SequenceEqual(expectedIds), textExact = generation.Text == expectedText;
                    lock (rows)
                    {
                        rows.Add(new { pageIndex = context.Page.PageIndex, regionIndex = index,
                            bounds = new { x = region.Bounds.X, y = region.Bounds.Y, width = region.Bounds.Width, height = region.Bounds.Height },
                            cropInputSha256 = input.SourceSha256, patchSha256 = patchSha, generatedTokenIds = generation.TokenIds,
                            text = generation.Text, tokenExact = tokensExact, textExact, finishReason = generation.FinishReason.ToString(),
                            elapsedMilliseconds = generation.Elapsed.TotalMilliseconds });
                        Save();
                    }
                    Assert.AreEqual(GenerationFinishReason.EndOfSequence, generation.FinishReason);
                    Assert.IsTrue(tokensExact);
                    Assert.IsTrue(textExact);
                    return generation;
                });
            var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { layout, stage });
            var actual = await pipeline.RunManyConcurrentAsync(pages, 2);
            Assert.AreEqual(2, actual.Count);
            for (int index = 0; index < actual.Count; index++)
            {
                var result = actual[index].GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TableStructureRecognition);
                Assert.AreEqual(specifications[index].Index, result.Metadata.PageIndex);
                Assert.AreEqual(((OpenCvImageSource)pages[index].Source).Sha256, result.Metadata.InputSha256);
                Assert.AreEqual(specifications[index].Regions.Length, result.SucceededCount);
                Assert.AreEqual(0, result.FailedCount);
                Assert.IsFalse(result.CoordinatesAvailable);
                for (int regionIndex = 0; regionIndex < result.Executions.Count; regionIndex++)
                    Assert.AreEqual(specifications[index].Regions[regionIndex], result.Executions[regionIndex].SourceRegion.Bounds);
                File.WriteAllText(Path.Combine(output, "result-page-" + result.Metadata.PageIndex + ".json"), PaddleDocumentPipelineExport.ToJson(actual[index]));
            }
            report["status"] = "three-regions-two-assembled-pages-exact";
            report["totalGeneratedTokens"] = expectedIds.Length * 3;
            Save();
        }
        catch (Exception exception) { report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw; }
    }

    private static void WritePage(string output, PageSpec spec, int width, int height, byte[] rgb)
    {
        using var page = new Mat(spec.Height, spec.Width, MatType.CV_8UC3, new Scalar(255, 255, 255));
        foreach (RectangleF region in spec.Regions)
        {
            var row = new byte[width * 3];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int source = ((y * width) + x) * 3;
                    row[x * 3] = rgb[source + 2]; row[(x * 3) + 1] = rgb[source + 1]; row[(x * 3) + 2] = rgb[source];
                }
                Marshal.Copy(row, 0, IntPtr.Add(page.Data, ((int)region.Y + y) * (int)page.Step.ToUInt64() + ((int)region.X * 3)), row.Length);
            }
        }
        File.WriteAllBytes(Path.Combine(output, spec.Name), ImageCodecs.ImEncode(".png", page));
    }

    private sealed record PageSpec(string Name, int Width, int Height, int Index, RectangleF[] Regions);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static string Required(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + variable + " for the external VL page test.");
        return value!;
    }
    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module) => new("paddleocr-vl/1.6/" + module,
        "PaddleOCR-VL-1.6", module, "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
}
