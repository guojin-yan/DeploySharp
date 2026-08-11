#!/usr/bin/env python3
"""Generate pinned official Donut/CORD processor and Predictor evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import re
import time
from pathlib import Path

import numpy as np
import onnx
import onnxruntime
import openvino
import pyarrow.parquet as parquet
import torch
import transformers
from PIL import Image
from transformers import DonutProcessor, VisionEncoderDecoderModel


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def save_f32(path: Path, value: torch.Tensor) -> dict[str, object]:
    array = value.detach().cpu().contiguous().numpy().astype("<f4", copy=False)
    path.write_bytes(array.tobytes())
    return {
        "relativePath": path.name,
        "shape": list(array.shape),
        "size": path.stat().st_size,
        "sha256": sha256(path),
        "minimum": float(array.min()),
        "maximum": float(array.max()),
        "mean": float(array.mean(dtype=np.float64)),
    }


def clean_sequence(processor: DonutProcessor, sequence: str) -> str:
    sequence = sequence.replace(processor.tokenizer.eos_token, "")
    sequence = sequence.replace(processor.tokenizer.pad_token, "")
    return re.sub(r"<.*?>", "", sequence, count=1).strip()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--sample-index", type=int, default=0)
    args = parser.parse_args()

    checkpoint = args.model_root / "checkpoint"
    evidence = args.model_root / "evidence" / f"cord-test-{args.sample_index}"
    evidence.mkdir(parents=True, exist_ok=True)

    table = parquet.read_table(args.dataset, columns=["image", "ground_truth"])
    row = table.slice(args.sample_index, 1).to_pylist()[0]
    image_bytes = row["image"]["bytes"]
    image_path = evidence / "document.png"
    image_path.write_bytes(image_bytes)
    (evidence / "ground-truth.json").write_text(row["ground_truth"] + "\n", encoding="utf-8")

    started = time.perf_counter()
    processor = DonutProcessor.from_pretrained(checkpoint, local_files_only=True, use_fast=False)
    model = VisionEncoderDecoderModel.from_pretrained(checkpoint, local_files_only=True)
    model.eval()
    load_ms = (time.perf_counter() - started) * 1000.0

    with Image.open(image_path) as opened:
        image = opened.convert("RGB")
        source_size = list(image.size)
        started = time.perf_counter()
        pixel_values = processor(image, return_tensors="pt").pixel_values
        preprocess_ms = (time.perf_counter() - started) * 1000.0

    task_prompt = "<s_cord-v2>"
    prompt_ids = processor.tokenizer(
        task_prompt, add_special_tokens=False, return_tensors="pt"
    ).input_ids

    with torch.inference_mode():
        started = time.perf_counter()
        encoder_hidden = model.encoder(pixel_values).last_hidden_state
        encoder_ms = (time.perf_counter() - started) * 1000.0

        started = time.perf_counter()
        prefill = model.decoder(
            input_ids=prompt_ids,
            encoder_hidden_states=encoder_hidden,
            use_cache=True,
            return_dict=True,
        )
        prefill_ms = (time.perf_counter() - started) * 1000.0

        started = time.perf_counter()
        generated = model.generate(
            pixel_values,
            decoder_input_ids=prompt_ids,
            max_length=model.decoder.config.max_position_embeddings,
            early_stopping=True,
            pad_token_id=processor.tokenizer.pad_token_id,
            eos_token_id=processor.tokenizer.eos_token_id,
            use_cache=True,
            num_beams=1,
            bad_words_ids=[[processor.tokenizer.unk_token_id]],
            return_dict_in_generate=True,
            output_scores=True,
        )
        generate_ms = (time.perf_counter() - started) * 1000.0

    sequence_ids = generated.sequences[0].detach().cpu().tolist()
    decoded = processor.batch_decode(generated.sequences)[0]
    cleaned = clean_sequence(processor, decoded)
    structured = processor.token2json(cleaned)
    top_tokens = []
    for index, score in enumerate(generated.scores):
        finite = torch.isfinite(score)
        if bool(torch.isnan(score).any()) or bool(torch.isposinf(score).any()) or not bool(finite.any()):
            raise RuntimeError(f"Invalid official score at generation step {index}")
        token_id = int(torch.argmax(score[0]).item())
        top_tokens.append({"step": index, "tokenId": token_id, "logit": float(score[0, token_id])})

    pixels = save_f32(evidence / "pixel-values.f32", pixel_values)
    features = save_f32(evidence / "encoder-hidden-state.f32", encoder_hidden)
    logits = save_f32(evidence / "prefill-logits.f32", prefill.logits)
    result = {
        "schemaVersion": "1.0",
        "modelRevision": "8003d433113256b4ce3a0f5bf604b29ff78a7451",
        "datasetRevision": "7f0115a4b758a71d6473b8d085751692da2fef98",
        "datasetLicense": "CC-BY-4.0",
        "sampleIndex": args.sample_index,
        "sourceSize": source_size,
        "imageSize": image_path.stat().st_size,
        "imageSha256": sha256(image_path),
        "groundTruthSha256": sha256(evidence / "ground-truth.json"),
        "taskPrompt": task_prompt,
        "promptIds": prompt_ids[0].tolist(),
        "sequenceIds": sequence_ids,
        "completionIds": sequence_ids[len(prompt_ids[0]) :],
        "decodedText": decoded,
        "cleanedText": cleaned,
        "structured": structured,
        "topTokens": top_tokens,
        "pixelValues": pixels,
        "encoderHiddenState": features,
        "prefillLogits": logits,
        "pastKeyValues": [
            {
                "layer": index,
                "values": [
                    list(value.shape) if isinstance(value, torch.Tensor) else None
                    for value in layer
                ],
            }
            for index, layer in enumerate(prefill.past_key_values)
        ],
        "timingMilliseconds": {
            "modelLoad": load_ms,
            "preprocess": preprocess_ms,
            "encoder": encoder_ms,
            "prefill": prefill_ms,
            "generate": generate_ms,
        },
        "environment": {
            "python": platform.python_version(),
            "torch": torch.__version__,
            "transformers": transformers.__version__,
            "onnx": onnx.__version__,
            "onnxRuntime": onnxruntime.__version__,
            "openvino": openvino.__version__,
            "numpy": np.__version__,
        },
    }
    (evidence / "official-predictor.json").write_text(
        json.dumps(result, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )
    print(
        "DEPLOYSHARP_STAGE27_DONUT_OFFICIAL_OK "
        f"tokens={len(sequence_ids)} fields={len(structured)} imageSha={result['imageSha256']}"
    )


if __name__ == "__main__":
    main()
