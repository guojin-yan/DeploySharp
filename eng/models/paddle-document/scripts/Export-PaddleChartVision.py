"""Export the vision component of the legacy PP-Chart2Table checkpoint.

This does NOT export text generation and is NOT a complete DeploySharp model.
Requires the pinned PaddleX 3.7.2 wheel, Paddle, Paddle2ONNX, ONNX and ORT.
The official vision classes are loaded unchanged; all renamed weights must match.
"""
import argparse
import ast
import gc
import hashlib
import json
import linecache
import types
import zipfile
from pathlib import Path

import numpy as np
import paddle
import paddle.nn as nn
import paddle.nn.functional as F
from PIL import Image

ACT2FN = {"gelu": F.gelu}
SOURCE_SHA = "cb6526f80d07d6aac678410cdf0a25da8baf6e9a70e69542c64e459b456cf3fa"


class ChartVision(nn.Layer):
    def __init__(self, config):
        super().__init__()
        self.vision = GotOcr2VisionEncoder(config.vision_config)
        self.projector = GotOcr2MultiModalProjector(config)

    def forward(self, pixel_values):
        return self.projector(self.vision(pixel_values))


def legacy_key(key):
    if key.startswith("projector."):
        return (key.replace("projector.conv_upsampler1.", "qwen2.vision_tower_high.net_2.")
                .replace("projector.conv_upsampler2.", "qwen2.vision_tower_high.net_3.")
                .replace("projector.multimodal_projector.", "qwen2.mm_projector_vary."))
    return (key.replace("vision.", "qwen2.vision_tower_high.", 1)
            .replace("patch_embed.projection.", "patch_embed.proj.")
            .replace(".layers.", ".blocks.").replace(".layer_norm1.", ".norm1.").replace(".layer_norm2.", ".norm2.")
            .replace(".neck.conv1.", ".neck.0.").replace(".neck.norm1.", ".neck.1.")
            .replace(".neck.conv2.", ".neck.2.").replace(".neck.norm2.", ".neck.3."))


def sha(path):
    with open(path, "rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--wheel", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--image", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    destination = Path(args.output)
    destination.mkdir(parents=True, exist_ok=True)
    record = dict(component="vision-only", completeModel=False, status="started", sourceSha256=SOURCE_SHA)
    try:
        with zipfile.ZipFile(args.wheel) as wheel:
            content = wheel.read("paddlex/inference/models/doc_vlm/modeling/GOT_ocr_2_0.py")
        assert hashlib.sha256(content).hexdigest() == SOURCE_SHA, "Use the pinned PaddleX 3.7.2 wheel."
        source = content.decode("utf-8")
        filename = "<paddlex-3.7.2-GOT_ocr_2_0>"
        linecache.cache[filename] = (len(source), None, source.splitlines(True), filename)
        tree = ast.parse(source)
        nodes = [n for n in tree.body if isinstance(n, ast.ClassDef) and n.name.startswith("GotOcr2") and n.name != "GotOcr2Model"]
        exec(compile(ast.Module(body=nodes, type_ignores=[]), filename, "exec"), globals())
        vision = types.SimpleNamespace(hidden_size=768, output_channels=256, num_hidden_layers=12,
            num_attention_heads=12, num_channels=3, image_size=1024, patch_size=16, hidden_act="gelu",
            layer_norm_eps=1e-6, qkv_bias=True, use_abs_pos=True, use_rel_pos=True, window_size=14,
            global_attn_indexes=[2, 5, 8, 11], mlp_dim=3072, attention_dropout=0.)
        config = types.SimpleNamespace(vision_config=vision, text_config=types.SimpleNamespace(hidden_size=1024))
        model = ChartVision(config)
        weights = paddle.load(args.checkpoint)
        mapped = {}
        consumed = set()
        for key, expected in model.state_dict().items():
            original = legacy_key(key)
            assert original in weights, f"Missing checkpoint tensor: {original}"
            assert list(weights[original].shape) == list(expected.shape), f"Shape mismatch: {original}"
            mapped[key] = weights[original].astype("float32")
            consumed.add(original)
        required = {key for key in weights if key.startswith("qwen2.vision_tower_high.") or key.startswith("qwen2.mm_projector_vary.")}
        assert consumed == required, f"Unmapped vision weights: {required - consumed}"
        model.set_state_dict(mapped)
        model.eval()
        del mapped, weights
        gc.collect()
        print(f"Loaded {len(consumed)} vision/projector tensors", flush=True)
        image = Image.open(args.image).convert("RGB").resize((1024, 1024), Image.Resampling.BICUBIC)
        pixels = np.asarray(image).astype("float32") / 255
        pixels = (pixels - np.array([.48145466, .4578275, .40821073], dtype="float32")) / np.array([.26862954, .26130258, .27577711], dtype="float32")
        pixels = np.ascontiguousarray(pixels.transpose(2, 0, 1)[None])
        with paddle.no_grad():
            expected = model(paddle.to_tensor(pixels)).numpy()
        assert expected.shape == (1, 256, 1024) and np.isfinite(expected).all()
        print("Paddle vision reference complete", flush=True)
        prefix = str(destination / "chart-vision")
        paddle.onnx.export(model, prefix, input_spec=[paddle.static.InputSpec([1, 3, 1024, 1024], "float32", "pixel_values")], opset_version=17)
        import onnx
        import onnxruntime as ort
        onnx.checker.check_model(prefix + ".onnx")
        options = ort.SessionOptions()
        options.intra_op_num_threads = 4
        session = ort.InferenceSession(prefix + ".onnx", sess_options=options, providers=["CPUExecutionProvider"])
        actual = session.run(None, {session.get_inputs()[0].name: pixels})[0]
        np.testing.assert_allclose(actual, expected, rtol=.002, atol=.005)
        record.update(status="verified-component", checkpointSha256=sha(args.checkpoint), onnxSha256=sha(prefix + ".onnx"), inputSha256=sha(args.image), shape=list(actual.shape), maximumAbsoluteError=float(np.abs(actual - expected).max()), mappedTensors=len(consumed))
    except Exception as error:
        record.update(status="blocked", exception=type(error).__name__, reason=str(error))
        raise
    finally:
        (destination / "chart-vision-export.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
        print(json.dumps(record, indent=2), flush=True)


if __name__ == "__main__":
    main()
