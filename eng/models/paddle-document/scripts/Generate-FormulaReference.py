"""Generate validation tensors using pinned, unmodified PaddleX processor classes.

Requires numpy, Pillow and opencv-python-headless. Writes only to --output.
No model weights are changed. This is a preprocessing oracle, not an accuracy score.
"""
import argparse
import ast
import hashlib
import json
import math
import urllib.request
from pathlib import Path
from typing import List, Optional, Tuple, Union

import cv2
import numpy as np
from PIL import Image, ImageOps

REVISION = "c50f5da858020db473a2285f089bb8c7bbd6afdc"
URL = f"https://raw.githubusercontent.com/PaddlePaddle/PaddleX/{REVISION}/paddlex/inference/models/formula_recognition/processors.py"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--processor-source", help="Optional offline copy of the pinned official processors.py")
    args = parser.parse_args()
    destination = Path(args.output)
    destination.mkdir(parents=True, exist_ok=True)
    source = Path(args.processor_source).read_bytes() if args.processor_source else urllib.request.urlopen(URL, timeout=60).read()
    assert hashlib.sha256(source).hexdigest() == "4a2c197634a210a90cca7bf3f417ce594b610db0058d2ccfa060dc2dcfb752eb", "Unexpected PaddleX processor source"
    tree = ast.parse(source)
    wanted = {"UniMERNetImgDecode", "UniMERNetTestTransform", "UniMERNetImageFormat", "LatexImageFormat"}
    nodes = [node for node in tree.body if isinstance(node, ast.ClassDef) and node.name in wanted]
    assert len(nodes) == len(wanted)
    # Only omit benchmark decoration, which does not alter numeric operations.
    for node in nodes:
        node.decorator_list = []
    namespace = dict(globals())
    exec(compile(ast.Module(body=nodes, type_ignores=[]), URL, "exec"), namespace)
    image = cv2.imread(args.image)
    assert image is not None
    image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)  # Predictor ReadImage(format="RGB")
    results = []
    for width, height in [(384, 384), (768, 768), (672, 192)]:
        decoded = namespace["UniMERNetImgDecode"]((height, width))([image])[0]
        normalized = namespace["UniMERNetTestTransform"]()([decoded])[0]
        tensor = namespace["UniMERNetImageFormat" if width == 672 else "LatexImageFormat"]()([normalized])[0]
        tensor = np.ascontiguousarray(tensor, dtype="<f4")
        name = f"formula-{width}x{height}.f32"
        tensor.tofile(destination / name)
        results.append(dict(file=name, shape=list(tensor.shape), sha256=hashlib.sha256(tensor.tobytes()).hexdigest()))
    manifest = dict(source=URL, sourceSha256=hashlib.sha256(source).hexdigest(), imageSha256=hashlib.sha256(Path(args.image).read_bytes()).hexdigest(), numpy=np.__version__, opencv=cv2.__version__, pillow=Image.__version__, tensors=results)
    (destination / "formula-reference.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
