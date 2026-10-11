#!/usr/bin/env python3
"""Audit formula ONNX graphs for TensorRT conversion risk without building engines.

This is intentionally static: formula graphs contain large autoregressive Loop
bodies and a builder crash must not be repeated for every model just to produce
the same negative evidence.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

import onnx


MODELS = (
    ("paddle-formula/pp-formulanet-plus-s", "pp-formulanet-plus-s.onnx"),
    ("paddle-formula/pp-formulanet-plus-m", "pp-formulanet-plus-m.onnx"),
    ("paddle-formula/pp-formulanet-plus-l", "pp-formulanet-plus-l.onnx"),
    ("paddle-formula/pp-formulanet-s", "pp-formulanet-s.onnx"),
    ("paddle-formula/pp-formulanet-l", "pp-formulanet-l.onnx"),
    ("paddle-formula/unimernet", "unimernet.onnx"),
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dim_value(dim) -> int | str:
    if dim.HasField("dim_value"):
        return int(dim.dim_value)
    if dim.HasField("dim_param"):
        return dim.dim_param
    return "?"


def shape(value_info):
    tensor = value_info.type.tensor_type
    return [dim_value(dim) for dim in tensor.shape.dim]


def graph_ops(graph):
    return Counter(node.op_type for node in graph.node)


def loop_summary(node):
    body = next((attribute.g for attribute in node.attribute if attribute.name == "body"), None)
    if body is None:
        return {"bodyNodeCount": None, "bodyOpCounts": {}, "bodyInputs": [], "bodyOutputs": []}
    return {
        "bodyNodeCount": len(body.node),
        "bodyOpCounts": dict(sorted(graph_ops(body).items())),
        "bodyInputs": [{"name": value.name, "shape": shape(value)} for value in body.input if value.type.HasField("tensor_type")],
        "bodyOutputs": [value.name for value in body.output],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", default=r"E:\Model\PaddleDocument\onnx")
    parser.add_argument("--output-json", default="eng/models/paddle-document/verification/formula-tensorrt-static-compatibility-20261011.json")
    parser.add_argument("--output-markdown", default="eng/models/paddle-document/verification/formula-tensorrt-static-compatibility-20261011.md")
    args = parser.parse_args()
    model_root = Path(args.model_root)
    rows = []
    for model_id, filename in MODELS:
        path = model_root / filename
        if not path.is_file():
            raise FileNotFoundError(path)
        model = onnx.load(str(path), load_external_data=False)
        loops = [node for node in model.graph.node if node.op_type == "Loop"]
        body_summaries = [loop_summary(node) for node in loops]
        rows.append({
            "model": model_id,
            "filename": filename,
            "bytes": path.stat().st_size,
            "sha256": sha256(path),
            "irVersion": model.ir_version,
            "opsets": [{"domain": item.domain, "version": item.version} for item in model.opset_import],
            "inputs": [{"name": value.name, "shape": shape(value)} for value in model.graph.input],
            "outputs": [{"name": value.name, "shape": shape(value)} for value in model.graph.output],
            "nodeCount": len(model.graph.node),
            "opCounts": dict(sorted(graph_ops(model.graph).items())),
            "loopCount": len(loops),
            "loops": body_summaries,
            "riskFlags": sorted(set(
                (["dynamicInputOrOutput"] if any(any(isinstance(dim, str) for dim in item["shape"]) for item in (
                    [{"shape": shape(value)} for value in model.graph.input] + [{"shape": shape(value)} for value in model.graph.output]
                )) else [])
                + (["autoregressiveLoop"] if loops else [])
                + (["controlFlowInLoop"] if any("If" in summary["bodyOpCounts"] for summary in body_summaries) else [])
                + (["dynamicShapeOpsInLoop"] if any(any(op in summary["bodyOpCounts"] for op in ("Shape", "Expand", "Range", "Reshape", "Slice")) for summary in body_summaries) else [])
                + (["bitwiseOpsInLoop"] if any(any(op.startswith("Bitwise") for op in summary["bodyOpCounts"]) for summary in body_summaries) else [])
            )),
        })
    report = {
        "schemaVersion": "deploysharp-formula-tensorrt-static-compatibility-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "scope": "Static ONNX graph audit only; no TensorRT engine is built by this script.",
        "models": rows,
        "interpretation": "All six graphs expose a dynamic batch/output axis and an autoregressive Loop. The body-level flags identify parser-risk structure; they are not proof that a specific TensorRT release cannot support an operator.",
    }
    output_json = Path(args.output_json)
    output_md = Path(args.output_markdown)
    output_json.parent.mkdir(parents=True, exist_ok=True)
    output_json.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    lines = [
        "# Formula TensorRT static compatibility audit (2026-10-11)",
        "",
        "This report inspects the six formula ONNX graphs without building engines. It is a parser-risk inventory, not a support verdict.",
        "",
        "| Model | Nodes | Loop body nodes | Input | Output | Risk flags |",
        "|---|---:|---:|---|---|---|",
    ]
    for row in rows:
        body_nodes = ", ".join(str(item["bodyNodeCount"]) for item in row["loops"]) or "0"
        flags = ", ".join(row["riskFlags"])
        lines.append(f"| `{row['model']}` | {row['nodeCount']} | {body_nodes} | `{row['inputs'][0]['shape']}` | `{row['outputs'][0]['shape']}` | {flags} |")
    lines += [
        "",
        "## Boundary",
        "",
        "All six graphs contain an autoregressive ONNX `Loop`; each loop body uses control flow and dynamic-shape operations. The Plus-S live TensorRT probe separately terminated with `0xC0000005` during parser startup. This audit does not prove that every TensorRT version fails, and it does not admit any formula TensorRT backend.",
        "",
        "The machine-readable report preserves model SHA-256, opset, top-level operator counts and loop-body operator counts so a future compatibility export can be compared without rebuilding the original graph first.",
    ]
    output_md.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {output_json} and {output_md}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
