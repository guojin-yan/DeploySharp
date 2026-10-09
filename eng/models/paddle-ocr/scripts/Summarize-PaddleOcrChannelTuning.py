"""Summarize PaddleOCR stage-channel tuning without hiding contract instability.

Each child directory under ``--run-root`` is expected to contain ``run.json``
and one CSV per selected page.  The benchmark reports page-level P50/P95
values, so this script summarizes those values across pages; it does not
pretend that a median of page percentiles is a pooled dataset percentile.
The optional diagnostic CSVs are single-image reruns and are reported
separately from the three-page tuning groups.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import statistics
from pathlib import Path
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def number(row: dict[str, str], key: str) -> float:
    value = row.get(key, "")
    if value in (None, ""):
        raise ValueError(f"Missing {key!r} in {row}")
    result = float(value)
    if not math.isfinite(result):
        raise ValueError(f"Non-finite {key!r}: {value!r}")
    return result


def percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        raise ValueError("Cannot summarize an empty sequence")
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return ordered[lower]
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)


def summarize(values: list[float]) -> dict[str, float]:
    return {
        "min": min(values),
        "median": statistics.median(values),
        "mean": statistics.fmean(values),
        "max": max(values),
        "p25": percentile(values, 0.25),
        "p75": percentile(values, 0.75),
    }


def load_group(path: Path) -> dict[str, Any]:
    metadata = read_json(path / "run.json")
    csv_paths = sorted((path / "csv").glob("*.csv"))
    if not csv_paths:
        raise FileNotFoundError(f"No page CSVs under {path}")
    rows: list[dict[str, str]] = []
    for csv_path in csv_paths:
        with csv_path.open("r", encoding="utf-8-sig", newline="") as stream:
            page_rows = list(csv.DictReader(stream))
        if len(page_rows) != 1:
            raise ValueError(f"Expected one row in {csv_path}, got {len(page_rows)}")
        rows.append(page_rows[0])
    passed = [row for row in rows if row.get("status") == "pass"]
    failed = [row for row in rows if row.get("status") != "pass"]
    item: dict[str, Any] = {
        "model": metadata.get("model"),
        "backend": metadata.get("backend"),
        "device": rows[0].get("device") if rows else None,
        "channels": int(metadata.get("inferenceChannels", 0)),
        "batchSize": int(metadata.get("batchSize", 0)),
        "warmup": int(metadata.get("warmup", 0)),
        "iterations": int(metadata.get("iterations", 0)),
        "runDirectory": str(path),
        "selectedImageCount": int(metadata.get("selectedImageCount", len(rows))),
        "successfulPages": len(passed),
        "failedPages": len(failed),
        "failedImages": [row.get("image_path") for row in failed],
        "sourceRevision": metadata.get("sourceRevision"),
    }
    if passed:
        for key, output_key in (("total_ms", "pageMeanMs"), ("total_p50_ms", "pageP50Ms"), ("total_p95_ms", "pageP95Ms")):
            item[output_key] = summarize([number(row, key) for row in passed])
        item["contractVariantCounts"] = sorted({int(row.get("result_contract_variants") or "1") for row in passed})
        item["semanticContractSha256"] = sorted({row.get("result_semantic_contract_sha256") for row in passed if row.get("result_semantic_contract_sha256")})
        item["textSha256Variants"] = len({row.get("result_text_sha256") for row in passed})
        item["strictContractSha256Variants"] = len({row.get("result_contract_sha256") for row in passed})
    else:
        item["pageMeanMs"] = item["pageP50Ms"] = item["pageP95Ms"] = None
        item["contractVariantCounts"] = []
        item["semanticContractSha256"] = []
        item["textSha256Variants"] = None
        item["strictContractSha256Variants"] = None
    return item


def load_diagnostic(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    if len(rows) != 1:
        raise ValueError(f"Expected one diagnostic row in {path}, got {len(rows)}")
    row = rows[0]
    metadata_path = Path(str(path) + ".environment.json")
    metadata = read_json(metadata_path) if metadata_path.is_file() else {}
    return {
        "csv": str(path),
        "metadata": str(metadata_path) if metadata_path.is_file() else None,
        "model": f"{row.get('version')}/{row.get('variant')}",
        "backend": row.get("backend"),
        "image": row.get("image_path"),
        "status": row.get("status"),
        "warmup": metadata.get("protocol", {}).get("warmup", metadata.get("warmup")),
        "iterations": metadata.get("protocol", {}).get("iterations", metadata.get("iterations")),
        "batchSize": row.get("selected_batch_size"),
        "channels": row.get("selected_inference_channels"),
        "totalMs": float(row["total_ms"]) if row.get("total_ms") else None,
        "totalP50Ms": float(row["total_p50_ms"]) if row.get("total_p50_ms") else None,
        "totalP95Ms": float(row["total_p95_ms"]) if row.get("total_p95_ms") else None,
        "regions": int(row["regions"]) if row.get("regions") else None,
        "resultTextSha256": row.get("result_text_sha256"),
        "resultContractSha256": row.get("result_contract_sha256"),
        "resultSemanticContractSha256": row.get("result_semantic_contract_sha256"),
        "resultContractVariants": int(row.get("result_contract_variants") or "1"),
        "resultNumericMaxAbsDrift": float(row["result_numeric_max_abs_drift"]) if row.get("result_numeric_max_abs_drift") else None,
        "detail": row.get("detail"),
    }


def load_followup_rows(path: Path) -> list[dict[str, Any]]:
    """Load one or more page rows from a direct benchmark CSV.

    The benchmark can emit all three v6 variants in one CSV when
    ``DEPLOYSHARP_PADDLEOCR_VERSIONS=v6``.  Keeping the rows here (instead of
    asking callers to split the file by hand) makes the follow-up report
    faithful to the actual command output and preserves the page image path.
    """
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    if not rows:
        raise ValueError(f"Expected at least one benchmark row in {path}")
    metadata_path = Path(str(path) + ".environment.json")
    metadata = read_json(metadata_path) if metadata_path.is_file() else {}
    protocol = metadata.get("protocol", {})
    result: list[dict[str, Any]] = []
    for row in rows:
        result.append(
            {
                "sourceCsv": str(path),
                "model": f"{row.get('version')}/{row.get('variant')}",
                "version": row.get("version"),
                "variant": row.get("variant"),
                "backend": row.get("backend"),
                "device": row.get("device"),
                "status": row.get("status"),
                "image": row.get("image_path"),
                "batchSize": int(row["selected_batch_size"]) if row.get("selected_batch_size") else protocol.get("batchSize"),
                "channels": int(row["selected_inference_channels"]) if row.get("selected_inference_channels") else protocol.get("stageConcurrency"),
                "totalMs": float(row["total_ms"]) if row.get("total_ms") else None,
                "totalP50Ms": float(row["total_p50_ms"]) if row.get("total_p50_ms") else None,
                "totalP95Ms": float(row["total_p95_ms"]) if row.get("total_p95_ms") else None,
                "regions": int(row["regions"]) if row.get("regions") else None,
                "resultTextSha256": row.get("result_text_sha256"),
                "resultContractSha256": row.get("result_contract_sha256"),
                "resultSemanticContractSha256": row.get("result_semantic_contract_sha256"),
                "resultContractVariants": int(row.get("result_contract_variants") or "1") if row.get("status") == "pass" else None,
                "resultNumericMaxAbsDrift": float(row["result_numeric_max_abs_drift"]) if row.get("result_numeric_max_abs_drift") else None,
                "detail": row.get("detail"),
            }
        )
    return result


def summarize_followup(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[tuple[Any, ...], list[dict[str, Any]]] = {}
    for row in rows:
        key = (row["model"], row["backend"], row["channels"], row["batchSize"])
        groups.setdefault(key, []).append(row)
    output: list[dict[str, Any]] = []
    for key, group_rows in sorted(groups.items(), key=lambda item: tuple(str(value) for value in item[0])):
        passed = [row for row in group_rows if row["status"] == "pass"]
        failed = [row for row in group_rows if row["status"] != "pass"]
        semantic_values = {row["resultSemanticContractSha256"] for row in passed if row["resultSemanticContractSha256"]}
        text_values = {row["resultTextSha256"] for row in passed if row["resultTextSha256"]}
        strict_values = {row["resultContractSha256"] for row in passed if row["resultContractSha256"]}
        strict_stable_pages = sum(1 for row in passed if row["resultContractVariants"] == 1)
        semantic_stable_pages = sum(1 for row in passed if row["resultSemanticContractSha256"])
        max_drift = max(
            (row["resultNumericMaxAbsDrift"] or 0.0 for row in passed),
            default=0.0,
        )
        if failed:
            classification = "mixed-or-blocked"
        elif strict_stable_pages == len(passed):
            classification = "strict-stable"
        elif semantic_stable_pages == len(passed) and len(text_values) == 1:
            classification = "semantic-stable-numeric-drift"
        else:
            classification = "unstable"
        item: dict[str, Any] = {
            "model": key[0],
            "backend": key[1],
            "channels": key[2],
            "batchSize": key[3],
            "selectedPages": len(group_rows),
            "successfulPages": len(passed),
            "failedPages": len(failed),
            "failedImages": [row["image"] for row in failed],
            "classification": classification,
            "pageP50Ms": summarize([row["totalP50Ms"] for row in passed if row["totalP50Ms"] is not None]) if passed else None,
            "pageP95Ms": summarize([row["totalP95Ms"] for row in passed if row["totalP95Ms"] is not None]) if passed else None,
            "regions": {
                "min": min((row["regions"] for row in passed if row["regions"] is not None), default=None),
                "max": max((row["regions"] for row in passed if row["regions"] is not None), default=None),
            },
            "strictStablePages": strict_stable_pages,
            "semanticStablePages": semantic_stable_pages,
            "textSha256Variants": len(text_values),
            "semanticContractSha256Variants": len(semantic_values),
            "strictContractSha256VariantsAcrossPages": len(strict_values),
            "contractVariantCounts": sorted({row["resultContractVariants"] for row in passed if row["resultContractVariants"] is not None}),
            "maximumNumericDrift": max_drift,
            "rows": group_rows,
        }
        output.append(item)
    return output


def markdown(report: dict[str, Any]) -> str:
    lines = [
        "# PaddleOCR channel tuning and contract-drift follow-up (2026-10-09)",
        "",
        "This report records a deliberately narrow ORT CUDA tuning experiment on one JYPPX Windows host. It does not change the library default channel count and does not promote a channel count from one model to every model.",
        "",
        "## Protocol",
        "",
        "- Dataset: HierText `sample-002`, three selected validation pages (`5c4d5de59518fe4d`, `97a0add3f8f47b65`, `d14658b78cec2cf7`).",
        "- Backend: `onnxruntime-cuda`; batch `16`; `5` warm-up and `50` measured iterations; model loading excluded; input reuse disabled.",
        "- Page P50/P95 are calculated by the benchmark over the repeated calls for one page. The group median is a summary of those page-level values, not a pooled dataset percentile.",
        "- The original three-page runs were collected from a dirty worktree at source revision `c1dcb4123c9d720852e4c3251ab24a4ac39b67ae`. The contract-diagnostic rerun uses the semantic-contract instrumentation introduced after that run.",
        "",
        "## Three-page tuning results",
        "",
        "| Model | Channels | Pages pass/fail | Mean of page P50 (ms) | Median page P50 (ms) | Median page P95 (ms) | Strict contract variants across pages |",
        "|---|---:|---:|---:|---:|---:|---|",
    ]
    for item in report["groups"]:
        if item["pageP50Ms"] is None:
            lines.append(f"| {item['model']} | {item['channels']} | {item['successfulPages']}/{item['failedPages']} | — | — | — | — |")
            continue
        lines.append(
            f"| {item['model']} | {item['channels']} | {item['successfulPages']}/{item['failedPages']} | {item['pageP50Ms']['mean']:.3f} | {item['pageP50Ms']['median']:.3f} | {item['pageP95Ms']['median']:.3f} | {item['contractVariantCounts']} |"
        )
    lines += [
        "",
        "### Interpretation",
        "",
        "- v6 Tiny averaged approximately `125.48 / 96.10 / 110.21 ms` page-P50 for channels `1 / 2 / 4`; channels=2 was the fastest in this three-page selection, while channels=4 was slower and more variable.",
        "- v5 Mobile channels=1 and channels=2 both completed all three pages; the page-level results must be considered together with the recognition-batch count and should not be generalized beyond this model/device.",
        "- v6 Medium channels=2 did not complete all three pages under the original strict contract gate. The failure was on `97a0add3f8f47b65`, so it is not a valid universal recommendation.",
        "",
        "## v6 Medium strict-contract diagnostic",
        "",
        "The same `v6/medium`, page `97a0add3f8f47b65`, batch=16/channels=2 workload was rerun with `DEPLOYSHARP_PADDLEOCR_ALLOW_NUMERIC_CONTRACT_DRIFT=1`. The diagnostic completed `5 + 50` calls with `result_text_sha256` and the new semantic contract SHA stable, while the full floating-point-inclusive contract was summarized below with its observed variant count. This isolates the observed drift to fields intentionally excluded from the semantic fingerprint (scores, polygon coordinates, or confidence values); it does not prove geometric tolerance or accuracy parity.",
        "",
    ]
    for diagnostic in report["diagnostics"]:
        lines.append(
            f"- `{diagnostic['model']}` `{diagnostic['image']}`: status `{diagnostic['status']}`, total P50/P95 `{diagnostic['totalP50Ms']}`/`{diagnostic['totalP95Ms']}` ms, strict contract variants `{diagnostic['resultContractVariants']}`, maximum absolute numeric drift `{diagnostic['resultNumericMaxAbsDrift']}`, semantic SHA `{diagnostic['resultSemanticContractSha256']}`, text SHA `{diagnostic['resultTextSha256']}`."
        )
    lines += [
        "",
        "The benchmark still rejects a contract change by default. The diagnostic switch is opt-in and only permits a run when the text and semantic contract remain stable; a semantic change remains a failure. The switch must not be used to turn a quality or accuracy run into a pass.",
        "",
        "## Decision and next work",
        "",
        "- Keep the production/default `inferenceChannels` unchanged. Do not write channels=2 into a global default based on this sample.",
        "- Treat v6 Tiny channels=2 and v5 Mobile channels=2 as candidates for a larger multi-page confirmation, not as a library-wide recommendation.",
        "- Before using channels>1 for v6 Medium, collect a larger diagnostic set and quantify coordinate/score/confidence drift against an explicit tolerance. A semantic drift or text drift remains a blocking failure.",
        "- This report is a performance/concurrency diagnostic only; it does not close the PP-OCR accuracy gate, TensorRT/OpenVINO/OpenCV matrix, or cross-device evidence requirements.",
        "",
    ]
    followup = report.get("followupGroups", [])
    if followup:
        lines += [
            "## Multi-page contract follow-up",
            "",
            "These rows are direct `5 warmup + 50 measured` reruns with ORT CUDA, batch `16`, channels `2`, `MAX_REGIONS=64` for the diagnostic page. The three v6 rows in a single CSV are split by model here so the report does not hide per-variant outcomes.",
            "",
            "| Model | Pages pass/fail | Classification | Page P50 median (ms) | Page P95 median (ms) | Strict-stable pages | Semantic-stable pages | Max numeric drift |",
            "|---|---:|---|---:|---:|---:|---:|---:|",
        ]
        for item in followup:
            if item["pageP50Ms"] is None:
                p50 = p95 = "—"
            else:
                p50 = f"{item['pageP50Ms']['median']:.3f}"
                p95 = f"{item['pageP95Ms']['median']:.3f}"
            lines.append(
                f"| {item['model']} / channels={item['channels']} | {item['successfulPages']}/{item['failedPages']} | `{item['classification']}` | {p50} | {p95} | {item['strictStablePages']} | {item['semanticStablePages']} | `{item['maximumNumericDrift']:.9g}` |"
            )
        lines += [
            "",
            "The v5 Mobile and v6 Tiny groups are strict-stable across all three pages. The v6 Medium group contains one page rejected by the default 32-region budget; rerunning that page with `MAX_REGIONS=64` kept text and semantic contracts stable but produced three strict-contract variants and a maximum numeric drift of approximately `2.28047371e-4`. This is diagnostic evidence only; it does not define a release tolerance or promote channels=2 to a global default.",
            "",
        ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-root", type=Path, action="append", required=True)
    parser.add_argument("--diagnostic-csv", type=Path, action="append", default=[])
    parser.add_argument("--followup-csv", type=Path, action="append", default=[])
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    groups: list[dict[str, Any]] = []
    for root in args.run_root:
        root = root.resolve()
        for path in sorted(root.iterdir()):
            if path.is_dir() and (path / "run.json").is_file():
                groups.append(load_group(path))
    groups.sort(key=lambda item: (item["model"] or "", item["channels"]))
    diagnostics = [load_diagnostic(path.resolve()) for path in args.diagnostic_csv]
    followup_rows: list[dict[str, Any]] = []
    for path in args.followup_csv:
        followup_rows.extend(load_followup_rows(path.resolve()))
    report = {
        "schemaVersion": 1,
        "scope": "PP-OCR ORT CUDA channel tuning and strict/semantic contract diagnostic",
        "groups": groups,
        "diagnostics": diagnostics,
        "followupGroups": summarize_followup(followup_rows),
        "decision": "do-not-change-global-default",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.output_markdown.write_text(markdown(report), encoding="utf-8")
    print(f"groups={len(groups)} diagnostics={len(diagnostics)}")
    print(f"json={args.output_json}")
    print(f"markdown={args.output_markdown}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
