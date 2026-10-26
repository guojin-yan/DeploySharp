from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "Audit-PaddleOcrVlLayoutWordCoverage.py"
SPEC = importlib.util.spec_from_file_location("audit_paddleocr_vl_layout_word_coverage", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
AUDIT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(AUDIT)


class LayoutWordCoverageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.image_sha = "a" * 64
        words = [self._word("both", 1, 1), self._word("skipped", 6, 6), self._word("outside", 20, 20)]
        self.annotation = self.root / "annotations.jsonl"
        self.annotation.write_text(json.dumps({
            "record_type": "image", "source_sample_id": "synthetic-1", "width": 100, "height": 100,
            "source_provenance": {"image_sha256": self.image_sha}, "instances": words,
        }) + "\n", encoding="utf-8")
        self.page = self.root / "page-7.json"
        self._write_page()

    def tearDown(self) -> None:
        self.temp.cleanup()

    @staticmethod
    def _word(name: str, x: int, y: int) -> dict:
        return {"instance_id": name, "ignore": False,
                "polygon": [[x, y], [x + 1, y], [x + 1, y + 1], [x, y + 1]]}

    @staticmethod
    def _region(index: int, category: str, bounds: dict, status: str) -> dict:
        return {"regionIndex": index, "source": {"category": category, "score": 0.9, "bounds": bounds},
                "status": status, "result": {"finishReason": "EndOfSequence"} if status == "Succeeded" else None}

    def _write_page(self, image_sha: str | None = None) -> None:
        small = {"x": 0, "y": 0, "width": 3, "height": 3}
        large = {"x": 0, "y": 0, "width": 10, "height": 10}
        regions = [self._region(0, "text", small, "Succeeded"),
                   self._region(1, "image", large, "Skipped")]
        self.page.write_text(json.dumps({
            "page": {"pageIndex": 7, "sourceSize": {"width": 100, "height": 100}},
            "results": [
                {"module": "LayoutDetection", "metadata": {"modelId": "paddle-doc/pp-doclayout-l",
                    "inputSha256": image_sha or self.image_sha},
                 "regions": [region["source"] for region in regions]},
                {"module": "VisionLanguageParsing", "metadata": {"inputSha256": image_sha or self.image_sha},
                 "payload": {"routing": "layout-category", "regions": regions}},
            ],
        }), encoding="utf-8")

    def test_overlapping_skip_does_not_count_as_omitted(self) -> None:
        report = AUDIT.audit(self.annotation, [self.page])
        counts = report["totals"]
        self.assertEqual((counts["words"], counts["completed"], counts["skippedOnly"], counts["uncovered"]),
                         (3, 1, 1, 1))
        self.assertEqual(counts["completedAndSkipped"], 1)
        self.assertEqual([region["coveredWords"] for region in report["pages"][0]["regions"]], [1, 2])
        self.assertNotIn("instance_id", json.dumps(report))

    def test_failed_region_is_not_counted_as_completed(self) -> None:
        content = json.loads(self.page.read_text(encoding="utf-8"))
        content["results"][1]["payload"]["regions"][0]["status"] = "Failed"
        content["results"][1]["payload"]["regions"][0]["result"] = None
        self.page.write_text(json.dumps(content), encoding="utf-8")
        counts = AUDIT.audit(self.annotation, [self.page])["totals"]
        self.assertEqual((counts["failedOnly"], counts["skippedOnly"]), (1, 1))

    def test_wrong_source_sha_is_rejected(self) -> None:
        self._write_page("b" * 64)
        with self.assertRaisesRegex(ValueError, "No annotation matches"):
            AUDIT.audit(self.annotation, [self.page])

    def test_duplicate_page_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "Duplicate page index"):
            AUDIT.audit(self.annotation, [self.page, self.page])

    def test_routed_region_must_match_detector(self) -> None:
        content = json.loads(self.page.read_text(encoding="utf-8"))
        content["results"][1]["payload"]["regions"][0]["source"]["category"] = "table"
        self.page.write_text(json.dumps(content), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "does not match"):
            AUDIT.audit(self.annotation, [self.page])


if __name__ == "__main__":
    unittest.main()
