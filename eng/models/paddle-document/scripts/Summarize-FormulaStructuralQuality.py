"""Compute conservative structural-token diagnostics for MathNet formula reports.

This deliberately avoids parsing LaTeX or claiming mathematical equivalence. It
only compares a bounded multiset of commands, identifiers, numeric literals,
grouping delimiters, sub/superscript markers and common operators.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


COMMAND = re.compile(r"\\(?:[A-Za-z]+|[^A-Za-z\s])")
NUMBER = re.compile(r"\d+(?:\.\d+)?")


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tokens(value: str) -> Counter[str]:
    result: Counter[str] = Counter()
    index = 0
    while index < len(value):
        char = value[index]
        if char.isspace():
            index += 1
            continue
        if char == "\\":
            match = COMMAND.match(value, index)
            token = match.group(0) if match else "\\"
            result[f"cmd:{token}"] += 1
            index += len(token)
            continue
        if char.isdigit():
            match = NUMBER.match(value, index)
            token = match.group(0)
            result[f"num:{token}"] += 1
            index += len(token)
            continue
        if char.isalpha():
            result["id"] += 1
        elif char in "{}_[]()":
            result[f"group:{char}"] += 1
        elif char == "_":
            result["sub"] += 1
        elif char == "^":
            result["sup"] += 1
        elif char in "+-*/=< >|:,;&".replace(" ", ""):
            result[f"op:{char}"] += 1
        index += 1
    return result


def overlap(expected: Counter[str], actual: Counter[str]) -> int:
    return sum((expected & actual).values())


def metrics(expected: str, actual: str) -> dict[str, float | int]:
    reference = tokens(expected)
    prediction = tokens(actual)
    reference_count = sum(reference.values())
    prediction_count = sum(prediction.values())
    matched = overlap(reference, prediction)
    precision = matched / prediction_count if prediction_count else 0.0
    recall = matched / reference_count if reference_count else (1.0 if prediction_count == 0 else 0.0)
    f1 = (2 * precision * recall / (precision + recall)) if precision + recall else 0.0
    return {
        "expectedStructuralTokenCount": reference_count,
        "actualStructuralTokenCount": prediction_count,
        "matchedStructuralTokenCount": matched,
        "structuralTokenPrecision": precision,
        "structuralTokenRecall": recall,
        "structuralTokenF1": f1,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-root", type=Path, required=True)
    parser.add_argument("--dataset-root", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    manifest = args.dataset_root / "realFormula-manifest.jsonl"
    references: dict[str, dict] = {}
    for line in manifest.read_text(encoding="utf-8").splitlines():
        if line.strip():
            row = json.loads(line)
            references[str(row["Image"])] = row

    reports = [
        "formula-realformula-pp-formulanet-plus-s-ort-20261007.json",
        "formula-realformula-pp-formulanet-plus-m-ort-20261007.json",
        "formula-realformula-pp-formulanet-plus-l-ort-20261007.json",
        "formula-realformula-pp-formulanet-s-ort-20261007.json",
        "formula-realformula-pp-formulanet-l-ort-20261007.json",
        "formula-realformula-unimernet-ort-20261007.json",
    ]
    models: list[dict] = []
    for name in reports:
        report = json.loads((args.input_root / name).read_text(encoding="utf-8-sig"))
        model = report["modelResults"][0]
        rows = model["results"]
        rows_metrics: list[dict] = []
        sums = Counter()
        for row in rows:
            image = str(row["image"])
            reference = references[image]
            if str(reference.get("ImageSha256")) != str(row.get("imageSha256")):
                raise ValueError(f"image SHA mismatch: {name} / {image}")
            item = metrics(str(reference["ReferenceLatex"]), str(row.get("prediction", "")))
            for key in ("expectedStructuralTokenCount", "actualStructuralTokenCount", "matchedStructuralTokenCount"):
                sums[key] += int(item[key])
            rows_metrics.append({
                "image": image,
                "imageSha256": row.get("imageSha256"),
                "referenceLength": row.get("referenceLength"),
                "reachedEndOfSequence": row.get("reachedEndOfSequence"),
                **item,
            })
        precision = sums["matchedStructuralTokenCount"] / sums["actualStructuralTokenCount"] if sums["actualStructuralTokenCount"] else 0.0
        recall = sums["matchedStructuralTokenCount"] / sums["expectedStructuralTokenCount"] if sums["expectedStructuralTokenCount"] else 0.0
        f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0.0
        models.append({
            "model": model.get("model"),
            "backend": model.get("backend"),
            "sourceReport": name,
            "sampleCount": len(rows),
            **dict(sums),
            "microStructuralTokenPrecision": precision,
            "microStructuralTokenRecall": recall,
            "microStructuralTokenF1": f1,
            "samples": rows_metrics,
        })

    result = {
        "schemaVersion": "deploysharp-realformula-structural-token-quality-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "dataset": {"name": "MathNet realFormula", "manifestSha256": digest(manifest), "sampleCount": len(references), "rawReferencePolicy": "reference LaTeX is not copied into the report"},
        "models": models,
        "boundary": "Conservative structural-token overlap is a lexical diagnostic. It does not establish TeX parsing, rendered-image equivalence, mathematical semantic equivalence, held-out accuracy or quality on another backend.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# Formula realFormula structural-token quality diagnostics",
        "",
        f"- Manifest SHA-256: `{result['dataset']['manifestSha256']}`; rows per model: `121`.",
        "- Tokens are a conservative lexical inventory of commands, identifiers, numeric literals, grouping delimiters, sub/superscripts and common operators.",
        "",
        "| Model | Samples | Expected | Actual | Matched | Micro P | Micro R | Micro F1 |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for model in models:
        lines.append(
            f"| `{model['model']}` | {model['sampleCount']} | {model['expectedStructuralTokenCount']} | {model['actualStructuralTokenCount']} | {model['matchedStructuralTokenCount']} | "
            f"{model['microStructuralTokenPrecision'] * 100:.2f}% | {model['microStructuralTokenRecall'] * 100:.2f}% | {model['microStructuralTokenF1'] * 100:.2f}% |"
        )
    lines += ["", "This is a lexical/structural diagnostic only; it must not be presented as a mathematical or rendered-quality score. The source predictions are ORT CPU and do not add OpenVINO, OpenCV DNN or TensorRT evidence."]
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown), "models": len(models)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
