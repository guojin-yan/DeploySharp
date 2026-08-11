#!/usr/bin/env python3
"""Export and verify the pinned Wav2Vec2 CTC model on one real LibriSpeech row."""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import time
import wave
from collections import Counter
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
import openvino as ov
import torch
import transformers
from transformers import Wav2Vec2ForCTC, Wav2Vec2Processor


MODEL_REVISION = "22aad52d435eb6dbaf354bdad9b0da84ce7d6156"
DATASET_REVISION = "71cacbfb7e2354c4226d01e70d77d5fca3d04ba1"
DATASET_ROW_ID = "6930-75918-0000"
DATASET_TEXT = "CONCORD RETURNED TO ITS PLACE AMIDST THE TENTS"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_array(value: np.ndarray) -> str:
    return hashlib.sha256(value.astype("<f4", copy=False).tobytes()).hexdigest()


def array_summary(value: np.ndarray) -> dict[str, object]:
    source = value.astype(np.float64, copy=False)
    return {
        "shape": list(value.shape),
        "sha256": sha256_array(value),
        "minimum": float(source.min()),
        "maximum": float(source.max()),
        "mean": float(source.mean()),
    }


def read_pcm16_mono(path: Path) -> tuple[np.ndarray, dict[str, object]]:
    with wave.open(str(path), "rb") as stream:
        channels = stream.getnchannels()
        sample_width = stream.getsampwidth()
        sample_rate = stream.getframerate()
        frame_count = stream.getnframes()
        compression = stream.getcomptype()
        frames = stream.readframes(frame_count)
    if channels != 1 or sample_width != 2 or sample_rate != 16000 or compression != "NONE":
        raise RuntimeError(
            f"Expected mono 16-bit PCM at 16000 Hz, got channels={channels}, "
            f"width={sample_width}, rate={sample_rate}, compression={compression}."
        )
    samples = np.frombuffer(frames, dtype="<i2").astype(np.float32) / 32768.0
    return samples, {
        "path": path.name,
        "size": path.stat().st_size,
        "sha256": sha256_file(path),
        "encoding": "signed-int16-little-endian",
        "sampleRate": sample_rate,
        "channels": channels,
        "frameCount": frame_count,
        "durationSeconds": frame_count / sample_rate,
    }


def stable_softmax_selected(logits: np.ndarray, token_ids: np.ndarray) -> np.ndarray:
    maximum = logits.max(axis=-1, keepdims=True)
    denominator = np.exp(logits - maximum).sum(axis=-1)
    selected = logits[np.arange(logits.shape[0]), token_ids]
    return np.exp(selected - maximum[:, 0]) / denominator


def ctc_segments(
    token_ids: np.ndarray,
    logits: np.ndarray,
    id_to_token: dict[int, str],
    blank_id: int,
    word_delimiter: str,
    frame_seconds: float,
) -> tuple[str, list[dict[str, object]], list[dict[str, object]]]:
    selected_probabilities = stable_softmax_selected(logits, token_ids)
    segments: list[dict[str, object]] = []
    decisions: list[dict[str, object]] = []
    previous = -1
    for frame, token_id_value in enumerate(token_ids.tolist()):
        token_id = int(token_id_value)
        token = id_to_token.get(token_id, "<unknown-id>")
        decisions.append(
            {
                "frame": frame,
                "tokenId": token_id,
                "token": token,
                "logit": float(logits[frame, token_id]),
                "probability": float(selected_probabilities[frame]),
            }
        )
        if token_id != blank_id and token_id != previous:
            segments.append(
                {
                    "tokenId": token_id,
                    "token": token,
                    "startFrame": frame,
                    "endFrameExclusive": frame + 1,
                    "startSeconds": frame * frame_seconds,
                    "endSeconds": (frame + 1) * frame_seconds,
                    "probabilitySum": float(selected_probabilities[frame]),
                    "frameCount": 1,
                }
            )
        elif token_id != blank_id and token_id == previous:
            segment = segments[-1]
            segment["endFrameExclusive"] = frame + 1
            segment["endSeconds"] = (frame + 1) * frame_seconds
            segment["probabilitySum"] = float(segment["probabilitySum"]) + float(selected_probabilities[frame])
            segment["frameCount"] = int(segment["frameCount"]) + 1
        previous = token_id
    pieces: list[str] = []
    for segment in segments:
        count = int(segment.pop("frameCount"))
        probability_sum = float(segment.pop("probabilitySum"))
        segment["meanSelectedProbability"] = probability_sum / count
        token = str(segment["token"])
        if token == word_delimiter:
            pieces.append(" ")
        elif token.startswith("<") and token.endswith(">"):
            if token == "<unk>":
                pieces.append(token)
        else:
            pieces.append(token)
    return "".join(pieces).strip(), segments, decisions


