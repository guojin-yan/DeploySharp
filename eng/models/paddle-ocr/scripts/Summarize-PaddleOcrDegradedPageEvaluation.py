"""Summarize full-page degradation evaluation by condition."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import statistics
import sys
from pathlib import Path
from typing import Any


CONDITION_PATTERN = re.compile(r"-degraded-(?P<condition>.+?)-(?P<severity>[^-]+)$")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def percentile(values: list[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * fraction
    lower = int(position)
    upper = min(lower + 1, len(ordered) - 1)
    weight = position - lower
    return ordered[lower] + (ordered[upper] - ordered[lower]) * weight


def condition_for(image_id: str) -> tuple[str, str]:
    match = CONDITION_PATTERN.search(image_id)
    if not match:
        raise ValueError(f"Cannot infer degradation condition from image id: {image_id}")
    return match.group("condition"), match.group("severity")


def summarize_group(records: list[dict[str, Any]], prediction: dict[str, Any], evaluate: Any) -> dict[str, Any]:
    result = evaluate(records, prediction, [0.5, 0.75])
    image_rows = prediction.get("images", [])
    timings = [float(row.get("timings_ms", {}).get("total")) for row in image_rows if row.get("timings_ms", {}).get("total") is not None]
    det_timings = [float(row.get("timings_ms", {}).get("det")) for row in image_rows if row.get("timings_ms", {}).get("det") is not None]
    rec_timings = [float(row.get("timings_ms", {}).get("rec")) for row in image_rows if row.get("timings_ms", {}).get("rec") is not None]
    statuses = {str(row.get("status", "unknown")) for row in image_rows}
    return {
        "images": len(records),
        "predictedImages": len(image_rows),
        "statusCounts": {status: sum(1 for row in image_rows if str(row.get("status", "unknown")) == status) for status in sorted(statuses)},
        "detection": result.get("det"),
        "recognition": result.get("rec"),
        "pipeline": result.get("pipeline"),
        "latencyMs": {
            "total": {"count": len(timings), "p50": percentile(timings, 0.5), "p95": percentile(timings, 0.95), "mean": statistics.fmean(timings) if timings else None},
            "det": {"count": len(det_timings), "p50": percentile(det_timings, 0.5), "p95": percentile(det_timings, 0.95), "mean": statistics.fmean(det_timings) if det_timings else None},
            "rec": {"count": len(rec_timings), "p50": percentile(rec_timings, 0.5), "p95": percentile(rec_timings, 0.95), "mean": statistics.fmean(rec_timings) if rec_timings else None},
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--predictions", type=Path, required=True)
    parser.add_argument("--evaluation", type=Path, required=True)
    parser.add_argument("--ocrbench-root", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    sys.path.insert(0, str(args.ocrbench_root.resolve()))
    from ocrbench.manifest import read_jsonl
    from ocrbench.metrics import evaluate

    records = read_jsonl(args.manifest.resolve())
    prediction = json.loads(args.predictions.read_text(encoding="utf-8-sig"))
    evaluation = json.loads(args.evaluation.read_text(encoding="utf-8-sig"))
    record_by_id = {str(record["image_id"]): record for record in records}
    prediction_by_id = {str(row["image_id"]): row for row in prediction.get("images", [])}
    if set(record_by_id) != set(prediction_by_id):
        missing = sorted(set(record_by_id) - set(prediction_by_id))
        unknown = sorted(set(prediction_by_id) - set(record_by_id))
        raise ValueError(f"Prediction IDs differ from manifest; missing={missing[:3]}, unknown={unknown[:3]}")

    groups: dict[tuple[str, str], list[str]] = {}
    for image_id in record_by_id:
        groups.setdefault(condition_for(image_id), []).append(image_id)
    summaries: dict[str, dict[str, Any]] = {}
    for (condition, severity), image_ids in sorted(groups.items()):
        group_records = [record_by_id[image_id] for image_id in image_ids]
        group_prediction = {key: value for key, value in prediction.items() if key != "images"}
        group_prediction["images"] = [prediction_by_id[image_id] for image_id in image_ids]
        summaries[condition] = summarize_group(group_records, group_prediction, evaluate)
        summaries[condition]["severity"] = severity

    value = {
        "schemaVersion": "deploysharp-paddleocr-full-page-degradation-summary-v1",
        "sourceManifestSha256": sha256(args.manifest.resolve()),
        "predictionsSha256": sha256(args.predictions.resolve()),
        "evaluationSha256": sha256(args.evaluation.resolve()),
        "model": prediction.get("model"),
        "backend": prediction.get("run_metadata", {}).get("backend"),
        "sourceRevision": prediction.get("run_metadata", {}).get("source_revision"),
        "conditions": summaries,
        "globalEvaluation": {
            "images": evaluation.get("images"),
            "detection": evaluation.get("det"),
            "recognition": evaluation.get("rec"),
            "pipeline": evaluation.get("pipeline"),
            "latencyMs": evaluation.get("latency_ms"),
        },
        "interpretation": "Controlled full-page degradation smoke. Conditions preserve SROIE polygons but do not represent a natural low-quality test split or a production accuracy guarantee.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# PaddleOCR full-page degradation summary",
        "",
        f"- Model/backend: `{prediction.get('model', {}).get('version', 'unknown')}` / `{value['backend']}`",
        f"- Source manifest SHA-256: `{value['sourceManifestSha256']}`",
        f"- Records: `{len(records)}` (`{', '.join(sorted(summaries))}`)",
        "- Scope: controlled full-page degradation smoke; the polygons and text come from the original SROIE annotations, so this is not a natural low-quality split or a release accuracy claim.",
        "",
        "| Condition | Images | Empty | IoU0.5 F1 | Matched CER | E2E CER | Total P50 ms | Total P95 ms |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for condition, summary in summaries.items():
        det = summary["detection"]["0.5"]
        rec = summary["recognition"]
        pipe = summary["pipeline"]
        total = summary["latencyMs"]["total"]
        empty = summary["statusCounts"].get("empty", 0)
        lines.append(f"| {condition} | {summary['images']} | {empty} | {det['f1_hmean']:.4f} | {rec['cer']:.4f} | {pipe['end_to_end_cer']:.4f} | {total['p50']:.2f} | {total['p95']:.2f} |")
    lines.extend([
        "",
        "The normal row is the page-level baseline for this generated selection. Severe rows are useful for comparing relative degradation and augmentation decisions, but should not be interpreted as a model benchmark outside this controlled protocol.",
    ])
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown), "conditions": sorted(summaries)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
