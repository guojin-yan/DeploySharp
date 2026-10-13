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
    available = [
        row for row in records
        if row.get("candidateText", "") or any(candidate.get("text", "") for candidate in row.get("candidates", []))
    ]
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
    final_cer = [float(row["finalCaseFoldedCer"]) for row in records if row.get("finalCaseFoldedCer") is not None]
    final_wer = [float(row["finalCaseFoldedWer"]) for row in records if row.get("finalCaseFoldedWer") is not None]
    original_cer = [float(row["originalCaseFoldedCer"]) for row in records if row.get("originalCaseFoldedCer") is not None]
    original_wer = [float(row["originalCaseFoldedWer"]) for row in records if row.get("originalCaseFoldedWer") is not None]
    consensus_counts = [int(row.get("consensusMatchCount", 0)) for row in records]
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
        "meanOriginalCaseFoldedCer": mean(original_cer),
        "meanFinalCaseFoldedCer": mean(final_cer),
        "meanOriginalCaseFoldedWer": mean(original_wer),
        "meanFinalCaseFoldedWer": mean(final_wer),
        "recordsWithCandidateConsensus": sum(value >= 2 for value in consensus_counts),
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


def record_key(row: dict[str, Any]) -> tuple[str, str, str, str]:
    return (
        str(row.get("imageId", "")),
        str(row.get("condition", "")),
        str(row.get("sourceCropSha256", "")),
        str(row.get("degradedImageSha256", "")),
    )


def final_text(row: dict[str, Any]) -> str:
    if "finalText" in row:
        return str(row.get("finalText", ""))
    if bool(row.get("candidateSelected")):
        return str(row.get("candidateText", ""))
    return str(row.get("originalText", ""))


def best_consensus_candidate(row: dict[str, Any]) -> dict[str, Any] | None:
    candidates = [candidate for candidate in row.get("candidates", []) if str(candidate.get("text", ""))]
    counts: dict[str, int] = {}
    for candidate in candidates:
        text = str(candidate["text"])
        counts[text] = counts.get(text, 0) + 1
    eligible = [candidate for candidate in candidates if counts[str(candidate["text"])] >= 2]
    if not eligible:
        return None
    return max(eligible, key=lambda candidate: float(candidate.get("confidence", 0)))


def consensus_threshold_sweep(records: list[dict[str, Any]]) -> dict[str, Any]:
    thresholds = [0, .005, .01, .02, .03, .05, .075, .1, .15, .2]
    rows: list[dict[str, Any]] = []
    for threshold in thresholds:
        selected: list[tuple[dict[str, Any], dict[str, Any]]] = []
        final_cer: list[float] = []
        final_wer: list[float] = []
        for row in records:
            candidate = best_consensus_candidate(row)
            original_confidence = number(row.get("originalConfidence"))
            candidate_confidence = number(candidate.get("confidence")) if candidate else None
            gain = candidate_confidence - original_confidence if candidate_confidence is not None and original_confidence is not None else None
            choose = candidate is not None and gain is not None and gain > 0 and gain >= threshold
            if choose:
                selected.append((row, candidate))
                candidate_cer = number(candidate.get("caseFoldedCer"))
                candidate_wer = number(candidate.get("caseFoldedWer"))
                if candidate_cer is not None:
                    final_cer.append(candidate_cer)
                if candidate_wer is not None:
                    final_wer.append(candidate_wer)
            else:
                original_cer = number(row.get("originalCaseFoldedCer"))
                original_wer = number(row.get("originalCaseFoldedWer"))
                if original_cer is not None:
                    final_cer.append(original_cer)
                if original_wer is not None:
                    final_wer.append(original_wer)
        better_cer = sum(
            number(candidate.get("caseFoldedCer")) is not None
            and number(row.get("originalCaseFoldedCer")) is not None
            and number(candidate["caseFoldedCer"]) < number(row["originalCaseFoldedCer"])
            for row, candidate in selected
        )
        worse_cer = sum(
            number(candidate.get("caseFoldedCer")) is not None
            and number(row.get("originalCaseFoldedCer")) is not None
            and number(candidate["caseFoldedCer"]) > number(row["originalCaseFoldedCer"])
            for row, candidate in selected
        )
        rows.append({
            "minimumConfidenceGain": threshold,
            "selected": len(selected),
            "selectedBetterCaseFoldedCer": better_cer,
            "selectedWorseCaseFoldedCer": worse_cer,
            "selectedEqualCaseFoldedCer": len(selected) - better_cer - worse_cer,
            "meanFinalCaseFoldedCer": mean(final_cer),
            "meanFinalCaseFoldedWer": mean(final_wer),
        })
    beneficial_safe = [row for row in rows if row["selectedWorseCaseFoldedCer"] == 0 and row["selectedBetterCaseFoldedCer"] > 0]
    return {
        "points": rows,
        "minimumObservedZeroWorseThresholdWithBenefit": beneficial_safe[0]["minimumConfidenceGain"] if beneficial_safe else None,
        "boundary": "Post-hoc diagnostic on this fixed evidence set; it must not be reused as a production threshold without an independent validation split.",
    }


