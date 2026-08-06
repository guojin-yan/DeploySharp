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


def semantic_segmentation() -> onnx.ModelProto:
    """Expose a tiny RGB-like tensor as three-class logits without hidden postprocessing."""
    graph = helper.make_graph(
        [helper.make_node("Identity", ["images"], ["logits"])],
        "deploysharp_semantic_segmentation",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 2, 3])],
        [helper.make_tensor_value_info("logits", TensorProto.FLOAT, [1, 3, 2, 3])],
    )
    return graph_model(graph, "semantic-segmentation")


def binary_segmentation() -> onnx.ModelProto:
    """Expose one channel as precomputed probabilities for explicit threshold tests."""
    graph = helper.make_graph(
        [helper.make_node("Identity", ["images"], ["probabilities"])],
        "deploysharp_binary_segmentation",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 1, 2, 3])],
        [helper.make_tensor_value_info("probabilities", TensorProto.FLOAT, [1, 1, 2, 3])],
    )
    return graph_model(graph, "binary-segmentation")


def semantic_label_map() -> onnx.ModelProto:
    """Produce a backend-owned Int64 label map by taking channel-wise argmax."""
    graph = helper.make_graph(
        [helper.make_node("ArgMax", ["images"], ["labels"], axis=1, keepdims=0, select_last_index=0)],
        "deploysharp_semantic_label_map",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 2, 3])],
        [helper.make_tensor_value_info("labels", TensorProto.INT64, [1, 2, 3])],
    )
    return graph_model(graph, "semantic-label-map")


def direct_pose() -> onnx.ModelProto:
    """Emit boxes, scores, and direct keypoints for strict named multi-output Pose tests."""
    boxes = numpy_helper.from_array(
        np.asarray([[[10, 10, 50, 50], [11, 11, 51, 51], [60, 60, 90, 90]]], dtype=np.float32),
        name="boxes_value",
    )
    scores = numpy_helper.from_array(np.asarray([[0.90, 0.80, 0.90]], dtype=np.float32), name="scores_value")
    # x, y, confidence, explicit visibility. Candidates 0/1 exercise OKS; 0/2 exercise stable score ties.
    keypoints = numpy_helper.from_array(
        np.asarray(
            [[
                [[20, 20, 0.90, 1], [30, 30, 0.80, 1], [40, 40, 0.70, 1]],
                [[21, 21, 0.90, 1], [31, 31, 0.80, 1], [41, 41, 0.70, 1]],
                [[65, 65, 0.80, 1], [75, 75, 0.70, 1], [85, 85, 0.60, 1]],
            ]],
            dtype=np.float32,
        ),
        name="keypoints_value",
    )
    graph = helper.make_graph(
        [
            helper.make_node("Constant", [], ["boxes"], value=boxes),
            helper.make_node("Constant", [], ["scores"], value=scores),
            helper.make_node("Constant", [], ["keypoints"], value=keypoints),
        ],
        "deploysharp_direct_pose",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 100, 100])],
        [
            helper.make_tensor_value_info("boxes", TensorProto.FLOAT, [1, 3, 4]),
            helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 3]),
            helper.make_tensor_value_info("keypoints", TensorProto.FLOAT, [1, 3, 3, 4]),
        ],
    )
    return graph_model(graph, "direct-pose")


def heatmap_pose() -> onnx.ModelProto:
    """Emit three deterministic probability heatmaps with a tie and a boundary peak."""
    heatmaps = numpy_helper.from_array(
        np.asarray([[[[0.90, 0], [0, 0]], [[0.10, 0.80], [0.80, 0]], [[0, 0], [0, 0.70]]]], dtype=np.float32),
        name="heatmaps_value",
    )
    score = numpy_helper.from_array(np.asarray([0.95], dtype=np.float32), name="pose_score_value")
    graph = helper.make_graph(
        [
            helper.make_node("Constant", [], ["heatmaps"], value=heatmaps),
            helper.make_node("Constant", [], ["pose_score"], value=score),
        ],
        "deploysharp_heatmap_pose",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 8, 8])],
        [
            helper.make_tensor_value_info("heatmaps", TensorProto.FLOAT, [1, 3, 2, 2]),
            helper.make_tensor_value_info("pose_score", TensorProto.FLOAT, [1]),
        ],
    )
    return graph_model(graph, "heatmap-pose")


