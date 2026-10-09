"""Merge sharded PaddleOCR public-dataset runs into one auditable run.

The benchmark runner deliberately supports bounded image selections.  This
utility joins those selections after they have completed, while refusing to
hide protocol or provenance differences.  It copies only machine-generated
CSV/width sidecars and the selected JSONL records; source images and
predictions remain outside the repository.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
from typing import Any


PROTOCOL_FIELDS = (
    "model",
    "backend",
    "sourceRevision",
    "benchmarkAssemblySha256",
    "warmup",
    "iterations",
    "batchSize",
    "inferenceChannels",
    "maximumRegions",
    "overflowMode",
    "windowOverlap",
    "maximumWindowsPerRegion",
    "pipelineTimeoutMs",
)


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read_manifest(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip():
            continue
        try:
            record = json.loads(line)
        except json.JSONDecodeError as exc:
            raise ValueError(f"Invalid JSON in {path} line {line_number}: {exc}") from exc
        if not isinstance(record, dict) or not record.get("image_id"):
            raise ValueError(f"Manifest record in {path} line {line_number} has no image_id")
        records.append(record)
    if not records:
        raise ValueError(f"Manifest is empty: {path}")
    return records


def copy_without_overwrite(source: Path, destination: Path) -> None:
    if destination.exists():
        raise ValueError(f"Duplicate generated artifact would overwrite {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def copy_run_artifacts(source: Path, output: Path) -> None:
    for relative in ("csv", "raw"):
        source_root = source / relative
        if not source_root.is_dir():
            raise FileNotFoundError(f"Run is missing {relative} directory: {source_root}")
        for item in source_root.rglob("*"):
            if item.is_dir():
                continue
            copy_without_overwrite(item, output / relative / item.relative_to(source_root))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-directory", required=True, action="append", type=Path, dest="runs")
    parser.add_argument("--output-directory", required=True, type=Path)
    args = parser.parse_args()

    if len(args.runs) < 2:
        raise ValueError("At least two --run-directory values are required")
    runs = [path.resolve() for path in args.runs]
    output = args.output_directory.resolve()
    if output.exists() and any(output.iterdir()):
        raise FileExistsError(f"Output directory is not empty: {output}")

    base_metadata: dict[str, Any] | None = None
    all_records: list[dict[str, Any]] = []
    image_ids: set[str] = set()
    source_manifest_hashes: list[str] = []

    for run in runs:
        metadata_path = run / "run.json"
        selected_manifest = run / "selected-manifest.jsonl"
        if not metadata_path.is_file() or not selected_manifest.is_file():
            raise FileNotFoundError(f"Run must contain run.json and selected-manifest.jsonl: {run}")
        metadata = read_json(metadata_path)
        if base_metadata is None:
            base_metadata = metadata
        else:
            differences = {
                field: {"first": base_metadata.get(field), "current": metadata.get(field)}
                for field in PROTOCOL_FIELDS
                if base_metadata.get(field) != metadata.get(field)
            }
            if differences:
                raise ValueError(f"Run protocol/provenance differs for {run}: {json.dumps(differences, ensure_ascii=False)}")

        selected_manifest_sha256 = sha256_file(selected_manifest)
        expected_manifest_sha256 = metadata.get("selectedManifestSha256")
        if expected_manifest_sha256 and str(expected_manifest_sha256).lower() != selected_manifest_sha256:
            raise ValueError(
                f"Selected manifest SHA-256 differs from run metadata for {run}: "
                f"expected {expected_manifest_sha256}, actual {selected_manifest_sha256}"
            )
        records = read_manifest(selected_manifest)
        expected_image_count = metadata.get("selectedImageCount")
        if expected_image_count is not None and int(expected_image_count) != len(records):
            raise ValueError(
                f"Selected image count differs from run metadata for {run}: "
                f"expected {expected_image_count}, actual {len(records)}"
            )
        for record in records:
            image_id = str(record["image_id"])
            if image_id in image_ids:
                raise ValueError(f"Duplicate image_id across runs: {image_id}")
            image_ids.add(image_id)
            all_records.append(record)
        source_manifest_hashes.append(selected_manifest_sha256)

    assert base_metadata is not None
    output.mkdir(parents=True, exist_ok=True)
    for run in runs:
        copy_run_artifacts(run, output)

    manifest_bytes = ("".join(json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n" for record in all_records)).encode("utf-8")
    selected_manifest_path = output / "selected-manifest.jsonl"
    selected_manifest_path.write_bytes(manifest_bytes)
    selected_manifest_sha256 = sha256_bytes(manifest_bytes)

    merged_metadata = dict(base_metadata)
    merged_metadata.update(
        {
            "generatedUtc": datetime.now(timezone.utc).isoformat(),
            "manifest": "merged-selected-manifest.jsonl",
            "manifestSha256": selected_manifest_sha256,
            "sourceManifests": [str((run / "selected-manifest.jsonl").resolve()) for run in runs],
            "sourceManifestSha256s": source_manifest_hashes,
            "selectedManifest": str(selected_manifest_path),
            "selectedManifestSha256": selected_manifest_sha256,
            "selectedImageCount": len(all_records),
            "startIndex": 0,
            "mergedRunCount": len(runs),
            "mergeTool": "Merge-PaddleOcrPublicDatasetRuns.py",
        }
    )
    (output / "run.json").write_text(json.dumps(merged_metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "outputDirectory": str(output),
        "runs": len(runs),
        "images": len(all_records),
        "selectedManifestSha256": selected_manifest_sha256,
        "sourceManifestSha256s": source_manifest_hashes,
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (FileNotFoundError, FileExistsError, ValueError) as exc:
        raise SystemExit(f"error: {exc}") from exc
