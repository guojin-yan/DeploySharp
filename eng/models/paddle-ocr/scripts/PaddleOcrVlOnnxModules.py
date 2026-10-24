"""Single-image ONNX adapters for pinned PaddleOCR-VL-1.6 weights.

The attention, rotary and projector equations are adapted from the Apache-2.0
PaddlePaddle/Hugging Face implementation (Transformers 5.20.2). Modules retain
the original parameters. No approximate attention, tied embeddings, fixed image
grid, baked prompt, or Python DynamicCache object is used in the ONNX graph.
"""

from __future__ import annotations

import torch
from torch import nn
from torch.nn import functional as F


def rotate_half(value):
    a, b = value.chunk(2, dim=-1)
    return torch.cat((-b, a), dim=-1)


class SingleImageVision(nn.Module):
    """Input [grid_h,grid_w,3,14,14], output [grid_h*grid_w/4,1024]."""

    def __init__(self, model):
        super().__init__()
        self.vision = model.model.visual.vision_model
        self.projector = model.model.projector

    def forward(self, pixel_patches):
        height, width = pixel_patches.shape[:2]
        count = height * width
        embedding = self.vision.embeddings
        hidden = embedding.patch_embedding(pixel_patches.reshape(-1, 3, 14, 14)).reshape(count, 1152)
        # Exactly the upstream align_corners=True four-tap gather, with sizes
        # taken from dynamic tensor axes. The source learned grid is 27x27.
        flat = torch.arange(count, device=pixel_patches.device)
        rows = torch.div(flat, width, rounding_mode="floor")
        columns = flat % width
        y = rows.float() * 26 / (height - 1)
        x = columns.float() * 26 / (width - 1)
        yf, xf = y.floor(), x.floor()
        taps = torch.arange(2, device=pixel_patches.device)
        yi = (yf.long()[:, None] + taps).clamp(0, 26)
        xi = (xf.long()[:, None] + taps).clamp(0, 26)
        yw = (1 - (y[:, None] - yf[:, None] - taps).abs()).clamp(min=0)
        xw = (1 - (x[:, None] - xf[:, None] - taps).abs()).clamp(min=0)
        indices = (yi[:, :, None] * 27 + xi[:, None, :]).reshape(-1, 4)
        weights = (yw[:, :, None] * xw[:, None, :]).reshape(-1, 4)
        hidden = hidden + (embedding.position_embedding(indices) * weights[:, :, None]).sum(1)
        inverse = self.vision.encoder.rotary_pos_emb.inv_freq
        phase = torch.stack((rows, columns), dim=-1).float()[..., None] * inverse
        phase = torch.cat((phase[:, 0], phase[:, 1]), dim=-1)
        phase = torch.cat((phase, phase), dim=-1)
        cosine, sine = phase.cos()[:, None, :], phase.sin()[:, None, :]
        for layer in self.vision.encoder.layers:
            normalized = layer.layer_norm1(hidden)
            attention = layer.self_attn
            q = attention.q_proj(normalized).reshape(count, 16, 72)
            k = attention.k_proj(normalized).reshape(count, 16, 72)
            v = attention.v_proj(normalized).reshape(count, 16, 72)
            q = q * cosine + rotate_half(q) * sine
            k = k * cosine + rotate_half(k) * sine
            attended = F.scaled_dot_product_attention(q.transpose(0, 1)[None], k.transpose(0, 1)[None],
                v.transpose(0, 1)[None], dropout_p=0.0, scale=attention.scaling)
            hidden = hidden + attention.out_proj(attended[0].transpose(0, 1).reshape(count, 1152))
            hidden = hidden + layer.mlp(layer.layer_norm2(hidden))
        hidden = self.projector.pre_norm(self.vision.post_layernorm(hidden))
        hidden = hidden.reshape(height // 2, 2, width // 2, 2, 1152).transpose(1, 2).reshape(-1, 4608)
        return self.projector.linear_2(self.projector.act(self.projector.linear_1(hidden)))


class TokenEmbedding(nn.Module):
    def __init__(self, model):
        super().__init__()
        self.embedding = model.model.language_model.embed_tokens

    def forward(self, input_ids):
        return self.embedding(input_ids)


class LanguageHead(nn.Module):
    def __init__(self, model):
        super().__init__()
        self.head = model.lm_head

    def forward(self, last_hidden_state):
        return self.head(last_hidden_state)


class DynamicTextDecoder(nn.Module):
    """Same graph for prefill (past=0) and decode; single unpadded request.

    The cache has 18 pairs of [1,2,past,128]. Present lengths are past+query.
    Only the final normalized hidden vector is returned for lm_head: allocating
    prompt_length*vocab_size logits is unnecessary for greedy generation.
    """

    def __init__(self, model):
        super().__init__()
        self.language = model.model.language_model

    def forward(self, inputs_embeds, position_ids, *past):
        hidden = inputs_embeds
        query_length = inputs_embeds.shape[1]
        past_length = past[0].shape[2]
        # Explicit physical cache positions for the causal mask. Multimodal
        # RoPE coordinates must never be used as causal-mask indices.
        q_index = torch.arange(query_length, device=hidden.device) + past_length
        k_index = torch.arange(past_length + query_length, device=hidden.device)
        causal = (k_index[None, :] <= q_index[:, None])[None, None]
        inverse = self.language.rotary_emb.inv_freq
        phase = position_ids[..., None].float() * inverse
        phase = torch.cat((phase[0, ..., :16], phase[1, ..., 16:40], phase[2, ..., 40:64]), dim=-1)
        phase = torch.cat((phase, phase), dim=-1)
        cosine, sine = phase.cos()[:, None], phase.sin()[:, None]
        present = []
        for index, layer in enumerate(self.language.layers):
            normalized = layer.input_layernorm(hidden)
            attention = layer.self_attn
            q = attention.q_proj(normalized).reshape(1, query_length, 16, 128).transpose(1, 2)
            k = attention.k_proj(normalized).reshape(1, query_length, 2, 128).transpose(1, 2)
            v = attention.v_proj(normalized).reshape(1, query_length, 2, 128).transpose(1, 2)
            q = q * cosine + rotate_half(q) * sine
            k = k * cosine + rotate_half(k) * sine
            k = torch.cat((past[index * 2], k), dim=2)
            v = torch.cat((past[index * 2 + 1], v), dim=2)
            present.extend((k, v))
            # This has the same head ordering as upstream repeat_kv.
            repeated_k = k[:, :, None].expand(1, 2, 8, k.shape[2], 128).reshape(1, 16, k.shape[2], 128)
            repeated_v = v[:, :, None].expand(1, 2, 8, v.shape[2], 128).reshape(1, 16, v.shape[2], 128)
            attended = F.scaled_dot_product_attention(q, repeated_k, repeated_v, attn_mask=causal,
                dropout_p=0.0, scale=attention.scaling)
            hidden = hidden + attention.o_proj(attended.transpose(1, 2).reshape(1, query_length, 2048))
            hidden = hidden + layer.mlp(layer.post_attention_layernorm(hidden))
        return (self.language.norm(hidden[:, -1:]), *present)


def single_image_positions(input_ids, grid_height, grid_width):
    """Single image, no padding; independently checked against both upstreams."""
    ids = input_ids.reshape(-1)
    offsets = torch.where(ids == 100295)[0]
    expected = grid_height * grid_width // 4
    if len(offsets) != expected or not torch.equal(offsets, torch.arange(offsets[0], offsets[0] + expected)):
        raise ValueError("Image token count/order does not match the image grid")
    start = int(offsets[0])
    merged_h, merged_w = grid_height // 2, grid_width // 2
    text_before = torch.arange(start).repeat(3, 1)
    image = torch.stack((torch.zeros(expected, dtype=torch.long),
        torch.arange(merged_h).repeat_interleave(merged_w), torch.arange(merged_w).repeat(merged_h))) + start
    tail_start = start + max(merged_h, merged_w)
    text_after = (torch.arange(len(ids) - start - expected) + tail_start).repeat(3, 1)
    positions = torch.cat((text_before, image, text_after), dim=1)[:, None]
    delta = int(positions.max()) + 1 - len(ids)
    return positions, delta
