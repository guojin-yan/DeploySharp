"""Convert one audited YOLO ONNX artifact to an FP32 OpenVINO IR pair.

将一个已审计的 YOLO ONNX 工件转换为 FP32 OpenVINO IR 文件对。
"""

from pathlib import Path
import sys

import openvino as ov


def main() -> None:
    """Run deterministic OVC conversion without downloading or modifying the source. / 执行不下载且不修改源文件的确定性 OVC 转换。"""
    if len(sys.argv) != 3:
        raise SystemExit("usage: convert_yolo_onnx_to_openvino_ir.py MODEL.onnx OUTPUT.xml")
    source = Path(sys.argv[1]).resolve(strict=True)
    output = Path(sys.argv[2]).resolve(strict=False)
    output.parent.mkdir(parents=True, exist_ok=True)
    model = ov.convert_model(source)
    ov.save_model(model, output, compress_to_fp16=False)


if __name__ == "__main__":
    main()
