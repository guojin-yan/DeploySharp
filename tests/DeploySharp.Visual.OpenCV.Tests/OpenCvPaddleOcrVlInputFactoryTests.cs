using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
public sealed class OpenCvPaddleOcrVlInputFactoryTests
{
    [TestMethod]
    [TestCategory("ExternalAssets")]
    public void EncodedOfficialImagesMatchPinnedPillowPatches()
    {
        string? fixtureRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES");
        string? inputManifest = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_INPUT_MANIFEST");
        string? imageRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_ENCODED_IMAGE_ROOT");
        if (string.IsNullOrWhiteSpace(fixtureRoot) || string.IsNullOrWhiteSpace(inputManifest) || string.IsNullOrWhiteSpace(imageRoot))
            Assert.Inconclusive("Set the VL processor fixture, input manifest and encoded image root paths.");
        if (!BitConverter.IsLittleEndian) Assert.Inconclusive("The pinned float32 patches are little-endian.");

        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot!, "fixtures.json")));
        using JsonDocument inputs = JsonDocument.Parse(File.ReadAllBytes(inputManifest!));
        Assert.AreEqual("deploysharp-paddleocr-vl-processor-fixtures-v1", fixtures.RootElement.GetProperty("schemaVersion").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, fixtures.RootElement.GetProperty("checkpointRevision").GetString());
        Assert.AreEqual(PaddleOcrVlInputPlanning.OfficialV16Revision, inputs.RootElement.GetProperty("checkpointRevision").GetString());
        var rows = new List<object>();
        var differences = new List<string>();
        var factory = new OpenCvPaddleOcrVlInputFactory(128 * 1024 * 1024, normalizeExifOrientation: false);
        string fullImageRoot = Path.GetFullPath(imageRoot!);
        foreach (JsonElement fixture in fixtures.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = fixture.GetProperty("id").GetString()!;
            JsonElement source = inputs.RootElement.GetProperty("cases").EnumerateArray()
                .Single(item => item.GetProperty("id").GetString() == id);
            string relativePath = source.GetProperty("image").GetString()!.Replace('/', Path.DirectorySeparatorChar);
            string encodedPath = Path.GetFullPath(Path.Combine(fullImageRoot, relativePath));
            Assert.IsTrue(encodedPath.StartsWith(fullImageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), id);
            string encodedSha = Hash(File.ReadAllBytes(encodedPath));
            Assert.AreEqual(source.GetProperty("sha256").GetString(), encodedSha, id);
            Assert.AreEqual(fixture.GetProperty("inputSha256").GetString(), encodedSha, id);
            string patchName = fixture.GetProperty("patchFile").GetString()!;
            Assert.AreEqual(patchName, Path.GetFileName(patchName), id);
            byte[] reference = File.ReadAllBytes(Path.Combine(fixtureRoot!, patchName));
            Assert.AreEqual(fixture.GetProperty("patchSha256").GetString(), Hash(reference), id);
            PaddleOcrVlTask task = Enum.Parse<PaddleOcrVlTask>(fixture.GetProperty("task").GetString()!);
            PaddleOcrVlPreparedInput prepared = factory.CreateFromFile(encodedPath, task);
            Assert.AreEqual(encodedSha, prepared.SourceSha256, id);
            Assert.AreEqual(new TensorShape(fixture.GetProperty("patchShape").EnumerateArray().Select(value => value.GetInt64()).ToArray()),
                prepared.PixelPatches.Shape, id);
            float[] actualValues = prepared.PixelPatches.ToArray();
            Assert.AreEqual(reference.Length, checked(actualValues.Length * sizeof(float)), id);
            var actual = new byte[reference.Length];
            Buffer.BlockCopy(actualValues, 0, actual, 0, actual.Length);
            int bitDifferences = 0;
            double maximumAbsoluteDifference = 0;
            for (int index = 0; index < actualValues.Length; index++)
            {
                float expected = BitConverter.ToSingle(reference, index * sizeof(float));
                if (BitConverter.SingleToInt32Bits(expected) != BitConverter.SingleToInt32Bits(actualValues[index])) bitDifferences++;
                maximumAbsoluteDifference = Math.Max(maximumAbsoluteDifference, Math.Abs((double)expected - actualValues[index]));
            }
            PaddleOcrVlPreparedInput canonical = new OpenCvPaddleOcrVlInputFactory().CreateFromFile(encodedPath, task);
            float[] canonicalValues = canonical.PixelPatches.ToArray();
            int? canonicalDifferences = canonicalValues.Length == actualValues.Length
                ? Enumerable.Range(0, actualValues.Length).Count(index =>
                    BitConverter.SingleToInt32Bits(BitConverter.ToSingle(reference, index * sizeof(float))) != BitConverter.SingleToInt32Bits(canonicalValues[index]))
                : null;
            rows.Add(new { id, task = task.ToString(), fileName = Path.GetFileName(encodedPath), encodedSha256 = encodedSha,
                format = Path.GetExtension(encodedPath).ToLowerInvariant(), patchValues = actualValues.Length,
                officialPatchSha256 = Hash(reference), rawOpenCvPatchSha256 = Hash(actual), rawBitDifferences = bitDifferences,
                canonicalPatchShape = canonical.PixelPatches.Shape.ToString(), canonicalBitDifferences = canonicalDifferences,
                maximumAbsoluteDifference });
            if (bitDifferences != 0) differences.Add(id + ": " + bitDifferences + " differing patch values");
        }

        string? reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_ENCODED_PARITY_REPORT");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            string fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllText(fullReportPath, JsonSerializer.Serialize(new
            {
                schemaVersion = "deploysharp-paddleocr-vl-encoded-parity-v1", generatedUtc = DateTimeOffset.UtcNow,
                status = differences.Count == 0 ? "all-bit-exact" : "decoder-differences-recorded",
                checkpointRevision = PaddleOcrVlInputPlanning.OfficialV16Revision,
                environment = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(), processorCount = Environment.ProcessorCount,
                    openCvManagedVersion = typeof(Mat).Assembly.GetName().Version?.ToString() },
                fixtureManifestSha256 = Hash(File.ReadAllBytes(Path.Combine(fixtureRoot!, "fixtures.json"))),
                inputManifestSha256 = Hash(File.ReadAllBytes(inputManifest!)),
                visualAssemblySha256 = Hash(File.ReadAllBytes(typeof(PaddleOcrVlImageProcessor).Assembly.Location)),
                openCvAssemblySha256 = Hash(File.ReadAllBytes(typeof(OpenCvPaddleOcrVlInputFactory).Assembly.Location)),
                testAssemblySha256 = Hash(File.ReadAllBytes(typeof(OpenCvPaddleOcrVlInputFactoryTests).Assembly.Location)),
                boundary = "Pinned encoded images through production OpenCV decode and managed patch preparation versus official Pillow patches; no VL generation, labelled quality or performance claim.",
                cases = rows
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Assert.IsTrue(rows.Count > 0);
        Assert.AreEqual(0, differences.Count, string.Join(Environment.NewLine, differences));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [TestMethod]
    public void DecodesOnceAndPreservesEncodedSourceIdentity()
    {
        using var image = new Mat(2, 2, MatType.CV_8UC3, new Scalar(30, 20, 10));
        byte[] encoded = ImageCodecs.ImEncode(".png", image);
        OpenCvImageSource source = OpenCvImageSource.FromBytes(encoded);
        PaddleOcrVlPreparedInput prepared = new OpenCvPaddleOcrVlInputFactory().Create(source,
            PaddleOcrVlTask.TextRecognition, minimumPixels: 784, maximumPixels: 784);

        Assert.AreEqual(source.Sha256, prepared.SourceSha256);
        Assert.AreEqual(new VisualSize(2, 2), prepared.ImagePlan.SourceSize);
        Assert.AreEqual(new VisualSize(2, 2), prepared.SourceFrame!.PageSize);
        Assert.AreEqual(0, prepared.SourceFrame.OffsetX);
        Assert.AreEqual(0, prepared.SourceFrame.OffsetY);
        Assert.AreEqual(new TensorShape(2, 2, 3, 14, 14), prepared.PixelPatches.Shape);
        Assert.IsTrue(((Tensor<float>)prepared.PixelPatches).ToArray()[0] < 0f);
    }

    [TestMethod]
    public void FileAndByteEntrypointsProduceTheSamePatchTensor()
    {
        string path = Path.Combine(Path.GetTempPath(), "deploysharp-vl-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var image = new Mat(3, 4, MatType.CV_8UC3, new Scalar(90, 80, 70)))
                File.WriteAllBytes(path, ImageCodecs.ImEncode(".png", image));
            var factory = new OpenCvPaddleOcrVlInputFactory();
            PaddleOcrVlPreparedInput fromFile = factory.CreateFromFile(path, maximumPixels: 3136, minimumPixels: 784);
            PaddleOcrVlPreparedInput fromBytes = factory.CreateFromBytes(File.ReadAllBytes(path), maximumPixels: 3136, minimumPixels: 784);
            CollectionAssert.AreEqual(((Tensor<float>)fromFile.PixelPatches).ToArray(), ((Tensor<float>)fromBytes.PixelPatches).ToArray());
            Assert.AreEqual(fromFile.SourceSha256, fromBytes.SourceSha256);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void NonContiguousPageCropMatchesIndependentRgbPixelsAndClipsBounds()
    {
        using var image = new Mat(6, 9, MatType.CV_8UC3);
        for (int y = 0; y < image.Rows; y++)
        {
            byte[] row = Enumerable.Range(0, image.Cols).SelectMany(x => new[] { (byte)(x + 1), (byte)(y + 10), (byte)(x + y + 20) }).ToArray();
            Marshal.Copy(row, 0, IntPtr.Add(image.Data, y * (int)image.Step.ToUInt64()), row.Length);
        }
        OpenCvImageSource source = OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image));
        using OpenCvDecodedRoiImage page = new OpenCvVisualInputFactory().DecodeForRois(source);
        var factory = new OpenCvPaddleOcrVlInputFactory();
        // Floor/ceil and clipping: [-0.2,1.2 .. 4.4,3.4] -> [0,1 .. 5,4].
        PaddleOcrVlPreparedInput actual = factory.CreateRegion(page, new RectangleF(-.2f, 1.2f, 4.6f, 2.2f), minimumPixels: 784, maximumPixels: 3136);
        byte[] rgb = Enumerable.Range(1, 3).SelectMany(y => Enumerable.Range(0, 5)
            .SelectMany(x => new[] { (byte)(x + y + 20), (byte)(y + 10), (byte)(x + 1) })).ToArray();
        PaddleOcrVlPreparedInput expected = new PaddleOcrVlImageProcessor().Create(rgb, new VisualSize(5, 3), minimumPixels: 784, maximumPixels: 3136);

        Assert.AreEqual(new VisualSize(5, 3), actual.ImagePlan.SourceSize);
        Assert.AreEqual(new VisualSize(9, 6), actual.SourceFrame!.PageSize);
        Assert.AreEqual(new VisualSize(5, 3), actual.SourceFrame.SourceSize);
        Assert.AreEqual(0, actual.SourceFrame.OffsetX);
        Assert.AreEqual(1, actual.SourceFrame.OffsetY);
        CollectionAssert.AreEqual(expected.PixelPatches.ToArray(), actual.PixelPatches.ToArray());
        Assert.AreNotEqual(source.Sha256, actual.SourceSha256);
        Assert.AreEqual(actual.SourceSha256, factory.CreateRegion(page, new RectangleF(0, 1, 5, 3), minimumPixels: 784, maximumPixels: 3136).SourceSha256);
        Assert.AreNotEqual(actual.SourceSha256, factory.CreateRegion(page, new RectangleF(1, 1, 5, 3), minimumPixels: 784, maximumPixels: 3136).SourceSha256);
        Assert.AreEqual(source.Sha256, factory.CreateFromPage(page, minimumPixels: 784, maximumPixels: 3136).SourceSha256);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    public void ReusableCropConvertsGrayOrBgraToRgb(int channels)
    {
        using var image = new Mat(4, 6, channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC4, new Scalar(30, 20, 10, 40));
        using OpenCvDecodedRoiImage page = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image)));
        PaddleOcrVlPreparedInput actual = new OpenCvPaddleOcrVlInputFactory().CreateRegion(page, new RectangleF(1, 1, 2, 2), minimumPixels: 784, maximumPixels: 784);
        byte[] pixel = channels == 1 ? new byte[] { 30, 30, 30 } : new byte[] { 10, 20, 30 };
        PaddleOcrVlPreparedInput expected = new PaddleOcrVlImageProcessor().Create(Enumerable.Range(0, 4).SelectMany(_ => pixel).ToArray(),
            new VisualSize(2, 2), minimumPixels: 784, maximumPixels: 784);
        CollectionAssert.AreEqual(expected.PixelPatches.ToArray(), actual.PixelPatches.ToArray());
    }

    [TestMethod]
    public void ClipsFiniteOversizedBoundsBeforeIntegerConversion()
    {
        using var image = new Mat(5, 6, MatType.CV_8UC3, new Scalar(30, 20, 10));
        using OpenCvDecodedRoiImage page = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image)));
        var factory = new OpenCvPaddleOcrVlInputFactory();
        PaddleOcrVlPreparedInput actual = factory.CreateRegion(page, new RectangleF(1, 1, float.MaxValue, float.MaxValue), minimumPixels: 784, maximumPixels: 3136);
        PaddleOcrVlPreparedInput expected = factory.CreateRegion(page, new RectangleF(1, 1, 5, 4), minimumPixels: 784, maximumPixels: 3136);
        Assert.AreEqual(new VisualSize(5, 4), actual.ImagePlan.SourceSize);
        Assert.AreEqual(expected.SourceSha256, actual.SourceSha256);
        CollectionAssert.AreEqual(expected.PixelPatches.ToArray(), actual.PixelPatches.ToArray());
    }

    [TestMethod]
    public async Task DecodedPageSurvivesSourceRemovalAndSupportsConcurrentCrops()
    {
        string path = Path.Combine(Path.GetTempPath(), "deploysharp-vl-page-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var image = new Mat(20, 30, MatType.CV_8UC3, new Scalar(10, 60, 120)))
                File.WriteAllBytes(path, ImageCodecs.ImEncode(".png", image));
            using OpenCvDecodedRoiImage page = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(path));
            File.Delete(path);
            var factory = new OpenCvPaddleOcrVlInputFactory();
            PaddleOcrVlPreparedInput[] results = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(index => Task.Run(() => factory.CreateRegion(page, new RectangleF(index, 1, 10, 10), minimumPixels: 784, maximumPixels: 784))));
            foreach (PaddleOcrVlPreparedInput result in results)
            {
                Assert.AreEqual(results[0].SourceSha256, result.SourceSha256); // Same RGB content, distinct page locations.
                CollectionAssert.AreEqual(results[0].PixelPatches.ToArray(), result.PixelPatches.ToArray());
            }
            CollectionAssert.AreEqual(Enumerable.Range(0, 8).ToArray(), results.Select(value => value.SourceFrame!.OffsetX).ToArray());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    public void RejectsInvalidCropCancellationSourceLimitAndDisposedOwner()
    {
        using var image = new Mat(5, 6, MatType.CV_8UC3, new Scalar(30, 20, 10));
        var page = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromBytes(ImageCodecs.ImEncode(".png", image)));
        try
        {
            var factory = new OpenCvPaddleOcrVlInputFactory(maximumSourcePixels: 4);
            Assert.ThrowsExactly<OpenCvVisualException>(() => factory.CreateRegion(page, new RectangleF(7, 0, 1, 1)));
            Assert.ThrowsExactly<OpenCvVisualException>(() => factory.CreateRegion(page, new RectangleF(0, 0, 5, 5)));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            OpenCvVisualException cancellation = Assert.ThrowsExactly<OpenCvVisualException>(() => factory.CreateRegion(page, new RectangleF(0, 0, 2, 2), cancellationToken: cancelled.Token));
            Assert.AreEqual(OpenCvErrorCodes.Cancelled, cancellation.ErrorCode);
            Assert.AreEqual(new VisualSize(2, 2), factory.CreateRegion(page, new RectangleF(1, 1, 2, 2), minimumPixels: 784, maximumPixels: 784).ImagePlan.SourceSize);
        }
        finally { page.Dispose(); }
        OpenCvVisualException disposed = Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPaddleOcrVlInputFactory().CreateFromPage(page));
        Assert.AreEqual(OpenCvErrorCodes.ObjectDisposed, disposed.ErrorCode);
    }
}
