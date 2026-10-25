"""Compare original/head-grouped vision features on the six pinned real inputs.

Runs each case/graph in a fresh CPU process to bound allocator retention. This
measures graph-rewrite numerical parity, not labelled accuracy or CUDA latency.
Generation/provider evidence is collected separately.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import time

import numpy as np
import onnxruntime as ort


def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def byte_digest(array):
    return hashlib.sha256(array.astype("<f4", copy=False).tobytes(order="C")).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-bundle", type=Path, required=True)
    parser.add_argument("--grouped-bundle", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--tensor-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--worker-case", help=argparse.SUPPRESS)
    parser.add_argument("--worker-kind", choices=("original", "grouped"), help=argparse.SUPPRESS)
    args = parser.parse_args()
    if args.threads < 1 or args.output.exists():
        raise ValueError("Use positive threads and a new report path")
    reference = json.loads(args.reference.read_text(encoding="utf-8"))
    if args.worker_case:
        case = next(case for case in reference["cases"] if case["id"] == args.worker_case)
        pixels = args.tensor_root / (case["id"] + "-pixel_values.bin")
        contract = case["tensors"]["pixel_values"]
        if digest(pixels) != contract["sha256"] or pixels.stat().st_size != contract["bytes"]:
            raise ValueError("Pixel identity mismatch: " + case["id"])
        grid = case["tensors"]["image_grid_thw"]["firstValues"]
        if grid[0] != 1 or len(grid) != 3:
            raise ValueError("Expected a single-image grid")
        patches = np.fromfile(pixels, dtype="<f4").reshape(grid[1], grid[2], 3, 14, 14)
        options = ort.SessionOptions()
        options.intra_op_num_threads = args.threads
        options.inter_op_num_threads = 1
        options.enable_mem_pattern = False
        options.enable_cpu_mem_arena = False
        options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
        bundle = args.original_bundle if args.worker_kind == "original" else args.grouped_bundle
        session = ort.InferenceSession(str(bundle / "vision.onnx"), options, providers=["CPUExecutionProvider"])
        started = time.perf_counter()
        actual = session.run(["image_features"], {"pixel_patches": patches})[0]
        elapsed = (time.perf_counter() - started) * 1000
        np.save(args.output, actual, allow_pickle=False)
        args.output.with_suffix(".json").write_text(json.dumps({"elapsedMilliseconds": elapsed, "runtime": ort.__version__}) + "\n", encoding="utf-8")
        return
    derivative_path = args.grouped_bundle / "head-group-export.json"
    derivative = json.loads(derivative_path.read_text(encoding="utf-8"))
    if reference["checkpoint"]["revision"] != derivative["checkpointRevision"] or reference["status"] != "reference-generation-complete":
        raise ValueError("Reference/derivative checkpoint mismatch")
    if len(reference["cases"]) != 6 or len({case["id"] for case in reference["cases"]}) != 6:
        raise ValueError("Expected the six unique fixed task inputs")
    for key, root in (("sourceGraphs", args.original_bundle), ("graphs", args.grouped_bundle)):
        for graph in derivative[key]:
            if digest(root / (graph["role"] + ".onnx")) != graph["sha256"]:
                raise ValueError(f"Graph hash mismatch: {key}/{graph['role']}")
    report = {"schemaVersion": "deploysharp-paddleocr-vl-head-group-feature-parity-v1", "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "status": "running", "scriptSha256": digest(__file__), "referenceSha256": digest(args.reference),
              "derivativeReportSha256": digest(derivative_path), "checkpointRevision": derivative["checkpointRevision"],
              "protocol": {"provider": "CPUExecutionProvider", "threads": args.threads, "interOpThreads": 1,
                           "enableMemoryPattern": False, "enableCpuMemoryArena": False, "graphOptimization": "All", "rtol": 1e-3, "atol": 3e-3,
                           "isolation": "fresh subprocess per case and graph"},
              "packages": {name: importlib.metadata.version(name) for name in ("onnxruntime", "numpy")},
              "boundary": "Original and derivative ONNX vision features on six pinned inputs with the same CPU runtime/options. Tolerance is the pre-existing exporter tolerance, not bit equality, labelled quality, CUDA feature parity, formal performance or upstream original-model precision proof.", "cases": []}
    args.output.parent.mkdir(parents=True, exist_ok=True)

    def save():
        args.output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")

    try:
        with tempfile.TemporaryDirectory(prefix=args.output.stem + "-features-", dir=args.output.parent) as temporary:
            for case in reference["cases"]:
                grid = case["tensors"]["image_grid_thw"]["firstValues"]
                row = {"id": case["id"], "patchSha256": case["tensors"]["pixel_values"]["sha256"], "grid": grid[1:], "status": "running"}
                report["cases"].append(row)
                arrays = {}
                for label in ("original", "grouped"):
                    report["phase"] = label
                    save()
                    path = Path(temporary) / (case["id"] + "-" + label + ".npy")
                    subprocess.run([sys.executable, str(Path(__file__).resolve()), "--original-bundle", str(args.original_bundle),
                        "--grouped-bundle", str(args.grouped_bundle), "--reference", str(args.reference), "--tensor-root", str(args.tensor_root),
                        "--output", str(path), "--threads", str(args.threads), "--worker-case", case["id"], "--worker-kind", label], check=True)
                    arrays[label] = np.load(path, allow_pickle=False)
                    timing = json.loads(path.with_suffix(".json").read_text(encoding="utf-8"))
                    if timing["runtime"] != ort.__version__:
                        raise ValueError("Worker runtime mismatch")
                    row[label + "ElapsedMilliseconds"] = timing["elapsedMilliseconds"]
                    row[label + "FeatureBytesSha256"] = byte_digest(arrays[label])
                expected, actual = arrays["original"], arrays["grouped"]
                if actual.shape != expected.shape:
                    raise ValueError("Feature shape mismatch: " + case["id"])
                difference = np.abs(actual.astype(np.float64) - expected.astype(np.float64))
                close = np.isclose(actual, expected, rtol=1e-3, atol=3e-3)
                row.update({"shape": list(actual.shape), "maximumAbsoluteDifference": float(difference.max()), "meanAbsoluteDifference": float(difference.mean()),
                            "nonfiniteValues": int(np.count_nonzero(~np.isfinite(actual))), "outsideToleranceValues": int(np.count_nonzero(~close)),
                            "bitExact": bool(np.array_equal(actual, expected)),
                            "status": "within-tolerance" if bool(close.all()) and bool(np.isfinite(actual).all()) else "feature-tolerance-failed"})
                print(case["id"], row["status"], flush=True)
                save()
        report.pop("phase", None)
        report["status"] = "six-task-feature-parity-within-tolerance" if all(row["status"] == "within-tolerance" for row in report["cases"]) else "feature-tolerance-failed"
        save()
        if report["status"] != "six-task-feature-parity-within-tolerance":
            raise SystemExit(1)
    except Exception as error:
        report["status"] = "failed"
        report["error"] = repr(error)
        save()
        raise


if __name__ == "__main__":
    main()