def direct_instance_segmentation() -> onnx.ModelProto:
    """Emit strict named candidates and direct per-candidate probability masks."""
    boxes = numpy_helper.from_array(
        np.asarray([[[0, 0, 3, 3], [0.1, 0.1, 3.1, 3.1], [2, 0, 4, 2]]], dtype=np.float32),
        name="instance_boxes_value",
    )
    scores = numpy_helper.from_array(np.asarray([[0.90, 0.80, 0.90]], dtype=np.float32), name="instance_scores_value")
    classes = numpy_helper.from_array(np.asarray([[0, 0, 1]], dtype=np.float32), name="instance_classes_value")
    masks = numpy_helper.from_array(np.ones((1, 3, 4, 4), dtype=np.float32), name="instance_masks_value")
    graph = helper.make_graph(
        [
            helper.make_node("Constant", [], ["boxes"], value=boxes),
            helper.make_node("Constant", [], ["scores"], value=scores),
            helper.make_node("Constant", [], ["classes"], value=classes),
            helper.make_node("Constant", [], ["masks"], value=masks),
        ],
        "deploysharp_direct_instance_segmentation",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 4, 4])],
        [
            helper.make_tensor_value_info("boxes", TensorProto.FLOAT, [1, 3, 4]),
            helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 3]),
            helper.make_tensor_value_info("classes", TensorProto.FLOAT, [1, 3]),
            helper.make_tensor_value_info("masks", TensorProto.FLOAT, [1, 3, 4, 4]),
        ],
    )
    return graph_model(graph, "direct-instance-segmentation")


def prototype_instance_segmentation() -> onnx.ModelProto:
    """Emit strict candidates, two NCHW prototypes, and per-candidate coefficients."""
    boxes = numpy_helper.from_array(
        np.asarray([[[0, 0, 3, 3], [0.1, 0.1, 3.1, 3.1], [2, 0, 4, 2]]], dtype=np.float32),
        name="prototype_boxes_value",
    )
    scores = numpy_helper.from_array(np.asarray([[0.90, 0.80, 0.90]], dtype=np.float32), name="prototype_scores_value")
    classes = numpy_helper.from_array(np.asarray([[0, 0, 1]], dtype=np.float32), name="prototype_classes_value")
    left = np.asarray(
        [[10, 10, -10, -10], [10, 10, -10, -10], [10, 10, -10, -10], [10, 10, -10, -10]],
        dtype=np.float32,
    )
    right = -left
    prototypes = numpy_helper.from_array(np.asarray([[left, right]], dtype=np.float32), name="prototypes_value")
    coefficients = numpy_helper.from_array(np.asarray([[[1, 0], [1, 0], [0, 1]]], dtype=np.float32), name="coefficients_value")
    graph = helper.make_graph(
        [
            helper.make_node("Constant", [], ["boxes"], value=boxes),
            helper.make_node("Constant", [], ["scores"], value=scores),
            helper.make_node("Constant", [], ["classes"], value=classes),
            helper.make_node("Constant", [], ["prototypes"], value=prototypes),
            helper.make_node("Constant", [], ["coefficients"], value=coefficients),
        ],
        "deploysharp_prototype_instance_segmentation",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 4, 4])],
        [
            helper.make_tensor_value_info("boxes", TensorProto.FLOAT, [1, 3, 4]),
            helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 3]),
            helper.make_tensor_value_info("classes", TensorProto.FLOAT, [1, 3]),
            helper.make_tensor_value_info("prototypes", TensorProto.FLOAT, [1, 2, 4, 4]),
            helper.make_tensor_value_info("coefficients", TensorProto.FLOAT, [1, 3, 2]),
        ],
    )
    return graph_model(graph, "prototype-instance-segmentation")


def direct_obb() -> onnx.ModelProto:
    """Emit center-x, center-y, width, height, and clockwise radians with strict named vectors."""
    boxes = numpy_helper.from_array(
        np.asarray(
            [[[25, 25, 20, 10, 0.4], [26, 25, 20, 10, 0.4], [25, 25, 20, 10, 0.4], [75, 75, 16, 8, -np.pi / 2]]],
            dtype=np.float32,
        ),
        name="obb_boxes_value",
    )
    scores = numpy_helper.from_array(np.asarray([[0.90, 0.80, 0.90, 0.05]], dtype=np.float32), name="obb_scores_value")
    classes = numpy_helper.from_array(np.asarray([[0, 0, 1, 0]], dtype=np.float32), name="obb_classes_value")
    graph = helper.make_graph(
        [
            helper.make_node("Constant", [], ["boxes"], value=boxes),
            helper.make_node("Constant", [], ["scores"], value=scores),
            helper.make_node("Constant", [], ["classes"], value=classes),
        ],
        "deploysharp_direct_obb",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 100, 100])],
        [
            helper.make_tensor_value_info("boxes", TensorProto.FLOAT, [1, 4, 5]),
            helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 4]),
            helper.make_tensor_value_info("classes", TensorProto.FLOAT, [1, 4]),
        ],
    )
    return graph_model(graph, "direct-obb")


