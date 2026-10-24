"""Export Spotting vectors using SHA-checked, unmodified official PaddleX code.

Only the four regex assignments and post_process_for_spotting are AST-selected.
The source is never patched, and no model or third-party pipeline is imported.
"""

from __future__ import annotations

import argparse
import ast
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
import re


REVISION = "4c968d30b9d0d4a693d9eaaef0881c20f8d7d0e0"
SOURCE_SHA256 = "63bf7d0ccf03225dd6be582e2b25628283e2eed0ad0b1c7864e086e3b95f923a"
SOURCE_BLOB = "601dfbfae08c97b0b4cf227ea8a9d8360627cd12"
CHECKPOINT = "c5630abae1d940eafe0697512a0325494b02ab42"
CONSTANTS = {"ANNOT_TEXT_RE", "LOC_BLOCK_RE", "LOC_ITEM_RE", "LOC_TOKEN_RE"}
REPORT = "eng/models/paddle-ocr/verification/paddleocr-vl-six-task-reference-linux-20261023.json"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def locations(values: list[int]) -> str:
    return "".join(f"<|LOC_{value}|>" for value in values)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = args.source.read_bytes()
    if digest(args.source) != SOURCE_SHA256:
        raise ValueError("Pinned official source SHA mismatch")
    if hashlib.sha1(b"blob " + str(len(source)).encode() + b"\0" + source).hexdigest() != SOURCE_BLOB:
        raise ValueError("Pinned official Git blob mismatch")
    selected, seen = [], set()
    for node in ast.parse(source).body:
        if isinstance(node, ast.FunctionDef) and node.name == "post_process_for_spotting":
            selected.append(node)
            seen.add(node.name)
        elif isinstance(node, ast.Assign):
            names = {target.id for target in node.targets if isinstance(target, ast.Name)}
            if names & CONSTANTS:
                if len(names) != 1 or not names <= CONSTANTS:
                    raise ValueError("Unexpected regex assignment")
                selected.append(node)
                seen.update(names)
    if seen != CONSTANTS | {"post_process_for_spotting"}:
        raise ValueError("Missing official definitions")
    namespace = {"re": re}
    exec(compile(ast.Module(body=selected, type_ignores=[]), str(args.source), "exec"), namespace)
    function = namespace["post_process_for_spotting"]
    box = locations([0, 0, 1000, 0, 1000, 1000, 0, 1000])
    skew = locations([33, 74, 908, 100, 882, 741, 27, 700])
    block = lambda text, coords: f"<|TEXT_START|>{text}<|TEXT_END|><|LOC_BEGIN|>{coords}<|LOC_END|>"
    cases = []
    for identity, raw in (
        ("block-paired", block("  中文 😀  ", box) + "\n" + block("second\nline", skew)),
        ("block-grouped", "<|TEXT_START|>first<|TEXT_END|><|TEXT_START|>second<|TEXT_END|>"
         + f"<|LOC_BEGIN|>{box}<|LOC_END|><|LOC_BEGIN|>{skew}<|LOC_END|>"),
        ("flat-unicode", "  中文 😀 \\(\\beta\\)  " + skew + " \n"),
        ("flat-two", "first" + box + "\nsecond" + skew),
        ("empty-text", box),
        ("reverse-winding", "reverse" + locations([0, 1000, 1000, 1000, 1000, 0, 0, 0])),
        ("concave", "concave" + locations([0, 0, 1000, 0, 250, 250, 0, 1000])),
        ("coordinate-whitespace", "white" + " \t\n".join(re.findall(r"<\|LOC_\d+\|>", skew))),
    ):
        cases.append({"id": identity, "rawText": raw, "sourceSize": [511, 333], "origin": "synthetic-parser-case"})
    path = args.repo_root / REPORT
    report = json.loads(path.read_text(encoding="utf-8"))
    if report["checkpoint"]["revision"] != CHECKPOINT:
        raise ValueError("Unexpected checkpoint")
    for case in report["cases"]:
        if case["task"] != "spotting":
            continue
        if case["stopReason"] != "eos":
            raise ValueError("Real Spotting must end with EOS")
        cases.append({"id": case["id"], "rawText": case["decoded"], "sourceSize": case["sourceSize"],
                      "inputSha256": case["inputSha256"], "generatedTokenIds": case["generatedTokenIds"],
                      "origin": REPORT, "generationStopReason": case["stopReason"]})
    if len(cases) != 9 or len({case["id"] for case in cases}) != 9:
        raise ValueError("Unexpected reference corpus")
    for case in cases:
        text, parsed = function(case["rawText"], *case["sourceSize"])
        case.update({"text": text, "texts": parsed["rec_texts"], "sourcePolygons": parsed["rec_polys"],
                     "rawTextSha256": hashlib.sha256(case["rawText"].encode("utf-8")).hexdigest()})
        print(f"EXPORTED {case['id']} regions={len(parsed['rec_polys'])}", flush=True)
    output = {
        "schemaVersion": "deploysharp-paddleocr-vl-official-spotting-reference-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "checkpointRevision": CHECKPOINT,
        "officialSource": {"repository": "PaddlePaddle/PaddleX", "revision": REVISION,
                           "path": "paddlex/inference/pipelines/paddleocr_vl/uilts.py",
                           "sha256": SOURCE_SHA256, "gitBlob": SOURCE_BLOB,
                           "method": "unmodified AST-selected post_process_for_spotting and four regex assignments"},
        "generationReference": {"path": REPORT, "sha256": digest(path)},
        "generatorSha256": digest(Path(__file__)), "python": platform.python_version(), "cases": cases,
        "boundary": "Parser correctness on eight synthetic vectors and one real EOS output; not labelled text/localization quality, another weight version, backend coverage or formal performance.",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
