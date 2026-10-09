from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[1] / "Merge-PaddleOcrPublicDatasetRuns.py"
SPEC = importlib.util.spec_from_file_location("merge_public_dataset_runs", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MERGER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MERGER)


def manifest_bytes(image_id: str) -> bytes:
    return (json.dumps({"image_id": image_id, "image_relpath": f"{image_id}.jpg"}) + "\n").encode()


class MergePublicDatasetRunsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.runs = [self._make_run("run-a", "page-a"), self._make_run("run-b", "page-b")]
        self.output = self.root / "merged"

    def tearDown(self) -> None:
        self.temp.cleanup()

    def _make_run(self, name: str, image_id: str) -> Path:
        run = self.root / name
        (run / "csv").mkdir(parents=True)
        (run / "raw").mkdir()
        manifest = manifest_bytes(image_id)
        (run / "selected-manifest.jsonl").write_bytes(manifest)
        (run / "csv" / f"{image_id}.csv").write_text("status\npass\n", encoding="utf-8")
        (run / "raw" / f"{image_id}.json").write_text("{}\n", encoding="utf-8")
        (run / "run.json").write_text(json.dumps({
            "generatedUtc": "2026-10-09T00:00:00Z",
            "model": "v6/small",
            "backend": "onnxruntime",
            "sourceRevision": "abc123",
            "benchmarkAssemblySha256": "assembly-sha",
            "warmup": 1,
            "iterations": 1,
            "batchSize": 16,
            "inferenceChannels": 1,
            "maximumRegions": 1024,
            "overflowMode": "SlidingWindow",
            "windowOverlap": 0.2,
            "maximumWindowsPerRegion": 32,
            "pipelineTimeoutMs": 60000,
            "selectedManifestSha256": hashlib.sha256(manifest).hexdigest(),
            "selectedImageCount": 1,
        }), encoding="utf-8")
        return run

    def _merge(self) -> None:
        with patch("sys.argv", [str(SCRIPT), "--run-directory", str(self.runs[0]),
                                 "--run-directory", str(self.runs[1]),
                                 "--output-directory", str(self.output)]):
            MERGER.main()

    def test_merged_metadata_points_to_existing_union_manifest(self) -> None:
        self._merge()

        metadata = json.loads((self.output / "run.json").read_text(encoding="utf-8"))
        manifest_path = self.output / metadata["manifest"]
        self.assertTrue(manifest_path.is_file())
        self.assertEqual("selected-manifest.jsonl", metadata["manifest"])
        self.assertEqual(2, metadata["selectedImageCount"])
        self.assertEqual(
            metadata["selectedManifestSha256"],
            hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
        )
        ids = [json.loads(line)["image_id"] for line in manifest_path.read_text(encoding="utf-8").splitlines()]
        self.assertEqual(["page-a", "page-b"], ids)

    def test_duplicate_image_ids_are_rejected_before_output_creation(self) -> None:
        self.runs[1] = self._make_run("run-c", "page-a")
        with self.assertRaisesRegex(ValueError, "Duplicate image_id"):
            self._merge()
        self.assertFalse(self.output.exists())

    def test_protocol_mismatch_is_rejected_before_output_creation(self) -> None:
        metadata_path = self.runs[1] / "run.json"
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        metadata["batchSize"] = 8
        metadata_path.write_text(json.dumps(metadata), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "protocol/provenance differs"):
            self._merge()
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
