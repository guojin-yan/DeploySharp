"""Alpha-rename ONNX subgraph parameters for importers with name-capture collisions.

The original artifact is preserved. Requires the conversion environment's onnx package.
Example: python normalize_loop_parameters.py input.onnx output.onnx
"""
import argparse
import hashlib
import json
from pathlib import Path

import onnx


def subgraphs(graph):
    for node in graph.node:
        for attribute in node.attribute:
            if attribute.type == onnx.AttributeProto.GRAPH:
                yield attribute.g
            elif attribute.type == onnx.AttributeProto.GRAPHS:
                yield from attribute.graphs


def all_names(graph):
    result = {value.name for value in graph.input}
    result.update(value.name for value in graph.initializer)
    for node in graph.node:
        result.update(node.input)
        result.update(node.output)
    for child in subgraphs(graph):
        result.update(all_names(child))
    return result


def replace_uses(graph, mapping):
    # Formal parameters and local values in nested scopes shadow captures.
    for node in graph.node:
        for index, name in enumerate(node.input):
            node.input[index] = mapping.get(name, name)
    for value in list(graph.output) + list(graph.value_info):
        value.name = mapping.get(value.name, value.name)
    for child in subgraphs(graph):
        local = {value.name for value in child.input}
        local.update(value.name for value in child.initializer)
        local.update(name for node in child.node for name in node.output)
        replace_uses(child, {old: new for old, new in mapping.items() if old not in local})


def normalize(model):
    used = all_names(model.graph)
    changes = []

    def visit(parent):
        for graph in subgraphs(parent):
            mapping = {}
            for value in graph.input:
                if any(initializer.name == value.name for initializer in graph.initializer):
                    raise ValueError("An overridable initializer cannot be alpha-renamed: " + value.name)
                suffix = len(changes)
                name = "deploysharp_local_parameter_" + str(suffix)
                while name in used:
                    suffix += 1
                    name = "deploysharp_local_parameter_" + str(suffix)
                used.add(name)
                mapping[value.name] = name
                changes.append({"from": value.name, "to": name})
                value.name = name
            replace_uses(graph, mapping)
            visit(graph)

    visit(model.graph)
    return changes


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve() or args.output.exists():
        parser.error("Output must be a new path; the original model is never overwritten.")
    model = onnx.load(args.source)
    changes = normalize(model)
    onnx.checker.check_model(model)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    onnx.save_model(model, args.output)
    print(json.dumps({"source": str(args.source), "source_sha256": digest(args.source),
                     "output": str(args.output), "output_sha256": digest(args.output),
                     "renamed_parameters": len(changes)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
