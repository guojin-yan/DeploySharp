"""Summarize parent-linked OCR degradation crop evidence without copying test data."""

from __future__ import annotations

import argparse
import hashlib
import json
import statistics
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def mean(values: list[float]) -> float | None:
    return statistics.fmean(values) if values else None


def load(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict) or not isinstance(value.get("records"), list):
        raise ValueError(f"Expected a per-backend records JSON: {path}")
    return value


def summarize(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in records:
        groups[str(row.get("condition", "unknown"))].append(row)
    result = []
    for condition in sorted(groups):
        rows = groups[condition]
        cer = [float(row["caseFoldedCharacterErrorRate"]) for row in rows]
        wer = [float(row["caseFoldedWordErrorRate"]) for row in rows]
        confidence = [float(row["confidence"]) for row in rows if row.get("confidence") is not None]
        result.append({
            "condition": condition,
            "records": len(rows),
            "emptyRecognized": sum(not str(row.get("recognizedText", "")) for row in rows),
            "exactCaseFolded": sum(float(row["caseFoldedCharacterErrorRate"]) == 0 for row in rows),
            "meanCaseFoldedCharacterErrorRate": mean(cer),
            "meanCaseFoldedWordErrorRate": mean(wer),
            "meanConfidence": mean(confidence),
        })
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, action="append", required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    manifest = args.manifest.resolve()
    evidence = [path.resolve() for path in args.evidence]
    values = [load(path) for path in evidence]
    if not values:
        raise ValueError("At least one evidence file is required")
    record_counts = {len(value["records"]) for value in values}
    if len(record_counts) != 1:
        raise ValueError(f"Backend record counts differ: {sorted(record_counts)}")
    first = values[0]
    expected_ids = {str(row["imageId"]) for row in first["records"]}
    parity = []
    for value in values[1:]:
        actual_ids = {str(row["imageId"]) for row in value["records"]}
        parity.append({
            "backend": value.get("backend"),
            "recordIdSetEqual": expected_ids == actual_ids,
            "exactRecognizedText": sum(
                str(left.get("recognizedText", "")) == str(right.get("recognizedText", ""))
                for left, right in zip(first["records"], value["records"])
            ),
            "caseFoldedCerEqual": sum(
                float(left["caseFoldedCharacterErrorRate"]) == float(right["caseFoldedCharacterErrorRate"])
                for left, right in zip(first["records"], value["records"])
            ),
            "maxConfidenceDelta": max(
                abs(float(left.get("confidence", 0)) - float(right.get("confidence", 0)))
                for left, right in zip(first["records"], value["records"])
            ),
        })

    source_manifest = manifest
    generated_manifest = Path(first["manifestPath"]).resolve()
    source_info = {
        "dataset": "FUNSD",
        "split": "test",
        "revision": first["records"][0].get("sourceRevision"),
        "sourceManifest": {"path": str(source_manifest), "sha256": sha256(source_manifest)},
        "generatedManifest": {"path": str(generated_manifest), "sha256": sha256(generated_manifest)},
        "parentPages": len({str(row["sourceParentImageId"]) for row in first["records"]}),
        "sourceCrops": len({str(row["sourceCropSha256"]) for row in first["records"]}),
        "records": len(first["records"]),
        "conditions": sorted({str(row.get("condition")) for row in first["records"]}),
        "severity": sorted({str(item) for item in (first.get("severity") or [])}),
        "annotationGranularity": "word-crop; not line/page truth",
    }
    backends = [{
        "backend": str(value.get("backend")),
        "runtime": value.get("runtime"),
        "records": len(value["records"]),
        "detectorSha256": value.get("detectorSha256"),
        "recognizerSha256": value.get("recognizerSha256"),
        "dictionarySha256": value.get("dictionarySha256"),
        "evidenceSha256": sha256(path),
        "conditions": summarize(value["records"]),
    } for value, path in zip(values, evidence)]
    result = {
        "schemaVersion": "deploysharp-paddleocr-degraded-crop-quality-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "sample": "synthetic-degraded-funsd-b1b-20261012",
        "dataset": source_info,
        "backends": backends,
        "parity": parity,
        "boundary": "Controlled parent-linked FUNSD word crops. Severe transformations are synthetic; FUNSD annotations are word/entity-level and cannot establish line/page robustness, release accuracy, or default enhancement benefit.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# FUNSD parent-linked degradation crop evidence (2026-10-12)",
        "",
        "This is a controlled severe-degradation smoke over source-linked FUNSD word crops. It is not line/page accuracy, natural robustness, or an approval to enable enhancement by default.",
        "",
        f"- Parent pages: **{source_info['parentPages']}**; source crops: **{source_info['sourceCrops']}**; records: **{source_info['records']}**",
        f"- Conditions: `{', '.join(source_info['conditions'])}`; severity: `{', '.join(source_info['severity'])}`",
        f"- Source manifest SHA-256: `{source_info['sourceManifest']['sha256']}`",
        f"- Generated manifest SHA-256: `{source_info['generatedManifest']['sha256']}`",
        "",
        "| Backend | Condition | Records | Empty | Exact (case-folded) | Mean CER | Mean WER |",
        "|---|---|---:|---:|---:|---:|---:|",
    ]
    for backend in backends:
        for row in backend["conditions"]:
            lines.append(f"| {backend['backend']} | {row['condition']} | {row['records']} | {row['emptyRecognized']} | {row['exactCaseFolded']} | {row['meanCaseFoldedCharacterErrorRate']:.4f} | {row['meanCaseFoldedWordErrorRate']:.4f} |")
    lines.extend([
        "",
        "## Interpretation",
        "",
        "Blur is the dominant failure mode in this generated FUNSD sample; the other conditions mostly preserve the same word-crop error profile. The two CPU backends are compared on identical generated inputs and this run does not authorize a production enhancement policy.",
        "",
        "A larger, quality-controlled line/page set and explicit false-correction/cost gates are still required for B1b/B2/B3.",
    ])
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
