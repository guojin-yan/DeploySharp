#!/usr/bin/env python3
"""Acquire pinned Stage 27 document-model assets into the external warehouse."""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import sys
from datetime import datetime, timezone
from pathlib import Path

import huggingface_hub
from huggingface_hub import hf_hub_download, snapshot_download


MODELS = (
    {
        "name": "layoutlmv3-base",
        "repo": "microsoft/layoutlmv3-base",
        "revision": "cfbbbff0762e6aab37086fdd4739ad14fe7d5db4",
        "license": "CC-BY-NC-SA-4.0",
        "mode": "contract",
        "allow": (
            "README.md",
            "config.json",
            "preprocessor_config.json",
            "tokenizer_config.json",
            "vocab.json",
            "merges.txt",
        ),
    },
    {
        "name": "donut-base-finetuned-cord-v2",
        "repo": "naver-clova-ix/donut-base-finetuned-cord-v2",
        "revision": "8003d433113256b4ce3a0f5bf604b29ff78a7451",
        "license": "MIT",
        "mode": "executable",
        "allow": (
            "README.md",
            "added_tokens.json",
            "config.json",
            "preprocessor_config.json",
            "pytorch_model.bin",
            "sentencepiece.bpe.model",
            "special_tokens_map.json",
            "tokenizer.json",
            "tokenizer_config.json",
        ),
    },
    {
        "name": "pix2struct-docvqa-base",
        "repo": "google/pix2struct-docvqa-base",
        "revision": "63f6b3de436e39f75c7a486881a9c2c14a7f4e89",
        "license": "Apache-2.0",
        "mode": "contract",
        "allow": (
            "README.md",
            "config.json",
            "preprocessor_config.json",
            "special_tokens_map.json",
            "spiece.model",
            "tokenizer.json",
            "tokenizer_config.json",
        ),
    },
)

CORD_DATASET = {
    "repo": "naver-clova-ix/cord-v2",
    "revision": "7f0115a4b758a71d6473b8d085751692da2fef98",
    "license": "CC-BY-4.0",
    "file": "data/test-00000-of-00001-9c204eb3f4e11791.parquet",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def inventory(root: Path) -> list[dict[str, object]]:
    return [
        {
            "relativePath": path.relative_to(root).as_posix(),
            "size": path.stat().st_size,
            "sha256": sha256(path),
        }
        for path in sorted(root.rglob("*"))
        if path.is_file() and ".cache" not in path.parts
    ]


def acquire_model(warehouse: Path, spec: dict[str, object]) -> None:
    root = warehouse / str(spec["name"])
    checkpoint = root / "checkpoint"
    checkpoint.mkdir(parents=True, exist_ok=True)
    snapshot_download(
        repo_id=str(spec["repo"]),
        revision=str(spec["revision"]),
        local_dir=checkpoint,
        allow_patterns=list(spec["allow"]),
    )
    audit = {
        "schemaVersion": "1.0",
        "accessedAt": datetime.now(timezone.utc).isoformat(),
        "repository": spec["repo"],
        "revision": spec["revision"],
        "license": spec["license"],
        "admission": spec["mode"],
        "redistributionAllowed": False,
        "uploaded": False,
        "downloadable": False,
        "files": inventory(checkpoint),
    }
    evidence = root / "evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / "source-audit.json").write_text(
        json.dumps(audit, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )


def acquire_cord_test(warehouse: Path) -> None:
    root = warehouse / "donut-base-finetuned-cord-v2" / "dataset"
    root.mkdir(parents=True, exist_ok=True)
    cached = hf_hub_download(
        repo_id=CORD_DATASET["repo"],
        repo_type="dataset",
        revision=CORD_DATASET["revision"],
        filename=CORD_DATASET["file"],
    )
    target = root / Path(CORD_DATASET["file"]).name
    if not target.exists() or sha256(target) != sha256(Path(cached)):
        target.write_bytes(Path(cached).read_bytes())
    audit = {
        "repository": CORD_DATASET["repo"],
        "revision": CORD_DATASET["revision"],
        "license": CORD_DATASET["license"],
        "file": target.name,
        "size": target.stat().st_size,
        "sha256": sha256(target),
    }
    (root / "source-audit.json").write_text(
        json.dumps(audit, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--warehouse", type=Path, required=True)
    parser.add_argument("--include-cord-test", action="store_true")
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args()
    args.warehouse.mkdir(parents=True, exist_ok=True)
    for model in MODELS:
        if args.audit_only:
            checkpoint = args.warehouse / str(model["name"]) / "checkpoint"
            if not checkpoint.is_dir():
                raise FileNotFoundError(checkpoint)
            evidence = args.warehouse / str(model["name"]) / "evidence"
            evidence.mkdir(parents=True, exist_ok=True)
            audit = {
                "schemaVersion": "1.0",
                "accessedAt": datetime.now(timezone.utc).isoformat(),
                "repository": model["repo"],
                "revision": model["revision"],
                "license": model["license"],
                "admission": model["mode"],
                "redistributionAllowed": False,
                "uploaded": False,
                "downloadable": False,
                "files": inventory(checkpoint),
            }
            (evidence / "source-audit.json").write_text(
                json.dumps(audit, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
            )
        else:
            acquire_model(args.warehouse, model)
    if args.include_cord_test and not args.audit_only:
        acquire_cord_test(args.warehouse)
    environment = {
        "python": platform.python_version(),
        "huggingfaceHub": huggingface_hub.__version__,
        "command": " ".join(sys.argv),
    }
    target = args.warehouse / "donut-base-finetuned-cord-v2" / "evidence" / "acquisition-environment.json"
    target.write_text(json.dumps(environment, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
