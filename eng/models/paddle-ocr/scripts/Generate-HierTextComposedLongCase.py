"""Compose a >3200-character OCR line from real HierText text-line crops."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--characters", type=int, default=3600)
    parser.add_argument("--target-height", type=int, default=48)
    parser.add_argument("--gap", type=int, default=8)
    args = parser.parse_args()
    if args.characters < 3201 or args.characters > 12000:
        raise SystemExit("characters must be between 3201 and 12000")
    if args.target_height < 24 or args.target_height > 128 or args.gap < 0 or args.gap > 64:
        raise SystemExit("invalid target-height or gap")

    annotations = {}
    for name in ("hiertext-validation-sample-002.jsonl", "hiertext-validation-sample-003.jsonl"):
        path = args.dataset_root / "data" / "annotations" / "manifests" / name
        for line in path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            record = json.loads(line)
            for instance in record.get("instances", []):
                annotations[instance["instance_id"]] = instance

    crop_manifests = sorted((args.dataset_root / "data" / "crops" / "hiertext").glob("**/manifest.jsonl"))
    selected = []
    seen = set()
    total = 0
    for manifest in crop_manifests:
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            crop = json.loads(line)
            instance = annotations.get(crop.get("parent_instance_id"))
            text = (instance or {}).get("text", "")
            if len(text) < 16 or crop.get("crop_id") in seen:
                continue
            source_path = args.dataset_root / crop["crop_relpath"]
            if not source_path.is_file():
                continue
            image = cv2.imread(str(source_path), cv2.IMREAD_COLOR)
            if image is None:
                continue
            seen.add(crop["crop_id"])
            selected.append((crop, instance, source_path, image))
            total += len(text) + (1 if len(selected) > 1 else 0)
            if total >= args.characters:
                break
        if total >= args.characters:
            break
    if total < args.characters or len(selected) < 8:
        raise SystemExit(f"only {total} attributable characters were available")

    target_height = args.target_height
    widths = []
    images = []
    for crop, instance, source_path, image in selected:
        height, width = image.shape[:2]
        scaled_width = max(1, int(round(width * target_height / height)))
        resized = cv2.resize(image, (scaled_width, target_height), interpolation=cv2.INTER_LINEAR)
        images.append(resized)
        widths.append(scaled_width)
    output_width = sum(widths) + args.gap * (len(images) - 1)
    canvas = np.full((target_height, output_width, 3), 255, dtype=np.uint8)
    segments = []
    x = 0
    expected_parts = []
    char_offset = 0
    for index, ((crop, instance, source_path, _), image, width) in enumerate(zip(selected, images, widths)):
        canvas[:, x:x + width] = image
        text = instance["text"]
        if expected_parts:
            char_offset += 1
        start = char_offset
        expected_parts.append(text)
        char_offset += len(text)
        segments.append({
            "index": index,
            "cropId": crop["crop_id"],
            "parentImageId": crop["parent_image_id"],
            "sourceInstanceId": crop["parent_instance_id"],
            "text": text,
            "expectedCharStart": start,
            "expectedCharEnd": char_offset,
            "sourceCropRelpath": crop["crop_relpath"],
            "sourceCropSha256": crop["crop_sha256"],
            "sourceImageSha256": sha(args.dataset_root / "data" / "images" / "hiertext" / "validation" / (crop["parent_image_id"] + ".jpg")) if (args.dataset_root / "data" / "images" / "hiertext" / "validation" / (crop["parent_image_id"] + ".jpg")).is_file() else None,
            "xStart": x,
            "xEnd": x + width
        })
        x += width + args.gap
    expected = " ".join(expected_parts)
    if len(expected) < args.characters:
        raise SystemExit(f"selected text has {len(expected)} characters, below requested {args.characters}")

    root = args.output_root.resolve()
    image_path = root / "data" / "images" / "synthetic" / "hiertext-composed-long.png"
    manifest_path = root / "data" / "annotations" / "manifests" / "hiertext-composed-long.jsonl"
    image_path.parent.mkdir(parents=True, exist_ok=True); manifest_path.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(image_path), canvas)
    record = {
        "schema_version": "1.0",
        "record_type": "composed_long_text",
        "image_id": "hiertext-composed-long-3600",
        "image_relpath": str(image_path.relative_to(root)).replace("\\", "/"),
        "width": int(output_width), "height": int(target_height),
        "expected_text": expected,
        "source_dataset": "google-research-datasets/hiertext",
        "source_split": "validation",
        "source_revision": "70b6620b2b112597d8219e11eee9773a1403827c",
        "license_scope": "parent-linked source crops; local composed smoke only",
        "benchmark_eligibility": "controlled_attributable_composition_only",
        "image_sha256": sha(image_path),
        "segments": segments,
        "composition": {"targetHeight": target_height, "gap": args.gap, "segmentCount": len(segments), "expectedCharacters": len(expected)}
    }
    manifest_path.write_text(json.dumps(record, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"image": str(image_path), "manifest": str(manifest_path), "characters": len(expected), "segments": len(segments), "imageSha256": record["image_sha256"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
