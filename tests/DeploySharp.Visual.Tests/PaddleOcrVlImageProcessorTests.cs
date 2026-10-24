using System;
using System.Linq;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlImageProcessorTests
{
    [TestMethod]
    public void PacksRgbPlanesInOfficialRasterOrderAndNormalizesToMinusOneToOne()
    {
        var rgb = new byte[]
        {
            0, 128, 255, 0, 128, 255,
            0, 128, 255, 0, 128, 255
        };
        var processor = new PaddleOcrVlImageProcessor();
        PaddleOcrVlPreparedInput prepared = processor.Create(rgb, new VisualSize(2, 2),
            minimumPixels: 784, maximumPixels: 784, sourceSha256: new string('a', 64));

        Assert.AreEqual(new VisualSize(2, 2), prepared.ImagePlan.SourceSize);
        Assert.AreEqual(new VisualSize(28, 28), prepared.ImagePlan.ResizedSize);
        Assert.AreEqual(new TensorShape(2, 2, 3, 14, 14), prepared.PixelPatches.Shape);
        Assert.AreEqual(new string('a', 64), prepared.SourceSha256);

        float[] values = ((Tensor<float>)prepared.PixelPatches).ToArray();
        Assert.AreEqual(2 * 2 * 3 * 14 * 14, values.Length);
        Assert.IsTrue(values.All(float.IsFinite));
        Assert.AreEqual(-1f, values[0], 0.000001f);
        Assert.AreEqual((128f / 127.5f) - 1f, values[14 * 14], 0.000001f);
        Assert.AreEqual(1f, values[2 * 14 * 14], 0.000001f);
    }

    [TestMethod]
    public void SpottingPerformsTheOfficialUpscaleBeforeFinalGeometry()
    {
        byte[] rgb = Enumerable.Repeat((byte)127, 80 * 100 * 3).ToArray();
        var processor = new PaddleOcrVlImageProcessor();
        PaddleOcrVlPreparedInput prepared = processor.Create(rgb, new VisualSize(80, 100),
            PaddleOcrVlTask.TextSpotting, minimumPixels: 784, maximumPixels: 3136);

        Assert.IsTrue(prepared.ImagePlan.RequiresSpottingUpscale);
        Assert.AreEqual(new VisualSize(160, 200), prepared.ImagePlan.PreparedSize);
        Assert.AreEqual(new VisualSize(28, 56), prepared.ImagePlan.ResizedSize);
        Assert.AreEqual(new TensorShape(4, 2, 3, 14, 14), prepared.PixelPatches.Shape);
    }

    [TestMethod]
    public void RejectsWrongPackedRgbLengthAndCancellationBeforeAllocation()
    {
        var processor = new PaddleOcrVlImageProcessor();
        Assert.ThrowsExactly<ArgumentException>(() => processor.Create(new byte[3], new VisualSize(2, 2), minimumPixels: 784, maximumPixels: 784));
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => processor.Create(new byte[2 * 2 * 3], new VisualSize(2, 2), minimumPixels: 784, maximumPixels: 784, cancellationToken: cancellation.Token));
    }
}
