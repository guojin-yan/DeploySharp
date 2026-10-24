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
public sealed class PaddleOcrVlSpottingPageIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public async Task FractionalPageCropGeneratesAllOfficialTokensAndMapsEverySpottingPoint()
    {
        string bundle = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string model = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string fixtureRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string referencePath = Required("DEPLOYSHARP_PADDLEOCR_VL_SPOTTING_REFERENCE");
        string output = Path.GetFullPath(Required("DEPLOYSHARP_PADDLEOCR_VL_SPOTTING_OUTPUT"));
        Directory.CreateDirectory(output);
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json")));
        using var reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, reference.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, fixtures.RootElement.GetProperty("checkpointRevision").GetString());
        JsonElement expected = reference.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("id").GetString() == "spotting-demo-1");
        JsonElement fixture = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("task").GetString() == "TextSpotting");
        Assert.AreEqual(expected.GetProperty("inputSha256").GetString(), fixture.GetProperty("inputSha256").GetString());
        string rgbName = fixture.GetProperty("rgbFile").GetString()!;
        Assert.AreEqual(rgbName, Path.GetFileName(rgbName));
        byte[] rgb = File.ReadAllBytes(Path.Combine(fixtureRoot, rgbName));
        Assert.AreEqual(fixture.GetProperty("rgbSha256").GetString(), Hash(rgb));
        int width = fixture.GetProperty("sourceSize")[0].GetInt32(), height = fixture.GetProperty("sourceSize")[1].GetInt32();
        Assert.AreEqual(checked(width * height * 3), rgb.Length);
        const int x = 17, y = 29;
        var pageSize = new VisualSize(width + 61, height + 67);
        string imagePath = Path.Combine(output, "spotting-page.png");
        using (var canvas = new Mat(pageSize.Height, pageSize.Width, MatType.CV_8UC3, new Scalar(255, 255, 255)))
        {
            var row = new byte[width * 3];
            for (int line = 0; line < height; line++)
            {
                for (int column = 0; column < width; column++)
                {
                    int offset = line * row.Length + column * 3;
                    row[column * 3] = rgb[offset + 2]; row[column * 3 + 1] = rgb[offset + 1]; row[column * 3 + 2] = rgb[offset];
                }
                Marshal.Copy(row, 0, IntPtr.Add(canvas.Data, (line + y) * (int)canvas.Step.ToUInt64() + x * 3), row.Length);
            }
            File.WriteAllBytes(imagePath, ImageCodecs.ImEncode(".png", canvas));
        }
        OpenCvImageSource source = OpenCvImageSource.FromFile(imagePath);
        // floor(left/top), ceil(right/bottom) yields exactly the original RGB crop.
        var requested = new RectangleF(x + .25f, y + .75f, width - .5f, height - 1f);
        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-spotting-page-v1", ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["checkpointRevision"] = PaddleOcrVlInputPlanning.OfficialV16Revision, ["status"] = "running",
            ["referenceSha256"] = Hash(File.ReadAllBytes(referencePath)),
            ["processorFixtureManifestSha256"] = Hash(File.ReadAllBytes(Path.Combine(fixtureRoot, "fixtures.json"))),
            ["page"] = new { pageIndex = 7, width = pageSize.Width, height = pageSize.Height, encodedSha256 = source.Sha256,
                requestedBounds = new { x = requested.X, y = requested.Y, width = requested.Width, height = requested.Height },
                actualCrop = new { x, y, width, height } },
            ["environment"] = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), provider = "CPUExecutionProvider", threads = 4,
                openCv = OpenCvRuntimePreflight.Check().NativeVersion },
            ["assemblies"] = new[] { typeof(PaddleOcrVlOnnxSession).Assembly, typeof(OpenCvPaddleOcrVlInputFactory).Assembly,
                typeof(OnnxRuntimeOptions).Assembly, typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly, GetType().Assembly }
                .Select(assembly => new { name = assembly.GetName().Name, sha256 = HashFile(assembly.Location) }).ToArray(),
            ["graphs"] = new[] { "vision", "embedding", "decoder", "head" }.Select(role => new { role, sha256 = HashFile(Path.Combine(bundle, role + ".onnx")) }).ToArray(),
            ["boundary"] = "One lossless page assembled from fixed real Spotting RGB. Fractional manual layout crop; all source pixels, tokens and official source coordinates checked. Page translation checked independently. Not native detector quality, reading order, labelled localization/text quality or formal performance."
        };
        void Save() => File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Save();
        try
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1, enableMemoryPattern: false, enableCpuMemoryArena: false));
            using var session = PaddleOcrVlOnnxSession.Create(registry, bundle,
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
            var tokenizer = new PaddleOcrVlTokenizer(model);
            int sourceCalls = 0;
            var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(
                new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                    new PaddleDocumentResultMetadata(Model(PaddleDocumentModule.LayoutDetection), "manual-layout", TimeSpan.Zero, source.Sha256, 7),
                    new[] { new PaddleDocumentRegion("spotting", 1f, requested) })));
            var stage = new OpenCvPaddleOcrVlRegionStage(Model(PaddleDocumentModule.TextRecognition), "onnxruntime-cpu", PaddleOcrVlTask.TextSpotting,
                page => { sourceCalls++; return (OpenCvImageSource)page.Source; },
                async (context, region, index, input, token) =>
                {
                    Assert.AreEqual(new VisualSize(width, height), input.ImagePlan.SourceSize);
                    Assert.IsTrue(input.ImagePlan.RequiresSpottingUpscale);
                    Assert.AreEqual(pageSize, input.SourceFrame!.PageSize);
                    Assert.AreEqual(x, input.SourceFrame.OffsetX); Assert.AreEqual(y, input.SourceFrame.OffsetY);
                    float[] patches = input.PixelPatches.ToArray();
                    var bytes = new byte[patches.Length * sizeof(float)]; Buffer.BlockCopy(patches, 0, bytes, 0, bytes.Length);
                    string patchSha = Hash(bytes);
                    Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), patchSha);
                    var generation = await session.GenerateAsync(input, tokenizer, new PaddleOcrVlGenerationOptions(timeout: TimeSpan.FromMinutes(15)), token);
                    Assert.AreEqual(GenerationFinishReason.EndOfSequence, generation.FinishReason);
                    CollectionAssert.AreEqual(expected.GetProperty("generatedTokenIds").EnumerateArray().Select(value => value.GetInt32()).ToArray(), generation.TokenIds.ToArray());
                    Assert.AreEqual(expected.GetProperty("rawText").GetString(), generation.Text);
                    report["generation"] = new { cropInputSha256 = input.SourceSha256, patchSha256 = patchSha, patchFloatCount = patches.Length,
                        generatedTokenIds = generation.TokenIds, tokenCount = generation.TokenIds.Count, text = generation.Text,
                        tokenExact = true, textExact = true, finishReason = generation.FinishReason.ToString(), elapsedMilliseconds = generation.Elapsed.TotalMilliseconds };
                    Save(); return generation;
                });
            var page = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { stage, layout })
                .RunAsync(new PaddleDocumentPage(source, pageSize, 7));
            var result = page.GetRequired<PaddleDocumentVisionLanguagePageResult>(PaddleDocumentModule.TextRecognition);
            Assert.IsTrue(result.CoordinatesAvailable); Assert.IsTrue(result.PageCoordinatesAvailable);
            PaddleDocumentVisionLanguageResult regional = result.Executions[0].Result!;
            Assert.AreEqual(requested, regional.SourceRegion!.Bounds);
            var spotting = regional.Spotting!;
            Assert.IsTrue(spotting.Succeeded); Assert.AreEqual(15, spotting.Regions.Count);
            Assert.AreEqual(expected.GetProperty("text").GetString(), spotting.Text);
            for (int region = 0; region < spotting.Regions.Count; region++)
            {
                Assert.AreEqual(expected.GetProperty("texts")[region].GetString(), spotting.Regions[region].Text);
                for (int point = 0; point < 4; point++)
                {
                    double px = expected.GetProperty("sourcePolygons")[region][point][0].GetDouble();
                    double py = expected.GetProperty("sourcePolygons")[region][point][1].GetDouble();
                    Assert.AreEqual(px, spotting.Regions[region].SourcePolygon[point].X);
                    Assert.AreEqual(py, spotting.Regions[region].SourcePolygon[point].Y);
                    Assert.AreEqual(px + x, spotting.Regions[region].PagePolygon![point].X);
                    Assert.AreEqual(py + y, spotting.Regions[region].PagePolygon![point].Y);
                }
            }
            Assert.AreEqual(1, sourceCalls);
            string json = PaddleDocumentPipelineExport.ToJson(page);
            File.WriteAllText(Path.Combine(output, "page-7.json"), json);
            File.WriteAllText(Path.Combine(output, "page-7.md"), PaddleDocumentPipelineExport.ToMarkdown(page));
            File.WriteAllText(Path.Combine(output, "pages.json"), JsonSerializer.Serialize(new { task = "TextSpotting", maximumNewTokens = 512,
                pages = new[] { new { image = "spotting-page.png", width = pageSize.Width, height = pageSize.Height, pageIndex = 7,
                    regions = new[] { new { category = "spotting", x = requested.X, y = requested.Y, width = requested.Width, height = requested.Height } } } } }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            report["sourceFactoryCalls"] = sourceCalls; report["spottingRegionCount"] = spotting.Regions.Count;
            report["sourceAndPagePointCount"] = spotting.Regions.Count * 4;
            report["sourceCoordinatesExact"] = true; report["pageCoordinatesExact"] = true;
            report["exportSha256"] = Hash(File.ReadAllBytes(Path.Combine(output, "page-7.json")));
            report["status"] = "real-spotting-crop-tokens-source-and-page-coordinates-exact"; Save();
        }
        catch (Exception exception) { report["status"] = "failed"; report["error"] = exception.ToString(); Save(); throw; }
    }

    private static PaddleDocumentModelDescriptor Model(PaddleDocumentModule module)
        => new("paddleocr-vl/1.6/" + module, "PaddleOCR-VL-1.6", module, "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Optional real PaddleOCR-VL integration requires " + name);
        return value!;
    }
}
