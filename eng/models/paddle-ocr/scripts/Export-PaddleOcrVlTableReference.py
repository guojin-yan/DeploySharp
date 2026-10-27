"""Export OTSL cells/HTML using unmodified, pinned official PaddleX functions.

Only the table classes, constants and functions are AST-selected; unrelated
pipeline imports/model code are not executed. The original file is SHA-checked.
Run in an isolated environment with pydantic 2.12.3. No model is executed here.
"""

from __future__ import annotations

import argparse
import ast
from datetime import datetime, timezone
import hashlib
import html
import itertools
import json
from pathlib import Path
import platform
import re
import sys
import types
from typing import Any, Dict, List, Tuple, Union

import pydantic
from pydantic import BaseModel, computed_field, model_validator


REVISION = "4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0"
SOURCE_PATH = "paddlex/inference/pipelines/paddleocr_vl/uilts.py"
SOURCE_SHA256 = "63bf7d0ccf03225dd6be582e2b25628283e2eed0ad0b1c7864e086e3b95f923a"
SOURCE_BLOB = "601dfbfae08c97b0b4cf227ea8a9d8360627cd12"
CHECKPOINT = "c5630abae1d940eafe0697512a0325494b02ab42"
DEFINITIONS = {
    "TableCell", "TableData", "otsl_extract_tokens_and_text", "otsl_parse_texts",
    "export_to_html", "otsl_pad_to_sqr_v2", "convert_otsl_to_html",
}
CONSTANTS = {
    "OTSL_NL", "OTSL_FCEL", "OTSL_ECEL", "OTSL_LCEL", "OTSL_UCEL", "OTSL_XCEL",
    "NON_CAPTURING_TAG_GROUP", "OTSL_FIND_PATTERN",
}
REPORTS = (
    "tests/assets/paddleocr-vl/six-task-reference.json",
    "tests/assets/paddleocr-vl/detected-crop-reference.json",
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--corpus", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if digest(args.source) != SOURCE_SHA256:
        raise ValueError("Pinned official PaddleX source SHA mismatch")
    source = args.source.read_bytes()
    blob = hashlib.sha1(b"blob " + str(len(source)).encode() + b"\0" + source).hexdigest()
    if blob != SOURCE_BLOB:
        raise ValueError("Pinned official PaddleX Git blob mismatch")
    tree = ast.parse(source, filename=str(args.source))
    selected = []
    seen = set()
    for node in tree.body:
        if isinstance(node, (ast.FunctionDef, ast.ClassDef)) and node.name in DEFINITIONS:
            selected.append(node)
            seen.add(node.name)
        elif isinstance(node, ast.Assign):
            names = {target.id for target in node.targets if isinstance(target, ast.Name)}
            if names & CONSTANTS:
                if len(names) != 1 or not names <= CONSTANTS:
                    raise ValueError("Unexpected official constant declaration")
                selected.append(node)
                seen.update(names)
    if seen != DEFINITIONS | CONSTANTS:
        raise ValueError("Pinned official table definitions are incomplete")
    module = types.ModuleType("deploysharp_official_otsl_reference")
    module.__dict__.update({name: globals()[name] for name in (
        "html", "itertools", "re", "Any", "Dict", "List", "Tuple", "Union",
        "BaseModel", "computed_field", "model_validator")})
    sys.modules[module.__name__] = module
    exec(compile(ast.Module(body=selected, type_ignores=[]), str(args.source), "exec"), module.__dict__)

    corpus = json.loads(args.corpus.read_text(encoding="utf-8"))
    if corpus["schemaVersion"] != "deploysharp-paddleocr-vl-otsl-inputs-v1":
        raise ValueError("Unexpected input corpus schema")
    cases = [{**case, "origin": "synthetic-parser-case"} for case in corpus["cases"]]
    reports = []
    for relative in REPORTS:
        path = args.repo_root / relative
        report = json.loads(path.read_text(encoding="utf-8"))
        if report["checkpoint"]["revision"] != CHECKPOINT:
            raise ValueError("Unexpected generation checkpoint revision")
        reports.append({"path": relative, "sha256": digest(path)})
        for case in report["cases"]:
            if case["task"] != "table":
                continue
            if case["stopReason"] != "eos":
                raise ValueError("Real table reference must have ended with EOS")
            cases.append({"id": case["id"], "rawText": case["decoded"], "requiresRepair": False,
                          "origin": relative, "inputSha256": case["inputSha256"],
                          "generationStopReason": case["stopReason"]})
    if len({case["id"] for case in cases}) != len(cases):
        raise ValueError("Duplicate reference IDs")
    for case in cases:
        raw = case["rawText"]
        normalized = module.otsl_pad_to_sqr_v2(raw)
        tokens, texts = module.otsl_extract_tokens_and_text(normalized)
        cells, rows = module.otsl_parse_texts(texts, tokens)
        case.update({
            "rawTextSha256": hashlib.sha256(raw.encode("utf-8")).hexdigest(),
            "normalizedOtsl": normalized, "rowCount": len(rows), "columnCount": max(map(len, rows)),
            "html": module.convert_otsl_to_html(raw),
            "cells": [{"row": cell.start_row_offset_idx, "column": cell.start_col_offset_idx,
                       "rowSpan": cell.row_span, "columnSpan": cell.col_span, "text": cell.text,
                       "isEmpty": rows[cell.start_row_offset_idx][cell.start_col_offset_idx] == "<ecel>"}
                      for cell in cells],
        })
        print(f"EXPORTED {case['id']} rows={case['rowCount']} columns={case['columnCount']} cells={len(cells)}", flush=True)
    report = {
        "schemaVersion": "deploysharp-paddleocr-vl-official-otsl-reference-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "upstream": {"repository": "PaddlePaddle/PaddleX", "revision": REVISION, "path": SOURCE_PATH,
                     "sourceSha256": SOURCE_SHA256, "gitBlob": SOURCE_BLOB,
                     "selectedDefinitions": sorted(seen)},
        "scriptSha256": digest(Path(__file__)), "inputCorpusSha256": digest(args.corpus),
        "generationReports": reports, "environment": {"python": platform.python_version(), "pydantic": pydantic.__version__},
        "boundary": "Official parser comparison on 12 synthetic and two existing real generated tables; no new inference, labeled table quality or backend claim.",
        "cases": cases,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
