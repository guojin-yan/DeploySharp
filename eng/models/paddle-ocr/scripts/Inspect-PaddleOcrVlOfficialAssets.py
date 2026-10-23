"""Inspect pinned official PaddleOCR-VL assets without loading model weights.

Caches small source/config/tokenizer files outside Git, verifies Hub object hashes,
and emits an acquisition/format report. This is not an ONNX export or inference
support claim. No repository Python modeling/processing module is imported.
"""

from __future__ import annotations

import argparse
import ast
import contextlib
import hashlib
import io
import json
import math
from datetime import datetime, timezone
from pathlib import Path
import time
import urllib.request


REPOSITORY = "PaddlePaddle/PaddleOCR-VL-1.6"
REVISION = "c5630abae1d940eafe0697512a0325494b02ab42"
OCR_REVISION = "dab3fe35379033fdcb2d0e9572fac0b36c9a9ebf"
FILES = (
    "config.json", "generation_config.json", "preprocessor_config.json",
    "processor_config.json", "tokenizer_config.json", "special_tokens_map.json",
    "added_tokens.json", "tokenizer.json", "tokenizer.model", "chat_template.jinja",
    "configuration_paddleocr_vl.py", "image_processing_paddleocr_vl.py",
    "processing_paddleocr_vl.py", "modeling_paddleocr_vl.py", "inference.yml", "LICENSE", "README.md",
)
PROMPTS = {
    "ocr": "OCR:", "table": "Table Recognition:", "formula": "Formula Recognition:",
    "chart": "Chart Recognition:", "spotting": "Spotting:", "seal": "Seal Recognition:",
}


def fetch(url: str) -> bytes:
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "DeploySharp-PaddleOCR-VL-inspection/1"})
            with urllib.request.urlopen(request, timeout=45) as response:
                return response.read()
        except OSError:
            if attempt == 2:
                raise
            time.sleep(attempt + 1)
    raise AssertionError("unreachable")


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def matches(data: bytes, item: dict) -> bool:
    if len(data) != item["size"]:
        return False
    if item.get("lfs"):
        return digest(data) == item["lfs"]["oid"]
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\0" + data).hexdigest() == item["oid"]


