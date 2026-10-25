"""Bound PaddleOCR-VL-1.6 FP32 attention workspaces by grouping independent heads.

Each query still attends to every key in its original head. No pixels, tokens,
keys, head dimensions, weights or floating-point types are removed or changed.
The resulting bundle is a separately identified derivative, not the original
export. CUDA/CPU parity and actual allocator behaviour require runtime tests.
"""

from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import datetime, timezone
import gc
import hashlib
import json
from pathlib import Path
import shutil

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper


ORIGINAL_GRAPHS = {
    "vision": "940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f",
    "embedding": "1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86",
    "decoder": "823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad",
    "head": "8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148",
}


def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def chunked_attention(prefix, q, kt, v, alpha, heads_per_group, query_chunk_size, output):
    """Sequential full-key attention with bounded query rows for the pinned 1x16xNx72 layout."""
    constants, nodes = [], []

    def integer(label, values, shape=None):
        name = prefix + "/" + label
        constants.append(helper.make_tensor(name, TensorProto.INT64, [len(values)] if shape is None else shape, values))
        return name

    def node(op, label, inputs, **attributes):
        name = prefix + "/" + label
        nodes.append(helper.make_node(op, inputs, [name], name=name, **attributes))
        return name

    chunk = integer("chunk_size", [query_chunk_size], [])
    group_size = integer("group_size", [heads_per_group], [])
    group_count = integer("group_count", [16 // heads_per_group], [])
    query_axis = integer("query_axis", [2], [])
    axes0, axes1, axes2 = integer("axes0", [0]), integer("axes1", [1]), integer("axes2", [2])
    one = integer("slice_steps", [1])
    zeros6, zero_vector = integer("zeros6", [0] * 6), integer("zero_vector", [0])
    scan_shape = integer("scan_shape", [16 // heads_per_group, -1, 1, heads_per_group, query_chunk_size, 72])
    merged_shape = integer("merged_shape", [1, 16, -1, 72])
    q_shape = node("Shape", "q_shape", [q])
    query_count = node("Gather", "query_count", [q_shape, query_axis], axis=0)
    remainder = node("Mod", "query_remainder", [query_count, chunk])
    extra = node("Sub", "padding_candidate", [chunk, remainder])
    padding = node("Mod", "padding", [extra, chunk])
    padded_count = node("Add", "padded_query_count", [query_count, padding])
    chunks = node("Div", "query_chunks", [padded_count, chunk])
    padding_vector = node("Unsqueeze", "padding_vector", [padding, axes0])
    pads = node("Concat", "pads", [zeros6, padding_vector, zero_vector], axis=0)
    padded_q = node("Pad", "padded_q", [q, pads], mode="constant")
    trips = node("Mul", "trips", [group_count, chunks])
    condition = prefix + "/condition"
    constants.append(helper.make_tensor(condition, TensorProto.BOOL, [], [True]))
    iteration, cond_in, cond_out = prefix + "/iteration", prefix + "/cond_in", prefix + "/cond_out"
    carried = [prefix + "/input_" + role for role in ("q", "kt", "v")]
    final = [prefix + "/output_" + role for role in ("q", "kt", "v")]
    outer_nodes, nodes = nodes, []
    group = node("Div", "head_group", [iteration, chunks])
    head_start = node("Mul", "head_start", [group, group_size])
    head_end = node("Add", "head_end", [head_start, group_size])
    starts = node("Unsqueeze", "head_starts", [head_start, axes0])
    ends = node("Unsqueeze", "head_ends", [head_end, axes0])
    query_group = node("Mod", "query_group", [iteration, chunks])
    query_start = node("Mul", "query_start", [query_group, chunk])
    query_end = node("Add", "query_end", [query_start, chunk])
    query_starts = node("Unsqueeze", "query_starts", [query_start, axes0])
    query_ends = node("Unsqueeze", "query_ends", [query_end, axes0])
    slices = [node("Slice", "head_" + role, [value, starts, ends, axes1, one])
              for role, value in zip(("q", "kt", "v"), carried)]
    query = node("Slice", "query", [slices[0], query_starts, query_ends, axes2, one])
    raw = node("MatMul", "raw_scores", [query, slices[1]])
    scores = node("Mul", "scores", [raw, alpha])
    probabilities = node("Softmax", "probabilities", [scores], axis=-1)
    attended = node("MatMul", "attended", [probabilities, slices[2]])
    nodes.append(helper.make_node("Identity", [cond_in], [cond_out], name=cond_out))
    nodes.extend(helper.make_node("Identity", [source], [target], name=target) for source, target in zip(carried, final))
    body = helper.make_graph(nodes, prefix + "/body", [
        helper.make_tensor_value_info(iteration, TensorProto.INT64, []),
        helper.make_tensor_value_info(cond_in, TensorProto.BOOL, []),
        *[helper.make_tensor_value_info(name, TensorProto.FLOAT, [1, 16, None, None]) for name in carried]], [
        helper.make_tensor_value_info(cond_out, TensorProto.BOOL, []),
        *[helper.make_tensor_value_info(name, TensorProto.FLOAT, [1, 16, None, None]) for name in final],
        helper.make_tensor_value_info(attended, TensorProto.FLOAT, [1, heads_per_group, query_chunk_size, 72])])
    nodes = outer_nodes
    scan = prefix + "/scan"
    nodes.append(helper.make_node("Loop", [trips, condition, padded_q, kt, v],
        [*[prefix + "/final_" + role for role in ("q", "kt", "v")], scan], body=body, name=prefix + "/loop"))
    expanded = node("Reshape", "expanded", [scan, scan_shape])
    ordered = node("Transpose", "ordered", [expanded], perm=[2, 0, 3, 1, 4, 5])
    merged = node("Reshape", "merged", [ordered, merged_shape])
    end = node("Unsqueeze", "original_query_end", [query_count, axes0])
    nodes.append(helper.make_node("Slice", [merged, zero_vector, end, axes2, one], [output], name=prefix + "/unpad"))
    return nodes, constants


def group_attention_heads(model, heads_per_group, expected_blocks=27, schedule="loop", query_chunk_size=0):
    """Rewrite only private MatMul -> Softmax(last axis) -> MatMul branches.

    The caller must establish the 16-head rank-four Q/K/V contract. The CLI
    requires the exact pinned source hashes; tests use explicit synthetic shapes.
    Validate every candidate before mutating the graph.
    """
    if heads_per_group not in (1, 2, 4, 8):
        raise ValueError("heads_per_group must be 1, 2, 4 or 8 (out of 16)")
    if expected_blocks < 1:
        raise ValueError("expected_blocks must be positive")
    if schedule not in ("loop", "unrolled"):
        raise ValueError("schedule must be loop or unrolled")
    if query_chunk_size < 0 or query_chunk_size > 1024 or (query_chunk_size and schedule != "loop"):
        raise ValueError("query_chunk_size must be 0..1024; nonzero requires loop scheduling")
    if any(node.name.startswith("ds_vl_headgroups/") for node in model.graph.node):
        raise ValueError("The graph is already head-grouped")
    nodes = list(model.graph.node)
    producers = {value: (index, node) for index, node in enumerate(nodes) for value in node.output}
    consumers = defaultdict(list)
    for index, node in enumerate(nodes):
        for value in node.input:
            consumers[value].append((index, node))
    graph_outputs = {value.name for value in model.graph.output}

    def unscale(value):
        # Match ORT's existing MatMulScaleFusion arithmetic, which combines
        # the scalar Q/K multipliers into one float32 output alpha. Leaving
        # those multipliers before Slice prevents that fusion and introduces
        # an avoidable rounding difference in the deep vision encoder.
        _, node = producers.get(value, (-1, None))
        if node is None or node.domain or node.op_type != "Mul" or len(node.input) != 2:
            return value, np.float32(1)
        for position, name in enumerate(node.input):
            _, constant = producers.get(name, (-1, None))
            tensor = next((item for item in model.graph.initializer if item.name == name), None)
            if constant is not None and not constant.domain and constant.op_type == "Constant":
                tensor = next((attribute.t for attribute in constant.attribute if attribute.name == "value"), None)
            if tensor is None or tensor.data_type != TensorProto.FLOAT:
                continue
            number = numpy_helper.to_array(tensor)
            if number.size == 1 and np.isfinite(number).all():
                return node.input[1 - position], np.float32(number.reshape(-1)[0])
        return value, np.float32(1)

    candidates = []
    for softmax_index, softmax in enumerate(nodes):
        if softmax.domain or softmax.op_type != "Softmax":
            continue
        axis = next((attribute.i for attribute in softmax.attribute if attribute.name == "axis"), -1)
        if axis != -1 or len(softmax.input) != 1 or len(softmax.output) != 1:
            raise ValueError("Expected Softmax over the full final key axis")
        score_index, score = producers.get(softmax.input[0], (-1, None))
        final_consumers = consumers[softmax.output[0]]
        if score is None or score.domain or score.op_type != "MatMul" or len(score.input) != 2 or len(score.output) != 1:
            raise ValueError("Softmax input is not a private attention MatMul")
        if len(final_consumers) != 1 or len(consumers[score.output[0]]) != 1:
            raise ValueError("Shared attention intermediates must not be replaced")
        final_index, final = final_consumers[0]
        if final.domain or final.op_type != "MatMul" or len(final.input) != 2 or len(final.output) != 1 or final.input[0] != softmax.output[0]:
            raise ValueError("Expected probabilities @ V, in that order")
        if score.output[0] in graph_outputs or softmax.output[0] in graph_outputs:
            raise ValueError("Public intermediate outputs must not be replaced")
        if not score_index < softmax_index < final_index:
            raise ValueError("Unexpected source topological order")
        candidates.append((score_index, softmax_index, final_index, score, final))
    if len(candidates) != expected_blocks:
        raise ValueError(f"Expected {expected_blocks} attention blocks, got {len(candidates)}")
    removed = set()
    replacements = {}
    constants = []
    rows = []
    for block, (score_index, softmax_index, final_index, score, final) in enumerate(candidates):
        removed.update((score_index, softmax_index, final_index))
        prefix = f"ds_vl_headgroups/{block}"
        axes, steps = prefix + "/axes", prefix + "/steps"
        constants.extend((helper.make_tensor(axes, TensorProto.INT64, [1], [1]), helper.make_tensor(steps, TensorProto.INT64, [1], [1])))
        q, q_scale = unscale(score.input[0])
        kt, k_scale = unscale(score.input[1])
        alpha = np.float32(q_scale * k_scale)
        alpha_name = prefix + "/alpha"
        constants.append(helper.make_tensor(alpha_name, TensorProto.FLOAT, [], [float(alpha)]))
        replacement = []
        outputs = []
        if query_chunk_size:
            replacement, chunk_constants = chunked_attention(prefix, q, kt, final.input[1], alpha_name,
                heads_per_group, query_chunk_size, final.output[0])
            constants.extend(chunk_constants)
        elif schedule == "loop":
            # Independent unrolled branches may have overlapping live score
            # buffers after runtime scheduling/fusion. Loop provides an actual
            # sequential execution boundary and reuses one body allocation plan.
            # Scan only the small attended values, never the attention scores.
            trip, condition = prefix + "/trip", prefix + "/condition"
            group_size, vector_axis = prefix + "/group_size", prefix + "/vector_axis"
            constants.extend((helper.make_tensor(trip, TensorProto.INT64, [], [16 // heads_per_group]),
                              helper.make_tensor(condition, TensorProto.BOOL, [], [True]),
                              helper.make_tensor(group_size, TensorProto.INT64, [], [heads_per_group]),
                              helper.make_tensor(vector_axis, TensorProto.INT64, [1], [0])))
            iteration, cond_in, cond_out = prefix + "/iteration", prefix + "/cond_in", prefix + "/cond_out"
            start, end = prefix + "/start", prefix + "/end"
            starts, ends = prefix + "/starts", prefix + "/ends"
            # Make Q/K/V real Loop inputs rather than lexical captures. This
            # exposes their lifetime to the outer graph's allocation planner.
            carried_inputs = [prefix + "/input_" + role for role in ("q", "kt", "v")]
            carried_outputs = [prefix + "/output_" + role for role in ("q", "kt", "v")]
            body_nodes = [helper.make_node("Mul", [iteration, group_size], [start], name=start),
                          helper.make_node("Add", [start, group_size], [end], name=end),
                          helper.make_node("Unsqueeze", [start, vector_axis], [starts], name=starts),
                          helper.make_node("Unsqueeze", [end, vector_axis], [ends], name=ends)]
            inputs = []
            for role, value in zip(("q", "kt", "v"), carried_inputs):
                sliced = prefix + "/body_" + role
                body_nodes.append(helper.make_node("Slice", [value, starts, ends, axes, steps], [sliced], name=sliced))
                inputs.append(sliced)
            raw_scores, scores = prefix + "/raw_scores", prefix + "/scores"
            probabilities, attended = prefix + "/probabilities", prefix + "/attended"
            body_nodes.extend((helper.make_node("MatMul", inputs[:2], [raw_scores], name=raw_scores),
                               helper.make_node("Mul", [raw_scores, alpha_name], [scores], name=scores),
                               helper.make_node("Softmax", [scores], [probabilities], axis=-1, name=probabilities),
                               helper.make_node("MatMul", [probabilities, inputs[2]], [attended], name=attended),
                               helper.make_node("Identity", [cond_in], [cond_out], name=cond_out)))
            body_nodes.extend(helper.make_node("Identity", [source], [target], name=target)
                              for source, target in zip(carried_inputs, carried_outputs))
            body = helper.make_graph(body_nodes, prefix + "/body", [
                helper.make_tensor_value_info(iteration, TensorProto.INT64, []),
                helper.make_tensor_value_info(cond_in, TensorProto.BOOL, []),
                *[helper.make_tensor_value_info(name, TensorProto.FLOAT, [None, 16, None, None]) for name in carried_inputs]], [
                helper.make_tensor_value_info(cond_out, TensorProto.BOOL, []),
                *[helper.make_tensor_value_info(name, TensorProto.FLOAT, [None, 16, None, None]) for name in carried_outputs],
                helper.make_tensor_value_info(attended, TensorProto.FLOAT, [None, heads_per_group, None, None])])
            scanned, ordered, output_shape = prefix + "/scan", prefix + "/ordered", prefix + "/output_shape"
            unused_states = [prefix + "/final_" + role for role in ("q", "kt", "v")]
            replacement.extend((helper.make_node("Loop", [trip, condition, q, kt, final.input[1]], [*unused_states, scanned],
                                                body=body, name=prefix + "/loop"),
                                helper.make_node("Transpose", [scanned], [ordered], perm=[1, 0, 2, 3, 4], name=ordered),
                                helper.make_node("Shape", [q], [output_shape], name=output_shape),
                                helper.make_node("Reshape", [ordered, output_shape], list(final.output), name=prefix + "/reshape")))
        for start in range(0, 16, heads_per_group) if schedule == "unrolled" else ():
            group = prefix + f"/heads_{start}_{start + heads_per_group}"
            starts, ends = group + "/starts", group + "/ends"
            constants.extend((helper.make_tensor(starts, TensorProto.INT64, [1], [start]),
                              helper.make_tensor(ends, TensorProto.INT64, [1], [start + heads_per_group])))
            inputs = []
            for role, value in zip(("q", "kt", "v"), (q, kt, final.input[1])):
                sliced = group + "/" + role
                replacement.append(helper.make_node("Slice", [value, starts, ends, axes, steps], [sliced], name=sliced))
                inputs.append(sliced)
            raw_scores, scores, probabilities, attended = group + "/raw_scores", group + "/scores", group + "/probabilities", group + "/attended"
            replacement.extend((helper.make_node("MatMul", inputs[:2], [raw_scores], name=raw_scores),
                                helper.make_node("Mul", [raw_scores, alpha_name], [scores], name=scores),
                                helper.make_node("Softmax", [scores], [probabilities], axis=-1, name=probabilities),
                                helper.make_node("MatMul", [probabilities, inputs[2]], [attended], name=attended)))
            outputs.append(attended)
        if schedule == "unrolled":
            replacement.append(helper.make_node("Concat", outputs, list(final.output), axis=1, name=prefix + "/concat"))
        replacements[final_index] = replacement
        rows.append({"block": block, "scoreNode": score.name, "outputNode": final.name,
                     "groups": 16 // heads_per_group, "headsPerGroup": heads_per_group, "schedule": schedule,
                     "queriesPerChunk": query_chunk_size,
                     "float32ScoreAlpha": float(alpha)})
    rewritten = []
    for index, node in enumerate(nodes):
        if index in replacements:
            rewritten.extend(replacements[index])
        elif index not in removed:
            rewritten.append(node)
    del model.graph.node[:]
    model.graph.node.extend(rewritten)
    model.graph.initializer.extend(constants)
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--heads-per-group", type=int, choices=(1, 2, 4, 8), default=1)
    parser.add_argument("--schedule", choices=("loop", "unrolled"), default="loop",
                        help="Loop serializes head groups; unrolled branches remain runtime-scheduled")
    parser.add_argument("--queries-per-chunk", type=int, choices=(0, 64, 128, 256, 512), default=0,
                        help="Optional padded query blocks; every original query still attends to all original keys")
    args = parser.parse_args()
    if args.output_root.exists():
        raise FileExistsError("Use a new derivative directory; original graphs and reports are preserved")
    source_rows = []
    for role, expected in ORIGINAL_GRAPHS.items():
        path = args.bundle_root / f"{role}.onnx"
        actual = digest(path)
        if actual != expected:
            raise ValueError(f"Pinned {role} source graph hash mismatch")
        source_rows.append({"role": role, "sha256": actual, "bytes": path.stat().st_size})
    args.output_root.mkdir(parents=True)
    report_path = args.output_root / "head-group-export.json"
    report = {"schemaVersion": "deploysharp-paddleocr-vl-head-group-export-v1", "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "status": "running", "scriptSha256": digest(__file__), "sourceGraphs": source_rows,
              "checkpointRevision": "c5630abae1d940eafe0697512a0325494b02ab42", "heads": 16, "headsPerGroup": args.heads_per_group,
              "schedule": args.schedule,
              "queriesPerChunk": args.queries_per_chunk,
              "loopInputBinding": "explicit-carried-qkv" if args.schedule == "loop" else "not-applicable",
              "scalePlacement": "float32 output alpha matching ORT MatMulScaleFusion",
              "precision": "unchanged-float32", "boundary": "Exact independent-head partition; every query retains every key. The score-buffer size per branch is reduced, but actual peak VRAM, numerical parity, full generation and backend support require separate runtime evidence."}

    def save():
        report_path.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")

    save()
    try:
        model = onnx.load(str(args.bundle_root / "vision.onnx"), load_external_data=False)
        report["blocks"] = group_attention_heads(model, args.heads_per_group, schedule=args.schedule,
                                                query_chunk_size=args.queries_per_chunk)
        output = args.output_root / "vision.onnx"
        onnx.save_model(model, str(output))
        del model
        gc.collect()
        onnx.checker.check_model(str(output))
        for role in ("embedding", "decoder", "head"):
            shutil.copy2(args.bundle_root / f"{role}.onnx", args.output_root / f"{role}.onnx")
        report["graphs"] = [{"role": role, "sha256": digest(args.output_root / f"{role}.onnx"),
                             "bytes": (args.output_root / f"{role}.onnx").stat().st_size} for role in ORIGINAL_GRAPHS]
        report["checker"] = "passed"
        report["status"] = "exported-not-runtime-verified"
        save()
    except Exception as error:
        report["status"] = "failed"
        report["error"] = repr(error)
        save()
        raise


if __name__ == "__main__":
    main()
