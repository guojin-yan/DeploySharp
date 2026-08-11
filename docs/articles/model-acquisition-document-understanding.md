# Acquire document-understanding models / 获取文档理解模型

This article reproduces the Stage 27 warehouse. Models, converted graphs, Tokenizer/Processor/Schema files, datasets, and Golden evidence belong only under `E:\DeploySharp-Models\<model-name>`. Use a disposable temporary directory for Python and caches. Do not write to or copy from `E:\Model` or `E:\Data`, and do not place model assets in Git, NuGet, the official catalog, or Release. / 本文复现阶段 27 warehouse；全部业务工件仅进入模型名目录。Python 与缓存使用可删除临时目录，不触碰用户资产根，也不进入 Git/NuGet/Catalog/Release。

## Pinned supply chain / 固定供应链

Facts were checked against official sources on 2026-08-09.

| Item / 项目 | Exact revision / 精确 Revision | License/status / 许可与状态 |
| --- | --- | --- |
| Microsoft LayoutLMv3 code | `microsoft/unilm@833df7e7832e5064a281131ee64a481afa8e5b95` | MIT code repository |
| LayoutLMv3 base checkpoint | `microsoft/layoutlmv3-base@cfbbbff0762e6aab37086fdd4739ad14fe7d5db4` | CC-BY-NC-SA-4.0, noncommercial; base has no task head |
| NAVER Donut code | `clovaai/donut@4cfcf972560e1a0f26eb3e294c8fc88a0d336626` | MIT |
| Donut CORD-v2 checkpoint | `naver-clova-ix/donut-base-finetuned-cord-v2@8003d433113256b4ce3a0f5bf604b29ff78a7451` | MIT model card; External only here |
| CORD-v2 dataset | `naver-clova-ix/cord-v2@7f0115a4b758a71d6473b8d085751692da2fef98` | CC-BY-4.0 |
| Google Pix2Struct code | `google-research/pix2struct@6fe25c1dc8151823ee3b479519d8d5948812fee4` | Apache-2.0 |
| Pix2Struct DocVQA base | `google/pix2struct-docvqa-base@63f6b3de436e39f75c7a486881a9c2c14a7f4e89` | Apache-2.0; source-contract blocker |
| Transformers source | `huggingface/transformers@e8ea728a3eeeb903e77c7d1bd29267c80a1be71f` | Apache-2.0 |
| ONNX Runtime source | `microsoft/onnxruntime@2e76898a35eb76b7dedc0354ee58095317b64d9f` | MIT |
| OpenVINO source | `openvinotoolkit/openvino@0f453eb8dca021e7176cdcc8570242c9f2fec7c5` | Apache-2.0 |

Code and checkpoint terms are separate. Local possession and an open model card do not authorize DeploySharp redistribution; all three manifests deliberately set `redistributionAllowed:false`. / 代码许可与权重/数据许可分别审核；本机持有不等于再分发授权，三份 Manifest 均显式禁止再分发。

## Acquire exact files / 获取精确文件

The PowerShell acquisition script uses immutable Hugging Face resolve URIs and writes directly to the three warehouse directories. `-IncludeCordTest` also obtains the pinned official test parquet.

```powershell
& eng\models\document-understanding\Acquire-Stage27Models.ps1 `
  -Warehouse 'E:\DeploySharp-Models' `
  -IncludeCordTest
```

Audit the local files and write source records without downloading or changing them:

```powershell
$python = '<isolated-python>\python.exe'
& $python eng\models\document-understanding\scripts\acquire_stage27.py `
  --warehouse 'E:\DeploySharp-Models' `
  --audit-only
```

The script downloads only small configuration/processor/tokenizer files for LayoutLMv3 and Pix2Struct. It does not download their weights because neither has an admitted executable Stage 27 bundle. It acquires the complete Donut checkpoint because that model is the real end-to-end representative. / LayoutLMv3 与 Pix2Struct 仅获取合同文件；缺少可执行 bundle 时不为“看起来完整”而下载权重。Donut 取得完整 checkpoint 用于真实链路。

## Isolated dependency lock / 隔离依赖锁

The audited environment used Python 3.11.15 and these exact packages: torch `2.9.1+cpu`, transformers `4.57.3`, onnx `1.20.0`, onnxruntime `1.23.2`, OpenVINO `2026.2.1`, optimum `2.1.0`, optimum-onnx `0.1.0`, huggingface-hub `0.36.0`, Pillow `12.0.0`, sentencepiece `0.2.1`, numpy `2.3.5`, and pyarrow `22.0.0`. One reproducible `uv` setup is:

