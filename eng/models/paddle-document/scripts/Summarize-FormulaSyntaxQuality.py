#!/usr/bin/env python3
"""Conservative LaTeX syntax/shape diagnostics for the MathNet formula reports.

This is deliberately not a TeX engine. It validates the subset needed to detect
unclosed groups/environments and missing arguments for common formula commands,
then emits an atom-abstracted structure fingerprint. A fingerprint match is a
layout/token-shape diagnostic, never a mathematical-equivalence score.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


COMMAND_RE = re.compile(r"\\(?:[A-Za-z]+|[^A-Za-z\s])")
ONE_ARGUMENT = {
    "frac", "dfrac", "tfrac", "sqrt", "overline", "underline", "bar",
    "hat", "widehat", "tilde", "widetilde", "vec", "mathbf", "mathrm",
    "mathbb", "mathcal", "mathfrak", "mathscr", "boldsymbol", "operatorname",
    "text", "textbf", "textit", "left", "right",
}
TWO_ARGUMENT = {"frac", "dfrac", "tfrac", "binom", "overset", "underset"}


class SyntaxErrorValue(ValueError):
    pass


@dataclass(frozen=True)
class Token:
    kind: str
    value: str


def tokenize(value: str) -> list[Token]:
    tokens: list[Token] = []
    index = 0
    while index < len(value):
        char = value[index]
        if char.isspace():
            index += 1
            continue
        if char == "\\":
            match = COMMAND_RE.match(value, index)
            if not match:
                raise SyntaxErrorValue(f"dangling backslash at offset {index}")
            text = match.group(0)
            tokens.append(Token("command", text[1:]))
            index = match.end()
            continue
        if char in "{}[]()^_&":
            tokens.append(Token(char, char))
        elif char.isdigit():
            tokens.append(Token("number", char))
        elif char.isalpha():
            tokens.append(Token("atom", char))
        else:
            tokens.append(Token("symbol", char))
        index += 1
    return tokens


class Parser:
    def __init__(self, tokens: list[Token]):
        self.tokens = tokens
        self.index = 0

    def peek(self) -> Token | None:
        return self.tokens[self.index] if self.index < len(self.tokens) else None

    def take(self) -> Token:
        token = self.peek()
        if token is None:
            raise SyntaxErrorValue("unexpected end of formula")
        self.index += 1
        return token

    def parse(self) -> Any:
        value = self.sequence(None)
        if self.peek() is not None:
            raise SyntaxErrorValue(f"unexpected token {self.peek().value!r}")
        return value

    def sequence(self, closing: str | None) -> Any:
        result: list[Any] = []
        while True:
            token = self.peek()
            if token is None:
                if closing is not None:
                    raise SyntaxErrorValue(f"missing closing {closing}")
                return ("seq", tuple(result))
            if closing is not None and token.kind == closing:
                self.take()
                return ("group", closing, tuple(result))
            if token.kind in "}] )":
                raise SyntaxErrorValue(f"unexpected closing {token.value!r}")
            result.append(self.atom())

    def atom(self) -> Any:
        token = self.take()
        if token.kind in "{[()":
            closing = {"{": "}", "[": "]", "(": ")"}[token.kind]
            return self.sequence(closing)
        if token.kind in "^_":
            return ("script", token.kind, self.argument())
        if token.kind == "command":
            if token.value == "end":
                raise SyntaxErrorValue("unexpected \\end")
            if token.value == "begin":
                env = self.environment_name()
                body = self.sequence_until_environment_end(env)
                return ("env", env, body)
            if token.value in TWO_ARGUMENT:
                return ("command", token.value, self.argument(), self.argument())
            if token.value in ONE_ARGUMENT:
                if token.value in {"left", "right"}:
                    return ("command", token.value, self.take().value)
                return ("command", token.value, self.argument())
            return ("command", token.value)
        if token.kind == "number":
            return ("number",)
        if token.kind == "atom":
            return ("atom",)
        return ("symbol", token.value)

    def argument(self) -> Any:
        token = self.peek()
        if token is None:
            raise SyntaxErrorValue("missing command argument")
        if token.kind in "{[()":
            return self.atom()
        if token.kind in "}]":
            raise SyntaxErrorValue("missing command argument")
        return self.atom()

    def environment_name(self) -> str:
        token = self.take()
        if token.kind != "{":
            raise SyntaxErrorValue("\\begin requires an environment name")
        names: list[str] = []
        while True:
            current = self.take()
            if current.kind == "}":
                break
            if current.kind not in {"atom", "number", "symbol"}:
                raise SyntaxErrorValue("environment name is not a simple group")
            names.append(current.value)
        if not names:
            raise SyntaxErrorValue("empty environment name")
        return "".join(names)

    def sequence_until_environment_end(self, environment: str) -> Any:
        result: list[Any] = []
        while True:
            token = self.peek()
            if token is None:
                raise SyntaxErrorValue(f"missing \\end{{{environment}}}")
            if token.kind == "command" and token.value == "end":
                self.take()
                if self.environment_name() != environment:
                    raise SyntaxErrorValue(f"mismatched environment end; expected {environment}")
                return ("envbody", tuple(result))
            result.append(self.atom())


def fingerprint(value: str) -> tuple[bool, str | None, str | None]:
    try:
        tree = Parser(tokenize(value)).parse()
    except SyntaxErrorValue as error:
        return False, None, str(error)
    encoded = json.dumps(tree, ensure_ascii=False, separators=(",", ":"))
    return True, hashlib.sha256(encoded.encode("utf-8")).hexdigest(), None


def load_manifest(path: Path) -> dict[str, dict[str, Any]]:
    rows: dict[str, dict[str, Any]] = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.strip():
            row = json.loads(line)
            rows[row["Image"]] = row
    return rows


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset-root", type=Path, default=Path(r"F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public"))
    parser.add_argument("--verification-root", type=Path, default=Path("eng/models/paddle-document/verification"))
    parser.add_argument("--output-json", type=Path, default=Path("eng/models/paddle-document/verification/formula-realformula-syntax-quality-20261012.json"))
    parser.add_argument("--output-markdown", type=Path, default=Path("eng/models/paddle-document/verification/formula-realformula-syntax-quality-20261012.md"))
    args = parser.parse_args()

    manifest = load_manifest(args.dataset_root / "realFormula-manifest.jsonl")
    if len(manifest) != 121:
        raise SystemExit(f"Expected 121 manifest rows, found {len(manifest)}")
    model_files = (
        "pp-formulanet-plus-s", "pp-formulanet-plus-m", "pp-formulanet-plus-l",
        "pp-formulanet-s", "pp-formulanet-l", "unimernet",
    )
    rows: list[dict[str, Any]] = []
    for model in model_files:
        source = args.verification_root / f"formula-realformula-{model}-ort-20261007.json"
        document = json.loads(source.read_text(encoding="utf-8-sig"))
        samples = document["modelResults"][0]["results"]
        if len(samples) != 121:
            raise SystemExit(f"Expected 121 results in {source}")
        expected_valid = prediction_valid = paired = 0
        invalid_examples: list[dict[str, str]] = []
        for sample in samples:
            reference = manifest[sample["image"]]["ReferenceLatex"]
            expected_ok, expected_fp, expected_error = fingerprint(reference)
            actual_ok, actual_fp, actual_error = fingerprint(sample.get("prediction", ""))
            expected_valid += int(expected_ok)
            prediction_valid += int(actual_ok)
            matched = expected_ok and actual_ok and expected_fp == actual_fp
            paired += int(matched)
            if (not actual_ok or (expected_ok and not matched)) and len(invalid_examples) < 8:
                invalid_examples.append({
                    "image": sample["image"],
                    "predictionError": actual_error or "structure-mismatch",
                    "referenceError": expected_error or "",
                })
        rows.append({
            "model": f"paddle-formula/{model}",
            "backend": "onnxruntime-cpu",
            "sampleCount": len(samples),
            "referenceSyntaxValid": expected_valid,
            "predictionSyntaxValid": prediction_valid,
            "predictionSyntaxValidRate": prediction_valid / len(samples),
            "pairedStructureFingerprintMatches": paired,
            "pairedStructureFingerprintMatchRate": paired / len(samples),
            "invalidOrMismatchedExamples": invalid_examples,
            "sourceReport": source.name,
        })

    output = {
        "schemaVersion": "deploysharp-realformula-syntax-quality-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "dataset": {"name": "MathNet realFormula", "version": "Zenodo 11296815 v1", "manifestSha256": hashlib.sha256((args.dataset_root / "realFormula-manifest.jsonl").read_bytes()).hexdigest(), "sampleCount": 121},
        "models": rows,
        "boundary": "Conservative parser diagnostics only. Syntax validity checks common command arguments, groups and begin/end environments; structure fingerprints abstract letters and digits. A match is not TeX rendering, semantic equivalence, mathematical correctness, or a release-quality score. Source predictions are ORT CPU only; no other backend is implied.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# Formula realFormula conservative syntax/structure diagnostics (2026-10-12)", "",
        "This report parses the 121 MathNet references and six ORT CPU prediction files with a deliberately conservative subset parser. It detects unclosed groups/environments and missing arguments, then compares atom-abstracted structure fingerprints. It is not a TeX renderer or a mathematical-semantic evaluator.", "",
        "| Model | Samples | Prediction syntax valid | Structure fingerprint match |", "|---|---:|---:|---:|",
    ]
    for row in rows:
        lines.append(f"| `{row['model']}` | {row['sampleCount']} | {row['predictionSyntaxValid']}/{row['sampleCount']} ({row['predictionSyntaxValidRate']:.2%}) | {row['pairedStructureFingerprintMatches']}/{row['sampleCount']} ({row['pairedStructureFingerprintMatchRate']:.2%}) |")
    lines += ["", "## Boundary", "", "- The parser is intentionally conservative and covers only common commands; a valid result does not prove that a full TeX engine can render it.", "- Fingerprints abstract letters and digits but preserve command names, grouping, scripts and environment shape. Matching is a structure diagnostic, not mathematical equivalence.", "- Predictions are the checked-in ONNX Runtime CPU reports. OpenVINO, OpenCV DNN and TensorRT quality are not represented."]
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {args.output_json} and {args.output_markdown}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
