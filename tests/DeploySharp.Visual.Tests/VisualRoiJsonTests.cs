using System;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class VisualRoiJsonTests
    {
        [TestMethod]
        public void RoundTripPreservesStableSnapshotAndAllGeometryKinds()
        {
            var source = new VisualSize(64, 32);
            var snapshot = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("polygon", new PolygonRoiGeometry(new[] { new PointF(1, 2), new PointF(50, 2), new PointF(20, 20) }), RoiCoordinateSpace.SourcePixels, priority: 3, tags: new[] { "b", "a" }, metadata: new[] { new System.Collections.Generic.KeyValuePair<string, string>("line", "1") }),
                new VisualRoi("rotated", new RotatedRectangleRoiGeometry(new PointF(20, 10), new SizeF(8, 4), 15), RoiCoordinateSpace.SourcePixels),
                new VisualRoi("mask", new MaskRoiGeometry(source, Enumerable.Repeat((byte)1, source.Width * source.Height).ToArray()), RoiCoordinateSpace.SourcePixels, executionMode: RoiExecutionMode.CropAndInfer),
                new VisualRoi("rectangle", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), RoiCoordinateSpace.Normalized, inclusionMode: RoiInclusionMode.Exclude)
            }, version: 7);

            string json = VisualRoiJsonSerializer.Serialize(snapshot);
            VisualRoiSnapshot restored = VisualRoiJsonSerializer.Deserialize(json);

            Assert.AreEqual(7L, restored.Version);
            Assert.AreEqual(source, restored.SourceSize);
            CollectionAssert.AreEqual(new[] { "mask", "polygon", "rectangle", "rotated" }, restored.Rois.OrderBy(value => value.Id, StringComparer.Ordinal).Select(value => value.Id).ToArray());
            Assert.AreEqual(VisualRoiGeometryKind.Mask, restored.Rois.Single(value => value.Id == "mask").Geometry.Kind);
            Assert.AreEqual(2, restored.Rois.Single(value => value.Id == "polygon").Tags.Count);
            Assert.AreEqual("1", restored.Rois.Single(value => value.Id == "polygon").Metadata["line"]);
            Assert.AreEqual(json, VisualRoiJsonSerializer.Serialize(restored));
        }

        [TestMethod]
        public void UnknownPropertyAndUnsupportedSchemaAreRejectedWithPaths()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(10, 10), new[] { new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(1, 1, 2, 2))) });
            string json = VisualRoiJsonSerializer.Serialize(snapshot);

            VisualRoiJsonException unknown = Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.Deserialize(json.Replace("\"version\": 1", "\"version\": 1,\"extra\":true", StringComparison.Ordinal)));
            Assert.AreEqual("unknown_property", unknown.Code);
            Assert.AreEqual("$.extra", unknown.Path);

            VisualRoiJsonException schema = Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.Deserialize(json.Replace("\"1.0\"", "\"2.0\"", StringComparison.Ordinal)));
            Assert.AreEqual("unsupported_schema", schema.Code);
            Assert.AreEqual("$.schemaVersion", schema.Path);
        }

        [TestMethod]
        public void DocumentLimitIsEnforcedBeforeParsingLargeInput()
        {
            VisualRoiJsonException exception = Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.Deserialize("{}", new VisualRoiJsonOptions(maximumDocumentBytes: 1)));
            Assert.AreEqual("limit_exceeded", exception.Code);
        }

        [TestMethod]
        public void GeometrySpecificUnknownPropertiesAndNormalizedMasksAreRejected()
        {
            var snapshot = new VisualRoiSnapshot(new VisualSize(10, 10), new[] { new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(1, 1, 2, 2))) });
            string rectangleJson = VisualRoiJsonSerializer.Serialize(snapshot).Replace("\"height\": 2", "\"height\": 2,\"angleDegrees\": 10", StringComparison.Ordinal);
            VisualRoiJsonException unknown = Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.Deserialize(rectangleJson));
            Assert.AreEqual("unknown_property", unknown.Code);
            Assert.AreEqual("$.rois[0].geometry.angleDegrees", unknown.Path);

            var mask = new VisualRoi("mask", new MaskRoiGeometry(new VisualSize(2, 2), new byte[] { 1, 1, 1, 1 }), RoiCoordinateSpace.Normalized);
            VisualRoiJsonException invalidSpace = Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.Deserialize(VisualRoiJsonSerializer.Serialize(new VisualRoiSnapshot(new VisualSize(2, 2), new[] { mask }))));
            Assert.AreEqual("invalid_geometry", invalidSpace.Code);
            Assert.AreEqual("$.rois[0].coordinateSpace", invalidSpace.Path);
        }

        [TestMethod]
        public void TileLocalAndWorldMasksRoundTripWithNativeRasterDimensions()
        {
            var source = new VisualSize(4, 4);
            var tileMask = new VisualRoi("tile-mask", new MaskRoiGeometry(new VisualSize(2, 2), new byte[]
            {
                1, 0,
                0, 1
            }), RoiCoordinateSpace.TileLocal);
            var worldMask = new VisualRoi("world-mask", new MaskRoiGeometry(new VisualSize(2, 2), new byte[]
            {
                0, 1,
                1, 0
            }), RoiCoordinateSpace.World);
            var snapshot = new VisualRoiSnapshot(source, new[] { tileMask, worldMask });

            string json = VisualRoiJsonSerializer.Serialize(snapshot);
            VisualRoiSnapshot restored = VisualRoiJsonSerializer.Deserialize(json);
            Assert.AreEqual(new VisualSize(2, 2), ((MaskRoiGeometry)restored.Rois.Single(value => value.Id == "tile-mask").Geometry).SourceSize);
            Assert.AreEqual(new VisualSize(2, 2), ((MaskRoiGeometry)restored.Rois.Single(value => value.Id == "world-mask").Geometry).SourceSize);

            VisualRoi restoredTile = restored.Rois.Single(value => value.Id == "tile-mask");
            IVisualRoiGeometry resolvedTile = new VisualRoiSnapshot(source, new[] { restoredTile }).Resolve(restoredTile, VisualRoiCoordinateContext.ForTile(source, new RectangleF(1, 1, 2, 2)));
            Assert.AreEqual(2, ((MaskRoiGeometry)resolvedTile).PixelCount);
            Assert.IsTrue(((MaskRoiGeometry)resolvedTile).Contains(new PointF(1.5f, 1.5f)));
            Assert.IsTrue(((MaskRoiGeometry)resolvedTile).Contains(new PointF(2.5f, 2.5f)));

            VisualRoi restoredWorld = restored.Rois.Single(value => value.Id == "world-mask");
            IVisualRoiGeometry resolvedWorld = new VisualRoiSnapshot(source, new[] { restoredWorld }).Resolve(restoredWorld, VisualRoiCoordinateContext.ForWorld(source, VisualRoiWorldTransform.CreateAffine(2, 0, 0, 0, 2, 0)));
            Assert.AreEqual(8, ((MaskRoiGeometry)resolvedWorld).PixelCount);
            Assert.IsTrue(((MaskRoiGeometry)resolvedWorld).Contains(new PointF(2.5f, .5f)));
            Assert.IsFalse(((MaskRoiGeometry)resolvedWorld).Contains(new PointF(.5f, .5f)));
        }

        [TestMethod]
        public void MaskValuesLengthMismatchIsReportedAsStructuredJsonError()
        {
            const string json = "{\"schemaVersion\":\"1.0\",\"source\":{\"width\":2,\"height\":2},\"version\":1,\"rois\":[{\"id\":\"mask\",\"name\":\"mask\",\"enabled\":true,\"priority\":0,\"coordinateSpace\":\"TileLocal\",\"inclusionMode\":\"Include\",\"executionMode\":\"CropAndInfer\",\"hitTestMode\":\"CenterPoint\",\"hitThreshold\":0.5,\"margin\":0,\"taskFilter\":[],\"classFilter\":[],\"tags\":[],\"metadata\":{},\"geometry\":{\"type\":\"Mask\",\"width\":2,\"height\":2,\"valuesBase64\":\"AQ==\"}}]}";
            VisualRoiJsonException exception = Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.Deserialize(json));
            Assert.AreEqual("invalid_geometry", exception.Code);
            Assert.AreEqual("$.rois[0].geometry", exception.Path);
        }

        [TestMethod]
        public void LoadAndReplaceChangesManagerOnlyAfterSuccessfulValidation()
        {
            var manager = new VisualRoiManager(new VisualRoiSnapshot(new VisualSize(10, 10), new[] { new VisualRoi("old", new RectangleRoiGeometry(new RectangleF(0, 0, 1, 1))) }));
            var candidate = new VisualRoiSnapshot(new VisualSize(20, 20), new[] { new VisualRoi("new", new RectangleRoiGeometry(new RectangleF(1, 1, 2, 2))) }, version: 99);
            string json = VisualRoiJsonSerializer.Serialize(candidate);

            VisualRoiSnapshot installed = VisualRoiJsonSerializer.LoadAndReplace(manager, json);
            Assert.AreEqual(2L, installed.Version);
            Assert.AreEqual("new", manager.Snapshot.Rois[0].Id);

            Assert.ThrowsExactly<VisualRoiJsonException>(() => VisualRoiJsonSerializer.LoadAndReplace(manager, "{\"schemaVersion\":\"2.0\"}"));
            Assert.AreEqual(2L, manager.Snapshot.Version);
        }

        [TestMethod]
        public void SemanticDiffIgnoresVersionAndOrdersChangesByRoiId()
        {
            VisualRoi beforeZone = new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 4)), priority: 1);
            var before = new VisualRoiSnapshot(new VisualSize(10, 10), new[]
            {
                beforeZone,
                new VisualRoi("removed", new RectangleRoiGeometry(new RectangleF(1, 1, 2, 2)))
            }, version: 1);
            var after = new VisualRoiSnapshot(new VisualSize(20, 10), new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 4, 4)), priority: 5, tags: new[] { "inspection" }),
                new VisualRoi("added", new RectangleRoiGeometry(new RectangleF(2, 2, 2, 2)))
            }, version: 999);

            VisualRoiDiff diff = VisualRoiJsonDiff.Compare(before, after);

            Assert.IsTrue(diff.HasChanges);
            Assert.IsTrue(diff.SourceSizeChanged);
            CollectionAssert.AreEqual(new[] { "added", "removed", "zone" }, diff.Changes.Select(value => value.RoiId).ToArray());
            Assert.AreEqual(VisualRoiChangeKind.Added, diff.Changes[0].Kind);
            Assert.AreEqual(VisualRoiChangeKind.Removed, diff.Changes[1].Kind);
            Assert.AreEqual(VisualRoiChangeKind.Changed, diff.Changes[2].Kind);
            CollectionAssert.AreEqual(new[] { "priority", "tags" }, diff.Changes[2].ChangedPaths.ToArray());

            VisualRoiDiff versionOnly = VisualRoiJsonDiff.Compare(before, new VisualRoiSnapshot(before.SourceSize, new[] { beforeZone, before.Rois[1] }, version: 3));
            Assert.IsFalse(versionOnly.HasChanges);
        }

        [TestMethod]
        public void ConfidenceOverrideRoundTripsAndAppearsInSemanticDiff()
        {
            var source = new VisualSize(10, 10);
            var before = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)))
            });
            var configured = new VisualRoiSnapshot(source, new[]
            {
                new VisualRoi("zone", new RectangleRoiGeometry(new RectangleF(0, 0, 10, 10)), confidenceOverride: .75f)
            });

            string json = VisualRoiJsonSerializer.Serialize(configured);
            VisualRoiSnapshot restored = VisualRoiJsonSerializer.Deserialize(json);

            Assert.AreEqual(.75f, restored.Rois[0].ConfidenceOverride!.Value, .000001f);
            StringAssert.Contains(json, "confidenceOverride");
            CollectionAssert.Contains(VisualRoiJsonDiff.Compare(before, restored).Changes[0].ChangedPaths.ToArray(), "confidenceOverride");
        }
    }
}