```powershell
uv venv --python 3.11.15 E:\DeploySharp-Stage27\py311
$python = 'E:\DeploySharp-Stage27\py311\Scripts\python.exe'
uv pip install --python $python torch==2.9.1 --index-url https://download.pytorch.org/whl/cpu
uv pip install --python $python transformers==4.57.3 onnx==1.20.0 `
  onnxruntime==1.23.2 openvino==2026.2.1 optimum==2.1.0 `
  optimum-onnx==0.1.0 huggingface-hub==0.36.0 pillow==12.0.0 `
  sentencepiece==0.2.1 numpy==2.3.5 pyarrow==22.0.0
```

The isolated interpreter is evidence tooling only. No Python process or package is required by the runtime or distributed in NuGet. / 隔离 Python 仅生成证据；运行时和 NuGet 都不依赖 Python。

## Donut export and official Golden / Donut 导出与官方 Golden

Export from the pinned local checkpoint into a new warehouse subdirectory. Do not overwrite the checkpoint or reuse a directory produced by another dependency lock.

```powershell
$root = 'E:\DeploySharp-Models\donut-base-finetuned-cord-v2'
& $python -m optimum.exporters.onnx `
  --model "$root\checkpoint" `
  --task image-to-text `
  --opset 17 `
  "$root\onnx"
```

The result is three FP32 opset-17 graphs with inline weights: `encoder_model.onnx`, `decoder_model.onnx`, and `decoder_with_past_model.onnx`. No `.data` External Data sidecar was emitted. Generate official Processor/Predictor evidence from CORD test row 0:

```powershell
& $python eng\models\document-understanding\scripts\generate_donut_evidence.py `
  --model-root $root `
  --dataset "$root\dataset\test-00000-of-00001-9c204eb3f4e11791.parquet" `
  --sample-index 0
```

This command records the licensed source PNG and SHA, raw ground truth, official pixels `[1,3,1280,960]`, Encoder `[1,1200,1024]`, first-step logits `[1,1,57580]`, prompt/completion tokens, structured output, package versions, and one-run timing. It does not provide runtime output to DeploySharp. / 该脚本只生成可审计 Golden，不作为 DeploySharp 运行时代理。

Convert each ONNX graph to explicit FP32 OpenVINO IR without FP16 compression:

```powershell
& $python eng\models\document-understanding\scripts\convert_donut_openvino.py `
  --model-root $root
```

Run independent Python backend comparisons:

```powershell
& $python eng\models\document-understanding\scripts\run_donut_backends.py `
  --model-root $root --backend ort --model-directory onnx
& $python eng\models\document-understanding\scripts\run_donut_backends.py `
  --model-root $root --backend openvino --model-directory onnx
& $python eng\models\document-understanding\scripts\run_donut_backends.py `
  --model-root $root --backend openvino --model-directory openvino
```

Each comparison binds exact named ports, validates finite logits, performs greedy stable-tie generation, carries four layers of self/cross KV, and fails if the completion tokens or structured output differ from the pinned official Predictor. / 每条比较都按精确端口运行并验证 Logit、Greedy Tie、四层 KV、Token 与结构结果。

## Critical files / 关键文件

The ModelPack manifest lists every warehouse file. Critical executable and identity files are summarized here:

