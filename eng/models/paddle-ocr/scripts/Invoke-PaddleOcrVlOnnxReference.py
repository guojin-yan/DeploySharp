"""Generate all six pinned real-image cases through four ONNX Runtime graphs.

Vision is run once per image and unloaded before language generation. Decoder
prefill uses empty explicit KV tensors; all following calls consume one token
and the previous 18 key/value pairs. No PyTorch model or weight loader runs.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
import hashlib
import importlib.metadata
import json
from pathlib import Path
import time

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def save(path, report):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    temporary.replace(path)


def options(threads, optimization="all"):
    value = ort.SessionOptions()
    value.intra_op_num_threads = threads
    value.inter_op_num_threads = 1
    # Do not retain O(patches^2) vision temporaries while keeping four models.
    value.enable_cpu_mem_arena = False
    value.enable_mem_pattern = False
    value.graph_optimization_level = {"all": ort.GraphOptimizationLevel.ORT_ENABLE_ALL,
        "basic": ort.GraphOptimizationLevel.ORT_ENABLE_BASIC, "none": ort.GraphOptimizationLevel.ORT_DISABLE_ALL}[optimization]
    return value


def first_difference(a, b):
    return next((i for i, pair in enumerate(zip(a, b)) if pair[0] != pair[1]), min(len(a), len(b)) if len(a) != len(b) else None)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--positions", type=Path, required=True)
    parser.add_argument("--tensor-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--maximum-new-tokens", type=int, default=512)
    parser.add_argument("--case", action="append", default=[])
    parser.add_argument("--vision-optimization", choices=("all", "basic", "none"), default="all")
    parser.add_argument("--diagnose-component-differences", action="store_true",
        help="Keep failed feature tolerances visible while testing whether they alter generated tokens; never promotes component status.")
    args = parser.parse_args()
    if not 1 <= args.maximum_new_tokens <= 4096 or args.threads < 1:
        parser.error("Use 1..4096 generated tokens and positive threads")
    if args.output.exists():
        raise FileExistsError("Preserve existing evidence and choose a new output report")
    reference = json.loads(args.reference.read_text(encoding="utf-8"))
    position_report = json.loads(args.positions.read_text(encoding="utf-8"))
    if reference["status"] != "reference-generation-complete" or position_report["status"] != "all-positions-exact":
        raise ValueError("Incomplete upstream or position reference")
    if position_report["referenceSha256"] != digest(args.reference):
        raise ValueError("Position report and generation reference identities differ")
    selected = [row for row in reference["cases"] if not args.case or row["id"] in args.case]
    if not selected or (args.case and set(args.case) != {row["id"] for row in selected}):
        raise ValueError("Unknown or empty real-image case selection")
    tokenizer_path = args.model_root / "tokenizer.json"
    if digest(tokenizer_path) != "c8a215a59183d0d0781adc33bacd3ce6162716f7fd568fb30234a74d69803a7d":
        raise ValueError("Pinned tokenizer identity mismatch")
    tokenizer = Tokenizer.from_file(str(tokenizer_path))
    positions = {row["id"]: row for row in position_report["cases"]}
    graphs, component_deviations = {}, []
    for name in ("vision", "embedding", "decoder", "head"):
        stage_path = args.bundle_root / f"{name}-export.json"
        stage = json.loads(stage_path.read_text(encoding="utf-8"))
        if stage["referenceSha256"] != digest(args.reference) or stage.get("graph", {}).get("checker") != "passed":
            raise ValueError(f"Unverified export component: {name}")
        if stage["status"] != "export-and-component-parity-passed":
            deviations = [row for row in stage.get("parity", []) if not row.get("onnxVsReference", {}).get("allClose", True)]
            if not args.diagnose_component_differences or name != "vision" or len(stage.get("parity", [])) != 6 or not deviations:
                raise ValueError(f"Failed component must be fixed or explicitly diagnosed: {name}")
            if any(not row.get("adapterVsReference", {}).get("allClose", False) for row in stage["parity"]):
                raise ValueError("A non-equivalent native vision adapter cannot be diagnosed as ONNX precision drift")
            component_deviations.append({"stage": name, "originalStatus": stage["status"], "cases": deviations,
                                         "toleranceUnchanged": True, "componentPromotedToPassed": False})
        path = args.bundle_root / stage["graph"]["file"]
        if digest(path) != stage["graph"]["sha256"]:
            raise ValueError(f"ONNX graph identity mismatch: {name}")
        graphs[name] = {"file": path.name, "sha256": digest(path), "exportReportSha256": digest(stage_path)}
    report = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "checkpoint": reference["checkpoint"], "graphs": graphs, "referenceSha256": digest(args.reference),
              "positionsSha256": digest(args.positions), "scriptSha256": digest(Path(__file__)),
              "environment": {"packages": {name: importlib.metadata.version(name) for name in ("onnxruntime", "numpy", "tokenizers")},
                              "provider": "CPUExecutionProvider", "threads": args.threads},
              "protocol": {"dtype": "float32", "maximumNewTokens": args.maximum_new_tokens, "warmup": 0, "measured": 1,
                           "sampling": "greedy", "visionPassesPerImage": 1, "initialPastTokens": 0,
                           "cacheLayers": 18, "keyValueHeads": 2, "headDimension": 128, "batch": 1},
              "status": "running", "cases": [], "componentDeviations": component_deviations,
              "boundary": "Python ONNX Runtime CPU generation from saved official pixels. Not a .NET pixel processor/Session, independent quality benchmark, GPU/multi-backend evidence or formal latency benchmark."}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    save(args.output, report)
    report["protocol"]["visionOptimization"] = args.vision_optimization
    report["protocol"]["componentDeviationsExplicitlyAllowed"] = args.diagnose_component_differences
    try:
        features = {}
        session = ort.InferenceSession(str(args.bundle_root / graphs["vision"]["file"]),
            sess_options=options(args.threads, args.vision_optimization), providers=["CPUExecutionProvider"])
        for case in selected:
            grid = case["imageGridThw"][0]
            raw = args.tensor_root / f"{case['id']}-pixel_values.bin"
            if digest(raw) != case["tensors"]["pixel_values"]["sha256"]:
                raise ValueError(f"Image patch identity mismatch: {case['id']}")
            pixels = np.fromfile(raw, dtype=np.float32).reshape(grid[1], grid[2], 3, 14, 14)
            start = time.perf_counter()
            result = session.run(None, {"pixel_patches": pixels})[0]
            duration = (time.perf_counter() - start) * 1000
            if result.shape != (grid[1] * grid[2] // 4, 1024) or not np.isfinite(result).all():
                raise ValueError("Invalid projected image features")
            features[case["id"]] = result
            row = {"id": case["id"], "task": case["task"], "sourceSize": case["sourceSize"], "gridThw": grid,
                   "inputSha256": case["inputSha256"], "pixelSha256": digest(raw), "visionMilliseconds": duration,
                   "visionPasses": 1, "status": "image-encoded"}
            report["cases"].append(row)
            save(args.output, report)
            print(f"ONNX_VISION {case['id']} {duration:.1f}ms", flush=True)
        del session, pixels
        gc.collect()
        sessions = {name: ort.InferenceSession(str(args.bundle_root / graphs[name]["file"]),
            sess_options=options(args.threads), providers=["CPUExecutionProvider"]) for name in ("embedding", "decoder", "head")}
        for case, row in zip(selected, report["cases"]):
            ids = np.asarray([case["inputTokenIds"]], dtype=np.int64)
            position = positions[case["id"]]
            if position["inputTokenIds"] != case["inputTokenIds"] or not position["allThreeEqual"]:
                raise ValueError("Prompt and position reference mismatch")
            prefix_positions = np.asarray(position["positionIds"], dtype=np.int64)
            embedding = sessions["embedding"].run(None, {"input_ids": ids})[0]
            image_indices = np.flatnonzero(ids[0] == 100295)
            if not np.array_equal(image_indices, np.arange(image_indices[0], image_indices[0] + len(image_indices))):
                raise ValueError("Single-image placeholders must be contiguous")
            embedding[0, image_indices] = features.pop(case["id"])
            cache = [np.empty((1, 2, 0, 128), dtype=np.float32) for _ in range(36)]
            prompt_length = ids.shape[1]
            generated, calls, decisions = [], [], []
            delta = position["ropeDelta"]
            start = time.perf_counter()
            for step in range(args.maximum_new_tokens):
                if step == 0:
                    values, current_positions, past_length = embedding, prefix_positions, 0
                else:
                    values = sessions["embedding"].run(None, {"input_ids": np.asarray([[generated[-1]]], dtype=np.int64)})[0]
                    past_length = prompt_length + step - 1
                    current_positions = np.full((3, 1, 1), past_length + delta, dtype=np.int64)
                inputs = {"inputs_embeds": values, "position_ids": current_positions}
                for layer in range(18):
                    inputs[f"past_key_{layer}"] = cache[2 * layer]
                    inputs[f"past_value_{layer}"] = cache[2 * layer + 1]
                result = sessions["decoder"].run(None, inputs)
                cache = result[1:]
                expected_length = prompt_length + step
                if len(cache) != 36 or any(tensor.shape != (1, 2, expected_length, 128) for tensor in cache):
                    raise ValueError("Dynamic KV length/axes mismatch")
                logits = sessions["head"].run(None, {"last_hidden_state": result[0]})[0]
                if logits.shape != (1, 1, 103424) or not np.isfinite(logits).all():
                    raise ValueError("Invalid lm_head logits")
                selected_token = int(logits.reshape(-1).argmax())
                top = np.sort(np.partition(logits.reshape(-1), -2)[-2:])
                generated.append(selected_token)
                calls.append({"queryTokens": int(values.shape[1]), "pastTokens": past_length, "presentTokens": expected_length})
                decisions.append({"tokenId": selected_token, "topTwoMargin": float(top[1] - top[0])})
                if selected_token == 2:
                    break
            row.update(status="generated", inputTokenIds=case["inputTokenIds"], generatedTokenIds=generated,
                generationMilliseconds=(time.perf_counter() - start) * 1000,
                stopReason="eos" if generated[-1] == 2 else "maximum-new-tokens", forwardCalls=calls,
                greedyDecisions=decisions, decoded=tokenizer.decode(generated, skip_special_tokens=True),
                decodedWithSpecialTokens=tokenizer.decode(generated, skip_special_tokens=False),
                tokenExact=generated == case["generatedTokenIds"], firstDifferentToken=first_difference(generated, case["generatedTokenIds"]))
            row["textExact"] = row["decoded"] == case["decoded"] and row["decodedWithSpecialTokens"] == case["decodedWithSpecialTokens"]
            save(args.output, report)
            print(f"ONNX_GENERATED {case['id']} tokens={len(generated)} stop={row['stopReason']} exact={row['tokenExact']}", flush=True)
        exact = len(selected) == 6 and {case["task"] for case in selected} == {"ocr", "table", "formula", "chart", "spotting", "seal"} and all(row["tokenExact"] and row["textExact"] and row["stopReason"] == "eos" for row in report["cases"])
        report["status"] = ("generation-exact-with-component-deviations" if component_deviations else "all-six-task-generation-exact") if exact else "generation-differences-recorded"
        save(args.output, report)
        print(f"COMPLETED {report['status']}", flush=True)
    except Exception as exc:
        report["status"] = "failed"
        report["error"] = f"{type(exc).__name__}: {exc}"
        save(args.output, report)
        raise


if __name__ == "__main__":
    main()
