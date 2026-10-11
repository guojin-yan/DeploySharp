"""Summarize OCR enhancement retry evidence with explicit safety and cost gates.

The input files are the per-backend JSON emitted by the opt-in degraded-crop
integration test.  They intentionally contain error metrics rather than copied
ground-truth labels.  This tool therefore reports bounded diagnostics only:
selection counts, false-correction counts (candidate CER/WER worse than the
original), and latency percentiles.  It never turns a smoke sample into a
release accuracy claim.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import statistics
from pathlib import Path
from typing import Any


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
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)


def mean(values: list[float]) -> float | None:
    return statistics.fmean(values) if values else None


def number(value: Any) -> float | None:
    if value is None:
        return None
    return float(value)


def fmt(value: Any, digits: int = 2) -> str:
    if value is None:
        return "-"
    return f"{float(value):.{digits}f}"


def summarize_records(records: list[dict[str, Any]], include_conditions: bool = True) -> dict[str, Any]:
    selected = [row for row in records if bool(row.get("candidateSelected"))]
    available = [row for row in records if row.get("candidateText", "")]
    worse_cer = [
        row for row in selected
        if number(row.get("candidateCaseFoldedCer")) is not None
        and number(row.get("originalCaseFoldedCer")) is not None
        and number(row["candidateCaseFoldedCer"]) > number(row["originalCaseFoldedCer"])
    ]
    worse_wer = [
        row for row in selected
        if number(row.get("candidateCaseFoldedWer")) is not None
        and number(row.get("originalCaseFoldedWer")) is not None
        and number(row["candidateCaseFoldedWer"]) > number(row["originalCaseFoldedWer"])
    ]
    equal = [
        row for row in selected
        if number(row.get("candidateCaseFoldedCer")) is not None
        and number(row.get("originalCaseFoldedCer")) is not None
        and number(row["candidateCaseFoldedCer"]) == number(row["originalCaseFoldedCer"])
    ]
    elapsed = [float(row["elapsedMs"]) for row in records if row.get("elapsedMs") is not None]
    selected_elapsed = [float(row["elapsedMs"]) for row in selected if row.get("elapsedMs") is not None]
    return {
        "records": len(records),
        "candidateAvailable": len(available),
        "candidateSelected": len(selected),
        "insufficientGain": sum(str(row.get("decision")) == "InsufficientGain" for row in records),
        "noDetectedRegion": sum(str(row.get("decision")) == "no-detected-region" for row in records),
        "selectedEqualCaseFoldedCer": len(equal),
        "selectedWorseCaseFoldedCer": len(worse_cer),
        "selectedWorseCaseFoldedWer": len(worse_wer),
        "selectedFalseCorrectionRateByCer": (len(worse_cer) / len(selected)) if selected else None,
        "selectedFalseCorrectionRateByWer": (len(worse_wer) / len(selected)) if selected else None,
        "latencyMs": {
            "all": {"count": len(elapsed), "mean": mean(elapsed), "p50": percentile(elapsed, 0.5), "p95": percentile(elapsed, 0.95)},
            "selected": {"count": len(selected_elapsed), "mean": mean(selected_elapsed), "p50": percentile(selected_elapsed, 0.5), "p95": percentile(selected_elapsed, 0.95)},
        },
        "byCondition": {
            condition: summarize_records([row for row in records if str(row.get("condition")) == condition], include_conditions=False)
            for condition in sorted({str(row.get("condition", "unknown")) for row in records})
        } if include_conditions else {},
    }


def load(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict) or not isinstance(value.get("records"), list):
        raise ValueError(f"Expected enhancement evidence JSON with a records array: {path}")
    return value


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, action="append", required=True, help="Per-backend enhancement evidence JSON.")
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    summaries: list[dict[str, Any]] = []
    for path in args.input:
        value = load(path.resolve())
        summaries.append({
            "backend": value.get("backend", path.stem),
            "evidenceId": path.stem,
            "source": path.as_posix(),
            "sourceSha256": sha256(path.resolve()),
            "recipe": value.get("recipe"),
            "selection": value.get("selection"),
            "summary": summarize_records(value["records"]),
        })

    result = {
        "schemaVersion": "deploysharp-paddleocr-enhancement-safety-cost-v1",
        "generatedUtc": __import__("datetime").datetime.now(__import__("datetime").timezone.utc).isoformat(),
        "backends": summaries,
        "boundary": "Controlled parent-linked crop evidence. False-correction and latency figures are diagnostic gates, not page/line accuracy, robustness, or a default-policy approval.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    lines = [
        "# OCR enhancement safety and cost summary",
        "",
        "This report is a bounded diagnostic over parent-linked degraded crops. A false correction means a selected candidate has a higher case-folded CER or WER than the original row. It is not a release accuracy or robustness claim.",
        "",
        "| Evidence | Backend | Records | Selected | CER-worse selected | CER false-correction rate | WER-worse selected | All P50/P95 ms | Selected P50/P95 ms |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for item in summaries:
        summary = item["summary"]
        all_latency = summary["latencyMs"]["all"]
        selected_latency = summary["latencyMs"]["selected"]
        rate = summary["selectedFalseCorrectionRateByCer"]
        lines.append(
            f"| `{item['evidenceId']}` | {item['backend']} | {summary['records']} | {summary['candidateSelected']} | "
            f"{summary['selectedWorseCaseFoldedCer']} | {fmt(rate, 4)} | {summary['selectedWorseCaseFoldedWer']} | "
            f"{fmt(all_latency['p50'])}/{fmt(all_latency['p95'])} | {fmt(selected_latency['p50'])}/{fmt(selected_latency['p95'])} |"
        )
    lines.extend([
        "",
        "The current policy remains opt-in. Any production threshold must be selected from a larger, quality-controlled labeled set and must include a maximum false-correction rate and an explicit latency budget.",
    ])
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown), "backends": [item["backend"] for item in summaries]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
