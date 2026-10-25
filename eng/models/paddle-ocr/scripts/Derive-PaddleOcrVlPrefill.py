"""Derive a two-input PaddleOCR-VL prefill graph from the pinned decoder export.

The 36 past-KV inputs are replaced with empty Float32 initializers. This keeps
the exported attention equations and weights while removing empty runtime I/O.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gc
import hashlib
import json
from pathlib import Path

import numpy as np
import onnx
from onnx import TensorProto, helper
import onnxruntime as ort


DECODER_SHA256 = "823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad"
PAST_NAMES = [f"past_{kind}_{layer}" for layer in range(18) for kind in ("key", "value")]
PRESENT_NAMES = [f"present_{kind}_{layer}" for layer in range(18) for kind in ("key", "value")]


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def save_report(path: Path, report: dict) -> None:
    path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--decoder", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--query-tokens", type=int, default=7)
    args = parser.parse_args()
    if args.query_tokens < 1 or args.query_tokens > 1024:
        parser.error("query-tokens must be between 1 and 1024")

    source = args.decoder.resolve(strict=True)
    source_hash = digest(source)
    if source_hash != DECODER_SHA256:
        raise ValueError("The source is not the pinned PaddleOCR-VL-1.6 decoder")
    root = args.output_root.resolve()
    root.mkdir(parents=True, exist_ok=True)
    derived = root / "prefill.onnx"
    report_path = root / "prefill-derivation.json"
    if derived.exists() or report_path.exists():
        raise FileExistsError("Use a fresh output directory")

    report = {
        "schemaVersion": "deploysharp-paddleocr-vl-prefill-derivation-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "sourceSha256": source_hash,
        "sourceBytes": source.stat().st_size,
        "scriptSha256": digest(Path(__file__)),
        "queryTokens": args.query_tokens,
        "status": "deriving",
        "boundary": "Two-input ONNX component parity only; not TensorRT inference or full generation.",
    }
    save_report(report_path, report)

    model = onnx.load_model(str(source), load_external_data=False)
    expected_inputs = ["inputs_embeds", "position_ids", *PAST_NAMES]
    expected_outputs = ["last_hidden_state", *PRESENT_NAMES]
    if [value.name for value in model.graph.input] != expected_inputs:
        raise ValueError("Decoder input ports differ from the pinned export")
    if [value.name for value in model.graph.output] != expected_outputs:
        raise ValueError("Decoder output ports differ from the pinned export")
    if [(value.domain, value.version) for value in model.opset_import] != [("", 18)]:
        raise ValueError("Decoder ONNX opset differs from the pinned export")
    for name in PAST_NAMES:
        model.graph.initializer.append(helper.make_tensor(name, TensorProto.FLOAT, [1, 2, 0, 128], []))
    del model.graph.input[2:]
    model.graph.name = "PaddleOCR-VL-1.6 prefill without runtime past KV"
    onnx.save_model(model, str(derived))
    del model
    gc.collect()
    onnx.checker.check_model(str(derived))
    report["derivedSha256"] = digest(derived)
    report["derivedBytes"] = derived.stat().st_size
    report["status"] = "checking-parity"
    save_report(report_path, report)

    length = args.query_tokens
    embeddings = (np.arange(length * 1024, dtype=np.float32) % 53 - 26).reshape(1, length, 1024) / 1000
    positions = np.broadcast_to(np.arange(length, dtype=np.int64)[None, None, :], (3, 1, length)).copy()
    inputs = {"inputs_embeds": embeddings, "position_ids": positions}
    inputs.update({name: np.empty((1, 2, 0, 128), dtype=np.float32) for name in PAST_NAMES})
    options = ort.SessionOptions()
    options.intra_op_num_threads = 4
    options.inter_op_num_threads = 1
    reference_session = ort.InferenceSession(str(source), sess_options=options, providers=["CPUExecutionProvider"])
    reference = reference_session.run(None, inputs)
    del reference_session, inputs
    gc.collect()
    prefill_session = ort.InferenceSession(str(derived), sess_options=options, providers=["CPUExecutionProvider"])
    if [value.name for value in prefill_session.get_inputs()] != expected_inputs[:2]:
        raise ValueError("Derived prefill graph has unexpected inputs")
    actual = prefill_session.run(None, {"inputs_embeds": embeddings, "position_ids": positions})
    del prefill_session
    if len(actual) != len(reference) or len(actual) != 37:
        raise ValueError("Derived prefill graph has unexpected outputs")

    maximum = 0.0
    squared = 0.0
    count = 0
    mismatches = []
    for name, got, wanted in zip(expected_outputs, actual, reference):
        if got.shape != wanted.shape or not np.isfinite(got).all():
            mismatches.append(name + ": shape or finite")
            continue
        errors = np.abs(got.astype(np.float64) - wanted.astype(np.float64))
        maximum = max(maximum, float(errors.max(initial=0)))
        squared += float(np.square(errors).sum())
        count += errors.size
        if not np.allclose(got, wanted, rtol=1e-5, atol=1e-5):
            mismatches.append(name)
    report.update({
        "inputNames": expected_inputs[:2],
        "outputNames": expected_outputs,
        "comparedElementCount": count,
        "maximumAbsoluteDifference": maximum,
        "rootMeanSquareDifference": (squared / count) ** 0.5 if count else None,
        "mismatchedOutputs": mismatches,
        "status": "prefill-parity-pass" if not mismatches and count else "prefill-parity-failed",
    })
    save_report(report_path, report)
    print(f"PREFILL_DERIVATION status={report['status']} max_abs={maximum} outputs={len(actual)}", flush=True)
    if report["status"] != "prefill-parity-pass":
        raise RuntimeError("Derived prefill graph does not match the original decoder")


if __name__ == "__main__":
    main()
