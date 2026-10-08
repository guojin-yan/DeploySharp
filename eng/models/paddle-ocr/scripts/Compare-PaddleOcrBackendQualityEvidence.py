"""Compare complete-pipeline PaddleOCR quality runs across backends.

The comparator intentionally checks run provenance before comparing page and
region outputs. It reports behavior differences; it does not decide whether a
backend is accurate or suitable for release.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
from typing import Any


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def first_existing(directory: Path, names: tuple[str, ...]) -> Path:
    for name in names:
        path = directory / name
        if path.is_file():
            return path
    raise FileNotFoundError(f"None of {names!r} exists in {directory}")


def discover_runs(root: Path) -> dict[str, Path]:
    runs: dict[str, Path] = {}
    for metadata_path in sorted(root.rglob("run.json")):
        run_dir = metadata_path.parent
        metadata = read_json(metadata_path)
        model_id = str(metadata.get("model", "")).replace("/", "-").lower()
        if not model_id:
            continue
        if model_id in runs:
            raise ValueError(f"Duplicate run for {model_id} under {root}")
        runs[model_id] = run_dir
    return runs


def load_run(run_dir: Path) -> dict[str, Any]:
    metadata = read_json(run_dir / "run.json")
    predictions_path = first_existing(
        run_dir,
        ("predictions-corrected.json", "predictions-verified.json", "predictions.json"),
    )
    evaluation_path = first_existing(
        run_dir,
        ("evaluation-corrected.json", "evaluation-verified.json", "evaluation.json"),
    )
    predictions = read_json(predictions_path)
    evaluation = read_json(evaluation_path)
    width_files = sorted((run_dir / "raw").rglob("*.width.json"))
    width_sample = read_json(width_files[0]) if width_files else {}
    return {
        "directory": run_dir,
        "metadata": metadata,
        "predictions_path": predictions_path,
        "evaluation_path": evaluation_path,
        "predictions": predictions,
        "evaluation": evaluation,
        "widthSample": width_sample,
    }


def model_assets(run: dict[str, Any]) -> dict[str, Any]:
    model = run["predictions"].get("model", {})
    assets = {
        "detector": model.get("detector_sha256"),
        "recognizer": model.get("recognizer_sha256"),
        "classifier": model.get("classifier_sha256"),
    }
    # The prediction document intentionally omits the dictionary path. The
    # width report carries a stable character-set digest for the same session.
    regions = run.get("widthSample", {}).get("Regions", [])
    dictionary_hashes = sorted({
        str(region.get("CharacterSetSha256"))
        for region in regions
        if region.get("CharacterSetSha256")
    })
    assets["dictionary"] = dictionary_hashes[0] if len(dictionary_hashes) == 1 else dictionary_hashes
    return assets


def numeric_max_delta(left: Any, right: Any) -> float | None:
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        if not math.isfinite(float(left)) or not math.isfinite(float(right)):
            return None
        return abs(float(left) - float(right))
    if not isinstance(left, list) or not isinstance(right, list) or len(left) != len(right):
        return None
    deltas: list[float] = []
    for a, b in zip(left, right):
        if isinstance(a, list) or isinstance(b, list):
            nested = numeric_max_delta(a, b)
            if nested is None:
                return None
            deltas.append(nested)
        elif isinstance(a, (int, float)) and isinstance(b, (int, float)):
            if not math.isfinite(float(a)) or not math.isfinite(float(b)):
                return None
            deltas.append(abs(float(a) - float(b)))
        else:
            return None
    return max(deltas, default=0.0)


def text_digest(regions: list[dict[str, Any]]) -> str:
    text = "\u001f".join(str(region.get("text", "")) for region in regions)
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def metric_summary(run: dict[str, Any]) -> dict[str, Any]:
    evaluation = run["evaluation"]
    det = evaluation.get("det", {}).get("0.5", {})
    rec = evaluation.get("rec", {})
    pipeline = evaluation.get("pipeline", {})
    latency = evaluation.get("latency_ms", {}).get("total", {})
    images = run["predictions"].get("images", [])
    return {
        "pages": len(images),
        "pageFailures": sum(image.get("status") != "ok" for image in images),
        "emptyPages": sum(not image.get("regions") for image in images if image.get("status") == "ok"),
        "detIou05": {key: det.get(key) for key in ("tp", "fp", "fn", "f1_hmean")},
        "matchedCer": rec.get("cer"),
        "matchedWer": rec.get("matched_region_wer"),
        "endToEndCer": pipeline.get("end_to_end_cer"),
        "endToEndWer": pipeline.get("end_to_end_wer"),
        "totalLatencyMs": {key: latency.get(key) for key in ("p50", "p95")},
    }


def compare_pair(model_id: str, reference: dict[str, Any], candidate: dict[str, Any]) -> dict[str, Any]:
    ref_meta = reference["metadata"]
    cand_meta = candidate["metadata"]
    ref_assets = model_assets(reference)
    cand_assets = model_assets(candidate)
    protocol_fields = (
        "sourceRevision",
        "benchmarkAssemblySha256",
        "manifestSha256",
        "selectedManifestSha256",
        "selectedImageCount",
        "warmup",
        "iterations",
        "batchSize",
        "inferenceChannels",
        "overflowMode",
        "windowOverlap",
        "maximumWindowsPerRegion",
        "pipelineTimeoutMs",
    )
    protocol_differences = {
        field: {"reference": ref_meta.get(field), "candidate": cand_meta.get(field)}
        for field in protocol_fields
        if ref_meta.get(field) != cand_meta.get(field)
    }

    ref_images = {image["image_id"]: image for image in reference["predictions"].get("images", [])}
    cand_images = {image["image_id"]: image for image in candidate["predictions"].get("images", [])}
    page_ids = sorted(set(ref_images) | set(cand_images))
    page_results: list[dict[str, Any]] = []
    sequence_matches = 0
    region_count_matches = 0
    ordered_region_text_matches = 0
    total_reference_regions = 0
    total_candidate_regions = 0
    total_region_text_mismatches = 0
    max_polygon_delta: float | None = 0.0
    max_confidence_delta: float | None = 0.0

    for page_id in page_ids:
        ref_image = ref_images.get(page_id)
        cand_image = cand_images.get(page_id)
        if ref_image is None or cand_image is None:
            page_results.append({"imageId": page_id, "status": "missing-page"})
            max_polygon_delta = None
            max_confidence_delta = None
            continue
        ref_regions = ref_image.get("regions", [])
        cand_regions = cand_image.get("regions", [])
        total_reference_regions += len(ref_regions)
        total_candidate_regions += len(cand_regions)
        same_count = len(ref_regions) == len(cand_regions)
        if same_count:
            region_count_matches += 1
        same_sequence = text_digest(ref_regions) == text_digest(cand_regions)
        if same_sequence:
            sequence_matches += 1
            ordered_region_text_matches += 1
        mismatches = sum(
            left.get("text") != right.get("text")
            for left, right in zip(ref_regions, cand_regions)
        ) + abs(len(ref_regions) - len(cand_regions))
        total_region_text_mismatches += mismatches

        page_polygon_delta: float | None = 0.0
        page_confidence_delta: float | None = 0.0
        if not same_count:
            page_polygon_delta = None
            page_confidence_delta = None
        else:
            for left, right in zip(ref_regions, cand_regions):
                polygon_delta = numeric_max_delta(left.get("polygon"), right.get("polygon"))
                confidence_delta = numeric_max_delta(left.get("confidence"), right.get("confidence"))
                if polygon_delta is None:
                    page_polygon_delta = None
                elif page_polygon_delta is not None:
                    page_polygon_delta = max(page_polygon_delta, polygon_delta)
                if confidence_delta is None:
                    page_confidence_delta = None
                elif page_confidence_delta is not None:
                    page_confidence_delta = max(page_confidence_delta, confidence_delta)
        if page_polygon_delta is None:
            max_polygon_delta = None
        elif max_polygon_delta is not None:
            max_polygon_delta = max(max_polygon_delta, page_polygon_delta)
        if page_confidence_delta is None:
            max_confidence_delta = None
        elif max_confidence_delta is not None:
            max_confidence_delta = max(max_confidence_delta, page_confidence_delta)

        page_results.append({
            "imageId": page_id,
            "referenceStatus": ref_image.get("status"),
            "candidateStatus": cand_image.get("status"),
            "referenceRegions": len(ref_regions),
            "candidateRegions": len(cand_regions),
            "regionCountMatch": same_count,
            "orderedTextSequenceMatch": same_sequence,
            "indexedRegionTextMismatches": mismatches,
            "maxIndexedPolygonAbsDeltaPx": page_polygon_delta,
            "maxIndexedConfidenceAbsDelta": page_confidence_delta,
        })

    common_assets = ref_assets == cand_assets
    reference_metrics = metric_summary(reference)
    candidate_metrics = metric_summary(candidate)
    return {
        "model": model_id,
        "referenceBackend": ref_meta.get("backend"),
        "candidateBackend": cand_meta.get("backend"),
        "referenceDirectory": str(reference["directory"]),
        "candidateDirectory": str(candidate["directory"]),
        "protocolCompatible": not protocol_differences,
        "protocolDifferences": protocol_differences,
        "modelAssetsMatch": common_assets,
        "modelAssetsSha256": {"reference": ref_assets, "candidate": cand_assets},
        "referenceMetrics": reference_metrics,
        "candidateMetrics": candidate_metrics,
        "pageComparison": {
            "pagesCompared": len(page_ids),
            "orderedTextSequenceMatches": sequence_matches,
            "regionCountMatches": region_count_matches,
            "orderedRegionTextMatchPages": ordered_region_text_matches,
            "referenceRegions": total_reference_regions,
            "candidateRegions": total_candidate_regions,
            "indexedRegionTextMismatches": total_region_text_mismatches,
            "maxIndexedPolygonAbsDeltaPx": max_polygon_delta,
            "maxIndexedConfidenceAbsDelta": max_confidence_delta,
            "alignmentNote": "Coordinate and confidence deltas are index-aligned only when page region counts match; this does not perform geometric rematching.",
        },
        "pages": page_results,
    }


def markdown_report(report: dict[str, Any]) -> str:
    reference_backend = str(report.get("referenceBackend") or "unknown")
    candidate_backends = ", ".join(str(value) for value in report.get("candidateBackends", [])) or "unknown"
    reproduction = report.get("reproduction", {})
    command = [
        "uv run --offline python .\\eng\\models\\paddle-ocr\\scripts\\Compare-PaddleOcrBackendQualityEvidence.py `",
        f"  --reference-root {reproduction.get('referenceRoot', '<reference-run-root>')} `",
    ]
    candidate_roots = reproduction.get("candidateRoots", [])
    if candidate_roots:
        command.extend(
            f"  --candidate-root {root} `"
            for root in candidate_roots
        )
    else:
        command.append("  --candidate-root <candidate-run-root> `")
    command.extend((
        f"  --output-json {reproduction.get('outputJson', '<output.json>')} `",
        f"  --output-markdown {reproduction.get('outputMarkdown', '<output.md>')}",
    ))
    lines = [
        "# PP-OCR core-model backend quality comparison (2026-10-08)",
        "",
        "This report compares complete DET → optional CLS → REC pipeline outputs on the same ten HierText `sample-002` pages. It is smoke evidence, not a release accuracy score or formal performance benchmark. Each page has one warm-up and one measured run.",
        "",
        f"- Machine: `{report['runtime']['machine']}` / `{report['runtime']['operatingSystem']}` / `{report['runtime']['processorArchitecture']}`.",
        f"- Reference backend: `{reference_backend}`; candidate backend(s): `{candidate_backends}`.",
        f"- Models: {report['summary']['models']}; backend comparisons: {report['summary']['comparisons']}; page runs: {report['summary']['successfulPageRuns']}/{report['summary']['expectedPageRuns']} successful.",
        f"- Selection SHA-256: `{report['dataset']['selectedManifestSha256']}`; model, image, evaluator and assembly provenance are preserved in the JSON evidence.",
        "- The dataset remains local smoke data pending image-specific redistribution review. No images, annotations, or predictions are included in the repository.",
        "",
        "## Results",
        "",
        "| Model | Backend | Pages | Det F1 | Matched CER/WER | End-to-end CER/WER | Text pages vs reference | Regions | Text mismatches | Max indexed polygon drift (px) | Max confidence drift | One-shot total P50/P95 (ms) |",
        "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for item in report["comparisons"]:
        metrics = item["candidateMetrics"]
        page = item["pageComparison"]
        f1 = metrics["detIou05"].get("f1_hmean")
        f1_text = f"{100*f1:.2f}%" if f1 is not None else "n/a"
        matched = f"{100*metrics['matchedCer']:.2f}% / {100*metrics['matchedWer']:.2f}%"
        end_to_end = f"{100*metrics['endToEndCer']:.2f}% / {100*metrics['endToEndWer']:.2f}%"
        polygon_delta = page["maxIndexedPolygonAbsDeltaPx"]
        confidence_delta = page["maxIndexedConfidenceAbsDelta"]
        polygon_text = "n/a (region count differs)" if polygon_delta is None else f"{polygon_delta:.6g}"
        confidence_text = "n/a (region count differs)" if confidence_delta is None else f"{confidence_delta:.6g}"
        latency = metrics["totalLatencyMs"]
        latency_text = f"{latency['p50']:.2f} / {latency['p95']:.2f}" if latency["p50"] is not None and latency["p95"] is not None else "n/a"
        lines.append(
            f"| {item['model']} | {item['candidateBackend']} | {metrics['pages'] - metrics['pageFailures']}/{metrics['pages']} | {f1_text} | {matched} | {end_to_end} | {page['orderedTextSequenceMatches']}/{page['pagesCompared']} | {page['referenceRegions']} / {page['candidateRegions']} | {page['indexedRegionTextMismatches']} | {polygon_text} | {confidence_text} | {latency_text} |"
        )
    lines += [
        "",
        "## Interpretation and limits",
        "",
        f"- The table reports each candidate backend relative to `{reference_backend}` for the exact model assets and ten-page selection. A matching aggregate CER/F1 does not imply identical region outputs.",
        "- Polygon and confidence drift are indexed comparisons only, and are omitted when a page has different region counts. They are not IoU-based region reassociation.",
        "- End-to-end CER/WER can exceed 100% because missed labels count as deletions and unmatched predictions as insertions. Matched-crop CER alone overstates complete-page quality.",
        "- Latency values in the JSON are one-shot observations across different pages; they are not 5-warmup/50-iteration performance results and must not be used as a backend ranking.",
        "- This result covers only the backends named above. It does not promote untested backend/model combinations, and it does not close the formal P2 quality gate or the 5-warmup/50-iteration performance matrix.",
        "",
        "## Reproduction",
        "",
        "Run the quality evaluations first with the same selected manifest and protocol, then compare their local run directories. The script refuses to compare different model hashes, selected manifests, source revisions, assemblies or run protocols.",
        "",
        "```powershell",
        *command,
        "```",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-root", required=True, type=Path)
    parser.add_argument("--candidate-root", required=True, type=Path, action="append")
    parser.add_argument("--output-json", required=True, type=Path)
    parser.add_argument("--output-markdown", required=True, type=Path)
    args = parser.parse_args()

    reference_runs = discover_runs(args.reference_root.resolve())
    if not reference_runs:
        raise FileNotFoundError(f"No run.json files found under {args.reference_root}")
    candidates = [(root.resolve(), discover_runs(root.resolve())) for root in args.candidate_root]
    comparisons: list[dict[str, Any]] = []
    backends: set[str] = set()
    for root, candidate_runs in candidates:
        missing = sorted(set(reference_runs) - set(candidate_runs))
        extra = sorted(set(candidate_runs) - set(reference_runs))
        if missing or extra:
            raise ValueError(f"Model set differs for {root}: missing={missing}, extra={extra}")
        for model_id in sorted(reference_runs):
            reference = load_run(reference_runs[model_id])
            candidate = load_run(candidate_runs[model_id])
            comparison = compare_pair(model_id, reference, candidate)
            if not comparison["protocolCompatible"]:
                raise ValueError(f"Protocol mismatch for {model_id} vs {comparison['candidateBackend']}: {comparison['protocolDifferences']}")
            if not comparison["modelAssetsMatch"]:
                raise ValueError(f"Model asset hash mismatch for {model_id} vs {comparison['candidateBackend']}")
            expected_backend = str(candidate["metadata"].get("backend", "unknown"))
            backends.add(expected_backend)
            comparisons.append(comparison)

    reference_run_dir = next(iter(reference_runs.values()))
    reference_meta = read_json(reference_run_dir / "run.json")
    reference_prediction = read_json(first_existing(reference_run_dir, ("predictions-corrected.json", "predictions-verified.json", "predictions.json")))
    image_ids = [image.get("image_id") for image in reference_prediction.get("images", [])]
    selected_manifest_path = Path(str(reference_meta.get("selectedManifest", "")))
    manifest_images: list[dict[str, Any]] = []
    if selected_manifest_path.is_file():
        for line in selected_manifest_path.read_text(encoding="utf-8-sig").splitlines():
            if not line.strip():
                continue
            record = json.loads(line)
            manifest_images.append({
                "imageId": record.get("image_id"),
                "imageSha256": record.get("source_image_sha256") or record.get("image_sha256"),
            })
        if [item["imageId"] for item in manifest_images] != image_ids:
            raise ValueError("Selected manifest image order does not match the reference prediction document")
    all_page_runs = sum(item["candidateMetrics"]["pages"] for item in comparisons)
    all_page_failures = sum(item["candidateMetrics"]["pageFailures"] for item in comparisons)
    report = {
        "schemaVersion": 1,
        "scope": "PP-OCR v4/v5/v6 complete-pipeline backend quality comparison",
        "referenceBackend": reference_meta.get("backend"),
        "candidateBackends": sorted(backends),
        "runtime": {
            "machine": reference_meta.get("machine"),
            "operatingSystem": reference_meta.get("operatingSystem"),
            "processorArchitecture": reference_meta.get("processArchitecture"),
            "processorCount": reference_meta.get("processorCount"),
        },
        "dataset": {
            "sourceManifestSha256": reference_meta.get("manifestSha256"),
            "selectedManifestSha256": reference_meta.get("selectedManifestSha256"),
            "imageCount": reference_meta.get("selectedImageCount"),
            "imageIds": image_ids,
            "images": manifest_images,
            "redistributionBoundary": "Smoke-only pending per-image source review; no dataset assets or predictions are committed.",
        },
        "protocol": {
            "pipeline": "det -> crop -> optional cls -> rec -> merge",
            "warmupPerPage": reference_meta.get("warmup"),
            "measuredIterationsPerPage": reference_meta.get("iterations"),
            "recognitionBatchSize": reference_meta.get("batchSize"),
            "inferenceChannels": reference_meta.get("inferenceChannels"),
            "overflowMode": reference_meta.get("overflowMode"),
            "benchmarkAssemblySha256": reference_meta.get("benchmarkAssemblySha256"),
            "sourceRevision": reference_meta.get("sourceRevision"),
            "performanceBoundary": "One measured iteration across ten distinct pages; not the formal 5-warmup/50-iteration benchmark.",
        },
        "summary": {
            "models": len(reference_runs),
            "comparisons": len(comparisons),
            "expectedPageRuns": all_page_runs,
            "successfulPageRuns": all_page_runs - all_page_failures,
            "failedPageRuns": all_page_failures,
            "outputMismatches": sum(item["pageComparison"]["indexedRegionTextMismatches"] for item in comparisons),
            "candidateBackends": sorted(backends),
        },
        "reproduction": {
            "referenceRoot": str(args.reference_root),
            "candidateRoots": [str(root) for root in args.candidate_root],
            "outputJson": str(args.output_json),
            "outputMarkdown": str(args.output_markdown),
        },
        "comparisons": comparisons,
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    args.output_markdown.write_text(markdown_report(report), encoding="utf-8")
    print(json.dumps(report["summary"], ensure_ascii=False, indent=2))
    print(f"JSON={args.output_json.resolve()}")
    print(f"MARKDOWN={args.output_markdown.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
