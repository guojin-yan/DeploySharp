#!/usr/bin/env python3
"""Convert the audited Donut ONNX bundle to explicit FP32 OpenVINO IR."""

from __future__ import annotations

import argparse
import hashlib
import json
import time
from pathlib import Path

import openvino as ov


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", type=Path, required=True)
    args = parser.parse_args()
    source = args.model_root / "onnx"
    target = args.model_root / "openvino"
    target.mkdir(parents=True, exist_ok=True)
    core = ov.Core()
    records = []
    for name in ("encoder_model", "decoder_model", "decoder_with_past_model"):
        started = time.perf_counter()
        model = core.read_model(source / f"{name}.onnx")
        xml = target / f"{name}.xml"
        ov.save_model(model, xml, compress_to_fp16=False)
        elapsed = (time.perf_counter() - started) * 1000.0
        files = []
        for path in (xml, xml.with_suffix(".bin")):
            files.append(
                {
                    "relativePath": path.relative_to(args.model_root).as_posix(),
                    "size": path.stat().st_size,
                    "sha256": sha256(path),
                }
            )
        records.append(
            {
                "role": name,
                "conversionMilliseconds": elapsed,
                "inputs": [value.any_name for value in model.inputs],
                "outputs": [value.any_name for value in model.outputs],
                "files": files,
            }
        )
    evidence = {
        "schemaVersion": "1.0",
        "openvino": ov.__version__,
        "sourceOpset": 17,
        "precision": "FP32",
        "compressToFp16": False,
        "records": records,
    }
    path = args.model_root / "evidence" / "openvino-conversion.json"
    path.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    print("DEPLOYSHARP_STAGE27_DONUT_OPENVINO_CONVERSION_OK")


if __name__ == "__main__":
    main()
