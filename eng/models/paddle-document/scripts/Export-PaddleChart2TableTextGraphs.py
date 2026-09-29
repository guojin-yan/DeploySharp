"""Export PP-Chart2Table token-embedding, prompt-prefill, and dynamic-KV decode ONNX graphs.

The source checkpoint stays untouched. Vision/projector export is handled by
Export-PaddleChartVision.py; this script emits the other three bundle graphs.
Use the official local checkpoint and the pinned PaddleX 3.7.2 source wheel.
"""
import argparse
import gc
import hashlib
import json
import math
import subprocess
import sys
import time
from pathlib import Path

import numpy as np
import onnx
import paddle
import paddle.nn as nn
import paddle.nn.functional as F
from onnx import helper, numpy_helper
from paddlex.inference.models.doc_vlm.modeling.GOT_ocr_2_0 import PPChart2TableInference
from paddlex.inference.models.doc_vlm.modeling._config_pp_chart2table import PPChart2TableConfig
import paddlex.inference.models.doc_vlm.modeling.qwen2 as qwen2_impl


SOURCE_FILE = "paddlex/inference/models/doc_vlm/modeling/GOT_ocr_2_0.py"
SOURCE_SHA256 = "cb6526f80d07d6aac678410cdf0a25da8baf6e9a70e69542c64e459b456cf3fa"
EPSILON_NAME = "chart2table_rms_norm_eps"
qwen2_impl.get_device_type = lambda: "cpu"


def attention_without_static_shape_assertions(query_states, config, key_states, value_states, attention_mask, output_attentions, attn_mask_startend_row_indices=None, training=True, sequence_parallel=False, skip_recompute=False):
    """Keep PaddleX Qwen2 attention math while allowing dynamic KV-axis tracing."""
    batch, query_length, heads, head_dimension = query_states.shape
    _, kv_length, _, _ = value_states.shape
    query = paddle.transpose(query_states, [0, 2, 1, 3])
    key = paddle.transpose(key_states, [0, 2, 1, 3])
    value = paddle.transpose(value_states, [0, 2, 1, 3])
    scale = 32 if paddle.in_dynamic_mode() and query.dtype == paddle.float16 else 1
    scores = paddle.matmul(query / (math.sqrt(head_dimension) * scale), key.transpose([0, 1, 3, 2]))
    if attention_mask is None:
        attention_mask = qwen2_impl.get_triangle_upper_mask(scores)
    scores = scores + attention_mask.reshape([batch, 1, query_length, kv_length])
    if not paddle.in_dynamic_mode():
        scores = F.softmax(scores * scale, axis=-1, dtype="float32").astype(query.dtype)
    else:
        with paddle.amp.auto_cast(False):
            scores = F.softmax(scores.astype("float32") * scale, axis=-1, dtype="float32").astype(query.dtype)
    scores = F.dropout(scores, p=config.attention_dropout, training=training)
    result = paddle.matmul(scores, value).transpose([0, 2, 1, 3])
    if sequence_parallel:
        return result.reshape([batch * query_length, heads * head_dimension])
    return result.reshape([batch, query_length, heads * head_dimension])


qwen2_impl.scaled_dot_product_attention = attention_without_static_shape_assertions


def map_checkpoint_key(key):
    if key == "lm_head.weight":
        return key
    if key.startswith("qwen2.embed_tokens."):
        return "model.language_model.embed_tokens." + key[len("qwen2.embed_tokens."):]
    if key.startswith("qwen2.layers."):
        return "model.language_model.layers." + key[len("qwen2.layers."):]
    if key.startswith("qwen2.norm."):
        return "model.language_model.norm." + key[len("qwen2.norm."):]
    if key.startswith("qwen2.mm_projector_vary."):
        return "model.multi_modal_projector.multimodal_projector." + key[len("qwen2.mm_projector_vary."):]
    suffix = key[len("qwen2.vision_tower_high."):]
    suffix = suffix.replace("patch_embed.proj.", "patch_embed.projection.")
    suffix = suffix.replace("blocks.", "layers.").replace("norm1.", "layer_norm1.").replace("norm2.", "layer_norm2.")
    suffix = suffix.replace("neck.0.", "neck.conv1.").replace("neck.1.", "neck.layer_norm1.")
    suffix = suffix.replace("neck.2.", "neck.conv2.").replace("neck.3.", "neck.layer_norm2.")
    if suffix.startswith("net_2."):
        return "model.multi_modal_projector.conv_upsampler1." + suffix[6:]
    if suffix.startswith("net_3."):
        return "model.multi_modal_projector.conv_upsampler2." + suffix[6:]
    return "model.vision_tower." + suffix


class ExportRotary(nn.Layer):
    """Rebuild the small rotary table per call to avoid static-tracer aliasing."""
    def __init__(self, dimension, base):
        super().__init__()
        self.dimension = dimension
        self.base = base

    def forward(self, value, seq_len=None):
        positions = paddle.arange(seq_len, dtype="float32")
        inverse = 1.0 / (self.base ** (paddle.arange(0, self.dimension, 2, dtype="float32") / self.dimension))
        frequencies = paddle.einsum("i,j->ij", positions, inverse)
        embedding = paddle.concat([frequencies, frequencies], axis=-1)
        return embedding.cos()[None, :, None, :].cast(value.dtype), embedding.sin()[None, :, None, :].cast(value.dtype)


