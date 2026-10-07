#!/usr/bin/env python3
"""Inspect and safely probe PP-FormulaNet-L's exported ONNX decode limit.

The script never edits the supplied source ONNX or Paddle inference program.
Derived graphs and the JSON report are written only beneath --output-dir.
Requires Python packages onnx, onnxruntime and numpy.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterator

import numpy as np
import onnx
import onnxruntime as ort
from onnx import numpy_helper


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def graph_tree(graph: onnx.GraphProto, path: str = "graph") -> Iterator[tuple[str, onnx.GraphProto]]:
    yield path, graph
    for node in graph.node:
        for attribute in node.attribute:
            if attribute.type == onnx.AttributeProto.GRAPH:
                yield from graph_tree(attribute.g, f"{path}/{node.name}/{attribute.name}")
            elif attribute.type == onnx.AttributeProto.GRAPHS:
                for index, nested in enumerate(attribute.graphs):
                    yield from graph_tree(nested, f"{path}/{node.name}/{attribute.name}[{index}]")


def output_id(output: Any) -> int | None:
    if not isinstance(output, dict):
        return None
    value = output.get("%")
    return value if isinstance(value, int) else None


def paddle_ir_loop_cap(program_path: Path) -> dict[str, Any]:
    document = json.loads(program_path.read_text(encoding="utf-8-sig"))
    blocks = [block for region in document["program"]["regions"] for block in region.get("blocks", [])]
    top = next((block for block in blocks if any(op.get("#") == "1.while" for op in block.get("ops", []))), None)
    if top is None:
        raise ValueError("Could not find the top-level Paddle PIR while operation.")
    operations = top["ops"]
    producers: dict[int, dict[str, Any]] = {}
    for operation in operations:
        for output in operation.get("O", []):
            identity = output_id(output)
            if identity is not None:
                producers[identity] = operation

    loop = next(op for op in operations if op.get("#") == "1.while")
    condition_id = output_id(loop.get("I", [None])[0])
    condition = producers.get(condition_id)
    if condition is None or condition.get("#") != "1.logical_and":
        raise ValueError("The Paddle PIR loop condition is not the expected logical_and.")
    runtime_stop_id = output_id(condition.get("I", [None, None])[1])
    runtime_stop = producers.get(runtime_stop_id)
    if runtime_stop is None or runtime_stop.get("#") != "1.logical_not":
        raise ValueError("Could not verify the runtime stop predicate in the Paddle PIR loop condition.")
    less_id = output_id(condition.get("I", [None])[0])
    less = producers.get(less_id)
    if less is None or less.get("#") != "1.less_than":
        raise ValueError("Could not find the sequence-length comparison in the Paddle PIR loop condition.")
    bound_id = output_id(less.get("I", [None, None])[1])
    cast = producers.get(bound_id)
    if cast is None or cast.get("#") != "1.cast":
        raise ValueError("Could not find the casted generation bound in Paddle PIR.")
    assigned_id = output_id(cast.get("I", [None])[0])
    assigned = producers.get(assigned_id)
    if assigned is None or assigned.get("#") != "1.assign_value_":
        raise ValueError("Could not find the assigned generation bound in Paddle PIR.")
    values = next((attr.get("AT", {}).get("D") for attr in assigned.get("A", []) if attr.get("N") == "values"), None)
    if not isinstance(values, list) or len(values) != 1:
        raise ValueError(f"Unexpected Paddle PIR generation-bound value: {values!r}")
    raw = values[0].get("D") if isinstance(values[0], dict) else values[0]
    cap = int(raw)
    return {
        "whileOperation": loop.get("#"),
        "conditionOperation": condition.get("#"),
        "sequenceComparisonOperation": less.get("#"),
        "boundAssignmentOperation": assigned.get("#"),
        "loopConditionCombinesLengthBoundAndRuntimeStopPredicate": True,
        "decodeIterationCap": cap,
    }


def onnx_loop_cap(model: onnx.ModelProto, expected_cap: int) -> tuple[onnx.GraphProto, dict[str, Any]]:
    loops = [node for node in model.graph.node if node.op_type == "Loop"]
    if len(loops) != 1:
        raise ValueError(f"Expected one top-level ONNX Loop; found {len(loops)}.")
    loop = loops[0]
    body_attribute = next((attribute for attribute in loop.attribute if attribute.name == "body"), None)
    if body_attribute is None:
        raise ValueError("ONNX Loop has no body graph.")
    body = body_attribute.g
    initializers = {tensor.name: tensor for tensor in body.initializer}
    matches: list[onnx.TensorProto] = []
    for node in body.node:
        if node.op_type != "Less":
            continue
        for input_name in node.input:
            tensor = initializers.get(input_name)
            if tensor is None:
                continue
            value = numpy_helper.to_array(tensor)
            if value.size == 1 and float(value.reshape(-1)[0]) == float(expected_cap):
                matches.append(tensor)
    unique = {tensor.name: tensor for tensor in matches}
    if len(unique) != 1:
        raise ValueError(f"Expected one Loop-body Less bound of {expected_cap}; found {[t.name for t in unique.values()]}")

    body_producers = {output: node for node in body.node for output in node.output}
    loop_condition = body_producers.get(body.output[0].name)
    if loop_condition is None or loop_condition.op_type != "And":
        raise ValueError("The ONNX Loop body condition is not an And node.")
    condition_ops = [body_producers.get(name).op_type if body_producers.get(name) is not None else None for name in loop_condition.input]
    if "Less" not in condition_ops or "Not" not in condition_ops:
        raise ValueError(f"Expected the loop length bound and runtime stop predicate; got {condition_ops}.")

    position_tables: list[dict[str, Any]] = []
    for path, graph in graph_tree(body, "loop-body"):
        graph_initializers = {tensor.name: tensor for tensor in graph.initializer}
        for node in graph.node:
            if node.op_type != "Gather" or len(node.input) < 2:
                continue
            table = graph_initializers.get(node.input[0])
            if table is not None and table.name == "m_bart_learned_positional_embedding_3.w_0":
                position_tables.append({"graph": path, "node": node.name, "initializer": table.name, "shape": list(table.dims)})
    if len(position_tables) != 1:
        raise ValueError(f"Expected one positional-embedding Gather table; found {len(position_tables)}.")
    return body, {
        "loopOperation": loop.name,
        "conditionOperation": "And",
        "conditionInputs": condition_ops,
        "combinesLengthBoundAndRuntimeStopPredicate": True,
        "lengthComparisonOperation": "Less",
        "loopBoundInitializer": next(iter(unique.values())).name,
        "decodeIterationCap": expected_cap,
        "positionalEmbeddingGather": position_tables[0],
    }


def run_inference(model_path: Path, tensor: np.ndarray) -> dict[str, Any]:
    session_options = ort.SessionOptions()
    session_options.log_severity_level = 3
    session_options.intra_op_num_threads = 1
    session_options.inter_op_num_threads = 1
    start = time.perf_counter()
    try:
        session = ort.InferenceSession(str(model_path), sess_options=session_options, providers=["CPUExecutionProvider"])
        create_seconds = time.perf_counter() - start
        start = time.perf_counter()
        output = np.asarray(session.run(None, {session.get_inputs()[0].name: tensor})[0]).reshape(-1).astype(np.int64, copy=False)
        inference_seconds = time.perf_counter() - start
        eos = np.flatnonzero(output == 2)
        return {
            "status": "completed",
            "sessionCreateSeconds": create_seconds,
            "inferenceSeconds": inference_seconds,
            "rawTokenCount": int(output.size),
            "bosCount": int(np.count_nonzero(output == 0)),
            "padCount": int(np.count_nonzero(output == 1)),
            "eosCount": int(eos.size),
            "firstEosIndex": int(eos[0]) if eos.size else None,
            "unkCount": int(np.count_nonzero(output == 3)),
            "rawTokenIdsSha256": hashlib.sha256(output.astype("<i8", copy=False).tobytes()).hexdigest(),
            "first24TokenIds": output[:24].tolist(),
            "last24TokenIds": output[-24:].tolist(),
        }
    except Exception as error:  # Expected for caps exceeding the exported position table.
        return {"status": "failed", "errorType": type(error).__name__, "error": str(error)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--onnx", required=True, type=Path)
    parser.add_argument("--paddle-program", required=True, type=Path, help="Official inference.json")
    parser.add_argument("--input-tensor", required=True, type=Path, help="Little-endian Float32 [1,1,768,768] tensor")
    parser.add_argument("--output-dir", required=True, type=Path, help="External directory for derived ONNX probes and report")
    parser.add_argument("--caps", default="1024,1025,2048", help="Comma-separated temporary Loop-bound values")
    parser.add_argument("--report-name", default="formulanet-l-generation-limit-probe.json")
    args = parser.parse_args()
    for path in (args.onnx, args.paddle_program, args.input_tensor):
        if not path.is_file():
            raise FileNotFoundError(path)
    caps = list(dict.fromkeys(int(value.strip()) for value in args.caps.split(",") if value.strip()))
    if not caps or any(cap <= 0 for cap in caps):
        raise ValueError("--caps must contain positive integer values.")

    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    input_bytes = args.input_tensor.read_bytes()
    expected_bytes = 1 * 1 * 768 * 768 * np.dtype("<f4").itemsize
    if len(input_bytes) != expected_bytes:
        raise ValueError(f"Expected {expected_bytes} input bytes for [1,1,768,768]; got {len(input_bytes)}.")
    tensor = np.frombuffer(input_bytes, dtype="<f4").reshape(1, 1, 768, 768)

    ir = paddle_ir_loop_cap(args.paddle_program)
    source_model = onnx.load(str(args.onnx), load_external_data=False)
    body, graph = onnx_loop_cap(source_model, ir["decodeIterationCap"])
    position_shape = graph["positionalEmbeddingGather"]["shape"]
    del body
    report: dict[str, Any] = {
        "schemaVersion": "deploysharp-formulanet-l-generation-limit-probe-v1",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "runtime": {
            "python": platform.python_version(),
            "onnx": onnx.__version__,
            "onnxruntime": ort.__version__,
            "provider": "CPUExecutionProvider",
            "logicalProcessorCount": __import__("os").cpu_count(),
        },
        "model": {"id": "paddle-formula/pp-formulanet-l", "path": str(args.onnx.resolve()), "sha256": sha256_file(args.onnx)},
        "officialPaddleProgram": {"path": str(args.paddle_program.resolve()), "sha256": sha256_file(args.paddle_program), "loop": ir},
        "exportedOnnxLoop": graph,
        "input": {"shape": [1, 1, 768, 768], "path": str(args.input_tensor.resolve()), "sha256": sha256_file(args.input_tensor)},
        "probes": [],
        "boundary": "One FormulaNet-L sample on CPU. Derived graph probes diagnose this export's sequence limit and position-table boundary; they do not establish formula quality, checkpoint behavior after the export limit, or any other sample/backend.",
    }
    if len(position_shape) >= 1:
        report["exportedOnnxLoop"]["positionTableMaximumIndex"] = int(position_shape[0]) - 1

    for cap in caps:
        probe_path = output_dir / f"pp-formulanet-l-loop-cap-{cap}.onnx"
        if cap == ir["decodeIterationCap"]:
            probe_path = args.onnx
        else:
            model = onnx.load(str(args.onnx), load_external_data=False)
            body, loop_metadata = onnx_loop_cap(model, ir["decodeIterationCap"])
            bound = next(tensor_proto for tensor_proto in body.initializer if tensor_proto.name == loop_metadata["loopBoundInitializer"])
            bound.CopyFrom(numpy_helper.from_array(np.asarray(cap, dtype=np.float32), name=bound.name))
            onnx.checker.check_model(model)
            onnx.save_model(model, str(probe_path))
            del model, body
        probe = run_inference(probe_path, tensor)
        report["probes"].append({
            "requestedLoopCap": cap,
            "usesOriginalGraph": cap == ir["decodeIterationCap"],
            "graphPath": str(probe_path.resolve()),
            "graphSha256": sha256_file(probe_path),
            **probe,
        })

    report_path = output_dir / args.report_name
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"report": str(report_path), "probes": report["probes"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
