"""Compare one saved DeploySharp/ONNX formula token trace with the official PaddleX predictor.

The predictor is run on the exact Float32 tensor captured from DeploySharp, so
this isolates model/export/runtime execution from image preprocessing differences.
Optionally run PaddleX's own preprocessing for the selected source image, save
that tensor, and compare it numerically with a separately captured DeploySharp
preprocessing tensor. Requires the acquired PaddleX/Paddle environment and NumPy;
writes only the explicitly selected output tensor and --output report.
"""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path

import numpy as np
import paddle
import paddlex
from paddlex import create_predictor


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def compact_latex(value: str) -> str:
    return "".join(value.split())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--trace-report", required=True, type=Path)
    parser.add_argument("--input-dump-dir", required=True, type=Path)
    parser.add_argument("--model-dir", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--official-preprocess-image", type=Path)
    parser.add_argument("--deploysharp-preprocess-reference", type=Path)
    parser.add_argument("--official-preprocessed-output", type=Path)
    parser.add_argument("--baseline-trace-report", type=Path)
    args = parser.parse_args()

    preprocess_args = (
        args.official_preprocess_image,
        args.deploysharp_preprocess_reference,
        args.official_preprocessed_output,
    )
    if any(value is not None for value in preprocess_args) and not all(value is not None for value in preprocess_args):
        raise ValueError(
            "--official-preprocess-image, --deploysharp-preprocess-reference and "
            "--official-preprocessed-output must be supplied together."
        )

    trace = json.loads(args.trace_report.read_text(encoding="utf-8-sig"))
    if len(trace.get("modelResults", [])) != 1:
        raise ValueError("The trace report must contain exactly one selected model.")
    model = trace["modelResults"][0]
    if model["model"] != "paddle-formula/pp-formulanet-l":
        raise ValueError(f"Expected pp-formulanet-l trace, got {model['model']}.")
    if len(model.get("results", [])) != 1:
        raise ValueError("The trace report must contain exactly one selected sample.")
    sample = model["results"][0]
    raw_ids = sample.get("rawOutputTokenIds")
    if not raw_ids:
        raise ValueError("Raw ONNX Runtime output token IDs are missing; rerun with token tracing enabled.")

    yaml_text = (args.model_dir / "inference.yml").read_text(encoding="utf-8")
    match = re.search(r"(?m)^\s*max_seq_len:\s*(\d+)", yaml_text)
    if not match:
        raise ValueError("Official inference.yml does not declare max_seq_len.")
    yaml_max_sequence_length = int(match.group(1))
    if yaml_max_sequence_length != model.get("yamlMaximumSequenceLength"):
        raise ValueError("Trace and official inference.yml max_seq_len values differ.")

    dump_name = sample.get("inputTensorDumpFile")
    if not dump_name:
        raise ValueError("Trace does not name the captured DeploySharp input tensor.")
    tensor_path = args.input_dump_dir / dump_name
    input_shape = tuple(int(value) for value in sample["inputShape"])
    input_tensor = np.fromfile(tensor_path, dtype="<f4")
    if input_tensor.size != int(np.prod(input_shape)):
        raise ValueError(f"Captured tensor length {input_tensor.size} does not match shape {input_shape}.")
    actual_input_sha = sha256(tensor_path)
    if actual_input_sha != sample["inputTensorSha256"]:
        raise ValueError("Captured tensor SHA-256 does not match the test report.")
    input_tensor = input_tensor.reshape(input_shape)

    predictor = create_predictor(
        "PP-FormulaNet-L", model_dir=str(args.model_dir), device="cpu", batch_size=1
    )
    preprocessing_comparison = None
    if args.official_preprocess_image is not None:
        image_path = args.official_preprocess_image.resolve()
        if not image_path.is_file():
            raise FileNotFoundError(f"Official preprocessing image does not exist: {image_path}")
        image_sha = sha256(image_path)
        if image_sha != sample["imageSha256"]:
            raise ValueError("Official preprocessing image SHA-256 differs from the traced sample.")
        raw_images = predictor.pre_tfs["Read"](imgs=[str(image_path)])
        decoded = predictor.pre_tfs["UniMERNetImgDecode"](imgs=raw_images)
        transformed = predictor.pre_tfs["UniMERNetTestTransform"](imgs=decoded)
        formatted = predictor.pre_tfs["LatexImageFormat"](imgs=transformed)
        if len(formatted) != 1 or formatted[0] is None:
            raise ValueError("PaddleX official preprocessing did not produce exactly one image tensor.")
        official_preprocessed = np.asarray(formatted[0])
        if official_preprocessed.dtype != np.float32 or tuple(official_preprocessed.shape) != input_shape:
            raise ValueError(
                f"PaddleX official preprocessing produced {official_preprocessed.dtype} "
                f"{official_preprocessed.shape}; expected float32 {input_shape}."
            )
        reference_tensor = np.fromfile(args.deploysharp_preprocess_reference, dtype="<f4")
        if reference_tensor.size != official_preprocessed.size:
            raise ValueError("PaddleX and DeploySharp preprocessing tensor element counts differ.")
        reference_tensor = reference_tensor.reshape(input_shape)
        difference = np.abs(official_preprocessed - reference_tensor)
        official_output = args.official_preprocessed_output
        official_output.parent.mkdir(parents=True, exist_ok=True)
        official_preprocessed.astype("<f4", copy=False).tofile(official_output)
        preprocessing_comparison = {
            "sourceImage": str(image_path),
            "sourceImageSha256": image_sha,
            "officialPreprocessedTensorSha256": sha256(official_output),
            "officialPreprocessedTensorPath": str(official_output.resolve()),
            "deploySharpPreprocessedTensorPath": str(args.deploysharp_preprocess_reference.resolve()),
            "deploySharpPreprocessedTensorSha256": sha256(args.deploysharp_preprocess_reference),
            "inputShape": list(input_shape),
            "elementCount": int(official_preprocessed.size),
            "exactElementMatchCount": int(np.count_nonzero(difference == 0)),
            "differentElementCount": int(np.count_nonzero(difference != 0)),
            "maximumAbsoluteDifference": float(difference.max(initial=0)),
            "meanAbsoluteDifference": float(difference.mean()) if difference.size else 0.0,
            "officialRange": [float(official_preprocessed.min()), float(official_preprocessed.max())],
            "deploySharpRange": [float(reference_tensor.min()), float(reference_tensor.max())],
            "method": "PaddleX Predictor pre_tfs Read -> UniMERNetImgDecode -> UniMERNetTestTransform -> LatexImageFormat",
        }
    official_batch = predictor.runner(x=[input_tensor])[0]
    official_ids = np.asarray(official_batch[0]).reshape(-1).astype(np.int64, copy=False)
    onnx_ids = np.asarray(raw_ids, dtype=np.int64).reshape(-1)
    exact_token_match = official_ids.shape == onnx_ids.shape and np.array_equal(official_ids, onnx_ids)
    first_difference = None
    if official_ids.shape == onnx_ids.shape:
        differences = np.flatnonzero(official_ids != onnx_ids)
        if differences.size:
            first_difference = int(differences[0])
    elif official_ids.shape != onnx_ids.shape:
        first_difference = int(min(official_ids.size, onnx_ids.size))

    official_latex = predictor.post_op([official_ids])[0]
    traced_latex = str(sample["prediction"])
    official_compact = compact_latex(official_latex)
    traced_compact = compact_latex(traced_latex)
    reference_rows = [
        json.loads(line)
        for line in args.manifest.read_text(encoding="utf-8-sig").splitlines()
        if line.strip()
    ]
    reference = next((row for row in reference_rows if row["Image"] == sample["image"]), None)
    if reference is None:
        raise ValueError(f"No reference-label row for {sample['image']}.")
    reference_compact = compact_latex(reference["ReferenceLatex"])

    eos_id = int(model["endTokenId"])
    eos_positions = np.flatnonzero(official_ids == eos_id)
    raw_starts_with_bos = bool(official_ids.size and official_ids[0] == int(model["startTokenId"]))
    generated_ids = official_ids[1:] if raw_starts_with_bos else official_ids
    reached_eos = bool(eos_positions.size)
    preprocessing_sensitivity = None
    if args.baseline_trace_report is not None:
        baseline_trace = json.loads(args.baseline_trace_report.read_text(encoding="utf-8-sig"))
        baseline_models = baseline_trace.get("modelResults", [])
        if len(baseline_models) != 1 or baseline_models[0].get("model") != model["model"]:
            raise ValueError("Baseline report must contain exactly the same single formula model.")
        baseline_results = baseline_models[0].get("results", [])
        if len(baseline_results) != 1:
            raise ValueError("Baseline report must contain exactly one formula sample.")
        baseline = baseline_results[0]
        if baseline.get("image") != sample["image"] or baseline.get("imageSha256") != sample["imageSha256"]:
            raise ValueError("Baseline and current traces must identify the same source image and SHA-256.")
        if baseline_models[0].get("endTokenId") != model["endTokenId"]:
            raise ValueError("Baseline and current traces disagree on the EOS token ID.")
        baseline_input_sha = baseline.get("inputTensorSha256")
        if baseline_input_sha == actual_input_sha:
            raise ValueError("Baseline and current preprocessing traces use the same input tensor hash.")
        baseline_ids_value = baseline.get("rawOutputTokenIds")
        if not baseline_ids_value:
            raise ValueError("Baseline trace does not contain raw token IDs; rerun with token tracing enabled.")
        baseline_ids = np.asarray(baseline_ids_value, dtype=np.int64).reshape(-1)
        common_length = min(baseline_ids.size, onnx_ids.size)
        differing_count = int(np.count_nonzero(baseline_ids[:common_length] != onnx_ids[:common_length]))
        differing_count += abs(int(baseline_ids.size) - int(onnx_ids.size))
        baseline_eos_positions = np.flatnonzero(baseline_ids == eos_id)
        preprocessing_sensitivity = {
            "baselineInputTensorSha256": baseline_input_sha,
            "currentInputTensorSha256": actual_input_sha,
            "baselineRawTokenCount": int(baseline_ids.size),
            "currentRawTokenCount": int(onnx_ids.size),
            "rawTokenIdsExactMatch": bool(baseline_ids.shape == onnx_ids.shape and np.array_equal(baseline_ids, onnx_ids)),
            "differentRawTokenCount": differing_count,
            "baselineEosIndex": int(baseline_eos_positions[0]) if baseline_eos_positions.size else None,
            "currentEosIndex": int(eos_positions[0]) if eos_positions.size else None,
            "baselineReachedEos": bool(baseline_eos_positions.size),
            "currentReachedEos": reached_eos,
            "interpretationBoundary": "Single sample/input-preprocessing sensitivity check; does not establish general preprocessing equivalence or formula quality.",
        }
    result = {
        "schemaVersion": "deploysharp-formulanet-l-official-onnx-trace-v1",
        "runtime": {
            "paddlex": paddlex.__version__,
            "paddle": paddle.__version__,
            "device": "cpu",
            "runner": "official PaddleX PP-FormulaNet-L predictor runner",
        },
        "model": {
            "id": model["model"],
            "onnxSha256": model["modelSha256"],
            "officialProgramSha256": model["sourceInferenceProgramSha256"],
            "officialWeightsSha256": model["sourceInferenceWeightsSha256"],
            "inferenceYamlSha256": model["tokenizerYamlSha256"],
            "yamlMaxSequenceLength": yaml_max_sequence_length,
            "decoderSafetyLimitSequenceLength": model["decoderSafetyLimitSequenceLength"],
            "startTokenId": int(model["startTokenId"]),
            "endTokenId": eos_id,
        },
        "sample": {
            "image": sample["image"],
            "imageSha256": sample["imageSha256"],
            "referenceLatexSha256": sample["referenceSha256"],
            "inputShape": list(input_shape),
            "inputTensorSha256": actual_input_sha,
            "rawOutputShape": sample["rawOutputShape"],
            "onnxRawTokenCount": int(onnx_ids.size),
            "officialRawTokenCount": int(official_ids.size),
            "rawStartsWithBos": raw_starts_with_bos,
            "generatedTokenCountExcludingBos": int(generated_ids.size),
            "officialAndOnnxRawTokenIdsExactMatch": bool(exact_token_match),
            "firstDifferingTokenIndex": first_difference,
            "eosTokenOccurrences": int(eos_positions.size),
            "eosTokenIndex": int(eos_positions[0]) if eos_positions.size else None,
            "reachedEos": reached_eos,
            "onnxDecoderWarning": sample["warnings"],
            "officialLatexCharacters": len(official_latex),
            "deploySharpLatexCharacters": len(traced_latex),
            "officialAndDeploySharpWhitespaceStrippedLatexExactMatch": official_compact == traced_compact,
            "referenceLatexCharacters": len(reference_compact),
            "officialExactMatchToReferenceAfterWhitespaceRemoval": official_compact == reference_compact,
            "deploySharpExactMatchToReferenceAfterWhitespaceRemoval": traced_compact == reference_compact,
            "officialLatexPreview": official_latex[:300],
            "officialLatexSuffix": official_latex[-300:],
        },
        "preprocessingComparison": preprocessing_comparison,
        "preprocessingSensitivity": preprocessing_sensitivity,
        "conclusion": (
            "The official PaddleX runner and ONNX Runtime produced byte-for-byte identical token IDs for the exact same captured Float32 input. "
            "Neither output contains EOS; both emit BOS plus 1024 generated positions. The matching inference.yml max_seq_len=1024 is configured under the preprocessing label encoder, "
            "so this observation alone does not prove that YAML field is the causal generation cap. "
            "The trace localizes this sample's non-EOS behavior to the official/exported execution path rather than DeploySharp token decoding."
            if exact_token_match and not reached_eos and generated_ids.size >= yaml_max_sequence_length
            else "The observed official-vs-ONNX token trace did not satisfy the expected cap/parity pattern; inspect per-token evidence before drawing a cause."
        ),
        "boundary": "One realFormula sample, one official checkpoint export and CPU runtime pair. This does not establish a general quality result, a semantic-equivalence judgment, or behavior on other models/backends.",
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + os.linesep, encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
