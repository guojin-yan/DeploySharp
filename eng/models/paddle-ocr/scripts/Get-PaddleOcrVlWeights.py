"""Acquire the pinned official PaddleOCR-VL-1.6 weights outside the repository."""

from __future__ import annotations

import argparse
import hashlib
import json
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
EXPECTED_BYTES = 1917255968
EXPECTED_SHA256 = "85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db"
URL = f"https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6/resolve/{REVISION}/model.safetensors"


def file_hash(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    args.model_root.mkdir(parents=True, exist_ok=True)
    target = args.model_root / "model.safetensors"
    partial = target.with_suffix(".safetensors.partial")
    if target.exists():
        if target.stat().st_size != EXPECTED_BYTES or file_hash(target) != EXPECTED_SHA256:
            raise ValueError("Existing model.safetensors differs from the pinned official file; preserved without overwriting.")
    else:
        for attempt in range(5):
            offset = partial.stat().st_size if partial.exists() else 0
            if offset > EXPECTED_BYTES:
                raise ValueError("Partial file exceeds expected size; preserved for inspection.")
            if offset == EXPECTED_BYTES:
                break
            headers = {"User-Agent": "DeploySharp-PaddleOCR-VL-acquisition/1"}
            if offset:
                headers["Range"] = f"bytes={offset}-"
            request = urllib.request.Request(URL, headers=headers)
            try:
                with urllib.request.urlopen(request, timeout=45) as response:
                    if offset and (response.status != 206 or not response.headers.get("Content-Range", "").startswith(f"bytes {offset}-")):
                        raise ValueError("Server did not accept exact resume offset; partial file preserved.")
                    with partial.open("ab" if offset else "wb") as output:
                        checkpoint = offset // (32 * 1024 * 1024)
                        while data := response.read(1024 * 1024):
                            if offset + len(data) > EXPECTED_BYTES:
                                raise ValueError("Weight response exceeds pinned file size.")
                            output.write(data)
                            offset += len(data)
                            if offset // (32 * 1024 * 1024) != checkpoint:
                                checkpoint = offset // (32 * 1024 * 1024)
                                print(f"DOWNLOADED {offset}/{EXPECTED_BYTES}", flush=True)
                if offset != EXPECTED_BYTES:
                    raise OSError("Weight response ended before pinned file size.")
                break
            except (OSError, urllib.error.URLError) as error:
                print(f"DOWNLOAD_ATTEMPT {attempt + 1} failed ({type(error).__name__}); partial file retained.", flush=True)
                if attempt == 4:
                    raise RuntimeError("Pinned weight acquisition did not finish after five attempts.") from None
                time.sleep(1 + attempt)
        if partial.stat().st_size != EXPECTED_BYTES or file_hash(partial) != EXPECTED_SHA256:
            raise ValueError("Downloaded weights failed pinned size/SHA256; partial file preserved.")
        partial.replace(target)
    report = {"schemaVersion": 1, "verifiedUtc": datetime.now(timezone.utc).isoformat(),
              "repository": "PaddlePaddle/PaddleOCR-VL-1.6", "revision": REVISION,
              "fileName": target.name, "bytes": EXPECTED_BYTES, "sha256": EXPECTED_SHA256,
              "sourceUrl": URL, "status": "verified-local-file",
              "scriptSha256": file_hash(Path(__file__)),
              "boundary": "Verified weight acquisition only; no model generation or ONNX backend support is established."}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"VERIFIED {target.name} {EXPECTED_SHA256}", flush=True)


if __name__ == "__main__":
    main()
