"""Diagnose FP32 vision differences without relaxing the component tolerance."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
import hashlib
import inspect
import json
from pathlib import Path

import numpy as np
import onnxruntime as ort
import torch
from transformers import AutoModelForImageTextToText

from PaddleOcrVlOnnxModules import SingleImageVision


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def error(actual, expected):
    difference = np.abs(actual.astype(np.float64) - expected.astype(np.float64))
    rejected = difference > (3e-3 + 1e-3 * np.abs(expected.astype(np.float64)))
    return {"allClose": bool(not rejected.any()), "maximumAbsoluteDifference": float(difference.max()),
            "rootMeanSquareDifference": float(np.sqrt(np.mean(difference ** 2))),
            "rejectedElements": int(rejected.sum()), "totalElements": actual.size,
            "meanAbsoluteDifference": float(difference.mean())}


def save(path, report):
    path.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--tensor-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--case", default="spotting-demo-1")
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError("Preserve the prior diagnostic report")
    torch.set_num_threads(4)
    reference = json.loads(args.reference.read_text(encoding="utf-8"))
    case = next(row for row in reference["cases"] if row["id"] == args.case)
    if digest(args.model_root / "model.safetensors") != "85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db":
        raise ValueError("Weight identity mismatch")
    stage = json.loads((args.bundle_root / "vision-export.json").read_text(encoding="utf-8"))
    graph = args.bundle_root / stage["graph"]["file"]
    if digest(graph) != stage["graph"]["sha256"]:
        raise ValueError("Graph identity mismatch")
    pixels_path = args.tensor_root / f"{case['id']}-pixel_values.bin"
    if digest(pixels_path) != case["tensors"]["pixel_values"]["sha256"]:
        raise ValueError("Saved pixel identity mismatch")
    grid = case["imageGridThw"][0]
    pixels = np.fromfile(pixels_path, dtype=np.float32).reshape(grid[1], grid[2], 3, 14, 14)
    model = AutoModelForImageTextToText.from_pretrained(str(args.model_root), local_files_only=True,
        dtype=torch.float32, attn_implementation="sdpa").eval()
    if digest(Path(inspect.getfile(type(model)))) != "9471f809b2c3c4dcb722e578b6f91d33af08abd8b0c4047bad067c011f499d32":
        raise ValueError("Runtime source identity mismatch")
    del model.model.language_model, model.lm_head
    with torch.inference_mode():
        expected = torch.cat(model.model.get_image_features(torch.from_numpy(pixels.reshape(-1, 3, 14, 14)),
            image_grid_thw=torch.tensor([grid])).pooler_output).numpy()
        adapted = SingleImageVision(model)(torch.from_numpy(pixels)).numpy()
    report = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(), "case": args.case,
              "scriptSha256": digest(Path(__file__)), "graphSha256": digest(graph), "pixelsSha256": digest(pixels_path),
              "referenceSha256": digest(args.reference), "rtol": 1e-3, "atol": 3e-3,
              "nativeAdapter": error(adapted, expected), "optimizations": [], "status": "running",
              "boundary": "FP32 feature precision diagnostic only, no generation/quality/performance claim."}
    del model, adapted
    gc.collect()
    save(args.output, report)
    for name, level in [("basic", ort.GraphOptimizationLevel.ORT_ENABLE_BASIC),
                        ("none", ort.GraphOptimizationLevel.ORT_DISABLE_ALL),
                        ("all", ort.GraphOptimizationLevel.ORT_ENABLE_ALL)]:
        settings = ort.SessionOptions()
        settings.intra_op_num_threads = 4
        settings.inter_op_num_threads = 1
        settings.enable_cpu_mem_arena = False
        settings.enable_mem_pattern = False
        settings.graph_optimization_level = level
        session = ort.InferenceSession(str(graph), sess_options=settings, providers=["CPUExecutionProvider"])
        actual = session.run(None, {"pixel_patches": pixels})[0]
        row = {"optimization": name, **error(actual, expected)}
        report["optimizations"].append(row)
        save(args.output, report)
        print(f"PRECISION {name} {row}", flush=True)
        del session, actual
        gc.collect()
    report["status"] = "diagnostic-complete"
    save(args.output, report)


if __name__ == "__main__":
    main()
