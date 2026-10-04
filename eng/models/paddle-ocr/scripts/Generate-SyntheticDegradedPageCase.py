"""Generate deterministic full-page OCR degradation cases from annotated SROIE pages."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path

import cv2
import numpy as np


CONDITIONS = ("normal", "low-contrast", "blur", "noise", "shadow", "jpeg")


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stable_seed(image_id: str, condition: str, severity: str, seed: int) -> int:
    material = f"{seed}:{image_id}:{condition}:{severity}".encode("utf-8")
    return int.from_bytes(hashlib.sha256(material).digest()[:8], "little") & 0xFFFFFFFF


def degrade(image: np.ndarray, condition: str, severity: str, seed: int) -> np.ndarray:
    if condition == "normal":
        return image.copy()
    if condition == "low-contrast":
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        gray = cv2.cvtColor(gray, cv2.COLOR_GRAY2BGR)
        alpha = 0.12 if severity == "severe" else 0.32
        return cv2.addWeighted(image, alpha, gray, 1.0 - alpha, 0)
    if condition == "blur":
        kernel = (9, 9) if severity == "severe" else (5, 5)
        sigma = 3.0 if severity == "severe" else 1.25
        return cv2.GaussianBlur(image, kernel, sigma)
    if condition == "noise":
        rng = np.random.default_rng(seed)
        sigma = 35.0 if severity == "severe" else 16.0
        noise = rng.normal(0.0, sigma, image.shape).astype(np.float32)
        return np.clip(image.astype(np.float32) + noise, 0, 255).astype(np.uint8)
    if condition == "shadow":
        height, width = image.shape[:2]
        end = 0.18 if severity == "severe" else 0.48
        gradient = np.linspace(1.0, end, max(1, width), dtype=np.float32)
        mask = np.tile(gradient[None, :, None], (height, 1, 3))
        return np.clip(image.astype(np.float32) * mask, 0, 255).astype(np.uint8)
    if condition == "jpeg":
        quality = 15 if severity == "severe" else 35
        ok, encoded = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, quality])
        if not ok:
            raise RuntimeError("JPEG encoding failed")
        decoded = cv2.imdecode(encoded, cv2.IMREAD_COLOR)
        if decoded is None:
            raise RuntimeError("JPEG decoding failed")
        return decoded
    raise ValueError(f"unknown condition: {condition}")


def read_records(paths: list[Path], maximum: int) -> list[dict]:
    records: list[dict] = []
    seen: set[str] = set()
    for path in paths:
        for line in path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            record = json.loads(line)
            image_id = str(record.get("image_id", ""))
            if not image_id or image_id in seen:
                continue
            seen.add(image_id)
            records.append(record)
            if len(records) >= maximum:
                return records
    return records


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-root", type=Path, required=True)
    parser.add_argument("--source-manifest", type=Path, action="append", required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--max-pages", type=int, default=10)
    parser.add_argument("--severity", choices=("mild", "severe"), default="severe")
    parser.add_argument("--conditions", default=",".join(CONDITIONS))
    parser.add_argument("--seed", type=int, default=20261004)
    args = parser.parse_args()
    if args.max_pages < 1 or args.max_pages > 128:
        raise SystemExit("max-pages must be between 1 and 128")
    conditions = tuple(value.strip().lower() for value in args.conditions.split(",") if value.strip())
    if not conditions or any(value not in CONDITIONS for value in conditions):
        raise SystemExit(f"conditions must be selected from: {', '.join(CONDITIONS)}")

    dataset_root = args.dataset_root.resolve()
    output_root = args.output_root.resolve()
    records = read_records([path.resolve() for path in args.source_manifest], args.max_pages)
    if not records:
        raise SystemExit("no source records were selected")

    image_root = output_root / "data" / "images" / "sroie-page-degraded" / args.severity
    manifest_path = output_root / "data" / "annotations" / "manifests" / f"sroie-page-degraded-{args.severity}.jsonl"
    image_root.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    generated: list[dict] = []

    for record in records:
        relative_source = Path(*str(record["image_relpath"]).split("/"))
        source_path = (dataset_root / relative_source).resolve()
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        source_image = cv2.imread(str(source_path), cv2.IMREAD_COLOR)
        if source_image is None:
            raise RuntimeError(f"failed to decode source image: {source_path}")
        source_digest = digest(source_path)
        for condition in conditions:
            image_id = f"{record['image_id']}-degraded-{condition}-{args.severity}"
            relative = Path("data") / "images" / "sroie-page-degraded" / args.severity / f"{image_id}.jpg"
            destination = output_root / relative
            if condition == "normal":
                shutil.copyfile(source_path, destination)
            else:
                output = degrade(source_image, condition, args.severity, stable_seed(str(record["image_id"]), condition, args.severity, args.seed))
                if not cv2.imwrite(str(destination), output, [cv2.IMWRITE_JPEG_QUALITY, 95]):
                    raise RuntimeError(f"failed to encode {destination}")
            generated_record = copy.deepcopy(record)
            generated_record["image_id"] = image_id
            generated_record["image_relpath"] = relative.as_posix()
            generated_record["width"] = int(source_image.shape[1])
            generated_record["height"] = int(source_image.shape[0])
            generated_record["source_split"] = f"{record.get('source_split', 'unknown')}-degraded-{args.severity}"
            generated_record["benchmark_eligibility"] = "controlled_full_page_degradation_smoke"
            provenance = dict(generated_record.get("source_provenance") or {})
            provenance.update(
                {
                    "generator": "Generate-SyntheticDegradedPageCase.py",
                    "generator_seed": args.seed,
                    "parent_image_id": record["image_id"],
                    "parent_image_sha256": source_digest,
                    "condition": condition,
                    "severity": args.severity,
                    "image_sha256": digest(destination),
                }
            )
            generated_record["source_provenance"] = provenance
            generated.append(generated_record)

    manifest_path.write_text("\n".join(json.dumps(item, ensure_ascii=False, separators=(",", ":")) for item in generated) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "records": len(generated), "pages": len(records), "conditions": list(conditions), "severity": args.severity}, ensure_ascii=False))


if __name__ == "__main__":
    main()