class TokenEmbedding(nn.Layer):
    def __init__(self, embedding):
        super().__init__()
        self.embedding = embedding

    def forward(self, input_ids):
        return self.embedding(input_ids).astype("float32")


class PromptPrefill(nn.Layer):
    def __init__(self, decoder, lm_head):
        super().__init__()
        self.decoder = decoder
        self.lm_head = lm_head

    def forward(self, inputs_embeds, attention_mask, position_ids):
        hidden, cache = self.decoder(inputs_embeds=inputs_embeds, attention_mask=attention_mask, position_ids=position_ids, past_key_values=None, use_cache=True, return_dict=False)
        values = [self.lm_head(hidden[:, -1:, :])]
        for key, value in cache:
            values.extend([key, value])
        return tuple(values)


class DynamicPastDecode(nn.Layer):
    def __init__(self, decoder, lm_head):
        super().__init__()
        self.decoder = decoder
        self.lm_head = lm_head

    def forward(self, inputs_embeds, attention_mask, position_ids, past_key_0, past_value_0, past_key_1, past_value_1, past_key_2, past_value_2, past_key_3, past_value_3, past_key_4, past_value_4, past_key_5, past_value_5, past_key_6, past_value_6, past_key_7, past_value_7, past_key_8, past_value_8, past_key_9, past_value_9, past_key_10, past_value_10, past_key_11, past_value_11, past_key_12, past_value_12, past_key_13, past_value_13, past_key_14, past_value_14, past_key_15, past_value_15, past_key_16, past_value_16, past_key_17, past_value_17, past_key_18, past_value_18, past_key_19, past_value_19, past_key_20, past_value_20, past_key_21, past_value_21, past_key_22, past_value_22, past_key_23, past_value_23):
        past = ((past_key_0, past_value_0), (past_key_1, past_value_1), (past_key_2, past_value_2), (past_key_3, past_value_3), (past_key_4, past_value_4), (past_key_5, past_value_5), (past_key_6, past_value_6), (past_key_7, past_value_7), (past_key_8, past_value_8), (past_key_9, past_value_9), (past_key_10, past_value_10), (past_key_11, past_value_11), (past_key_12, past_value_12), (past_key_13, past_value_13), (past_key_14, past_value_14), (past_key_15, past_value_15), (past_key_16, past_value_16), (past_key_17, past_value_17), (past_key_18, past_value_18), (past_key_19, past_value_19), (past_key_20, past_value_20), (past_key_21, past_value_21), (past_key_22, past_value_22), (past_key_23, past_value_23))
        hidden, cache = self.decoder(inputs_embeds=inputs_embeds, attention_mask=attention_mask, position_ids=position_ids, past_key_values=past, use_cache=True, return_dict=False)
        values = [self.lm_head(hidden)]
        for key, value in cache:
            values.extend([key, value])
        return tuple(values)


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def convert_graph(prefix, opset):
    onnx_path = prefix.with_suffix(".onnx")
    command = [
        sys.executable, "-m", "paddle2onnx.command",
        "--model_dir", str(prefix.parent),
        "--model_filename", prefix.name + ".json",
        "--params_filename", prefix.name + ".pdiparams",
        "--save_file", str(onnx_path),
        "--opset_version", str(opset),
        "--enable_onnx_checker", "False",
        "--enable_dist_prim_all", "True",
    ]
    subprocess.run(command, check=True)
    return patch_rmsnorm_epsilon(onnx_path)


