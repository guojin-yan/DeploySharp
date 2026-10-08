"""Summarize complete-pipeline PaddleOCR performance runs without inventing percentiles.

The benchmark writes one CSV per input page.  Each row contains P50/P95 over
the requested repeated iterations for that page.  This tool reports those
page-level percentiles verbatim and summarizes their distribution across the
selected pages; it deliberately does not call a median of page P95 values an
aggregate dataset P95.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import platform
import statistics
from pathlib import Path
from typing import Any


EXPECTED_MODELS = (
    "v4-mobile",
    "v4-server",
    "v5-mobile",
    "v5-server",
    "v6-medium",
    "v6-small",
    "v6-tiny",
)


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def number(row: dict[str, str], key: str) -> float:
    value = row.get(key, "")
    if value in (None, ""):
        raise ValueError(f"Missing numeric column {key!r} in {row}")
    result = float(value)
    if not math.isfinite(result):
        raise ValueError(f"Non-finite value for {key!r}: {value!r}")
    return result


def percentile(values: list[float], fraction: float) -> float:
    if not values:
        raise ValueError("Cannot summarize an empty list")
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return ordered[lower]
    weight = position - lower
    return ordered[lower] + (ordered[upper] - ordered[lower]) * weight


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def summarize_values(values: list[float]) -> dict[str, float]:
    return {
        "min": min(values),
        "median": statistics.median(values),
        "max": max(values),
        "p25": percentile(values, 0.25),
        "p75": percentile(values, 0.75),
    }


def load_model(run_dir: Path) -> dict[str, Any]:
    run_metadata = read_json(run_dir / "run.json")
    model_id = f"{run_metadata.get('model', '').replace('/', '-').lower()}"
    csv_dir = run_dir / "csv"
    csv_paths = sorted(csv_dir.glob("*.csv"))
    if not csv_paths:
        raise FileNotFoundError(f"No page CSV files under {csv_dir}")
    rows: list[dict[str, str]] = []
    for path in csv_paths:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            page_rows = list(csv.DictReader(stream))
        if len(page_rows) != 1:
            raise ValueError(f"Expected one row in {path}, found {len(page_rows)}")
        row = page_rows[0]
        if row.get("status") != "pass":
            raise ValueError(f"Non-passing page row in {path}: {row.get('status')}")
        rows.append(row)

    def series(key: str) -> list[float]:
        return [number(row, key) for row in rows]

    page_p50 = series("total_p50_ms")
    page_p95 = series("total_p95_ms")
    page_mean = series("total_ms")
    stages = {
        key: summarize_values(series(key))
        for key in (
            "preprocess_ms",
            "detection_ms",
            "detection_inference_ms",
            "detection_postprocess_ms",
            "crop_ms",
            "orientation_ms",
            "recognition_ms",
            "recognition_prepare_work_ms",
            "recognition_inference_work_ms",
            "recognition_postprocess_work_ms",
            "merge_ms",
            "total_ms",
        )
    }
    return {
        "model": model_id,
        "runDirectory": str(run_dir),
        "pageCount": len(rows),
        "successfulPages": len(rows),
        "backend": run_metadata.get("backend"),
        "device": rows[0].get("device"),
        "batchSize": int(run_metadata["batchSize"]),
        "inferenceChannels": int(run_metadata["inferenceChannels"]),
        "warmup": int(run_metadata["warmup"]),
        "iterations": int(run_metadata["iterations"]),
        "pageP50Ms": summarize_values(page_p50),
        "pageP95Ms": summarize_values(page_p95),
        "pageMeanMs": summarize_values(page_mean),
        "stagesMs": stages,
        "pageRows": [
            {
                "csv": str(path),
                "image": row.get("image_path"),
                "regions": int(row.get("regions", "0")),
                "totalMeanMs": number(row, "total_ms"),
                "totalP50Ms": number(row, "total_p50_ms"),
                "totalP95Ms": number(row, "total_p95_ms"),
                "resultTextSha256": row.get("result_text_sha256"),
                "resultContractSha256": row.get("result_contract_sha256"),
            }
            for path, row in zip(csv_paths, rows)
        ],
    }


def markdown(report: dict[str, Any]) -> str:
    runtime = report["runtime"]
    protocol = report["protocol"]
    lines = [
        "# PaddleOCR core seven-model ORT CUDA performance matrix (2026-10-08)",
        "",
        "This is a fixed-device complete-pipeline performance record for PP-OCR v4/v5/v6. Every selected page uses five warm-up iterations and fifty measured iterations with batch=16 and one stage channel. The benchmark excludes model-load time.",
        "",
        f"- Machine: `{runtime['machine']}` / `{runtime['operatingSystem']}` / `{runtime['processorArchitecture']}` / {runtime['processorCount']} logical CPUs.",
        f"- Backend/device: `{protocol['backend']}` / `{protocol['device']}`; model pages: `{report['summary']['successfulPages']}/{report['summary']['expectedPages']}` successful.",
        f"- Protocol: warmup `{protocol['warmup']}`, measured iterations `{protocol['iterations']}`, batch `{protocol['batchSize']}`, inference channels `{protocol['inferenceChannels']}`, overflow `{protocol['overflowMode']}`, window overlap `{protocol['windowOverlap']}`.",
        f"- Source revision: `{protocol['sourceRevision']}`; benchmark assembly SHA-256: `{protocol['benchmarkAssemblySha256']}`; selected manifest SHA-256: `{protocol['selectedManifestSha256']}`.",
        "- P50/P95 columns are page-level percentiles over the fifty repetitions of each page. The model-level `median page P50` and `median page P95` columns summarize ten page-level percentiles; they are not a pooled dataset percentile.",
        "- No GPU clock/power/frequency telemetry was captured by this runner. These numbers must not be described as a locked-clock or device-limit result.",
        "",
        "## Summary",
        "",
        "| Model | Pages | Batch | Channels | Median page P50 (ms) | Page P50 range (ms) | Median page P95 (ms) | Page P95 range (ms) | Median page mean (ms) | Preprocess median (ms) | Detection median (ms) | Recognition median (ms) |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for item in report["models"]:
        p50 = item["pageP50Ms"]
        p95 = item["pageP95Ms"]
        stages = item["stagesMs"]
        lines.append(
            f"| {item['model']} | {item['successfulPages']}/{item['pageCount']} | {item['batchSize']} | {item['inferenceChannels']} | {p50['median']:.3f} | {p50['min']:.3f}–{p50['max']:.3f} | {p95['median']:.3f} | {p95['min']:.3f}–{p95['max']:.3f} | {item['pageMeanMs']['median']:.3f} | {stages['preprocess_ms']['median']:.3f} | {stages['detection_ms']['median']:.3f} | {stages['recognition_ms']['median']:.3f} |"
        )
    lines += [
        "",
        "## Interpretation and limits",
        "",
        "- This matrix measures the DeploySharp full page path (`decode/preprocess → DET → crop → optional CLS → REC → merge`) on one Windows host and one ORT CUDA provider configuration. It is not a cross-device ranking.",
        "- Recognition and crop work depend on the number and width of detected regions. Compare models only with the page-level region counts and the raw CSVs available in the local run directory.",
        "- CUDA quality differences versus ORT CPU are reported separately in [the seven-model quality comparison](paddleocr-core-seven-models-ort-cuda-sample-002-quality-20261008.md). This matrix does not imply CPU/CUDA output equivalence, TensorRT support, or PP-Structure support.",
        "- The raw run directories are intentionally not committed because they contain local dataset paths, images and per-page outputs. The JSON report preserves the protocol and per-page provenance needed to reproduce it.",
        "",
        "## Reproduction",
        "",
        "```powershell",
        "pwsh -NoProfile -File .\\eng\\models\\paddle-ocr\\scripts\\Invoke-PaddleOcrPublicDataset.ps1 `",
        "  -ManifestPath 'F:\\OCRBenchmarkTesting\\data\\annotations\\manifests\\hiertext-validation-sample-002.jsonl' `",
        "  -DatasetRoot 'F:\\OCRBenchmarkTesting' -Version v6 -Variant tiny `",
        "  -Backend onnxruntime-cuda -MaxImages 10 -Warmup 5 -Iterations 50 `",
        "  -BatchSize 16 -InferenceChannels 1 `",
        "  -OutputDirectory 'artifacts/public-ocr-evaluation/cuda-seven-models-performance-sample-002-20261008/v6-tiny'",
        "",
        "uv run --offline python .\\eng\\models\\paddle-ocr\\scripts\\Summarize-PaddleOcrPerformanceMatrix.py `",
        "  --run-root .\\artifacts\\public-ocr-evaluation\\cuda-seven-models-performance-sample-002-20261008 `",
        "  --output-json .\\eng\\models\\paddle-ocr\\verification\\paddleocr-core-seven-models-ort-cuda-performance-20261008.json `",
        "  --output-markdown .\\eng\\models\\paddle-ocr\\verification\\paddleocr-core-seven-models-ort-cuda-performance-20261008.md",
        "```",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-root", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    run_root = args.run_root.resolve()
    run_dirs = {path.name: path for path in run_root.iterdir() if path.is_dir() and (path / "run.json").is_file()}
    missing = sorted(set(EXPECTED_MODELS) - set(run_dirs))
    extra = sorted(set(run_dirs) - set(EXPECTED_MODELS))
    if missing or extra:
        raise ValueError(f"Expected exactly the seven core models; missing={missing}, extra={extra}")
    models = [load_model(run_dirs[name]) for name in EXPECTED_MODELS]
    reference = read_json(run_dirs[EXPECTED_MODELS[0]] / "run.json")
    protocol_fields = (
        "backend",
        "warmup",
        "iterations",
        "batchSize",
        "inferenceChannels",
        "overflowMode",
        "windowOverlap",
        "maximumWindowsPerRegion",
        "pipelineTimeoutMs",
        "sourceRevision",
        "benchmarkAssemblySha256",
        "selectedManifestSha256",
        "machine",
        "operatingSystem",
        "processArchitecture",
        "processorCount",
    )
    for item in models:
        metadata = read_json(Path(item["runDirectory"]) / "run.json")
        differences = {
            key: (reference.get(key), metadata.get(key))
            for key in protocol_fields
            if reference.get(key) != metadata.get(key)
        }
        if differences:
            raise ValueError(f"Protocol/runtime mismatch for {item['model']}: {differences}")
    report = {
        "schemaVersion": 1,
        "scope": "PP-OCR v4/v5/v6 complete-pipeline ORT CUDA fixed-device performance matrix",
        "runtime": {
            "machine": reference.get("machine"),
            "operatingSystem": reference.get("operatingSystem"),
            "processorArchitecture": reference.get("processArchitecture"),
            "processorCount": reference.get("processorCount"),
            "python": platform.python_version(),
        },
        "protocol": {
            "backend": reference.get("backend"),
            "device": "cuda",
            "warmup": reference.get("warmup"),
            "iterations": reference.get("iterations"),
            "batchSize": reference.get("batchSize"),
            "inferenceChannels": reference.get("inferenceChannels"),
            "overflowMode": reference.get("overflowMode"),
            "windowOverlap": reference.get("windowOverlap"),
            "maximumWindowsPerRegion": reference.get("maximumWindowsPerRegion"),
            "pipelineTimeoutMs": reference.get("pipelineTimeoutMs"),
            "sourceRevision": reference.get("sourceRevision"),
            "benchmarkAssemblySha256": reference.get("benchmarkAssemblySha256"),
            "selectedManifestSha256": reference.get("selectedManifestSha256"),
        },
        "summary": {
            "models": len(models),
            "expectedPages": sum(item["pageCount"] for item in models),
            "successfulPages": sum(item["successfulPages"] for item in models),
            "failedPages": 0,
            "gpuTelemetryCaptured": False,
        },
        "models": models,
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.output_markdown.write_text(markdown(report), encoding="utf-8")
    print(json.dumps(report["summary"], ensure_ascii=False, indent=2))
    print(f"JSON={args.output_json.resolve()}")
    print(f"MARKDOWN={args.output_markdown.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
