"""Compare original checkpoint, integrated Transformers and ONNX-adapter RoPE.

Extract only the SHA-verified upstream pure position method, without importing
or modifying the legacy checkpoint model. This is position parity, not full
original-versus-integrated weight/model-generation parity.
"""

from __future__ import annotations

import argparse
import ast
from datetime import datetime, timezone
import hashlib
import inspect
import json
from pathlib import Path
from types import SimpleNamespace

import torch
from transformers import AutoConfig
from transformers.models.paddleocr_vl.modeling_paddleocr_vl import PaddleOCRVLModel

from PaddleOcrVlOnnxModules import single_image_positions

SOURCE_SHA256 = "c5013dff57ca8b87dc1de64d0fd839a44313de09d230a4fb2d08289d2cad5111"
RUNTIME_SHA256 = "9471f809b2c3c4dcb722e578b6f91d33af08abd8b0c4047bad067c011f499d32"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = args.model_root / "modeling_paddleocr_vl.py"
    if digest(source) != SOURCE_SHA256 or digest(Path(inspect.getfile(PaddleOCRVLModel))) != RUNTIME_SHA256:
        raise ValueError("Pinned source identity mismatch")
    tree = ast.parse(source.read_text(encoding="utf-8"))
    owner = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == "PaddleOCRVLForConditionalGeneration")
    method = next(node for node in owner.body if isinstance(node, ast.FunctionDef) and node.name == "get_rope_index")
    namespace = {"torch": torch}
    pure = ast.Module(body=[ast.ImportFrom(module="__future__", names=[ast.alias(name="annotations")], level=0), method], type_ignores=[])
    exec(compile(ast.fix_missing_locations(pure), str(source), "exec"), namespace)
    config = AutoConfig.from_pretrained(str(args.model_root), local_files_only=True)
    original = json.loads((args.model_root / "config.json").read_text(encoding="utf-8"))
    old = SimpleNamespace(config=SimpleNamespace(**{**original, "vision_config": SimpleNamespace(**original["vision_config"])}))
    current = SimpleNamespace(config=config)
    current.get_vision_position_ids = lambda *a, **kw: PaddleOCRVLModel.get_vision_position_ids(current, *a, **kw)
    reference = json.loads(args.reference.read_text(encoding="utf-8"))
    vectors = [(row["id"], row["inputTokenIds"], row["imageGridThw"][0], "actual-image-prompt") for row in reference["cases"]]
    for height, width in [(2, 2), (4, 6), (6, 4), (2, 100), (100, 2), (24, 24)]:
        for tail in [1, 31]:
            ids = [100273, 34, 101305] + [100295] * (height * width // 4) + [101306] + [65] * tail
            vectors.append((f"grid-{height}x{width}-tail-{tail}", ids, [1, height, width], "synthetic-prompt"))
    report = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(),
              "originalSourceSha256": SOURCE_SHA256, "integratedSourceSha256": RUNTIME_SHA256,
              "scriptSha256": digest(Path(__file__)), "referenceSha256": digest(args.reference),
              "adaptersSha256": digest(Path(__file__).with_name("PaddleOcrVlOnnxModules.py")), "cases": [],
              "boundary": "Single-image, unpadded 3D position/delta parity only; not full original model generation or ONNX/C# inference."}
    for identity, values, grid, scope in vectors:
        ids = torch.tensor([values], dtype=torch.long)
        grids = torch.tensor([grid], dtype=torch.long)
        original_position, original_delta = namespace["get_rope_index"](old, input_ids=ids, image_grid_thw=grids)
        runtime_position, runtime_delta = PaddleOCRVLModel.get_rope_index(current, input_ids=ids,
            mm_token_type_ids=(ids == 100295).int(), image_grid_thw=grids)
        adapter_position, adapter_delta = single_image_positions(ids, grid[1], grid[2])
        if not (torch.equal(original_position, runtime_position) and torch.equal(original_position, adapter_position)
                and int(original_delta.item()) == int(runtime_delta.item()) == adapter_delta):
            raise ValueError(f"3D RoPE mismatch in {identity}")
        row = {"id": identity, "scope": scope, "gridThw": grid, "inputTokenIds": values,
               "positionShape": list(original_position.shape), "positionIds": original_position.tolist(),
               "ropeDelta": adapter_delta, "allThreeEqual": True, "decode": []}
        for generated in [0, 1, 7, 31, 79]:
            past = len(values) + generated
            row["decode"].append({"pastTokens": past, "positionIds": [past + adapter_delta] * 3})
        report["cases"].append(row)
    report["status"] = "all-positions-exact"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"POSITIONS_EXACT {len(report['cases'])} cases", flush=True)


if __name__ == "__main__":
    main()
