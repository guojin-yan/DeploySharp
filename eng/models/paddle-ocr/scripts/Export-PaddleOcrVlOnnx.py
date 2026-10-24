"""Export genuine dynamic, single-image FP32 PaddleOCR-VL-1.6 ONNX components.

Run in the dedicated pinned reference environment. This is conversion tooling,
not a claim that the DeploySharp C# generation Session has been implemented.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
import hashlib
import importlib.metadata
import inspect
import json
from pathlib import Path
import time

import numpy as np
import onnx
import onnxruntime as ort
import torch
from transformers import AutoModelForImageTextToText

from PaddleOcrVlOnnxModules import DynamicTextDecoder, LanguageHead, SingleImageVision, TokenEmbedding, single_image_positions

REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
WEIGHT_SHA256 = "85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db"
MODEL_SOURCE_SHA256 = "9471f809b2c3c4dcb722e578b6f91d33af08abd8b0c4047bad067c011f499d32"


def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def save(path, report):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    temporary.replace(path)


def difference(actual, expected):
    a, b = np.asarray(actual), np.asarray(expected)
    same = a.shape == b.shape
    return {"shape": list(a.shape), "referenceShape": list(b.shape), "sameShape": same,
            "maximumAbsoluteDifference": float(np.max(np.abs(a.astype(np.float64) - b.astype(np.float64)))) if same and a.size else 0.0 if same else None,
            "allClose": bool(same and np.allclose(a, b, rtol=1e-3, atol=3e-3))}


def export(module, inputs, path, input_names, output_names, axes):
    if path.exists():
        raise FileExistsError(f"Refusing to replace an existing export: {path}")
    start = time.perf_counter()
    with torch.inference_mode():
        torch.onnx.export(module.eval(), inputs, str(path), input_names=input_names, output_names=output_names,
            dynamic_axes=axes, opset_version=18, dynamo=False, external_data=True, do_constant_folding=True)
    onnx.checker.check_model(str(path))
    # Load metadata only; a serialized external-data model can exceed 2 GB.
    graph = onnx.load(str(path), load_external_data=False)
    def ports(values):
        return [{"name": value.name, "dtype": value.type.tensor_type.elem_type,
                 "shape": [dim.dim_param or dim.dim_value for dim in value.type.tensor_type.shape.dim]} for value in values]
    result = {"file": path.name, "bytes": path.stat().st_size, "sha256": digest(path),
              "checker": "passed", "inputs": ports(graph.graph.input), "outputs": ports(graph.graph.output),
              "exportMilliseconds": (time.perf_counter() - start) * 1000}
    del graph
    gc.collect()
    print(f"EXPORTED {path.name} bytes={result['bytes']}", flush=True)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--tensor-root", type=Path, required=True)
    parser.add_argument("--stage", choices=("vision", "embedding", "decoder", "head"), required=True)
    parser.add_argument("--threads", type=int, default=4)
    args = parser.parse_args()
    if args.threads < 1:
        parser.error("threads must be positive")
    torch.set_num_threads(args.threads)
    torch.manual_seed(0)
    weights = args.model_root / "model.safetensors"
    if weights.stat().st_size != 1917255968 or digest(weights) != WEIGHT_SHA256:
        raise ValueError("Official weight identity mismatch")
    reference = json.loads(args.reference.read_text(encoding="utf-8"))
    if reference["checkpoint"]["revision"] != REVISION or reference["checkpoint"]["weightSha256"] != WEIGHT_SHA256:
        raise ValueError("Reference checkpoint mismatch")
    args.output_root.mkdir(parents=True, exist_ok=True)
    report_path = args.output_root / f"{args.stage}-export.json"
    if report_path.exists():
        raise FileExistsError("Use a new output directory or preserve the existing stage report")
    report = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(), "stage": args.stage,
              "checkpointRevision": REVISION, "weightSha256": WEIGHT_SHA256, "referenceSha256": digest(args.reference),
              "scriptSha256": digest(Path(__file__)), "adaptersSha256": digest(Path(__file__).with_name("PaddleOcrVlOnnxModules.py")),
              "packages": {name: importlib.metadata.version(name) for name in ("torch", "transformers", "onnx", "onnxruntime", "onnxscript", "numpy")},
              "protocol": {"dtype": "float32", "device": "cpu", "threads": args.threads, "opset": 18,
                           "dynamicBatch": False, "singleUnpaddedImage": True, "rtol": 1e-3, "atol": 3e-3},
              "status": "running", "parity": [],
              "boundary": "Python CPU FP32 component export/parity; not a C# generation, quality, performance or multi-backend support claim."}
    save(report_path, report)
    try:
        model = AutoModelForImageTextToText.from_pretrained(str(args.model_root), local_files_only=True,
            dtype=torch.float32, attn_implementation="sdpa").eval()
        if digest(Path(inspect.getfile(type(model)))) != MODEL_SOURCE_SHA256:
            raise ValueError("Transformers model source differs from the validated reference")
        print(f"MODEL_LOADED stage={args.stage}", flush=True)
        path = args.output_root / f"{args.stage}.onnx"
        if args.stage == "vision":
            module = SingleImageVision(model)
            del model.model.language_model, model.lm_head
            gc.collect()
            # Deliberately export at a size absent from the six real images.
            sample = torch.zeros(4, 6, 3, 14, 14)
            report["graph"] = export(module, (sample,), path, ["pixel_patches"], ["image_features"],
                {"pixel_patches": {0: "grid_height", 1: "grid_width"}, "image_features": {0: "image_tokens"}})
            save(report_path, report)
            options = ort.SessionOptions()
            options.intra_op_num_threads = args.threads
            options.inter_op_num_threads = 1
            session = ort.InferenceSession(str(path), sess_options=options, providers=["CPUExecutionProvider"])
            for case in reference["cases"]:
                grid = case["imageGridThw"][0]
                raw = args.tensor_root / f"{case['id']}-pixel_values.bin"
                if digest(raw) != case["tensors"]["pixel_values"]["sha256"]:
                    raise ValueError("Saved reference pixel identity mismatch")
                pixels = np.fromfile(raw, dtype=np.float32).reshape(grid[1], grid[2], 3, 14, 14)
                with torch.inference_mode():
                    expected = model.model.get_image_features(torch.from_numpy(pixels.reshape(-1, 3, 14, 14)),
                        image_grid_thw=torch.tensor([grid])).pooler_output
                    if isinstance(expected, (list, tuple)):
                        expected = torch.cat(expected)
                    native = module(torch.from_numpy(pixels)).numpy()
                actual = session.run(None, {"pixel_patches": pixels})[0]
                row = {"id": case["id"], "grid": grid, "pixelsSha256": digest(raw),
                       "adapterVsReference": difference(native, expected.numpy()), "onnxVsReference": difference(actual, expected.numpy())}
                report["parity"].append(row)
                save(report_path, report)
                print(f"VISION_PARITY {case['id']} {row['onnxVsReference']}", flush=True)
        elif args.stage in ("embedding", "head"):
            module = TokenEmbedding(model) if args.stage == "embedding" else LanguageHead(model)
            del model
            gc.collect()
            if args.stage == "embedding":
                sample = torch.tensor([[100273, 100295, 65]], dtype=torch.long)
                names, outputs = ["input_ids"], ["inputs_embeds"]
                axes = {"input_ids": {1: "query_tokens"}, "inputs_embeds": {1: "query_tokens"}}
            else:
                sample = torch.zeros(1, 1, 1024)
                names, outputs = ["last_hidden_state"], ["logits"]
                axes = {}
            report["graph"] = export(module, (sample,), path, names, outputs, axes)
            options = ort.SessionOptions()
            options.intra_op_num_threads = args.threads
            session = ort.InferenceSession(str(path), sess_options=options, providers=["CPUExecutionProvider"])
            for length in ([1, 13, 173, 337, 1309] if args.stage == "embedding" else [1, 1, 1]):
                values = torch.arange(length).reshape(1, -1) * 73 % 103424 if args.stage == "embedding" else torch.randn(1, 1, 1024)
                with torch.inference_mode():
                    expected = module(values).numpy()
                row = {"queryTokens": length, **difference(session.run(None, {names[0]: values.numpy()})[0], expected)}
                report["parity"].append(row)
        else:
            module = DynamicTextDecoder(model)
            del model.model.visual, model.model.projector, model.lm_head
            del module.language.embed_tokens
            gc.collect()
            sample = (torch.randn(1, 3, 1024), torch.arange(3)[None, None].expand(3, 1, -1),
                      *(torch.zeros(1, 2, 5, 128) for _ in range(36)))
            inputs = ["inputs_embeds", "position_ids"] + [f"past_{kind}_{i}" for i in range(18) for kind in ("key", "value")]
            outputs = ["last_hidden_state"] + [f"present_{kind}_{i}" for i in range(18) for kind in ("key", "value")]
            axes = {"inputs_embeds": {1: "query_tokens"}, "position_ids": {2: "query_tokens"}}
            axes.update({name: {2: "past_tokens"} for name in inputs[2:]})
            axes.update({name: {2: "total_tokens"} for name in outputs[1:]})
            report["graph"] = export(module, sample, path, inputs, outputs, axes)
            save(report_path, report)
            options = ort.SessionOptions()
            options.intra_op_num_threads = args.threads
            options.inter_op_num_threads = 1
            session = ort.InferenceSession(str(path), sess_options=options, providers=["CPUExecutionProvider"])
            from transformers.cache_utils import DynamicCache
            for query, past_length, delta in [(7, 0, 0), (1, 7, -3), (3, 17, -11), (173, 0, -144), (1, 173, -144)]:
                hidden = torch.randn(1, query, 1024)
                positions = (torch.arange(query) + past_length + delta).repeat(3, 1)[:, None]
                old = tuple(torch.randn(1, 2, past_length, 128) for _ in range(36))
                cache = DynamicCache(config=module.language.config)
                for i in range(18):
                    cache.update(old[2 * i], old[2 * i + 1], i)
                with torch.inference_mode():
                    expected = module.language(inputs_embeds=hidden, position_ids=positions, past_key_values=cache, use_cache=True)
                    native = module(hidden, positions, *old)
                actual = session.run(None, {name: value.numpy() for name, value in zip(inputs, (hidden, positions, *old))})
                reference_outputs = (expected.last_hidden_state[:, -1:].numpy(),
                    *(tensor.numpy() for layer in cache.layers for tensor in (layer.keys, layer.values)))
                checks = [difference(a, b) for a, b in zip(actual, reference_outputs)]
                row = {"queryTokens": query, "pastTokens": past_length, "ropeDelta": delta,
                       "adapterVsReference": difference(native[0].numpy(), reference_outputs[0]),
                       "onnxVsReference": checks[0], "cacheChecks": checks[1:], "allClose": all(item["allClose"] for item in checks)}
                report["parity"].append(row)
                save(report_path, report)
                print(f"DECODER_PARITY query={query} past={past_length} close={row['allClose']}", flush=True)
        def passed(row):
            return row.get("allClose", True) and all(row.get(key, {}).get("allClose", True) for key in ("adapterVsReference", "onnxVsReference"))
        report["status"] = "export-and-component-parity-passed" if report["parity"] and all(passed(row) for row in report["parity"]) else "parity-failed"
        save(report_path, report)
        print(f"COMPLETED {args.stage} {report['status']}", flush=True)
        if report["status"] != "export-and-component-parity-passed":
            raise RuntimeError("Component parity failed; see exact recorded differences")
    except Exception as exc:
        report["status"] = "failed"
        report["error"] = f"{type(exc).__name__}: {exc}"
        save(report_path, report)
        raise


if __name__ == "__main__":
    main()
