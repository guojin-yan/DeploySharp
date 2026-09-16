using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class OpenCvInputTests
    {
        private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, name);

        [TestMethod]
        public void OptionsRejectContradictoryNormalizationAndAlpha()
        {
            Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPreprocessOptions(new VisualSize(2, 2), means: new[] { 0f, 1f }, standardDeviations: new[] { 1f }));
            Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPreprocessOptions(new VisualSize(2, 2), colorOrder: VisualColorOrder.Rgb, alphaMode: OpenCvAlphaMode.Preserve));
            Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvPreprocessOptions(new VisualSize(2, 2), outputType: OpenCvOutputType.UInt8, means: new[] { 0f }));
        }

        [TestMethod]
        public void SourceCopiesBytesAndComputesHash()
        {
            byte[] bytes = File.ReadAllBytes(Fixture("rgb.png"));
            OpenCvImageSource source = OpenCvImageSource.FromBytes(bytes);
            bytes[0] = 0;
            Assert.AreEqual(OpenCvImageSourceKind.Bytes, source.Kind);
            Assert.AreEqual(77L, source.Length);
            Assert.AreEqual("ecfec9e141ed0da523a03079244ecaee41da7f262d735c0bb11447f95119184d", source.Sha256);
        }

        [TestMethod]
        public void StreamSourceIsIndependentAndBoundariesAreStable()
        {
            byte[] bytes = File.ReadAllBytes(Fixture("rgb.png"));
            using var stream = new MemoryStream(bytes, writable: true);
            OpenCvImageSource source = OpenCvImageSource.FromStream(stream);
            stream.Position = 0;
            stream.WriteByte(0);

            Assert.AreEqual(OpenCvImageSourceKind.Stream, source.Kind);
            Assert.AreEqual(77L, source.Length);
            Assert.AreEqual("ecfec9e141ed0da523a03079244ecaee41da7f262d735c0bb11447f95119184d", source.Sha256);
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvImageSource.FromFile("relative.png")).ErrorCode);
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvImageSource.FromBytes(Array.Empty<byte>())).ErrorCode);
            Assert.AreEqual(OpenCvErrorCodes.InputBoundary, Assert.ThrowsExactly<OpenCvVisualException>(() => OpenCvImageSource.FromBytes(bytes, 16)).ErrorCode);
        }

        [TestMethod]
        public void RuntimePreflightReportsExactManagedNativePair()
        {
            OpenCvRuntimeInfo info = OpenCvRuntimePreflight.Check();
            Assert.AreEqual("5.0.0", info.ManagedPackageVersion);
            Assert.AreEqual("5.0.0", info.OpenCvVersion);
            Assert.IsTrue(info.IsCompatible);
            Assert.AreEqual("JYPPX.OpenCV.Native", info.NativeLibraryName);
        }

        [TestMethod]
        public void CompactBgrDecodeMatchesExistingUInt8HwcPath()
        {
            OpenCvBgrImage compact = new OpenCvBgrImageFactory().CreateFromFile(Fixture("rgb.png"), "compact");
            var options = new OpenCvPreprocessOptions(
                new VisualSize(compact.Width, compact.Height),
                OpenCvResizeMode.Resize,
                VisualColorOrder.Bgr,
                layout: VisualTensorLayout.Hwc,
                outputType: OpenCvOutputType.UInt8);
            using PreparedVisualInput existing = new OpenCvVisualInputFactory().CreateFromFile(Fixture("rgb.png"), "existing", options);

            Assert.AreEqual(3, compact.Width);
            Assert.AreEqual(2, compact.Height);
            Assert.AreEqual(18, compact.ByteLength);
            Assert.AreEqual("compact", compact.InputId);
            CollectionAssert.AreEqual(((Tensor<byte>)existing.Tensor).ToArray(), compact.ToArray());
        }

        [TestMethod]
        public void DecodeNormalizesJpegExifOrientationForFileAndByteInputs()
        {
            using Mat source = ImageCodecs.ImRead(Fixture("rgb.png"), ImreadModes.Unchanged);
            byte[] jpeg = ImageCodecs.ImEncode(".jpg", source);
            var factory = new OpenCvBgrImageFactory();
            OpenCvBgrImage raw = factory.Create(OpenCvImageSource.FromBytes(jpeg));

            byte[] orientation3 = AddExifOrientation(jpeg, 3);
            OpenCvBgrImage rotated = factory.Create(OpenCvImageSource.FromBytes(orientation3));
            Assert.AreEqual(raw.Width, rotated.Width);
            Assert.AreEqual(raw.Height, rotated.Height);
            AssertPixelsRotated180(raw.ToArray(), rotated.ToArray());

            string path = Path.Combine(Path.GetTempPath(), "deploysharp-exif-" + Guid.NewGuid().ToString("N") + ".jpg");
            try
            {
                File.WriteAllBytes(path, AddExifOrientation(jpeg, 6));
                OpenCvBgrImage clockwise = factory.CreateFromFile(path);
                Assert.AreEqual(raw.Height, clockwise.Width);
                Assert.AreEqual(raw.Width, clockwise.Height);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void CompactBgrImageHonorsCopyAndTransferOwnership()
        {
            byte[] copiedSource = { 1, 2, 3 };
            var copied = new OpenCvBgrImage(1, 1, copiedSource, TensorBufferOwnership.Copy);
            copiedSource[0] = 9;
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, copied.ToArray());

            byte[] transferredSource = { 4, 5, 6 };
            var transferred = new OpenCvBgrImage(1, 1, transferredSource, TensorBufferOwnership.Transfer);
            Assert.AreSame(transferredSource, transferred.GetReadOnlyInteropBuffer());
            transferredSource[0] = 7;
            CollectionAssert.AreEqual(new byte[] { 7, 5, 6 }, transferred.ToArray());
        }

        [TestMethod]
        public void RgbInputProducesNchwFloatTensorAndResizeTransform()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), means: new[] { 1f, 2f, 3f }, standardDeviations: new[] { 1f, 2f, 4f });
            using (PreparedVisualInput input = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options))
            {
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), input.Tensor.Shape);
                Assert.AreEqual(VisualTensorLayout.Nchw, input.Layout);
                Assert.AreEqual(ImageTransformKind.Resize, input.Transform.Kind);
                var tensor = (Tensor<float>)input.Tensor;
                float[] values = tensor.ToArray();
                Assert.AreEqual(12, values.Length);
                Assert.AreEqual(190f, values[0], 0.1f);
                Assert.AreEqual(31f, values[4], 0.1f);
            }
        }

        [TestMethod]
        public void GrayAndAlphaInputsCoverLayoutAndComposite()
        {
            var grayOptions = new OpenCvPreprocessOptions(new VisualSize(3, 2), colorOrder: VisualColorOrder.Gray, layout: VisualTensorLayout.Hwc, outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput gray = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("gray.png")), "images", grayOptions))
            {
                Assert.AreEqual(new TensorShape(2, 3, 1), gray.Tensor.Shape);
                Assert.IsInstanceOfType(gray.Tensor, typeof(Tensor<byte>));
            }

            var alphaOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), colorOrder: VisualColorOrder.Rgb, alphaMode: OpenCvAlphaMode.Composite, layout: VisualTensorLayout.Nhwc, outputType: OpenCvOutputType.UInt8, alphaBackground: new OpenCvRgbColor(255, 255, 255));
            using (PreparedVisualInput alpha = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("alpha.png")), "images", alphaOptions))
            {
                Assert.AreEqual(new TensorShape(1, 2, 2, 3), alpha.Tensor.Shape);
                Assert.IsTrue(((Tensor<byte>)alpha.Tensor).ToArray().Any(value => value > 0));
            }
        }

        [TestMethod]
        public void RgbToGrayLetterboxCenterCropAndBatchHaveDeterministicShapes()
        {
            var grayOptions = new OpenCvPreprocessOptions(new VisualSize(3, 2), colorOrder: VisualColorOrder.Gray, layout: VisualTensorLayout.Hwc, outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput gray = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", grayOptions))
            {
                Assert.AreEqual(new TensorShape(2, 3, 1), gray.Tensor.Shape);
                Assert.IsTrue(((Tensor<byte>)gray.Tensor).ToArray().Any(value => value > 0));
            }

            var letterboxOptions = new OpenCvPreprocessOptions(new VisualSize(6, 6), resizeMode: OpenCvResizeMode.Letterbox, layout: VisualTensorLayout.Nhwc, batchSize: 2, outputType: OpenCvOutputType.UInt8, paddingColor: new OpenCvRgbColor(7, 11, 13));
            using (PreparedVisualInput letterbox = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", letterboxOptions))
            {
                Assert.AreEqual(new TensorShape(2, 6, 6, 3), letterbox.Tensor.Shape);
                Assert.AreEqual(ImageTransformKind.Letterbox, letterbox.Transform.Kind);
                Assert.AreEqual(1f, letterbox.Transform.OffsetY);
            }

            var cropOptions = new OpenCvPreprocessOptions(new VisualSize(2, 2), resizeMode: OpenCvResizeMode.CenterCrop, outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput crop = new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", cropOptions))
            {
                Assert.AreEqual(ImageTransformKind.Crop, crop.Transform.Kind);
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), crop.Tensor.Shape);
            }
        }

        [TestMethod]
        public void RectangleRoiUsesSubMatAndComposesTransformIntoOriginalSourceSpace()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), resizeMode: OpenCvResizeMode.Resize, outputType: OpenCvOutputType.UInt8);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateRectangleRoi(
                OpenCvImageSource.FromFile(Fixture("rgb.png")),
                new JYPPX.DeploySharp.Geometry.RectangleF(1, 0, 2, 2),
                "images",
                options);

            Assert.AreEqual(new VisualSize(3, 2), input.SourceSize);
            Assert.AreEqual(ImageTransformKind.Custom, input.Transform.Kind);
            Assert.AreEqual(-1f, input.Transform.OffsetX, .001f);
            Assert.AreEqual(new JYPPX.DeploySharp.Geometry.PointF(1, 0), input.Transform.ToSource(new JYPPX.DeploySharp.Geometry.PointF(0, 0)));
            Assert.AreEqual(new TensorShape(1, 3, 2, 2), input.Tensor.Shape);
        }

        [TestMethod]
        public async Task DecodedRoiImagePreparesConcurrentRectanglesAndRejectsUseAfterDispose()
        {
            var factory = new OpenCvVisualInputFactory();
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), resizeMode: OpenCvResizeMode.Resize, outputType: OpenCvOutputType.UInt8);
            OpenCvDecodedRoiImage decoded = factory.DecodeForRois(OpenCvImageSource.FromFile(Fixture("rgb.png")));
            Assert.AreEqual(new VisualSize(3, 2), decoded.SourceSize);

            Task<PreparedVisualInput> first = Task.Run(() => decoded.PrepareRectangle(new RectangleRoiGeometry(new RectangleF(0, 0, 2, 2)), "images", options));
            Task<PreparedVisualInput> second = Task.Run(() => decoded.PrepareRectangle(new RectangleRoiGeometry(new RectangleF(1, 0, 2, 2)), "images", options));
            PreparedVisualInput[] prepared = await Task.WhenAll(first, second);
            try
            {
                Assert.AreEqual(0f, prepared[0].Transform.ToSource(new PointF(0, 0)).X, .001f);
                Assert.AreEqual(1f, prepared[1].Transform.ToSource(new PointF(0, 0)).X, .001f);
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), prepared[0].Tensor.Shape);
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), prepared[1].Tensor.Shape);
            }
            finally
            {
                foreach (PreparedVisualInput input in prepared) input.Dispose();
                decoded.Dispose();
            }

            Assert.AreEqual(OpenCvErrorCodes.ObjectDisposed, Assert.ThrowsExactly<OpenCvVisualException>(() => decoded.PrepareRectangle(new RectangleRoiGeometry(new RectangleF(0, 0, 1, 1)), "images", options)).ErrorCode);
        }

        [TestMethod]
        public void PolygonRoiMasksPixelsAndKeepsOriginalSourceProjection()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(3, 2), resizeMode: OpenCvResizeMode.Resize, layout: VisualTensorLayout.Nhwc, outputType: OpenCvOutputType.UInt8);
            var polygon = new PolygonRoiGeometry(new[] { new PointF(0, 0), new PointF(3, 0), new PointF(0, 2) });
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateRoi(OpenCvImageSource.FromFile(Fixture("rgb.png")), polygon, "images", options);

            byte[] values = ((Tensor<byte>)input.Tensor).ToArray();
            Assert.AreEqual(new VisualSize(3, 2), input.SourceSize);
            Assert.AreEqual(new PointF(0, 0), input.Transform.ToSource(new PointF(0, 0)));
            Assert.IsTrue(values.Any(value => value != 0));
            Assert.AreEqual(0, values[((1 * 3) + 2) * 3]);
            Assert.AreEqual(0, values[(((1 * 3) + 2) * 3) + 1]);
            Assert.AreEqual(0, values[(((1 * 3) + 2) * 3) + 2]);
        }

        [TestMethod]
        public void MaskRoiUsesTightCropRejectsEmptyMaskAndCanReuseDecodedImage()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), resizeMode: OpenCvResizeMode.Resize, layout: VisualTensorLayout.Nhwc, outputType: OpenCvOutputType.UInt8);
            byte[] maskValues = { 0, 1, 1, 0, 1, 0 };
            var mask = new MaskRoiGeometry(new VisualSize(3, 2), maskValues);
            using OpenCvDecodedRoiImage decoded = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(Fixture("rgb.png")));
            using PreparedVisualInput input = decoded.Prepare(mask, "images", options);

            Assert.AreEqual(new PointF(1, 0), input.Transform.ToSource(new PointF(0, 0)));
            Assert.AreEqual(new VisualSize(3, 2), input.SourceSize);
            Assert.IsTrue(((Tensor<byte>)input.Tensor).ToArray().Any(value => value != 0));

            var empty = new MaskRoiGeometry(new VisualSize(3, 2), new byte[6]);
            OpenCvVisualException exception = Assert.ThrowsExactly<OpenCvVisualException>(() => decoded.Prepare(empty, "images", options));
            Assert.AreEqual(OpenCvErrorCodes.PreprocessInvalid, exception.ErrorCode);
        }

        [TestMethod]
        public void PerspectiveAndRotatedRoiInputsExposeProjectiveSourceMapping()
        {
            var options = new OpenCvPreprocessOptions(new VisualSize(4, 4), resizeMode: OpenCvResizeMode.Resize, layout: VisualTensorLayout.Nhwc, outputType: OpenCvOutputType.UInt8);
            var quadrilateral = new[] { new PointF(0, 0), new PointF(3, 0), new PointF(2, 2), new PointF(0, 2) };
            using PreparedVisualInput perspective = new OpenCvVisualInputFactory().CreateQuadrilateralRoi(OpenCvImageSource.FromFile(Fixture("rgb.png")), quadrilateral, "images", options);
            Assert.IsTrue(perspective.Transform.IsProjective);
            PointF source = perspective.Transform.ToSource(new PointF(0, 0));
            Assert.AreEqual(0f, source.X, .001f);
            Assert.AreEqual(0f, source.Y, .001f);

            var rotated = new RotatedRectangleRoiGeometry(new PointF(1.5f, 1f), new SizeF(2f, 1.5f), 12f);
            using PreparedVisualInput rotatedInput = new OpenCvVisualInputFactory().CreateRotatedRectangleRoi(OpenCvImageSource.FromFile(Fixture("rgb.png")), rotated, "images", options);
            Assert.IsTrue(rotatedInput.Transform.IsProjective);
            PointF center = rotatedInput.Transform.ToSource(rotatedInput.Transform.ToModel(rotated.Center));
            Assert.AreEqual(rotated.Center.X, center.X, .001f);
            Assert.AreEqual(rotated.Center.Y, center.Y, .001f);
        }

        [TestMethod]
        public void MultipleRoisPackIntoTrueBatchAndRetainIndependentTransforms()
        {
            var factory = new OpenCvVisualInputFactory();
            var geometries = new IVisualRoiGeometry[]
            {
                new RectangleRoiGeometry(new RectangleF(0, 0, 2, 2)),
                new RectangleRoiGeometry(new RectangleF(1, 0, 2, 2))
            };

            foreach (VisualTensorLayout layout in new[] { VisualTensorLayout.Nchw, VisualTensorLayout.Nhwc })
            {
                var options = new OpenCvPreprocessOptions(
                    new VisualSize(2, 2),
                    resizeMode: OpenCvResizeMode.Resize,
                    layout: layout,
                    outputType: OpenCvOutputType.UInt8,
                    batchSize: 99);
                using PreparedVisualInput input = factory.CreateRoiBatch(
                    OpenCvImageSource.FromFile(Fixture("rgb.png")),
                    geometries,
                    "images",
                    options);

                Assert.AreEqual(2, input.BatchSize);
                Assert.AreEqual(2, input.BatchFrames.Count);
                Assert.AreEqual(layout, input.Layout);
                Assert.AreEqual(layout == VisualTensorLayout.Nchw ? new TensorShape(2, 3, 2, 2) : new TensorShape(2, 2, 2, 3), input.Tensor.Shape);
                Assert.AreEqual(0f, input.BatchFrames[0].Transform.ToSource(new PointF(0, 0)).X, .001f);
                Assert.AreEqual(1f, input.BatchFrames[1].Transform.ToSource(new PointF(0, 0)).X, .001f);
                Assert.AreEqual(0f, input.BatchFrames[0].Transform.ToSource(new PointF(0, 0)).Y, .001f);
                Assert.AreEqual(0f, input.BatchFrames[1].Transform.ToSource(new PointF(0, 0)).Y, .001f);
                Assert.IsTrue(((Tensor<byte>)input.Tensor).ToArray().Any(value => value != 0));
            }
        }

        [TestMethod]
        public void RoiBatchHandlesSubMatStrideAndGrayOrBgraChannelContracts()
        {
            var factory = new OpenCvVisualInputFactory();
            var rois = new IVisualRoiGeometry[]
            {
                new RectangleRoiGeometry(new RectangleF(0, 0, 2, 2)),
                new RectangleRoiGeometry(new RectangleF(1, 0, 2, 2))
            };

            var grayOptions = new OpenCvPreprocessOptions(
                new VisualSize(2, 2),
                resizeMode: OpenCvResizeMode.Resize,
                colorOrder: VisualColorOrder.Gray,
                layout: VisualTensorLayout.Nhwc,
                outputType: OpenCvOutputType.UInt8);
            using (PreparedVisualInput gray = factory.CreateRoiBatch(OpenCvImageSource.FromFile(Fixture("rgb.png")), rois, "images", grayOptions))
            {
                Assert.AreEqual(new TensorShape(2, 2, 2, 1), gray.Tensor.Shape);
                Assert.AreEqual(2, gray.BatchFrames.Count);
                Assert.IsTrue(((Tensor<byte>)gray.Tensor).ToArray().Any(value => value != 0));
            }

            var bgraOptions = new OpenCvPreprocessOptions(
                new VisualSize(2, 2),
                resizeMode: OpenCvResizeMode.Resize,
                colorOrder: VisualColorOrder.Rgba,
                alphaMode: OpenCvAlphaMode.Preserve,
                layout: VisualTensorLayout.Nhwc,
                outputType: OpenCvOutputType.UInt8);
            using PreparedVisualInput bgra = factory.CreateRoiBatch(OpenCvImageSource.FromFile(Fixture("alpha.png")), rois, "images", bgraOptions);
            Assert.AreEqual(new TensorShape(2, 2, 2, 4), bgra.Tensor.Shape);
            Assert.AreEqual(VisualColorOrder.Rgba, bgra.Preprocessing.ColorOrder);
            Assert.IsTrue(((Tensor<byte>)bgra.Tensor).ToArray().Any(value => value != 0));
        }

        [TestMethod]
        public void YoloProfileProducesOfficialLetterboxAndNormalizationContract()
        {
            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV8,
                new ModelId("tests/yolov8n-detect"),
                new string('a', 64),
                YoloLabelSets.Coco80,
                "1367566337fb8056223a1aeb469360747f1b1bcd",
                "8.3.78",
                new YoloDetectionProfileOptions(19, new VisualSize(6, 6)));

            OpenCvPreprocessOptions options = OpenCvYoloPreprocessing.CreateOptions(profile);
            Assert.AreEqual(OpenCvResizeMode.Letterbox, options.ResizeMode);
            Assert.AreEqual(VisualColorOrder.Rgb, options.ColorOrder);
            Assert.AreEqual(VisualTensorLayout.Nchw, options.Layout);
            Assert.AreEqual(OpenCvOutputType.Float32, options.OutputType);
            Assert.AreEqual(new OpenCvRgbColor(114, 114, 114), options.PaddingColor);
            Assert.AreEqual(255f, options.StandardDeviations.Single());

            using (PreparedVisualInput input = new OpenCvVisualInputFactory().Create(
                OpenCvImageSource.FromFile(Fixture("rgb.png")),
                profile.VisualProfile.Input.Name,
                options))
            {
                Assert.AreEqual(new TensorShape(1, 3, 6, 6), input.Tensor.Shape);
                Assert.AreEqual(ImageTransformKind.Letterbox, input.Transform.Kind);
                Assert.AreEqual(1f, input.Transform.OffsetY);
                Assert.AreEqual(3, input.Preprocessing.Scales.Count);
                foreach (float scale in input.Preprocessing.Scales)
                {
                    Assert.AreEqual(1f / 255f, scale, 0.000001f);
                }

                float[] values = ((Tensor<float>)input.Tensor).ToArray();
                const float expectedPadding = 114f / 255f;
                Assert.AreEqual(expectedPadding, values[0], 0.000001f);
                Assert.AreEqual(expectedPadding, values[36], 0.000001f);
                Assert.AreEqual(expectedPadding, values[72], 0.000001f);
            }
        }

        [TestMethod]
        public void SharedProfileContractSwitchesYoloGeometryAndNormalization()
        {
            var shared = new VisualPreprocessingOptions(
                new VisualSize(6, 6),
                VisualResizeMode.Resize,
                VisualColorOrder.Bgr,
                VisualNormalizationOptions.MeanStandardDeviation(new[] { 127.5f }, new[] { 127.5f }));
            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV8,
                new ModelId("tests/yolov8n-custom-preprocess"),
                new string('b', 64),
                YoloLabelSets.Coco80,
                "commit",
                "exporter",
                new YoloDetectionProfileOptions(19, new VisualSize(6, 6), preprocessing: shared));

            OpenCvPreprocessOptions options = OpenCvYoloPreprocessing.CreateOptions(profile);
            Assert.AreEqual(OpenCvResizeMode.Resize, options.ResizeMode);
            Assert.AreEqual(VisualColorOrder.Bgr, options.ColorOrder);
            Assert.AreEqual(127.5f, options.Means.Single());

            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(Fixture("rgb.png"), profile.VisualProfile);
            Assert.AreEqual(ImageTransformKind.Resize, input.Transform.Kind);
            Assert.AreEqual(new TensorShape(1, 3, 6, 6), input.Tensor.Shape);
        }

        [TestMethod]
        public void SharedContractMapsAllCommonGeometryModes()
        {
            var factory = new OpenCvVisualInputFactory();
            foreach (VisualResizeMode mode in new[] { VisualResizeMode.Resize, VisualResizeMode.Letterbox, VisualResizeMode.CenterCrop })
            {
                var shared = new VisualPreprocessingOptions(new VisualSize(6, 6), mode, VisualColorOrder.Rgb, VisualNormalizationOptions.Scale(255f));
                using PreparedVisualInput input = factory.CreateFromFile(Fixture("rgb.png"), "images", shared);
                ImageTransformKind expected = mode == VisualResizeMode.Resize ? ImageTransformKind.Resize : mode == VisualResizeMode.Letterbox ? ImageTransformKind.Letterbox : ImageTransformKind.Crop;
                Assert.AreEqual(expected, input.Transform.Kind);
                Assert.AreEqual(new TensorShape(1, 3, 6, 6), input.Tensor.Shape);
            }
        }

        [TestMethod]
        public void DeimProfileUsesBlackFloorLetterboxAndFullCanvasTargetSizes()
        {
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateDEIMv2(
                new ModelId("tests/deimv2"),
                new PortableDetectorProfileOptions(
                    16,
                    new VisualSize(6, 6),
                    new[] { "object" },
                    artifactSha256: new string('c', 64),
                    upstreamRepository: "https://example.invalid/deim",
                    upstreamCommit: "commit",
                    exporterVersion: "exporter",
                    license: "Apache-2.0"));

            OpenCvPreprocessOptions options = OpenCvPortableDetectorPreprocessing.CreateOptions(profile);
            Assert.AreEqual(OpenCvResizeMode.Letterbox, options.ResizeMode);
            Assert.AreEqual(OpenCvLetterboxRounding.Floor, options.LetterboxRounding);
            Assert.AreEqual(OpenCvRgbColor.Black, options.PaddingColor);
            CollectionAssert.AreEqual(new[] { .485f, .456f, .406f }, options.Means.ToArray());
            CollectionAssert.AreEqual(new[] { .229f, .224f, .225f }, options.StandardDeviations.ToArray());

            using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), Fixture("rgb.png"), profile);
            Assert.AreEqual(new TensorShape(1, 3, 6, 6), input.Tensor.Shape);
            Assert.AreEqual(1, input.AuxiliaryInputs.Count);
            Assert.AreEqual("orig_target_sizes", input.AuxiliaryInputs[0].Name);
            CollectionAssert.AreEqual(new long[] { 6, 6 }, ((Tensor<long>)input.AuxiliaryInputs[0].Tensor).ToArray());
        }

        [TestMethod]
        public void RtDetrProfilesUseSingleSourceAuxiliaryRulesForFilesAndBytes()
        {
            var v2Options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(6, 6),
                new[] { "object" },
                inputName: "images",
                artifactSha256: new string('d', 64),
                upstreamRepository: "https://github.com/lyuwenyu/RT-DETR",
                upstreamCommit: "commit",
                exporterVersion: "torch",
                license: "Apache-2.0",
                rfDetrQueryCount: 300,
                hasDynamicBatchAxis: true);
            PortableDetectorProfile v2 = PortableDetectorProfiles.CreateRTDETRv2(new ModelId("tests/rtdetrv2"), v2Options);
            var factory = new OpenCvVisualInputFactory();
            using PreparedVisualInput file = OpenCvPortableDetectorPreprocessing.CreateFromFile(factory, Fixture("rgb.png"), v2);
            using PreparedVisualInput bytes = OpenCvPortableDetectorPreprocessing.CreateFromBytes(factory, File.ReadAllBytes(Fixture("rgb.png")), v2);
            CollectionAssert.AreEqual(new long[] { 3, 2 }, ((Tensor<long>)file.AuxiliaryInputs.Single().Tensor).ToArray());
            CollectionAssert.AreEqual(((Tensor<float>)file.Tensor).ToArray(), ((Tensor<float>)bytes.Tensor).ToArray());
            CollectionAssert.AreEqual(((Tensor<long>)file.AuxiliaryInputs.Single().Tensor).ToArray(), ((Tensor<long>)bytes.AuxiliaryInputs.Single().Tensor).ToArray());

            foreach (string fixture in new[] { "gray.png", "alpha.png" })
            {
                using PreparedVisualInput converted = OpenCvPortableDetectorPreprocessing.CreateFromFile(factory, Fixture(fixture), v2);
                Assert.AreEqual(new TensorShape(1, 3, 6, 6), converted.Tensor.Shape);
                Assert.AreEqual(VisualColorOrder.Rgb, converted.Preprocessing.ColorOrder);
            }

            var paddleOptions = new PortableDetectorProfileOptions(
                16,
                new VisualSize(6, 6),
                new[] { "object" },
                inputName: "image",
                artifactSha256: new string('e', 64),
                boxesOutputName: "bbox",
                countOutputName: "bbox_num",
                hasDynamicBatchAxis: true,
                paddleCountShape: PortableDetectorCountShape.BatchVector);
            PortableDetectorProfile paddle = PortableDetectorProfiles.CreateRTDETR(new ModelId("tests/rtdetr-paddle"), paddleOptions);
            using PreparedVisualInput prepared = OpenCvPortableDetectorPreprocessing.CreateFromFile(factory, Fixture("rgb.png"), paddle);
            CollectionAssert.AreEqual(new[] { 6f, 6f }, ((Tensor<float>)prepared.AuxiliaryInputs[0].Tensor).ToArray());
            CollectionAssert.AreEqual(new[] { 3f, 2f }, ((Tensor<float>)prepared.AuxiliaryInputs[1].Tensor).ToArray());
        }

        [TestMethod]
        public void YoloScaleUpFalseFlowsToOpenCvGeometry()
        {
            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV5,
                new ModelId("tests/yolov5n-no-scale-up"),
                new string('b', 64),
                YoloLabelSets.Coco80,
                "20d1d78a08277e365d57bfa3a2cce752772d9e59",
                "7.0",
                new YoloDetectionProfileOptions(12, new VisualSize(640, 640), scaleUp: false));

            OpenCvPreprocessOptions options = OpenCvYoloPreprocessing.CreateOptions(profile);
            Assert.IsFalse(options.ScaleUp);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(Fixture("rgb.png"), profile.VisualProfile.Input.Name, options);
            Assert.AreEqual(ImageTransformKind.Letterbox, input.Transform.Kind);
            Assert.AreEqual(1f, input.Transform.ScaleX);
            Assert.AreEqual(1f, input.Transform.ScaleY);
        }

        [TestMethod]
        public void DecoderUsesContentNotExtensionAndRecoversAfterCorruptInput()
        {
            var factory = new OpenCvVisualInputFactory();
            var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), outputType: OpenCvOutputType.UInt8);
            Assert.AreEqual(
                OpenCvErrorCodes.DecodeFailed,
                Assert.ThrowsExactly<OpenCvVisualException>(() => factory.Create(OpenCvImageSource.FromBytes(new byte[] { 1, 2, 3, 4 }), "images", options)).ErrorCode);

            string disguised = Path.Combine(Path.GetTempPath(), "deploysharp-opencv-" + Guid.NewGuid().ToString("N") + ".data");
            try
            {
                File.Copy(Fixture("rgb.png"), disguised);
                using PreparedVisualInput input = factory.Create(OpenCvImageSource.FromFile(disguised), "images", options);
                Assert.AreEqual(new TensorShape(1, 3, 2, 2), input.Tensor.Shape);
            }
            finally
            {
                if (File.Exists(disguised)) File.Delete(disguised);
            }
        }

        [TestMethod]
        public void CancellationIsObservedBeforeNativeDecode()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var options = new OpenCvPreprocessOptions(new VisualSize(2, 2));
                OpenCvVisualException exception = Assert.ThrowsExactly<OpenCvVisualException>(() => new OpenCvVisualInputFactory().Create(OpenCvImageSource.FromFile(Fixture("rgb.png")), "images", options, cancellationToken: cancellation.Token));
                Assert.AreEqual(OpenCvErrorCodes.Cancelled, exception.ErrorCode);
            }
        }

        private static byte[] AddExifOrientation(byte[] jpeg, ushort orientation)
        {
            byte[] app1 =
            {
                0xff, 0xe1, 0x00, 0x22,
                (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
                (byte)'I', (byte)'I', 0x2a, 0x00, 0x08, 0x00, 0x00, 0x00,
                0x01, 0x00,
                0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
                (byte)(orientation & 0xff), (byte)(orientation >> 8), 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };
            var result = new byte[jpeg.Length + app1.Length];
            Buffer.BlockCopy(jpeg, 0, result, 0, 2);
            Buffer.BlockCopy(app1, 0, result, 2, app1.Length);
            Buffer.BlockCopy(jpeg, 2, result, 2 + app1.Length, jpeg.Length - 2);
            return result;
        }

        private static void AssertPixelsRotated180(byte[] source, byte[] actual)
        {
            Assert.AreEqual(source.Length, actual.Length);
            int pixels = source.Length / 3;
            for (int destination = 0; destination < pixels; destination++)
            {
                int expectedOffset = (pixels - 1 - destination) * 3;
                int actualOffset = destination * 3;
                Assert.AreEqual(source[expectedOffset], actual[actualOffset]);
                Assert.AreEqual(source[expectedOffset + 1], actual[actualOffset + 1]);
                Assert.AreEqual(source[expectedOffset + 2], actual[actualOffset + 2]);
            }
        }
    }
}
