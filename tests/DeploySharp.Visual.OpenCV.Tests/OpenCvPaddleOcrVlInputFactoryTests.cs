using System;
using System.IO;
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
}
