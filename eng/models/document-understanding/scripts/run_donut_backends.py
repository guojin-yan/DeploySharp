#!/usr/bin/env python3
"""Run the exported Donut bundle with exact named ORT or OpenVINO ports."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import time
from pathlib import Path
from typing import Callable

import numpy as np
import onnxruntime as ort
import openvino as ov
from transformers import DonutProcessor


def sha256_bytes(value: np.ndarray) -> str:
    return hashlib.sha256(value.astype("<f4", copy=False).tobytes()).hexdigest()


def shape_summary(value: np.ndarray) -> dict[str, object]:
    source = value.astype(np.float64, copy=False)
    return {
        "shape": list(value.shape),
        "sha256": sha256_bytes(value),
        "minimum": float(source.min()),
        "maximum": float(source.max()),
        "mean": float(source.mean()),
    }


def clean_sequence(processor: DonutProcessor, sequence: str) -> str:
    sequence = sequence.replace(processor.tokenizer.eos_token, "")
    sequence = sequence.replace(processor.tokenizer.pad_token, "")
    return re.sub(r"<.*?>", "", sequence, count=1).strip()


def ort_runner(path: Path) -> tuple[Callable[[dict[str, np.ndarray]], dict[str, np.ndarray]], list[dict[str, object]], list[dict[str, object]]]:
    session = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
    inputs = [{"name": value.name, "type": value.type, "shape": value.shape} for value in session.get_inputs()]
    outputs = [{"name": value.name, "type": value.type, "shape": value.shape} for value in session.get_outputs()]

    def run(values: dict[str, np.ndarray]) -> dict[str, np.ndarray]:
        return {name: value for name, value in zip([item["name"] for item in outputs], session.run(None, values))}

    return run, inputs, outputs


def openvino_runner(path: Path) -> tuple[Callable[[dict[str, np.ndarray]], dict[str, np.ndarray]], list[dict[str, object]], list[dict[str, object]]]:
    core = ov.Core()
    compiled = core.compile_model(str(path), "CPU")
    inputs = [{"name": value.any_name, "type": str(value.element_type), "shape": str(value.partial_shape)} for value in compiled.inputs]
    outputs = [{"name": value.any_name, "type": str(value.element_type), "shape": str(value.partial_shape)} for value in compiled.outputs]

    def run(values: dict[str, np.ndarray]) -> dict[str, np.ndarray]:
        result = compiled(values)
        return {port.any_name: np.array(value, copy=True) for port, value in result.items()}

    return run, inputs, outputs


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--backend", choices=("ort", "openvino"), required=True)
    parser.add_argument("--model-directory", default="onnx")
    args = parser.parse_args()

    model_directory = args.model_root / args.model_directory
    extension = ".xml" if args.model_directory == "openvino" else ".onnx"
    evidence = args.model_root / "evidence" / "cord-test-0"
    official = json.loads((evidence / "official-predictor.json").read_text(encoding="utf-8"))
    processor = DonutProcessor.from_pretrained(args.model_root / "checkpoint", local_files_only=True, use_fast=False)
    pixels = np.fromfile(evidence / "pixel-values.f32", dtype="<f4").reshape(1, 3, 1280, 960)
    prompt = np.array([official["promptIds"]], dtype=np.int64)

    factory = ort_runner if args.backend == "ort" else openvino_runner
    started = time.perf_counter()
    encoder, encoder_inputs, encoder_outputs = factory(model_directory / f"encoder_model{extension}")
    prefill, prefill_inputs, prefill_outputs = factory(model_directory / f"decoder_model{extension}")
    decode, decode_inputs, decode_outputs = factory(model_directory / f"decoder_with_past_model{extension}")
    compile_ms = (time.perf_counter() - started) * 1000.0

    started = time.perf_counter()
    encoder_result = encoder({"pixel_values": pixels})
    encoder_ms = (time.perf_counter() - started) * 1000.0
    hidden = encoder_result["last_hidden_state"]

    started = time.perf_counter()
    current = prefill({"input_ids": prompt, "encoder_hidden_states": hidden})
    prefill_ms = (time.perf_counter() - started) * 1000.0

    sequence = list(official["promptIds"])
    top_tokens: list[dict[str, object]] = []
    decode_timings: list[float] = []
    first_logits = current["logits"]
    maximum_length = 768
    unk_id = int(processor.tokenizer.unk_token_id)
    eos_id = int(processor.tokenizer.eos_token_id)
    for step in range(maximum_length - len(sequence)):
        logits = current["logits"][0, -1].astype(np.float64, copy=True)
        if np.isnan(logits).any() or np.isposinf(logits).any():
            raise RuntimeError(f"Invalid logits at {args.backend} step {step}")
        logits[unk_id] = -np.inf
        token = int(np.argmax(logits))
        top_tokens.append({"step": step, "tokenId": token, "logit": float(logits[token])})
        sequence.append(token)
        if token == eos_id:
            break
        values: dict[str, np.ndarray] = {"input_ids": np.array([[token]], dtype=np.int64)}
        for layer in range(4):
            values[f"past_key_values.{layer}.decoder.key"] = current[f"present.{layer}.decoder.key"]
            values[f"past_key_values.{layer}.decoder.value"] = current[f"present.{layer}.decoder.value"]
            if step == 0:
                values[f"past_key_values.{layer}.encoder.key"] = current[f"present.{layer}.encoder.key"]
                values[f"past_key_values.{layer}.encoder.value"] = current[f"present.{layer}.encoder.value"]
            else:
                values[f"past_key_values.{layer}.encoder.key"] = encoder_kv[layer][0]
                values[f"past_key_values.{layer}.encoder.value"] = encoder_kv[layer][1]
        if step == 0:
            encoder_kv = [
                (
                    values[f"past_key_values.{layer}.encoder.key"],
                    values[f"past_key_values.{layer}.encoder.value"],
                )
                for layer in range(4)
            ]
        started = time.perf_counter()
        current = decode(values)
        decode_timings.append((time.perf_counter() - started) * 1000.0)
    else:
        raise RuntimeError(f"{args.backend} generation exceeded the 768-token contract")

    decoded = processor.batch_decode([sequence])[0]
    cleaned = clean_sequence(processor, decoded)
    structured = processor.token2json(cleaned)
    final_decoder_shape = list(current["present.0.decoder.key"].shape)
    result = {
        "schemaVersion": "1.0",
        "backend": args.backend,
        "modelDirectory": args.model_directory,
        "sequenceIds": sequence,
        "matchesOfficialTokens": sequence == official["sequenceIds"],
        "decodedText": decoded,
        "cleanedText": cleaned,
        "structured": structured,
        "matchesOfficialStructure": structured == official["structured"],
        "encoderHiddenState": shape_summary(hidden),
        "prefillLogits": shape_summary(first_logits),
        "finalDecoderKeyShape": final_decoder_shape,
        "topTokens": top_tokens,
        "ports": {
            "encoder": {"inputs": encoder_inputs, "outputs": encoder_outputs},
            "prefill": {"inputs": prefill_inputs, "outputs": prefill_outputs},
            "decode": {"inputs": decode_inputs, "outputs": decode_outputs},
        },
        "timingMilliseconds": {
            "compile": compile_ms,
            "encoder": encoder_ms,
            "prefill": prefill_ms,
            "decodeSteps": decode_timings,
            "decodeTotal": sum(decode_timings),
        },
    }
    suffix = "-ir" if args.model_directory == "openvino" else ""
    target = evidence / f"python-{args.backend}{suffix}.json"
    target.write_text(json.dumps(result, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    if not result["matchesOfficialTokens"] or not result["matchesOfficialStructure"]:
        raise RuntimeError(f"{args.backend} differs from the pinned official Predictor")
    print(
        f"DEPLOYSHARP_STAGE27_DONUT_{args.backend.upper()}_OK "
        f"tokens={len(sequence)} encoderMs={encoder_ms:.3f} decodeMs={sum(decode_timings):.3f}"
    )


if __name__ == "__main__":
    main()
