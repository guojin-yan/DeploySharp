using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PaddleDocumentTableCellAlignmentTests
    {
        private static readonly string SourceSha = new string('a', 64);

        [TestMethod]
        public void AlignsShuffledDetectorBoxesWithoutUsingStructureGeometry()
        {
            PaddleDocumentTableResult structure = Structure("<tr><td colspan=\"2\"></td></tr><tr><td></td><td></td></tr>", 3);
            PaddleDocumentRegionResult detections = Detections(
                Region(50, 30, 50, 25), Region(0, 0, 100, 25), Region(0, 30, 50, 25));

            PaddleDocumentTableCellAlignmentResult alignment = PaddleDocumentTableCellAligner.Align(structure, detections, 100, 60);

            Assert.IsTrue(alignment.Succeeded, string.Join(",", alignment.Diagnostics));
            Assert.AreEqual(2, alignment.RowCount);
            Assert.AreEqual(3, alignment.Matches.Count);
            Assert.AreEqual(1, alignment.Matches[0].DetectionIndex);
            Assert.AreEqual(2, alignment.Matches[1].DetectionIndex);
            Assert.AreEqual(0, alignment.Matches[2].DetectionIndex);
            Assert.AreEqual(1, alignment.Matches[2].RowIndex);
            Assert.AreEqual(1, alignment.Matches[2].CellIndexInRow);
        }

        [TestMethod]
        public void RejectsCellCountMismatchWithoutPartialAssignments()
        {
            PaddleDocumentTableCellAlignmentResult result = PaddleDocumentTableCellAligner.Align(
                Structure("<tr><td></td><td></td></tr>", 2), Detections(Region(0, 0, 50, 25)), 100, 60);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("detection-count-mismatch", result.Diagnostics[0]);
            Assert.AreEqual(0, result.Matches.Count);
        }

        [TestMethod]
        public void RejectsAmbiguousRowGeometryAndUntrustedMarkup()
        {
            PaddleDocumentRegionResult detections = Detections(
                Region(50, 55, 50, 20), Region(0, 0, 100, 25), Region(0, 30, 50, 20));
            PaddleDocumentTableCellAlignmentResult geometry = PaddleDocumentTableCellAligner.Align(
                Structure("<tr><td></td></tr><tr><td></td><td></td></tr>", 3), detections, 100, 80);
            Assert.IsFalse(geometry.Succeeded);
            Assert.AreEqual("ambiguous-row-geometry:1", geometry.Diagnostics[0]);
            Assert.AreEqual(0, geometry.Matches.Count);

            PaddleDocumentTableCellAlignmentResult markup = PaddleDocumentTableCellAligner.Align(
                Structure("<!DOCTYPE table [<!ENTITY x SYSTEM 'file:///test'>]><tr><td></td></tr>", 1),
                Detections(Region(0, 0, 50, 25)), 100, 80);
            Assert.IsFalse(markup.Succeeded);
            Assert.AreEqual("invalid-structure-markup", markup.Diagnostics[0]);
        }

        [TestMethod]
        public void RejectsDifferentSourceAndIncompleteStructure()
        {
            PaddleDocumentTableResult structure = Structure("<tr><td></td></tr>", 1);
            PaddleDocumentRegionResult otherSource = Detections(new string('b', 64), Region(0, 0, 50, 25));
            Assert.AreEqual("source-mismatch", PaddleDocumentTableCellAligner.Align(structure, otherSource, 100, 60).Diagnostics[0]);

            PaddleDocumentTableResult incomplete = Structure("<tr><td></td></tr>", 1, new[] { "missing-eos:sequence-may-be-truncated" });
            Assert.AreEqual("incomplete-structure", PaddleDocumentTableCellAligner.Align(incomplete,
                Detections(Region(0, 0, 50, 25)), 100, 60).Diagnostics[0]);
        }

        [TestMethod]
        public void DoesNotGuessRowspanningCellAssignments()
        {
            PaddleDocumentTableCellAlignmentResult result = PaddleDocumentTableCellAligner.Align(
                Structure("<tr><td rowspan=\"2\"></td></tr><tr><td></td></tr>", 2),
                Detections(Region(0, 0, 50, 50), Region(50, 25, 50, 25)), 100, 60);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("rowspan-not-validated", result.Diagnostics[0]);
            Assert.AreEqual(0, result.Matches.Count);
        }

        [TestMethod]
        public void FillsOnlyVerifiedCellsAndEscapesRecognizedText()
        {
            PaddleDocumentTableResult structure = Structure("<tr><td></td><td></td></tr>", 2);
            PaddleDocumentTableCellAlignmentResult alignment = PaddleDocumentTableCellAligner.Align(structure,
                Detections(Region(50, 0, 50, 25), Region(0, 0, 50, 25)), 100, 60);
            Assert.IsTrue(alignment.Succeeded);

            string markup = PaddleDocumentTableCellAligner.FillMarkup(structure, alignment, new[] { "A & B", "<script>" });
            StringAssert.Contains(markup, "<td>A &amp; B</td>");
            StringAssert.Contains(markup, "<td>&lt;script&gt;</td>");
            Assert.ThrowsExactly<InvalidOperationException>(() => PaddleDocumentTableCellAligner.FillMarkup(
                Structure("<tr><td></td><td></td></tr>", 2), alignment, new[] { "A", "B" }));
            Assert.ThrowsExactly<InvalidOperationException>(() => PaddleDocumentTableCellAligner.FillMarkup(structure,
                PaddleDocumentTableCellAligner.Align(structure, Detections(Region(0, 0, 50, 25)), 100, 60), new[] { "A", "B" }));
        }

        private static PaddleDocumentTableResult Structure(string markup, int cells, IReadOnlyList<string>? warnings = null)
        {
            var regions = new PaddleDocumentRegion[cells];
            for (int index = 0; index < cells; index++) regions[index] = Region(0, 0, 1, 1);
            return new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition,
                new PaddleDocumentResultMetadata(PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired"), "test", TimeSpan.Zero, SourceSha),
                markup, regions, warnings: warnings);
        }

        private static PaddleDocumentRegionResult Detections(params PaddleDocumentRegion[] regions) => Detections(SourceSha, regions);

        private static PaddleDocumentRegionResult Detections(string sourceSha, params PaddleDocumentRegion[] regions)
            => new PaddleDocumentRegionResult(PaddleDocumentModule.TableCellDetection,
                new PaddleDocumentResultMetadata(PaddleDocumentModelCatalog.Get("paddle-table/rt-detr-l-wired-cell-det"), "test", TimeSpan.Zero, sourceSha), regions);

        private static PaddleDocumentRegion Region(float x, float y, float width, float height)
            => new PaddleDocumentRegion("table-cell", 0.9f, new RectangleF(x, y, width, height));
    }
}
