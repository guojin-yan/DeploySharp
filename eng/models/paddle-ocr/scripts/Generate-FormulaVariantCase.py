"""Create deterministic geometry/quality variants of the official formula image."""
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
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args()
    image = cv2.imread(str(args.image), cv2.IMREAD_COLOR)
    if image is None:
        raise SystemExit(f"Unable to read formula image: {args.image}")
    root = args.output_root.resolve()
    image_dir = root / "data" / "images" / "formula"
    manifest = root / "data" / "annotations" / "manifests" / "formula-variants.jsonl"
    image_dir.mkdir(parents=True, exist_ok=True); manifest.parent.mkdir(parents=True, exist_ok=True)
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    variants = {
        "original": image,
        "white-border": cv2.copyMakeBorder(image, 32, 32, 32, 32, cv2.BORDER_CONSTANT, value=(255, 255, 255)),
        "contrast": cv2.convertScaleAbs(image, alpha=0.72, beta=34),
        "jpeg": cv2.imdecode(cv2.imencode('.jpg', image, [cv2.IMWRITE_JPEG_QUALITY, 45])[1], cv2.IMREAD_COLOR),
        "blur": cv2.GaussianBlur(image, (3, 3), .7),
    }
    rows = []
    for name, value in variants.items():
        path = image_dir / f"{name}.png"
        cv2.imwrite(str(path), value)
        rows.append({"schema_version":"1.0","record_type":"formula_variant","variant":name,"image_relpath":str(path.relative_to(root)).replace('\\','/'),"source_image_sha256":sha(args.image),"image_sha256":sha(path),"expected_label":"general_formula_rec_001"})
    manifest.write_text('\n'.join(json.dumps(row, ensure_ascii=False) for row in rows)+'\n',encoding='utf8')
    print(json.dumps({"manifest":str(manifest),"variants":len(rows)},ensure_ascii=False))


if __name__ == '__main__': main()
