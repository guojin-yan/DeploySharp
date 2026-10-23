"""Run pinned PaddleOCR-VL-1.6 assets with an explicit Transformers reference.

This is an offline Python correctness reference, not a DeploySharp ONNX backend.
Record both the checkpoint and actual library implementation. CPU FP32 is the
default so that BF16-only upstream examples are not copied onto unsupported GPUs.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import inspect
import json
import platform
import time
from pathlib import Path

from PIL import Image
import torch
import transformers
from transformers import AutoModelForImageTextToText, AutoProcessor

REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
WEIGHT_SHA256 = "85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db"
PROMPTS = {
    "ocr": "OCR:", "table": "Table Recognition:", "formula": "Formula Recognition:",
    "chart": "Chart Recognition:", "spotting": "Spotting:", "seal": "Seal Recognition:",
}


def file_hash(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


def tensor_record(tensor: torch.Tensor) -> dict:
    data = tensor.detach().cpu().contiguous().numpy()
    return {"shape": list(data.shape), "dtype": str(data.dtype), "bytes": data.nbytes,
            "sha256": hashlib.sha256(data.tobytes()).hexdigest(),
            "firstValues": data.reshape(-1)[:12].tolist(), "lastValues": data.reshape(-1)[-12:].tolist()}


def save_report(path: Path, report: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--tensor-output", type=Path)
    parser.add_argument("--processor-only", action="store_true")
    parser.add_argument("--processor-backend", choices=("pil", "torchvision"), default="pil")
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    parser.add_argument("--dtype", choices=("float32", "float16", "bfloat16"), default="float32")
    parser.add_argument("--attention", choices=("eager", "sdpa"), default="sdpa")
    parser.add_argument("--use-cache", action="store_true")
    parser.add_argument("--compare-uncached-tokens", type=int, default=0)
    parser.add_argument("--maximum-new-tokens", type=int, default=512)
    parser.add_argument("--threads", type=int, default=4)
    parser.add_argument("--case", action="append", default=[])
    args = parser.parse_args()
    if args.maximum_new_tokens < 1 or args.maximum_new_tokens > 4096:
        parser.error("maximum-new-tokens must be between 1 and 4096")
    if args.threads < 1 or args.compare_uncached_tokens < 0:
        parser.error("threads must be positive and compare-uncached-tokens non-negative")
    if args.device == "cpu" and args.dtype != "float32":
        parser.error("This CPU reference requires FP32; validate other precision separately")
    torch.set_num_threads(args.threads)
    torch.manual_seed(0)
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    if manifest["checkpointRevision"] != REVISION:
        raise ValueError("Input manifest refers to a different checkpoint revision")
    selected = [case for case in manifest["cases"] if not args.case or case["id"] in args.case]
    if not selected or (args.case and set(args.case) != {case["id"] for case in selected}):
        raise ValueError("Requested case is absent from the fixed manifest")
    processor = AutoProcessor.from_pretrained(str(args.model_root), local_files_only=True, backend=args.processor_backend)
    config = json.loads((args.model_root / "config.json").read_text(encoding="utf-8"))
    report = {
        "schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "checkpoint": {"repository": "PaddlePaddle/PaddleOCR-VL-1.6", "revision": REVISION,
                       "configSha256": file_hash(args.model_root / "config.json"),
                       "weightSha256": None, "weightVerified": False},
        "environment": {"os": platform.platform(), "machine": platform.machine(), "python": platform.python_version(),
                        "packages": {name: importlib.metadata.version(name) for name in ("torch", "torchvision", "transformers", "tokenizers", "safetensors", "numpy", "Pillow")},
                        "device": args.device, "dtype": args.dtype, "threads": args.threads,
                        "cudaAvailable": torch.cuda.is_available()},
        "protocol": {"processorOnly": args.processor_only, "processorBackend": args.processor_backend,
                     "attention": args.attention, "maximumNewTokens": args.maximum_new_tokens,
                     "requestedUseCache": args.use_cache, "checkpointUseCache": config["use_cache"],
                     "compareUncachedTokens": args.compare_uncached_tokens, "doSample": False,
                     "warmup": 0, "measured": 1, "performanceBoundary": "Correctness smoke timing only; not a formal 5+50 benchmark."},
        "implementation": {"processorClass": type(processor).__name__, "imageProcessorClass": type(processor.image_processor).__name__,
                           "processorModuleSha256": file_hash(Path(inspect.getfile(type(processor)))),
                           "imageProcessorModuleSha256": file_hash(Path(inspect.getfile(type(processor.image_processor)))),
                           "transformersVersion": transformers.__version__},
        "scriptSha256": file_hash(Path(__file__)), "manifestSha256": file_hash(args.manifest),
        "cases": [], "status": "running",
        "boundary": "Pinned official weights with the recorded Transformers built-in implementation; not an ONNX export, C# inference, quality benchmark or multi-backend support claim.",
    }
    model = None
    if not args.processor_only:
        weights = args.model_root / "model.safetensors"
        if weights.stat().st_size != 1917255968 or file_hash(weights) != WEIGHT_SHA256:
            raise ValueError("Model weights failed pinned official size/SHA256 verification")
        report["checkpoint"].update(weightSha256=WEIGHT_SHA256, weightVerified=True)
        print("WEIGHTS_VERIFIED", flush=True)
        start = time.perf_counter()
        model = AutoModelForImageTextToText.from_pretrained(str(args.model_root), local_files_only=True,
            dtype=getattr(torch, args.dtype), attn_implementation=args.attention).to(args.device).eval()
        report["modelLoadMilliseconds"] = (time.perf_counter() - start) * 1000
        report["implementation"].update(modelClass=type(model).__name__, modelModuleSha256=file_hash(Path(inspect.getfile(type(model)))))
        print(f"MODEL_LOADED {report['modelLoadMilliseconds']:.1f}ms", flush=True)
    save_report(args.output, report)
    for case in selected:
        task = case["task"]
        image_path = args.manifest.parent / case["image"]
        if file_hash(image_path) != case["sha256"]:
            raise ValueError(f"Fixed image identity mismatch: {case['id']}")
        row = {"id": case["id"], "task": task, "inputFile": case["image"], "inputSha256": case["sha256"],
               "source": case.get("source"), "scope": case.get("scope", "single image/region"), "status": "running"}
        report["cases"].append(row)
        save_report(args.output, report)
        try:
            image = Image.open(image_path).convert("RGB")
            row["sourceSize"] = list(image.size)
            if task == "spotting" and image.width < 1500 and image.height < 1500:
                image = image.resize((image.width * 2, image.height * 2), Image.Resampling.LANCZOS)
            row["preparedSize"] = list(image.size)
            messages = [{"role": "user", "content": [{"type": "image", "image": image}, {"type": "text", "text": PROMPTS[task]}]}]
            maximum_pixels = 1605632 if task == "spotting" else 1003520
            start = time.perf_counter()
            inputs = processor.apply_chat_template(messages, add_generation_prompt=True, tokenize=True,
                return_dict=True, return_tensors="pt", processor_kwargs={"images_kwargs": {"size": {"shortest_edge": 112896, "longest_edge": maximum_pixels}}})
            row["prepareMilliseconds"] = (time.perf_counter() - start) * 1000
            row["tensors"] = {name: tensor_record(value) for name, value in inputs.items() if isinstance(value, torch.Tensor)}
            row["inputTokenIds"] = inputs["input_ids"][0].tolist()
            row["imageGridThw"] = inputs["image_grid_thw"].tolist()
            row["imageTokenCount"] = row["inputTokenIds"].count(100295)
            grid = row["imageGridThw"][0]
            if row["inputTokenIds"][0] != 100273 or row["imageTokenCount"] != grid[0] * grid[1] * grid[2] // 4:
                raise ValueError("Actual processor tokens differ from official CLS/image-grid contract")
            if args.tensor_output:
                args.tensor_output.mkdir(parents=True, exist_ok=True)
                for name, value in inputs.items():
                    if isinstance(value, torch.Tensor):
                        path = args.tensor_output / f"{case['id']}-{name}.bin"
                        value.detach().cpu().contiguous().numpy().tofile(path)
            if model is not None:
                inputs = inputs.to(args.device)
                if "pixel_values" in inputs:
                    inputs["pixel_values"] = inputs["pixel_values"].to(getattr(torch, args.dtype))
                calls = []
                def capture(_module, _arguments, keywords):
                    ids = keywords.get("input_ids")
                    cache = keywords.get("past_key_values")
                    calls.append({"tokens": int(ids.shape[-1]) if ids is not None else None,
                                  "pastLength": int(cache.get_seq_length()) if cache is not None and hasattr(cache, "get_seq_length") else None,
                                  "pixelValuesPresent": keywords.get("pixel_values") is not None})
                hook = model.register_forward_pre_hook(capture, with_kwargs=True)
                try:
                    start = time.perf_counter()
                    with torch.inference_mode():
                        outputs = model.generate(**inputs, max_new_tokens=args.maximum_new_tokens, do_sample=False,
                                                 use_cache=args.use_cache, return_dict_in_generate=True)
                    if args.device == "cuda":
                        torch.cuda.synchronize()
                    row["generationMilliseconds"] = (time.perf_counter() - start) * 1000
                finally:
                    hook.remove()
                new_ids = outputs.sequences[0, inputs["input_ids"].shape[-1]:].detach().cpu().tolist()
                row["generatedTokenIds"] = new_ids
                row["forwardCalls"] = calls
                row["stopReason"] = "eos" if new_ids and new_ids[-1] == 2 else "maximum-new-tokens" if len(new_ids) == args.maximum_new_tokens else "other"
                row["decodedWithSpecialTokens"] = processor.decode(new_ids, skip_special_tokens=False)
                row["decoded"] = processor.decode(new_ids, skip_special_tokens=True)
                if args.compare_uncached_tokens:
                    limit = min(args.compare_uncached_tokens, args.maximum_new_tokens)
                    with torch.inference_mode():
                        uncached = model.generate(**inputs, max_new_tokens=limit, do_sample=False, use_cache=False)
                    reference_ids = uncached[0, inputs["input_ids"].shape[-1]:].detach().cpu().tolist()
                    row["uncachedComparison"] = {"maximumNewTokens": limit, "tokenIds": reference_ids,
                                                 "equal": reference_ids == new_ids[:len(reference_ids)]}
                    if not row["uncachedComparison"]["equal"]:
                        raise ValueError("Cached and uncached greedy token decisions diverged")
                row["status"] = "generated"
                print(f"GENERATED {case['id']} {len(new_ids)} tokens {row['stopReason']} {row['generationMilliseconds']:.1f}ms", flush=True)
            else:
                row["status"] = "processor-only"
                print(f"PREPARED {case['id']} {row['imageTokenCount']} image tokens", flush=True)
        except Exception as error:
            row.update(status="failed", errorType=type(error).__name__, errorMessage=str(error))
            report["status"] = "failed"
            save_report(args.output, report)
            raise
        save_report(args.output, report)
    report["status"] = "processor-only-complete" if args.processor_only else "reference-generation-complete"
    report["completedUtc"] = datetime.now(timezone.utc).isoformat()
    save_report(args.output, report)


if __name__ == "__main__":
    main()
