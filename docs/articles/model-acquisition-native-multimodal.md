# Acquire native multimodal models / 获取原生多模态模型

This article reproduces the Stage 26 external warehouse. Models and converted graphs belong only under `E:\DeploySharp-Models\<model-name>`. Use a disposable temporary directory for source, environments, and scripts; do not write to `E:\Model`, `E:\Data`, Git, NuGet, catalog, or Release. / 本文复现阶段 26 外部统一仓库。模型与转换图只能进入 `E:\DeploySharp-Models\<模型名>`；源码、环境和脚本使用可删除临时目录，不写入用户资产、Git、NuGet、Catalog 或 Release。

## Pinned supply chain / 固定供应链

| Item / 项目 | Revision/license/size / Revision、许可与大小 |
| --- | --- |
| LLaVA-NeXT code | `LLaVA-VL/LLaVA-NeXT@bce12e479bc4dfee2b9c50c88137b01ff51bd483`, Apache-2.0 |
| LLaVA OneVision HF bundle | `llava-hf/llava-onevision-qwen2-0.5b-ov-hf@74dd0bf867a4cda7950c17663794267c60cf4b40`, Apache-2.0 |
| Original LLaVA weights | `lmms-lab/llava-onevision-qwen2-0.5b-ov@381d9947148efb1e58a577f451c05705ceec666e`, Apache-2.0 |
| Qwen2.5-VL 3B | `Qwen/Qwen2.5-VL-3B-Instruct@66285546d2b821cf421d4f5eb2576359d3770cd3`, 3,754,622,976 weight bytes; Qwen Research License Agreement |
| Phi-3.5 Vision | `microsoft/Phi-3.5-vision-instruct@12b77fb40b63a2c73c68243d3f767aab688a1b2a`, 4,146,621,440 weight bytes; MIT |

The Qwen code repository is Apache-2.0, but the selected checkpoint LICENSE is the Qwen Research License. Treat code and weights separately. / Qwen 代码仓库是 Apache-2.0，但所选权重使用 Qwen Research License；代码许可与权重许可必须分开审核。

## LLaVA OneVision warehouse / LLaVA OneVision 仓库

Use the pinned Hugging Face revision and download only into the model directory. The audited transfer used the official repository metadata and verified every LFS hash; a mirror was used only when the official large-file endpoint timed out, and the returned commit had to equal the pinned revision. New users should prefer the official endpoint. / 使用固定 HF Revision 且只下载到模型目录。审核时官方大文件端点超时后仅将镜像作为传输通道，并强制校验返回 Commit 与 LFS SHA；新用户优先使用官方端点。

```powershell
$revision = '74dd0bf867a4cda7950c17663794267c60cf4b40'
$warehouse = 'E:\DeploySharp-Models\llava-onevision-qwen2-0.5b-ov-hf'
New-Item -ItemType Directory -Force $warehouse
# huggingface-cli download llava-hf/llava-onevision-qwen2-0.5b-ov-hf --revision $revision --local-dir $warehouse
Get-ChildItem $warehouse -Recurse -File | Get-FileHash -Algorithm SHA256
```

Critical audited files:

| File / 文件 | Bytes | SHA256 |
| --- | ---: | --- |
| `model.safetensors` | 1,787,445,680 | `07b3362c3412de79baf2379e44e5b0b2a8f4b965ebebd11d7b5b3eb4450fe96e` |
| `official-onnx-int8/vision_encoder.onnx` | 1,598,932,026 | `06cf8f4eefdea6cb8f095724e37da8fa0358820a3506e1c85915d5d2bdadab43` |
| `official-onnx-int8/embed_tokens_int8.onnx` | 136,192,544 | `4b4dec69949d75a775d871c5e1dc3db6bd4fd6e8ceffb3deafe64e8f16a8323d` |
| `official-onnx-int8/decoder_model_merged_int8.onnx` | 512,154,211 | `cc674946412447fa76df18686c32541b1388c0fa62cbf53c36dccd1a90649c3f` |
| `official-onnx-int8/vision_encoder_int8.onnx` | 404,097,518 | `d284a45f927b2dbf8a61622444b1c622275c323d95cbd1d07bd3a38c72caf1bb` |
| `tokenizer.json` | 7,028,579 | `3c0ce3213b50ff38d8aa1e91136a2d2cb142a3f569246170872e439cb2a29d15` |
| `vocab.json` | 2,776,833 | `ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910` |
| `merges.txt` | 1,671,853 | `8831e4f1a044471340f7c0a83d7bd71306a5b867e95fd870f74d0c5308a904d5` |
| `preprocessor_config.json` | 1,732 | `3644c108b9f0fa53e62ff422a9be6639642f0e64dab4a71f961c7911d4386384` |
| `generation_config.json` | 126 | `89dc53229f50b59570b6852056dafeac8116c458f1a748bff491b6d4d24d3b51` |

