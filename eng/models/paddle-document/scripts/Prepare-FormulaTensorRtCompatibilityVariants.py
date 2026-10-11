#!/usr/bin/env python3
"""Prepare non-publishable FormulaNet TensorRT compatibility probes.

The script deliberately creates copies of one official ONNX graph.  It never
modifies the source artifact.  The variants are diagnostic only: they explore
whether the TensorRT parser/builder boundary is caused by the exported integer
mask operators and an unused Loop carry.  A generated file is not a supported
DeploySharp model unless it later passes decoder and parity validation.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def save_checked(model: onnx.ModelProto, path: Path) -> dict[str, object]:
    onnx.checker.check_model(model)
    path.parent.mkdir(parents=True, exist_ok=True)
    onnx.save(model, path)
    return {
        "path": str(path),
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "nodeCount": len(model.graph.node),
        "onnxChecker": "pass",
    }


def make_bool_mask(source: onnx.ModelProto) -> onnx.ModelProto:
    model = onnx.ModelProto()
    model.CopyFrom(source)
    loop = next(node for node in model.graph.node if node.op_type == "Loop")
    body = loop.attribute[0].g

    cast34 = next(node for node in body.node if node.name == "Cast.34")
    next(attribute for attribute in cast34.attribute if attribute.name == "to").i = TensorProto.BOOL

    bitwise_not = next(node for node in body.node if node.name == "BitwiseNot.0")
    bitwise_not.op_type = "Not"
    bitwise_not.name = "deploysharp.formula.logical_not_mask"

    bitwise_and = next(node for node in body.node if node.name == "BitwiseAnd.0")
    bitwise_and.op_type = "And"
    bitwise_and.name = "deploysharp.formula.logical_and_mask"
    original_output = bitwise_and.output[0]

    # The other operand is an int64 mask.  Convert just this diagnostic edge to
    # bool and cast the result back to int64 so the surrounding graph contract
    # stays unchanged.
    bool_input = "deploysharp.formula.full_with_tensor_1.bool"
    insertion = next(index for index, node in enumerate(body.node) if node is bitwise_and)
    body.node.insert(
        insertion,
        helper.make_node(
            "Cast",
            ["p2o.pd_op.full_with_tensor.1.0"],
            [bool_input],
            name="deploysharp.formula.cast.full_with_tensor_1.bool",
            to=TensorProto.BOOL,
        ),
    )
    bitwise_and.input[0] = bool_input
    bool_output = "deploysharp.formula.logical_and_mask.bool"
    bitwise_and.output[0] = bool_output
    insertion = next(index for index, node in enumerate(body.node) if node is bitwise_and)
    body.node.insert(
        insertion + 1,
        helper.make_node(
            "Cast",
            [bool_output],
            [original_output],
            name="deploysharp.formula.logical_and_mask.cast_int64",
            to=TensorProto.INT64,
        ),
    )
    return model


def drop_dead_scalar_carry(source: onnx.ModelProto) -> onnx.ModelProto:
    model = onnx.ModelProto()
    model.CopyFrom(source)
    loop = next(node for node in model.graph.node if node.op_type == "Loop")
    body = loop.attribute[0].g

    # In the official Plus-S export this scalar is not consumed by any body
    # node, but TensorRT still creates a recurrence layer for it.  Removing it
    # is useful as a builder diagnostic; it is not a proof that the source graph
    # is semantically safe to rewrite.
    assert loop.input[2] == "p2o.pd_op.full.26.0"
    assert loop.output[1] == "p2o.pd_op.while.0.1"
    assert body.input[2].name == "p2o.pd_op.full.26.0"
    # The first body output is the loop condition; carried state at input[2]
    # is therefore returned at body.output[1].
    assert body.output[1].name == "p2o.sub_block.pd_op.if.2.0"
    del loop.input[2]
    del loop.output[1]
    del body.input[2]
    del body.output[1]
    return model


def expand_shape_changing_carry(source: onnx.ModelProto) -> onnx.ModelProto:
    model = onnx.ModelProto()
    model.CopyFrom(source)
    loop = next(node for node in model.graph.node if node.op_type == "Loop")
    body = loop.attribute[0].g
    cast = next(node for node in body.node if node.name == "Cast.31")
    original_output = "p2o.sub_block.pd_op.scale.15.0"
    assert cast.output[0] == original_output

    # ORT executes the official export with a shape-changing carry: the first
    # iteration starts with arange=[0,1,2], while the corresponding body output
    # is a one-element tensor. TensorRT rejects that recurrence. Broadcasting
    # the scalar to the initial three-channel shape is a diagnostic attempt;
    # it is deliberately not asserted to be numerically equivalent.
    scalar_output = "deploysharp.formula.scale15.scalar"
    cast.output[0] = scalar_output
    shape_name = "deploysharp.formula.scale15.shape"
    body.initializer.append(
        numpy_helper.from_array(np.asarray([3], dtype=np.int64), name=shape_name)
    )
    index = next(i for i, node in enumerate(body.node) if node is cast)
    body.node.insert(
        index + 1,
        helper.make_node(
            "Expand",
            [scalar_output, shape_name],
            [original_output],
            name="deploysharp.formula.scale15.expand_to_three",
        ),
    )
    return model


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--sanitized", required=True, type=Path)
    parser.add_argument("--bool-mask", required=True, type=Path)
    parser.add_argument("--drop-dead", required=True, type=Path)
    parser.add_argument("--expand-carry", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    args = parser.parse_args()

    source = onnx.load(args.source, load_external_data=False)
    sanitized = onnx.load(args.sanitized, load_external_data=False)
    bool_mask = make_bool_mask(sanitized)
    drop_dead = drop_dead_scalar_carry(bool_mask)
    expand_carry = expand_shape_changing_carry(bool_mask)

    records = {
        "source": {
            "path": str(args.source),
            "sha256": sha256(args.source),
            "bytes": args.source.stat().st_size,
        },
        "variants": [
            save_checked(sanitized, args.sanitized),
            save_checked(bool_mask, args.bool_mask),
            save_checked(drop_dead, args.drop_dead),
            save_checked(expand_carry, args.expand_carry),
        ],
        "scope": "diagnostic-only; no generated variant is a publishable model",
    }
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    args.manifest.write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(records, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
