"""Score DeploySharp width reports against the local OCRBenchmarkTesting manifests."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import sys
from typing import Any


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _normalized_path(path: str | Path) -> str:
    return os.path.normcase(str(Path(path).resolve()))


def _write_new(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        raise FileExistsError(f"Refusing to overwrite existing evidence: {path}")
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _csv_rows(directory: Path) -> dict[str, dict[str, str]]:
    rows: dict[str, dict[str, str]] = {}
    for path in directory.rglob("*.csv"):
        if path.name.lower() == "summary.csv":
            continue
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                image = row.get("image_path")
                if image:
                    key = _normalized_path(image)
                    if key in rows:
                        raise ValueError(f"Duplicate timing row for image {image}")
                    environment_path = Path(str(path) + ".environment.json")
                    if environment_path.is_file():
                        row["__environment"] = json.loads(environment_path.read_text(encoding="utf-8-sig"))
                    rows[key] = row
    return rows


def _width_reports(directory: Path) -> dict[str, dict[str, Any]]:
    reports: dict[str, dict[str, Any]] = {}
    for path in directory.rglob("*.width.json"):
        value = json.loads(path.read_text(encoding="utf-8-sig"))
        image = value.get("Image", {}).get("Path")
        if not image:
            raise ValueError(f"Width report has no source image path: {path}")
        key = _normalized_path(image)
        if key in reports:
            raise ValueError(f"Duplicate width report for image {image}")
        reports[key] = value
    return reports


def _number(value: Any) -> float | None:
    if value in (None, ""):
        return None
    return float(value)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--dataset-root", required=True, type=Path)
    parser.add_argument("--run-directory", required=True, type=Path)
    parser.add_argument("--ocrbench-root", required=True, type=Path)
    parser.add_argument("--predictions", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()

    sys.path.insert(0, str(args.ocrbench_root.resolve()))
    from ocrbench.manifest import read_jsonl, validate_prediction_document
    from ocrbench.geometry import polygon_iou
    from ocrbench.metrics import _match, _valid_gt, edit_distance, evaluate, normalize_text

    run_directory = args.run_directory.resolve()
    run_metadata = json.loads((run_directory / "run.json").read_text(encoding="utf-8-sig"))
    manifest_path = args.manifest.resolve()
    selected_manifest_sha256 = _sha256(manifest_path)
    expected_selected_sha256 = run_metadata.get("selectedManifestSha256")
    if expected_selected_sha256 and selected_manifest_sha256 != expected_selected_sha256:
        raise ValueError(
            "Selected manifest hash differs from the benchmark run metadata: "
            f"expected {expected_selected_sha256}, actual {selected_manifest_sha256}"
        )
    records = read_jsonl(manifest_path)
    widths = _width_reports(run_directory / "raw")
    timings = _csv_rows(run_directory / "csv")
    images: list[dict[str, Any]] = []
    missing: list[str] = []
    for record in records:
        image_path = (args.dataset_root / record["image_relpath"]).resolve()
        key = _normalized_path(image_path)
        width = widths.get(key)
        timing = timings.get(key)
        if timing is None:
            missing.append(record["image_id"])
            continue
        expected_hash = record.get("source_provenance", {}).get("image_sha256")
        actual_hash = _sha256(image_path)
        if width is None:
            if timing.get("status") == "pass":
                missing.append(record["image_id"])
                continue
            images.append({
                "image_id": record["image_id"],
                "status": "error",
                "error": timing.get("detail") or f"benchmark status: {timing.get('status', 'unknown')}",
                "regions": [],
                "timings_ms": {
                    "det": _number(timing.get("detection_ms")),
                    "cls": _number(timing.get("orientation_ms")),
                    "rec": _number(timing.get("recognition_ms")),
                    "total": _number(timing.get("total_ms")),
                },
            })
            continue
        reported_hash = width.get("Image", {}).get("Sha256")
        if expected_hash and expected_hash != actual_hash:
            raise ValueError(f"Dataset image hash differs from manifest for {record['image_id']}")
        if reported_hash and reported_hash != actual_hash:
            raise ValueError(f"Benchmark image hash differs from source for {record['image_id']}")
        regions = []
        for item in width.get("Regions", []):
            vertices = item.get("Polygon", [])
            polygon = [[float(point["X"]), float(point["Y"])] for point in vertices]
            orientation = str(item.get("Orientation", ""))
            predicted_class = {"Degrees0": "0", "Degrees180": "180", "Degrees90": "90", "Degrees270": "270"}.get(orientation)
            regions.append({
                "text": str(item.get("Text", "")),
                "polygon": polygon,
                "confidence": _number(item.get("Confidence")),
                "cls": {"status": "supported", "predicted_class": predicted_class, "confidence": None},
            })
        status = "ok" if regions else "empty"
        images.append({
            "image_id": record["image_id"],
            "status": status,
            "error": None,
            "regions": regions,
            "timings_ms": {
                "det": _number(timing.get("detection_ms")),
                "cls": _number(timing.get("orientation_ms")),
                "rec": _number(timing.get("recognition_ms")),
                "total": _number(timing.get("total_ms")),
            },
        })
    if missing:
        raise ValueError(f"Missing DeploySharp results for {len(missing)} manifest records: {', '.join(missing[:12])}")

    first_report = next(iter(widths.values()), {})
    first_timing = next(iter(timings.values()), {})
    environment = first_timing.get("__environment", {})
    prediction = {
        "schema_version": "1.0",
        "run_id": run_metadata["generatedUtc"],
        "model": {
            "name": "DeploySharp PaddleOCR",
            "version": run_metadata["model"],
            "detector_sha256": first_report.get("Detector", {}).get("Sha256"),
            "recognizer_sha256": first_report.get("Recognizer", {}).get("Sha256"),
            "classifier_sha256": (first_report.get("Classifier") or {}).get("Sha256"),
        },
        "normalization": {"unicode_form": "NFC", "case_sensitive": True, "ignore_whitespace": False, "ignore_punctuation": False},
        "evaluation_context": "detected_crops",
        "run_metadata": {
            "hardware": {
                "platform": environment.get("os", run_metadata.get("operatingSystem", "unknown")),
                "machine": environment.get("machine", run_metadata.get("machine", "unknown")),
                "processor_count": environment.get("processorCount", run_metadata.get("processorCount")),
                "device": "cpu",
            },
            "batch_size": run_metadata["batchSize"],
            "preprocessing": "DeploySharp PaddleOCR full-page DET -> optional CLS -> REC; polygons scored at IoU thresholds",
            "warmup_iterations": run_metadata["warmup"],
            "timing_synchronized": True,
            "source_revision": run_metadata["sourceRevision"],
            "backend": run_metadata["backend"],
            "manifest_sha256": selected_manifest_sha256,
        },
        "images": images,
    }
    integrity = validate_prediction_document(prediction, {record["image_id"] for record in records})
    if integrity:
        raise ValueError("Prediction integrity validation failed: " + "; ".join(integrity))
    report = evaluate(records, prediction, [0.5, 0.75])

    # The public data evaluator currently reports CER but not WER. Calculate both
    # matched-region and end-to-end word error rates with the same IoU matching.
    rules = prediction["normalization"]
    matched_word_edits = matched_reference_words = 0
    e2e_word_edits = e2e_reference_words = 0
    long_region_stats: dict[str, dict[str, int]] = {
        "gt_chars_ge_32": {"regions": 0, "characters": 0, "end_to_end_edits": 0, "matched_regions": 0},
        "gt_chars_ge_128": {"regions": 0, "characters": 0, "end_to_end_edits": 0, "matched_regions": 0},
        "gt_chars_ge_3200": {"regions": 0, "characters": 0, "end_to_end_edits": 0, "matched_regions": 0},
    }
    long_text_examples: list[dict[str, Any]] = []
    pred_by_id = {image["image_id"]: image for image in images}
    for record in records:
        gt_regions = _valid_gt(record)
        pred_regions = pred_by_id[record["image_id"]]["regions"]
        matches, used_gt, used_pred = _match(gt_regions, pred_regions, 0.5)
        matched_by_gt = {gt_index: pred_index for gt_index, pred_index, _ in matches}
        matched_iou_by_gt = {gt_index: iou for gt_index, _, iou in matches}
        for gt_index, gt in enumerate(gt_regions):
            normalized_gt = normalize_text(gt.get("text", ""), rules)
            count = len(normalized_gt)
            if count >= 32:
                overlaps = [(polygon_iou(gt["polygon"], pred["polygon"]), pred_index, pred)
                            for pred_index, pred in enumerate(pred_regions)]
                best_iou, _, best_prediction = max(overlaps, default=(0.0, None, None), key=lambda row: row[0])
                matched_prediction = pred_regions[matched_by_gt[gt_index]] if gt_index in matched_by_gt else None
                matched_text = normalize_text(matched_prediction.get("text", ""), rules) if matched_prediction else ""
                long_text_examples.append({
                    "image_id": record["image_id"],
                    "instance_id": gt.get("instance_id"),
                    "reference_characters": count,
                    "reference_text": gt.get("text", ""),
                    "match_status": "matched" if matched_prediction else "missed",
                    "matched_iou": matched_iou_by_gt.get(gt_index),
                    "best_iou": best_iou,
                    "best_overlap_text": best_prediction.get("text", "") if best_prediction else None,
                    "matched_text": matched_prediction.get("text", "") if matched_prediction else None,
                    "matched_character_edit_distance": edit_distance(list(normalized_gt), list(matched_text)) if matched_prediction else None,
                })
            for threshold, key in ((32, "gt_chars_ge_32"), (128, "gt_chars_ge_128"), (3200, "gt_chars_ge_3200")):
                if count >= threshold:
                    long_region_stats[key]["regions"] += 1
                    long_region_stats[key]["characters"] += count
            ref_words = normalized_gt.split()
            if gt_index in matched_by_gt:
                normalized_pred = normalize_text(pred_regions[matched_by_gt[gt_index]].get("text", ""), rules)
                pred_words = normalized_pred.split()
                distance = edit_distance(ref_words, pred_words)
                character_distance = edit_distance(list(normalized_gt), list(normalized_pred))
                matched_word_edits += distance
                matched_reference_words += len(ref_words)
                e2e_word_edits += distance
                e2e_reference_words += len(ref_words)
                for threshold, key in ((32, "gt_chars_ge_32"), (128, "gt_chars_ge_128"), (3200, "gt_chars_ge_3200")):
                    if count >= threshold:
                        long_region_stats[key]["end_to_end_edits"] += character_distance
                        long_region_stats[key]["matched_regions"] += 1
            else:
                e2e_word_edits += len(ref_words)
                e2e_reference_words += len(ref_words)
                for threshold, key in ((32, "gt_chars_ge_32"), (128, "gt_chars_ge_128"), (3200, "gt_chars_ge_3200")):
                    if count >= threshold:
                        long_region_stats[key]["end_to_end_edits"] += count
        for pred_index, pred in enumerate(pred_regions):
            if pred_index not in used_pred:
                e2e_word_edits += len(normalize_text(pred.get("text", ""), rules).split())
    report["rec"]["matched_region_wer"] = matched_word_edits / matched_reference_words if matched_reference_words else None
    report["rec"]["matched_reference_words"] = matched_reference_words
    report["rec"]["matched_word_edit_distance"] = matched_word_edits
    report["pipeline"]["end_to_end_wer"] = e2e_word_edits / e2e_reference_words if e2e_reference_words else None
    report["pipeline"]["reference_words"] = e2e_reference_words
    report["pipeline"]["word_edit_distance"] = e2e_word_edits
    report["long_text_regions"] = long_region_stats
    report["long_text_regions"] = {
        key: {
            **value,
            "end_to_end_cer": value["end_to_end_edits"] / value["characters"] if value["characters"] else None,
        }
        for key, value in long_region_stats.items()
    }
    report["long_text_examples"] = long_text_examples
    source_datasets = sorted({str(record.get("source_dataset", "unknown")) for record in records})
    licenses = sorted({str(record.get("license", "unknown")) for record in records})
    benchmark_eligibility = sorted({
        str(record.get("benchmark_eligibility", "unspecified")) for record in records
    })
    report["evaluation_provenance"] = {
        "manifest_path": str(manifest_path),
        "manifest_sha256": selected_manifest_sha256,
        "source_manifest_path": run_metadata["manifest"],
        "source_manifest_sha256": run_metadata["manifestSha256"],
        "selected_manifest_sha256": selected_manifest_sha256,
        "dataset_root": str(args.dataset_root.resolve()),
        "source_datasets": source_datasets,
        "licenses": licenses,
        "benchmark_eligibility": benchmark_eligibility,
        "license_scope": "Follow the source-specific image and annotation terms recorded in each manifest. This report does not grant redistribution rights.",
        "benchmark_source_revision": run_metadata.get("sourceRevision"),
        "source_working_tree_dirty": run_metadata.get("sourceWorkingTreeDirty"),
        "source_working_tree_status_sha256": run_metadata.get("sourceWorkingTreeStatusSha256"),
        "benchmark_assembly_sha256": run_metadata.get("benchmarkAssemblySha256"),
        "metric_notes": "CER/WER are case-sensitive NFC metrics. Matched-region metrics require IoU >= 0.5; end-to-end metrics count missed GT regions as deletions and unmatched detections as insertions.",
    }
    _write_new(args.predictions, prediction)
    _write_new(args.report, report)
    print(json.dumps({
        "images": len(images),
        "machine": run_metadata["machine"],
        "operating_system": run_metadata["operatingSystem"],
        "processor_architecture": run_metadata["processArchitecture"],
        "processor_count": run_metadata["processorCount"],
        "det_iou_0.5": report["det"]["0.5"],
        "matched_cer": report["rec"]["cer"],
        "matched_wer": report["rec"]["matched_region_wer"],
        "end_to_end_cer": report["pipeline"]["end_to_end_cer"],
        "end_to_end_wer": report["pipeline"]["end_to_end_wer"],
        "long_text_regions": long_region_stats,
        "predictions": str(args.predictions.resolve()),
        "report": str(args.report.resolve()),
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
