using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
public sealed class PaddleDocumentFormulaInputTests
{
    private static VisualModelProfile Profile(int width, int height) => PaddleDocumentProfiles.CreateFormula(
        PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s"),
        new PaddleDocumentFormulaSchema(new[] { "<s>", "x", "</s>" }, 2, 0), modelSize: new VisualSize(width, height)).VisualProfile;

    [TestMethod]
    public void UniformImagesAreFiniteAndKeepExpectedGrayNormalization()
    {
        foreach (byte level in new byte[] { 0, 255 })
        {
            var image = new OpenCvBgrImage(16, 16, Enumerable.Repeat(level, 16 * 16 * 3).ToArray(), TensorBufferOwnership.Transfer);
            using var input = new PaddleDocumentFormulaInputFactory().Create(image, Profile(32, 32));
            Assert.IsTrue(((float[])input.Tensor.Buffer).All(value => Math.Abs(value - (level / 255f - .7931f) / .1738f) < .00001f));
            Assert.AreEqual(new TensorShape(1, 1, 32, 32), input.Tensor.Shape);
        }
    }

    [TestMethod]
    public void ColorInputUsesPaddleRgbReaderThenFloatBgrGrayTransform()
    {
        var pixels = new byte[16 * 16 * 3];
        for (int i = 0; i < pixels.Length; i += 3) pixels[i + 2] = 255; // BGR red
        using var input = new PaddleDocumentFormulaInputFactory().Create(new OpenCvBgrImage(16, 16, pixels), Profile(32, 32));
        // The upstream transform deliberately calls COLOR_BGR2GRAY on its RGB array.
        float expected = .114f * ((1 - .7931f) / .1738f) + (.587f + .299f) * (-.7931f / .1738f);
        Assert.IsTrue(((float[])input.Tensor.Buffer).All(value => Math.Abs(value - expected) < .00001f));
    }

    [TestMethod]
    public void MarginCropIsReversibleAndVeryThinInputsAreBounded()
    {
        var pixels = Enumerable.Repeat((byte)255, 16 * 16 * 3).ToArray();
        for (int y = 4; y < 12; y++) for (int x = 2; x < 14; x++)
            for (int c = 0; c < 3; c++) pixels[(y * 16 + x) * 3 + c] = 0;
        using var input = new PaddleDocumentFormulaInputFactory().Create(new OpenCvBgrImage(16, 16, pixels), Profile(32, 32));
        Assert.AreEqual(32f / 12, input.Transform.ScaleX, .00001f);
        Assert.AreEqual(-2f * input.Transform.ScaleX, input.Transform.OffsetX, .00001f);
        Assert.AreEqual(16, input.SourceSize.Width);
        Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentFormulaInputFactory().Create(new OpenCvBgrImage(1, 10000, new byte[30000]), Profile(384, 384)));
    }

    [TestMethod]
    [DataRow(384, 384)]
    [DataRow(768, 768)]
    [DataRow(672, 192)]
    [TestCategory("ExternalModels")]
    public void FormulaInputMatchesPinnedPaddleXPreprocessing(int width, int height)
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ACCURACY") != "1") Assert.Inconclusive("Enable external validation and run Generate-FormulaReference.py first.");
        string root = Path.Combine(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ROOT") ?? @"E:\Model\PaddleDocument", "validation");
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, $"formula-{width}x{height}.f32"));
        var expected = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
        using var input = new OpenCvVisualInputFactory().CreateFromFile(Path.Combine(root, "general_formula_rec_001.png"), Profile(width, height));
        var actual = (float[])input.Tensor.Buffer;
        Assert.AreEqual(expected.Length, actual.Length);
        double maximum = 0, sum = 0;
        for (int i = 0; i < actual.Length; i++)
        {
            double delta = Math.Abs(actual[i] - expected[i]);
            Assert.IsTrue(double.IsFinite(delta)); maximum = Math.Max(maximum, delta); sum += delta;
        }
        Console.WriteLine($"FORMULA_PREPROCESS_REFERENCE shape={width}x{height};maxAbs={maximum:R};meanAbs={sum / actual.Length:R}");
        // Pillow's integer filter coefficient quantization can differ by one pixel.
        Assert.IsTrue(maximum < .046, "At most two gray levels of normalized resampling error are accepted.");
        Assert.IsTrue(sum / actual.Length < .0005, "Widespread numeric differences indicate a preprocessing regression.");
    }
}
