"""Generate a bounded, self-labeled long OCR line for SlidingWindow contract checks."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--characters", type=int, default=3600)
    args = parser.parse_args()
    if args.characters < 3201 or args.characters > 4096:
        raise SystemExit("characters must be between 3201 and 4096")

    phrase = "DeploySharp OCR long text contract 0123456789; sliding-window seam check. "
    text = (phrase * ((args.characters // len(phrase)) + 1))[: args.characters]
    font_path = Path(r"C:\Windows\Fonts\arial.ttf")
    if not font_path.exists():
        raise SystemExit(f"Arial font was not found: {font_path}")
    font = ImageFont.truetype(str(font_path), 22)
    probe = Image.new("RGB", (32, 64), "white")
    draw = ImageDraw.Draw(probe)
    left, top, right, bottom = draw.textbbox((0, 0), text, font=font, stroke_width=0)
    width = right - left + 40
    height = bottom - top + 40
    image = Image.new("RGB", (width, height), "white")
    ImageDraw.Draw(image).text((20 - left, 20 - top), text, fill="black", font=font)

    root = args.output_root.resolve()
    image_path = root / "data" / "images" / "synthetic" / "long-text-3600.png"
    manifest_path = root / "data" / "annotations" / "manifests" / "synthetic-long-text-3600.jsonl"
    image_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(image_path, format="PNG", optimize=False)
    digest = hashlib.sha256(image_path.read_bytes()).hexdigest()
    record = {
        "schema_version": "1.0",
        "record_type": "image",
        "image_id": "synthetic-long-text-3600",
        "image_relpath": "data/images/synthetic/long-text-3600.png",
        "width": width,
        "height": height,
        "task_types": ["rec", "pipeline"],
        "source_dataset": "deploysharp-controlled-synthetic",
        "source_split": "contract",
        "license": "Apache-2.0 generated test asset",
        "benchmark_eligibility": "controlled_contract_only",
        "source_provenance": {"generator": "Generate-SyntheticLongTextCase.py", "image_sha256": digest},
        "dimensions": {"orientation": "horizontal", "geometry": "axis_aligned", "source_rotation_degrees_ccw": 0.0},
        "statistics": {"valid_region_count": 1, "character_count": len(text), "synthetic": True},
        "instances": [{
            "instance_id": "synthetic-long-text-3600-line-000",
            "text": text,
            "polygon": [[20, 20], [width - 20, 20], [width - 20, height - 20], [20, height - 20]],
            "reading_order": 0,
            "ignore": False,
            "language": "en",
            "direction_label": {"class": 0, "label_source": "controlled_generator"},
            "attributes": {"char_count": len(text), "vertical": {"value": False}, "handwritten": {"value": False}, "text_direction": "horizontal", "length_bucket": "synthetic_ge_3200"},
        }],
    }
    manifest_path.write_text(json.dumps(record, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"image": str(image_path), "manifest": str(manifest_path), "width": width, "height": height, "characters": len(text), "image_sha256": digest}, ensure_ascii=False))


if __name__ == "__main__":
    main()
