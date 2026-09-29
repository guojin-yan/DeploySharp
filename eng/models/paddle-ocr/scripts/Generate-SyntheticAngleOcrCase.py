"""Generate deterministic rotated OCR crops with known quadrilateral geometry."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rotate(image: np.ndarray, angle: float) -> tuple[np.ndarray, list[list[float]]]:
    height, width = image.shape[:2]
    center = (width / 2.0, height / 2.0)
    matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
    radians = np.deg2rad(angle)
    cosine = abs(np.cos(radians))
    sine = abs(np.sin(radians))
    output_width = int(np.ceil(height * sine + width * cosine))
    output_height = int(np.ceil(height * cosine + width * sine))
    matrix[0, 2] += output_width / 2.0 - center[0]
    matrix[1, 2] += output_height / 2.0 - center[1]
    result = cv2.warpAffine(image, matrix, (output_width, output_height), flags=cv2.INTER_LINEAR, borderValue=(255, 255, 255))
    corners = np.float32([[0, 0], [width, 0], [width, height], [0, height]])
    transformed = cv2.transform(corners[None, :, :], matrix)[0]
    return result, [[float(point[0]), float(point[1])] for point in transformed]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--characters", type=int, default=180)
    parser.add_argument("--angles", default="0,12,-12,27,-27,180")
    args = parser.parse_args()
    if args.characters < 32 or args.characters > 512:
        raise SystemExit("characters must be between 32 and 512")
    angles = [float(value.strip()) for value in args.angles.split(",") if value.strip()]
    if not angles or len(angles) > 12 or any(abs(angle) > 180 for angle in angles):
        raise SystemExit("angles must contain 1..12 values in [-180,180]")

    phrase = "DeploySharp angle geometry contract 0123456789; perspective crop and affine parity. "
    text = (phrase * ((args.characters // len(phrase)) + 1))[: args.characters]
    font_path = Path(r"C:\Windows\Fonts\arial.ttf")
    if not font_path.exists():
        raise SystemExit(f"Arial font was not found: {font_path}")
    font = ImageFont.truetype(str(font_path), 32)
    probe = Image.new("RGB", (32, 64), "white")
    left, top, right, bottom = ImageDraw.Draw(probe).textbbox((0, 0), text, font=font)
    margin = 32
    width = right - left + margin * 2
    height = bottom - top + margin * 2
    base = Image.new("RGB", (width, height), "white")
    ImageDraw.Draw(base).text((margin - left, margin - top), text, fill="black", font=font)
    base_array = cv2.cvtColor(np.asarray(base), cv2.COLOR_RGB2BGR)

    root = args.output_root.resolve()
    image_dir = root / "data" / "images" / "synthetic-angle"
    manifest_path = root / "data" / "annotations" / "manifests" / "synthetic-angle-ocr.jsonl"
    image_dir.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    records = []
    for index, angle in enumerate(angles):
        output, polygon = rotate(base_array, angle)
        image_path = image_dir / f"angle-{index:02d}-{angle:g}.png"
        cv2.imwrite(str(image_path), output)
        records.append(
            {
                "schema_version": "1.0",
                "record_type": "image",
                "image_id": f"synthetic-angle-{index:02d}-{angle:g}",
                "image_relpath": str(image_path.relative_to(root)).replace("\\", "/"),
                "width": int(output.shape[1]),
                "height": int(output.shape[0]),
                "angle_degrees_ccw": angle,
                "expected_text": text,
                "polygon": polygon,
                "source_dataset": "deploysharp-controlled-synthetic-angle",
                "source_split": "contract",
                "license": "Apache-2.0 generated test asset",
                "benchmark_eligibility": "controlled_geometry_contract_only",
                "image_sha256": sha256(image_path),
            }
        )
    manifest_path.write_text("\n".join(json.dumps(record, ensure_ascii=False) for record in records) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "characters": len(text), "angles": angles, "count": len(records)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
