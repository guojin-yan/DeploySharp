#!/usr/bin/env python3
"""Capture an independent PP-OCRv5 mobile-cls golden with Paddle Predictor."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
import paddle


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def file_record(path: Path) -> dict[str, object]:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {"fileName": path.name, "size": path.stat().st_size, "sha256": digest.hexdigest()}


def classifier_tensor(image_path: Path) -> np.ndarray:
    image = cv2.imread(str(image_path), cv2.IMREAD_COLOR)
    if image is None:
        raise RuntimeError(f"Unable to read image: {image_path}")
    resized = cv2.resize(image, (160, 80))
    rgb = cv2.cvtColor(resized, cv2.COLOR_BGR2RGB)
    normalized = (rgb.astype(np.float32) / np.float32(255.0) - np.array([0.485, 0.456, 0.406], dtype=np.float32)) / np.array([0.229, 0.224, 0.225], dtype=np.float32)
    return np.ascontiguousarray(normalized.transpose(2, 0, 1)[np.newaxis, ...])


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-directory", type=Path, required=True)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    model_file = args.model_directory / "inference.json"
    params_file = args.model_directory / "inference.pdiparams"
    for path in (model_file, params_file, args.image):
        if not path.is_file():
            raise RuntimeError(f"Missing required input: {path}")

    tensor = classifier_tensor(args.image)
    config = paddle.inference.Config(str(model_file), str(params_file))
    config.disable_gpu()
    config.disable_glog_info()
    config.switch_ir_optim(True)
    predictor = paddle.inference.create_predictor(config)
    input_names = predictor.get_input_names()
    output_names = predictor.get_output_names()
    if len(input_names) != 1 or len(output_names) != 1:
        raise RuntimeError(f"Expected one input and output, got {input_names}/{output_names}")

    input_handle = predictor.get_input_handle(input_names[0])
    input_handle.reshape(tensor.shape)
    input_handle.copy_from_cpu(tensor)
    predictor.run()
    output = np.ascontiguousarray(predictor.get_output_handle(output_names[0]).copy_to_cpu())
    if output.shape != (1, 2):
        raise RuntimeError(f"Expected classifier output [1,2], got {output.shape}")
    index = int(output[0].argmax())

    result = {
        "schemaVersion": "1.0",
        "purpose": "independent official Paddle Predictor golden for PP-OCRv5 mobile-cls",
        "runtime": {
            "engine": "paddle.inference.Predictor",
            "paddleVersion": paddle.__version__,
            "paddleCommit": getattr(paddle.version, "commit", "unknown"),
            "device": "cpu",
            "irOptimization": True,
        },
        "model": {
            "inferenceJson": file_record(model_file),
            "inferenceParams": file_record(params_file),
        },
        "input": {
            "image": file_record(args.image),
            "preprocessing": "ResizeImage(size=[160,80]); BGR-to-RGB; (uint8/255-[0.485,0.456,0.406])/[0.229,0.224,0.225]",
            "shape": list(tensor.shape),
            "sha256": sha256_bytes(tensor.tobytes()),
        },
        "output": {
            "name": output_names[0],
            "shape": list(output.shape),
            "sha256": sha256_bytes(output.tobytes()),
            "values": [float(value) for value in output.reshape(-1)],
            "classIndex": index,
            "label": ["0_degree", "180_degree"][index],
            "confidence": float(output[0, index]),
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
