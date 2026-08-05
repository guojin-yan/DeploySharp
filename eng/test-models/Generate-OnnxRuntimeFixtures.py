"""Generate deterministic ONNX Runtime contract fixtures owned by DeploySharp tests."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "tests" / "assets" / "onnxruntime"
OPSET = 13


def graph_model(graph: onnx.GraphProto, name: str) -> onnx.ModelProto:
    model = helper.make_model(
        graph,
        producer_name="DeploySharp.Tests",
        producer_version="2.0.0",
        opset_imports=[helper.make_opsetid("", OPSET)],
    )
    model.ir_version = 11
    model.model_version = 1
    model.domain = "jyppx.deploysharp.tests"
    model.doc_string = f"DeploySharp reproducible {name} contract fixture; not an algorithm model."
    onnx.checker.check_model(model)
    return model


def classification() -> onnx.ModelProto:
    graph = helper.make_graph(
        [helper.make_node("ReduceMean", ["images"], ["scores"], axes=[2, 3], keepdims=0)],
        "deploysharp_classification",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 2, 2])],
        [helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 3])],
    )
    return graph_model(graph, "classification")


def detection() -> onnx.ModelProto:
    # xyxy, objectness, class-0, class-1. Rows 0 and 1 overlap and exercise class-aware NMS.
    values = np.asarray(
        [[[10, 10, 50, 50, 0.90, 0.90, 0.10],
          [12, 12, 48, 48, 0.80, 0.85, 0.15],
          [60, 60, 90, 90, 0.95, 0.05, 0.95]]],
        dtype=np.float32,
    )
    tensor = numpy_helper.from_array(values, name="detections_value")
    graph = helper.make_graph(
        [helper.make_node("Constant", [], ["detections"], value=tensor)],
        "deploysharp_detection",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 100, 100])],
        [helper.make_tensor_value_info("detections", TensorProto.FLOAT, [1, 3, 7])],
    )
    return graph_model(graph, "detection")


def dynamic_identity() -> onnx.ModelProto:
    graph = helper.make_graph(
        [helper.make_node("Identity", ["input"], ["output"])],
        "deploysharp_dynamic_identity",
        [helper.make_tensor_value_info("input", TensorProto.FLOAT, ["batch", 2])],
        [helper.make_tensor_value_info("output", TensorProto.FLOAT, ["batch", 2])],
    )
    return graph_model(graph, "dynamic-identity")


def numeric_types() -> onnx.ModelProto:
    types = [
        ("bool", TensorProto.BOOL),
        ("int8", TensorProto.INT8),
        ("uint8", TensorProto.UINT8),
        ("int16", TensorProto.INT16),
        ("uint16", TensorProto.UINT16),
        ("int32", TensorProto.INT32),
        ("uint32", TensorProto.UINT32),
        ("int64", TensorProto.INT64),
        ("uint64", TensorProto.UINT64),
        ("float32", TensorProto.FLOAT),
        ("float64", TensorProto.DOUBLE),
    ]
    inputs = [helper.make_tensor_value_info(f"{name}_in", kind, [2]) for name, kind in types]
    outputs = [helper.make_tensor_value_info(f"{name}_out", kind, [2]) for name, kind in types]
    nodes = [helper.make_node("Identity", [f"{name}_in"], [f"{name}_out"]) for name, _ in types]
    return graph_model(helper.make_graph(nodes, "deploysharp_numeric_types", inputs, outputs), "numeric-types")


def unsupported_types() -> onnx.ModelProto:
    inputs = [helper.make_tensor_value_info("string_in", TensorProto.STRING, [1])]
    outputs = [helper.make_tensor_value_info("string_out", TensorProto.STRING, [1])]
    nodes = [helper.make_node("Identity", [value.name], [value.name.replace("_in", "_out")]) for value in inputs]
    return graph_model(helper.make_graph(nodes, "deploysharp_unsupported_types", inputs, outputs), "unsupported-types")


def cancellable_loop() -> onnx.ModelProto:
    size = 128
    identity = np.eye(size, dtype=np.float32)
    weight_values = identity * np.float32(0.997) + np.roll(identity, 1, axis=1) * np.float32(0.003)
    weight = numpy_helper.from_array(weight_values, name="weight")
    nodes = []
    previous = "state"
    for index in range(256):
        product = f"product_{index:03d}"
        output = "output" if index == 255 else f"state_{index:03d}"
        nodes.append(helper.make_node("MatMul", [previous, "weight"], [product], name=f"matmul_{index:03d}"))
        nodes.append(helper.make_node("Tanh", [product], [output], name=f"tanh_{index:03d}"))
        previous = output
    graph = helper.make_graph(
        nodes,
        "deploysharp_cancellable_matmul_chain",
        [helper.make_tensor_value_info("state", TensorProto.FLOAT, [size, size])],
        [helper.make_tensor_value_info("output", TensorProto.FLOAT, [size, size])],
        [weight],
    )
    return graph_model(graph, "cancellable-matmul-chain")


def serialized_loop() -> onnx.ModelProto:
    iteration = helper.make_tensor_value_info("iteration", TensorProto.INT64, [])
    condition_in = helper.make_tensor_value_info("condition_in", TensorProto.BOOL, [])
    state_in = helper.make_tensor_value_info("state_in", TensorProto.FLOAT, [])
    condition_out = helper.make_tensor_value_info("condition_out", TensorProto.BOOL, [])
    state_out = helper.make_tensor_value_info("state_out", TensorProto.FLOAT, [])
    one = numpy_helper.from_array(np.asarray(1.0, dtype=np.float32), name="one")
    body = helper.make_graph(
        [
            helper.make_node("Identity", ["condition_in"], ["condition_out"]),
            helper.make_node("Add", ["state_in", "one"], ["state_out"]),
        ],
        "deploysharp_serialized_loop_body",
        [iteration, condition_in, state_in],
        [condition_out, state_out],
        [one],
    )
    condition = numpy_helper.from_array(np.asarray(True, dtype=np.bool_), name="condition")
    graph = helper.make_graph(
        [helper.make_node("Loop", ["trip_count", "condition", "state"], ["output"], body=body)],
        "deploysharp_serialized_loop",
        [
            helper.make_tensor_value_info("state", TensorProto.FLOAT, []),
            helper.make_tensor_value_info("trip_count", TensorProto.INT64, []),
        ],
        [helper.make_tensor_value_info("output", TensorProto.FLOAT, [])],
        [condition],
    )
    return graph_model(graph, "serialized-loop")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    models = {
        "classification.onnx": classification(),
        "detection.onnx": detection(),
        "dynamic-identity.onnx": dynamic_identity(),
        "numeric-types.onnx": numeric_types(),
        "unsupported-types.onnx": unsupported_types(),
        "cancellable-loop.onnx": cancellable_loop(),
        "serialized-loop.onnx": serialized_loop(),
    }
    manifest: dict[str, object] = {"generator": "onnx==1.22.0", "opset": OPSET, "files": {}}
    for file_name, model in models.items():
        data = model.SerializeToString(deterministic=True)
        (OUTPUT / file_name).write_bytes(data)
        manifest["files"][file_name] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    (OUTPUT / "fixtures.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
