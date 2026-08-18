#!/usr/bin/env python3
"""Generate a local-only PP-OCRv5 mobile golden from official PaddleOCR semantics.

This runner intentionally keeps model weights and images outside the repository. It
uses the pinned PaddleOCR source's DB contour/unclip algorithm against the local
ONNX exports so the resulting JSON can be reviewed and replayed without publishing
any test image.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import cv2
import numpy as np
import onnxruntime as ort
import pyclipper
from shapely.geometry import Polygon


PADDLEOCR_COMMIT = "2661c7c0ef5c613e8f93c6e93b2e052399f0f854"
PADDLEOCR_REPOSITORY = "https://github.com/PaddlePaddle/PaddleOCR"
PADDLEOCR_DB_SOURCE = (
    PADDLEOCR_REPOSITORY
    + "/blob/"
    + PADDLEOCR_COMMIT
    + "/ppocr/postprocess/db_postprocess.py"
)
PADDLEOCR_OPERATORS_SOURCE = (
    PADDLEOCR_REPOSITORY
    + "/blob/"
    + PADDLEOCR_COMMIT
    + "/ppocr/data/imaug/operators.py"
)
OFFICIAL_ASSET_URLS = {
    "detArchive": "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv5_mobile_det_infer.tar",
    "recArchive": "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv5_mobile_rec_infer.tar",
    "clsArchive": "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-LCNet_x0_25_textline_ori_infer.tar",
    "detImage": "https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/general_ocr_001.png",
    "recImage": "https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/general_ocr_rec_001.png",
    "clsImage": "https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/textline_rot180_demo.jpg",
}


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def file_record(path: Path) -> dict[str, Any]:
    return {
        "fileName": path.name,
        "size": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def read_bgr(path: Path) -> np.ndarray:
    image = cv2.imread(str(path), cv2.IMREAD_COLOR)
    if image is None:
        raise RuntimeError(f"Unable to read image: {path}")
    return image


def det_resize_long(image: np.ndarray, resize_long: int = 960) -> tuple[np.ndarray, float, float]:
    """Match DetResizeForTest.resize_image_type2 in the pinned PaddleOCR source."""
    height, width = image.shape[:2]
    ratio = resize_long / float(max(height, width))
    resized_height = int(height * ratio)
    resized_width = int(width * ratio)
    stride = 128
    resized_height = (resized_height + stride - 1) // stride * stride
    resized_width = (resized_width + stride - 1) // stride * stride
    resized = cv2.resize(image, (resized_width, resized_height))
    return resized, resized_height / float(height), resized_width / float(width)


def det_tensor(image: np.ndarray) -> tuple[np.ndarray, dict[str, Any]]:
    resized, ratio_h, ratio_w = det_resize_long(image)
    normalized = (resized.astype(np.float32) / np.float32(255.0) - np.array([0.485, 0.456, 0.406], dtype=np.float32)) / np.array([0.229, 0.224, 0.225], dtype=np.float32)
    tensor = np.ascontiguousarray(normalized.transpose(2, 0, 1)[np.newaxis, ...])
    return tensor, {
        "resize": "DetResizeForTest.resize_image_type2(resize_long=960,stride=128)",
        "colorOrder": "bgr",
        "normalization": "(uint8/255-[0.485,0.456,0.406])/[0.229,0.224,0.225]",
        "shape": list(tensor.shape),
        "sha256": sha256_bytes(tensor.tobytes()),
        "ratioHeight": ratio_h,
        "ratioWidth": ratio_w,
    }


def ordered_min_box(contour: np.ndarray) -> tuple[np.ndarray, float]:
    rectangle = cv2.minAreaRect(contour)
    points = sorted(cv2.boxPoints(rectangle).tolist(), key=lambda point: point[0])
    if points[1][1] > points[0][1]:
        first, fourth = 0, 1
    else:
        first, fourth = 1, 0
    if points[3][1] > points[2][1]:
        second, third = 2, 3
    else:
        second, third = 3, 2
    return np.array([points[first], points[second], points[third], points[fourth]], dtype=np.float32), min(rectangle[1])


def box_score_fast(probabilities: np.ndarray, box: np.ndarray) -> float:
    height, width = probabilities.shape[:2]
    points = box.copy()
    xmin = int(np.clip(np.floor(points[:, 0].min()), 0, width - 1))
    xmax = int(np.clip(np.ceil(points[:, 0].max()), 0, width - 1))
    ymin = int(np.clip(np.floor(points[:, 1].min()), 0, height - 1))
    ymax = int(np.clip(np.ceil(points[:, 1].max()), 0, height - 1))
    mask = np.zeros((ymax - ymin + 1, xmax - xmin + 1), dtype=np.uint8)
    points[:, 0] -= xmin
    points[:, 1] -= ymin
    cv2.fillPoly(mask, points.reshape(1, -1, 2).astype(np.int32), 1)
    return float(cv2.mean(probabilities[ymin : ymax + 1, xmin : xmax + 1], mask)[0])


def unclip(box: np.ndarray, ratio: float) -> list[list[int]]:
    polygon = Polygon(box)
    distance = polygon.area * ratio / polygon.length
    offset = pyclipper.PyclipperOffset()
    offset.AddPath(box.tolist(), pyclipper.JT_ROUND, pyclipper.ET_CLOSEDPOLYGON)
    return offset.Execute(distance)


def db_boxes_from_bitmap(probabilities: np.ndarray, destination_width: int, destination_height: int) -> tuple[list[dict[str, Any]], int]:
    """Match DBPostProcess.boxes_from_bitmap with the archive's default quad mode."""
    bitmap = probabilities > 0.3
    contours, _ = cv2.findContours((bitmap * 255).astype(np.uint8), cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    boxes: list[dict[str, Any]] = []
    for contour in contours[:1000]:
        points, short_side = ordered_min_box(contour)
        if short_side < 3:
            continue
        score = box_score_fast(probabilities, points)
        if score < 0.6:
            continue
        expanded = unclip(points, 1.5)
        if len(expanded) > 1:
            continue
        expanded_points = np.array(expanded, dtype=np.float32).reshape(-1, 1, 2)
        points, short_side = ordered_min_box(expanded_points)
        if short_side < 5:
            continue
        points[:, 0] = np.clip(np.round(points[:, 0] / probabilities.shape[1] * destination_width), 0, destination_width)
        points[:, 1] = np.clip(np.round(points[:, 1] / probabilities.shape[0] * destination_height), 0, destination_height)
        boxes.append({
            "score": score,
            "points": [[int(point[0]), int(point[1])] for point in points],
        })
    return boxes, len(contours)


def rec_tensor(image: np.ndarray) -> tuple[np.ndarray, dict[str, Any]]:
    """Match RecResizeImg([3,48,320]) in the pinned inference archive."""
    resized = cv2.resize(image, (320, 48))
    tensor = np.ascontiguousarray(resized.transpose(2, 0, 1).astype(np.float32) / np.float32(255.0))
    tensor = (tensor - np.float32(0.5)) / np.float32(0.5)
    tensor = tensor[np.newaxis, ...]
    return tensor, {
        "resize": "RecResizeImg(image_shape=[3,48,320])",
        "colorOrder": "bgr",
        "normalization": "(uint8/255-0.5)/0.5",
        "shape": list(tensor.shape),
        "sha256": sha256_bytes(tensor.tobytes()),
    }


def decode_ctc(probabilities: np.ndarray, dictionary: list[str]) -> dict[str, Any]:
    if probabilities.ndim != 3 or probabilities.shape[0] != 1:
        raise RuntimeError(f"Expected recognizer output [1,T,C], got {probabilities.shape}")
    indexes = probabilities[0].argmax(axis=1)
    confidences = probabilities[0].max(axis=1)
    tokens: list[int] = []
    token_confidences: list[float] = []
    previous = -1
    for index, confidence in zip(indexes.tolist(), confidences.tolist()):
        if index != 0 and index != previous:
            tokens.append(index)
            token_confidences.append(float(confidence))
        previous = index
    if any(index >= len(dictionary) for index in tokens):
        raise RuntimeError("Recognizer selected an index outside the pinned dictionary.")
    return {
        "indexes": tokens,
        "text": "".join(dictionary[index] for index in tokens),
        "confidence": float(np.mean(token_confidences)) if token_confidences else 0.0,
    }


def cls_tensor(image: np.ndarray) -> tuple[np.ndarray, dict[str, Any]]:
    """Match the PP-LCNet textline orientation archive's RGB preprocessing."""
    resized = cv2.resize(image, (160, 80))
    rgb = cv2.cvtColor(resized, cv2.COLOR_BGR2RGB)
    normalized = (rgb.astype(np.float32) / np.float32(255.0) - np.array([0.485, 0.456, 0.406], dtype=np.float32)) / np.array([0.229, 0.224, 0.225], dtype=np.float32)
    tensor = np.ascontiguousarray(normalized.transpose(2, 0, 1)[np.newaxis, ...])
    return tensor, {
        "resize": "ResizeImage(size=[160,80])",
        "colorOrder": "rgb",
        "normalization": "(uint8/255-[0.485,0.456,0.406])/[0.229,0.224,0.225]",
        "shape": list(tensor.shape),
        "sha256": sha256_bytes(tensor.tobytes()),
    }


def run_model(model: Path, tensor: np.ndarray) -> tuple[np.ndarray, dict[str, Any]]:
    session = ort.InferenceSession(str(model), providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name
    output_name = session.get_outputs()[0].name
    output = session.run([output_name], {input_name: tensor})[0]
    return output, {
        "input": input_name,
        "output": output_name,
        "shape": list(output.shape),
        "sha256": sha256_bytes(np.ascontiguousarray(output).tobytes()),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--det-model", type=Path, required=True)
    parser.add_argument("--rec-model", type=Path, required=True)
    parser.add_argument("--cls-model", type=Path, required=True)
    parser.add_argument("--dictionary", type=Path, required=True)
    parser.add_argument("--det-image", type=Path, required=True)
    parser.add_argument("--rec-image", type=Path, required=True)
    parser.add_argument("--cls-image", type=Path, required=True)
    parser.add_argument("--det-archive", type=Path, required=True)
    parser.add_argument("--rec-archive", type=Path, required=True)
    parser.add_argument("--cls-archive", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    required = [
        args.det_model, args.rec_model, args.cls_model, args.dictionary,
        args.det_image, args.rec_image, args.cls_image,
        args.det_archive, args.rec_archive, args.cls_archive,
    ]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise RuntimeError("Missing required evidence files: " + "; ".join(missing))

    det_image = read_bgr(args.det_image)
    det_input, det_input_record = det_tensor(det_image)
    det_output, det_output_record = run_model(args.det_model, det_input)
    if det_output.shape[0:2] != (1, 1):
        raise RuntimeError(f"Expected detector output [1,1,H,W], got {det_output.shape}")
    det_boxes, contour_count = db_boxes_from_bitmap(det_output[0, 0], det_image.shape[1], det_image.shape[0])

    rec_input, rec_input_record = rec_tensor(read_bgr(args.rec_image))
    rec_output, rec_output_record = run_model(args.rec_model, rec_input)
    dictionary = [""] + args.dictionary.read_text(encoding="utf-8").splitlines()

    cls_input, cls_input_record = cls_tensor(read_bgr(args.cls_image))
    cls_output, cls_output_record = run_model(args.cls_model, cls_input)
    if cls_output.shape != (1, 2):
        raise RuntimeError(f"Expected classifier output [1,2], got {cls_output.shape}")
    cls_index = int(cls_output[0].argmax())

    result = {
        "schemaVersion": "1.0",
        "purpose": "local-only PP-OCRv5 mobile official-semantics golden; test images are not release assets",
        "paddleOcr": {
            "repository": PADDLEOCR_REPOSITORY,
            "commit": PADDLEOCR_COMMIT,
            "dbPostprocessSource": PADDLEOCR_DB_SOURCE,
            "preprocessSource": PADDLEOCR_OPERATORS_SOURCE,
        },
        "officialAssetUrls": OFFICIAL_ASSET_URLS,
        "referenceArchives": {
            "det": file_record(args.det_archive),
            "rec": file_record(args.rec_archive),
            "cls": file_record(args.cls_archive),
        },
        "models": {
            "det": file_record(args.det_model),
            "rec": file_record(args.rec_model),
            "cls": file_record(args.cls_model),
            "dictionary": file_record(args.dictionary),
        },
        "detection": {
            "image": file_record(args.det_image),
            "input": det_input_record,
            "output": det_output_record,
            "contoursBeforeFiltering": contour_count,
            "boxes": det_boxes,
        },
        "recognition": {
            "image": file_record(args.rec_image),
            "input": rec_input_record,
            "output": rec_output_record,
            "ctc": decode_ctc(rec_output, dictionary),
        },
        "orientation": {
            "image": file_record(args.cls_image),
            "input": cls_input_record,
            "output": cls_output_record,
            "classIndex": cls_index,
            "label": ["0_degree", "180_degree"][cls_index],
            "confidence": float(cls_output[0, cls_index]),
        },
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
    print("DEPLOYSHARP_PADDLE_OCR_OFFICIAL_GOLDEN_OK output=" + str(args.output.resolve()))


if __name__ == "__main__":
    main()