def inspect(root: Path, tokenizer_reference: bool) -> dict:
    tree = json.loads(fetch(f"https://huggingface.co/api/models/{REPOSITORY}/tree/{REVISION}?recursive=false&expand=false"))
    entries = {item["path"]: item for item in tree if item["type"] == "file"}
    root.mkdir(parents=True, exist_ok=True)
    records = []
    content: dict[str, bytes] = {}
    for name in FILES:
        item = entries[name]
        path = root / name
        data = path.read_bytes() if path.is_file() else b""
        cached = matches(data, item)
        if not cached:
            data = fetch(f"https://huggingface.co/{REPOSITORY}/resolve/{REVISION}/{name}")
            if not matches(data, item):
                raise ValueError(f"Official file failed size/object verification: {name}")
            path.write_bytes(data)
        content[name] = data
        records.append({"fileName": name, "bytes": len(data), "sha256": digest(data), "hubObjectId": item["oid"],
                        "verifiedAgainst": "lfs-sha256" if item.get("lfs") else "git-blob-sha1",
                        "sourceUrl": f"https://huggingface.co/{REPOSITORY}/resolve/{REVISION}/{name}"})
        print(f"VERIFIED {name} ({len(data)} bytes; {'cache' if cached else 'download'})", flush=True)

    config = json.loads(content["config.json"])
    preprocessor = json.loads(content["preprocessor_config.json"])
    tokenizer_config = json.loads(content["tokenizer_config.json"])
    tokenizer = json.loads(content["tokenizer.json"])
    added = json.loads(content["added_tokens.json"])
    generation = json.loads(content["generation_config.json"])
    if config["model_type"] != "paddleocr_vl" or config["architectures"] != ["PaddleOCRVLForConditionalGeneration"]:
        raise ValueError("Unexpected official model architecture")
    if added["<|IMAGE_PLACEHOLDER|>"] != config["image_token_id"] or added["<|IMAGE_START|>"] != config["vision_start_token_id"] or added["<|IMAGE_END|>"] != config["vision_end_token_id"]:
        raise ValueError("Image markers disagree between model and tokenizer")
    added_token_ids = {value["content"]: int(key) for key, value in tokenizer_config["added_tokens_decoder"].items()}
    if added_token_ids[tokenizer_config["eos_token"]] != generation["eos_token_id"]:
        raise ValueError("EOS disagrees between tokenizer and generation config")
    for prompt in PROMPTS.values():
        if json.dumps(prompt)[1:-1] not in content["README.md"].decode("utf-8"):
            raise ValueError(f"Prompt not present in official usage example: {prompt}")

    # Extract only the inspected, pinned scalar helper, not a remote model module.
    # No torch/transformers import or model initialization is needed for geometry.
    source = content["image_processing_paddleocr_vl.py"].decode("utf-8")
    function = next(node for node in ast.parse(source).body if isinstance(node, ast.FunctionDef) and node.name == "smart_resize")
    namespace = {"math": math}
    exec(compile(ast.Module(body=[function], type_ignores=[]), "pinned-official-smart-resize", "exec"), namespace)
    cases = []
    for height, width in ((1000, 700), (700, 1000), (48, 3200), (10, 200), (4032, 3024), (336, 336), (1, 201)):
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                resized_h, resized_w = namespace["smart_resize"](height, width,
                    factor=preprocessor["patch_size"] * preprocessor["merge_size"],
                    min_pixels=preprocessor["min_pixels"], max_pixels=preprocessor["max_pixels"])
            factor = preprocessor["patch_size"] * preprocessor["merge_size"]
            if resized_h <= 0 or resized_w <= 0 or resized_h % factor or resized_w % factor:
                raise AssertionError("Unexpected official resize geometry")
            grid_h, grid_w = resized_h // preprocessor["patch_size"], resized_w // preprocessor["patch_size"]
            cases.append({"sourceHeight": height, "sourceWidth": width, "status": "geometry-only",
                          "resizedHeight": resized_h, "resizedWidth": resized_w, "gridHeight": grid_h, "gridWidth": grid_w,
                          "imagePlaceholderTokens": grid_h * grid_w // preprocessor["merge_size"] ** 2})
        except ValueError as error:
            cases.append({"sourceHeight": height, "sourceWidth": width, "status": "official-helper-rejected", "reason": str(error)})

    token_reference = None
    if tokenizer_reference:
        import sentencepiece
        import tokenizers

        fast = tokenizers.Tokenizer.from_file(str(root / "tokenizer.json"))
        sentence_piece = sentencepiece.SentencePieceProcessor(model_file=str(root / "tokenizer.model"))
        plain = ("OCR:", "Table Recognition:", "hello world", "hello  world", " leading\ntext",
                 "\u4f60\u597d\uff0c\u4e16\u754c\uff01", "\u516c\u5f0f x^2 + y^2 = z^2", "A\tB\nC", "\U0001f9ea")
        vectors = []
        for text in plain:
            fast_ids = fast.encode(text, add_special_tokens=False).ids
            sp_ids = sentence_piece.encode(text)
            if fast_ids != sp_ids:
                raise ValueError("Fast/SentencePiece ordinary-text reference IDs disagree")
            vectors.append({"text": text, "tokenIds": fast_ids, "decoded": fast.decode(fast_ids, skip_special_tokens=False)})
        prompts = []
        for task, prompt in PROMPTS.items():
            rendered = tokenizer_config["cls_token"] + "User: <|IMAGE_START|><|IMAGE_PLACEHOLDER|><|IMAGE_END|>" + prompt + "\nAssistant:\n"
            ids = fast.encode(rendered, add_special_tokens=False).ids
            if ids.count(config["image_token_id"]) != 1:
                raise ValueError("Official single-image prompt must have one placeholder before image-token expansion")
            expanded = [value for item in ids for value in ([item] * 144 if item == config["image_token_id"] else [item])]
            prompts.append({"task": task, "prompt": rendered, "tokenIdsBeforeImageExpansion": ids,
                "imagePlaceholderCount": 144, "expandedTokenCount": len(expanded),
                "expandedTokenCsvSha256": digest(",".join(map(str, expanded)).encode("ascii"))})
        token_reference = {"tokenizersVersion": tokenizers.__version__, "sentencepieceVersion": sentencepiece.__version__,
            "ordinaryTextVectors": vectors, "taskPromptVectors": prompts,
            "boundary": "Runs local tokenizer assets, not an official full processor/model. Ordinary-text IDs agree between the two upstream assets; added/special markers use tokenizer.json. C# tokenizer, prompt template application, 3D positions, pixel packing and model generation parity remain untested."}

    weight = entries["model.safetensors"]
    weight_path = root / "model.safetensors"
    # Do not read/allocate a 1.9 GB weight array for a metadata inspection.
    downloaded = weight_path.is_file()
    return {
        "schemaVersion": "deploysharp-paddleocr-vl-official-assets-v1",
        "generatedUtc": datetime.now(timezone.utc).isoformat(), "status": "official-format-and-scalar-geometry-inspected",
        "repository": REPOSITORY, "revision": REVISION,
        "officialAlgorithmUrl": f"https://github.com/PaddlePaddle/PaddleOCR/blob/{OCR_REVISION}/docs/version3.x/algorithm/PaddleOCR-VL/PaddleOCR-VL-1.6.en.md",
        "scriptSha256": digest(Path(__file__).read_bytes()), "files": records,
        "weights": {"fileName": "model.safetensors", "bytes": weight["size"], "officialLfsSha256": weight["lfs"]["oid"],
                    "presentInInspectedDirectory": downloaded, "locallyHashVerified": False, "loadedForInference": False,
                    "sourceUrl": f"https://huggingface.co/{REPOSITORY}/resolve/{REVISION}/model.safetensors"},
        "architecture": {key: config[key] for key in ("architectures", "model_type", "hidden_size", "head_dim", "num_hidden_layers", "num_attention_heads",
            "num_key_value_heads", "vocab_size", "image_token_id", "vision_start_token_id", "vision_end_token_id", "torch_dtype", "use_cache", "rope_scaling", "vision_config")},
        "preprocessor": preprocessor, "generation": generation,
        "tokenizer": {"declaredClass": tokenizer_config["tokenizer_class"], "jsonModelType": tokenizer["model"]["type"],
                      "bosToken": tokenizer_config["bos_token"], "clsToken": tokenizer_config["cls_token"],
                      "eosToken": tokenizer_config["eos_token"], "eosTokenId": generation["eos_token_id"],
                      "imageToken": tokenizer_config["image_token"], "addedTokenCount": len(added_token_ids)},
        "officialTaskPrompts": PROMPTS, "officialScalarResizeCases": cases, "tokenizerReference": token_reference,
        "openItems": ["Official image pixel/patch-packing and tokenizer baseline vectors", "Official real-image greedy generation",
            "Export vision/projector, embeddings, dynamic prefill and decode-with-past ONNX graphs", "C# tokenizer, multimodal positions, KV ownership and generation limits",
            "ORT end-to-end parity and quality", "Layout/ROI/multipage pipeline", "Exact OpenVINO/TensorRT/OpenCV tests and backend status", "Release model assets and full user samples"],
        "boundary": "Source/config/tokenizer acquisition plus official scalar resize only. Weight LFS SHA is upstream metadata, not locally verified weights. No model is imported or run, no ONNX is exported, no C# inference support/performance/quality/backend claim. Processor temporal_patch_size=1 overrides the vision config value 2; image pixel/packing parity remains untested."
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", type=Path, required=True, help="External cache directory for this pinned revision")
    parser.add_argument("--report-path", type=Path, required=True)
    parser.add_argument("--with-tokenizer-reference", action="store_true", help="Use installed tokenizers/sentencepiece to record real upstream-asset reference vectors")
    args = parser.parse_args()
    report = inspect(args.model_root.resolve(), args.with_tokenizer_reference)
    args.report_path.parent.mkdir(parents=True, exist_ok=True)
    args.report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("PADDLEOCR_VL_INSPECTION_REPORT=" + str(args.report_path.resolve()), flush=True)


if __name__ == "__main__":
    main()