def corner_obb() -> onnx.ModelProto:
    """Emit explicit counter-clockwise model-space quadrilaterals with deterministic overlap."""
    corners = numpy_helper.from_array(
        np.asarray(
            [[
                [10, 10, 30, 10, 30, 30, 10, 30],
                [12, 10, 32, 10, 32, 30, 12, 30],
                [10, 10, 30, 10, 30, 30, 10, 30],
                [60, 60, 80, 60, 80, 72, 60, 72],
            ]],
            dtype=np.float32,
        ),
        name="obb_corners_value",
    )
    scores = numpy_helper.from_array(np.asarray([[0.90, 0.80, 0.90, 0.90]], dtype=np.float32), name="corner_scores_value")
    classes = numpy_helper.from_array(np.asarray([[0, 0, 1, 0]], dtype=np.float32), name="corner_classes_value")
    graph = helper.make_graph(
        [
            helper.make_node("Constant", [], ["corners"], value=corners),
            helper.make_node("Constant", [], ["scores"], value=scores),
            helper.make_node("Constant", [], ["classes"], value=classes),
        ],
        "deploysharp_corner_obb",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 100, 100])],
        [
            helper.make_tensor_value_info("corners", TensorProto.FLOAT, [1, 4, 8]),
            helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 4]),
            helper.make_tensor_value_info("classes", TensorProto.FLOAT, [1, 4]),
        ],
    )
    return graph_model(graph, "corner-obb")


def text_detection() -> onnx.ModelProto:
    """Consume a 32x16 image and emit explicit TL/TR/BR/BL contract polygons and scores."""
    polygons = numpy_helper.from_array(
        np.asarray(
            [[
                [[2, 2], [14, 2], [14, 6], [2, 6]],
                [[3, 2], [15, 2], [15, 6], [3, 6]],
                [[16, 8], [30, 8], [30, 14], [16, 14]],
            ]],
            dtype=np.float32,
        ),
        name="text_polygons_value",
    )
    scores = numpy_helper.from_array(np.asarray([[0.95, 0.80, 0.90]], dtype=np.float32), name="text_scores_value")
    zero = numpy_helper.from_array(np.asarray(0.0, dtype=np.float32), name="text_zero")
    graph = helper.make_graph(
        [
            helper.make_node("ReduceMean", ["images"], ["image_mean"], axes=[1, 2, 3], keepdims=0),
            helper.make_node("Mul", ["image_mean", "text_zero"], ["image_zero"]),
            helper.make_node("Constant", [], ["polygon_constants"], value=polygons),
            helper.make_node("Constant", [], ["score_constants"], value=scores),
            helper.make_node("Add", ["polygon_constants", "image_zero"], ["polygons"]),
            helper.make_node("Add", ["score_constants", "image_zero"], ["scores"]),
        ],
        "deploysharp_text_detection",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 16, 32])],
        [
            helper.make_tensor_value_info("polygons", TensorProto.FLOAT, [1, 3, 4, 2]),
            helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 3]),
        ],
        [zero],
    )
    return graph_model(graph, "text-detection")


def text_recognition_ctc() -> onnx.ModelProto:
    """Consume two 3x8x16 crops and emit deterministic blank/repeat CTC logits for AB and CA."""
    selected = np.asarray([[0, 1, 1, 0, 2, 2], [3, 3, 0, 1, 1, 0]], dtype=np.int64)
    logits_values = np.zeros((2, 6, 4), dtype=np.float32)
    for batch in range(2):
        for timestep in range(6):
            logits_values[batch, timestep, selected[batch, timestep]] = 5.0
    logits = numpy_helper.from_array(logits_values, name="ctc_logits_value")
    zero = numpy_helper.from_array(np.asarray(0.0, dtype=np.float32), name="ctc_zero")
    axes = numpy_helper.from_array(np.asarray([1, 2], dtype=np.int64), name="ctc_unsqueeze_axes")
    graph = helper.make_graph(
        [
            helper.make_node("ReduceMean", ["crops"], ["crop_mean"], axes=[1, 2, 3], keepdims=0),
            helper.make_node("Mul", ["crop_mean", "ctc_zero"], ["crop_zero"]),
            helper.make_node("Unsqueeze", ["crop_zero", "ctc_unsqueeze_axes"], ["crop_zero_3d"]),
            helper.make_node("Constant", [], ["logit_constants"], value=logits),
            helper.make_node("Add", ["logit_constants", "crop_zero_3d"], ["logits"]),
        ],
        "deploysharp_text_recognition_ctc",
        [helper.make_tensor_value_info("crops", TensorProto.FLOAT, [2, 3, 8, 16])],
        [helper.make_tensor_value_info("logits", TensorProto.FLOAT, [2, 6, 4])],
        [zero, axes],
    )
    return graph_model(graph, "text-recognition-ctc")


