using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class VisualRoiContractTests
    {
        [TestMethod]
        public void CustomFilterAppliesTaskClassIncludeExcludeAndDeterministicProvenance()
        {
            var source = new VisualSize(100, 100);
            var items = new[]
            {
                (Id: "left", Center: new PointF(10, 10), ClassIndex: 1),
                (Id: "blocked", Center: new PointF(25, 25), ClassIndex: 1),
                (Id: "wrong-class", Center: new PointF(40, 40), ClassIndex: 2),
                (Id: "outside", Center: new PointF(90, 90), ClassIndex: 1)
            };
            var includeA = new VisualRoi(
                "zone-b",
                new RectangleRoiGeometry(new RectangleF(0, 0, 60, 60)),
                priority: 1,
                taskFilter: new[] { VisualTaskId.ObjectDetection },
                classFilter: new[] { 1 });
            var includeB = new VisualRoi(
                "zone-a",
                new RectangleRoiGeometry(new RectangleF(0, 0, 30, 30)),
                priority: 5,
                taskFilter: new[] { VisualTaskId.ObjectDetection },
                classFilter: new[] { 1 });
            var exclude = new VisualRoi(
                "ignore",
                new RectangleRoiGeometry(new RectangleF(20, 20, 10, 10)),
                inclusionMode: RoiInclusionMode.Exclude,
                taskFilter: new[] { VisualTaskId.ObjectDetection },
                classFilter: new[] { 1 });

            IReadOnlyList<RoiFilteredItem<(string Id, PointF Center, int ClassIndex)>> filtered = VisualRoiCustomFilter.Filter(
                items,
                new VisualRoiSnapshot(source, new[] { includeA, includeB, exclude }),
                VisualTaskId.ObjectDetection,
                (item, _, geometry) => geometry.Contains(item.Center),
                item => item.ClassIndex);

            Assert.AreEqual(1, filtered.Count);
            Assert.AreEqual("left", filtered[0].Item.Id);
            CollectionAssert.AreEqual(new[] { "zone-a", "zone-b" }, filtered[0].RoiIds.ToArray());
            Assert.AreEqual("zone-a", filtered[0].PrimaryRoiId);
        }

        [TestMethod]
        public void CustomFilterRejectsClassFilterWithoutClassSelector()
        {
            var source = new VisualSize(10, 10);
            var roi = new VisualRoi(
                "class-zone",
                new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)),
                taskFilter: new[] { VisualTaskId.ObjectDetection },
                classFilter: new[] { 3 });

            Assert.ThrowsExactly<VisualException>(() => VisualRoiCustomFilter.Filter(
                new[] { "item" },
                new VisualRoiSnapshot(source, new[] { roi }),
                VisualTaskId.ObjectDetection,
                (_, _, _) => true));
        }

        [TestMethod]
        public void RectangleRoiUsesHalfOpenContainmentAndStableBounds()
        {
            var geometry = new RectangleRoiGeometry(new RectangleF(10, 20, 30, 40));

            Assert.AreEqual(1200f, geometry.Area);
            Assert.IsTrue(geometry.Contains(new PointF(10, 20)));
            Assert.IsTrue(geometry.Contains(new PointF(39.99f, 59.99f)));
            Assert.IsFalse(geometry.Contains(new PointF(40, 30)));
            Assert.AreEqual(new RectangleF(10, 20, 30, 40), geometry.Bounds);
        }

        [TestMethod]
        public void AnomalyFilterReportsSourceStatisticsAndHonorsExcludeRois()
        {
            var source = new VisualSize(4, 3);
            var values = new float[]
            {
                .1f, .2f, .3f, .4f,
                .5f, .6f, .7f, .8f,
                .9f, 1f, .05f, .15f
            };
            var map = new AnomalyScoreMap(source, 4, 3, values, AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.MinMax);
            var binary = new AnomalyBinaryMask(4, 3, new byte[] { 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 0 });
            var anomaly = new AnomalyDetectionResult(.8f, null, map, binary, .5f, ImageTransform.Resize(source, source));
            var include = new VisualRoi("inspection", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 3)));
            var exclude = new VisualRoi("occlusion", new RectangleRoiGeometry(new RectangleF(1, 1, 1, 1)), inclusionMode: RoiInclusionMode.Exclude);
            var result = VisualRoiAnomalyFilter.Filter(anomaly, new VisualRoiSnapshot(source, new[] { include, exclude }));

            Assert.AreEqual(1, result.Regions.Count);
            RoiAnomalyRegion region = result.Regions[0];
            Assert.AreEqual(11L, region.CoveredPixelCount);
            Assert.AreEqual(6L, region.AnomalousPixelCount);
            Assert.AreEqual(6d / 11d, region.AnomalousPixelRatio, 0.000001d);
            Assert.AreEqual(.05d + .1d + .2d + .3d + .4d + .5d + .7d + .8d + .9d + 1d + .15d, region.MeanScore * 11d, 0.00001d);
            Assert.AreEqual(1f, region.MaximumScore, 0.000001f);
            Assert.AreEqual(1f, region.Percentile95Score, 0.000001f);
        }

        [TestMethod]
        public void AnomalyFilterRejectsNonSourceMapsAndClassFilters()
        {
            var source = new VisualSize(4, 3);
            var map = new AnomalyScoreMap(source, 2, 2, new[] { .1f, .2f, .3f, .4f }, AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.MinMax);
            var mask = new AnomalyBinaryMask(2, 2, new byte[4]);
            var anomaly = new AnomalyDetectionResult(.1f, null, map, mask, .5f, ImageTransform.Resize(source, source));
            var roi = new VisualRoi("bad", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 3)), classFilter: new[] { 1 });

            Assert.ThrowsExactly<VisualException>(() => VisualRoiAnomalyFilter.Filter(anomaly, new VisualRoiSnapshot(source, new[] { roi })));
            var sourceMap = new AnomalyScoreMap(source, 4, 3, new float[12], AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.MinMax);
            var sourceMask = new AnomalyBinaryMask(4, 3, new byte[12]);
            var sourceAnomaly = new AnomalyDetectionResult(.1f, null, sourceMap, sourceMask, .5f, ImageTransform.Resize(source, source));
            Assert.ThrowsExactly<VisualException>(() => VisualRoiAnomalyFilter.Filter(sourceAnomaly, new VisualRoiSnapshot(source, new[] { roi })));
        }

        [TestMethod]
        public void AlphaFilterReportsCoverageAndComposerUsesExplicitConflictPolicy()
        {
            var source = new VisualSize(3, 2);
            var alpha = new AlphaMask(3, 2, new[] { 0f, .2f, .4f, .6f, .8f, 1f });
            var model = new JYPPX.DeploySharp.Models.ModelId("tests/rmbg");
            var original = new BackgroundRemovalResult(alpha, source, ImageTransform.Resize(source, source), "rmbg-profile", model);
            var include = new VisualRoi("subject", new RectangleRoiGeometry(new RectangleF(0, 0, 3, 2)), taskFilter: new[] { VisualTaskId.ForegroundMatting });
            var exclude = new VisualRoi("ignore", new RectangleRoiGeometry(new RectangleF(1, 0, 1, 1)), inclusionMode: RoiInclusionMode.Exclude, taskFilter: new[] { VisualTaskId.ForegroundMatting });
            RoiAlphaResult filtered = VisualRoiAlphaFilter.Filter(original, new VisualRoiSnapshot(source, new[] { include, exclude }));

            Assert.AreEqual(1, filtered.Regions.Count);
            Assert.AreEqual(5L, filtered.Regions[0].CoveredPixelCount);
            Assert.AreEqual(3L, filtered.Regions[0].OpaquePixelCount);
            Assert.AreEqual(1f, filtered.Regions[0].Percentile95Alpha, 0.000001f);

            var low = new BackgroundRemovalResult(new AlphaMask(3, 2, new[] { .1f, .3f, .5f, .7f, .9f, .2f }), source, original.Transform, original.ProfileId, model);
            var high = new BackgroundRemovalResult(new AlphaMask(3, 2, new[] { .2f, .2f, .6f, .6f, 1f, .1f }), source, original.Transform, original.ProfileId, model);
            var projected = new[]
            {
                new RoiProjectedResult<BackgroundRemovalResult>("low", 1, low),
                new RoiProjectedResult<BackgroundRemovalResult>("high", 5, high)
            };
            BackgroundRemovalResult composed = new RoiAlphaResultComposer().Compose(projected, RoiAlphaMergeMode.RoiPriority);
            Assert.AreEqual(.2f, composed.Alpha.GetValue(0, 0), 0.000001f);
            Assert.AreEqual(.2f, composed.Alpha.GetValue(1, 0), 0.000001f);
            Assert.AreEqual(1f, composed.Alpha.GetValue(1, 1), 0.000001f);
        }

        [TestMethod]
        public void AlphaFilterRejectsWrongSourceSizeAndClassFilters()
        {
            var source = new VisualSize(2, 2);
            var other = new VisualSize(1, 1);
            var alpha = new BackgroundRemovalResult(new AlphaMask(1, 1, new[] { 1f }), other, ImageTransform.Resize(other, other), "rmbg", new JYPPX.DeploySharp.Models.ModelId("tests/rmbg"));
            var roi = new VisualRoi("bad", new RectangleRoiGeometry(new RectangleF(0, 0, 2, 2)), classFilter: new[] { 1 });
            Assert.ThrowsExactly<VisualException>(() => VisualRoiAlphaFilter.Filter(alpha, new VisualRoiSnapshot(source, new[] { roi })));
            var sourceAlpha = new BackgroundRemovalResult(new AlphaMask(2, 2, new[] { 1f, 1f, 1f, 1f }), source, ImageTransform.Resize(source, source), "rmbg", new JYPPX.DeploySharp.Models.ModelId("tests/rmbg"));
            Assert.ThrowsExactly<VisualException>(() => VisualRoiAlphaFilter.Filter(sourceAlpha, new VisualRoiSnapshot(source, new[] { roi })));
        }

        [TestMethod]
        public void SemanticSegmentationMergerFusesLabelsByRoiPriorityAndRecomputesStatistics()
        {
            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "part", new SegmentationColor(255, 0, 0))
            };
            SemanticSegmentationResult first = CreateSemanticResult(new ushort[] { 0, 0, 1, 1 }, classes);
            SemanticSegmentationResult second = CreateSemanticResult(new ushort[] { 1, 1, 0, 0 }, classes);
            var candidates = new[]
            {
                new RoiProjectedResult<SemanticSegmentationResult>("low", 1, first),
                new RoiProjectedResult<SemanticSegmentationResult>("high", 10, second)
            };

            RoiMergedSemanticSegmentationResult merged = new RoiSemanticSegmentationResultMerger().Merge(candidates, RoiSemanticLabelMergeMode.RoiPriority);

            CollectionAssert.AreEqual(new ushort[] { 1, 1, 0, 0 }, merged.Result.Mask.ToArray());
            CollectionAssert.AreEqual(new[] { "high", "high", "high", "high" }, merged.PixelOwnerRoiIds.ToArray());
            Assert.AreEqual(2L, merged.Result.Statistics.Single(value => value.ClassIndex == 0).PixelCount);
            Assert.AreEqual(2L, merged.Result.Statistics.Single(value => value.ClassIndex == 1).PixelCount);
            Assert.IsNull(merged.Result.ProbabilityMap);
        }

        [TestMethod]
        public void SemanticSegmentationMergerRejectsProbabilityFusionAndMismatchedClasses()
        {
            var classes = new[] { new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true) };
            SemanticSegmentationResult result = CreateSemanticResult(new ushort[] { 0, 0, 0, 0 }, classes);
            var differentClasses = new[] { new SemanticSegmentationClass(0, "other", new SegmentationColor(0, 0, 0), isBackground: true) };
            SemanticSegmentationResult different = CreateSemanticResult(new ushort[] { 0, 0, 0, 0 }, differentClasses);
            var candidates = new[] { new RoiProjectedResult<SemanticSegmentationResult>("a", 1, result), new RoiProjectedResult<SemanticSegmentationResult>("b", 2, different) };

            Assert.ThrowsExactly<VisualException>(() => new RoiSemanticSegmentationResultMerger().Merge(candidates));
            SemanticSegmentationResult withProbability = new SemanticSegmentationResult(
                new SemanticSegmentationMask(2, 2, new ushort[] { 0, 0, 0, 0 }),
                classes,
                new[] { new SegmentationClassStatistics(0, 4, 1) },
                probabilityMap: new SegmentationProbabilityMap(2, 2, 1, new[] { 1f, 1f, 1f, 1f }));
            Assert.ThrowsExactly<NotSupportedException>(() => new RoiSemanticSegmentationResultMerger().Merge(new[]
            {
                new RoiProjectedResult<SemanticSegmentationResult>("a", 1, withProbability),
                new RoiProjectedResult<SemanticSegmentationResult>("b", 2, withProbability)
            }));
        }

        private static SemanticSegmentationResult CreateSemanticResult(ushort[] values, IReadOnlyList<SemanticSegmentationClass> classes)
        {
            var mask = new SemanticSegmentationMask(2, 2, values);
            var statistics = classes.Select(value => new SegmentationClassStatistics(value.Index, values.Count(item => item == value.Index), values.Count(item => item == value.Index) / 4d));
            return new SemanticSegmentationResult(mask, classes, statistics);
        }

        [TestMethod]
        public void PolygonRejectsSelfIntersectionAndPreservesPoints()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new PolygonRoiGeometry(new[]
            {
                new PointF(0, 0), new PointF(10, 10), new PointF(0, 10), new PointF(10, 0)
            }));

            var polygon = new PolygonRoiGeometry(new[]
            {
                new PointF(0, 0), new PointF(10, 0), new PointF(10, 10), new PointF(0, 10)
            });
            Assert.AreEqual(100f, polygon.Area);
            Assert.AreEqual(4, polygon.Points.Count);
            Assert.IsTrue(polygon.Contains(new PointF(5, 5)));
            Assert.IsTrue(polygon.Contains(new PointF(0, 5)));
            Assert.IsTrue(polygon.Contains(new PointF(10, 10)));
            Assert.IsFalse(polygon.Contains(new PointF(float.NaN, 5)));
            Assert.IsFalse(polygon.Contains(new PointF(20, 5)));
        }

        [TestMethod]
        public void MaskRoiOwnsPixelsAndUsesSourceCoordinates()
        {
            var values = new byte[] { 0, 1, 0, 1, 1, 0 };
            var mask = new MaskRoiGeometry(new VisualSize(3, 2), values);
            values[1] = 0;

            Assert.AreEqual(3, mask.PixelCount);
            Assert.IsTrue(mask.Contains(new PointF(1.1f, 0.2f)));
            Assert.IsFalse(mask.Contains(new PointF(0, 0)));
            Assert.IsFalse(mask.Contains(new PointF(float.NaN, 1)));
            Assert.AreEqual(1, mask.ToArray()[1]);
        }

        [TestMethod]
        public void RoiProjectionMapsModelBoxesBackToSourceAndClipsBounds()
        {
            var source = new VisualSize(200, 100);
            var model = new VisualSize(100, 100);
            var roi = new VisualRoi("crop", new RectangleRoiGeometry(new RectangleF(50, 0, 100, 100)), executionMode: RoiExecutionMode.CropAndInfer);
            var projection = new RoiProjection(roi.Id, source, model, ImageTransform.Crop(source, model, new RectangleF(50, 0, 100, 100)));
            var detection = new Detection(new RectangleF(-10, 10, 40, 30), new LabelScore(0, "item", .9f));

            Detection mapped = projection.Project(detection);

            Assert.AreEqual(50f, mapped.Box.X, 0.001f);
            Assert.AreEqual(10f, mapped.Box.Y, 0.001f);
            Assert.AreEqual(30f, mapped.Box.Width, 0.001f);
            Assert.AreEqual(30f, mapped.Box.Height, 0.001f);
            PointF point = projection.ToSource(new PointF(25, 25));
            Assert.AreEqual(75f, point.X, 0.001f);
            Assert.AreEqual(25f, point.Y, 0.001f);
        }

        [TestMethod]
        public void PreparedInputProjectionUsesTheSelectedBatchRowTransformAndRoiBounds()
        {
            VisualSize model = new VisualSize(100, 100);
            VisualSize firstSource = new VisualSize(100, 100);
            VisualSize secondSource = new VisualSize(200, 100);
            var roi = new VisualRoi("right-half", new RectangleRoiGeometry(new RectangleF(.5f, 0, .5f, 1)), RoiCoordinateSpace.Normalized, executionMode: RoiExecutionMode.CropAndInfer);
            var frames = new[]
            {
                new VisualInputFrame(firstSource, model, ImageTransform.Resize(firstSource, model), "first"),
                new VisualInputFrame(secondSource, model, ImageTransform.Crop(secondSource, model, new RectangleF(50, 0, 100, 100)), "second")
            };
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 100, 100), new float[60000]), firstSource, model, 2, VisualTensorLayout.Nchw, frames[0].Transform, batchFrames: frames);

            RoiProjection projection = input.CreateRoiProjection(roi, 1);
            Assert.AreEqual(new RectangleF(100, 0, 50, 100), projection.SourceBounds);
            RectangleF mapped = projection.ToSource(new RectangleF(0, 0, 100, 100));
            Assert.AreEqual(new RectangleF(100, 0, 50, 100), mapped);
            Assert.AreEqual(100f, projection.ToSource(new PointF(50, 50)).X, .001f);
        }

        [TestMethod]
        public void RoiProjectionMapsOrderedPolygonPointsWithoutChangingOrder()
        {
            var projection = new RoiProjection("polygon", new VisualSize(20, 10), new VisualSize(10, 10), ImageTransform.Resize(new VisualSize(20, 10), new VisualSize(10, 10)));
            var points = projection.ToSourcePoints(new[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10) });

            Assert.AreEqual(3, points.Count);
            Assert.AreEqual(new PointF(0, 0), points[0]);
            Assert.AreEqual(new PointF(20, 0), points[1]);
            Assert.AreEqual(new PointF(20, 10), points[2]);
        }

        [TestMethod]
        public void NormalizedSnapshotResolvesToSourcePixelsAndOrdersByPriority()
        {
            var low = new VisualRoi("low", new RectangleRoiGeometry(new RectangleF(0, 0, 0.5f, 0.5f)), RoiCoordinateSpace.Normalized, priority: 1);
            var high = new VisualRoi("high", new RectangleRoiGeometry(new RectangleF(0.25f, 0.25f, 0.5f, 0.5f)), RoiCoordinateSpace.Normalized, priority: 10);
            var snapshot = new VisualRoiSnapshot(new VisualSize(1000, 800), new[] { low, high });

            Assert.AreEqual("high", snapshot.Rois[0].Id);
            Assert.AreEqual(new RectangleF(250, 200, 500, 400), snapshot.Resolve(high).Bounds);
            Assert.AreEqual(RoiExecutionMode.FilterResults, high.ExecutionMode);
        }

        [TestMethod]
        public void NormalizedRotatedRoiUsesQuadrilateralUnderNonUniformSourceScale()
        {
            var rotated = new RotatedRectangleRoiGeometry(new PointF(.5f, .5f), new SizeF(.4f, .2f), 25);
            var roi = new VisualRoi("rotated", rotated, RoiCoordinateSpace.Normalized);
            IVisualRoiGeometry resolved = new VisualRoiSnapshot(new VisualSize(200, 100), new[] { roi }).Resolve(roi);

            Assert.AreEqual(VisualRoiGeometryKind.Polygon, resolved.Kind);
            Assert.AreEqual(4, resolved.Points.Count);
            Assert.AreEqual(1600f, resolved.Area, .001f);
            Assert.IsTrue(resolved.Contains(new PointF(100, 50)));
        }

        [TestMethod]
        public void ModelInputSnapshotResolutionUsesTheExplicitFrameTransform()
        {
            var roi = new VisualRoi("model-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 50, 100)), RoiCoordinateSpace.ModelInput);
            var snapshot = new VisualRoiSnapshot(new VisualSize(200, 100), new[] { roi });
            var frame = new VisualInputFrame(new VisualSize(200, 100), new VisualSize(100, 100), ImageTransform.Crop(new VisualSize(200, 100), new VisualSize(100, 100), new RectangleF(50, 0, 100, 100)));

            IVisualRoiGeometry resolved = snapshot.Resolve(roi, frame);

            Assert.AreEqual(new RectangleF(50, 0, 50, 100), resolved.Bounds);
        }

        [TestMethod]
        public void PreparedInputProjectionSupportsModelInputRoiWithoutGuessingCropGeometry()
        {
            var roi = new VisualRoi("model-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 50, 100)), RoiCoordinateSpace.ModelInput);
            var source = new VisualSize(200, 100);
            var model = new VisualSize(100, 100);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, model, new RectangleF(50, 0, 100, 100)));

            RoiProjection projection = input.CreateRoiProjection(roi);

            Assert.AreEqual(new RectangleF(50, 0, 50, 100), projection.SourceBounds);
            Assert.AreEqual(new RectangleF(50, 0, 50, 100), projection.ToSource(new RectangleF(0, 0, 100, 100)));
        }

        [TestMethod]
        public void TileLocalContextResolvesGeometryAndCopiesMaskIntoSourceCoordinates()
        {
            var source = new VisualSize(10, 8);
            var tile = new RectangleF(3, 2, 4, 3);
            var rectangle = new VisualRoi("tile-box", new RectangleRoiGeometry(new RectangleF(1, 1, 2, 1)), RoiCoordinateSpace.TileLocal);
            var snapshot = new VisualRoiSnapshot(source, new[] { rectangle });
            Assert.AreEqual(new RectangleF(4, 3, 2, 1), snapshot.Resolve(rectangle, VisualRoiCoordinateContext.ForTile(source, tile)).Bounds);

            var mask = new VisualRoi("tile-mask", new MaskRoiGeometry(new VisualSize(4, 3), new byte[] { 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }), RoiCoordinateSpace.TileLocal);
            var maskSnapshot = new VisualRoiSnapshot(source, new[] { mask });
            MaskRoiGeometry resolved = (MaskRoiGeometry)maskSnapshot.Resolve(mask, VisualRoiCoordinateContext.ForTile(source, tile));
            Assert.AreEqual(source, resolved.SourceSize);
            Assert.IsTrue(resolved.Contains(new PointF(4.1f, 2.1f)));
            Assert.IsTrue(resolved.Contains(new PointF(5.1f, 3.1f)));
            Assert.IsTrue(resolved.Contains(new PointF(6.1f, 4.1f)));
            Assert.IsFalse(resolved.Contains(new PointF(0, 0)));
        }

        [TestMethod]
        public void WorldContextMapsAffineAndPerspectiveGeometryAndRasterizesMask()
        {
            var source = new VisualSize(1000, 1000);
            var worldRoi = new VisualRoi("world-zone", new RectangleRoiGeometry(new RectangleF(10, 20, 10, 5)), RoiCoordinateSpace.World);
            var affine = VisualRoiCoordinateContext.ForWorld(source, VisualRoiWorldTransform.CreateAffine(2, 0, 100, 0, 3, 200));
            IVisualRoiGeometry resolved = new VisualRoiSnapshot(source, new[] { worldRoi }).Resolve(worldRoi, affine);
            Assert.AreEqual(VisualRoiGeometryKind.Polygon, resolved.Kind);
            Assert.AreEqual(new RectangleF(120, 260, 20, 15), resolved.Bounds);
            PointF worldPoint = affine.WorldTransform!.ToWorld(new PointF(120, 260));
            Assert.AreEqual(10f, worldPoint.X, .0001f);
            Assert.AreEqual(20f, worldPoint.Y, .0001f);

            var perspective = VisualRoiCoordinateContext.ForWorld(source, new VisualRoiWorldTransform(1, 0, 0, 0, 1, 0, .001f, 0, 1));
            IVisualRoiGeometry perspectiveResolved = new VisualRoiSnapshot(source, new[] { worldRoi }).Resolve(worldRoi, perspective);
            Assert.AreEqual(VisualRoiGeometryKind.Polygon, perspectiveResolved.Kind);

            var worldMask = new VisualRoi("world-mask", new MaskRoiGeometry(new VisualSize(2, 2), new byte[] { 1, 0, 0, 1 }), RoiCoordinateSpace.World);
            var identityContext = VisualRoiCoordinateContext.ForWorld(new VisualSize(2, 2), VisualRoiWorldTransform.CreateAffine(1, 0, 0, 0, 1, 0));
            MaskRoiGeometry resolvedMask = (MaskRoiGeometry)new VisualRoiSnapshot(new VisualSize(2, 2), new[] { worldMask }).Resolve(worldMask, identityContext);
            Assert.AreEqual(2, resolvedMask.PixelCount);
            Assert.IsTrue(resolvedMask.Contains(new PointF(.25f, .25f)));
            Assert.IsTrue(resolvedMask.Contains(new PointF(1.25f, 1.25f)));
            Assert.IsFalse(resolvedMask.Contains(new PointF(1.25f, .25f)));
        }

        [TestMethod]
        public void ImageTransformPerspectiveMapsAllCornersAndRoundTripsInteriorPoint()
        {
            var source = new VisualSize(100, 80);
            var model = new VisualSize(40, 30);
            var sourcePoints = new[] { new PointF(10, 8), new PointF(90, 4), new PointF(84, 70), new PointF(14, 74) };
            var modelPoints = new[] { new PointF(0, 0), new PointF(40, 0), new PointF(40, 30), new PointF(0, 30) };
            ImageTransform transform = ImageTransform.Perspective(source, model, sourcePoints, modelPoints);

            Assert.IsTrue(transform.IsProjective);
            for (int index = 0; index < 4; index++)
            {
                PointF mapped = transform.ToModel(sourcePoints[index]);
                Assert.AreEqual(modelPoints[index].X, mapped.X, .0001f);
                Assert.AreEqual(modelPoints[index].Y, mapped.Y, .0001f);
            }
            PointF interior = new PointF(44, 38);
            PointF roundTrip = transform.ToSource(transform.ToModel(interior));
            Assert.AreEqual(interior.X, roundTrip.X, .0001f);
            Assert.AreEqual(interior.Y, roundTrip.Y, .0001f);
        }

        [TestMethod]
        public void ExplicitModelSpaceProjectorsMapDetectionObbPoseAndTextOnce()
        {
            var source = new VisualSize(100, 100);
            var model = new VisualSize(20, 20);
            var projection = new RoiProjection("crop", source, model, ImageTransform.Crop(source, model, new RectangleF(40, 30, 20, 20)), new RectangleF(40, 30, 20, 20));

            var detection = new DetectionResult(new[] { new Detection(new RectangleF(2, 2, 4, 4), new LabelScore(0, "item", .9f)) });
            DetectionResult projectedDetection = new ModelSpaceDetectionRoiProjector().Project(detection, projection);
            Assert.AreEqual(new RectangleF(42, 32, 4, 4), projectedDetection.Detections[0].Box);

            OrientedQuadrilateral obb = OrientedQuadrilateral.Canonicalize(new[] { new PointF(2, 2), new PointF(6, 2), new PointF(6, 4), new PointF(2, 4) }, OrientedVertexOrder.CounterClockwise);
            var obbResult = new JYPPX.DeploySharp.Visual.OrientedDetectionResult(new[] { new JYPPX.DeploySharp.Visual.OrientedDetection(0, 0, "item", .9f, obb, 0, true) }, model, "obb", new JYPPX.DeploySharp.Models.ModelId("obb"));
            JYPPX.DeploySharp.Visual.OrientedDetectionResult projectedObb = new ModelSpaceOrientedDetectionRoiProjector().Project(obbResult, projection);
            Assert.AreEqual(42f, projectedObb.Detections[0].Quadrilateral.First.X, .001f);
            Assert.IsTrue(projectedObb.Detections[0].HasExactRotatedRectangle);

            var topology = new PoseTopology(new[] { new PoseKeypointDefinition(0, "root") });
            var pose = new PoseEstimationResult(topology, new[] { new PoseInstance(0, .9f, new[] { new PoseKeypoint(0, new PointF(3, 4), .8f, PoseKeypointVisibility.Visible, true) }, new RectangleF(2, 2, 4, 4)) }, model, "pose", new JYPPX.DeploySharp.Models.ModelId("pose"));
            PoseEstimationResult projectedPose = new ModelSpacePoseRoiProjector().Project(pose, projection);
            Assert.AreEqual(new PointF(43, 34), projectedPose.Instances[0].Keypoints[0].Point);

            TextPolygon text = TextPolygon.Canonicalize(new[] { new PointF(1, 1), new PointF(5, 1), new PointF(5, 3), new PointF(1, 3) }, OrientedVertexOrder.CounterClockwise);
            var textResult = new TextDetectionResult(new[] { new JYPPX.DeploySharp.Visual.TextRegion(0, .9f, text) }, model, "text", new JYPPX.DeploySharp.Models.ModelId("text"));
            TextDetectionResult projectedText = new ModelSpaceTextDetectionRoiProjector().Project(textResult, projection);
            Assert.AreEqual(new PointF(41, 31), projectedText.Regions[0].Polygon.Vertices[0]);
        }

        [TestMethod]
        public void DenseModelSpaceProjectorsRestoreInstanceAndSemanticMasksToSource()
        {
            var source = new VisualSize(4, 4);
            var model = new VisualSize(2, 2);
            var projection = new RoiProjection("crop", source, model, ImageTransform.Crop(source, model, new RectangleF(1, 1, 2, 2)), new RectangleF(1, 1, 2, 2));

            var instance = new InstanceSegmentationInstance(0, 1, "item", .9f, new RectangleF(0, 0, 2, 2), new InstanceBinaryMask(2, 2, new byte[] { 1, 0, 0, 1 }));
            var instanceResult = new InstanceSegmentationResult(new[] { instance }, model, "instance", new JYPPX.DeploySharp.Models.ModelId("instance"));
            InstanceSegmentationResult projectedInstance = new ModelSpaceInstanceSegmentationRoiProjector().Project(instanceResult, projection);

            Assert.AreEqual(source, projectedInstance.SourceSize);
            Assert.AreEqual(new RectangleF(1, 1, 2, 2), projectedInstance.Instances[0].BoundingBox);
            Assert.IsTrue(projectedInstance.Instances[0].Mask.IsForeground(1, 1));
            Assert.IsTrue(projectedInstance.Instances[0].Mask.IsForeground(2, 2));
            Assert.IsFalse(projectedInstance.Instances[0].Mask.IsForeground(0, 0));

            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "one", new SegmentationColor(1, 1, 1)),
                new SemanticSegmentationClass(2, "two", new SegmentationColor(2, 2, 2))
            };
            var semantic = new SemanticSegmentationResult(
                new SemanticSegmentationMask(2, 2, new ushort[] { 1, 0, 0, 2 }),
                classes,
                new[] { new SegmentationClassStatistics(0, 2, .5), new SegmentationClassStatistics(1, 1, .25), new SegmentationClassStatistics(2, 1, .25) });
            SemanticSegmentationResult projectedSemantic = new ModelSpaceSemanticSegmentationRoiProjector(0).Project(semantic, projection);

            Assert.AreEqual(4, projectedSemantic.Mask.Width);
            Assert.AreEqual(4, projectedSemantic.Mask.Height);
            Assert.AreEqual(1, projectedSemantic.Mask.GetClassIndex(1, 1));
            Assert.AreEqual(2, projectedSemantic.Mask.GetClassIndex(2, 2));
            Assert.AreEqual(0, projectedSemantic.Mask.GetClassIndex(0, 0));
            Assert.AreEqual(14L, projectedSemantic.Statistics.Single(value => value.ClassIndex == 0).PixelCount);
        }

        [TestMethod]
        public void DenseModelSpaceAlphaProjectorRestoresLocalCropToSource()
        {
            var source = new VisualSize(4, 4);
            var model = new VisualSize(2, 2);
            var projection = new RoiProjection("alpha-crop", source, model, ImageTransform.Crop(source, model, new RectangleF(1, 1, 2, 2)), new RectangleF(1, 1, 2, 2));
            var local = new BackgroundRemovalResult(
                new AlphaMask(2, 2, new[] { .1f, .2f, .3f, .9f }),
                model,
                ImageTransform.Resize(model, model),
                "rmbg",
                new JYPPX.DeploySharp.Models.ModelId("rmbg"));

            BackgroundRemovalResult projected = new ModelSpaceAlphaRoiProjector().Project(local, projection);

            Assert.AreEqual(source, projected.SourceSize);
            Assert.AreEqual(0f, projected.Alpha.GetValue(0, 0));
            Assert.AreEqual(.1f, projected.Alpha.GetValue(1, 1), .0001f);
            Assert.AreEqual(.9f, projected.Alpha.GetValue(2, 2), .0001f);
            Assert.AreEqual(0f, projected.Alpha.GetValue(3, 3));
        }

        [TestMethod]
        public void DenseProjectorsHonorNonAxisAlignedSourceGeometryInsteadOfItsBounds()
        {
            var source = new VisualSize(4, 4);
            var triangle = new PolygonRoiGeometry(new[]
            {
                new PointF(0, 0), new PointF(4, 0), new PointF(0, 4)
            });
            var projection = new RoiProjection("triangle", source, source, ImageTransform.Resize(source, source), sourceGeometry: triangle);
            var local = new BackgroundRemovalResult(
                new AlphaMask(4, 4, Enumerable.Repeat(1f, 16).ToArray()),
                source,
                ImageTransform.Resize(source, source),
                "rmbg",
                new JYPPX.DeploySharp.Models.ModelId("rmbg"));

            BackgroundRemovalResult projected = new ModelSpaceAlphaRoiProjector().Project(local, projection);

            Assert.AreEqual(1f, projected.Alpha.GetValue(0, 0), .0001f);
            Assert.AreEqual(1f, projected.Alpha.GetValue(1, 1), .0001f);
            Assert.AreEqual(0f, projected.Alpha.GetValue(3, 3), .0001f);
            Assert.AreEqual(0f, projected.Alpha.GetValue(3, 1), .0001f);
            Assert.IsTrue(projection.ContainsSourcePixel(1, 1));
            Assert.IsFalse(projection.ContainsSourcePixel(3, 3));
            Assert.IsFalse(projection.ContainsSourcePixel(-1, 0));
        }

        [TestMethod]
        public void RoiProjectionPixelOwnershipHonorsRectangleBounds()
        {
            var source = new VisualSize(8, 8);
            var roi = new RectangleRoiGeometry(new RectangleF(2, 2, 3, 3));
            var projection = new RoiProjection("rect", source, source, ImageTransform.Resize(source, source), sourceGeometry: roi);

            Assert.IsTrue(projection.ContainsSourcePixel(2, 2));
            Assert.IsTrue(projection.ContainsSourcePixel(4, 4));
            Assert.IsFalse(projection.ContainsSourcePixel(1, 2));
            Assert.IsFalse(projection.ContainsSourcePixel(5, 4));
        }

        [TestMethod]
        public void SemanticProbabilityProjectorHonorsNonAxisAlignedSourceGeometry()
        {
            var source = new VisualSize(4, 4);
            var triangle = new PolygonRoiGeometry(new[]
            {
                new PointF(0, 0), new PointF(4, 0), new PointF(0, 4)
            });
            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "object", new SegmentationColor(255, 255, 255))
            };
            var labels = new SemanticSegmentationMask(4, 4, Enumerable.Repeat((ushort)1, 16).ToArray());
            var probabilities = new float[32];
            for (int index = 0; index < 16; index++) { probabilities[index * 2] = 0.1f; probabilities[(index * 2) + 1] = 0.9f; }
            var local = new SemanticSegmentationResult(
                labels,
                classes,
                new[] { new SegmentationClassStatistics(1, 16, 1) },
                probabilityMap: new SegmentationProbabilityMap(4, 4, 2, probabilities));
            var projection = new RoiProjection("triangle", source, source, ImageTransform.Resize(source, source), sourceGeometry: triangle);

            SemanticSegmentationResult projected = new ModelSpaceSemanticSegmentationRoiProjector(0).Project(local, projection);

            Assert.IsNotNull(projected.ProbabilityMap);
            float[] projectedValues = projected.ProbabilityMap!.ToArray();
            Assert.AreEqual(.9f, projectedValues[((1 * source.Width) + 1) * 2 + 1], .0001f);
            Assert.AreEqual(0f, projectedValues[((3 * source.Width) + 3) * 2 + 1], .0001f);
            Assert.AreEqual(0f, projectedValues[((1 * source.Width) + 3) * 2 + 1], .0001f);
        }

        [TestMethod]
        public void DenseModelSpaceAnomalyProjectorRestoresMapAndThresholdMask()
        {
            var source = new VisualSize(4, 4);
            var model = new VisualSize(2, 2);
            var projection = new RoiProjection("anomaly-crop", source, model, ImageTransform.Crop(source, model, new RectangleF(1, 1, 2, 2)), new RectangleF(1, 1, 2, 2));
            var local = new AnomalyDetectionResult(
                .9f,
                null,
                new AnomalyScoreMap(model, 2, 2, new[] { .1f, .7f, .2f, .9f }, AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.None),
                new AnomalyBinaryMask(2, 2, new byte[] { 0, 1, 0, 1 }),
                .5f,
                ImageTransform.Resize(model, model));

            AnomalyDetectionResult projected = new ModelSpaceAnomalyRoiProjector().Project(local, projection);

            Assert.AreEqual(source, projected.NormalizedMap.SourceSize);
            Assert.AreEqual(0f, projected.NormalizedMap.GetValue(0, 0));
            Assert.AreEqual(.1f, projected.NormalizedMap.GetValue(1, 1), .0001f);
            Assert.AreEqual(.9f, projected.NormalizedMap.GetValue(2, 2), .0001f);
            Assert.IsTrue(projected.Mask.IsAnomalous(2, 1));
            Assert.IsTrue(projected.Mask.IsAnomalous(2, 2));
            Assert.IsFalse(projected.Mask.IsAnomalous(0, 0));
        }

        [TestMethod]
        public void AnomalyRoiComposerFusesMapsAndRebuildsThresholdMask()
        {
            var source = new VisualSize(2, 2);
            var first = new AnomalyDetectionResult(
                .4f,
                null,
                new AnomalyScoreMap(source, 2, 2, new[] { .1f, .8f, .2f, .3f }, AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.None),
                new AnomalyBinaryMask(2, 2, new byte[] { 0, 1, 0, 0 }),
                .5f,
                ImageTransform.Resize(source, source));
            var second = new AnomalyDetectionResult(
                .7f,
                null,
                new AnomalyScoreMap(source, 2, 2, new[] { .6f, .4f, .9f, .2f }, AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.None),
                new AnomalyBinaryMask(2, 2, new byte[] { 1, 0, 1, 0 }),
                .5f,
                ImageTransform.Resize(source, source));

            AnomalyDetectionResult merged = new RoiAnomalyResultComposer().Compose(new[]
            {
                new RoiProjectedResult<AnomalyDetectionResult>("a", 1, first),
                new RoiProjectedResult<AnomalyDetectionResult>("b", 2, second)
            });

            Assert.AreEqual(.6f, merged.NormalizedMap.GetValue(0, 0), .0001f);
            Assert.AreEqual(.8f, merged.NormalizedMap.GetValue(1, 0), .0001f);
            Assert.AreEqual(.9f, merged.NormalizedMap.GetValue(0, 1), .0001f);
            Assert.IsTrue(merged.Mask.IsAnomalous(0, 0));
            Assert.IsTrue(merged.Mask.IsAnomalous(1, 0));
            Assert.IsTrue(merged.Mask.IsAnomalous(0, 1));
            Assert.IsFalse(merged.Mask.IsAnomalous(1, 1));
        }

        [TestMethod]
        public void SemanticProbabilityFusionPreservesMapAndRebuildsLabels()
        {
            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "object", new SegmentationColor(1, 1, 1))
            };
            var first = new SemanticSegmentationResult(
                new SemanticSegmentationMask(2, 1, new ushort[] { 0, 1 }),
                classes,
                new[] { new SegmentationClassStatistics(0, 1, .5), new SegmentationClassStatistics(1, 1, .5) },
                probabilityMap: new SegmentationProbabilityMap(2, 1, 2, new[] { .8f, .2f, .2f, .8f }));
            var second = new SemanticSegmentationResult(
                new SemanticSegmentationMask(2, 1, new ushort[] { 1, 0 }),
                classes,
                new[] { new SegmentationClassStatistics(0, 1, .5), new SegmentationClassStatistics(1, 1, .5) },
                probabilityMap: new SegmentationProbabilityMap(2, 1, 2, new[] { .4f, .6f, .7f, .3f }));

            RoiMergedSemanticSegmentationResult merged = new RoiSemanticSegmentationResultMerger().MergeWithProbabilities(new[]
            {
                new RoiProjectedResult<SemanticSegmentationResult>("first", 1, first),
                new RoiProjectedResult<SemanticSegmentationResult>("second", 2, second)
            }, RoiSemanticProbabilityMergeMode.Mean);

            Assert.AreEqual(0, merged.Result.Mask.GetClassIndex(0, 0));
            Assert.AreEqual(1, merged.Result.Mask.GetClassIndex(1, 0));
            Assert.IsNotNull(merged.Result.ProbabilityMap);
            Assert.AreEqual(.6f, merged.Result.ProbabilityMap!.ToArray()[0], .0001f);
            Assert.AreEqual(.45f, merged.Result.ProbabilityMap.ToArray()[2], .0001f);
        }

        [TestMethod]
        public void ModelInputRotatedRoiBecomesPolygonForAnisotropicTransform()
        {
            var roi = new VisualRoi("model-rotated", new RotatedRectangleRoiGeometry(new PointF(50, 50), new SizeF(40, 20), 30), RoiCoordinateSpace.ModelInput);
            var snapshot = new VisualRoiSnapshot(new VisualSize(200, 100), new[] { roi });
            var frame = new VisualInputFrame(new VisualSize(200, 100), new VisualSize(100, 100), ImageTransform.Crop(new VisualSize(200, 100), new VisualSize(100, 100), new RectangleF(0, 0, 200, 100)));

            IVisualRoiGeometry resolved = snapshot.Resolve(roi, frame);

            Assert.AreEqual(VisualRoiGeometryKind.Polygon, resolved.Kind);
            Assert.AreEqual(4, resolved.Points.Count);
        }

        [TestMethod]
        public void ManagerReplaceIncrementsVersionAndRejectsDuplicateIds()
        {
            var manager = new VisualRoiManager(new VisualRoiSnapshot(new VisualSize(100, 100), Array.Empty<VisualRoi>()));
            VisualRoi roi = new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(1, 1, 10, 10)), taskFilter: new[] { VisualTaskId.ObjectDetection });
            VisualRoiSnapshot next = manager.Replace(new VisualSize(200, 100), new[] { roi });

            Assert.AreEqual(2L, next.Version);
            Assert.AreSame(next, manager.Snapshot);
            Assert.ThrowsExactly<ArgumentException>(() => new VisualRoiSnapshot(new VisualSize(100, 100), new[] { roi, roi }));
            Assert.IsTrue(roi.AppliesTo(VisualTaskId.ObjectDetection));
            Assert.IsFalse(roi.AppliesTo(new VisualTaskId("other-task"), 2));
            Assert.AreEqual(1, manager.Snapshot.Rois.Count(value => value.Enabled));
        }

        [TestMethod]
        public void KeypointCoverageRequiresPoseOnlyTaskFilter()
        {
            var missingFilter = new VisualRoi("coverage", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), hitTestMode: RoiHitTestMode.KeypointCoverage);
            Assert.ThrowsExactly<ArgumentException>(() => new VisualRoiSnapshot(new VisualSize(10, 10), new[] { missingFilter }));

            var wrongFilter = new VisualRoi("coverage", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), hitTestMode: RoiHitTestMode.KeypointCoverage, taskFilter: new[] { VisualTaskId.ObjectDetection });
            Assert.ThrowsExactly<ArgumentException>(() => new VisualRoiSnapshot(new VisualSize(10, 10), new[] { wrongFilter }));
        }

        [TestMethod]
        public void RoiFiltersAndMetadataAreCopiedAndDeterministic()
        {
            var tags = new[] { "inspection", "camera" };
            var classes = new[] { 3, 1, 3 };
            var roi = new VisualRoi(
                "filter",
                new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)),
                taskFilter: new[] { VisualTaskId.ObjectDetection },
                classFilter: classes,
                tags: tags,
                metadata: new[] { new System.Collections.Generic.KeyValuePair<string, string>("line", "A") });

            tags[0] = "changed";
            classes[0] = 99;
            Assert.AreEqual(2, roi.Tags.Count);
            CollectionAssert.AreEqual(new[] { 1, 3 }, roi.ClassFilter.ToArray());
            Assert.AreEqual("A", roi.Metadata["line"]);
            Assert.IsTrue(roi.AppliesTo(VisualTaskId.ObjectDetection, 1));
            Assert.IsFalse(roi.AppliesTo(VisualTaskId.ObjectDetection, 2));
        }

        [TestMethod]
        public void DetectionFilterAppliesIncludeExcludeAndIntersectionRules()
        {
            var include = new VisualRoi(
                "include",
                new RectangleRoiGeometry(new RectangleF(0, 0, 50, 100)),
                hitTestMode: RoiHitTestMode.IntersectionOverResult,
                hitThreshold: 0.5f,
                taskFilter: new[] { VisualTaskId.ObjectDetection });
            var exclude = new VisualRoi(
                "exclude",
                new PolygonRoiGeometry(new[] { new PointF(20, 20), new PointF(45, 20), new PointF(45, 45), new PointF(20, 45) }),
                inclusionMode: RoiInclusionMode.Exclude,
                hitTestMode: RoiHitTestMode.CenterPoint,
                taskFilter: new[] { VisualTaskId.ObjectDetection });
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[] { include, exclude });
            var detections = new DetectionResult(new[]
            {
                new Detection(new RectangleF(5, 5, 10, 10), new LabelScore(1, "keep", .9f)),
                new Detection(new RectangleF(25, 25, 10, 10), new LabelScore(1, "exclude", .8f)),
                new Detection(new RectangleF(70, 5, 10, 10), new LabelScore(1, "outside", .7f))
            });

            RoiDetectionResult result = VisualRoiDetectionFilter.Filter(detections, snapshot, VisualTaskId.ObjectDetection);

            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual("keep", result.Detections[0].Detection.Label.Label);
            Assert.AreEqual("include", result.Detections[0].PrimaryRoiId);
        }

        [TestMethod]
        public void DetectionFilterHonorsPerRoiConfidenceOverride()
        {
            var roi = new VisualRoi(
                "confident",
                new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)),
                taskFilter: new[] { VisualTaskId.ObjectDetection },
                confidenceOverride: .8f);
            var detections = new DetectionResult(new[]
            {
                new Detection(new RectangleF(1, 1, 2, 2), new LabelScore(0, "low", .79f)),
                new Detection(new RectangleF(2, 2, 2, 2), new LabelScore(0, "high", .8f))
            });

            RoiDetectionResult result = VisualRoiDetectionFilter.Filter(
                detections,
                new VisualRoiSnapshot(new VisualSize(20, 20), new[] { roi }),
                VisualTaskId.ObjectDetection);

            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual("high", result.Detections[0].Detection.Label.Label);
        }

        [TestMethod]
        public void DetectionRunnerHonorsRectangleMarginAndKeepsSnapshotVersion()
        {
            var roi = new VisualRoi("inspection", new RectangleRoiGeometry(new RectangleF(10, 10, 10, 10)), margin: 5, hitTestMode: RoiHitTestMode.CenterPoint);
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[] { roi });
            var detections = new DetectionResult(new[]
            {
                new Detection(new RectangleF(4, 14, 4, 4), new LabelScore(0, "edge", .9f)),
                new Detection(new RectangleF(0, 0, 2, 2), new LabelScore(0, "outside", .8f))
            });

            RoiDetectionResult result = new VisualRoiDetectionRunner().Run(detections, snapshot);

            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual("edge", result.Detections[0].Detection.Label.Label);
            Assert.AreEqual(snapshot.Version, result.SnapshotVersion);
        }

        [TestMethod]
        public async Task RoiRunnerUsesSnapshotOrderAndPipelineSessionPool()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                float marker = ((float[])inputs.GetRequired("images").Buffer)[0];
                float[] scores = marker > 0 ? new[] { 0f, 1f, 0f } : new[] { 1f, 0f, 0f };
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), scores));
            }, maximumConcurrency: 2);
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("right", new RectangleRoiGeometry(new RectangleF(50, 0, 50, 100)), priority: 10, executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("left", new RectangleRoiGeometry(new RectangleF(0, 0, 50, 100)), priority: 1, executionMode: RoiExecutionMode.CropAndInfer)
            });
            var runner = new VisualRoiRunner(fixture.Pipeline);

            RoiInferenceBatchResult result = await runner.RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                var source = new VisualSize(100, 100);
                var model = new VisualSize(2, 2);
                var tensor = new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]);
                ((float[])tensor.Buffer)[0] = roi.Id == "right" ? 1f : 0f;
                return Task.FromResult(new PreparedVisualInput("images", tensor, source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, model, geometry.Bounds)));
            }, new RoiExecutionOptions(prefetch: 2, correlationId: "roi-test"), CancellationToken.None);

            Assert.AreEqual(2, result.Items.Count);
            Assert.AreEqual("right", result.Items[0].Roi.Id);
            Assert.AreEqual("left", result.Items[1].Roi.Id);
            Assert.IsTrue(result.Items.All(item => item.Succeeded));
            Assert.IsTrue(result.Items.All(item => item.Projection != null));
            Assert.AreEqual("one", result.Items[0].Result!.GetValue<JYPPX.DeploySharp.Results.Vision.ClassificationResult>().TopPrediction!.Label);
            Assert.AreEqual("zero", result.Items[1].Result!.GetValue<JYPPX.DeploySharp.Results.Vision.ClassificationResult>().TopPrediction!.Label);
            Assert.AreEqual("roi-test", result.CorrelationId);
            Assert.AreEqual("one", result.GetSuccessful<ClassificationResult>()[0].Value.TopPrediction!.Label);
            Assert.AreEqual("right", result.GetSuccessful<ClassificationResult>()[0].Roi.Id);
        }

        [TestMethod]
        public async Task RoiRunnerSupportsTrueBatchRowsWithBuiltInBatchSelector()
        {
            var profile = new VisualModelProfile(
                "tests/classification-batch.v1",
                VisualTestData.ClassificationModelId,
                VisualTaskId.ImageClassification,
                "1.0",
                "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, minimumBatch: 1, maximumBatch: 4),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 1));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(2, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(2, 3), new[] { 2f, 0f, 0f, 0f, 2f, 0f })));
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("first", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("second", new RectangleRoiGeometry(new RectangleF(10, 0, 10, 20)), executionMode: RoiExecutionMode.CropAndInfer)
            });

            RoiInferenceBatchResult result = await new VisualRoiRunner(fixture.Pipeline).RunCropBatchAsync(
                snapshot,
                (rois, geometries, token) =>
                {
                    var source = snapshot.SourceSize;
                    var model = new VisualSize(2, 2);
                    var frames = geometries.Select(geometry => new VisualInputFrame(source, model, ImageTransform.Crop(source, model, geometry.Bounds))).ToList();
                    return Task.FromResult(new PreparedVisualInput(
                        "images",
                        new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]),
                        source,
                        model,
                        2,
                        VisualTensorLayout.Nchw,
                        frames[0].Transform,
                        batchFrames: frames));
                },
                VisualRoiBatchResultSelectors.SelectKnownBatchRow);

            Assert.AreEqual(2, result.Succeeded.Count);
            Assert.AreEqual("zero", result.GetSuccessful<ClassificationResult>()[0].Value.TopPrediction!.Label);
            Assert.AreEqual("one", result.GetSuccessful<ClassificationResult>()[1].Value.TopPrediction!.Label);
            Assert.AreEqual(1, fixture.Provider.CreatedSessions.Count);
        }

        [TestMethod]
        public async Task RoiRunnerCanDecodeProjectAndMergeTrueBatchRowsInOneCall()
        {
            var profile = new VisualModelProfile(
                "tests/classification-batch-merge.v1",
                VisualTestData.ClassificationModelId,
                VisualTaskId.ImageClassification,
                "1.0",
                "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, minimumBatch: 1, maximumBatch: 2),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 1));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(2, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(2, 3), new[] { 2f, 0f, 0f, 0f, 2f, 0f })));
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("first", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("second", new RectangleRoiGeometry(new RectangleF(10, 0, 10, 20)), executionMode: RoiExecutionMode.CropAndInfer)
            });

            VisualRoiCropMergedResult<ClassificationResult> merged = await new VisualRoiRunner(fixture.Pipeline).RunCropBatchAndMergeAsync(
                snapshot,
                (rois, geometries, token) =>
                {
                    var source = snapshot.SourceSize;
                    var model = new VisualSize(2, 2);
                    var frames = geometries.Select(geometry => new VisualInputFrame(source, model, ImageTransform.Crop(source, model, geometry.Bounds))).ToList();
                    return Task.FromResult(new PreparedVisualInput(
                        "images",
                        new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]),
                        source,
                        model,
                        2,
                        VisualTensorLayout.Nchw,
                        frames[0].Transform,
                        batchFrames: frames));
                },
                VisualRoiBatchResultSelectors.SelectKnownBatchRow,
                value => value.GetValue<ClassificationResult>(),
                new IdentityRoiResultProjector<ClassificationResult>(),
                new ClassificationRoiResultMergerAdapter(),
                new RoiExecutionOptions(mergeMode: RoiResultMergeMode.KeepAll));

            Assert.AreEqual(2, merged.Batch.Succeeded.Count);
            Assert.AreEqual(2, merged.Merged.Count);
            Assert.AreEqual("zero", merged.Merged[0].TopPrediction!.Label);
            Assert.AreEqual("one", merged.Merged[1].TopPrediction!.Label);
            Assert.AreEqual(2, merged.Candidates.Count);
        }

        [TestMethod]
        public async Task RoiRunnerRejectsTrueBatchAboveConfiguredMaximum()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f })));
            var snapshot = new VisualRoiSnapshot(new VisualSize(10, 10), new[]
            {
                new VisualRoi("one", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 10)), executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("two", new RectangleRoiGeometry(new RectangleF(5, 0, 5, 10)), executionMode: RoiExecutionMode.CropAndInfer)
            });

            await Assert.ThrowsExactlyAsync<VisualException>(() => new VisualRoiRunner(fixture.Pipeline).RunCropBatchAsync(
                snapshot,
                (_, _, _) => throw new InvalidOperationException("The batch preparer must not run when the limit is exceeded."),
                VisualRoiBatchResultSelectors.SelectKnownBatchRow,
                new RoiExecutionOptions(maximumBatchSize: 1)));
        }

        [TestMethod]
        public async Task RoiRunnerAcceptsExplicitTileLocalProjectionContext()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f })));
            var source = new VisualSize(100, 80);
            var roi = new VisualRoi("tile", new RectangleRoiGeometry(new RectangleF(5, 6, 10, 12)), RoiCoordinateSpace.TileLocal, executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification });
            var snapshot = new VisualRoiSnapshot(source, new[] { roi });
            var context = VisualRoiCoordinateContext.ForTile(source, new RectangleF(20, 10, 40, 30));

            RoiInferenceBatchResult result = await new VisualRoiRunner(fixture.Pipeline).RunCropAsync(snapshot, (selected, geometry, token) =>
            {
                var model = new VisualSize(2, 2);
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, model, geometry.Bounds)));
            }, coordinateContext: context);

            Assert.AreEqual(1, result.Succeeded.Count);
            Assert.AreEqual(new RectangleF(25, 16, 10, 12), result.Items[0].Geometry.Bounds);
            Assert.AreEqual(new RectangleF(25, 16, 10, 12), result.Items[0].Projection!.SourceBounds);
        }

        [TestMethod]
        public async Task RoiRunnerExecutesOnlyEnabledIncludeCropRois()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f })));
            var snapshot = new VisualRoiSnapshot(new VisualSize(10, 10), new[]
            {
                new VisualRoi("crop", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("filter", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), executionMode: RoiExecutionMode.FilterResults),
                new VisualRoi("sliding", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), executionMode: RoiExecutionMode.SlidingWindow),
                new VisualRoi("exclude", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), inclusionMode: RoiInclusionMode.Exclude, executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("disabled", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), enabled: false, executionMode: RoiExecutionMode.CropAndInfer)
            });
            int prepared = 0;

            RoiInferenceBatchResult result = await new VisualRoiRunner(fixture.Pipeline).RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                prepared++;
                var source = new VisualSize(10, 10);
                var model = new VisualSize(2, 2);
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, model, geometry.Bounds)));
            });

            Assert.AreEqual(1, prepared);
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("crop", result.Items[0].Roi.Id);
        }

        [TestMethod]
        public async Task RoiRunnerReturnsPartialPreparationFailuresWithoutRepeatingSuccessfulInference()
        {
            int inferenceCount = 0;
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                Interlocked.Increment(ref inferenceCount);
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f }));
            }, maximumConcurrency: 2);
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("ok-a", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("fail", new RectangleRoiGeometry(new RectangleF(5, 0, 5, 5)), executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("ok-b", new RectangleRoiGeometry(new RectangleF(10, 0, 5, 5)), executionMode: RoiExecutionMode.CropAndInfer)
            });
            var runner = new VisualRoiRunner(fixture.Pipeline);

            RoiInferenceBatchResult result = await runner.RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                if (roi.Id == "fail") throw new InvalidOperationException("synthetic preparation failure");
                VisualSize source = new VisualSize(20, 20);
                VisualSize model = new VisualSize(2, 2);
                return Task.FromResult<PreparedVisualInput>(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, model, geometry.Bounds)));
            }, new RoiExecutionOptions(failureMode: RoiFailureMode.ReturnPartialResults));

            Assert.AreEqual(2, result.Succeeded.Count);
            Assert.AreEqual(1, result.Failed.Count);
            Assert.AreEqual("fail", result.Failed[0].Roi.Id);
            Assert.AreEqual(2, inferenceCount);
        }

        [TestMethod]
        public async Task RoiRunnerRejectsLocalOnlyPreparedCoordinatesBeforeInference()
        {
            int inferenceCount = 0;
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
            {
                Interlocked.Increment(ref inferenceCount);
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f }));
            });
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[] { new VisualRoi("roi", new RectangleRoiGeometry(new RectangleF(10, 10, 20, 20)), executionMode: RoiExecutionMode.CropAndInfer) });
            var runner = new VisualRoiRunner(fixture.Pipeline);

            await Assert.ThrowsExactlyAsync<VisualException>(() => runner.RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                var local = new VisualSize(20, 20);
                var model = new VisualSize(2, 2);
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), local, model, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(local, model)));
            }));
            Assert.AreEqual(0, inferenceCount);
        }

        [TestMethod]
        public async Task RoiSlidingWindowsStayInsideOffsetRegionAndRetainProvenance()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ =>
                InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 0f, 0f, 10f, 10f, .9f, .1f })), maximumConcurrency: 2);
            var roi = new VisualRoi("offset-zone", new RectangleRoiGeometry(new RectangleF(50, 40, 100, 80)), executionMode: RoiExecutionMode.SlidingWindow);
            var options = new SlidingWindowDetectionOptions(new VisualSize(60, 50), overlap: .5f, globalIouThreshold: .9f, coordinateMode: SlidingWindowCoordinateMode.TileLocal, minimumRoiCoverage: 1f);
            var runner = new SlidingWindowDetectionRunner(fixture.Pipeline);

            SlidingWindowDetectionResult result = await runner.RunRoiAsync(
                new VisualSize(200, 160), roi, roi.Geometry, options,
                (window, token) =>
                {
                    VisualSize tile = new VisualSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                    VisualSize model = new VisualSize(100, 100);
                    return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, model.Height, model.Width), new float[model.Width * model.Height * 3]), tile, model, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(tile, model)));
                });

            Assert.IsTrue(result.Windows.All(window => window.Bounds.X >= 50 && window.Bounds.Y >= 40));
            Assert.IsTrue(result.Windows.All(window => window.Bounds.Right <= 150 && window.Bounds.Bottom <= 120));
            Assert.IsTrue(result.Windows.All(window => window.RoiId == "offset-zone"));
            Assert.AreEqual(result.Detections.Detections.Count, result.DetectionsWithProvenance.Count);
            Assert.IsTrue(result.DetectionsWithProvenance.All(value => value.RoiId == "offset-zone"));
        }

        [TestMethod]
        public async Task MultiRoiSlidingWindowAppliesOneDeterministicGlobalNms()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ =>
                InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 0f, 0f, 20f, 20f, .9f, .1f })), maximumConcurrency: 2);
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("b-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), executionMode: RoiExecutionMode.SlidingWindow),
                new VisualRoi("a-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), executionMode: RoiExecutionMode.SlidingWindow)
            });
            var options = new SlidingWindowDetectionOptions(new VisualSize(100, 100), globalIouThreshold: .5f, maximumWindows: 2, coordinateMode: SlidingWindowCoordinateMode.TileLocal);
            var runner = new SlidingWindowDetectionRunner(fixture.Pipeline);

            SlidingWindowDetectionResult result = await runner.RunRoisAsync(snapshot, options, (roi, geometry, window, token) =>
            {
                var size = new VisualSize(100, 100);
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size)));
            });

            Assert.AreEqual(2, result.WindowCount);
            Assert.AreEqual(1, result.Detections.Detections.Count);
            Assert.AreEqual("a-zone", result.DetectionsWithProvenance[0].RoiId);
        }

        [TestMethod]
        public async Task CropDetectionMergerUsesRoiPriorityAndAggregatesProvenance()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), inputs =>
            {
                float marker = ((float[])inputs.GetRequired("images").Buffer)[0];
                float score = marker > 0 ? .9f : .6f;
                return InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 0f, 0f, 20f, 20f, score, .1f }));
            }, maximumConcurrency: 2);
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("high", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), priority: 10, executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("low", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), priority: 1, executionMode: RoiExecutionMode.CropAndInfer)
            });
            var runner = new VisualRoiRunner(fixture.Pipeline);
            RoiInferenceBatchResult batch = await runner.RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                var size = new VisualSize(100, 100);
                var values = new float[30000];
                values[0] = roi.Id == "low" ? 1f : 0f;
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), values), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(size, size, geometry.Bounds)));
            }, new RoiExecutionOptions(mergeMode: RoiResultMergeMode.RoiPriority));

            RoiMergedDetectionResult merged = new RoiDetectionResultMerger().Merge(batch, iouThreshold: .5f);

            Assert.AreEqual(1, merged.Items.Count);
            Assert.AreEqual("high", merged.Items[0].RoiId);
            Assert.AreEqual(.6f, merged.Items[0].Detection.Label.Score, .001f);
            CollectionAssert.AreEqual(new[] { "high", "low" }, merged.Items[0].ContributingRoiIds.ToArray());
            Assert.AreEqual(snapshot.Version, merged.SnapshotVersion);
        }

        [TestMethod]
        public async Task CropDetectionMergerPreservesCanonicalSourceCoordinatesFromDecoder()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ =>
                InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 0f, 0f, 20f, 20f, .9f, .1f })));
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("offset", new RectangleRoiGeometry(new RectangleF(40, 30, 40, 40)), executionMode: RoiExecutionMode.CropAndInfer)
            });
            RoiInferenceBatchResult batch = await new VisualRoiRunner(fixture.Pipeline).RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                var source = new VisualSize(100, 100);
                var model = new VisualSize(100, 100);
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), source, model, 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, model, geometry.Bounds)));
            });

            RoiMergedDetectionResult merged = new RoiDetectionResultMerger().Merge(batch);

            Assert.AreEqual(1, merged.Items.Count);
            RectangleF projected = merged.Items[0].Detection.Box;
            Assert.AreEqual(40f, projected.X, .001f);
            Assert.AreEqual(30f, projected.Y, .001f);
            Assert.AreEqual(8f, projected.Width, .001f);
            Assert.AreEqual(8f, projected.Height, .001f);
        }

        [TestMethod]
        public void OrientedDetectionFilterUsesExactCenterAndRetainsRoiProvenance()
        {
            OrientedQuadrilateral quadrilateral = OrientedQuadrilateral.Canonicalize(
                new[] { new PointF(20, 20), new PointF(60, 20), new PointF(60, 60), new PointF(20, 60) },
                OrientedVertexOrder.CounterClockwise);
            var detection = new JYPPX.DeploySharp.Visual.OrientedDetection(0, 1, "box", .9f, quadrilateral, angleRadiansCounterClockwise: 0, exactRotatedRectangle: true);
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(40, 40, 20, 20)), hitTestMode: RoiHitTestMode.CenterPoint, taskFilter: new[] { VisualTaskId.OrientedObjectDetection }),
                new VisualRoi("exclude", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), inclusionMode: RoiInclusionMode.Exclude, taskFilter: new[] { VisualTaskId.OrientedObjectDetection })
            });

            RoiOrientedDetectionResult result = VisualRoiOrientedDetectionFilter.Filter(new JYPPX.DeploySharp.Visual.OrientedDetectionResult(new[] { detection }, new VisualSize(100, 100), "test", new JYPPX.DeploySharp.Models.ModelId("obb")), snapshot);

            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual("zone", result.Detections[0].PrimaryRoiId);

            var partial = new VisualRoi("partial", new RectangleRoiGeometry(new RectangleF(20, 20, 10, 40)), hitTestMode: RoiHitTestMode.IntersectionOverResult, hitThreshold: .24f, taskFilter: new[] { VisualTaskId.OrientedObjectDetection });
            RoiOrientedDetectionResult partialResult = VisualRoiOrientedDetectionFilter.Filter(new JYPPX.DeploySharp.Visual.OrientedDetectionResult(new[] { detection }, new VisualSize(100, 100), "test", new JYPPX.DeploySharp.Models.ModelId("obb")), new VisualRoiSnapshot(new VisualSize(100, 100), new[] { partial }));
            Assert.AreEqual(1, partialResult.Detections.Count);
        }

        [TestMethod]
        public void OrientedDetectionMergerUsesRotatedIouAndTracksContributingRois()
        {
            OrientedQuadrilateral firstBox = OrientedQuadrilateral.Canonicalize(
                new[] { new PointF(10, 10), new PointF(30, 10), new PointF(30, 20), new PointF(10, 20) },
                OrientedVertexOrder.CounterClockwise);
            OrientedQuadrilateral secondBox = OrientedQuadrilateral.Canonicalize(
                new[] { new PointF(11, 10), new PointF(31, 10), new PointF(31, 20), new PointF(11, 20) },
                OrientedVertexOrder.CounterClockwise);
            var high = new JYPPX.DeploySharp.Visual.OrientedDetection(0, 1, "plane", .9f, firstBox);
            var low = new JYPPX.DeploySharp.Visual.OrientedDetection(1, 1, "plane", .8f, secondBox);
            var merger = new RoiOrientedDetectionResultMerger();

            IReadOnlyList<RoiProjectedOrientedDetection> merged = merger.Merge(new[]
            {
                new RoiProjectedOrientedDetection("a", 1, high),
                new RoiProjectedOrientedDetection("b", 2, low)
            }, RoiResultMergeMode.ClassAwareNms, .5f);

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("a", merged[0].RoiId);
            CollectionAssert.AreEqual(new[] { "a", "b" }, merged[0].ContributingRoiIds.ToArray());

            IReadOnlyList<RoiProjectedOrientedDetection> priority = merger.Merge(new[]
            {
                new RoiProjectedOrientedDetection("a", 1, high),
                new RoiProjectedOrientedDetection("b", 2, low)
            }, RoiResultMergeMode.RoiPriority, .5f);
            Assert.AreEqual("b", priority[0].RoiId);
        }

        [TestMethod]
        public void PoseFilterUsesDeclaredBoundingBoxAndClassFilter()
        {
            var topology = new PoseTopology(new[] { new PoseKeypointDefinition(0, "root") }, Array.Empty<PoseSkeletonEdge>());
            var instance = new PoseInstance(0, .9f, new[] { new PoseKeypoint(0, new PointF(5, 5), .9f, PoseKeypointVisibility.Visible, true) }, new RectangleF(20, 20, 20, 20), classIndex: 3);
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(20, 20, 20, 20)), hitTestMode: RoiHitTestMode.CenterPoint, taskFilter: new[] { VisualTaskId.PoseEstimation }, classFilter: new[] { 3 })
            });

            RoiPoseResult result = VisualRoiPoseFilter.Filter(new PoseEstimationResult(topology, new[] { instance }, new VisualSize(100, 100), "test", new JYPPX.DeploySharp.Models.ModelId("pose")), snapshot);

            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual("zone", result.Instances[0].PrimaryRoiId);

            var coverageRoi = new VisualRoi("keypoints", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), hitTestMode: RoiHitTestMode.KeypointCoverage, hitThreshold: .5f, taskFilter: new[] { VisualTaskId.PoseEstimation });
            RoiPoseResult coverage = VisualRoiPoseFilter.Filter(new PoseEstimationResult(topology, new[] { instance }, new VisualSize(100, 100), "test", new JYPPX.DeploySharp.Models.ModelId("pose")), new VisualRoiSnapshot(new VisualSize(100, 100), new[] { coverageRoi }));
            Assert.AreEqual(1, coverage.Instances.Count);
        }

        [TestMethod]
        public void PoseFilterSupportsAllAnyAndVisibleKeypointHitModes()
        {
            var topology = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "a"),
                new PoseKeypointDefinition(1, "b"),
                new PoseKeypointDefinition(2, "c")
            });
            var instance = new PoseInstance(0, .9f, new[]
            {
                new PoseKeypoint(0, new PointF(2, 2), .9f, PoseKeypointVisibility.Visible, true),
                new PoseKeypoint(1, new PointF(8, 8), .9f, PoseKeypointVisibility.Visible, true),
                new PoseKeypoint(2, new PointF(50, 50), .9f, PoseKeypointVisibility.NotVisible, true)
            }, new RectangleF(2, 2, 48, 48));
            var result = new PoseEstimationResult(topology, new[] { instance }, new VisualSize(100, 100), "pose", new JYPPX.DeploySharp.Models.ModelId("pose"));

            VisualRoi all = new VisualRoi("all", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), hitTestMode: RoiHitTestMode.AllKeypoints, taskFilter: new[] { VisualTaskId.PoseEstimation });
            VisualRoi any = new VisualRoi("any", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), hitTestMode: RoiHitTestMode.AnyKeypoint, taskFilter: new[] { VisualTaskId.PoseEstimation });
            VisualRoi visible = new VisualRoi("visible", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), hitTestMode: RoiHitTestMode.VisibleKeypointRatio, hitThreshold: 1, taskFilter: new[] { VisualTaskId.PoseEstimation });

            Assert.AreEqual(0, VisualRoiPoseFilter.Filter(result, new VisualRoiSnapshot(new VisualSize(100, 100), new[] { all })).Instances.Count);
            Assert.AreEqual(1, VisualRoiPoseFilter.Filter(result, new VisualRoiSnapshot(new VisualSize(100, 100), new[] { any })).Instances.Count);
            Assert.AreEqual(1, VisualRoiPoseFilter.Filter(result, new VisualRoiSnapshot(new VisualSize(100, 100), new[] { visible })).Instances.Count);
        }

        [TestMethod]
        public void PoseMergerUsesOksAndTracksContributingRois()
        {
            var topology = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "root", oksSigma: .05f),
                new PoseKeypointDefinition(1, "tip", oksSigma: .05f)
            });
            var first = new PoseInstance(0, .9f, new[]
            {
                new PoseKeypoint(0, new PointF(2, 2), .9f, PoseKeypointVisibility.Visible, true),
                new PoseKeypoint(1, new PointF(8, 8), .9f, PoseKeypointVisibility.Visible, true)
            }, new RectangleF(0, 0, 10, 10), classIndex: 0);
            var second = new PoseInstance(1, .8f, new[]
            {
                new PoseKeypoint(0, new PointF(2, 2), .9f, PoseKeypointVisibility.Visible, true),
                new PoseKeypoint(1, new PointF(8, 8), .9f, PoseKeypointVisibility.Visible, true)
            }, new RectangleF(0, 0, 10, 10), classIndex: 0);

            IReadOnlyList<RoiProjectedPoseInstance> merged = new RoiPoseResultMerger().Merge(new[]
            {
                new RoiProjectedPoseInstance("left", 1, first),
                new RoiProjectedPoseInstance("right", 2, second)
            }, topology, new PoseOksOptions(.8f));

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("left", merged[0].RoiId);
            CollectionAssert.AreEqual(new[] { "left", "right" }, merged[0].ContributingRoiIds.ToArray());
        }

        [TestMethod]
        public void InstanceSegmentationFilterUsesForegroundPixelsAndExcludeRoi()
        {
            var maskPixels = new byte[100];
            for (int y = 2; y < 8; y++) for (int x = 2; x < 8; x++) maskPixels[(y * 10) + x] = 1;
            var instance = new InstanceSegmentationInstance(0, 1, "object", .9f, new RectangleF(2, 2, 6, 6), new InstanceBinaryMask(10, 10, maskPixels));
            var segmentation = new InstanceSegmentationResult(new[] { instance }, new VisualSize(10, 10), "test", new JYPPX.DeploySharp.Models.ModelId("segment"));
            var snapshot = new VisualRoiSnapshot(new VisualSize(10, 10), new[]
            {
                new VisualRoi("include", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 10)), hitTestMode: RoiHitTestMode.IntersectionOverResult, hitThreshold: .4f, taskFilter: new[] { VisualTaskId.InstanceSegmentation }),
                new VisualRoi("exclude", new RectangleRoiGeometry(new RectangleF(2, 2, 1, 6)), inclusionMode: RoiInclusionMode.Exclude, hitTestMode: RoiHitTestMode.AnyIntersection, taskFilter: new[] { VisualTaskId.InstanceSegmentation })
            });

            RoiInstanceSegmentationResult result = VisualRoiInstanceSegmentationFilter.Filter(segmentation, snapshot);

            Assert.AreEqual(0, result.Instances.Count);
        }

        [TestMethod]
        public void InstanceSegmentationFilterSupportsMaskIntersectionRatioAndRequiresInstanceTask()
        {
            var pixels = new byte[16];
            for (int index = 0; index < pixels.Length; index++) pixels[index] = 1;
            var instance = new InstanceSegmentationInstance(0, 1, "object", .9f, new RectangleF(0, 0, 4, 4), new InstanceBinaryMask(4, 4, pixels));
            var segmentation = new InstanceSegmentationResult(new[] { instance }, new VisualSize(4, 4), "test", new JYPPX.DeploySharp.Models.ModelId("segment"));
            var roi = new VisualRoi("half", new RectangleRoiGeometry(new RectangleF(0, 0, 2, 4)), hitTestMode: RoiHitTestMode.MaskIntersectionRatio, hitThreshold: .5f, taskFilter: new[] { VisualTaskId.InstanceSegmentation });

            Assert.AreEqual(1, VisualRoiInstanceSegmentationFilter.Filter(segmentation, new VisualRoiSnapshot(new VisualSize(4, 4), new[] { roi })).Instances.Count);
            var invalid = new VisualRoi("invalid", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 4)), hitTestMode: RoiHitTestMode.MaskIntersectionRatio, taskFilter: new[] { VisualTaskId.ObjectDetection });
            Assert.ThrowsExactly<ArgumentException>(() => new VisualRoiSnapshot(new VisualSize(4, 4), new[] { invalid }));
        }

        [TestMethod]
        public void InstanceSegmentationMergerUsesPixelIouAndPreservesProvenance()
        {
            var firstPixels = new byte[16];
            var secondPixels = new byte[16];
            for (int y = 1; y < 3; y++)
            {
                for (int x = 1; x < 3; x++) firstPixels[(y * 4) + x] = 1;
                for (int x = 2; x < 4; x++) secondPixels[(y * 4) + x] = 1;
            }

            var first = new InstanceSegmentationInstance(0, 1, "item", .9f, new RectangleF(1, 1, 2, 2), new InstanceBinaryMask(4, 4, firstPixels));
            var second = new InstanceSegmentationInstance(1, 1, "item", .8f, new RectangleF(2, 1, 2, 2), new InstanceBinaryMask(4, 4, secondPixels));
            var firstResult = new InstanceSegmentationResult(new[] { first }, new VisualSize(4, 4), "segment", new JYPPX.DeploySharp.Models.ModelId("segment"));
            var secondResult = new InstanceSegmentationResult(new[] { second }, new VisualSize(4, 4), "segment", new JYPPX.DeploySharp.Models.ModelId("segment"));

            var candidates = new[]
            {
                new RoiProjectedResult<InstanceSegmentationResult>("left", 1, firstResult),
                new RoiProjectedResult<InstanceSegmentationResult>("right", 2, secondResult)
            };
            RoiMergedInstanceSegmentationResult merged = new RoiInstanceSegmentationResultMerger().Merge(candidates, RoiResultMergeMode.ClassAwareNms, .3f);

            Assert.AreEqual(1, merged.Items.Count);
            Assert.AreEqual(.9f, merged.Items[0].Instance.Score, .001f);
            CollectionAssert.AreEqual(new[] { "left", "right" }, merged.Items[0].RoiIds.ToArray());
            Assert.AreEqual(1, merged.Result.Instances.Count);
            Assert.AreEqual(InstanceMaskOverlapMode.Independent, merged.Result.OverlapMode);
        }

        [TestMethod]
        public void InstanceSegmentationMergerBuildsScorePriorityOwnershipMap()
        {
            byte[] firstPixels = Enumerable.Repeat((byte)0, 9).ToArray();
            byte[] secondPixels = Enumerable.Repeat((byte)0, 9).ToArray();
            firstPixels[4] = 1;
            secondPixels[4] = 1;
            var first = new InstanceSegmentationInstance(0, 1, "first", .9f, new RectangleF(1, 1, 1, 1), new InstanceBinaryMask(3, 3, firstPixels));
            var second = new InstanceSegmentationInstance(1, 2, "second", .8f, new RectangleF(1, 1, 1, 1), new InstanceBinaryMask(3, 3, secondPixels));
            var resultA = new InstanceSegmentationResult(new[] { first }, new VisualSize(3, 3), "segment", new JYPPX.DeploySharp.Models.ModelId("segment"));
            var resultB = new InstanceSegmentationResult(new[] { second }, new VisualSize(3, 3), "segment", new JYPPX.DeploySharp.Models.ModelId("segment"));

            RoiMergedInstanceSegmentationResult merged = new RoiInstanceSegmentationResultMerger().Merge(new[]
            {
                new RoiProjectedResult<InstanceSegmentationResult>("a", 0, resultA),
                new RoiProjectedResult<InstanceSegmentationResult>("b", 0, resultB)
            }, RoiResultMergeMode.KeepAll, overlapMode: InstanceMaskOverlapMode.ScorePriorityOwnership);

            Assert.IsNotNull(merged.Result.OwnershipMap);
            Assert.AreEqual(0, merged.Result.OwnershipMap!.GetOwnerIndex(1, 1));
            Assert.AreEqual(2, merged.Result.Instances.Count);
        }

        [TestMethod]
        public void SemanticSegmentationFilterSamplesPixelsAndHonorsClassAwareExclude()
        {
            ushort[] labels =
            {
                1, 1, 2, 2,
                1, 0, 2, 2,
                3, 3, 2, 0
            };
            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "one", new SegmentationColor(1, 1, 1)),
                new SemanticSegmentationClass(2, "two", new SegmentationColor(2, 2, 2)),
                new SemanticSegmentationClass(3, "three", new SegmentationColor(3, 3, 3))
            };
            var segmentation = new SemanticSegmentationResult(
                new SemanticSegmentationMask(4, 3, labels),
                classes,
                new[]
                {
                    new SegmentationClassStatistics(0, 2, 2d / 12d),
                    new SegmentationClassStatistics(1, 3, 3d / 12d),
                    new SegmentationClassStatistics(2, 5, 5d / 12d),
                    new SegmentationClassStatistics(3, 2, 2d / 12d)
                });
            var snapshot = new VisualRoiSnapshot(new VisualSize(4, 3), new[]
            {
                new VisualRoi("all", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 3)), priority: 5, taskFilter: new[] { VisualTaskId.SemanticSegmentation }),
                new VisualRoi("exclude-two", new RectangleRoiGeometry(new RectangleF(2, 0, 1, 3)), inclusionMode: RoiInclusionMode.Exclude, taskFilter: new[] { VisualTaskId.SemanticSegmentation }, classFilter: new[] { 2 }),
                new VisualRoi("only-three", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 3)), priority: 1, taskFilter: new[] { VisualTaskId.SemanticSegmentation }, classFilter: new[] { 3 })
            });

            RoiSemanticSegmentationResult result = VisualRoiSemanticSegmentationFilter.Filter(segmentation, snapshot);

            Assert.AreEqual(2, result.Regions.Count);
            RoiSemanticSegmentationRegion all = result.Regions.Single(value => value.RoiId == "all");
            Assert.AreEqual(9L, all.RoiPixelCount);
            Assert.AreEqual(9L, all.ClassifiedPixelCount);
            Assert.AreEqual(1, all.DominantClassIndex);
            Assert.AreEqual(2L, all.Statistics.Single(value => value.ClassIndex == 2).PixelCount);
            RoiSemanticSegmentationRegion onlyThree = result.Regions.Single(value => value.RoiId == "only-three");
            Assert.AreEqual(9L, onlyThree.RoiPixelCount);
            Assert.AreEqual(2L, onlyThree.ClassifiedPixelCount);
            Assert.AreEqual(3, onlyThree.DominantClassIndex);
            Assert.AreEqual(1d, onlyThree.DominantFraction, .0001d);
        }

        [TestMethod]
        public void TextDetectionRoiFilterPreservesReadingOrderAndProvenance()
        {
            TextPolygon firstPolygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(1, 1), new PointF(5, 1), new PointF(5, 3), new PointF(1, 3)
            }, OrientedVertexOrder.CounterClockwise);
            TextPolygon secondPolygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(11, 1), new PointF(15, 1), new PointF(15, 3), new PointF(11, 3)
            }, OrientedVertexOrder.CounterClockwise);
            var detections = new TextDetectionResult(
                new[]
                {
                    new JYPPX.DeploySharp.Visual.TextRegion(0, .95f, firstPolygon, externalId: "first"),
                    new JYPPX.DeploySharp.Visual.TextRegion(1, .88f, secondPolygon, externalId: "second")
                },
                new VisualSize(20, 10),
                "ocr-det",
                new JYPPX.DeploySharp.Models.ModelId("ocr-det"));
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 10), new[]
            {
                new VisualRoi("left", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), taskFilter: new[] { VisualTaskId.TextDetection }),
                new VisualRoi("exclude", new RectangleRoiGeometry(new RectangleF(12, 0, 2, 10)), inclusionMode: RoiInclusionMode.Exclude, taskFilter: new[] { VisualTaskId.TextDetection })
            });

            RoiTextDetectionResult result = VisualRoiTextDetectionFilter.Filter(detections, snapshot);

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("first", result.Items[0].Region.ExternalId);
            Assert.AreEqual("left", result.Items[0].PrimaryRoiId);
            Assert.AreEqual(1, result.Regions.Regions.Count);
            Assert.AreEqual("roi-filter", result.Regions.ProfileId);
        }

        [TestMethod]
        public void CompleteOcrRoiFilterKeepsRecognitionAndTimingWithoutRerun()
        {
            TextPolygon polygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(1, 1), new PointF(5, 1), new PointF(5, 3), new PointF(1, 3)
            }, OrientedVertexOrder.CounterClockwise);
            var region = new JYPPX.DeploySharp.Visual.TextRegion(0, .95f, polygon, externalId: "accepted");
            var recognition = new RecognizedText(0, "OK", .9f, Array.Empty<OcrToken>(), "latin", "1", new string('0', 64));
            var timing = new OcrStageTiming(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(4));
            var ocr = new JYPPX.DeploySharp.Visual.OcrResult(
                new[] { new OcrRegionResult(region, recognition) },
                new VisualSize(20, 10),
                "det", new JYPPX.DeploySharp.Models.ModelId("det"),
                "rec", new JYPPX.DeploySharp.Models.ModelId("rec"), timing);
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 10), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), taskFilter: new[] { VisualTaskId.OpticalCharacterRecognition })
            });

            RoiOcrResult filtered = VisualRoiOcrFilter.Filter(ocr, snapshot);

            Assert.AreEqual(1, filtered.Items.Count);
            Assert.AreEqual("OK", filtered.Items[0].Region.Recognition.Text);
            Assert.AreEqual("zone", filtered.Items[0].PrimaryRoiId);
            Assert.AreEqual(timing.Total, filtered.Result.Timing.Total);
            Assert.AreEqual("det", filtered.Result.DetectionProfileId);
        }

        [TestMethod]
        public void ModelSpaceOcrProjectorRestoresRegionsAndRecognitionToSource()
        {
            var source = new VisualSize(10, 8);
            var model = new VisualSize(4, 4);
            var crop = new RectangleF(2, 2, 4, 4);
            var localQuad = new TextQuadrilateral(new PointF(.5f, .5f), new PointF(3.5f, .5f), new PointF(3.5f, 2.5f), new PointF(.5f, 2.5f), TextCornerOrder.TopLeftClockwise);
            var region = new JYPPX.DeploySharp.Visual.TextRegion(3, .95f, localQuad.Polygon, localQuad, TextOrientation.Degrees0, externalId: "text");
            var recognition = new RecognizedText(3, "OK", .9f, Array.Empty<OcrToken>(), "latin", "1", new string('0', 64));
            OcrRecognitionWidthInfo width = new TextCropProfile("roi/width", 4, OcrRecognitionWidthMode.Dynamic, 16, 16).DescribeWidth(localQuad, region.Orientation);
            var timing = new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            var local = new JYPPX.DeploySharp.Visual.OcrResult(
                new[] { new OcrRegionResult(region, recognition, width) },
                model,
                "det", new JYPPX.DeploySharp.Models.ModelId("det"),
                "rec", new JYPPX.DeploySharp.Models.ModelId("rec"),
                timing);
            var projection = new RoiProjection("crop", source, model, ImageTransform.Crop(source, model, crop), crop);

            JYPPX.DeploySharp.Visual.OcrResult projected = new ModelSpaceOcrRoiProjector().Project(local, projection);

            Assert.AreEqual(source, projected.SourceSize);
            Assert.AreEqual("OK", projected.Regions[0].Recognition.Text);
            Assert.AreEqual(3, projected.Regions[0].Region.SourceIndex);
            Assert.AreEqual(new PointF(2.5f, 2.5f), projected.Regions[0].Region.Polygon.Vertices[0]);
            Assert.AreEqual(new PointF(5.5f, 4.5f), projected.Regions[0].Region.CropQuadrilateral!.BottomRight);
            Assert.AreEqual(width, projected.Regions[0].RecognitionWidth);
        }

        [TestMethod]
        public void ModelSpaceOcrProjectorRejectsGlobalOrientationProvenance()
        {
            var model = new VisualSize(4, 4);
            var timing = new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            var orientation = new OcrOrientationResult(
                TextOrientation.Degrees0,
                0,
                .9f,
                new[] { .9f, .1f },
                false,
                "orientation",
                new JYPPX.DeploySharp.Models.ModelId("orientation"),
                new JYPPX.DeploySharp.BackendId("test"),
                model,
                model,
                TimeSpan.Zero);
            var local = new JYPPX.DeploySharp.Visual.OcrResult(
                Array.Empty<OcrRegionResult>(), model,
                "det", new JYPPX.DeploySharp.Models.ModelId("det"),
                "rec", new JYPPX.DeploySharp.Models.ModelId("rec"), timing, orientation);
            var projection = new RoiProjection("crop", model, model, ImageTransform.Resize(model, model));

            VisualException error = Assert.ThrowsExactly<VisualException>(() => new ModelSpaceOcrRoiProjector().Project(local, projection));

            Assert.AreEqual(VisualErrorCodes.CapabilityUnavailable, error.ErrorCode);
        }

        [TestMethod]
        public void OcrMergerDeduplicatesSameTextByPolygonIouAndReordersReadingOrder()
        {
            TextPolygon leftPolygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(10, 10), new PointF(30, 10), new PointF(30, 20), new PointF(10, 20)
            }, OrientedVertexOrder.CounterClockwise);
            TextPolygon rightPolygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(11, 10), new PointF(31, 10), new PointF(31, 20), new PointF(11, 20)
            }, OrientedVertexOrder.CounterClockwise);
            string hash = new string('a', 64);
            var widthQuad = new TextQuadrilateral(new PointF(0, 0), new PointF(20, 0), new PointF(20, 10), new PointF(0, 10), TextCornerOrder.TopLeftClockwise);
            OcrRecognitionWidthInfo width = new TextCropProfile("roi/merge-width", 8, OcrRecognitionWidthMode.Fixed, 8, 8).DescribeWidth(widthQuad, TextOrientation.Degrees0);
            OcrRegionResult high = new OcrRegionResult(
                new JYPPX.DeploySharp.Visual.TextRegion(0, .95f, leftPolygon),
                new RecognizedText(0, "  OK  ", .95f, Array.Empty<OcrToken>(), "latin", "1", hash), width);
            OcrRegionResult low = new OcrRegionResult(
                new JYPPX.DeploySharp.Visual.TextRegion(1, .8f, rightPolygon),
                new RecognizedText(1, "OK", .8f, Array.Empty<OcrToken>(), "latin", "1", hash));
            var timing = new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            var first = new JYPPX.DeploySharp.Visual.OcrResult(new[] { high }, new VisualSize(100, 100), "det", new JYPPX.DeploySharp.Models.ModelId("det"), "rec", new JYPPX.DeploySharp.Models.ModelId("rec"), timing);
            var second = new JYPPX.DeploySharp.Visual.OcrResult(new[] { low }, new VisualSize(100, 100), "det", new JYPPX.DeploySharp.Models.ModelId("det"), "rec", new JYPPX.DeploySharp.Models.ModelId("rec"), timing);

            RoiMergedOcrResult merged = new RoiOcrResultMerger().Merge(new[]
            {
                new RoiProjectedResult<JYPPX.DeploySharp.Visual.OcrResult>("left", 1, first),
                new RoiProjectedResult<JYPPX.DeploySharp.Visual.OcrResult>("right", 2, second)
            }, RoiResultMergeMode.HighestConfidence, .5f);

            Assert.AreEqual(1, merged.Items.Count);
            Assert.AreEqual("  OK  ", merged.Items[0].Region.Recognition.Text);
            Assert.AreEqual(width, merged.Items[0].Region.RecognitionWidth);
            CollectionAssert.AreEqual(new[] { "left", "right" }, merged.Items[0].RoiIds.ToArray());
            Assert.AreEqual(new PointF(10, 10), merged.Items[0].Region.Region.Polygon.Vertices[0]);
        }

        [TestMethod]
        public void TextDetectionRoiFilterUsesPolygonIntersectionArea()
        {
            TextPolygon polygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(0, 0), new PointF(2, 0), new PointF(2, 2), new PointF(0, 2)
            }, OrientedVertexOrder.CounterClockwise);
            var detections = new TextDetectionResult(
                new[] { new JYPPX.DeploySharp.Visual.TextRegion(0, .9f, polygon) },
                new VisualSize(4, 4), "det", new JYPPX.DeploySharp.Models.ModelId("det"));
            var roi = new VisualRoi("triangle", new PolygonRoiGeometry(new[]
            {
                new PointF(0, 0), new PointF(4, 0), new PointF(0, 1)
            }), hitTestMode: RoiHitTestMode.IntersectionOverResult, hitThreshold: .3f, taskFilter: new[] { VisualTaskId.TextDetection });

            RoiTextDetectionResult filtered = VisualRoiTextDetectionFilter.Filter(detections, new VisualRoiSnapshot(new VisualSize(4, 4), new[] { roi }));

            Assert.AreEqual(1, filtered.Items.Count);
            VisualRoi strictRoi = new VisualRoi("triangle", roi.Geometry, hitTestMode: RoiHitTestMode.IntersectionOverResult, hitThreshold: .4f, taskFilter: new[] { VisualTaskId.TextDetection });
            Assert.AreEqual(0, VisualRoiTextDetectionFilter.Filter(detections, new VisualRoiSnapshot(new VisualSize(4, 4), new[] { strictRoi })).Items.Count);
        }

        [TestMethod]
        public async Task CropClassificationMergerSupportsKeepAllHighestConfidenceAndRoiPriority()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                float marker = ((float[])inputs.GetRequired("images").Buffer)[0];
                float[] scores = marker > 0 ? new[] { .6f, .4f, 0f } : new[] { .9f, .1f, 0f };
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), scores));
            }, maximumConcurrency: 2);
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("priority", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), priority: 20, executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("confidence", new RectangleRoiGeometry(new RectangleF(10, 0, 10, 20)), priority: 1, executionMode: RoiExecutionMode.CropAndInfer)
            });
            var runner = new VisualRoiRunner(fixture.Pipeline);
            RoiInferenceBatchResult batch = await runner.RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                var source = new VisualSize(20, 20);
                var values = new float[12];
                values[0] = roi.Id == "priority" ? 1f : 0f;
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), values), source, new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), geometry.Bounds)));
            });

            RoiClassificationResultMerger merger = new RoiClassificationResultMerger();
            RoiMergedClassificationResult all = merger.Merge(batch, RoiResultMergeMode.KeepAll);
            RoiMergedClassificationResult highest = merger.Merge(batch, RoiResultMergeMode.HighestConfidence);
            RoiMergedClassificationResult priority = merger.Merge(batch, RoiResultMergeMode.RoiPriority);

            Assert.AreEqual(2, all.Items.Count);
            Assert.AreEqual("confidence", highest.Primary!.RoiId);
            Assert.AreEqual("priority", priority.Primary!.RoiId);
            Assert.AreEqual(snapshot.Version, priority.SnapshotVersion);
            Assert.ThrowsExactly<NotSupportedException>(() => merger.Merge(batch, RoiResultMergeMode.ClassAwareNms));
        }

        [TestMethod]
        public async Task CropRunnerCanDecodeProjectAndMergeStronglyTypedResults()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            var source = new VisualSize(8, 4);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification })
            });
            var runner = new VisualRoiRunner(fixture.Pipeline);
            VisualRoiCropMergedResult<ClassificationResult> merged = await runner.RunCropAndMergeAsync(
                snapshot,
                (roi, geometry, token) => Task.FromResult(new PreparedVisualInput(
                    "images",
                    new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]),
                    source,
                    new VisualSize(2, 2),
                    1,
                    VisualTensorLayout.Nchw,
                    ImageTransform.Crop(source, new VisualSize(2, 2), geometry.Bounds))),
                inference => inference.GetValue<ClassificationResult>(),
                new IdentityClassificationProjector(),
                new KeepAllClassificationMerger(),
                new RoiExecutionOptions(mergeMode: RoiResultMergeMode.KeepAll));

            Assert.AreEqual(1, merged.Batch.Succeeded.Count);
            Assert.AreEqual(1, merged.Candidates.Count);
            Assert.AreEqual(1, merged.Merged.Count);
            Assert.AreEqual(1, merged.Merged[0].TopPrediction!.Index);
        }

        [TestMethod]
        public void GenericTaskMergerAdaptersPreserveTaskContractsAndWeightedDetectionFusion()
        {
            var first = new DetectionResult(new[]
            {
                new Detection(new RectangleF(0, 0, 10, 10), new LabelScore(0, "item", .9f))
            });
            var second = new DetectionResult(new[]
            {
                new Detection(new RectangleF(1, 1, 10, 10), new LabelScore(0, "item", .5f))
            });
            IReadOnlyList<DetectionResult> fused = new DetectionRoiResultMergerAdapter(.5f).Merge(new[]
            {
                new RoiProjectedResult<DetectionResult>("left", 1, first, 0),
                new RoiProjectedResult<DetectionResult>("right", 2, second, 1)
            }, RoiResultMergeMode.WeightedBoxFusion);

            Assert.AreEqual(1, fused.Count);
            Assert.AreEqual(1, fused[0].Detections.Count);
            Assert.AreEqual(.7f, fused[0].Detections[0].Label.Score, .0001f);
            Assert.AreEqual(0.3571f, fused[0].Detections[0].Box.X, .001f);

            var low = new ClassificationResult(new[] { new LabelScore(0, "low", .2f) });
            var high = new ClassificationResult(new[] { new LabelScore(1, "high", .8f) });
            IReadOnlyList<ClassificationResult> selected = new ClassificationRoiResultMergerAdapter().Merge(new[]
            {
                new RoiProjectedResult<ClassificationResult>("a", 1, low),
                new RoiProjectedResult<ClassificationResult>("b", 1, high)
            }, RoiResultMergeMode.HighestConfidence);
            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(1, selected[0].TopPrediction!.Index);
        }

        [TestMethod]
        public void PoseMergerAdapterComparesTheFullTopologyContract()
        {
            var topology = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "root", oksSigma: .5f),
                new PoseKeypointDefinition(1, "tip", oksSigma: .5f)
            }, new[] { new PoseSkeletonEdge(0, 1) });
            var equivalent = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "root", oksSigma: .5f),
                new PoseKeypointDefinition(1, "tip", oksSigma: .5f)
            }, new[] { new PoseSkeletonEdge(0, 1) });
            var different = new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "root", oksSigma: .5f),
                new PoseKeypointDefinition(1, "other", oksSigma: .5f)
            }, new[] { new PoseSkeletonEdge(0, 1) });
            var source = new VisualSize(32, 32);
            var model = new JYPPX.DeploySharp.Models.ModelId("pose");
            var emptyEquivalent = new PoseEstimationResult(equivalent, Array.Empty<PoseInstance>(), source, "profile", model);
            IReadOnlyList<PoseEstimationResult> merged = new PoseRoiResultMergerAdapter(topology).Merge(new[]
            {
                new RoiProjectedResult<PoseEstimationResult>("zone", 1, emptyEquivalent)
            }, RoiResultMergeMode.KeepAll);
            Assert.AreEqual(1, merged.Count);
            Assert.AreSame(topology, merged[0].Topology);

            var invalid = new PoseEstimationResult(different, Array.Empty<PoseInstance>(), source, "profile", model);
            Assert.ThrowsExactly<VisualException>(() => new PoseRoiResultMergerAdapter(topology).Merge(new[]
            {
                new RoiProjectedResult<PoseEstimationResult>("zone", 1, invalid)
            }, RoiResultMergeMode.KeepAll));
        }

        [TestMethod]
        public void DenseTaskMergerAdaptersUseExplicitCompositionPolicies()
        {
            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "part", new SegmentationColor(255, 0, 0))
            };
            SemanticSegmentationResult semantic = CreateSemanticResult(new ushort[] { 0, 0, 1, 1 }, classes);
            IReadOnlyList<SemanticSegmentationResult> semanticMerged = new SemanticSegmentationRoiResultMergerAdapter(RoiSemanticLabelMergeMode.KeepLast).Merge(new[]
            {
                new RoiProjectedResult<SemanticSegmentationResult>("a", 1, semantic),
                new RoiProjectedResult<SemanticSegmentationResult>("b", 2, CreateSemanticResult(new ushort[] { 1, 1, 0, 0 }, classes))
            }, RoiResultMergeMode.KeepAll);
            CollectionAssert.AreEqual(new ushort[] { 1, 1, 0, 0 }, semanticMerged[0].Mask.ToArray());

            var source = new VisualSize(2, 2);
            var anomaly = new AnomalyDetectionResult(.5f, null,
                new AnomalyScoreMap(source, 2, 2, new[] { .1f, .8f, .2f, .3f }, AnomalyMapValueMode.Probabilities, AnomalyNormalizationMode.None),
                new AnomalyBinaryMask(2, 2, new byte[] { 0, 1, 0, 0 }), .5f, ImageTransform.Resize(source, source));
            IReadOnlyList<AnomalyDetectionResult> anomalyMerged = new AnomalyRoiResultMergerAdapter(RoiAnomalyMergeMode.MaxScore).Merge(new[]
            {
                new RoiProjectedResult<AnomalyDetectionResult>("zone", 1, anomaly)
            }, RoiResultMergeMode.KeepAll);
            Assert.AreEqual(.8f, anomalyMerged[0].NormalizedMap.GetValue(1, 0), .0001f);
        }

        [TestMethod]
        public async Task CropRunnerRejectsAmbiguousMultiRowInputAndCanDropReturnedCandidates()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            var source = new VisualSize(8, 4);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification })
            });
            var runner = new VisualRoiRunner(fixture.Pipeline);

            VisualRoiCropMergedResult<ClassificationResult> merged = await runner.RunCropAndMergeAsync(
                snapshot,
                (roi, geometry, token) => Task.FromResult(new PreparedVisualInput(
                    "images",
                    new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]),
                    source,
                    new VisualSize(2, 2),
                    1,
                    VisualTensorLayout.Nchw,
                    ImageTransform.Crop(source, new VisualSize(2, 2), geometry.Bounds))),
                inference => inference.GetValue<ClassificationResult>(),
                new IdentityClassificationProjector(),
                new KeepAllClassificationMerger(),
                new RoiExecutionOptions(preserveIndividualResults: false));
            Assert.AreEqual(0, merged.Candidates.Count);
            Assert.AreEqual(1, merged.Batch.Succeeded.Count);

            await Assert.ThrowsExactlyAsync<VisualException>(async () => await runner.RunCropAsync(snapshot, (roi, geometry, token) => Task.FromResult(new PreparedVisualInput(
                "images",
                new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]),
                source,
                new VisualSize(2, 2),
                2,
                VisualTensorLayout.Nchw,
                ImageTransform.Crop(source, new VisualSize(2, 2), geometry.Bounds)))));
        }

        [TestMethod]
        public async Task CropRunnerSkipsRoisForOtherVisualTasks()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            VisualSize source = new VisualSize(8, 8);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("classification", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification }),
                new VisualRoi("detection-only", new RectangleRoiGeometry(new RectangleF(4, 0, 4, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            int preparationCount = 0;

            RoiInferenceBatchResult result = await new VisualRoiRunner(fixture.Pipeline).RunCropAsync(snapshot, (roi, geometry, token) =>
            {
                preparationCount++;
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, new VisualSize(2, 2))));
            });

            Assert.AreEqual(1, preparationCount);
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("classification", result.Items[0].Roi.Id);
        }

        [TestMethod]
        public async Task SlidingWindowReturnsEmptyWithoutInvokingMergerWhenNoRoiMatchesTask()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            var snapshot = new VisualRoiSnapshot(new VisualSize(8, 8), new[]
            {
                new VisualRoi("detection-only", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 8)), executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var runner = new VisualRoiSlidingWindowRunner<ClassificationResult>(fixture.Pipeline);

            VisualRoiSlidingWindowMergedResult<ClassificationResult> result = await runner.RunRoisAndMergeAsync(
                snapshot,
                new SlidingWindowDetectionOptions(new VisualSize(8, 8), maximumWindows: 1),
                (roi, geometry, window, token) => Task.FromResult(VisualTestData.ClassificationInput()),
                inference => inference.GetValue<ClassificationResult>(),
                new IdentityRoiResultProjector<ClassificationResult>(),
                new ThrowingClassificationMerger());

            Assert.AreEqual(0, result.Windows.Results.Count);
            Assert.AreEqual(0, result.Merged.Count);
            Assert.AreEqual(0, result.Diagnostics.SelectedRoiCount);
        }

        [TestMethod]
        public async Task GenericSlidingWindowRunnerReusesWindowPlanAndPreservesRoiProvenance()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), inputs =>
            {
                return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f }));
            }, maximumConcurrency: 2);
            var source = new VisualSize(8, 4);
            var roi = new VisualRoi("class-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 4)), priority: 7, executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.ImageClassification });
            var options = new SlidingWindowDetectionOptions(new VisualSize(4, 4), overlap: .5f, maximumWindows: 8);
            var runner = new VisualRoiSlidingWindowRunner<ClassificationResult>(fixture.Pipeline);

            VisualRoiSlidingWindowResult<ClassificationResult> result = await runner.RunRoiAsync(source, roi, roi.Geometry, options, (window, token) =>
            {
                var values = new float[3];
                return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), window.Bounds)));
            }, (inference, window, prepared) => new RoiProjectedResult<ClassificationResult>(roi.Id, roi.Priority, inference.GetValue<ClassificationResult>(), window.Index));

            Assert.AreEqual(3, result.WindowCount);
            Assert.AreEqual(result.WindowCount, result.Results.Count);
            Assert.IsTrue(result.Results.All(value => value.RoiId == "class-zone" && value.Priority == 7));
            Assert.IsTrue(result.Results.All(value => value.WindowIndex.HasValue));
            Assert.AreEqual(RoiExecutionMode.SlidingWindow, result.Diagnostics.ExecutionMode);
            Assert.AreEqual(3, result.Diagnostics.PreparedInputCount);
            Assert.AreEqual(1, result.Diagnostics.InferenceCallCount);
            Assert.AreEqual(3, result.Diagnostics.SucceededResultCount);
            Assert.AreEqual(0, result.Diagnostics.FailedResultCount);
            Assert.IsTrue(result.Diagnostics.PreparedPixelCount > 0);
        }

        [TestMethod]
        public async Task RoiCropDiagnosticsExposePreparationAndInferenceCounters()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })), maximumConcurrency: 2);
            VisualSize source = new VisualSize(10, 10);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("left", new RectangleRoiGeometry(new RectangleF(0, 0, 3, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification }),
                new VisualRoi("right", new RectangleRoiGeometry(new RectangleF(4, 0, 2, 5)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification })
            });

            RoiInferenceBatchResult result = await new VisualRoiRunner(fixture.Pipeline).RunCropAsync(snapshot, (roi, geometry, token) =>
                Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(source, new VisualSize(2, 2)))));

            Assert.AreEqual(RoiExecutionMode.CropAndInfer, result.Diagnostics.ExecutionMode);
            Assert.AreEqual(2, result.Diagnostics.SelectedRoiCount);
            Assert.AreEqual(2, result.Diagnostics.PreparedInputCount);
            Assert.AreEqual(1, result.Diagnostics.InferenceCallCount);
            Assert.AreEqual(2, result.Diagnostics.SucceededResultCount);
            Assert.AreEqual(0, result.Diagnostics.FailedResultCount);
            Assert.AreEqual(22L, result.Diagnostics.PreparedPixelCount);
            Assert.IsFalse(result.Diagnostics.UsedTrueBatch);
            Assert.IsTrue(result.Diagnostics.Elapsed >= TimeSpan.Zero);
        }

        [TestMethod]
        public async Task DetectionSlidingWindowConvenienceEntryProjectsAndMerges()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(1, 6));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 6), _ =>
                InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 6), new[] { 10f, 10f, 30f, 30f, .9f, .1f })));
            var source = new VisualSize(100, 100);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("detect-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.ObjectDetection })
            });

            VisualRoiSlidingWindowMergedResult<DetectionResult> result = await new VisualRoiSlidingWindowRunner<DetectionResult>(fixture.Pipeline).RunDetectionRoisAndMergeAsync(
                snapshot,
                new SlidingWindowDetectionOptions(new VisualSize(100, 100), maximumWindows: 1),
                (roi, geometry, window, token) => Task.FromResult(VisualTestData.DetectionInput(source, ImageTransform.Resize(source, source))),
                inference => inference.GetValue<DetectionResult>(),
                mergeMode: RoiResultMergeMode.ClassAwareNms);

            Assert.AreEqual(1, result.Windows.Results.Count);
            Assert.AreEqual(1, result.Merged.Count);
            Assert.AreEqual(1, result.Merged[0].Detections.Count);
            Assert.AreEqual(10f, result.Merged[0].Detections[0].Box.X, .001f);
            Assert.AreEqual("detect-zone", result.Windows.Results[0].RoiId);
        }

        [TestMethod]
        public async Task OcrSlidingWindowConvenienceEntryRestoresTextGeometryAndDiagnostics()
        {
            var modelId = new JYPPX.DeploySharp.Models.ModelId("tests/roi-ocr");
            VisualModelDefinition<string, JYPPX.DeploySharp.Visual.OcrResult> definition = new VisualModelDefinitionBuilder<string, JYPPX.DeploySharp.Visual.OcrResult>(
                "tests/roi-ocr.v1", modelId, VisualTaskId.OpticalCharacterRecognition, "1.0", "fake")
                .WithInput("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw)
                .AddOutput("dummy", TensorElementType.Float32, new TensorShape(1, 1))
                .WithInputPreprocessor((_, _, _) => VisualTestData.ClassificationInput())
                .WithDecoder(context => CreateRoiOcrResult(context.Input.ModelSize))
                .Build();
            using PipelineFixture fixture = VisualTestData.Pipeline(definition.Profile, new TensorShape(1, 1), _ =>
                InferenceOutputs.Create("dummy", new Tensor<float>(new TensorShape(1, 1), new[] { 0f })));

            VisualSize source = new VisualSize(8, 8);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("text-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 8)), priority: 3,
                    executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.OpticalCharacterRecognition })
            });
            VisualRoiSlidingWindowMergedResult<JYPPX.DeploySharp.Visual.OcrResult> result =
                await new VisualRoiSlidingWindowRunner<JYPPX.DeploySharp.Visual.OcrResult>(fixture.Pipeline).RunOcrRoisAndMergeAsync(
                    snapshot,
                    new SlidingWindowDetectionOptions(new VisualSize(8, 8), overlap: 0, maximumWindows: 1),
                    (roi, geometry, window, token) => Task.FromResult(new PreparedVisualInput(
                        "images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1,
                        VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), window.Bounds))),
                    inference => inference.GetValue<JYPPX.DeploySharp.Visual.OcrResult>());

            Assert.AreEqual(1, result.Windows.Results.Count);
            Assert.AreEqual(1, result.Merged.Count);
            Assert.AreEqual(1, result.Merged[0].Regions.Count);
            Assert.AreEqual("ROI", result.Merged[0].Regions[0].Recognition.Text);
            Assert.AreEqual(new PointF(1, 1), result.Merged[0].Regions[0].Region.Polygon.Vertices[0]);
            Assert.AreEqual(new PointF(7, 7), result.Merged[0].Regions[0].Region.Polygon.Vertices[2]);
            Assert.AreEqual(1, result.Diagnostics.InferenceCallCount);
            Assert.AreEqual(1, result.Diagnostics.SucceededResultCount);
            Assert.AreEqual(1, result.Diagnostics.SelectedRoiCount);
        }

        [TestMethod]
        public async Task SemanticSlidingWindowConvenienceEntryProjectsAndFusesSourceMask()
        {
            var modelId = new JYPPX.DeploySharp.Models.ModelId("tests/roi-semantic");
            VisualModelDefinition<string, SemanticSegmentationResult> definition = new VisualModelDefinitionBuilder<string, SemanticSegmentationResult>(
                "tests/roi-semantic.v1", modelId, VisualTaskId.SemanticSegmentation, "1.0", "fake")
                .WithInput("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw)
                .AddOutput("dummy", TensorElementType.Float32, new TensorShape(1, 1))
                .WithInputPreprocessor((_, _, _) => VisualTestData.ClassificationInput())
                .WithDecoder(context => CreateRoiSemanticResult(context.Input.ModelSize))
                .Build();
            using PipelineFixture fixture = VisualTestData.Pipeline(definition.Profile, new TensorShape(1, 1), _ =>
                InferenceOutputs.Create("dummy", new Tensor<float>(new TensorShape(1, 1), new[] { 0f })));

            VisualSize source = new VisualSize(8, 8);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("semantic-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 8)), priority: 9,
                    executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.SemanticSegmentation })
            });
            VisualRoiSlidingWindowMergedResult<SemanticSegmentationResult> result =
                await new VisualRoiSlidingWindowRunner<SemanticSegmentationResult>(fixture.Pipeline).RunSemanticSegmentationRoisAndMergeAsync(
                    snapshot,
                    new SlidingWindowDetectionOptions(new VisualSize(8, 8), overlap: 0, maximumWindows: 1),
                    (roi, geometry, window, token) => Task.FromResult(new PreparedVisualInput(
                        "images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1,
                        VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), window.Bounds))),
                    inference => inference.GetValue<SemanticSegmentationResult>(),
                    backgroundClassIndex: 0);

            Assert.AreEqual(1, result.Merged.Count);
            Assert.AreEqual(source, new VisualSize(result.Merged[0].Mask.Width, result.Merged[0].Mask.Height));
            Assert.AreEqual(16, result.Merged[0].Mask.ToArray().Count(value => value == 1));
            Assert.AreEqual(64, result.Diagnostics.PreparedPixelCount);
            Assert.AreEqual(1, result.Diagnostics.InferenceCallCount);
            Assert.AreEqual(1, result.Diagnostics.SucceededResultCount);
        }

        [TestMethod]
        public async Task TextDetectionSlidingWindowConvenienceEntryProjectsAndSuppressesOverlap()
        {
            var modelId = new JYPPX.DeploySharp.Models.ModelId("tests/roi-text-det");
            VisualModelDefinition<string, TextDetectionResult> definition = new VisualModelDefinitionBuilder<string, TextDetectionResult>(
                "tests/roi-text-det.v1", modelId, VisualTaskId.TextDetection, "1.0", "fake")
                .WithInput("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw)
                .AddOutput("dummy", TensorElementType.Float32, new TensorShape(1, 1))
                .WithInputPreprocessor((_, _, _) => VisualTestData.ClassificationInput())
                .WithDecoder(context => CreateRoiTextDetectionResult(context.Input.ModelSize, .9f))
                .Build();
            using PipelineFixture fixture = VisualTestData.Pipeline(definition.Profile, new TensorShape(1, 1), _ =>
                InferenceOutputs.Create("dummy", new Tensor<float>(new TensorShape(1, 1), new[] { 0f })));

            VisualSize source = new VisualSize(8, 8);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("text-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 8, 8)), executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.TextDetection })
            });
            VisualRoiSlidingWindowMergedResult<TextDetectionResult> result =
                await new VisualRoiSlidingWindowRunner<TextDetectionResult>(fixture.Pipeline).RunTextDetectionRoisAndMergeAsync(
                    snapshot,
                    new SlidingWindowDetectionOptions(new VisualSize(8, 8), overlap: 0, maximumWindows: 1),
                    (roi, geometry, window, token) => Task.FromResult(new PreparedVisualInput(
                        "images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1,
                        VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), window.Bounds))),
                    inference => inference.GetValue<TextDetectionResult>(),
                    mergeMode: RoiResultMergeMode.ClassAgnosticNms);

            Assert.AreEqual(1, result.Merged.Count);
            Assert.AreEqual(1, result.Merged[0].Regions.Count);
            Assert.AreEqual(source, result.Merged[0].SourceSize);
            Assert.AreEqual(new PointF(1, 1), result.Merged[0].Regions[0].Polygon.Vertices[0]);
            Assert.AreEqual(1, result.Diagnostics.InferenceCallCount);
        }

        private static JYPPX.DeploySharp.Visual.OcrResult CreateRoiOcrResult(VisualSize modelSize)
        {
            TextPolygon polygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(0.25f, 0.25f), new PointF(modelSize.Width - 0.25f, 0.25f),
                new PointF(modelSize.Width - 0.25f, modelSize.Height - 0.25f), new PointF(0.25f, modelSize.Height - 0.25f)
            }, OrientedVertexOrder.CounterClockwise);
            var region = new JYPPX.DeploySharp.Visual.TextRegion(0, .9f, polygon);
            var recognition = new RecognizedText(0, "ROI", .95f, Array.Empty<OcrToken>(), "latin", "1", new string('0', 64));
            return new JYPPX.DeploySharp.Visual.OcrResult(new[] { new OcrRegionResult(region, recognition) }, modelSize,
                "det", new JYPPX.DeploySharp.Models.ModelId("det"), "rec", new JYPPX.DeploySharp.Models.ModelId("rec"),
                new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
        }

        private static TextDetectionResult CreateRoiTextDetectionResult(VisualSize modelSize, float score)
        {
            TextPolygon polygon = TextPolygon.Canonicalize(new[]
            {
                new PointF(.25f, .25f), new PointF(modelSize.Width - .25f, .25f),
                new PointF(modelSize.Width - .25f, modelSize.Height - .25f), new PointF(.25f, modelSize.Height - .25f)
            }, OrientedVertexOrder.CounterClockwise);
            return new TextDetectionResult(new[] { new JYPPX.DeploySharp.Visual.TextRegion(0, score, polygon) }, modelSize, "det", new JYPPX.DeploySharp.Models.ModelId("det"));
        }

        private static SemanticSegmentationResult CreateRoiSemanticResult(VisualSize modelSize)
        {
            var classes = new[]
            {
                new SemanticSegmentationClass(0, "background", new SegmentationColor(0, 0, 0), isBackground: true),
                new SemanticSegmentationClass(1, "foreground", new SegmentationColor(255, 0, 0))
            };
            ushort[] labels = new ushort[checked(modelSize.Width * modelSize.Height)];
            labels[0] = 1;
            var mask = new SemanticSegmentationMask(modelSize.Width, modelSize.Height, labels);
            var statistics = classes.Select(value => new SegmentationClassStatistics(value.Index, labels.Count(item => item == value.Index), labels.Count(item => item == value.Index) / (double)labels.Length));
            return new SemanticSegmentationResult(mask, classes, statistics);
        }

        [TestMethod]
        public async Task RoiTrueBatchDiagnosticsMarkSingleBatchCall()
        {
            var profile = new VisualModelProfile(
                "tests/classification-diagnostics-batch.v1",
                VisualTestData.ClassificationModelId,
                VisualTaskId.ImageClassification,
                "1.0",
                "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, minimumBatch: 1, maximumBatch: 4),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 1));
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(2, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(2, 3), new[] { .1f, .8f, .1f, .2f, .7f, .1f })));
            VisualSize source = new VisualSize(10, 10);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("left", new RectangleRoiGeometry(new RectangleF(0, 0, 3, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification }),
                new VisualRoi("right", new RectangleRoiGeometry(new RectangleF(4, 0, 2, 5)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification })
            });
            RoiInferenceBatchResult result = await new VisualRoiRunner(fixture.Pipeline).RunCropBatchAsync(snapshot,
                (rois, geometries, token) => Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), source, new VisualSize(2, 2), 2, VisualTensorLayout.Nchw, ImageTransform.Resize(source, new VisualSize(2, 2)), batchFrames: new[]
                {
                    new VisualInputFrame(source, new VisualSize(2, 2), ImageTransform.Resize(source, new VisualSize(2, 2))),
                    new VisualInputFrame(source, new VisualSize(2, 2), ImageTransform.Resize(source, new VisualSize(2, 2)))
                })), VisualRoiBatchResultSelectors.SelectKnownBatchRow);

            Assert.IsTrue(result.Diagnostics.UsedTrueBatch);
            Assert.AreEqual(2, result.Diagnostics.SelectedRoiCount);
            Assert.AreEqual(1, result.Diagnostics.PreparedInputCount);
            Assert.AreEqual(1, result.Diagnostics.InferenceCallCount);
            Assert.AreEqual(2, result.Diagnostics.SucceededResultCount);
            Assert.AreEqual(22L, result.Diagnostics.PreparedPixelCount);
        }

        [TestMethod]
        public async Task GenericSlidingWindowRunnerChecksCrossRoiPixelBudgetBeforePreparation()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            var source = new VisualSize(8, 4);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("left", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 4)), executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.ImageClassification }),
                new VisualRoi("right", new RectangleRoiGeometry(new RectangleF(4, 0, 4, 4)), executionMode: RoiExecutionMode.SlidingWindow, taskFilter: new[] { VisualTaskId.ImageClassification })
            });
            var options = new SlidingWindowDetectionOptions(new VisualSize(4, 4), overlap: 0, maximumPreparedPixels: 20);
            int preparations = 0;
            var runner = new VisualRoiSlidingWindowRunner<ClassificationResult>(fixture.Pipeline);

            await Assert.ThrowsExactlyAsync<VisualException>(() => runner.RunRoisAsync(
                snapshot,
                options,
                (roi, geometry, window, token) =>
                {
                    preparations++;
                    return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), window.Bounds)));
                },
                (roi, inference, window, prepared) => new RoiProjectedResult<ClassificationResult>(roi.Id, roi.Priority, inference.GetValue<ClassificationResult>(), window.Index)));

            Assert.AreEqual(0, preparations);
        }

        [TestMethod]
        public async Task CropRunnersCheckPreparedPixelBudgetBeforePreparation()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            var source = new VisualSize(8, 4);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("left", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification }),
                new VisualRoi("right", new RectangleRoiGeometry(new RectangleF(4, 0, 4, 4)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification })
            });
            var options = new RoiExecutionOptions(maximumPreparedPixels: 20);
            int preparations = 0;
            var runner = new VisualRoiRunner(fixture.Pipeline);

            await Assert.ThrowsExactlyAsync<VisualException>(() => runner.RunCropAsync(
                snapshot,
                (roi, geometry, token) =>
                {
                    preparations++;
                    return Task.FromResult(VisualTestData.ClassificationInput());
                },
                options));
            Assert.AreEqual(0, preparations);

            await Assert.ThrowsExactlyAsync<VisualException>(() => runner.RunCropBatchAsync(
                snapshot,
                (rois, geometries, token) =>
                {
                    preparations++;
                    return Task.FromResult(VisualTestData.ClassificationInput());
                },
                VisualRoiBatchResultSelectors.SelectKnownBatchRow,
                options));
            Assert.AreEqual(0, preparations);
        }

        [TestMethod]
        public async Task CropCompleteThenFailFinishesLaterChunksBeforeSurfacingFailure()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 3), _ =>
                InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .1f })));
            var source = new VisualSize(8, 4);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("a", new RectangleRoiGeometry(new RectangleF(0, 0, 2, 2)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification }),
                new VisualRoi("b", new RectangleRoiGeometry(new RectangleF(2, 0, 2, 2)), executionMode: RoiExecutionMode.CropAndInfer, taskFilter: new[] { VisualTaskId.ImageClassification })
            });
            var options = new RoiExecutionOptions(prefetch: 1, failureMode: RoiFailureMode.CompleteThenFail);
            int preparations = 0;
            var runner = new VisualRoiRunner(fixture.Pipeline);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runner.RunCropAsync(
                snapshot,
                (roi, geometry, token) =>
                {
                    Interlocked.Increment(ref preparations);
                    if (roi.Id == "a") return Task.FromException<PreparedVisualInput>(new InvalidOperationException("first ROI failed"));
                    return Task.FromResult(new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), source, new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Crop(source, new VisualSize(2, 2), geometry.Bounds)));
                },
                options));

            Assert.AreEqual(2, preparations);
            Assert.AreEqual(1, fixture.Provider.CreatedSessions.Sum(session => session.RunCount));
        }

        [TestMethod]
        public void RoiEventProcessorEmitsDebouncedEnterDwellExitAndExpiresMissingTrack()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 50, 100)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(dwellDuration: TimeSpan.FromSeconds(1), missingTrackTimeout: TimeSpan.FromSeconds(2)));

            IReadOnlyList<VisualRoiEvent> entered = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(9, 9, 2, 2), start, 0));
            Assert.AreEqual(1, entered.Count);
            Assert.AreEqual(VisualRoiEventKind.Entered, entered[0].Kind);
            Assert.AreEqual(0L, processor.GetCounts()["zone:Dwell"]);

            IReadOnlyList<VisualRoiEvent> dwell = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(10, 10, 2, 2), start.AddSeconds(1), 1));
            Assert.AreEqual(1, dwell.Count);
            Assert.AreEqual(VisualRoiEventKind.Dwell, dwell[0].Kind);
            Assert.AreEqual(TimeSpan.FromSeconds(1), dwell[0].InsideDuration);

            IReadOnlyList<VisualRoiEvent> exited = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(59, 9, 2, 2), start.AddSeconds(1.1), 2));
            Assert.AreEqual(1, exited.Count);
            Assert.AreEqual(VisualRoiEventKind.Exited, exited[0].Kind);
            Assert.AreEqual(1L, processor.GetCounts()["zone:Exited"]);

            processor.Process(new VisualRoiTrackObservation("track-2", new RectangleF(10, 10, 2, 2), start.AddSeconds(2), 3));
            IReadOnlyList<VisualRoiEvent> expired = processor.Advance(start.AddSeconds(5), 5);
            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual(VisualRoiEventKind.Exited, expired[0].Kind);
            Assert.AreEqual(new PointF(11, 11), expired[0].Point);
        }

        [TestMethod]
        public void RoiEventProcessorDetectsDirectedLineCrossingAndIgnoresOutOfOrderByDefault()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var line = new VisualRoiLine("gate", "zone", new PointF(0, 50), new PointF(100, 50), VisualRoiLineDirection.Positive);
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, new[] { line });

            processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(9, 39, 2, 2), start, 0));
            IReadOnlyList<VisualRoiEvent> crossed = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(9, 59, 2, 2), start.AddMilliseconds(40), 1));
            Assert.AreEqual(1, crossed.Count);
            Assert.AreEqual(VisualRoiEventKind.LineCrossed, crossed[0].Kind);
            Assert.AreEqual("gate", crossed[0].LineId);
            Assert.AreEqual(50f, crossed[0].Point.Y, .001f);

            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(9, 59, 2, 2), start.AddMilliseconds(20), 0)).Count);
            Assert.AreEqual(1L, processor.GetCounts()["zone:LineCrossed"]);
        }

        [TestMethod]
        public void RoiEventProcessorRequiresCrossingTheFiniteLineSegment()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(200, 200), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 200, 200)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var line = new VisualRoiLine("gate", "zone", new PointF(0, 50), new PointF(100, 50));
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, new[] { line });

            processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(150, 40, 2, 2), start));
            IReadOnlyList<VisualRoiEvent> outside = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(150, 60, 2, 2), start.AddMilliseconds(10)));
            Assert.AreEqual(0, outside.Count);

            processor.Process(new VisualRoiTrackObservation("track-2", new RectangleF(40, 40, 2, 2), start));
            IReadOnlyList<VisualRoiEvent> inside = processor.Process(new VisualRoiTrackObservation("track-2", new RectangleF(40, 60, 2, 2), start.AddMilliseconds(10)));
            Assert.AreEqual(1, inside.Count);
            Assert.AreEqual(new PointF(41, 50), inside[0].Point);
        }

        [TestMethod]
        public void RoiEventProcessorEvaluatesAllConfiguredLinesAgainstTheSameMovementSegment()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(200, 200), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 200, 200)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var lines = new[]
            {
                new VisualRoiLine("horizontal", "zone", new PointF(0, 50), new PointF(200, 50)),
                new VisualRoiLine("vertical", "zone", new PointF(100, 0), new PointF(100, 200))
            };
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, lines);

            processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(48, 48, 2, 2), start));
            IReadOnlyList<VisualRoiEvent> crossed = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(148, 148, 2, 2), start.AddMilliseconds(10)));

            Assert.AreEqual(2, crossed.Count);
            CollectionAssert.AreEquivalent(new[] { "horizontal", "vertical" }, crossed.Select(value => value.LineId).ToArray());
            Assert.AreEqual(2L, processor.GetCounts()["zone:LineCrossed"]);
        }

        [TestMethod]
        public void RoiEventProcessorRejectsOutOfOrderWhenConfiguredAndCooldownSuppressesDuplicateEvent()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(cooldownDuration: TimeSpan.FromSeconds(1)));
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start)).Count);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(15, 1, 2, 2), start.AddMilliseconds(10))).Count);
            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start.AddMilliseconds(20))).Count);

            var rejecting = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(outOfOrderMode: VisualRoiOutOfOrderMode.Reject));
            rejecting.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start));
            Assert.ThrowsExactly<VisualException>(() => rejecting.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start.AddMilliseconds(-1))));
        }

        [TestMethod]
        public void RoiEventProcessorRequiresConfiguredStableObservationCount()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(minimumStableObservations: 3));

            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start)).Count);
            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start.AddMilliseconds(10))).Count);
            IReadOnlyList<VisualRoiEvent> entered = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 2, 2), start.AddMilliseconds(20)));
            Assert.AreEqual(1, entered.Count);
            Assert.AreEqual(VisualRoiEventKind.Entered, entered[0].Kind);
        }

        [TestMethod]
        public void RoiEventProcessorAppliesExitHysteresisWithoutDelayingEntry()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(hysteresisPixels: 1));

            Assert.AreEqual(VisualRoiEventKind.Entered, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(4, 4, 2, 2), start))[0].Kind);
            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(9.5f, 4, 2, 2), start.AddMilliseconds(10))).Count);
            IReadOnlyList<VisualRoiEvent> exited = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(11.5f, 4, 2, 2), start.AddMilliseconds(20)));
            Assert.AreEqual(1, exited.Count);
            Assert.AreEqual(VisualRoiEventKind.Exited, exited[0].Kind);
        }

        [TestMethod]
        public void RoiEventProcessorUpdatesSnapshotAtomicallyAndPreservesOrResetsTrackState()
        {
            var source = new VisualSize(20, 20);
            var initial = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 1);
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(initial, VisualTaskId.ObjectDetection);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(2, 2, 2, 2), start)).Count);

            var replacement = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 15, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 2);
            Assert.AreEqual(2L, processor.UpdateSnapshot(replacement));
            Assert.AreEqual(2L, processor.SnapshotVersion);
            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(3, 3, 2, 2), start.AddMilliseconds(10))).Count);

            var reset = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 15, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 3);
            processor.UpdateSnapshot(reset, updateMode: VisualRoiSnapshotUpdateMode.ResetTrackState);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(3, 3, 2, 2), start.AddMilliseconds(20))).Count);
        }

        [TestMethod]
        public void RoiEventProcessorCanResetOnlyChangedRoiState()
        {
            var source = new VisualSize(20, 20);
            var initial = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("stable", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection }),
                new VisualRoi("changed", new RectangleRoiGeometry(new RectangleF(10, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 10);
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(initial, VisualTaskId.ObjectDetection);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(4, 4, 2, 2), start)).Count);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-2", new RectangleF(14, 4, 2, 2), start)).Count);

            var replacement = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("stable", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection }),
                new VisualRoi("changed", new RectangleRoiGeometry(new RectangleF(8, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 11);
            processor.UpdateSnapshot(replacement, updateMode: VisualRoiSnapshotUpdateMode.ResetChangedRoiState);

            IReadOnlyList<VisualRoiEvent> stableEvents = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(4, 4, 2, 2), start.AddMilliseconds(10)));
            IReadOnlyList<VisualRoiEvent> changedEvents = processor.Process(new VisualRoiTrackObservation("track-2", new RectangleF(14, 4, 2, 2), start.AddMilliseconds(10)));
            Assert.AreEqual(0, stableEvents.Count);
            Assert.AreEqual(1, changedEvents.Count);
            Assert.AreEqual(VisualRoiEventKind.Entered, changedEvents[0].Kind);
        }

        [TestMethod]
        public void RoiEventProcessorCanReevaluateChangedGeometryWithoutSyntheticTransition()
        {
            var source = new VisualSize(40, 20);
            var initial = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 20);
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(initial, VisualTaskId.ObjectDetection);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(3, 3, 2, 2), start)).Count);

            var moved = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(20, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 21);
            processor.UpdateSnapshot(moved, updateMode: VisualRoiSnapshotUpdateMode.ReevaluateTrackState);

            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(4, 3, 2, 2), start.AddMilliseconds(10))).Count);
            IReadOnlyList<VisualRoiEvent> entered = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(23, 3, 2, 2), start.AddMilliseconds(20)));
            Assert.AreEqual(1, entered.Count);
            Assert.AreEqual(VisualRoiEventKind.Entered, entered[0].Kind);
        }

        [TestMethod]
        public void RoiEventProcessorRejectsInvalidSnapshotUpdateWithoutChangingActiveConfiguration()
        {
            var source = new VisualSize(20, 20);
            var initial = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 4);
            var processor = new VisualRoiEventProcessor(initial, VisualTaskId.ObjectDetection);
            var wrongSize = new VisualRoiSnapshot(new VisualSize(30, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 15, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            }, version: 5);

            Assert.ThrowsExactly<ArgumentException>(() => processor.UpdateSnapshot(wrongSize));
            Assert.AreEqual(4L, processor.SnapshotVersion);
            Assert.ThrowsExactly<ArgumentException>(() => processor.UpdateSnapshot(initial, new[] { new VisualRoiLine("bad", "missing", new PointF(0, 0), new PointF(1, 1)) }));
            Assert.AreEqual(4L, processor.SnapshotVersion);
        }

        [TestMethod]
        public void RoiEventProcessorResolvesWorldRoiWithExplicitCoordinateContext()
        {
            var source = new VisualSize(20, 20);
            var worldRoi = new VisualRoi("world-zone", new RectangleRoiGeometry(new RectangleF(1, 1, 3, 3)), RoiCoordinateSpace.World, taskFilter: new[] { VisualTaskId.ObjectDetection });
            var snapshot = new VisualRoiSnapshot(source, new[] { worldRoi });
            var context = VisualRoiCoordinateContext.ForWorld(source, VisualRoiWorldTransform.CreateAffine(2, 0, 0, 0, 2, 0));
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(), coordinateContext: context);

            IReadOnlyList<VisualRoiEvent> entered = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(3, 3, 2, 2), start));
            Assert.AreEqual(1, entered.Count);
            Assert.AreEqual(VisualRoiEventKind.Entered, entered[0].Kind);
            Assert.AreEqual(snapshot.Version, processor.SnapshotVersion);
        }

        [TestMethod]
        public void RoiEventProcessorResetsTileLocalStateWhenProjectionContextChanges()
        {
            var source = new VisualSize(20, 20);
            var roi = new VisualRoi("tile-zone", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), RoiCoordinateSpace.TileLocal, taskFilter: new[] { VisualTaskId.ObjectDetection });
            var initial = new VisualRoiSnapshot(source, new[] { roi }, version: 1);
            var firstContext = VisualRoiCoordinateContext.ForTile(source, new RectangleF(0, 0, 10, 10));
            var secondContext = VisualRoiCoordinateContext.ForTile(source, new RectangleF(10, 0, 10, 10));
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var processor = new VisualRoiEventProcessor(initial, VisualTaskId.ObjectDetection, coordinateContext: firstContext);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(1, 1, 1, 1), start)).Count);

            var replacement = new VisualRoiSnapshot(source, new[] { roi }, version: 2);
            processor.UpdateSnapshot(replacement, updateMode: VisualRoiSnapshotUpdateMode.ResetChangedRoiState, coordinateContext: secondContext);
            IReadOnlyList<VisualRoiEvent> entered = processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(11, 1, 1, 1), start.AddMilliseconds(10)));
            Assert.AreEqual(1, entered.Count);
            Assert.AreEqual(VisualRoiEventKind.Entered, entered[0].Kind);
        }

        [TestMethod]
        public void RoiEventProcessorResetsLineHistoryWhenChangedOnlySnapshotReplacesLines()
        {
            var source = new VisualSize(20, 20);
            var roi = new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection });
            var snapshot = new VisualRoiSnapshot(source, new[] { roi }, version: 1);
            var firstLine = new VisualRoiLine("gate", "zone", new PointF(5, 0), new PointF(5, 20));
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, new[] { firstLine });
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.AreEqual(VisualRoiEventKind.Entered, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(3, 3, 1, 1), start))[0].Kind);
            Assert.IsTrue(processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(7, 3, 1, 1), start.AddMilliseconds(10))).Any(value => value.Kind == VisualRoiEventKind.LineCrossed));

            var replacement = new VisualRoiSnapshot(source, new[] { roi }, version: 2);
            var movedLine = new VisualRoiLine("gate", "zone", new PointF(8, 0), new PointF(8, 20));
            processor.UpdateSnapshot(replacement, new[] { movedLine }, VisualRoiSnapshotUpdateMode.ResetChangedRoiState);
            Assert.AreEqual(0, processor.Process(new VisualRoiTrackObservation("track-1", new RectangleF(9, 3, 1, 1), start.AddMilliseconds(20))).Count);
        }

        [TestMethod]
        public void RoiEventProcessorProcessesFrameInInputOrderWithOneAtomicUpdate()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 100, 100)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection);
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;

            IReadOnlyList<VisualRoiEvent> events = processor.ProcessFrame(new[]
            {
                new VisualRoiTrackObservation("track-a", new RectangleF(10, 10, 2, 2), timestamp, 10),
                new VisualRoiTrackObservation("track-b", new RectangleF(20, 20, 2, 2), timestamp, 10)
            });

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("track-a", events[0].TrackId);
            Assert.AreEqual("track-b", events[1].TrackId);
            Assert.AreEqual(2L, processor.GetCounts()["zone:Entered"]);
        }

        [TestMethod]
        public void RoiEventProcessorRejectsDuplicateFrameIdsBeforeMutatingState()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection);
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;

            Assert.ThrowsExactly<VisualException>(() => processor.ProcessFrame(new[]
            {
                new VisualRoiTrackObservation("duplicate", new RectangleF(1, 1, 1, 1), timestamp),
                new VisualRoiTrackObservation("duplicate", new RectangleF(2, 2, 1, 1), timestamp)
            }));

            IReadOnlyList<VisualRoiEvent> recovered = processor.Process(new VisualRoiTrackObservation("duplicate", new RectangleF(1, 1, 1, 1), timestamp.AddMilliseconds(1)));
            Assert.AreEqual(1, recovered.Count);
            Assert.AreEqual(1L, processor.GetCounts()["zone:Entered"]);
        }

        [TestMethod]
        public void RoiEventProcessorRejectsOverCapacityFrameWithoutAddingAnyNewTrack()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection, options: new VisualRoiEventOptions(maximumTracks: 1));
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;

            Assert.ThrowsExactly<VisualException>(() => processor.ProcessFrame(new[]
            {
                new VisualRoiTrackObservation("track-a", new RectangleF(1, 1, 1, 1), timestamp),
                new VisualRoiTrackObservation("track-b", new RectangleF(2, 2, 1, 1), timestamp)
            }));

            IReadOnlyList<VisualRoiEvent> recovered = processor.Process(new VisualRoiTrackObservation("track-a", new RectangleF(1, 1, 1, 1), timestamp.AddMilliseconds(1)));
            Assert.AreEqual(1, recovered.Count);
            Assert.AreEqual(1L, processor.GetCounts()["zone:Entered"]);
        }

        [TestMethod]
        public void RoiEventProcessorRejectsMixedFrameClockValuesBeforeMutatingState()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(20, 20), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection);
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;

            Assert.ThrowsExactly<VisualException>(() => processor.ProcessFrame(new[]
            {
                new VisualRoiTrackObservation("track-a", new RectangleF(1, 1, 1, 1), timestamp, 3),
                new VisualRoiTrackObservation("track-b", new RectangleF(2, 2, 1, 1), timestamp.AddMilliseconds(1), 3)
            }));

            Assert.AreEqual(0L, processor.GetCounts()["zone:Entered"]);
            Assert.AreEqual(1, processor.Process(new VisualRoiTrackObservation("track-a", new RectangleF(1, 1, 1, 1), timestamp)).Count);
        }

        [TestMethod]
        public void RoiEventProcessorBoundsLongRunningTrackAndCooldownState()
        {
            var source = new VisualSize(20, 20);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 20, 20)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(
                snapshot,
                VisualTaskId.ObjectDetection,
                options: new VisualRoiEventOptions(missingTrackTimeout: TimeSpan.FromMilliseconds(1), maximumTracks: 5000));
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            for (int index = 0; index < 2000; index++)
            {
                processor.Process(new VisualRoiTrackObservation("rotating-" + index, new RectangleF(2, 2, 2, 2), start));
            }

            Assert.AreEqual(2000, processor.ActiveTrackCount);
            Assert.AreEqual(2000, processor.CooldownEntryCount);
            Assert.AreEqual(1, processor.CountKeyCount);
            processor.Advance(start.AddSeconds(1));

            Assert.AreEqual(0, processor.ActiveTrackCount);
            Assert.AreEqual(0, processor.CooldownEntryCount);
            Assert.AreEqual(2, processor.CountKeyCount);
        }

        [TestMethod]
        public void RoiEventProcessorRollingTwentyThousandFrameSoakKeepsTransientStateBounded()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(32, 32), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 32, 32)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(
                snapshot,
                VisualTaskId.ObjectDetection,
                options: new VisualRoiEventOptions(missingTrackTimeout: TimeSpan.FromMilliseconds(1), maximumTracks: 8));
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            const int frames = 20000;
            for (int index = 0; index < frames; index++)
            {
                DateTimeOffset timestamp = start.AddMilliseconds(index * 2L);
                processor.Process(new VisualRoiTrackObservation("rolling-" + (index % 128), new RectangleF(4, 4, 2, 2), timestamp, index));
                processor.Advance(timestamp.AddMilliseconds(1), index);
                if ((index & 1023) == 0)
                {
                    Assert.AreEqual(0, processor.ActiveTrackCount);
                    Assert.AreEqual(0, processor.CooldownEntryCount);
                }
            }

            Assert.AreEqual(0, processor.ActiveTrackCount);
            Assert.AreEqual(0, processor.CooldownEntryCount);
            Assert.AreEqual(2, processor.CountKeyCount);
            Assert.AreEqual((long)frames, processor.GetCounts()["zone:Entered"]);
            Assert.AreEqual((long)frames, processor.GetCounts()["zone:Exited"]);
        }

        [TestMethod]
        public async Task RoiVideoRunnerProcessesLazyFramesAndReportsBoundedHealthCounters()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(32, 32), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 32, 32)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(
                snapshot,
                VisualTaskId.ObjectDetection,
                options: new VisualRoiEventOptions(missingTrackTimeout: TimeSpan.FromMilliseconds(1), maximumTracks: 2));
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            int released = 0;
            var runner = new VisualRoiVideoRunner<int>();

            VisualRoiVideoRunReport report = await runner.RunAsync(
                Enumerable.Range(0, 20000),
                (frame, _) => Task.FromResult<IReadOnlyList<VisualRoiTrackObservation>>(
                    frame % 2 == 0
                        ? new[] { new VisualRoiTrackObservation("track", new RectangleF(4, 4, 2, 2), start.AddMilliseconds(frame * 2L), frame) }
                        : Array.Empty<VisualRoiTrackObservation>()),
                frame => start.AddMilliseconds(frame * 2L),
                processor,
                new VisualRoiVideoRunOptions(maximumFrames: 20000),
                _ => released++);

            Assert.AreEqual(20000, report.ProcessedFrames);
            Assert.AreEqual(0, report.FailedFrames);
            Assert.AreEqual(10000L, report.ObservationCount);
            Assert.AreEqual(20000, released);
            Assert.IsTrue(report.PeakActiveTrackCount <= 1);
            Assert.IsTrue(report.PeakCooldownEntryCount <= 1);
            Assert.AreEqual(0, processor.ActiveTrackCount);
        }

        [TestMethod]
        public async Task RoiVideoRunnerContinuesAfterTimestampOrTrackerFailureAndReleasesEveryFrame()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(16, 16), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 16, 16)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(
                snapshot,
                VisualTaskId.ObjectDetection,
                options: new VisualRoiEventOptions(missingTrackTimeout: TimeSpan.FromMilliseconds(1)));
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            int released = 0;
            var runner = new VisualRoiVideoRunner<int>();

            VisualRoiVideoRunReport report = await runner.RunAsync(
                Enumerable.Range(0, 5),
                (frame, _) =>
                {
                    if (frame == 3) throw new InvalidOperationException("tracker failure");
                    IReadOnlyList<VisualRoiTrackObservation> observations = frame == 0
                        ? new[] { new VisualRoiTrackObservation("track", new RectangleF(1, 1, 2, 2), start, frame) }
                        : Array.Empty<VisualRoiTrackObservation>();
                    return Task.FromResult(observations);
                },
                frame =>
                {
                    if (frame == 2) throw new InvalidOperationException("timestamp failure");
                    return start.AddMilliseconds(frame * 2L);
                },
                processor,
                new VisualRoiVideoRunOptions(maximumFrames: 5, continueOnFrameFailure: true, maximumRecordedFailures: 1),
                _ => released++);

            Assert.AreEqual(5, report.ProcessedFrames);
            Assert.AreEqual(2, report.FailedFrames);
            Assert.AreEqual(1, report.Failures.Count);
            Assert.AreEqual(1, report.DroppedFailureCount);
            Assert.AreEqual(1L, report.ObservationCount);
            Assert.AreEqual(5, released);
            Assert.AreEqual(0, processor.ActiveTrackCount);
        }

        [TestMethod]
        public async Task RoiVideoRunnerReleasesCurrentFrameWhenCancelledOrFrameLimitIsExceeded()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(16, 16), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 16, 16)), taskFilter: new[] { VisualTaskId.ObjectDetection })
            });
            var processor = new VisualRoiEventProcessor(snapshot, VisualTaskId.ObjectDetection);
            DateTimeOffset start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            int released = 0;
            var runner = new VisualRoiVideoRunner<int>();
            VisualException limited = await Assert.ThrowsExactlyAsync<VisualException>(() => runner.RunAsync(
                Enumerable.Range(0, 2),
                (_, _) => Task.FromResult<IReadOnlyList<VisualRoiTrackObservation>>(Array.Empty<VisualRoiTrackObservation>()),
                frame => start.AddMilliseconds(frame),
                processor,
                new VisualRoiVideoRunOptions(maximumFrames: 1, continueOnFrameFailure: true),
                _ => released++));

            Assert.AreEqual(VisualErrorCodes.InputInvalid, limited.ErrorCode);
            Assert.AreEqual(2, released, "The frame yielded after the hard limit must still be returned to the decoder.");

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            int cancelledRelease = 0;
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => runner.RunAsync(
                new[] { 1 },
                (_, _) => Task.FromResult<IReadOnlyList<VisualRoiTrackObservation>>(Array.Empty<VisualRoiTrackObservation>()),
                _ => start,
                processor,
                releaseFrame: _ => cancelledRelease++,
                cancellationToken: cancellation.Token));
            Assert.AreEqual(1, cancelledRelease);
        }

        [TestMethod]
        public void RoiPromptFactoryBuildsDeterministicBoxAndPolygonPrompts()
        {
            var polygon = new PolygonRoiGeometry(new[]
            {
                new PointF(10, 10), new PointF(90, 10), new PointF(50, 90)
            });
            var snapshot = new VisualRoiSnapshot(new VisualSize(100, 100), new[]
            {
                new VisualRoi("poly", polygon, taskFilter: new[] { VisualTaskId.InstanceSegmentation }),
                new VisualRoi("rect", new RectangleRoiGeometry(new RectangleF(20, 20, 30, 30)), taskFilter: new[] { VisualTaskId.InstanceSegmentation })
            });

            PromptableSegmentationPrompt prompt = VisualRoiPromptFactory.Create(snapshot, snapshot.Rois[0], new VisualRoiPromptOptions(maximumPoints: 5), "poly-prompt");
            Assert.AreEqual("poly-prompt", prompt.PromptId);
            Assert.AreEqual(new RectangleF(10, 10, 80, 80), prompt.Box);
            Assert.IsTrue(prompt.Points.Any(point => point.Label == PromptPointLabel.Foreground));
            Assert.IsTrue(prompt.Points.Any(point => point.Label == PromptPointLabel.Background));
            Assert.IsTrue(prompt.Points.Count <= 5);

            IReadOnlyList<PromptableSegmentationPrompt> prompts = VisualRoiPromptFactory.CreateMany(snapshot, VisualTaskId.InstanceSegmentation);
            Assert.AreEqual(2, prompts.Count);
            Assert.AreEqual("rect", prompts[1].PromptId);
        }

        [TestMethod]
        public void RoiPromptFactoryUsesMaskCentroidAndRejectsExcludeRoi()
        {
            var values = new byte[25];
            values[(2 * 5) + 3] = 1;
            var mask = new MaskRoiGeometry(new VisualSize(5, 5), values);
            var include = new VisualRoi("mask", mask, taskFilter: new[] { VisualTaskId.InstanceSegmentation });
            var exclude = new VisualRoi("exclude", new RectangleRoiGeometry(new RectangleF(0, 0, 5, 5)), inclusionMode: RoiInclusionMode.Exclude, taskFilter: new[] { VisualTaskId.InstanceSegmentation });
            var snapshot = new VisualRoiSnapshot(new VisualSize(5, 5), new[] { include, exclude });
            PromptableSegmentationPrompt prompt = VisualRoiPromptFactory.Create(snapshot, include, new VisualRoiPromptOptions(includeBoundaryNegatives: false));
            Assert.AreEqual(new PointF(3.5f, 2.5f), new PointF(prompt.Points[0].X, prompt.Points[0].Y));
            Assert.ThrowsExactly<VisualException>(() => VisualRoiPromptFactory.Create(snapshot, exclude));
        }

        private sealed class IdentityClassificationProjector : IRoiResultProjector<ClassificationResult>
        {
            public ClassificationResult Project(ClassificationResult result, RoiProjection projection) => result;
        }

        private sealed class KeepAllClassificationMerger : IRoiResultMerger<ClassificationResult>
        {
            public IReadOnlyList<ClassificationResult> Merge(IReadOnlyList<RoiProjectedResult<ClassificationResult>> results, RoiResultMergeMode mode)
            {
                return results.Select(value => value.Result).ToList().AsReadOnly();
            }
        }

        private sealed class ThrowingClassificationMerger : IRoiResultMerger<ClassificationResult>
        {
            public IReadOnlyList<ClassificationResult> Merge(IReadOnlyList<RoiProjectedResult<ClassificationResult>> results, RoiResultMergeMode mode)
            {
                throw new InvalidOperationException("The merger must not be called for an empty ROI selection.");
            }
        }
    }
}
