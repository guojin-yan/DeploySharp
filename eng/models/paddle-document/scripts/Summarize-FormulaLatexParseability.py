"""Run a reproducible SymPy/ANTLR LaTeX parseability diagnostic over formula predictions.

Parse acceptance is intentionally not treated as rendering success or mathematical
semantic equivalence. The reports are useful for separating malformed output from
well-formed-but-wrong formulas.
"""

from __future__ import annotations

import argparse
import contextlib
import io
import json
import re
import statistics
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def load(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object: {path}")
    return value


def parse_prediction(text: str) -> tuple[bool, str | None]:
    # SymPy emits grammar diagnostics through stderr; keep the machine report clean.
    from sympy.parsing.latex import parse_latex

    try:
        with contextlib.redirect_stderr(io.StringIO()):
            parse_latex(text)
        return True, None
    except Exception as error:  # parser versions expose different exception classes
        message = re.sub(r"\s+", " ", str(error)).strip()
        return False, message[:240] or type(error).__name__


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--reports-root", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    args = parser.parse_args()

    summary = load(args.summary.resolve())
    model_rows: list[dict[str, Any]] = []
    for model in summary.get("models", []):
        report_path = args.reports_root.resolve() / str(model["report"])
        report = load(report_path)
        results = report.get("modelResults", report.get("results", []))
        # The six-model files store a single modelResults entry; tolerate both shapes.
        if isinstance(results, list) and results and isinstance(results[0], dict) and isinstance(results[0].get("results"), list):
            results = results[0]["results"]
        accepted = 0
        failures: Counter[str] = Counter()
        examples: list[dict[str, str]] = []
        for row in results:
            prediction = str(row.get("prediction", ""))
            ok, error = parse_prediction(prediction)
            if ok:
                accepted += 1
            else:
                failures[error or "unknown"] += 1
                if len(examples) < 5:
                    examples.append({"image": str(row.get("image", "")), "error": error or "unknown"})
        model_rows.append({
            "model": model.get("model"),
            "report": str(model.get("report")),
            "modelSha256": model.get("modelSha256"),
            "sampleCount": len(results),
            "parseAccepted": accepted,
            "parseRejected": len(results) - accepted,
            "parseAcceptanceRate": accepted / len(results) if results else None,
            "eosCount": sum(bool(row.get("reachedEndOfSequence")) for row in results),
            "failureReasons": [{"reason": reason, "count": count} for reason, count in failures.most_common()],
            "failureExamples": examples,
        })
    total = sum(int(row["sampleCount"]) for row in model_rows)
    accepted = sum(int(row["parseAccepted"]) for row in model_rows)
    result = {
        "schemaVersion": "deploysharp-formula-latex-parseability-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "parser": {"library": "SymPy", "grammar": "ANTLR LaTeX", "runtime": "antlr4-python3-runtime 4.11", "purpose": "syntax/parseability diagnostic only"},
        "datasetSummary": {"path": str(args.summary), "sha256": __import__("hashlib").sha256(args.summary.read_bytes()).hexdigest(), "sampleCount": total},
        "models": model_rows,
        "aggregate": {"sampleCount": total, "parseAccepted": accepted, "parseRejected": total - accepted, "parseAcceptanceRate": accepted / total if total else None},
        "boundary": "SymPy/ANTLR parse acceptance is not TeX rendering, visual equivalence, or mathematical semantic correctness. Predictions and source images remain governed by the existing realFormula license and evidence boundaries.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# Formula LaTeX parseability diagnostic (2026-10-12)",
        "",
        "This diagnostic uses SymPy's ANTLR LaTeX parser to distinguish parser-accepted output from malformed output. Acceptance is not rendering success or mathematical semantic equivalence.",
        "",
        f"- Samples: **{total}**; parser accepted: **{accepted}**; rejected: **{total - accepted}**; acceptance rate: **{accepted / total:.2%}**",
        "",
        "| Model | Samples | EOS | Parse accepted | Parse rejected | Acceptance |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for row in model_rows:
        lines.append(f"| `{row['model']}` | {row['sampleCount']} | {row['eosCount']} | {row['parseAccepted']} | {row['parseRejected']} | {row['parseAcceptanceRate']:.2%} |")
    lines.extend(["", "## Boundary", "", result["boundary"]])
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"json": str(args.output_json), "markdown": str(args.output_markdown), "samples": total}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
