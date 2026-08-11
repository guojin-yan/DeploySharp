# Acquire and convert BLIP family models / 获取与转换 BLIP 模型族

This procedure reproduces the audited BLIP base Caption bundle. It does not claim a complete BLIP VQA, BLIP-2, or InstructBLIP native export. Use a new temporary checkout and the external warehouse; never write into `E:\Model`, `E:\Data`, Git package content, or a user checkpoint. / 本文复现已审核的 BLIP Base Caption Bundle，不宣称 BLIP VQA、BLIP-2 或 InstructBLIP 已有完整 Native Export。使用独立临时 Checkout 与外部仓库，不写入用户资产或包内容。

## Pinned supply chain / 固定供应链

| Item / 项目 | Exact value / 精确值 |
| --- | --- |
| BLIP source | `salesforce/BLIP@056a169437371659074aa2732649d5de3bffb4a8`, BSD-3-Clause |
| Caption checkpoint | `model_base_caption_capfilt_large.pth`, 896081425 bytes, SHA256 `96ac8749bd0a568c274ebe302b3a3748ab9be614c737f3d8c529697139174086` |
| BERT vocabulary | 231508 bytes, SHA256 `07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3` |
| Export environment | Python 3.12, torch 2.9.1 CPU, torchvision 0.24.1, transformers source revision `05fa1a7ac17bb7aa07b9e0c1e138ecb31a28bbfe`, ONNX 1.20.0 |
| Native contract | opset 17, dynamic batch/sequence, fixed RGB 384, full-prefix greedy decoder, no KV cache |

Official checkpoint URI: `https://storage.googleapis.com/sfr-vision-language-research/BLIP/models/model_base_caption_capfilt_large.pth`. Vocabulary URI: `https://s3.amazonaws.com/models.huggingface.co/bert/bert-base-uncased-vocab.txt`. Verify byte count and SHA before model load. / 官方 URI 如上；加载前必须核验大小与 SHA。

```powershell
$temporary = 'E:\DeploySharp-Stage25-<date>'
$warehouse = 'E:\DeploySharp-Models\blip-caption-base'
git clone https://github.com/salesforce/BLIP.git "$temporary\blip-upstream"
git -C "$temporary\blip-upstream" checkout 056a169437371659074aa2732649d5de3bffb4a8
New-Item -ItemType Directory -Force "$warehouse\converted-opset17"
Get-FileHash "$warehouse\model_base_caption_capfilt_large.pth" -Algorithm SHA256
Get-FileHash "$warehouse\bert-base-uncased-vocab.txt" -Algorithm SHA256
```

Export two wrappers from the official loaded model: `model.visual_encoder(pixel_values)` and `model.text_decoder(..., use_cache=False).logits`. The decoder inputs must be named `input_ids`, `attention_mask`, `encoder_hidden_states`, and `encoder_attention_mask`; do not export positional binding. Use the official `predict.py` RGB transform: fixed Pillow bicubic 384, mean `(0.48145466,0.4578275,0.40821073)`, std `(0.26862954,0.26130258,0.27577711)`. / 从官方模型导出 Vision Encoder 和 `use_cache=False` 的完整前缀 Decoder；必须使用上述具名端口及官方 RGB 变换，不得按位置绑定。

```python
torch.onnx.export(vision, (pixel_values,), vision_path,
    input_names=["pixel_values"], output_names=["encoder_hidden_states"],
    dynamic_axes={"pixel_values": {0: "batch"}, "encoder_hidden_states": {0: "batch"}},
    opset_version=17, do_constant_folding=True, dynamo=False)
torch.onnx.export(decoder, (prefix, attention, image_state, image_mask), decoder_path,
    input_names=["input_ids", "attention_mask", "encoder_hidden_states", "encoder_attention_mask"],
    output_names=["logits"],
    dynamic_axes={"input_ids": {0: "batch", 1: "sequence"}, "attention_mask": {0: "batch", 1: "sequence"}, "encoder_hidden_states": {0: "batch"}, "encoder_attention_mask": {0: "batch"}, "logits": {0: "batch", 1: "sequence"}},
    opset_version=17, do_constant_folding=True, dynamo=False)
```

Before accepting output, run the official predictor and an independent full-prefix loop on the same authorized image. The audited image produced full IDs `[30522,1037,3861,1997,1037,2177,1997,2111,3061,1999,2392,1997,1037,3902,102]` and `a group of people standing in front of a bus`. Retain pixel, encoder, token, processor, generation, and evidence sidecars beside the graphs, then run ONNX checker and the stage-25 external .NET gate. / 接收输出前必须在同一获授权图片上运行官方 Predictor 和独立完整前缀循环，并将 Pixel、Encoder、Token 与合同 Evidence Sidecar 放在转换图旁，再执行 ONNX Checker 与阶段 25 外部门控。

BLIP VQA checkpoint size was audited as 1446244375 bytes, but its SHA and complete question/answer/ranking native bundle were not acquired. BLIP-2 OPT-2.7B and InstructBLIP Flan-T5-XL have pinned LAVIS config files under their model-name folders, but no complete official ONNX/OpenVINO Q-Former/projection/LLM/KV bundle. Keep all three as blockers; do not substitute a caption decoder, Python daemon, remote API, or another small model. / 其余三条路径保持 blocker，不得替换模型或静默回退。
