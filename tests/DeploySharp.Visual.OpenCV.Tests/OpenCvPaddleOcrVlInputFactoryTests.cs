using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    public void DecodesOnceAndPreservesEncodedSourceIdentity()
    {
        using var image = new Mat(2, 2, MatType.CV_8UC3, new Scalar(30, 20, 10));
        byte[] encoded = ImageCodecs.ImEncode(".png", image);
        OpenCvImageSource source = OpenCvImageSource.FromBytes(encoded);
        PaddleOcrVlPreparedInput prepared = new OpenCvPaddleOcrVlInputFactory().Create(source,
            PaddleOcrVlTask.TextRecognition, minimumPixels: 784, maximumPixels: 784);

        Assert.AreEqual(source.Sha256, prepared.SourceSha256);
        Assert.AreEqual(new VisualSize(2, 2), prepared.ImagePlan.SourceSize);
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
