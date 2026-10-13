from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "Summarize-OcrEnhancementEvidence.py"
SPEC = importlib.util.spec_from_file_location("summarize_ocr_enhancement_evidence", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
SUMMARIZER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SUMMARIZER)


def consensus_row(group_id: str, candidate_cer: float, gain: float = .1) -> dict[str, object]:
    candidate = {
        "text": "same",
        "confidence": .5 + gain,
        "caseFoldedCer": candidate_cer,
        "caseFoldedWer": candidate_cer,
    }
    return {
        "imageId": group_id + "-normal",
        "condition": "normal",
        "sourceCropSha256": group_id,
        "degradedImageSha256": group_id + "-degraded",
        "originalConfidence": .5,
        "originalCaseFoldedCer": .5,
        "originalCaseFoldedWer": .5,
        "candidates": [candidate, dict(candidate)],
    }


class SummarizeOcrEnhancementEvidenceTests(unittest.TestCase):
    def test_grouped_cross_validation_keeps_parent_variants_in_one_balanced_fold(self) -> None:
        records = []
        for index in range(10):
            group_id = f"{index:064x}"
            records.extend([
                consensus_row(group_id, .25),
                {**consensus_row(group_id, .25), "imageId": group_id + "-blur", "condition": "blur"},
            ])

        report = SUMMARIZER.grouped_consensus_cross_validation(records, fold_count=5)

        self.assertEqual(10, report["parentCrops"])
        self.assertEqual(20, report["records"])
        validation_groups = [group_id for fold in report["folds"] for group_id in fold["validationGroupIds"]]
        self.assertEqual(10, len(validation_groups))
        self.assertEqual(10, len(set(validation_groups)))
        self.assertTrue(all(fold["validationParentCrops"] == 2 for fold in report["folds"]))
        self.assertTrue(all(fold["validationRecords"] == 4 for fold in report["folds"]))

    def test_held_out_false_correction_fails_observed_gate(self) -> None:
        records = [consensus_row(f"{index:064x}", .25) for index in range(9)]
        records.append(consensus_row(f"{9:064x}", .75))

        report = SUMMARIZER.grouped_consensus_cross_validation(records, fold_count=5)
        aggregate = report["validationAggregate"]

        self.assertEqual(1, aggregate["foldsWithSelectedThreshold"])
        self.assertEqual(4, aggregate["foldsAbstained"])
        self.assertEqual(2, aggregate["selected"])
        self.assertEqual(1, aggregate["selectedBetterCaseFoldedCer"])
        self.assertEqual(1, aggregate["selectedWorseCaseFoldedCer"])
        self.assertFalse(report["observedGatePassed"])

    def test_missing_parent_identity_is_rejected(self) -> None:
        row = consensus_row("a" * 64, .25)
        row["sourceCropSha256"] = ""

        with self.assertRaisesRegex(ValueError, "sourceCropSha256"):
            SUMMARIZER.grouped_consensus_cross_validation([row, consensus_row("b" * 64, .25)], fold_count=2)


if __name__ == "__main__":
    unittest.main()
