from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[1] / "Compare-PaddleOcrBackendQualityEvidence.py"
SPEC = importlib.util.spec_from_file_location("compare_public_dataset_quality_evidence", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
COMPARATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(COMPARATOR)


class ComparePublicDatasetQualityEvidenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.manifest_record = {
            "image_id": "receipt-001",
            "source_dataset": "example/SROIE",
            "source_split": "train",
            "source_provenance": {"image_sha256": "image-sha256-from-provenance"},
        }
        self.selected_manifest = (json.dumps(self.manifest_record) + "\n").encode("utf-8")
        self.manifest_sha = hashlib.sha256(self.selected_manifest).hexdigest()
        self.ref = self._make_run("reference", "onnxruntime")
        self.candidate = self._make_run("candidate", "openvino")
        self.output_json = self.root / "report" / "compare.json"
        self.output_md = self.root / "report" / "compare.md"

    def tearDown(self) -> None:
        self.temp.cleanup()

    def _make_run(self, name: str, backend: str) -> Path:
        run = self.root / name / "v6-small"
        (run / "raw").mkdir(parents=True)
        (run / "selected-manifest.jsonl").write_bytes(self.selected_manifest)
        (run / "run.json").write_text(json.dumps({
            "model": "v6/small",
            "backend": backend,
            "sourceRevision": "revision-sha",
            "benchmarkAssemblySha256": "assembly-sha",
            "manifestSha256": "source-manifest-sha",
            "selectedManifest": str(run / "selected-manifest.jsonl"),
            "selectedManifestSha256": self.manifest_sha,
            "selectedImageCount": 1,
            "warmup": 1,
            "iterations": 1,
            "batchSize": 1,
            "inferenceChannels": 1,
            "overflowMode": "SlidingWindow",
            "windowOverlap": 0.2,
            "maximumWindowsPerRegion": 32,
            "pipelineTimeoutMs": 60000,
        }), encoding="utf-8")
        (run / "predictions.json").write_text(json.dumps({
            "model": {"detector_sha256": "det-sha", "recognizer_sha256": "rec-sha"},
            "images": [{
                "image_id": "receipt-001",
                "status": "ok",
                "regions": [{"text": "receipt", "polygon": [[0, 0]], "confidence": 0.9}],
            }],
        }), encoding="utf-8")
        (run / "evaluation.json").write_text(json.dumps({
            "det": {"0.5": {"tp": 1, "fp": 0, "fn": 0, "f1_hmean": 1.0}},
            "rec": {"cer": 0.0, "matched_region_wer": 0.0},
            "pipeline": {"end_to_end_cer": 0.0, "end_to_end_wer": 0.0},
            "latency_ms": {"total": {"p50": 10.0, "p95": 10.0}},
        }), encoding="utf-8")
        (run / "raw" / "sample.width.json").write_text(json.dumps({
            "Regions": [{"CharacterSetSha256": "dictionary-sha"}],
        }), encoding="utf-8")
        return run

    def test_report_uses_nested_image_sha_and_actual_dataset_split(self) -> None:
        with patch("sys.argv", [
            str(SCRIPT),
            "--reference-root", str(self.ref.parent),
            "--candidate-root", str(self.candidate.parent),
            "--output-json", str(self.output_json),
            "--output-markdown", str(self.output_md),
        ]):
            COMPARATOR.main()

        report = json.loads(self.output_json.read_text(encoding="utf-8"))
        self.assertEqual(["example/SROIE"], report["dataset"]["sourceDatasets"])
        self.assertEqual(["train"], report["dataset"]["sourceSplits"])
        self.assertEqual("image-sha256-from-provenance", report["dataset"]["images"][0]["imageSha256"])
        self.assertEqual(
            "One measured iteration across 1 distinct page; not the formal 5-warmup/50-iteration benchmark.",
            report["protocol"]["performanceBoundary"],
        )
        markdown = self.output_md.read_text(encoding="utf-8")
        self.assertIn("example/SROIE (train)", markdown)
        self.assertNotIn("HierText `sample-002`", markdown)
        self.assertTrue(report["generatedUtc"])

    def test_semantically_identical_manifest_formatting_is_accepted(self) -> None:
        reformatted = json.dumps(self.manifest_record, sort_keys=True, separators=(", ", ": ")) + "\n"
        candidate_manifest = self.candidate / "selected-manifest.jsonl"
        candidate_manifest.write_text(reformatted, encoding="utf-8")
        candidate_metadata_path = self.candidate / "run.json"
        candidate_metadata = json.loads(candidate_metadata_path.read_text(encoding="utf-8"))
        candidate_metadata["selectedManifestSha256"] = hashlib.sha256(reformatted.encode("utf-8")).hexdigest()
        candidate_metadata_path.write_text(json.dumps(candidate_metadata), encoding="utf-8")

        with patch("sys.argv", [
            str(SCRIPT),
            "--reference-root", str(self.ref.parent),
            "--candidate-root", str(self.candidate.parent),
            "--output-json", str(self.output_json),
            "--output-markdown", str(self.output_md),
        ]):
            COMPARATOR.main()

        report = json.loads(self.output_json.read_text(encoding="utf-8"))
        comparison = report["comparisons"][0]
        self.assertTrue(comparison["protocolCompatible"])
        self.assertNotEqual(
            comparison["selectedManifestByteSha256"]["reference"],
            comparison["selectedManifestByteSha256"]["candidate"],
        )
        self.assertEqual(
            report["dataset"]["selectedManifestSemanticSha256"],
            comparison["selectedManifestSemanticSha256"],
        )

    def test_named_protocol_difference_requires_explicit_allowance(self) -> None:
        candidate_metadata_path = self.candidate / "run.json"
        candidate_metadata = json.loads(candidate_metadata_path.read_text(encoding="utf-8"))
        candidate_metadata["fixedRecognitionWidth"] = 320
        candidate_metadata_path.write_text(json.dumps(candidate_metadata), encoding="utf-8")

        reference = COMPARATOR.load_run(self.ref)
        candidate = COMPARATOR.load_run(self.candidate)
        blocked = COMPARATOR.compare_pair("v6-small", reference, candidate)
        allowed = COMPARATOR.compare_pair(
            "v6-small",
            reference,
            candidate,
            frozenset({"fixedRecognitionWidth"}),
        )

        self.assertFalse(blocked["protocolCompatible"])
        self.assertIn("fixedRecognitionWidth", blocked["blockingProtocolDifferences"])
        self.assertTrue(allowed["protocolCompatible"])
        self.assertIn("fixedRecognitionWidth", allowed["allowedProtocolDifferences"])

        with patch("sys.argv", [
            str(SCRIPT),
            "--reference-root", str(self.ref.parent),
            "--candidate-root", str(self.candidate.parent),
            "--allow-protocol-difference", "fixedRecognitionWidth",
            "--output-json", str(self.output_json),
            "--output-markdown", str(self.output_md),
        ]):
            COMPARATOR.main()

        report = json.loads(self.output_json.read_text(encoding="utf-8"))
        self.assertEqual(["fixedRecognitionWidth"], report["protocol"]["allowedProtocolDifferences"])
        self.assertEqual(["fixedRecognitionWidth"], report["reproduction"]["allowedProtocolDifferences"])
        self.assertIn(
            "--allow-protocol-difference fixedRecognitionWidth",
            self.output_md.read_text(encoding="utf-8"),
        )

    def test_semantically_different_manifest_is_blocked(self) -> None:
        changed_record = {**self.manifest_record, "source_split": "validation"}
        changed_manifest = json.dumps(changed_record) + "\n"
        candidate_manifest = self.candidate / "selected-manifest.jsonl"
        candidate_manifest.write_text(changed_manifest, encoding="utf-8")
        candidate_metadata_path = self.candidate / "run.json"
        candidate_metadata = json.loads(candidate_metadata_path.read_text(encoding="utf-8"))
        candidate_metadata["selectedManifestSha256"] = hashlib.sha256(changed_manifest.encode("utf-8")).hexdigest()
        candidate_metadata_path.write_text(json.dumps(candidate_metadata), encoding="utf-8")

        comparison = COMPARATOR.compare_pair(
            "v6-small",
            COMPARATOR.load_run(self.ref),
            COMPARATOR.load_run(self.candidate),
        )

        self.assertFalse(comparison["protocolCompatible"])
        self.assertIn("selectedManifestSemanticSha256", comparison["blockingProtocolDifferences"])


if __name__ == "__main__":
    unittest.main()
