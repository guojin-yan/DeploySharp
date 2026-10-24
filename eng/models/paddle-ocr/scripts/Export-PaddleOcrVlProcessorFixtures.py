"""Export decoded RGB and patch references from the pinned upstream processor.

Generated image/tensor files belong outside Git. They let the managed processor
be checked independently from OpenCV decoding and ONNX generation.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path

import numpy as np
import PIL
from PIL import Image


REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
SOURCE_SHA256 = "a4fa521b9cb16e207f94b7f2d16427771776dfc634420d319fc4916ee58049ec"
TASKS = {"ocr": "TextRecognition", "table": "TableRecognition",
         "formula": "FormulaRecognition", "chart": "ChartRecognition",
         "spotting": "TextSpotting", "seal": "SealRecognition"}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args()
    source = args.model_root / "image_processing_paddleocr_vl.py"
    if digest(source) != SOURCE_SHA256:
        raise ValueError("Pinned upstream processor source SHA mismatch")
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    if manifest["checkpointRevision"] != REVISION:
        raise ValueError("Unexpected checkpoint revision")
    spec = importlib.util.spec_from_file_location("deploysharp_vl_fixture_processor", source)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    config_path = args.model_root / "preprocessor_config.json"
    config = json.loads(config_path.read_text(encoding="utf-8"))
    output = args.output_root.resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = {
        "schemaVersion": "deploysharp-paddleocr-vl-processor-fixtures-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "checkpointRevision": REVISION, "scriptSha256": digest(Path(__file__)),
        "processorSourceSha256": SOURCE_SHA256, "configSha256": digest(config_path),
        "inputManifestSha256": digest(args.manifest),
        "environment": {"pillow": PIL.__version__, "numpy": np.__version__},
        "boundary": "Pinned upstream/Pillow RGB and patch tensors; no model inference, layout or labeled quality claim.",
        "cases": [],
    }
    seen = set()
    for case in manifest["cases"]:
        identifier = case["id"]
        if not identifier or Path(identifier).name != identifier or identifier in seen:
            raise ValueError("Fixture IDs must be unique file names")
        seen.add(identifier)
        task = case["task"]
        if task not in TASKS:
            raise ValueError("Unknown PaddleOCR-VL task")
        path = args.manifest.parent / case["image"]
        if digest(path) != case["sha256"]:
            raise ValueError(f"Input SHA mismatch: {identifier}")
        with Image.open(path) as encoded:
            image = encoded.convert("RGB")
        source_size = list(image.size)
        rgb = output / (identifier + ".rgb.bin")
        rgb.write_bytes(image.tobytes())
        if task == "spotting" and image.width < 1500 and image.height < 1500:
            image = image.resize((image.width * 2, image.height * 2), Image.Resampling.LANCZOS)
        maximum = 1605632 if task == "spotting" else 1003520
        processor = module.PaddleOCRVLImageProcessor(**{**config, "min_pixels": 112896, "max_pixels": maximum})
        reference = processor(images=image, return_tensors="np")
        grid = np.asarray(reference["image_grid_thw"], dtype=np.int64)
        if grid.shape != (1, 3) or grid[0, 0] != 1:
            raise ValueError("Only one unpadded image is supported")
        height, width = (int(value) for value in grid[0, 1:])
        values = np.asarray(reference["pixel_values"], dtype="<f4")
        if values.size != height * width * 3 * 14 * 14 or not np.isfinite(values).all():
            raise ValueError("Invalid official patch values")
        patches = output / (identifier + ".patch-fp32.bin")
        patches.write_bytes(values.tobytes(order="C"))
        report["cases"].append({
            "id": identifier, "task": TASKS[task], "inputSha256": case["sha256"],
            "sourceSize": source_size, "preparedSize": list(image.size),
            "patchShape": [height, width, 3, 14, 14],
            "rgbFile": rgb.name, "rgbSha256": digest(rgb),
            "patchFile": patches.name, "patchSha256": digest(patches),
        })
        print(f"EXPORTED {identifier} grid={height}x{width}", flush=True)
    if not report["cases"]:
        raise ValueError("At least one fixture is required")
    report["status"] = "exported"
    (output / "fixtures.json").write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
