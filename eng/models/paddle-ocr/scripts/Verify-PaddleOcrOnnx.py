"""Run a bounded ONNX Runtime CPU structural smoke for the core PP-OCR catalog."""
import argparse
import json
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort


def concrete_shape(value_info, family):
    dims = value_info.type.tensor_type.shape.dim
    result = []
    for index, dim in enumerate(dims):
        value = dim.dim_value
        if value:
            result.append(int(value))
            continue
        if index == 0:
            result.append(1)
        elif index == 1:
            result.append(3)
        elif family == "det":
            result.append(640)
        elif family == "rec":
            result.append(48 if index == 2 else 320)
        else:
            result.append(80 if index == 2 else 160)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-root", required=True)
    parser.add_argument("--catalog", default=str(Path(__file__).resolve().parents[1] / "paddle-ocr-models.json"))
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root = Path(args.model_root)
    catalog = json.loads(Path(args.catalog).read_text(encoding="utf-8"))
    rows = []
    for entry in catalog["models"]:
        path = root / entry["directory"] / entry["onnx"]
        row = {"id": entry["id"], "path": str(path), "status": "passed", "error": None}
        try:
            model = onnx.load(str(path), load_external_data=False)
            session = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
            input_meta = session.get_inputs()[0]
            shape = concrete_shape(model.graph.input[0], entry["family"])
            data = np.zeros(shape, dtype=np.float32)
            outputs = session.run(None, {input_meta.name: data})
            row["inputName"] = input_meta.name
            row["inputShape"] = shape
            row["outputNames"] = [item.name for item in session.get_outputs()]
            row["outputShapes"] = [list(np.asarray(item).shape) for item in outputs]
            row["opset"] = max((item.version for item in model.opset_import if not item.domain), default=None)
        except Exception as exc:  # diagnostic tool; preserve every row
            row["status"] = "failed"
            row["error"] = str(exc)
        rows.append(row)
        print(f"{row['status']:>6} {entry['id']}")
    output = {"schemaVersion": "deploysharp-paddle-ocr-onnx-smoke-v1", "provider": "onnxruntime-cpu", "models": rows}
    Path(args.output).write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if any(item["status"] != "passed" for item in rows):
        raise SystemExit(2)


if __name__ == "__main__":
    main()