| File / 文件 | Bytes | SHA256 |
| --- | ---: | --- |
| `checkpoint/pytorch_model.bin` | 806,248,251 | `31b78e3d3891072de8e2bf3553b71782242a1f3b589b914ec2b03feff7b14c54` |
| `checkpoint/preprocessor_config.json` | 362 | `46a79191272663118d1d5d6f2eaf4c497bce40cc336bd55724daac33a34b250b` |
| `checkpoint/sentencepiece.bpe.model` | 1,296,245 | `cb9e3dce4c326195d08fc3dd0f7e2eee1da8595c847bf4c1a9c78b7a82d47e2d` |
| `checkpoint/tokenizer.json` | 4,021,441 | `756fd46f7c829153e68d75ebac3e59fda91244f11c85d3498fe91b20dc5cdf59` |
| `checkpoint/added_tokens.json` | 1,516 | `f51dd68d1565c8fb24de0a93f0a98aaed273ff368908069219b74b091bebcbc5` |
| `onnx/encoder_model.onnx` | 311,234,390 | `cb165bb59193c73c9097b1a306eb55f2e139e9ba0306755b405777c43cd51cbc` |
| `onnx/decoder_model.onnx` | 743,754,196 | `082b4c414f70be269a5c191c12373e3a19f1e5be10f8d01d6b64dd8c0a8259a3` |
| `onnx/decoder_with_past_model.onnx` | 710,151,560 | `e5629e9e13b19e494652d3f1f57136a093593e03e2fca58329e61e06b8fa323e` |
| `openvino/encoder_model.xml` / `.bin` | 8,659,621 / 308,752,852 | `bbb77bde...` / `3758443b...` |
| `openvino/decoder_model.xml` / `.bin` | 516,304 / 743,612,652 | `bb2e2923...` / `a5ceca96...` |
| `openvino/decoder_with_past_model.xml` / `.bin` | 462,661 / 710,025,444 | `07381dad...` / `b49461b7...` |
| `schema/cord-v2-donut-tags.schema.json` | 539 | `11eef36f495e1c3911961469a23d71b6f6edbe377e420990227514b5c8777733` |
| `evidence/cord-test-0/document.png` | 338,392 | `8612d04b70f430f3aef07fbbd5200e382dcc4152b344cc2eff9f735f05a257c8` |
| `evidence/cord-test-0/deploysharp-dotnet.json` | 6,963 | `5da746b6ea980bb6a19c96d626326d83ada96b0f6ec38f46700de9cfef870ba7` |

OpenVINO conversion evidence records each exact XML/BIN, conversion time, source opset, `compressToFp16:false`, and named ports. All other config, tokenizer, dataset, Golden, and backend evidence size/SHA values are in `eng/models/document-understanding/manifests/donut-base-finetuned-cord-v2.modelpack.json`. / 其余逐文件大小与 SHA 见结构化 Manifest。

## LayoutLMv3 and Pix2Struct blockers / LayoutLMv3 与 Pix2Struct Blocker

`E:\DeploySharp-Models\layoutlmv3-base` contains six small official files. Key SHAs are config `2b044b1a...`, processor `35fa5991...`, vocabulary `06b4d46c...`, and merges `1ce16647...`. The official base model is licensed CC-BY-NC-SA-4.0 and lacks the task head needed for real classification/entity extraction. The formerly expected Microsoft FUNSD repository was unavailable under the official namespace on the access date. No third-party fine-tune is substituted. / LayoutLMv3 仅保留官方 base 合同；缺任务头且许可证非商业，不以第三方微调替代。

`E:\DeploySharp-Models\pix2struct-docvqa-base` contains seven small official files. Key SHAs are config `8d399737...`, processor `c84e4eeb...`, SentencePiece `7fd65033...`, and tokenizer JSON `0af109b2...`. The contract records maximum 2048 flattened 16x16 patches with row/column coordinates and the T5 question template. The official config sets `text_config.use_cache=false`; without a complete official or traceable dynamic-patch Encoder/Prefill/Decode ONNX and OpenVINO bundle, Stage 27 does not download weights or invent past/present KV. / Pix2Struct 保留动态 Patch/T5 合同；官方禁用 Cache，缺完整导出时不下载权重或虚构 KV。

## DeploySharp gate and publication / DeploySharp 门控与发布

```powershell
$env:DEPLOYSHARP_DOCUMENT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_DOCUMENT_MODEL_ROOT = 'E:\DeploySharp-Models\donut-base-finetuned-cord-v2'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~Stage27DocumentUnderstandingExternalIntegrationTests
```

The gate runs OpenCV plus .NET ORT and OpenVINO IR and compares the same 53 completion tokens and exact structured JSON with the official Predictor. Successful execution remains `External` and `AlgorithmVerified:false`; it does not change licensing. / 门控通过也只证明本机执行与当前官方 Golden 一致，不自动升级 AlgorithmVerified 或许可状态。

ModelFactory publication remains blocked: `uploaded:false`, `downloadable:false`, `redistributionAllowed:false`, no immutable Release URI, and the official catalog is empty. Publishing later requires an independent redistribution grant, Notice/source review, exact sidecar review, immutable URI, catalog validation, and explicit user authorization. No Actions, Release, tag, commit, push, or TensorRT operation belongs to this procedure. / 上传下载均为 false，不创建伪 URL；Actions、Release、Tag、Commit、Push 与 TensorRT 均不属于本流程。
