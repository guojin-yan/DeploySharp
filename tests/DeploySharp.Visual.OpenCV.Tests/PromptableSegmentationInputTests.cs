using System;
using System.IO;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class PromptableSegmentationInputTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);

        [TestMethod]
        public void SamLongestSidePaddingNormalizationAndIdentityMatchOfficialContract()
        {
            OpenCvImageSource source = OpenCvImageSource.FromFile(Fixture("rgb.png"));
            using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1(source, imageSize: 4);
            Assert.AreEqual(source.Sha256, input.InputId);
            Assert.AreEqual(new TensorShape(1, 3, 4, 4), input.Tensor.Shape);
            Assert.AreEqual(VisualColorOrder.Rgb, input.Preprocessing.ColorOrder);
            Assert.AreEqual(ImageTransformKind.Letterbox, input.Transform.Kind);
            Assert.AreEqual(0f, input.Transform.OffsetX);
            Assert.AreEqual(0f, input.Transform.OffsetY);
            Assert.AreEqual(4f / 3f, input.Transform.ScaleX, .000001f);
            Assert.AreEqual(3f / 2f, input.Transform.ScaleY, .000001f);

            float[] values = ((Tensor<float>)input.Tensor).ToArray();
            Assert.AreEqual((0f - 123.675f) / 58.395f, values[12], .000001f);
            Assert.AreEqual((0f - 116.28f) / 57.12f, values[28], .000001f);
            Assert.AreEqual((0f - 103.53f) / 57.375f, values[44], .000001f);
        }

        [TestMethod]
        [DataRow("rgb.png")]
        [DataRow("gray.png")]
        [DataRow("alpha.png")]
        public void SamFactoryCoversPngJpegGrayAndAlphaWithoutChangingContract(string fixture)
        {
            byte[] encoded = File.ReadAllBytes(Fixture(fixture));
            OpenCvImageSource source = OpenCvImageSource.FromBytes(encoded);
            using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1(source, imageSize: 8);
            Assert.AreEqual(new TensorShape(1, 3, 8, 8), input.Tensor.Shape);
            Assert.AreEqual(source.Sha256, input.InputId);
            Assert.AreEqual(VisualTensorLayout.Nchw, input.Layout);
            Assert.AreEqual(new VisualSize(8, 8), input.ModelSize);
        }

        [TestMethod]
        public void SamFactoryDecodesJpegBytesOnceIntoTheSameTypedContract()
        {
            const string jpeg = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAIBAQEBAQIBAQECAgICAgQDAgICAgUEBAMEBgUGBgYFBgYGBwkIBgcJBwYGCAsICQoKCgoKBggLDAsKDAkKCgr/2wBDAQICAgICAgUDAwUKBwYHCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgr/wAARCAACAAMDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD6An/ZR/Zb8UeI/EepeJv2a/AGo3MXjDWrSO4vvB1jM6W9vqVzBBCGeIkJHDHHEi9ESNVUAKACiiv9FPCb/k1eQ/8AYFhf/TFM/g3xN/5OTnX/AGF4n/09M//Z";
            byte[] encoded = Convert.FromBase64String(jpeg);
            OpenCvImageSource source = OpenCvImageSource.FromBytes(encoded);
            using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1(source, imageSize: 8);
            Assert.AreEqual(new TensorShape(1, 3, 8, 8), input.Tensor.Shape);
            Assert.AreEqual(source.Sha256, input.InputId);
        }

        [TestMethod]
        public void LongestSideUsesRoundedAxisScalesForPromptMapping()
        {
            using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromBytes(File.ReadAllBytes(Fixture("rgb.png")), imageSize: 5);
            Assert.AreEqual(5f / 3f, input.Transform.ScaleX, .000001f);
            Assert.AreEqual(3f / 2f, input.Transform.ScaleY, .000001f);
            Assert.AreEqual(0f, input.Transform.OffsetX);
            Assert.AreEqual(0f, input.Transform.OffsetY);
        }
    }
}
