"""Create a diagnostic SLANeXt graph with statically shaped Loop carries.

This is intentionally not a release converter. The original Paddle exports
initialize two recurrent carries as scalar tensors while the Loop body returns
``[batch, 8]`` and ``[batch, 50]`` tensors. The script replaces only those two
initializers with zero tensors of shape ``[1, 8]`` and ``[1, 50]`` so that a
TensorRT Builder probe can distinguish the recurrence-shape error from later
TensorRT graph failures. The source ONNX is never modified.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import onnx
from onnx import numpy_helper


REPLACEMENTS = {
    "p2o.pd_op.full.641.0": (1, 8),
    "p2o.pd_op.full.642.0": (1, 50),
}


def prepare(source: Path, destination: Path) -> None:
    model = onnx.load(str(source), load_external_data=False)
    initializers = {initializer.name: initializer for initializer in model.graph.initializer}
    for name, shape in REPLACEMENTS.items():
        original = initializers.get(name)
        if original is None:
            raise ValueError(f"{source}: missing initializer {name}")
        if original.data_type != onnx.TensorProto.FLOAT:
            raise ValueError(f"{source}: {name} is not float32")
        model.graph.initializer.remove(original)
        model.graph.initializer.append(
            numpy_helper.from_array(np.zeros(shape, dtype=np.float32), name=name)
        )

    model.producer_name = "DeploySharp diagnostic recurrence-carry shape probe"
    onnx.checker.check_model(model)
    destination.parent.mkdir(parents=True, exist_ok=True)
    onnx.save(model, str(destination))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    prepare(args.source, args.destination)
    print(f"wrote {args.destination}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
