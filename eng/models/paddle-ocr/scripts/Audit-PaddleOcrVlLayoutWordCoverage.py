#!/usr/bin/env python3
"""Audit labelled word-center coverage using completed auto-layout page exports."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import string


COUNT_KEYS = ("completed", "failedOnly", "skippedOnly", "uncovered", "completedAndSkipped", "multipleCompleted")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _annotation_index(path: Path) -> dict[str, dict]:
    records: dict[str, dict] = {}
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        record = json.loads(line)
        if record.get("record_type") != "image":
            continue
        image_sha = record.get("source_provenance", {}).get("image_sha256")
        if not isinstance(image_sha, str) or len(image_sha) != 64 or any(character not in string.hexdigits for character in image_sha):
            raise ValueError(f"Annotation line {line_number} has no image SHA-256")
        if image_sha in records:
            raise ValueError(f"Duplicate annotation image SHA-256 on line {line_number}")
        records[image_sha] = record
    return records


def _only_stage(result: dict, module: str) -> dict:
    stages = [stage for stage in result.get("results", []) if stage.get("module") == module]
    if len(stages) != 1:
        raise ValueError(f"Expected one {module} stage")
    return stages[0]


def _word_center(word: dict) -> tuple[float, float]:
    points = word.get("polygon")
    if not isinstance(points, list) or len(points) < 3:
        raise ValueError("An annotated word has no valid polygon")
    coordinates = [(float(point[0]), float(point[1])) for point in points]
    if not all(math.isfinite(value) for point in coordinates for value in point):
        raise ValueError("An annotated word has non-finite coordinates")
    xs, ys = zip(*coordinates)
    if max(xs) <= min(xs) or max(ys) <= min(ys):
        raise ValueError("An annotated word has zero-area geometry")
    return (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2


def _contains(region: dict, center: tuple[float, float]) -> bool:
    bounds = region["source"]["bounds"]
    x, y = float(bounds["x"]), float(bounds["y"])
    width, height = float(bounds["width"]), float(bounds["height"])
    if not all(math.isfinite(value) for value in (x, y, width, height)) or width <= 0 or height <= 0:
        raise ValueError("A detected region has invalid bounds")
    return x <= center[0] <= x + width and y <= center[1] <= y + height


def _audit_page(path: Path, annotation_by_sha: dict[str, dict]) -> dict:
    result = json.loads(path.read_text(encoding="utf-8"))
    detector = _only_stage(result, "LayoutDetection")
    vl = _only_stage(result, "VisionLanguageParsing")
    if detector.get("metadata", {}).get("modelId") != "paddle-doc/pp-doclayout-l":
        raise ValueError("Page output does not use PP-DocLayout-L")
    source_sha = detector["metadata"]["inputSha256"]
    if vl.get("metadata", {}).get("inputSha256") != source_sha:
        raise ValueError("Layout and VL stages use different page images")
    annotation = annotation_by_sha.get(source_sha)
    if annotation is None:
        raise ValueError("No annotation matches the page input SHA-256")
    if result["page"]["sourceSize"] != {"width": annotation["width"], "height": annotation["height"]}:
        raise ValueError("Page dimensions do not match the annotation")
    payload = vl["payload"]
    if payload.get("routing") != "layout-category":
        raise ValueError("VL output is not automatically routed from layout categories")
    regions = payload["regions"]
    detections = detector["regions"]
    if len(regions) != len(detections):
        raise ValueError("Detector and VL region counts differ")
    for index, (region, detection) in enumerate(zip(regions, detections)):
        source = region["source"]
        if (region["regionIndex"] != index or source["category"] != detection["category"]
                or source["score"] != detection["score"] or source["bounds"] != detection["bounds"]):
            raise ValueError("Routed region does not match its detector source")
        if region["status"] not in ("Succeeded", "Failed", "Skipped"):
            raise ValueError("Unknown routed region status")
        if region["status"] == "Succeeded" and not isinstance(region.get("result"), dict):
            raise ValueError("A successful region has no generation result")

    counts = {key: 0 for key in COUNT_KEYS}
    per_region = [{"regionIndex": index, "category": region["source"]["category"],
                   "status": region["status"], "coveredWords": 0}
                  for index, region in enumerate(regions)]
    ignored = 0
    for word in annotation["instances"]:
        if word.get("ignore"):
            ignored += 1
            continue
        center = _word_center(word)
        hits = [index for index, region in enumerate(regions) if _contains(region, center)]
        for index in hits:
            per_region[index]["coveredWords"] += 1
        completed = [index for index in hits if regions[index]["status"] == "Succeeded"
                     and regions[index].get("result", {}).get("finishReason") == "EndOfSequence"]
        failed = [index for index in hits if regions[index]["status"] == "Failed"
                  or (regions[index]["status"] == "Succeeded" and index not in completed)]
        skipped = [index for index in hits if regions[index]["status"] == "Skipped"]
        if completed:
            counts["completed"] += 1
        elif failed:
            counts["failedOnly"] += 1
        elif skipped:
            counts["skippedOnly"] += 1
        else:
            counts["uncovered"] += 1
        counts["completedAndSkipped"] += bool(completed and skipped)
        counts["multipleCompleted"] += len(completed) > 1

    counts["words"] = len(annotation["instances"]) - ignored
    counts["ignoredWords"] = ignored
    if sum(counts[key] for key in COUNT_KEYS[:4]) != counts["words"]:
        raise AssertionError("Word coverage partition is not exhaustive")
    return {
        "pageIndex": result["page"]["pageIndex"],
        "sourceSampleId": annotation["source_sample_id"],
        "sourceImageSha256": source_sha,
        "outputName": path.name,
        "outputSha256": sha256(path),
        "detectedRegions": len(regions),
        "counts": counts,
        "regions": per_region,
    }


def audit(annotations: Path, pages: list[Path]) -> dict:
    if not pages:
        raise ValueError("At least one page output is required")
    annotation_by_sha = _annotation_index(annotations)
    results = [_audit_page(path, annotation_by_sha) for path in pages]
    if len({result["pageIndex"] for result in results}) != len(results):
        raise ValueError("Duplicate page index")
    if len({result["sourceImageSha256"] for result in results}) != len(results):
        raise ValueError("Duplicate source image")
    results.sort(key=lambda result: result["pageIndex"])
    totals = {key: sum(result["counts"][key] for result in results)
              for key in (*COUNT_KEYS, "words", "ignoredWords")}
    return {
        "schemaVersion": "deploysharp-paddleocr-vl-layout-word-coverage-v1",
        "method": "Non-ignored annotated word bbox center inside detector source bounds; completed, failed-only, skipped-only, uncovered are disjoint with that precedence. Overlap counters are additional. No text or reading-order scoring.",
        "annotationManifestSha256": sha256(annotations),
        "pageCount": len(results),
        "totals": totals,
        "pages": results,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--annotations", required=True, type=Path)
    parser.add_argument("--page", required=True, type=Path, action="append")
    parser.add_argument("--output", required=True, type=Path)
    arguments = parser.parse_args()
    report = audit(arguments.annotations, arguments.page)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
    print(f"pages={report['pageCount']} words={report['totals']['words']} "
          f"completed={report['totals']['completed']} skippedOnly={report['totals']['skippedOnly']} "
          f"failedOnly={report['totals']['failedOnly']} uncovered={report['totals']['uncovered']}")


if __name__ == "__main__":
    main()
