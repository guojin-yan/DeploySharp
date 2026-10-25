"""Create a reproducible PaddleOCR-VL-1.6 Bundle and its integrity manifest.

The exporter writes the four ONNX graphs in one directory while the pinned
tokenizer assets normally live beside the original checkpoint. This helper
copies the eight required files and, when requested, the pinned derived
prefill graph. It refuses a conflicting target and writes the manifest
consumed by PaddleOcrVlBundleManifest before backend Session creation.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil


REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
SCHEMA = "deploysharp-paddleocr-vl-bundle-v1"
PREFILL_SCHEMA = "deploysharp-paddleocr-vl-bundle-v2"
BUNDLE_ID = "paddleocr-vl/1.6"
DECODER_SHA256 = "823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad"
PREFILL_SHA256 = "9247b437a1551652572b3e25162d4d86fc969edf12ae44577aa6fc75b9fc5bdc"

FILES = (
    ("vision", "vision.onnx", "onnx", "graph"),
    ("embedding", "embedding.onnx", "onnx", "graph"),
    ("decoder", "decoder.onnx", "onnx", "graph"),
    ("head", "head.onnx", "onnx", "graph"),
    ("tokenizer-model", "tokenizer.model", "sentencepiece", "tokenizer"),
    ("tokenizer-json", "tokenizer.json", "json", "tokenizer"),
    ("tokenizer-config", "tokenizer_config.json", "json", "tokenizer"),
    ("chat-template", "chat_template.jinja", "jinja", "tokenizer"),
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def copy_stable(source: Path, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    source_hash = sha256(source)
    if target.exists():
        if target.is_file() and target.stat().st_size == source.stat().st_size and sha256(target) == source_hash:
            return
        raise FileExistsError(f"Refusing to replace a different Bundle file: {target}")
    temporary = target.with_name(target.name + ".tmp")
    try:
        shutil.copyfile(source, temporary)
        if temporary.stat().st_size != source.stat().st_size or sha256(temporary) != source_hash:
            raise IOError(f"Copied Bundle file failed integrity verification: {source}")
        temporary.replace(target)
    finally:
        if temporary.exists():
            temporary.unlink()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--graph-root", type=Path, required=True, help="Directory containing vision.onnx, embedding.onnx, decoder.onnx and head.onnx")
    parser.add_argument("--tokenizer-root", type=Path, required=True, help="Pinned checkpoint directory containing tokenizer sidecars")
    parser.add_argument("--output-root", type=Path, required=True, help="Directory to receive the Bundle files and manifest")
    parser.add_argument("--prefill-graph", type=Path, help="Pinned derived prefill.onnx; enables the five-graph v2 Bundle")
    args = parser.parse_args()

    graph_root = args.graph_root.resolve()
    tokenizer_root = args.tokenizer_root.resolve()
    prefill_graph = args.prefill_graph.resolve(strict=True) if args.prefill_graph else None
    if prefill_graph:
        if sha256(graph_root / "decoder.onnx") != DECODER_SHA256 or sha256(prefill_graph) != PREFILL_SHA256:
            raise ValueError("The decoder and prefill graphs must match the pinned PaddleOCR-VL-1.6 derivation")
    files = list(FILES)
    if prefill_graph:
        files.insert(4, ("prefill", "prefill.onnx", "onnx", "prefill"))
    output_root = args.output_root.resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    rows = []
    for role, file_name, file_format, source_group in files:
        source = prefill_graph if source_group == "prefill" else (graph_root if source_group == "graph" else tokenizer_root) / file_name
        if not source.is_file():
            raise FileNotFoundError(f"Missing PaddleOCR-VL Bundle source: {source}")
        target = output_root / file_name
        copy_stable(source, target)
        rows.append({
            "role": role,
            "fileName": file_name,
            "format": file_format,
            "size": target.stat().st_size,
            "sha256": sha256(target),
        })

    manifest = {
        "schemaVersion": PREFILL_SCHEMA if prefill_graph else SCHEMA,
        "bundleId": BUNDLE_ID,
        "modelId": BUNDLE_ID,
        "checkpointRevision": REVISION,
        "artifacts": rows,
    }
    manifest_path = output_root / "paddleocr-vl-bundle.json"
    manifest_text = json.dumps(manifest, ensure_ascii=True, indent=2) + "\n"
    if manifest_path.exists():
        if manifest_path.read_text(encoding="utf-8") != manifest_text:
            raise FileExistsError(f"Refusing to replace a different Bundle manifest: {manifest_path}")
        print(json.dumps({"bundleRoot": str(output_root), "manifest": str(manifest_path), "artifacts": rows}, ensure_ascii=True, indent=2))
        return
    temporary = manifest_path.with_name(manifest_path.name + ".tmp")
    temporary.write_text(manifest_text, encoding="utf-8", newline="\n")
    temporary.replace(manifest_path)
    print(json.dumps({"bundleRoot": str(output_root), "manifest": str(manifest_path), "artifacts": rows}, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
