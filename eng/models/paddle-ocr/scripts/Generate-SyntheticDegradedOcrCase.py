"""Create a small, parent-linked OCR degradation contract from real SROIE crops."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def degrade(image: np.ndarray, condition: str, seed: int) -> np.ndarray:
    if condition == "normal":
        return image.copy()
    if condition == "low-contrast":
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        gray = cv2.cvtColor(gray, cv2.COLOR_GRAY2BGR)
        return cv2.addWeighted(image, 0.32, gray, 0.68, 0)
    if condition == "blur":
        return cv2.GaussianBlur(image, (5, 5), 1.25)
    if condition == "noise":
        rng = np.random.default_rng(seed)
        noise = rng.normal(0, 16, image.shape).astype(np.float32)
        return np.clip(image.astype(np.float32) + noise, 0, 255).astype(np.uint8)
    if condition == "shadow":
        height, width = image.shape[:2]
        gradient = np.linspace(1.0, 0.48, max(1, width), dtype=np.float32)
        mask = np.tile(gradient[None, :, None], (height, 1, 3))
        return np.clip(image.astype(np.float32) * mask, 0, 255).astype(np.uint8)
    if condition == "jpeg":
        ok, encoded = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, 35])
        if not ok:
            raise RuntimeError("JPEG encoding failed")
        decoded = cv2.imdecode(encoded, cv2.IMREAD_COLOR)
        if decoded is None:
            raise RuntimeError("JPEG decoding failed")
        return decoded
    raise ValueError(f"unknown condition: {condition}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-root", type=Path, required=True)
    parser.add_argument("--source-manifest", type=Path, required=True)
    parser.add_argument("--crop-manifest", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--max-crops", type=int, default=4)
    args = parser.parse_args()
    if args.max_crops < 1 or args.max_crops > 32:
        raise SystemExit("max-crops must be between 1 and 32")

    parent_rows = {}
    for line in args.source_manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        record = json.loads(line)
        for instance in record.get("instances", []):
            parent_rows[instance["instance_id"]] = (record, instance)

    selected = []
    selected_parent_ids: set[str] = set()
    for line in args.crop_manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        crop = json.loads(line)
        parent = parent_rows.get(crop.get("parent_instance_id"))
        if parent is None:
            continue
        if parent[0]["image_id"] in selected_parent_ids:
            continue
        text = parent[1].get("text") or ""
        if 8 <= len(text) <= 32 and crop.get("quality_check") != "invalid":
            selected.append((crop, parent[0], parent[1]))
            selected_parent_ids.add(parent[0]["image_id"])
        if len(selected) >= args.max_crops:
            break
    if len(selected) < args.max_crops:
        raise SystemExit(f"only {len(selected)} usable source crops were found")

    conditions = ("normal", "low-contrast", "blur", "noise", "shadow", "jpeg")
    root = args.output_root.resolve()
    image_root = root / "data" / "images" / "degraded"
    manifest_path = root / "data" / "annotations" / "manifests" / "synthetic-degraded-ocr.jsonl"
    image_root.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    records = []
    for source_index, (crop, parent, instance) in enumerate(selected):
        source_path = args.dataset_root / crop["crop_relpath"]
        image = cv2.imread(str(source_path), cv2.IMREAD_COLOR)
        if image is None:
            raise SystemExit(f"unable to read source crop: {source_path}")
        source_sha = digest(source_path)
        for condition_index, condition in enumerate(conditions):
            output = degrade(image, condition, 20260929 + source_index * 100 + condition_index)
            relative = Path("data/images/degraded") / f"{source_index:02d}-{condition}.png"
            output_path = root / relative
            output_path.parent.mkdir(parents=True, exist_ok=True)
            if not cv2.imwrite(str(output_path), output):
                raise SystemExit(f"unable to write output: {output_path}")
            records.append(
                {
                    "schema_version": "1.0",
                    "record_type": "degraded_crop",
                    "image_id": f"sroie-{source_index:02d}-{condition}",
                    "image_relpath": str(relative).replace("\\", "/"),
                    "source_dataset": parent["source_dataset"],
                    "source_split": parent["source_split"],
                    "source_revision": parent["source_revision"],
                    "source_parent_image_id": parent["image_id"],
                    "source_parent_image_sha256": parent["source_provenance"]["image_sha256"],
                    "source_instance_id": instance["instance_id"],
                    "source_crop_relpath": crop["crop_relpath"],
                    "source_crop_sha256": source_sha,
                    "source_text": instance["text"],
                    "condition": condition,
                    "condition_seed": 20260929 + source_index * 100 + condition_index,
                    "image_sha256": digest(output_path),
                    "benchmark_eligibility": "controlled_degradation_smoke_only",
                }
            )
    manifest_path.write_text("\n".join(json.dumps(record, ensure_ascii=False) for record in records) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "records": len(records), "sourceCrops": len(selected), "conditions": conditions}, ensure_ascii=False))


if __name__ == "__main__":
    main()
