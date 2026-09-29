"""Create parent-linked known-angle variants from real annotated SROIE word crops."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-root", type=Path, required=True)
    parser.add_argument("--degradation-manifest", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--angles", default="0,12,-12,27,-27,90,-90,180")
    parser.add_argument("--margin", type=int, default=24)
    args = parser.parse_args()
    if args.margin < 8 or args.margin > 256:
        raise SystemExit("margin must be 8..256")
    angles = [float(value.strip()) for value in args.angles.split(",") if value.strip()]
    if not angles or len(angles) > 12 or any(abs(value) > 180 for value in angles):
        raise SystemExit("angles must contain 1..12 values in [-180,180]")

    root = args.output_root.resolve()
    image_root = root / "data" / "images" / "sroie-angle"
    manifest_path = root / "data" / "annotations" / "manifests" / "sroie-known-angle-ocr.jsonl"
    image_root.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)

    source_rows = []
    seen_pages: set[str] = set()
    for line in args.degradation_manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        record = json.loads(line)
        if record.get("condition") != "normal":
            continue
        parent_id = record["source_parent_image_id"]
        if parent_id in seen_pages:
            continue
        source_crop = args.dataset_root / record["source_crop_relpath"]
        image = cv2.imread(str(source_crop), cv2.IMREAD_COLOR)
        if image is None:
            raise SystemExit(f"unable to read source crop: {source_crop}")
        if digest(source_crop) != record["source_crop_sha256"]:
            raise SystemExit(f"source crop SHA mismatch: {source_crop}")
        record["_local_source_path"] = str(source_crop)
        record["_width"] = int(image.shape[1])
        record["_height"] = int(image.shape[0])
        source_rows.append(record)
        seen_pages.add(parent_id)
    if len(source_rows) < 4:
        raise SystemExit("at least four distinct annotated SROIE pages are required")

    outputs = []
    for source_index, source in enumerate(source_rows[:4]):
        source_path = Path(source.pop("_local_source_path"))
        image = cv2.imread(str(source_path), cv2.IMREAD_COLOR)
        height, width = image.shape[:2]
        padded = cv2.copyMakeBorder(image, args.margin, args.margin, args.margin, args.margin,
                                    cv2.BORDER_CONSTANT, value=(255, 255, 255))
        padded_height, padded_width = padded.shape[:2]
        source_quad = np.float32([
            [args.margin, args.margin],
            [args.margin + width, args.margin],
            [args.margin + width, args.margin + height],
            [args.margin, args.margin + height],
        ])
        for angle_index, angle in enumerate(angles):
            center = (padded_width / 2.0, padded_height / 2.0)
            matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
            radians = np.deg2rad(angle)
            cosine, sine = abs(np.cos(radians)), abs(np.sin(radians))
            output_width = int(np.ceil(padded_height * sine + padded_width * cosine))
            output_height = int(np.ceil(padded_height * cosine + padded_width * sine))
            matrix[0, 2] += output_width / 2.0 - center[0]
            matrix[1, 2] += output_height / 2.0 - center[1]
            rotated = cv2.warpAffine(padded, matrix, (output_width, output_height),
                                     flags=cv2.INTER_LINEAR, borderValue=(255, 255, 255))
            quad = cv2.transform(source_quad[None, :, :], matrix)[0]
            relative = Path("data/images/sroie-angle") / f"{source_index:02d}-angle-{angle_index:02d}-{angle:g}.png"
            output_path = root / relative
            if not cv2.imwrite(str(output_path), rotated):
                raise SystemExit(f"unable to write generated image: {output_path}")
            outputs.append({
                "schema_version": "1.0",
                "record_type": "known_angle_crop",
                "image_id": f"sroie-angle-{source_index:02d}-{angle_index:02d}",
                "image_relpath": str(relative).replace("\\", "/"),
                "width": output_width,
                "height": output_height,
                "source_dataset": source["source_dataset"],
                "source_split": source["source_split"],
                "source_revision": source["source_revision"],
                "source_parent_image_id": source["source_parent_image_id"],
                "source_parent_image_sha256": source.get("source_parent_image_sha256"),
                "source_instance_id": source["source_instance_id"],
                "source_crop_relpath": source["source_crop_relpath"],
                "source_crop_sha256": source["source_crop_sha256"],
                "expected_text": source["source_text"],
                "angle_degrees_ccw": angle,
                "source_quad": [[float(point[0]), float(point[1])] for point in quad],
                "image_sha256": digest(output_path),
                "benchmark_eligibility": "controlled_real_crop_angle_smoke_only",
                "source_crop_margin_pixels": args.margin,
            })
    manifest_path.write_text("\n".join(json.dumps(record, ensure_ascii=False) for record in outputs) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "sourcePages": 4, "angles": angles, "records": len(outputs)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
