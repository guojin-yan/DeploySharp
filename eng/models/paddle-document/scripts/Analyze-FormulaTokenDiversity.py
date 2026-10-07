#!/usr/bin/env python3
"""Summarize bounded raw-token diversity from existing DeploySharp test reports.

The output includes per-window counts and hashes, not full token sequences.
It is descriptive only; its fixed thresholds are not a decoder or accuracy metric.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def exact_suffix_period(tokens: list[int], maximum_period: int = 64) -> dict[str, Any] | None:
    best_cycles = 1
    best_period = 0
    for period in range(1, min(maximum_period, len(tokens) // 3) + 1):
        suffix = tokens[-period:]
        cycles = 1
        while (cycles + 1) * period <= len(tokens) and tokens[-(cycles + 1) * period : -cycles * period] == suffix:
            cycles += 1
        if cycles > best_cycles or (cycles == best_cycles and cycles >= 3 and period < best_period):
            best_cycles = cycles
            best_period = period
    if best_cycles < 3:
        return None
    return {
        "periodTokenCount": best_period,
        "repeatedCycles": best_cycles,
        "coveredSuffixTokenCount": best_period * best_cycles,
        "periodTokenIds": tokens[-best_period:],
    }


def window_metrics(tokens: list[int], width: int, unique_token_max: int, unique_fourgram_ratio_max: float) -> dict[str, Any]:
    fourgrams = [tuple(tokens[index : index + 4]) for index in range(max(0, len(tokens) - 3))]
    unique_fourgrams = len(set(fourgrams))
    repeated_positions = len(fourgrams) - unique_fourgrams
    unique_tokens = len(set(tokens))
    unique_fourgram_ratio = unique_fourgrams / max(1, len(fourgrams))
    return {
        "startGeneratedTokenIndex": None,
        "tokenCount": len(tokens),
        "uniqueTokenCount": unique_tokens,
        "fourGramPositions": len(fourgrams),
        "uniqueFourGramCount": unique_fourgrams,
        "repeatedFourGramPositions": repeated_positions,
        "uniqueFourGramRatio": unique_fourgram_ratio,
        "lowDiversityFlag": unique_tokens <= unique_token_max and unique_fourgram_ratio <= unique_fourgram_ratio_max,
    }


def load_rows(paths: list[Path], model_id: str) -> tuple[str | None, int, int, dict[str, dict[str, Any]]]:
    model_sha: str | None = None
    start_token_id: int | None = None
    end_token_id: int | None = None
    samples: dict[str, dict[str, Any]] = {}
    for path in paths:
        document = json.loads(path.read_text(encoding="utf-8-sig"))
        model_results = document.get("modelResults")
        if not isinstance(model_results, list):
            raise ValueError(f"Report has no modelResults array: {path}")
        matching = [item for item in model_results if item.get("model") == model_id]
        if len(matching) != 1:
            raise ValueError(f"Expected exactly one {model_id} result in {path}; found {len(matching)}.")
        model = matching[0]
        current_sha = model.get("modelSha256")
        current_start_token_id = model.get("startTokenId")
        current_end_token_id = model.get("endTokenId")
        if not isinstance(current_start_token_id, int) or not isinstance(current_end_token_id, int):
            raise ValueError(f"Model startTokenId/endTokenId are missing or invalid in {path}.")
        if model_sha is None:
            model_sha = current_sha
            start_token_id = current_start_token_id
            end_token_id = current_end_token_id
        elif current_sha != model_sha:
            raise ValueError("Input reports contain different model SHA-256 values.")
        elif current_start_token_id != start_token_id or current_end_token_id != end_token_id:
            raise ValueError("Input reports contain different BOS/EOS token IDs.")
        for row in model.get("results", []):
            image = row.get("image")
            ids = row.get("rawOutputTokenIds")
            if not image or not isinstance(ids, list) or not ids:
                raise ValueError(f"Raw output token trace is missing in {path}: {image!r}")
            if image in samples:
                raise ValueError(f"Duplicate sample across input reports: {image}")
            samples[image] = row
    if not samples:
        raise ValueError("No raw token traces were found.")
    assert start_token_id is not None and end_token_id is not None
    return model_sha, start_token_id, end_token_id, samples


def summarize_sample(
    image: str,
    row: dict[str, Any],
    start_token_id: int,
    end_token_id: int,
    width: int,
    unique_token_max: int,
    unique_fourgram_ratio_max: float,
    exact_period_max: int,
) -> dict[str, Any]:
    raw = [int(value) for value in row["rawOutputTokenIds"]]
    eos_index = row.get("rawEosIndex")
    has_bos = raw[0] == start_token_id
    generated = raw[1:] if has_bos else raw[:]
    if eos_index is not None and 0 <= int(eos_index) < len(raw):
        eos_count = raw.count(end_token_id)
        generated = raw[1 : int(eos_index)] if has_bos else raw[: int(eos_index)]
    else:
        eos_count = raw.count(end_token_id)
        if eos_count:
            raise ValueError(f"EOS is present but rawEosIndex is missing or invalid for {image}.")
    windows: list[dict[str, Any]] = []
    for start in range(0, len(generated), width):
        values = generated[start : start + width]
        if len(values) != width:
            continue
        metrics = window_metrics(values, width, unique_token_max, unique_fourgram_ratio_max)
        metrics["startGeneratedTokenIndex"] = start
        windows.append(metrics)
    flagged = next((item for item in windows if item["lowDiversityFlag"]), None)
    digest = hashlib.sha256(b"".join(int(value).to_bytes(8, "little", signed=True) for value in raw)).hexdigest()
    return {
        "image": image,
        "imageSha256": row.get("imageSha256"),
        "inputTensorSha256": row.get("inputTensorSha256"),
        "rawOutputShape": row.get("rawOutputShape"),
        "rawTokenCount": len(raw),
        "rawTokenIdsSha256": digest,
        "bosCount": raw.count(0),
        "eosCount": eos_count,
        "rawEosIndex": eos_index,
        "reachedEos": eos_count > 0,
        "generatedTokenCountExcludingBosAndEos": len(generated),
        "decodedPredictionCharacters": row.get("predictionLength"),
        "referenceCharacters": row.get("referenceLength"),
        "normalizedCharacterCer": row.get("normalizedCharErrorRate"),
        "fullWindowsAnalyzed": len(windows),
        "firstLowDiversityWindow": flagged,
        "exactRepeatedSuffixPeriod": exact_suffix_period(generated, exact_period_max),
        "windowSummaries": windows,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input-reports", nargs="+", required=True, type=Path, help="Existing JSON test outputs with rawOutputTokenIds")
    parser.add_argument("--output", required=True, type=Path, help="Explicit path for the machine-readable summary")
    parser.add_argument("--model", default="paddle-formula/pp-formulanet-l")
    parser.add_argument("--window", type=int, default=128)
    parser.add_argument("--max-unique-tokens", type=int, default=4)
    parser.add_argument("--max-unique-fourgram-ratio", type=float, default=0.10)
    parser.add_argument("--max-period", type=int, default=64)
    args = parser.parse_args()
    if args.window < 4 or args.max_unique_tokens < 1 or not (0 < args.max_unique_fourgram_ratio <= 1):
        raise ValueError("Invalid window/diversity limits.")
    for path in args.input_reports:
        if not path.is_file():
            raise FileNotFoundError(path)
    model_sha, start_token_id, end_token_id, rows = load_rows(args.input_reports, args.model)
    samples = [
        summarize_sample(
            image,
            row,
            start_token_id,
            end_token_id,
            args.window,
            args.max_unique_tokens,
            args.max_unique_fourgram_ratio,
            args.max_period,
        )
        for image, row in sorted(rows.items())
    ]
    report = {
        "schemaVersion": "deploysharp-formula-token-diversity-diagnostic-v1",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "model": args.model,
        "modelSha256": model_sha,
        "startTokenId": start_token_id,
        "endTokenId": end_token_id,
        "inputs": [{"fileName": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()} for path in args.input_reports],
        "method": {
            "windowTokenCount": args.window,
            "windowAlignment": "Non-overlapping windows over generated token IDs after BOS/EOS removal.",
            "lowDiversityFlag": {
                "uniqueTokenCountAtMost": args.max_unique_tokens,
                "uniqueFourGramRatioAtMost": args.max_unique_fourgram_ratio,
                "meaning": "Exploratory descriptive threshold for this selected probe only; not a general detector, confidence score, or accuracy metric.",
            },
            "exactSuffixPeriodMaximumPeriodTokens": args.max_period,
        },
        "sampleCount": len(samples),
        "samples": samples,
        "boundary": "Purposively selected traces from one model/export and ORT CPU. These descriptive statistics do not establish causation, population prevalence, EOS correctness, formula accuracy, or other-backend behavior.",
    }
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "sampleCount": len(samples), "modelSha256": model_sha}, ensure_ascii=False))


if __name__ == "__main__":
    main()
