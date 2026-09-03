#!/usr/bin/env python3
"""Export and parity-check the pinned Whisper tiny.en three-graph contract.

This script is intentionally an admission tool, not a runtime implementation. It
produces an isolated ONNX bundle and evidence only after checking the encoder,
decoder-prefill, and named KV decoder graphs against the local Transformers model.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import time
from pathlib import Path
from typing import Iterable

import numpy as np
import onnx
import onnxruntime as ort
import torch
import transformers
from transformers import WhisperForConditionalGeneration
from transformers.cache_utils import EncoderDecoderCache


MODEL_REVISION = "87c7102498dcde7456f24cfd30239ca606ed9063"
VOCAB_SIZE = 51864
DECODER_START = 50257
NO_TIMESTAMPS = 50362
EOS = 50256
MEL_FRAMES = 3000
ENCODER_FRAMES = 1500
HIDDEN = 384
LAYERS = 4
HEADS = 6
HEAD_DIM = 64
OPSET = 17


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def assert_file(path: Path, expected_sha: str | None = None, expected_size: int | None = None) -> None:
    if not path.is_file():
        raise RuntimeError(f"Missing required checkpoint file: {path}")
    if expected_size is not None and path.stat().st_size != expected_size:
        raise RuntimeError(f"Unexpected size for {path.name}: {path.stat().st_size} != {expected_size}")
    if expected_sha is not None:
        actual = sha256_file(path)
        if actual.lower() != expected_sha.lower():
            raise RuntimeError(f"Unexpected SHA-256 for {path.name}: {actual} != {expected_sha}")


def tensor_numpy(value: torch.Tensor) -> np.ndarray:
    return value.detach().cpu().numpy().astype(np.float32, copy=False)


def cache_tensors(cache: EncoderDecoderCache) -> list[torch.Tensor]:
    values: list[torch.Tensor] = []
    for index in range(LAYERS):
        self_layer = cache.self_attention_cache.layers[index]
        cross_layer = cache.cross_attention_cache.layers[index]
        values.extend((self_layer.keys, self_layer.values, cross_layer.keys, cross_layer.values))
    return values


def make_cache(values: Iterable[torch.Tensor]) -> EncoderDecoderCache:
    tensors = list(values)
    if len(tensors) != LAYERS * 4:
        raise RuntimeError(f"Expected {LAYERS * 4} KV tensors, got {len(tensors)}")
    rows = [tuple(tensors[index:index + 4]) for index in range(0, len(tensors), 4)]
    return EncoderDecoderCache(rows)


class EncoderGraph(torch.nn.Module):
    def __init__(self, model: WhisperForConditionalGeneration) -> None:
        super().__init__()
        self.encoder = model.model.encoder

    def forward(self, input_features: torch.Tensor) -> torch.Tensor:
        return self.encoder(input_features=input_features, return_dict=False)[0]


class DecoderPrefillGraph(torch.nn.Module):
    def __init__(self, model: WhisperForConditionalGeneration) -> None:
        super().__init__()
        self.decoder = model.model.decoder
        self.projection = model.proj_out

    def forward(self, input_ids: torch.Tensor, encoder_hidden_states: torch.Tensor) -> tuple[torch.Tensor, ...]:
        output = self.decoder(
            input_ids=input_ids,
            encoder_hidden_states=encoder_hidden_states,
            use_cache=True,
            return_dict=True,
        )
        result: list[torch.Tensor] = [self.projection(output.last_hidden_state)]
        result.extend(cache_tensors(output.past_key_values))
        return tuple(result)


class DecoderWithPastGraph(torch.nn.Module):
    def __init__(self, model: WhisperForConditionalGeneration) -> None:
        super().__init__()
        self.decoder = model.model.decoder
        self.projection = model.proj_out

    def forward(self, input_ids: torch.Tensor, encoder_hidden_states: torch.Tensor, *past: torch.Tensor) -> tuple[torch.Tensor, ...]:
        output = self.decoder(
            input_ids=input_ids,
            # The non-null value keeps the cross-attention block in the graph;
            # because cross KV is already populated, ONNX removes this tensor
            # from Decode inputs and avoids a redundant transfer.
            encoder_hidden_states=encoder_hidden_states,
            past_key_values=make_cache(past),
            use_cache=True,
            return_dict=True,
        )
        result: list[torch.Tensor] = [self.projection(output.last_hidden_state)]
        result.extend(cache_tensors(output.past_key_values))
        return tuple(result)


def cache_names(prefix: str) -> list[str]:
    names: list[str] = []
    for layer in range(LAYERS):
        for decoder, kind in ((True, "decoder"), (False, "encoder")):
            for key in (True, False):
                suffix = "key" if key else "value"
                names.append(f"{prefix}.{layer}.{kind}.{suffix}")
    return names


def graph_metadata(path: Path) -> dict[str, object]:
    model = onnx.load(str(path), load_external_data=False)
    onnx.checker.check_model(model)
    return {
        "path": path.name,
        "size": path.stat().st_size,
        "sha256": sha256_file(path),
        "opset": next((item.version for item in model.opset_import if item.domain == ""), None),
        "inputs": [item.name for item in model.graph.input],
        "outputs": [item.name for item in model.graph.output],
        "nodes": len(model.graph.node),
    }


def export_graphs(model: WhisperForConditionalGeneration, output: Path) -> dict[str, object]:
    output.mkdir(parents=True, exist_ok=True)
    features = torch.zeros((1, 80, MEL_FRAMES), dtype=torch.float32)
    encoder_hidden = torch.zeros((1, ENCODER_FRAMES, HIDDEN), dtype=torch.float32)
    prefill_ids = torch.tensor([[DECODER_START, NO_TIMESTAMPS]], dtype=torch.int64)
    cache_shapes = [(1, HEADS, 2, HEAD_DIM), (1, HEADS, 2, HEAD_DIM), (1, HEADS, ENCODER_FRAMES, HEAD_DIM), (1, HEADS, ENCODER_FRAMES, HEAD_DIM)] * LAYERS
    past = tuple(torch.zeros(shape, dtype=torch.float32) for shape in cache_shapes)

    encoder_path = output / "whisper-tiny.en-encoder.onnx"
    prefill_path = output / "whisper-tiny.en-decoder-prefill.onnx"
    decode_path = output / "whisper-tiny.en-decoder-with-past.onnx"
    torch.onnx.export(
        EncoderGraph(model), (features,), str(encoder_path), input_names=["input_features"], output_names=["last_hidden_state"],
        dynamic_axes={"input_features": {0: "batch"}, "last_hidden_state": {0: "batch"}}, opset_version=OPSET,
        do_constant_folding=True, dynamo=False,
    )
    prefill_output_names = ["logits"] + cache_names("present")
    torch.onnx.export(
        DecoderPrefillGraph(model), (prefill_ids, encoder_hidden), str(prefill_path),
        input_names=["input_ids", "encoder_hidden_states"], output_names=prefill_output_names,
        dynamic_axes={"input_ids": {0: "batch", 1: "prompt_tokens"}, "encoder_hidden_states": {0: "batch"}, "logits": {0: "batch", 1: "prompt_tokens"}},
        opset_version=OPSET, do_constant_folding=True, dynamo=False,
    )
    past_names = cache_names("past_key_values")
    decode_output_names = ["logits"] + cache_names("present")
    dynamic_axes: dict[str, dict[int, str]] = {"input_ids": {0: "batch"}, "logits": {0: "batch"}}
    for index, name in enumerate(past_names):
        dynamic_axes[name] = {0: "batch"}
        if index % 4 < 2:
            dynamic_axes[name][2] = "past_tokens"
    for index, name in enumerate(decode_output_names[1:]):
        dynamic_axes[name] = {0: "batch"}
        if index % 4 < 2:
            dynamic_axes[name][2] = "present_tokens"
    torch.onnx.export(
        DecoderWithPastGraph(model), (torch.tensor([[NO_TIMESTAMPS]], dtype=torch.int64), encoder_hidden, *past), str(decode_path),
        input_names=["input_ids", "encoder_hidden_states"] + past_names, output_names=decode_output_names,
        dynamic_axes={**dynamic_axes, "encoder_hidden_states": {0: "batch"}}, opset_version=OPSET, do_constant_folding=True, dynamo=False,
    )
    return {"encoder": graph_metadata(encoder_path), "prefill": graph_metadata(prefill_path), "decode": graph_metadata(decode_path)}


def max_abs_difference(left: np.ndarray, right: np.ndarray) -> float:
    return float(np.max(np.abs(left.astype(np.float64) - right.astype(np.float64))))


def run_parity(model: WhisperForConditionalGeneration, output: Path) -> dict[str, object]:
    providers = ["CPUExecutionProvider"]
    encoder_session = ort.InferenceSession(str(output / "whisper-tiny.en-encoder.onnx"), providers=providers)
    prefill_session = ort.InferenceSession(str(output / "whisper-tiny.en-decoder-prefill.onnx"), providers=providers)
    decode_session = ort.InferenceSession(str(output / "whisper-tiny.en-decoder-with-past.onnx"), providers=providers)
    features = torch.linspace(-1.0, 1.0, 80 * MEL_FRAMES, dtype=torch.float32).reshape(1, 80, MEL_FRAMES)
    ids = torch.tensor([[DECODER_START, NO_TIMESTAMPS]], dtype=torch.int64)
    with torch.inference_mode():
        encoder = model.model.encoder(input_features=features, return_dict=True).last_hidden_state
        prefill = model.model.decoder(input_ids=ids, encoder_hidden_states=encoder, use_cache=True, return_dict=True)
        torch_prefill_logits = tensor_numpy(model.proj_out(prefill.last_hidden_state))
        torch_prefill_cache = cache_tensors(prefill.past_key_values)
    ort_encoder = encoder_session.run(["last_hidden_state"], {"input_features": tensor_numpy(features)})[0]
    ort_prefill = prefill_session.run(None, {"input_ids": ids.numpy(), "encoder_hidden_states": tensor_numpy(encoder)})
    if max_abs_difference(ort_encoder, tensor_numpy(encoder)) > 5e-3:
        raise RuntimeError("Whisper encoder parity failed.")
    prefill_diff = max_abs_difference(ort_prefill[0], torch_prefill_logits)
    if prefill_diff > 5e-3:
        raise RuntimeError(f"Whisper decoder prefill parity failed: max_abs={prefill_diff}")
    next_id = int(torch_prefill_logits[0, -1].argmax())
    torch_cache = list(torch_prefill_cache)
    ort_cache = [value for value in ort_prefill[1:]]
    decode_steps: list[dict[str, object]] = []
    for step in range(4):
        decode_ids = torch.tensor([[next_id]], dtype=torch.int64)
        with torch.inference_mode():
            torch_decode = model.model.decoder(
                input_ids=decode_ids, encoder_hidden_states=encoder,
                past_key_values=make_cache(torch_cache), use_cache=True, return_dict=True,
            )
            torch_decode_logits = tensor_numpy(model.proj_out(torch_decode.last_hidden_state))
            torch_next_cache = cache_tensors(torch_decode.past_key_values)
        decode_inputs: dict[str, np.ndarray] = {"input_ids": decode_ids.numpy()}
        for name, value in zip(cache_names("past_key_values"), ort_cache):
            decode_inputs[name] = value
        ort_decode = decode_session.run(None, decode_inputs)
        decode_diff = max_abs_difference(ort_decode[0], torch_decode_logits)
        cache_diff = max(max_abs_difference(left, tensor_numpy(right)) for left, right in zip(ort_decode[1:], torch_next_cache))
        if decode_diff > 5e-3 or cache_diff > 5e-3:
            raise RuntimeError(f"Whisper decoder-with-past parity failed at step {step}: logits={decode_diff}, cache={cache_diff}")
        torch_token = int(torch_decode_logits[0, -1].argmax())
        ort_token = int(ort_decode[0][0, -1].argmax())
        if torch_token != ort_token:
            raise RuntimeError(f"Whisper greedy token mismatch at step {step}: torch={torch_token}, ort={ort_token}")
        decode_steps.append({"step": step, "inputToken": next_id, "outputToken": ort_token, "logitsMaxAbsoluteDifference": decode_diff, "cacheMaxAbsoluteDifference": cache_diff})
        next_id = ort_token
        torch_cache = torch_next_cache
        ort_cache = [value for value in ort_decode[1:]]
    return {
        "provider": providers,
        "encoderMaxAbsoluteDifference": max_abs_difference(ort_encoder, tensor_numpy(encoder)),
        "prefillMaxAbsoluteDifference": prefill_diff,
        "decodeMaxAbsoluteDifference": max(float(item["logitsMaxAbsoluteDifference"]) for item in decode_steps),
        "decodeCacheMaxAbsoluteDifference": max(float(item["cacheMaxAbsoluteDifference"]) for item in decode_steps),
        "decodeSteps": decode_steps,
        "prefillNextToken": int(torch_prefill_logits[0, -1].argmax()),
        "decodeToken": decode_steps[-1]["outputToken"],
        "tokenParity": True,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True, help="Warehouse root containing checkpoint/")
    parser.add_argument("--output", type=Path, help="Output directory; defaults to <model-root>/onnx-whisper-three-graph")
    args = parser.parse_args()
    root = args.model_root.resolve()
    checkpoint = root / "checkpoint"
    output = (args.output or (root / "onnx-whisper-three-graph")).resolve()
    assert_file(checkpoint / "model.safetensors", "db59695928ded6043adaef491a53ef4e12da9611184d77c53baa691a60b958ad", 151060136)
    assert_file(checkpoint / "config.json", "b5366317df280f79e6cc366f259f379674f81a8721d458acf2e00680ccb10b1d", 1937)
    assert_file(checkpoint / "tokenizer.json", "5eb60cec1e77aeeb6869a2bb5a8e01a84c3fe5d072d75369343021fe6f5310d0", 2405679)
    assert_file(checkpoint / "generation_config.json", "38744c19d5cede6ff4dab5079c6d6ddc02ca726960bbef208fb602ad5a030eab", 1621)
    started = time.perf_counter()
    model = WhisperForConditionalGeneration.from_pretrained(checkpoint, local_files_only=True)
    model.eval()
    graphs = export_graphs(model, output)
    parity = run_parity(model, output)
    evidence = {
        "schemaVersion": "1.0",
        "status": "three-graph-export-parity-verified",
        "model": "openai/whisper-tiny.en",
        "modelRevision": MODEL_REVISION,
        "opset": OPSET,
        "contract": {
            "melShape": [1, 80, MEL_FRAMES], "encoderShape": [1, ENCODER_FRAMES, HIDDEN], "vocabularySize": VOCAB_SIZE,
            "decoderStartTokenId": DECODER_START, "noTimestampsTokenId": NO_TIMESTAMPS, "eosTokenId": EOS,
            "kvLayers": LAYERS, "kvHeads": HEADS, "kvHeadDimension": HEAD_DIM,
            "pastNames": cache_names("past_key_values"), "presentNames": cache_names("present"),
        },
        "graphs": graphs,
        "parity": parity,
        "environment": {"python": platform.python_version(), "torch": torch.__version__, "transformers": transformers.__version__, "onnx": onnx.__version__, "onnxruntime": ort.__version__, "numpy": np.__version__, "platform": platform.platform()},
        "elapsedMilliseconds": (time.perf_counter() - started) * 1000.0,
        "admissionNote": "This evidence does not enable the C# Whisper runtime; tokenizer, generation policy, and full audio parity remain a separate admission step.",
    }
    output.mkdir(parents=True, exist_ok=True)
    (output / "export-evidence.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    print("DEPLOYSHARP_WHISPER_THREE_GRAPH_EXPORT_PARITY_OK")
    print(json.dumps({"output": str(output), "graphs": graphs, "parity": parity}, indent=2))


if __name__ == "__main__":
    main()
