#!/usr/bin/env python3
"""Select a fixed, prediction-blind HierText train wide-line holdout.

Run with ``uv run --no-project --with ijson --python 3.12 python``. The
annotation and selected source images stay outside the repository.
"""

import argparse
import gzip
import hashlib
import json
import math
import re
from pathlib import Path

import ijson


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--annotations", required=True, type=Path)
    parser.add_argument("--expected-sha256", required=True)
    parser.add_argument("--image-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--count", type=int, default=12)
    parser.add_argument("--keys", help="Comma-separated exact image/paragraph/line keys for a prediction-blind challenge cohort")
    args = parser.parse_args()
    if args.count < 1 or args.count > 64:
        parser.error("--count must be between 1 and 64")
    if not re.fullmatch(r"[0-9a-f]{64}", args.expected_sha256):
        parser.error("--expected-sha256 must be a lowercase SHA-256 digest")
    revision = re.fullmatch(r"train-([0-9a-f]{40})\.jsonl\.gz", args.annotations.name)
    if revision is None:
        parser.error("annotation filename must pin a 40-hex train revision")
    requested_keys = None
    if args.keys is not None:
        keys = args.keys.split(",")
        if len(keys) != args.count or len(set(keys)) != len(keys) or any(not re.fullmatch(r"[0-9a-f]{16}/\d+/\d+", key) for key in keys):
            parser.error("--keys must contain --count unique exact image/paragraph/line keys")
        requested_keys = set(keys)
    annotation_sha = sha256(args.annotations)
    if annotation_sha != args.expected_sha256:
        raise ValueError("HierText annotation SHA-256 mismatch")

    eligible = []
    page_count = 0
    with gzip.open(args.annotations, "rb") as source:
        for page in ijson.items(source, "annotations.item"):
            page_count += 1
            image_id = page.get("image_id", "")
            if not re.fullmatch(r"[0-9a-f]{16}", image_id):
                continue
            for paragraph_index, paragraph in enumerate(page.get("paragraphs", [])):
                for line_index, line in enumerate(paragraph.get("lines", [])):
                    if not line.get("legible") or not str(line.get("text", "")).strip():
                        continue
                    vertices = line.get("vertices", [])
                    if len(vertices) != 4 or any(len(point) != 2 for point in vertices):
                        continue
                    xs = [float(point[0]) for point in vertices]
                    ys = [float(point[1]) for point in vertices]
                    width, height = max(xs) - min(xs), max(ys) - min(ys)
                    if width <= 0 or height < 2:
                        continue
                    projected = math.ceil(width * 48 / height)
                    if projected <= 3200:
                        continue
                    key = f"{image_id}/{paragraph_index}/{line_index}"
                    if requested_keys is not None and key not in requested_keys:
                        continue
                    rank = hashlib.sha256(f"deploysharp-a2-train-v1/{key}".encode()).hexdigest()
                    eligible.append((rank, key, image_id, paragraph_index, line_index,
                                     line, [coordinate for point in zip(xs, ys) for coordinate in point], width, height, projected))

    selected = []
    selected_images = set()
    if requested_keys is not None and len(eligible) != len(requested_keys):
        raise ValueError("One or more requested keys are not eligible natural wide lines")
    for _, key, image_id, paragraph_index, line_index, line, vertices, width, height, projected in sorted(eligible):
        if image_id in selected_images:
            continue
        image_path = args.image_root / f"{image_id}.jpg"
        exists = image_path.is_file()
        selected.append({
            "imageId": image_id,
            "paragraphIndex": paragraph_index,
            "lineIndex": line_index,
            "textLength": len(line["text"]),
            "projectedWidth": projected,
            "bboxWidth": width,
            "bboxHeight": height,
            "vertices": vertices,
            "imagePath": str(image_path),
            "imageExists": exists,
            "imageSizeBytes": image_path.stat().st_size if exists else None,
            "imageSha256": sha256(image_path) if exists else None,
        })
        selected_images.add(image_id)
        if len(selected) == args.count:
            break
    if len(selected) != args.count:
        raise ValueError(f"Only {len(selected)} distinct eligible images; requested {args.count}")

    with gzip.open(args.annotations, "rb") as source:
        annotation_version = next(ijson.items(source, "info.version"), "unknown")

    report = {
        "schemaVersion": "deploysharp-paddleocr-hiertext-natural-wide-a2-v1",
        "source": {
            "annotationPath": str(args.annotations.resolve()),
            "annotationSha256": annotation_sha,
            "sourceRevision": revision.group(1),
            "annotationVersion": str(annotation_version),
            "annotationCount": page_count,
            "imageRoot": str(args.image_root.resolve()),
            "split": "train",
            "imageSourceTemplate": "https://open-images-dataset.s3.amazonaws.com/ocr/train/{image_id}.jpg",
        },
        "selection": {
            "rule": "Officially legible lines, source bbox height >=2px, projected width >3200px at height 48; "
                    + ("exact preselected annotation keys; one line per image" if requested_keys is not None else "SHA-256 rank of deploysharp-a2-train-v1/<image/p/l>; one line per image"),
            "requestedLines": args.count,
            "eligibleLines": len(eligible),
            "distinctEligibleImages": len({row[2] for row in eligible}),
            "selectedKeys": [f"{row['imageId']}/{row['paragraphIndex']}/{row['lineIndex']}" for row in selected],
            "predictionBlind": True,
            **({"annotationStratified": True} if requested_keys is not None else {}),
        },
        "candidates": selected,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"TRAIN_A2_ELIGIBLE={len(eligible)}")
    print(f"TRAIN_A2_SELECTED={len(selected)}")
    print(f"TRAIN_A2_IMAGES_READY={sum(row['imageExists'] for row in selected)}")
    print(f"TRAIN_A2_REPORT={args.output.resolve()}")


if __name__ == "__main__":
    main()
