"""Run a structural ONNX Runtime CPU smoke test for converted Paddle document models.

This is deliberately a graph/input/output check, not a semantic parity claim. It records
the exact input shapes chosen for the smoke call and keeps failures per model.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort


def shape_for(value: object, axis: int) -> int:
    if isinstance(value, int) and value > 0:
        return value
    if axis == 0:
        return 1
    if axis == 1:
        return 3
    return 224


def input_shape(item: object, image_width: int, image_height: int) -> list[int]:
    """Select a conservative real-image shape for dynamic Paddle exports."""
    dimensions = list(getattr(item, "shape"))
    result = []
    for axis, value in enumerate(dimensions):
        if isinstance(value, int) and value > 0:
            result.append(value)
        elif axis == 0:
            result.append(1)
        elif len(dimensions) == 4 and axis == 1:
            result.append(1 if "uvdoc" in str(getattr(item, "name", "")).lower() else 3)
        elif len(dimensions) == 4 and axis == 2:
            result.append(image_height)
        elif len(dimensions) == 4 and axis == 3:
            result.append(image_width)
        elif len(dimensions) == 2 and axis == 1:
            result.append(2)
        else:
            result.append(224)
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--onnx-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--image", type=Path, help="Optional real image for a semantic smoke input.")
    parser.add_argument("--model", action="append", help="Restrict verification to one or more ONNX file stems.")
    args = parser.parse_args()
    image = None
    image_sha256 = None
    image_width = image_height = 224
    if args.image:
        from PIL import Image
        image_bytes = args.image.read_bytes()
        image_sha256 = hashlib.sha256(image_bytes).hexdigest()
        image = Image.open(args.image).convert("RGB")
        # Use a stride-friendly canvas for dynamic Paddle graphs. The source image
        # is resized into this canvas; using an odd source size can violate internal
        # downsample shape equalities in converted graphs.
        image_width = image_height = 640
    rows: list[dict[str, object]] = []
    selected_models = set(args.model or [])
    for path in sorted(args.onnx_root.glob("*.onnx")):
        if selected_models and path.stem not in selected_models:
            continue
        row: dict[str, object] = {"model": path.stem, "path": str(path), "status": "failed"}
        try:
            graph = onnx.load(str(path))
            session = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
            feeds: dict[str, np.ndarray] = {}
            input_shapes: dict[str, list[int]] = {}
            for item in session.get_inputs():
                dimensions = input_shape(item, min(image_width, 640), min(image_height, 640))
                input_shapes[item.name] = dimensions
                if item.type == "tensor(float)":
                    if image is not None and len(dimensions) == 4 and dimensions[1] in (1, 3):
                        from PIL import Image as PillowImage
                        prepared = image.resize((dimensions[3], dimensions[2]), PillowImage.Resampling.BILINEAR)
                        pixels = np.asarray(prepared).astype(np.float32) / 255.0
                        if dimensions[1] == 1:
                            pixels = pixels.mean(axis=2, keepdims=True)
                        elif path.stem.lower() == "uvdoc":
                            # UVDoc's exported graph consumes and emits byte-scale RGB.
                            pixels *= 255.0
                        else:
                            pixels = (pixels - np.asarray([.485, .456, .406], dtype=np.float32)) / np.asarray([.229, .224, .225], dtype=np.float32)
                        feeds[item.name] = pixels.transpose(2, 0, 1)[None]
                    elif image is not None and len(dimensions) == 2 and dimensions[1] == 2:
                        if "scale" in item.name.lower():
                            feeds[item.name] = np.ones(dimensions, dtype=np.float32)
                        else:
                            feeds[item.name] = np.asarray([[image_height, image_width]], dtype=np.float32)
                    else:
                        feeds[item.name] = np.zeros(dimensions, dtype=np.float32)
                elif item.type == "tensor(int64)":
                    feeds[item.name] = np.zeros(dimensions, dtype=np.int64)
                elif item.type == "tensor(int32)":
                    feeds[item.name] = np.zeros(dimensions, dtype=np.int32)
                else:
                    raise RuntimeError(f"unsupported input type {item.type}")
            outputs = session.run(None, feeds)
            digest = hashlib.sha256()
            for output in outputs:
                digest.update(np.ascontiguousarray(output).tobytes())
            row.update({
                "status": "ort-cpu-smoke-passed",
                "irVersion": graph.ir_version,
                "opset": max((item.version for item in graph.opset_import), default=0),
                "inputs": input_shapes,
                "outputShapes": [list(output.shape) for output in outputs],
                "outputSha256": digest.hexdigest(),
            })
            if image_sha256:
                row["inputImageSha256"] = image_sha256
                row["outputStats"] = [{"min": float(np.min(output)), "max": float(np.max(output)), "mean": float(np.mean(output))} for output in outputs if np.issubdtype(output.dtype, np.number)]
        except Exception as error:  # keep the matrix useful when one graph is blocked
            row["error"] = str(error)
        rows.append(row)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({"schemaVersion": "deploysharp-paddle-document-onnx-smoke-v1", "records": rows}, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
