"""Crop fixed detector outputs independently with Pillow for an official VL reference.

Consumes the preparation-only production C# report; does not generate text or
make a document accuracy claim. Every selected detector region is preserved;
only routed regions produce a crop, with floor/ceil and page clipping.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path

from PIL import Image, ImageOps

REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
TASKS = {"TextRecognition": "ocr", "TableRecognition": "table", "FormulaRecognition": "formula",
         "ChartRecognition": "chart", "TextSpotting": "spotting", "SealRecognition": "seal"}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pages", type=Path, required=True)
    parser.add_argument("--prepared-report", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    pages = json.loads(args.pages.read_text(encoding="utf-8"))
    prepared = json.loads(args.prepared_report.read_text(encoding="utf-8"))
    if (prepared["status"] != "real-layout-crops-prepared" or prepared["checkpointRevision"] != REVISION
            or prepared["manifestSha256"] != digest(args.pages)):
        raise ValueError("Unexpected or mismatched preparation report")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    by_id = {page["pageIndex"]: page for page in pages["pages"]}
    if len(by_id) != len(pages["pages"]):
        raise ValueError("Page IDs must be unique")
    cases = []
    seen = set()
    for region in prepared["regions"]:
        identifier = region["id"]
        if not identifier or Path(identifier).name != identifier or identifier in seen:
            raise ValueError("Region IDs must be safe unique file names")
        seen.add(identifier)
        page = by_id[region["pageIndex"]]
        path = args.pages.parent / page["image"]
        if digest(path) != page["sha256"]:
            raise ValueError("Page input SHA mismatch")
        with Image.open(path) as encoded:
            image = ImageOps.exif_transpose(encoded).convert("RGB")
        if image.size != (page["width"], page["height"]):
            raise ValueError("Decoded page size mismatch")
        bounds = region["sourceBounds"]
        if not all(math.isfinite(value) for value in bounds.values()) or bounds["width"] <= 0 or bounds["height"] <= 0:
            raise ValueError("Invalid source rectangle")
        # Recreate the exact RectangleF components, then add in double as the
        # production clipping adapter does before floor/ceil.
        def single(value: float) -> float:
            return struct.unpack("<f", struct.pack("<f", value))[0]
        x, y, width, height = (single(bounds[key]) for key in ("x", "y", "width", "height"))
        if x + width <= 0 or y + height <= 0 or x >= image.width or y >= image.height:
            raise ValueError("Nonintersecting source rectangle")
        left, top = math.floor(max(0, min(image.width - 1, x))), math.floor(max(0, min(image.height - 1, y)))
        right = math.ceil(max(left + 1, min(image.width, x + width)))
        bottom = math.ceil(max(top + 1, min(image.height, y + height)))
        if left >= right or top >= bottom:
            raise ValueError("Empty or nonintersecting crop")
        crop = image.crop((left, top, right, bottom))
        identity = hashlib.sha256(f"deploysharp-paddleocr-vl-rgb8-v1\n{crop.width}x{crop.height}\n".encode() + crop.tobytes()).hexdigest()
        if list(crop.size) != region["sourceSize"] or identity != region["cropRgbSha256"]:
            raise ValueError(f"Independent RGB crop differs from C#: {identifier}")
        target = output / (identifier + ".png")
        crop.save(target)
        cases.append({"id": identifier, "task": TASKS[region["task"]], "image": target.name, "sha256": digest(target),
                      "source": {"dataset": "FUNSD", "page": page["image"], "pageSha256": page["sha256"],
                                 "detectorRegionIndex": region["regionIndex"], "sourceBounds": bounds,
                                 "integerCrop": [left, top, right, bottom], "cropRgbSha256": identity,
                                 "managedPatchSha256": region["patchSha256"]},
                      "scope": "Actual detector crop, independent Pillow RGB validation; not labeled quality."})
    if not cases:
        raise ValueError("No routed regions were prepared")
    manifest = {"checkpointRevision": REVISION, "pagesManifestSha256": digest(args.pages),
                "preparedReportSha256": digest(args.prepared_report), "scriptSha256": digest(Path(__file__)), "cases": cases}
    (output / "inputs.json").write_text(json.dumps(manifest, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
    print(f"PREPARED {len(cases)} crops; independent RGB identity exact", flush=True)


if __name__ == "__main__":
    main()