class Wav2Vec2CtcExport(torch.nn.Module):
    def __init__(self, model: Wav2Vec2ForCTC) -> None:
        super().__init__()
        self.model = model

    def forward(self, input_values: torch.Tensor) -> torch.Tensor:
        return self.model(input_values=input_values, return_dict=False)[0]


def onnx_metadata(path: Path) -> dict[str, object]:
    model = onnx.load(str(path), load_external_data=False)
    onnx.checker.check_model(model)
    return {
        "irVersion": model.ir_version,
        "opsets": [{"domain": item.domain, "version": item.version} for item in model.opset_import],
        "nodeCount": len(model.graph.node),
        "nodeTypes": dict(sorted(Counter(node.op_type for node in model.graph.node).items())),
        "inputs": [value.name for value in model.graph.input],
        "outputs": [value.name for value in model.graph.output],
        "externalData": any(initializer.data_location == onnx.TensorProto.EXTERNAL for initializer in model.graph.initializer),
    }


def ort_run(path: Path, values: np.ndarray) -> tuple[np.ndarray, dict[str, object], float, float]:
    started = time.perf_counter()
    session = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
    compile_ms = (time.perf_counter() - started) * 1000.0
    started = time.perf_counter()
    result = session.run(["logits"], {"input_values": values})[0]
    inference_ms = (time.perf_counter() - started) * 1000.0
    metadata = {
        "providers": session.get_providers(),
        "inputs": [{"name": item.name, "type": item.type, "shape": item.shape} for item in session.get_inputs()],
        "outputs": [{"name": item.name, "type": item.type, "shape": item.shape} for item in session.get_outputs()],
    }
    return result, metadata, compile_ms, inference_ms


def openvino_run(path: Path, values: np.ndarray) -> tuple[np.ndarray, dict[str, object], float, float]:
    core = ov.Core()
    started = time.perf_counter()
    compiled = core.compile_model(str(path), "CPU")
    compile_ms = (time.perf_counter() - started) * 1000.0
    started = time.perf_counter()
    output = compiled({"input_values": values})
    result = np.array(output[compiled.output("logits")], copy=True)
    inference_ms = (time.perf_counter() - started) * 1000.0
    metadata = {
        "device": "CPU",
        "inputs": [{"name": item.any_name, "type": str(item.element_type), "shape": str(item.partial_shape)} for item in compiled.inputs],
        "outputs": [{"name": item.any_name, "type": str(item.element_type), "shape": str(item.partial_shape)} for item in compiled.outputs],
    }
    return result, metadata, compile_ms, inference_ms