The published ONNX graphs are already converted by the official `llava-hf` Transformers.js chain: Vision+projector opset 14, embedding opset 13, merged decoder opset 14. Do not re-export under the same path. If a new export is needed, write `converted-<tool>-<opset>-<date>` and retain command, dependency lock, model/sidecar SHA, dynamic axes, quantization, and exporter revision. / 官方仓库已发布上述 ONNX；不得覆盖原路径。重新导出必须使用独立命名目录并保留完整命令、锁文件、动态轴、量化与 SHA。

The INT8 Vision graph is not part of the executable bundle: ORT CPU 1.28.0 reproduces `ConvInteger(10)` failure at `/vision_tower/vision_model/embeddings/patch_embedding/Conv_quant`. Use the FP32 Vision graph and retain the failed graph as blocker evidence. / INT8 Vision 图会在上述节点稳定失败；可执行 Bundle 使用 FP32 Vision，失败图保留为 blocker 证据。

## Official golden procedure / 官方 Golden 流程

Use an isolated Python environment only to produce evidence, never as runtime fallback. The audited environment was Python 3.13.12, torch 2.9.1 CPU, Transformers 4.57.3, Pillow 12.0.0, and ORT 1.23.2. Pin the local model directory and use `local_files_only=True`. Run the official processor/model on an authorized image, save pixel values, projected features, packed features, expanded input IDs, completion IDs, selected logits, image-newline, version/timing JSON, then hash every file. / Python 仅用于生成证据，不作为运行时回退。固定本地模型目录，在获授权图片上保存 Pixel、Projected/Packed Feature、Prompt/Completion Token、Logit、Image-newline、版本与 Timing，并逐文件哈希。

The 350x350 gate image remained at `E:\Data\ocr\demo_2.jpg`; it was never copied. Its SHA256 is `957a9cc15da49312277796126be225e0ee653f3316578c12d626fa43fbe9561b`. Golden sidecars are stored under `E:\DeploySharp-Models\llava-onevision-qwen2-0.5b-ov-hf\evidence\ocr-demo2`. Pixel/projected/packed SHAs are `19951e6b...`, `c7f6b971...`, and `bc0bfc63...`. / 门控图片保持在调用方只读目录，未复制；Golden 进入模型名目录的 evidence 子目录。

Run the DeploySharp external gate with:

```powershell
$env:DEPLOYSHARP_NATIVE_VLM_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_NATIVE_VLM_MODEL_ROOT = 'E:\DeploySharp-Models\llava-onevision-qwen2-0.5b-ov-hf'
$env:DEPLOYSHARP_NATIVE_VLM_IMAGE = 'E:\Data\ocr\demo_2.jpg'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj --filter Stage26NativeMultimodalExternalIntegrationTests
```

Runtime version differences are evidence, not a reason to overwrite the official golden. ORT 1.28.0, OpenVINO 2026.2.1, and OpenCV results are documented in the main guide and Manifest. / Runtime 版本差异属于证据，不得覆盖官方 Golden；各后端结果见主指南与 Manifest。

## Qwen and Phi source-only directories / Qwen 与 Phi 仅来源目录

Only the following small official files were acquired; no weight shard was downloaded:

- `E:\DeploySharp-Models\qwen2.5-vl-3b-instruct`: config `7ed3eed5...`, processor `f2058c71...`, tokenizer config `4abd3520...`, LICENSE `b5c0e5cf...`, README `0b6da5a1...`.
- `E:\DeploySharp-Models\phi-3.5-vision-instruct`: config `567e28c6...`, preprocessor `0f3d920f...`, processor `286a9e7f...`, tokenizer config `581d4654...`, LICENSE `c2cfccb8...`, README `7df8e9b5...`.

Keep these models blocked until one complete official or traceable export preserves their exact dynamic image/position/RoPE/projector/chat/tokenizer/embedding/Prefill/KV contracts on both ORT and OpenVINO. A Python daemon, remote API, removed operator, different small VLM, or fixed output is not an export. / 在完整可追溯导出同时保真 ORT/OpenVINO 前保持 blocker；Python 常驻、远程 API、删算子、替代小模型或固定输出都不算导出。

All three ModelPack records declare `redistributionAllowed:false`. ModelFactory metadata is ready, but uploaded/downloadable remain false until independent license review, notices/source obligations, immutable release URI, catalog validation, and explicit upload authorization are complete. / 三条记录均禁止再分发；代码下载能力已具备，但资产上传/下载仍需独立许可审核和明确授权。
