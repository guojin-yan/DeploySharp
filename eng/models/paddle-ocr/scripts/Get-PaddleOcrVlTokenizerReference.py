"""Record actual pinned PaddleOCR-VL tokenizer output; no model weights are run."""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import struct
from datetime import datetime, timezone
from pathlib import Path

import tokenizers


REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
HASHES = {
    "tokenizer.model": "34ef7db83df785924fb83d7b887b6e822a031c56e15cff40aaf9b982988180df",
    "tokenizer.json": "c8a215a59183d0d0781adc33bacd3ce6162716f7fd568fb30234a74d69803a7d",
    "tokenizer_config.json": "1f979337347cc0cb72a6282d8a23ed183539aa81a87a906f022aee2bab83c7c5",
    "chat_template.jinja": "2f27812dab7f333e471884e0c803d807f11953d5453140dfb1aaba234f872bc8",
}
PROMPTS = {
    "TextRecognition": "OCR:", "TableRecognition": "Table Recognition:",
    "FormulaRecognition": "Formula Recognition:", "ChartRecognition": "Chart Recognition:",
    "TextSpotting": "Spotting:", "SealRecognition": "Seal Recognition:",
}


def csv_hash(ids: list[int]) -> str:
    return hashlib.sha256(",".join(map(str, ids)).encode("ascii")).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    for name, expected in HASHES.items():
        if hashlib.sha256((args.model_root / name).read_bytes()).hexdigest() != expected:
            raise ValueError(f"Pinned official tokenizer asset mismatch: {name}")
    tokenizer = tokenizers.Tokenizer.from_file(str(args.model_root / "tokenizer.json"))
    texts = [
        "OCR:", "Table Recognition:", "hello world", "hello  world", " leading\ntext",
        "你好，世界！", "公式 x^2 + y^2 = z^2", "A\tB\nC", "🧪", "", " ", "   ",
        "\r\n\t ", "e\u0301é\u200b", "<ecel> A<|TEXT_START|>B</s>",
        "<s>1 2 3</s><|begin_of_sentence|><|IMAGE_PLACEHOLDER|>",
        "<|TEXT_START|>你好<|TEXT_END|><x_17><y_29>", "0 1 2 3 4 5 6 7 8 9",
    ]
    vectors = []
    for text in texts:
        ids = tokenizer.encode(text, add_special_tokens=False).ids
        vectors.append({"text": text, "tokenIds": ids,
                        "decoded": tokenizer.decode(ids, skip_special_tokens=True),
                        "decodedWithSpecialTokens": tokenizer.decode(ids, skip_special_tokens=False)})
    tasks = []
    for task, prompt in PROMPTS.items():
        template = f"<|begin_of_sentence|>User: <|IMAGE_START|><|IMAGE_PLACEHOLDER|><|IMAGE_END|>{prompt}\nAssistant:\n"
        expanded = template.replace("<|IMAGE_PLACEHOLDER|>", "<|IMAGE_PLACEHOLDER|>" * 144)
        ids = tokenizer.encode(expanded, add_special_tokens=False).ids
        tasks.append({"task": task, "promptTemplate": template, "imageTokens": 144,
                      "expandedTokenCount": len(ids), "imageOffset": ids.index(100295),
                      "tokenCsvSha256": csv_hash(ids)})
    config = json.loads((args.model_root / "tokenizer_config.json").read_text(encoding="utf-8"))
    added = config["added_tokens_decoder"]
    for raw_id, value in added.items():
        ids = tokenizer.encode(value["content"], add_special_tokens=False).ids
        if ids != [int(raw_id)]:
            raise ValueError(f"Added-token encoding mismatch: {raw_id}")
        if tokenizer.decode(ids, skip_special_tokens=False) != value["content"]:
            raise ValueError(f"Added-token raw decoding mismatch: {raw_id}")
        expected = "" if value["special"] else value["content"]
        if tokenizer.decode(ids, skip_special_tokens=True) != expected:
            raise ValueError(f"Added-token special flag mismatch: {raw_id}")
    ids_in_order = sorted(map(int, added))
    decoded_vectors = []
    rng = random.Random(9016)
    byte_cases = [[rng.randrange(13, 269) for _ in range(rng.randrange(1, 18))] for _ in range(64)]
    for ids in ([253], [253, 172], [253, 22, 172], [253, 172, 180, 183], [2], [100273, 3, 93919, 5, 101308, 2],
                [253, 100273, 172, 103423, 180, 183], *byte_cases):
        decoded_vectors.append({"tokenIds": ids, "decoded": tokenizer.decode(ids, skip_special_tokens=True),
                                "decodedWithSpecialTokens": tokenizer.decode(ids, skip_special_tokens=False)})
    vocab_hashes = {}
    for skip_special in (True, False):
        digest = hashlib.sha256()
        for token_id in range(103424):
            data = tokenizer.decode([token_id], skip_special_tokens=skip_special).encode("utf-8")
            digest.update(struct.pack("<I", len(data)))
            digest.update(data)
        vocab_hashes["skipSpecialTokens" if skip_special else "includeSpecialTokens"] = digest.hexdigest()
    report = {
        "schemaVersion": 1, "generatedUtc": datetime.now(timezone.utc).isoformat(),
        "checkpointRevision": REVISION, "source": "PaddlePaddle/PaddleOCR-VL-1.6",
        "tokenizersVersion": tokenizers.__version__, "assetSha256": HASHES,
        "scriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "textVectors": vectors, "taskVectors": tasks, "decodeVectors": decoded_vectors,
        "allVocabularyDecode": {"count": 103424, "framing": "per ID: uint32 little-endian UTF-8 byte length, then UTF-8 bytes", "sha256": vocab_hashes},
        "addedTokenVerification": {"count": len(added), "special": sum(v["special"] for v in added.values()),
                                   "ordinary": sum(not v["special"] for v in added.values()),
                                   "tokenIdCsvSha256": csv_hash(ids_in_order), "status": "all-passed"},
        "boundary": "Actual tokenizer.json output only. Not full AutoProcessor, image pixels, positions or model generation parity.",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
    print(f"Recorded {len(vectors)} text, {len(tasks)} task and {len(decoded_vectors)} decode vectors; {len(added)} added tokens passed.")


if __name__ == "__main__":
    main()