def backend_evidence(
    name: str,
    logits: np.ndarray,
    official_logits: np.ndarray,
    official_ids: np.ndarray,
    processor: Wav2Vec2Processor,
    id_to_token: dict[int, str],
    blank_id: int,
    word_delimiter: str,
    frame_seconds: float,
    metadata: dict[str, object],
    compile_ms: float,
    inference_ms: float,
) -> dict[str, object]:
    token_ids = logits.argmax(axis=-1)[0].astype(np.int64)
    if not np.array_equal(token_ids, official_ids):
        mismatch = np.flatnonzero(token_ids != official_ids)
        raise RuntimeError(f"{name} CTC decisions differ at frames {mismatch[:16].tolist()}")
    transcript, segments, decisions = ctc_segments(token_ids, logits[0], id_to_token, blank_id, word_delimiter, frame_seconds)
    processor_transcript = processor.batch_decode(token_ids[np.newaxis, :])[0]
    difference = np.abs(logits.astype(np.float64) - official_logits.astype(np.float64))
    return {
        "schemaVersion": "1.0",
        "backend": name,
        "modelRevision": MODEL_REVISION,
        "compileMilliseconds": compile_ms,
        "inferenceMilliseconds": inference_ms,
        "metadata": metadata,
        "logits": array_summary(logits),
        "officialLogitsMaximumAbsoluteDifference": float(difference.max()),
        "officialLogitsMeanAbsoluteDifference": float(difference.mean()),
        "ctcDecisionSha256": hashlib.sha256(token_ids.astype("<i8").tobytes()).hexdigest(),
        "transcript": transcript,
        "processorTranscript": processor_transcript,
        "segments": segments,
        "frameDecisions": decisions,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", type=Path, required=True)
    args = parser.parse_args()

    root = args.model_root.resolve()
    checkpoint = root / "checkpoint"
    dataset = root / "dataset" / f"{DATASET_ROW_ID}.wav"
    onnx_dir = root / "onnx"
    openvino_dir = root / "openvino"
    evidence_dir = root / "evidence" / DATASET_ROW_ID
    onnx_dir.mkdir(parents=True, exist_ok=True)
    openvino_dir.mkdir(parents=True, exist_ok=True)
    evidence_dir.mkdir(parents=True, exist_ok=True)

    samples, source = read_pcm16_mono(dataset)
    source.update(
        {
            "dataset": "openslr/librispeech_asr",
            "datasetRevision": DATASET_REVISION,
            "datasetLicense": "CC-BY-4.0",
            "config": "clean",
            "split": "test",
            "rowIndex": 0,
            "rowId": DATASET_ROW_ID,
            "speakerId": 6930,
            "chapterId": 75918,
            "referenceText": DATASET_TEXT,
            "authorization": "Official CC-BY-4.0 dataset row; external evidence only; redistribution not approved.",
        }
    )
    (root / "dataset" / "source-audit.json").write_text(json.dumps(source, indent=2) + "\n", encoding="utf-8")

    processor = Wav2Vec2Processor.from_pretrained(checkpoint, local_files_only=True)
    model = Wav2Vec2ForCTC.from_pretrained(checkpoint, local_files_only=True)
    model.eval()
    inputs = processor(samples, sampling_rate=16000, return_tensors="pt")
    if list(inputs.keys()) != ["input_values"]:
        raise RuntimeError(f"Unexpected processor keys: {list(inputs.keys())}")
    input_values = inputs.input_values.detach().cpu().numpy().astype(np.float32, copy=False)
    input_values.astype("<f4", copy=False).tofile(evidence_dir / "input-values.f32")

    started = time.perf_counter()
    with torch.inference_mode():
        official_logits = model(input_values=inputs.input_values).logits.detach().cpu().numpy().astype(np.float32, copy=False)
    official_ms = (time.perf_counter() - started) * 1000.0
    official_logits.astype("<f4", copy=False).tofile(evidence_dir / "official-logits.f32")
    official_ids = official_logits.argmax(axis=-1)[0].astype(np.int64)

    vocabulary = json.loads((checkpoint / "vocab.json").read_text(encoding="utf-8"))
    id_to_token = {int(token_id): token for token, token_id in vocabulary.items()}
    blank_id = int(processor.tokenizer.pad_token_id)
    word_delimiter = str(processor.tokenizer.word_delimiter_token)
    frame_seconds = float(model.config.inputs_to_logits_ratio) / 16000.0
    official_transcript, official_segments, official_decisions = ctc_segments(
        official_ids, official_logits[0], id_to_token, blank_id, word_delimiter, frame_seconds
    )
    processor_transcript = processor.batch_decode(official_ids[np.newaxis, :])[0]
    if official_transcript != processor_transcript or processor_transcript != DATASET_TEXT:
        raise RuntimeError(
            f"Transcript mismatch: managed={official_transcript!r}, processor={processor_transcript!r}, expected={DATASET_TEXT!r}"
        )

    official = {
        "schemaVersion": "1.0",
        "model": "facebook/wav2vec2-base-960h",
        "modelRevision": MODEL_REVISION,
        "modelLicense": "Apache-2.0",
        "source": source,
        "processorId": "wav2vec2-feature-extractor-do-normalize-16000-v1",
        "tokenizerId": "facebook-wav2vec2-base-960h-vocab-22aad52",
        "sampleRate": 16000,
        "channels": 1,
        "frameStrideSamples": int(model.config.inputs_to_logits_ratio),
        "frameSeconds": frame_seconds,
        "blankTokenId": blank_id,
        "wordDelimiterToken": word_delimiter,
        "inputValues": array_summary(input_values),
        "logits": array_summary(official_logits),
        "predictorMilliseconds": official_ms,
        "ctcDecisionSha256": hashlib.sha256(official_ids.astype("<i8").tobytes()).hexdigest(),
        "transcript": official_transcript,
        "processorTranscript": processor_transcript,
        "segments": official_segments,
        "frameDecisions": official_decisions,
        "environment": {
            "python": platform.python_version(),
            "torch": torch.__version__,
            "transformers": transformers.__version__,
            "onnx": onnx.__version__,
            "onnxRuntime": ort.__version__,
            "openvino": ov.__version__,
            "numpy": np.__version__,
            "platform": platform.platform(),
        },
    }
    (evidence_dir / "official-predictor.json").write_text(json.dumps(official, indent=2) + "\n", encoding="utf-8")

    onnx_path = onnx_dir / "wav2vec2-base-960h-ctc.onnx"
    export = Wav2Vec2CtcExport(model)
    started = time.perf_counter()
    torch.onnx.export(
        export,
        (inputs.input_values,),
        str(onnx_path),
        input_names=["input_values"],
        output_names=["logits"],
        dynamic_axes={"input_values": {0: "batch", 1: "samples"}, "logits": {0: "batch", 1: "frames"}},
        opset_version=17,
        do_constant_folding=True,
        dynamo=False,
    )
    export_ms = (time.perf_counter() - started) * 1000.0
    model_metadata = onnx_metadata(onnx_path)

    core = ov.Core()
    started = time.perf_counter()
    openvino_model = core.read_model(str(onnx_path))
    xml_path = openvino_dir / "wav2vec2-base-960h-ctc.xml"
    ov.save_model(openvino_model, xml_path, compress_to_fp16=False)
    conversion_ms = (time.perf_counter() - started) * 1000.0

    conversion = {
        "schemaVersion": "1.0",
        "sourceRevision": MODEL_REVISION,
        "torchExporter": "torch.onnx.export legacy tracer",
        "torch": torch.__version__,
        "opset": 17,
        "dynamo": False,
        "constantFolding": True,
        "dynamicAxes": {"input_values": ["batch", "samples"], "logits": ["batch", "frames", 32]},
        "quantization": "none",
        "precision": "FP32",
        "externalData": False,
        "exportMilliseconds": export_ms,
        "openvino": ov.__version__,
        "openvinoConversionMilliseconds": conversion_ms,
        "onnxMetadata": model_metadata,
        "files": [
            {"path": "onnx/wav2vec2-base-960h-ctc.onnx", "size": onnx_path.stat().st_size, "sha256": sha256_file(onnx_path)},
            {"path": "openvino/wav2vec2-base-960h-ctc.xml", "size": xml_path.stat().st_size, "sha256": sha256_file(xml_path)},
            {"path": "openvino/wav2vec2-base-960h-ctc.bin", "size": xml_path.with_suffix(".bin").stat().st_size, "sha256": sha256_file(xml_path.with_suffix(".bin"))},
        ],
    }
    (root / "evidence" / "conversion.json").write_text(json.dumps(conversion, indent=2) + "\n", encoding="utf-8")

    ort_logits, ort_metadata, ort_compile_ms, ort_inference_ms = ort_run(onnx_path, input_values)
    ov_logits, ov_metadata, ov_compile_ms, ov_inference_ms = openvino_run(xml_path, input_values)
    ort_evidence = backend_evidence(
        "onnxruntime-cpu", ort_logits, official_logits, official_ids, processor, id_to_token, blank_id,
        word_delimiter, frame_seconds, ort_metadata, ort_compile_ms, ort_inference_ms
    )
    ov_evidence = backend_evidence(
        "openvino-cpu", ov_logits, official_logits, official_ids, processor, id_to_token, blank_id,
        word_delimiter, frame_seconds, ov_metadata, ov_compile_ms, ov_inference_ms
    )
    if ort_evidence["transcript"] != DATASET_TEXT or ov_evidence["transcript"] != DATASET_TEXT:
        raise RuntimeError("A backend transcript differs from the licensed dataset reference.")
    (evidence_dir / "python-ort.json").write_text(json.dumps(ort_evidence, indent=2) + "\n", encoding="utf-8")
    (evidence_dir / "python-openvino.json").write_text(json.dumps(ov_evidence, indent=2) + "\n", encoding="utf-8")
    print("DEPLOYSHARP_STAGE28_WAV2VEC2_EXPORT_EVIDENCE_OK")


if __name__ == "__main__":
    main()