def compare_backends(evidence: list[dict[str, Any]]) -> list[dict[str, Any]]:
    comparisons: list[dict[str, Any]] = []
    for left_index in range(len(evidence)):
        for right_index in range(left_index + 1, len(evidence)):
            left = evidence[left_index]
            right = evidence[right_index]
            left_rows = {record_key(row): row for row in left["value"]["records"]}
            right_rows = {record_key(row): row for row in right["value"]["records"]}
            if len(left_rows) != len(left["value"]["records"]) or len(right_rows) != len(right["value"]["records"]):
                raise ValueError("Enhancement evidence contains duplicate record identities.")
            common_keys = sorted(set(left_rows) & set(right_rows))
            comparisons.append({
                "leftBackend": left["backend"],
                "rightBackend": right["backend"],
                "leftRecords": len(left_rows),
                "rightRecords": len(right_rows),
                "commonRecords": len(common_keys),
                "leftOnlyRecords": len(set(left_rows) - set(right_rows)),
                "rightOnlyRecords": len(set(right_rows) - set(left_rows)),
                "decisionMatched": sum(str(left_rows[key].get("decision")) == str(right_rows[key].get("decision")) for key in common_keys),
                "candidateSelectedMatched": sum(bool(left_rows[key].get("candidateSelected")) == bool(right_rows[key].get("candidateSelected")) for key in common_keys),
                "finalTextMatched": sum(final_text(left_rows[key]) == final_text(right_rows[key]) for key in common_keys),
                "selectedCandidateIndexMatched": sum(left_rows[key].get("selectedCandidateIndex") == right_rows[key].get("selectedCandidateIndex") for key in common_keys),
                "consensusMatchCountMatched": sum(int(left_rows[key].get("consensusMatchCount", 0)) == int(right_rows[key].get("consensusMatchCount", 0)) for key in common_keys),
            })
    return comparisons


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, action="append", required=True, help="Per-backend enhancement evidence JSON.")
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    summaries: list[dict[str, Any]] = []
    evidence: list[dict[str, Any]] = []
    for path in args.input:
        value = load(path.resolve())
        backend = value.get("backend", path.stem)
        evidence.append({"backend": backend, "value": value})
        summaries.append({
            "backend": backend,
            "evidenceId": path.stem,
            "source": path.as_posix(),
            "sourceSha256": sha256(path.resolve()),
            "executionMode": value.get("executionMode", "legacy-detector-first-pipeline"),
            "recipes": value.get("recipes", [value.get("recipe")] if value.get("recipe") else []),
            "selection": value.get("selection"),
            "summary": summarize_records(value["records"]),
            "consensusThresholdSweep": consensus_threshold_sweep(value["records"]) if value.get("selection", "").startswith("ConsensusConfidenceGain") else None,
        })

    comparisons = compare_backends(evidence)
    result = {
        "schemaVersion": "deploysharp-paddleocr-enhancement-safety-cost-v2",
        "generatedUtc": __import__("datetime").datetime.now(__import__("datetime").timezone.utc).isoformat(),
        "backends": summaries,
        "crossBackend": comparisons,
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
        "| Evidence | Backend | Mode | Records | Consensus | Selected | CER before/after | WER before/after | CER-worse selected | CER false-correction rate | All P50/P95 ms |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for item in summaries:
        summary = item["summary"]
        all_latency = summary["latencyMs"]["all"]
        selected_latency = summary["latencyMs"]["selected"]
        rate = summary["selectedFalseCorrectionRateByCer"]
        lines.append(
            f"| `{item['evidenceId']}` | {item['backend']} | {item['executionMode']} | {summary['records']} | "
            f"{summary['recordsWithCandidateConsensus']} | {summary['candidateSelected']} | "
            f"{fmt(summary['meanOriginalCaseFoldedCer'], 4)}/{fmt(summary['meanFinalCaseFoldedCer'], 4)} | "
            f"{fmt(summary['meanOriginalCaseFoldedWer'], 4)}/{fmt(summary['meanFinalCaseFoldedWer'], 4)} | "
            f"{summary['selectedWorseCaseFoldedCer']} | {fmt(rate, 4)} | "
            f"{fmt(all_latency['p50'])}/{fmt(all_latency['p95'])} |"
        )
    lines.extend([
        "",
        "The current policy remains opt-in. Any production threshold must be selected from a larger, quality-controlled labeled set and must include a maximum false-correction rate and an explicit latency budget.",
    ])
    if comparisons:
        lines.extend([
            "",
            "## Cross-backend agreement",
            "",
            "| Pair | Common | Decision | Selection flag | Final text | Selected index | Consensus count |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
        ])
        for item in comparisons:
            common = item["commonRecords"]
            lines.append(
                f"| {item['leftBackend']} / {item['rightBackend']} | {common} | {item['decisionMatched']}/{common} | "
                f"{item['candidateSelectedMatched']}/{common} | {item['finalTextMatched']}/{common} | "
                f"{item['selectedCandidateIndexMatched']}/{common} | {item['consensusMatchCountMatched']}/{common} |"
            )
    for item in summaries:
        sweep = item.get("consensusThresholdSweep")
        if not sweep:
            continue
        lines.extend([
            "",
            f"## Consensus threshold diagnostic: {item['backend']}",
            "",
            "| Minimum gain | Selected | CER better | CER worse | CER equal | Mean final CER/WER |",
            "| ---: | ---: | ---: | ---: | ---: | ---: |",
        ])
        for point in sweep["points"]:
            lines.append(
                f"| {fmt(point['minimumConfidenceGain'], 3)} | {point['selected']} | "
                f"{point['selectedBetterCaseFoldedCer']} | {point['selectedWorseCaseFoldedCer']} | "
                f"{point['selectedEqualCaseFoldedCer']} | {fmt(point['meanFinalCaseFoldedCer'], 4)}/{fmt(point['meanFinalCaseFoldedWer'], 4)} |"
            )
        lines.extend([
            "",
            "Observed zero-worse threshold with at least one CER improvement: "
            + (fmt(sweep["minimumObservedZeroWorseThresholdWithBenefit"], 3) if sweep["minimumObservedZeroWorseThresholdWithBenefit"] is not None else "none"),
            "",
            sweep["boundary"],
        ])
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown), "backends": [item["backend"] for item in summaries]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
