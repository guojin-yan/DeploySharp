"""Validate completed six-task VL reference, pixel parity and cache evidence."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path


REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
WEIGHT_SHA256 = "85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--generation", type=Path, required=True)
    parser.add_argument("--uncached-formula", type=Path, required=True)
    parser.add_argument("--processor-parity", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--device", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
    report, uncached, pixels, manifest, device = map(read, (args.generation, args.uncached_formula, args.processor_parity, args.manifest, args.device))
    for item in (report, uncached):
        require(item["status"] == "reference-generation-complete", "Generation reference did not complete")
        require(item["checkpoint"]["revision"] == REVISION and item["checkpoint"]["weightVerified"]
                and item["checkpoint"]["weightSha256"] == WEIGHT_SHA256, "Wrong/unverified weight identity")
        require(item["manifestSha256"] == digest(args.manifest), "Input manifest hash mismatch")
        require(item["environment"]["device"] == "cpu" and item["environment"]["dtype"] == "float32", "Unexpected reference device/precision")
    require(report["protocol"]["requestedUseCache"] and not uncached["protocol"]["requestedUseCache"], "Expected cached and uncached protocols")
    require(report["environment"] == uncached["environment"] and report["implementation"] == uncached["implementation"], "Reference implementations/environments differ")
    require(report["scriptSha256"] == uncached["scriptSha256"], "Generation script identity differs")
    by_id = {case["id"]: case for case in manifest["cases"]}
    require(len(by_id) == 6 and len(report["cases"]) == 6, "Expected exactly six input tasks")
    rows = []
    for case in report["cases"]:
        source = by_id[case["id"]]
        require(case["inputSha256"] == source["sha256"] and case["task"] == source["task"], "Image/task mismatch")
        require(case["status"] == "generated" and case["stopReason"] == "eos" and case["generatedTokenIds"][-1] == 2, "Missing generated EOS")
        comparison = case["uncachedComparison"]
        require(comparison["maximumNewTokens"] == 8 and comparison["equal"] and comparison["tokenIds"] == case["generatedTokenIds"][:8], "Cached/uncached prefix mismatch")
        calls = case["forwardCalls"]
        require(len(calls) == len(case["generatedTokenIds"]), "Forward-call/token counts differ")
        prefix = len(case["inputTokenIds"])
        require(calls[0]["tokens"] == prefix and calls[0]["pixelValuesPresent"] and calls[0]["pastLength"] == 0, "Missing image prefill")
        for index, step in enumerate(calls[1:], 1):
            require(step["tokens"] == 1 and not step["pixelValuesPresent"] and step["pastLength"] == prefix + index - 1,
                    "Decode did not reuse exact KV length without image input")
        rows.append({"id": case["id"], "task": case["task"], "inputSha256": case["inputSha256"],
                     "sourceSize": case["sourceSize"], "preparedSize": case["preparedSize"],
                     "imageGridThw": case["imageGridThw"], "imageTokenCount": case["imageTokenCount"],
                     "promptTokenCount": prefix, "generatedTokenCount": len(case["generatedTokenIds"]),
                     "generatedTokenCsvSha256": hashlib.sha256(",".join(map(str, case["generatedTokenIds"])).encode("ascii")).hexdigest(),
                     "stopReason": case["stopReason"], "cachedUncachedFirst8Equal": True,
                     "singleImagePrefillThenOneTokenKvDecode": True,
                     "prepareMilliseconds": case["prepareMilliseconds"], "generationMilliseconds": case["generationMilliseconds"],
                     "decoded": case["decoded"]})
    require(len({case["id"] for case in rows}) == 6 and {case["task"] for case in rows} == {"ocr", "table", "formula", "chart", "spotting", "seal"}, "Missing or repeated task")
    require(pixels["status"] == "all-exact" and pixels["manifestSha256"] == digest(args.manifest), "Processor comparison did not pass")
    require(len(pixels["cases"]) == 6 and {case["id"] for case in pixels["cases"]} == set(by_id), "Processor input set differs")
    for case in pixels["cases"]:
        require(case["inputSha256"] == by_id[case["id"]]["sha256"] and case["pixelExact"] and case["gridEqual"]
                and case["maximumAbsoluteDifference"] == 0 and case["pinnedPixelSha256"] == case["currentPixelSha256"], "Processor parity mismatch")
        generation = next(row for row in report["cases"] if row["id"] == case["id"])
        require(generation["tensors"]["pixel_values"]["sha256"] == case["currentPixelSha256"], "Generation pixel tensor is not the compared pixel tensor")
    require(len(uncached["cases"]) == 1, "Expected one uncached formula reference")
    formula = next(case for case in report["cases"] if case["task"] == "formula")
    other = uncached["cases"][0]
    require(other["id"] == formula["id"] and other["status"] == "generated" and other["stopReason"] == "eos", "Uncached formula did not finish")
    require(other["generatedTokenIds"] == formula["generatedTokenIds"] and other["decodedWithSpecialTokens"] == formula["decodedWithSpecialTokens"], "Full uncached formula token/text mismatch")
    require(other["inputTokenIds"] == formula["inputTokenIds"] and other["tensors"] == formula["tensors"], "Full formula input differs")
    for index, step in enumerate(other["forwardCalls"]):
        require(step["pixelValuesPresent"] and step["tokens"] == len(other["inputTokenIds"]) + index and step["pastLength"] is None,
                "Uncached reference unexpectedly used image/KV caching")
    output = {"schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(), "status": "validated",
              "checkpoint": report["checkpoint"], "environment": report["environment"], "device": device,
              "protocol": report["protocol"], "implementation": report["implementation"], "cases": rows,
              "pixelParity": {"cases": 6, "exact": 6, "maximumAbsoluteDifference": 0},
              "uncachedFormula": {"tokenCount": len(other["generatedTokenIds"]), "fullTokenAndTextEqual": True,
                                  "stopReason": other["stopReason"], "generationMilliseconds": other["generationMilliseconds"]},
              "scriptSha256": digest(Path(__file__)),
              "evidence": [{"file": path.name, "sha256": digest(path)} for path in (args.generation, args.uncached_formula, args.processor_parity, args.manifest, args.device)],
              "openItems": ["Pinned original-model versus integrated-model generation parity", "Multimodal position reference", "ONNX dynamic KV graphs",
                            "C# pixels/patches and complete inference", "Backend validation", "Labeled quality and formal performance", "ROI/layout/multi-page pipeline", "Runtime catalog/release assets"],
              "boundary": "Six real single-image tasks on the recorded Python implementation and CPU FP32 only. Pixel parity compares unmodified pinned upstream image source. No labeled accuracy, formal timing, GPU, C# or ONNX backend support claim."}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Validated 6 EOS tasks, 6 exact image processors, 6 first-8 cache decisions and full 80-token formula cache parity.")


if __name__ == "__main__":
    main()
