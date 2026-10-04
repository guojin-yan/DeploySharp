"""Audit batch and sequence axes in real PP-Structure ONNX artifacts.

This is a graph-contract audit only. It does not run inference and does not
turn a dynamic ONNX axis into a backend support claim. Use it after acquiring
or deriving a model so the profile's batch setting can be tied to the actual
graph rather than to a filename or decoder assumption.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import onnx


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dimension(value: Any) -> int | str:
    if value.HasField("dim_value"):
        return int(value.dim_value)
    if value.HasField("dim_param") and value.dim_param:
        return str(value.dim_param)
    return "?"


def tensor_shape(value_info: Any) -> list[int | str]:
    return [dimension(item) for item in value_info.type.tensor_type.shape.dim]


def first_axis_status(shape: list[int | str]) -> str:
    if not shape:
        return "none"
    first = shape[0]
    if isinstance(first, int):
        return f"static:{first}"
    return "dynamic"


def graph_contract(path: Path) -> dict[str, Any]:
    try:
        if path.stat().st_size == 0:
            raise ValueError("empty ONNX file")
        graph = onnx.load(str(path), load_external_data=False)
        inputs = []
        for item in graph.graph.input:
            shape = tensor_shape(item)
            inputs.append({
                "name": item.name,
                "shape": shape,
                "batchAxis": first_axis_status(shape),
            })
        outputs = []
        for item in graph.graph.output:
            shape = tensor_shape(item)
            outputs.append({
                "name": item.name,
                "shape": shape,
                "firstAxis": first_axis_status(shape),
            })

        input_axes = [item["batchAxis"] for item in inputs]
        dynamic_input = any(item == "dynamic" for item in input_axes)
        static_batch_one = bool(input_axes) and all(item == "static:1" for item in input_axes)
        output_axes = [item["firstAxis"] for item in outputs]
        dynamic_output = any(item == "dynamic" for item in output_axes)
        lower_name = path.name.lower()
        if "chart-text" in lower_name or "chart-vision" in lower_name or "chart-token" in lower_name:
            capability = (
                "autoregressive-static-batch-1"
                if static_batch_one
                else "chart-contract-review"
            )
        elif dynamic_input and dynamic_output:
            capability = "dynamic-input-and-output-batch-axis"
        elif dynamic_input:
            capability = "dynamic-input-batch-axis-output-review"
        elif static_batch_one:
            capability = "static-batch-1"
        else:
            capability = "batch-contract-review"

        return {
            "status": "parsed",
            "fileSizeBytes": path.stat().st_size,
            "sha256": sha256(path),
            "irVersion": int(graph.ir_version),
            "opset": max((item.version for item in graph.opset_import), default=0),
            "inputs": inputs,
            "outputs": outputs,
            "dynamicInputBatch": dynamic_input,
            "dynamicOutputFirstAxis": dynamic_output,
            "batchCapability": capability,
        }
    except Exception as error:  # keep one bad/empty artifact from hiding others
        return {
            "status": "load-failed",
            "fileSizeBytes": path.stat().st_size,
            "sha256": sha256(path) if path.stat().st_size else None,
            "error": str(error),
            "batchCapability": "unavailable",
        }


def markdown(report: dict[str, Any]) -> str:
    records = report["records"]
    parsed = [item for item in records if item["status"] == "parsed"]
    dynamic = [item for item in parsed if item["dynamicInputBatch"]]
    static = [item for item in parsed if item["batchCapability"] == "static-batch-1"]
    chart = [item for item in parsed if item["batchCapability"] == "autoregressive-static-batch-1"]
    failed = [item for item in records if item["status"] != "parsed"]
    lines = [
        "# PP-Structure ONNX batch-axis audit",
        "",
        f"Generated `{report['generatedUtc']}`. This is a graph-contract inventory; it is not backend execution or accuracy evidence.",
        "",
        "## Summary",
        "",
        "| Measure | Count |",
        "| --- | ---: |",
        f"| ONNX files discovered | {len(records)} |",
        f"| Parsed graphs | {len(parsed)} |",
        f"| Graphs with a dynamic input batch axis | {len(dynamic)} |",
        f"| Graphs with static input `batch=1` | {len(static)} |",
        f"| Chart2Table graphs with static batch `1` | {len(chart)} |",
        f"| Load failures/empty artifacts | {len(failed)} |",
        "",
        "A dynamic graph axis only permits the runtime to attempt a batch. The selected backend, input names, output layout, decoder and memory budget still require independent execution evidence. Static official exports should use the Session pool/page-concurrency path instead of claiming model Batch.",
        "",
        "## Artifacts",
        "",
        "| Artifact | Status | Input batch axes | Output first axes | Capability reading | SHA-256 |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for item in records:
        path = item["relativePath"]
        if item["status"] != "parsed":
            lines.append(f"| `{path}` | `{item['status']}` | - | - | `{item['batchCapability']}` | `{item.get('sha256') or '-'}` |")
            continue
        inputs = "; ".join(f"{value['name']} {value['batchAxis']}" for value in item["inputs"])
        outputs = "; ".join(f"{value['name']} {value['firstAxis']}" for value in item["outputs"][:4])
        if len(item["outputs"]) > 4:
            outputs += f"; +{len(item['outputs']) - 4} outputs"
        lines.append(f"| `{path}` | `parsed` | {inputs} | {outputs} | `{item['batchCapability']}` | `{item['sha256']}` |")
    if failed:
        lines.extend(["", "## Load failures", ""])
        for item in failed:
            lines.append(f"- `{item['relativePath']}`: `{item.get('error', 'unknown error')}`")
    lines.extend([
        "",
        "## Interpretation",
        "",
        "- `dynamic-input-and-output-batch-axis` is the minimum graph evidence needed before trying a true model Batch path; it does not imply that every backend accepts the graph.",
        "- `static-batch-1` is an explicit reason to use independent Sessions or the page-concurrency scheduler for multiple images.",
        "- `autoregressive-static-batch-1` covers Chart2Table vision, prefill, decode and embedding graphs. Their sequence/KV axes may be dynamic while the request batch remains one; greedy generation remains one request at a time.",
        "- Load failures are retained in the JSON so an empty or incompatible conversion cannot silently disappear from the audit.",
    ])
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", action="append", type=Path, required=True, help="Root to scan recursively; repeat for separate artifact roots.")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--markdown", type=Path)
    args = parser.parse_args()

    records: list[dict[str, Any]] = []
    seen: set[str] = set()
    for root in args.model_root:
        root = root.resolve()
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.onnx")):
            resolved = str(path.resolve()).lower()
            if resolved in seen:
                continue
            seen.add(resolved)
            contract = graph_contract(path)
            contract["root"] = str(root)
            contract["relativePath"] = path.relative_to(root).as_posix()
            contract["absolutePath"] = str(path)
            records.append(contract)

    records.sort(key=lambda item: item["absolutePath"].lower())
    report = {
        "schemaVersion": "deploysharp-paddle-document-onnx-batch-axis-audit-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "roots": [str(root.resolve()) for root in args.model_root],
        "records": records,
        "boundary": "Graph shape evidence only; no runtime, decoder, accuracy, throughput or backend support claim is implied.",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if args.markdown:
        args.markdown.parent.mkdir(parents=True, exist_ok=True)
        args.markdown.write_text(markdown(report), encoding="utf-8")
    print(f"Audited {len(records)} ONNX graphs; output={args.output}")


if __name__ == "__main__":
    main()