def anomaly_detection() -> onnx.ModelProto:
    """Consume an odd-sized RGB tensor and emit a scalar score plus two-channel probability map."""
    map_values = numpy_helper.from_array(
        np.asarray(
            [[
                [[0.00, 0.10, 0.20, 0.30, 0.40], [0.50, 0.60, 0.70, 0.80, 0.90], [1.00, 0.00, 0.25, 0.75, 1.00]],
                [[0.05, 0.15, 0.25, 0.35, 0.45], [0.55, 0.65, 0.75, 0.85, 0.95], [0.90, 0.10, 0.50, 0.50, 0.00]],
            ]],
            dtype=np.float32,
        ),
        name="anomaly_map_value",
    )
    score = numpy_helper.from_array(np.asarray([0.875], dtype=np.float32), name="anomaly_score_value")
    zero = numpy_helper.from_array(np.asarray(0.0, dtype=np.float32), name="anomaly_zero")
    axes = numpy_helper.from_array(np.asarray([1, 2, 3], dtype=np.int64), name="anomaly_unsqueeze_axes")
    graph = helper.make_graph(
        [
            helper.make_node("ReduceMean", ["images"], ["image_mean"], axes=[1, 2, 3], keepdims=0),
            helper.make_node("Mul", ["image_mean", "anomaly_zero"], ["image_zero"]),
            helper.make_node("Unsqueeze", ["image_zero", "anomaly_unsqueeze_axes"], ["map_zero"]),
            helper.make_node("Constant", [], ["map_constants"], value=map_values),
            helper.make_node("Constant", [], ["score_constants"], value=score),
            helper.make_node("Add", ["map_constants", "map_zero"], ["anomaly_map"]),
            helper.make_node("Add", ["score_constants", "image_zero"], ["image_score"]),
        ],
        "deploysharp_anomaly_detection",
        [helper.make_tensor_value_info("images", TensorProto.FLOAT, [1, 3, 3, 5])],
        [
            helper.make_tensor_value_info("image_score", TensorProto.FLOAT, [1]),
            helper.make_tensor_value_info("anomaly_map", TensorProto.FLOAT, [1, 2, 3, 5]),
        ],
        [zero, axes],
    )
    return graph_model(graph, "anomaly-detection")


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


def signature(value: onnx.ValueInfoProto) -> dict[str, object]:
    tensor_type = value.type.tensor_type
    shape: list[int | str] = []
    for dimension in tensor_type.shape.dim:
        shape.append(dimension.dim_param if dimension.dim_param else dimension.dim_value)
    return {
        "name": value.name,
        "elementType": TensorProto.DataType.Name(tensor_type.elem_type).lower(),
        "shape": shape,
    }


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    models = {
        "classification.onnx": classification(),
        "detection.onnx": detection(),
        "semantic-segmentation.onnx": semantic_segmentation(),
        "binary-segmentation.onnx": binary_segmentation(),
        "semantic-label-map.onnx": semantic_label_map(),
        "direct-pose.onnx": direct_pose(),
        "heatmap-pose.onnx": heatmap_pose(),
        "direct-instance-segmentation.onnx": direct_instance_segmentation(),
        "prototype-instance-segmentation.onnx": prototype_instance_segmentation(),
        "direct-obb.onnx": direct_obb(),
        "corner-obb.onnx": corner_obb(),
        "text-detection.onnx": text_detection(),
        "text-recognition-ctc.onnx": text_recognition_ctc(),
        "anomaly-detection.onnx": anomaly_detection(),
        "dynamic-identity.onnx": dynamic_identity(),
        "numeric-types.onnx": numeric_types(),
        "unsupported-types.onnx": unsupported_types(),
        "cancellable-loop.onnx": cancellable_loop(),
        "serialized-loop.onnx": serialized_loop(),
    }
    manifest: dict[str, object] = {
        "generator": "eng/test-models/Generate-OnnxRuntimeFixtures.py with onnx==1.22.0",
        "license": "Apache-2.0",
        "purpose": "DeploySharp adapter contract fixtures; not official algorithm models",
        "opset": OPSET,
        "files": {},
    }
    for file_name, model in models.items():
        data = model.SerializeToString(deterministic=True)
        (OUTPUT / file_name).write_bytes(data)
        manifest["files"][file_name] = {
            "bytes": len(data),
            "sha256": hashlib.sha256(data).hexdigest(),
            "inputs": [signature(value) for value in model.graph.input],
            "outputs": [signature(value) for value in model.graph.output],
        }
    (OUTPUT / "fixtures.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
