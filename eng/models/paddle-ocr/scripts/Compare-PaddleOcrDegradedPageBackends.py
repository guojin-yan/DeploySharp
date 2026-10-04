"""Compare two full-page degradation prediction documents by condition."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any


CONDITION_PATTERN = re.compile(r"-degraded-(?P<condition>.+?)-(?P<severity>[^-]+)$")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def condition_for(image_id: str) -> tuple[str, str]:
    match = CONDITION_PATTERN.search(image_id)
    if not match:
        raise ValueError(f"Cannot infer degradation condition from image id: {image_id}")
    return match.group("condition"), match.group("severity")


def max_polygon_difference(left: list[list[float]], right: list[list[float]]) -> float:
    if len(left) != len(right):
        return float("inf")
    values = [abs(float(a) - float(b)) for left_point, right_point in zip(left, right) for a, b in zip(left_point, right_point)]
    return max(values, default=0.0)


def compare_image(left: dict[str, Any], right: dict[str, Any]) -> dict[str, Any]:
    left_regions = left.get("regions", [])
    right_regions = right.get("regions", [])
    count_match = len(left_regions) == len(right_regions)
    text_mismatches = 0
    max_polygon = 0.0
    max_confidence = 0.0
    compared = min(len(left_regions), len(right_regions))
    for left_region, right_region in zip(left_regions, right_regions):
        if str(left_region.get("text", "")) != str(right_region.get("text", "")):
            text_mismatches += 1
        max_polygon = max(max_polygon, max_polygon_difference(left_region.get("polygon", []), right_region.get("polygon", [])))
        left_confidence = left_region.get("confidence")
        right_confidence = right_region.get("confidence")
        if left_confidence is not None and right_confidence is not None:
            max_confidence = max(max_confidence, abs(float(left_confidence) - float(right_confidence)))
    return {
        "statusMatch": str(left.get("status")) == str(right.get("status")),
        "regionCountMatch": count_match,
        "leftRegionCount": len(left_regions),
        "rightRegionCount": len(right_regions),
        "comparedRegions": compared,
        "textMismatches": text_mismatches,
        "maxPolygonAbsDiff": max_polygon,
        "maxConfidenceAbsDiff": max_confidence,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--left", type=Path, required=True)
    parser.add_argument("--right", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    left = json.loads(args.left.read_text(encoding="utf-8-sig"))
    right = json.loads(args.right.read_text(encoding="utf-8-sig"))
    left_by_id = {str(row["image_id"]): row for row in left.get("images", [])}
    right_by_id = {str(row["image_id"]): row for row in right.get("images", [])}
    if set(left_by_id) != set(right_by_id):
        raise ValueError("The two prediction documents do not contain the same image IDs.")

    groups: dict[tuple[str, str], list[str]] = {}
    for image_id in sorted(left_by_id):
        groups.setdefault(condition_for(image_id), []).append(image_id)
    conditions: dict[str, dict[str, Any]] = {}
    all_rows: list[dict[str, Any]] = []
    for (condition, severity), image_ids in sorted(groups.items()):
        rows = [compare_image(left_by_id[image_id], right_by_id[image_id]) for image_id in image_ids]
        all_rows.extend(rows)
        conditions[condition] = {
            "severity": severity,
            "images": len(rows),
            "statusMatches": sum(1 for row in rows if row["statusMatch"]),
            "regionCountMatches": sum(1 for row in rows if row["regionCountMatch"]),
            "textMismatchedImages": sum(1 for row in rows if row["textMismatches"] > 0),
            "textMismatches": sum(int(row["textMismatches"]) for row in rows),
            "comparedRegions": sum(int(row["comparedRegions"]) for row in rows),
            "maxPolygonAbsDiff": max(float(row["maxPolygonAbsDiff"]) for row in rows),
            "maxConfidenceAbsDiff": max(float(row["maxConfidenceAbsDiff"]) for row in rows),
        }

    value = {
        "schemaVersion": "deploysharp-paddleocr-full-page-degradation-parity-v1",
        "left": {"path": str(args.left.resolve()), "sha256": sha256(args.left), "backend": left.get("run_metadata", {}).get("backend")},
        "right": {"path": str(args.right.resolve()), "sha256": sha256(args.right), "backend": right.get("run_metadata", {}).get("backend")},
        "images": len(all_rows),
        "statusMatches": sum(1 for row in all_rows if row["statusMatch"]),
        "regionCountMatches": sum(1 for row in all_rows if row["regionCountMatch"]),
        "textMismatchedImages": sum(1 for row in all_rows if row["textMismatches"] > 0),
        "textMismatches": sum(int(row["textMismatches"]) for row in all_rows),
        "comparedRegions": sum(int(row["comparedRegions"]) for row in all_rows),
        "maxPolygonAbsDiff": max(float(row["maxPolygonAbsDiff"]) for row in all_rows),
        "maxConfidenceAbsDiff": max(float(row["maxConfidenceAbsDiff"]) for row in all_rows),
        "conditions": conditions,
        "interpretation": "Prediction contract comparison only. It does not establish accuracy or a production performance ranking.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# PaddleOCR full-page degradation backend parity",
        "",
        f"- Left: `{value['left']['backend']}` (`{value['left']['sha256']}`)",
        f"- Right: `{value['right']['backend']}` (`{value['right']['sha256']}`)",
        f"- Images: `{value['images']}`; status `{value['statusMatches']}/{value['images']}`; region count `{value['regionCountMatches']}/{value['images']}`",
        f"- Compared regions: `{value['comparedRegions']}`; text mismatches `{value['textMismatches']}` across `{value['textMismatchedImages']}` images",
        f"- Maximum polygon absolute difference: `{value['maxPolygonAbsDiff']}`; confidence absolute difference: `{value['maxConfidenceAbsDiff']}`",
        "",
        "| Condition | Images | Region count | Text mismatched images | Text mismatches | Max polygon diff | Max confidence diff |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for condition, summary in conditions.items():
        lines.append(f"| {condition} | {summary['images']} | {summary['regionCountMatches']}/{summary['images']} | {summary['textMismatchedImages']} | {summary['textMismatches']} | {summary['maxPolygonAbsDiff']:.8g} | {summary['maxConfidenceAbsDiff']:.8g} |")
    lines.extend(["", "This is a prediction contract comparison only; it does not establish accuracy or a production performance ranking."])
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown), "textMismatches": value["textMismatches"]}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
