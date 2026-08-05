"""Generate deterministic OpenVINO IR fixtures from DeploySharp-owned ONNX graphs.

生成可复现的 OpenVINO IR 合同夹具；这些文件仅用于适配器测试，不是官方算法模型。
Requires / 依赖: openvino==2026.2.1
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import openvino as ov


EXPECTED_VERSION = "2026.2.1"
ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tests" / "assets" / "onnxruntime"
OUTPUT = ROOT / "tests" / "assets" / "openvino"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> None:
    if not ov.__version__.startswith(EXPECTED_VERSION):
        raise RuntimeError(f"Expected openvino {EXPECTED_VERSION}, found {ov.__version__}")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    files = []
    sources = ("classification", "semantic-segmentation", "direct-pose", "direct-instance-segmentation")
    for name in sources:
        model = ov.convert_model(SOURCE / f"{name}.onnx")
        xml_path = OUTPUT / f"{name}.xml"
        ov.save_model(model, xml_path, compress_to_fp16=False)
        for path in (xml_path, xml_path.with_suffix(".bin")):
            files.append({"file": path.name, "size": path.stat().st_size, "sha256": sha256(path)})
    manifest = {
        "generator": "eng/test-models/Generate-OpenVinoFixtures.py",
        "openvino": ov.__version__,
        "sources": [f"tests/assets/onnxruntime/{name}.onnx" for name in sources],
        "license": "Apache-2.0",
        "purpose": "DeploySharp adapter contract fixture; not an official algorithm model",
        "files": files,
    }
    (OUTPUT / "fixtures.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="ascii")


if __name__ == "__main__":
    main()