def patch_rmsnorm_epsilon(path):
    model = onnx.load(str(path), load_external_data=False)
    if not any(value.name == EPSILON_NAME for value in model.graph.initializer):
        model.graph.initializer.append(numpy_helper.from_array(np.asarray([1e-6], dtype=np.float32), EPSILON_NAME))
    nodes = []
    patched = 0
    for index, node in enumerate(list(model.graph.node)):
        if node.op_type == "Sqrt" and len(node.input) == 1:
            adjusted = "chart2table_rmsnorm_eps_" + str(index)
            nodes.append(helper.make_node("Add", [node.input[0], EPSILON_NAME], [adjusted], name="Chart2TableRmsNormEpsilon_" + str(index)))
            node.input[0] = adjusted
            patched += 1
        nodes.append(node)
    model.graph.ClearField("node")
    model.graph.node.extend(nodes)
    onnx.save(model, str(path))
    onnx.checker.check_model(str(path))
    return {"path": str(path), "bytes": path.stat().st_size, "sha256": sha256_file(path), "patchedSqrtNodes": patched}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-root", required=True, help="Existing official PP-Chart2Table model directory; no downloads are performed.")
    parser.add_argument("--paddlex-wheel", required=True, help="Pinned PaddleX 3.7.2 wheel used as the source revision proof.")
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--opset", type=int, default=17)
    args = parser.parse_args()
    root = Path(args.model_root).resolve()
    wheel_path = Path(args.paddlex_wheel).resolve()
    output = Path(args.output_root).resolve()
    output.mkdir(parents=True, exist_ok=True)
    required = [root / "config.json", root / "model_state.pdparams", wheel_path]
    for path in required:
        if not path.is_file():
            raise FileNotFoundError(path)
    import zipfile
    with zipfile.ZipFile(wheel_path) as wheel:
        source_bytes = wheel.read(SOURCE_FILE)
    source_sha = hashlib.sha256(source_bytes).hexdigest()
    if source_sha != SOURCE_SHA256:
        raise ValueError("The PaddleX wheel does not match the pinned GOT source: " + source_sha)

    record = {
        "status": "started",
        "modelId": "paddle-chart/pp-chart2table",
        "checkpointSha256": sha256_file(root / "model_state.pdparams"),
        "paddlexSourceSha256": source_sha,
        "opset": args.opset,
        "downloadedAssets": False,
        "graphs": [],
    }
    try:
        config = PPChart2TableConfig(**json.loads((root / "config.json").read_text(encoding="utf-8")))
        model = PPChart2TableInference(config)
        weights = paddle.load(str(root / "model_state.pdparams"))
        mapped = {
            map_checkpoint_key(key): (value.astype("float32") if str(value.dtype) in ("paddle.bfloat16", "bfloat16") else value)
            for key, value in weights.items()
        }
        expected = set(model.state_dict())
        if expected != set(mapped):
            raise ValueError("Checkpoint map mismatch: missing=" + str(sorted(expected - set(mapped))[:8]) + ";extra=" + str(sorted(set(mapped) - expected)[:8]))
        model.set_state_dict(mapped)
        model.eval()
        for layer in model.model.language_model.layers:
            rotary = layer.self_attn.rotary_emb
            layer.self_attn.rotary_emb = ExportRotary(rotary.dim, rotary.base)
        del weights, mapped
        gc.collect()

        graphs = []
        embedding_prefix = output / "chart-token-embedding-dynamic"
        embedding = TokenEmbedding(model.model.language_model.embed_tokens)
        embedding.eval()
        start = time.perf_counter()
        paddle.jit.save(paddle.jit.to_static(embedding, input_spec=[paddle.static.InputSpec([1, -1], "int64", "input_ids")]), str(embedding_prefix))
        graphs.append({"role": "token-embedding", "prefix": embedding_prefix, "staticSaveMs": (time.perf_counter() - start) * 1000})

        prefill_prefix = output / "chart-text-prefill-full"
        prefill = PromptPrefill(model.model.language_model, model.lm_head)
        prefill.eval()
        prefill_specs = [paddle.static.InputSpec([1, 286, 1024], "float32", "inputs_embeds"), paddle.static.InputSpec([1, 286], "bool", "attention_mask"), paddle.static.InputSpec([1, 286], "int64", "position_ids")]
        start = time.perf_counter()
        paddle.jit.save(paddle.jit.to_static(prefill, input_spec=prefill_specs), str(prefill_prefix))
        graphs.append({"role": "text-prefill", "prefix": prefill_prefix, "staticSaveMs": (time.perf_counter() - start) * 1000})

        decode_prefix = output / "chart-text-decoder-dynamic-past-full"
        decoder = DynamicPastDecode(model.model.language_model, model.lm_head)
        decoder.eval()
        decode_specs = [paddle.static.InputSpec([1, 1, 1024], "float32", "inputs_embeds"), paddle.static.InputSpec([1, -1], "bool", "attention_mask"), paddle.static.InputSpec([1, 1], "int64", "position_ids")]
        decode_sample = [paddle.zeros([1, 1, 1024], dtype="float32"), paddle.ones([1, 5], dtype="bool"), paddle.to_tensor([[4]], dtype="int64")]
        for layer in range(24):
            decode_specs.extend([paddle.static.InputSpec([1, -1, 16, 64], "float32", "past_key_" + str(layer)), paddle.static.InputSpec([1, -1, 16, 64], "float32", "past_value_" + str(layer))])
            decode_sample.extend([paddle.zeros([1, 4, 16, 64], dtype="float32"), paddle.zeros([1, 4, 16, 64], dtype="float32")])
        start = time.perf_counter()
        paddle.jit.save(paddle.jit.to_static(decoder, input_spec=decode_specs), str(decode_prefix))
        graphs.append({"role": "text-decode-with-past", "prefix": decode_prefix, "staticSaveMs": (time.perf_counter() - start) * 1000, "inputCount": len(decode_sample)})

        del prefill, decoder, embedding, model
        gc.collect()
        for graph in graphs:
            converted = convert_graph(graph["prefix"], args.opset)
            graph.update(converted)
            graph.pop("prefix", None)
            graph["opset"] = args.opset
        record["graphs"] = graphs
        record["status"] = "verified-three-text-graphs-exported"
    except Exception as error:
        record.update(status="blocked", exception=type(error).__name__, reason=str(error))
        raise
    finally:
        (output / "chart2table-text-graph-export.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
        print(json.dumps(record, indent=2), flush=True)


if __name__ == "__main__":
    main()
