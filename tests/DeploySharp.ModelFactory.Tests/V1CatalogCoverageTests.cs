using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class V1CatalogCoverageTests
    {
        [TestMethod]
        public void V1RowsHaveCompleteLocalCoverageAndThirtyTwoPublishedCatalogMappings()
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            var expected = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["YOLOCls"] = "yolo/v8/classify/s",
                ["YOLOv5Det"] = "yolo/v5/detect/n",
                ["YOLOv5Seg"] = "yolo/v5/segment/s",
                ["YOLOv6Det"] = "yolo/v6/detect/s",
                ["YOLOv7Det"] = "yolo/v7/detect/base",
                ["YOLOv8Det"] = "yolo/v8/detect/n",
                ["YOLOv8Seg"] = "yolo/v8/segment/n",
                ["YOLOv8Obb"] = "yolo/v8/obb/s",
                ["YOLOv8Pose"] = "yolo/v8/pose/s",
                ["YOLOv9Det"] = "yolo/v9/detect/s",
                ["YOLOv9Seg"] = "yolo/v9/segment/c",
                ["YOLOv10Det"] = "yolo/v10/detect/n",
                ["YOLOv11Det"] = "yolo/v11/detect/n",
                ["YOLOv11Seg"] = "yolo/v11/segment/s",
                ["YOLOv11Obb"] = "yolo/v11/obb/s",
                ["YOLOv11Pose"] = "yolo/v11/pose/s",
                ["YOLOv12Det"] = "yolo/v12/detect/n",
                ["YOLOv13Det"] = "yolo/v13/detect/n",
                ["YOLOv26Det"] = "yolo/v26/detect/n",
                ["YOLOv26Seg"] = "yolo/v26/segment/s",
                ["YOLOv26Obb"] = "yolo/v26/obb/s",
                ["YOLOv26Pose"] = "yolo/v26/pose/s",
                ["DEIMv2Det"] = "deim/v2/detect",
                ["RFDETRDet"] = "rf-detr/detect",
                ["RFDETRSeg"] = "rf-detr/segment",
                ["RTDETRDet"] = "rt-detr/r50vd-decoded-vector-onnx",
                ["PPYOLOETDet"] = "pp-yoloe/plus-crn-l",
                ["PaddleOcrDet"] = "paddleocr/ppocrv5/mobile-det",
                ["PaddleOcrCls"] = "paddleocr/ppocrv5/mobile-cls",
                ["PaddleOcrRec"] = "paddleocr/ppocrv5/mobile-rec",
                ["BriaRmbg"] = "bria/rmbg-1.4",
                ["AnomalibSeg"] = "anomalib/padim/mvtec-bottle"
            };

            Assert.AreEqual(32, expected.Count);
            var missing = expected.Where(pair => pair.Value == null).Select(pair => pair.Key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEquivalent(Array.Empty<string>(), missing);

            foreach (KeyValuePair<string, string?> pair in expected.Where(pair => pair.Value != null))
            {
                ModelCatalogEntry? entry = catalog.Document.Entries.SingleOrDefault(value => string.Equals(value.ModelId, pair.Value, StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(entry, pair.Key + " has no catalog mapping.");
                Assert.AreEqual(ModelCatalogStatus.Preview, entry!.Status, pair.Key + " is not Preview.");
                Assert.IsTrue(entry.Source!.RedistributionAllowed, pair.Key + " is not explicitly redistributable.");
                Assert.IsTrue(entry.Artifacts.SelectMany(value => value.Assets).All(asset => asset.DownloadUri != null && asset.Size > 0 && !string.IsNullOrWhiteSpace(asset.Sha256)), pair.Key + " has an incomplete downloadable asset contract.");
            }
        }
    }
}
