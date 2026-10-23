"""Fail-closed regression cases for VL model/pixel/cache evidence aggregation."""

from __future__ import annotations

import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[1] / "Summarize-PaddleOcrVlReference.py"
spec = importlib.util.spec_from_file_location("vl_reference_summary", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
EVIDENCE = SCRIPT.parents[1] / "verification"
FILES = {
    "generation": "paddleocr-vl-six-task-reference-linux-20261023.json",
    "uncached-formula": "paddleocr-vl-formula-uncached-reference-linux-20261023.json",
    "processor-parity": "paddleocr-vl-image-processor-parity-20261023.json",
    "manifest": "paddleocr-vl-reference-cases-20261023.json",
    "device": "paddleocr-vl-reference-device-linux-20261023.json",
}


class ReferenceSummaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.inputs = {key: (EVIDENCE / name).read_bytes() for key, name in FILES.items()}

    def run_summary(self, mutate=None):
        inputs = copy.deepcopy(self.inputs)
        if mutate:
            data = json.loads(inputs["generation"])
            mutate(data)
            inputs["generation"] = json.dumps(data).encode("utf-8")
        with tempfile.TemporaryDirectory(prefix="deploysharp-vl-reference-") as temporary:
            root = Path(temporary)
            argv = [str(SCRIPT)]
            for key, value in inputs.items():
                path = root / FILES[key]
                path.write_bytes(value)
                argv += ["--" + key, str(path)]
            output = root / "summary.json"
            with patch.object(sys, "argv", argv + ["--output", str(output)]):
                module.main()
            return json.loads(output.read_text(encoding="utf-8"))

    def test_actual_complete_evidence_passes(self):
        report = self.run_summary()
        self.assertEqual("validated", report["status"])
        self.assertEqual(6, len(report["cases"]))
        self.assertTrue(report["uncachedFormula"]["fullTokenAndTextEqual"])

    def test_rejects_incomplete_run(self):
        with self.assertRaisesRegex(ValueError, "did not complete"):
            self.run_summary(lambda data: data.update(status="running"))

    def test_rejects_different_checkpoint_weights(self):
        with self.assertRaisesRegex(ValueError, "weight identity"):
            self.run_summary(lambda data: data["checkpoint"].update(weightSha256="0" * 64))

    def test_rejects_length_truncation_disguised_as_success(self):
        with self.assertRaisesRegex(ValueError, "generated EOS"):
            self.run_summary(lambda data: data["cases"][0].update(stopReason="maximum-new-tokens"))

    def test_rejects_changed_uncached_prefix_decision(self):
        def mutate(data):
            data["cases"][0]["uncachedComparison"]["tokenIds"][0] = 0
        with self.assertRaisesRegex(ValueError, "prefix mismatch"):
            self.run_summary(mutate)

    def test_rejects_repeated_image_encoding_during_decode(self):
        def mutate(data):
            data["cases"][0]["forwardCalls"][1]["pixelValuesPresent"] = True
        with self.assertRaisesRegex(ValueError, "reuse exact KV"):
            self.run_summary(mutate)

    def test_rejects_generation_with_uncompared_pixels(self):
        def mutate(data):
            data["cases"][0]["tensors"]["pixel_values"]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "not the compared pixel tensor"):
            self.run_summary(mutate)

    def test_rejects_full_formula_cache_drift_after_prefix(self):
        def mutate(data):
            formula = next(case for case in data["cases"] if case["task"] == "formula")
            formula["generatedTokenIds"][20] = 0
        with self.assertRaisesRegex(ValueError, "Full uncached formula token/text mismatch"):
            self.run_summary(mutate)


if __name__ == "__main__":
    unittest.main()
