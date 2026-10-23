"""Compare unmodified pinned upstream image processing with Transformers PIL."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import inspect
import json
from pathlib import Path

import numpy as np
from PIL import Image
from transformers import AutoProcessor


SOURCE_SHA256 = "a4fa521b9cb16e207f94b7f2d16427771776dfc634420d319fc4916ee58049ec"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = args.model_root / "image_processing_paddleocr_vl.py"
    if digest(source) != SOURCE_SHA256:
        raise ValueError("Pinned upstream image-processing source SHA mismatch")
    spec = importlib.util.spec_from_file_location("deploysharp_pinned_paddleocr_vl_image", source)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    config = json.loads((args.model_root / "preprocessor_config.json").read_text(encoding="utf-8"))
    processor = AutoProcessor.from_pretrained(str(args.model_root), local_files_only=True, backend="pil")
    report = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "pinnedSourceSha256": SOURCE_SHA256, "scriptSha256": digest(Path(__file__)),
              "referenceClass": type(processor.image_processor).__name__,
              "referenceModuleSha256": digest(Path(inspect.getfile(type(processor.image_processor)))),
              "manifestSha256": digest(args.manifest), "cases": [],
              "boundary": "Actual image grid and normalized patch-pixel comparison only. Does not prove language positions, model-generation parity or a C# image resizer."}
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    for case in manifest["cases"]:
        path = args.manifest.parent / case["image"]
        if digest(path) != case["sha256"]:
            raise ValueError(f"Input SHA mismatch: {case['id']}")
        image = Image.open(path).convert("RGB")
        if case["task"] == "spotting" and image.width < 1500 and image.height < 1500:
            image = image.resize((image.width * 2, image.height * 2), Image.Resampling.LANCZOS)
        maximum = 1605632 if case["task"] == "spotting" else 1003520
        upstream = module.PaddleOCRVLImageProcessor(**{**config, "min_pixels": 112896, "max_pixels": maximum})
        pinned = upstream(images=image, return_tensors="np")
        current = processor.image_processor(images=image, return_tensors="np", size={"shortest_edge": 112896, "longest_edge": maximum})
        a, b = np.asarray(pinned["pixel_values"]), np.asarray(current["pixel_values"])
        grid_a, grid_b = np.asarray(pinned["image_grid_thw"]), np.asarray(current["image_grid_thw"])
        same_shape = a.shape == b.shape
        row = {"id": case["id"], "inputSha256": case["sha256"], "pinnedShape": list(a.shape), "currentShape": list(b.shape),
               "pinnedGridThw": grid_a.tolist(), "currentGridThw": grid_b.tolist(), "gridEqual": bool(np.array_equal(grid_a, grid_b)),
               "pinnedPixelSha256": hashlib.sha256(a.tobytes()).hexdigest(), "currentPixelSha256": hashlib.sha256(b.tobytes()).hexdigest(),
               "pixelExact": bool(np.array_equal(a, b)),
               "maximumAbsoluteDifference": float(np.max(np.abs(a.astype(np.float64) - b.astype(np.float64)))) if same_shape else None}
        report["cases"].append(row)
        print(f"COMPARED {case['id']} shape={same_shape} grid={row['gridEqual']} pixels={row['pixelExact']}", flush=True)
    report["status"] = "all-exact" if all(row["gridEqual"] and row["pixelExact"] for row in report["cases"]) else "differences-recorded"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
