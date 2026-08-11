# PaddleOCR, Anomalib, and BRIA RMBG / PaddleOCR、Anomalib 与 BRIA RMBG

Stage 19 adds artifact-bound execution contracts for `PaddleOcrDet`, `PaddleOcrRec`, `AnomalibSeg`, and `BriaRmbg` in the existing `JYPPX.DeploySharp.Visual` package. Image preparation remains optional in `JYPPX.DeploySharp.Visual.OpenCV`; ONNX Runtime and OpenVINO receive only named Core tensors. No vendor-specific Visual package, model, dictionary, image, native runtime, or TensorRT asset is bundled. / 阶段 19 在现有 `JYPPX.DeploySharp.Visual` 包中增加 `PaddleOcrDet`、`PaddleOcrRec`、`AnomalibSeg` 与 `BriaRmbg` 的工件绑定执行合同。图像准备继续作为 `JYPPX.DeploySharp.Visual.OpenCV` 的可选能力；ONNX Runtime 与 OpenVINO 只接收具名 Core tensor。不打包厂商专用 Visual 包、模型、字典、图片、native runtime 或 TensorRT 资产。

## Quick start / 快速开始

Profiles bind the exact artifact SHA256, opset, named tensors, exporter/source revision, preprocessing/postprocessing version, dictionary identity, license evidence, and bounded decode budgets. The application supplies the external files and native runtime packages. / Profile 绑定精确工件 SHA256、opset、具名 tensor、导出器/源码 revision、前后处理版本、字典身份、许可证证据和有界解码预算。外部文件与 native runtime 包由应用显式提供。

```csharp
OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(
    dictionaryPath, "external.ppocrv5", "v5", true, dictionarySha256);

PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(
    new ModelId("external/ppocrv5-mobile-det"), detectorArtifactContract);
PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(
    new ModelId("external/ppocrv5-mobile-rec"), recognizerArtifactContract, characters);

OpenCvPreprocessOptions detectorOptions =
    OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize);
```

The complete package-only example is `tests/clean-consumer/visual-ocr-anomaly-rmbg`. It prints `DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_SKIP` when required external files or native runtimes are absent and prints `DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_OK` only after real OCR detection plus recognition, anomaly inference, and alpha inference complete. / 完整的仅包示例位于 `tests/clean-consumer/visual-ocr-anomaly-rmbg`。缺少外部文件或 native runtime 时打印 `DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_SKIP`；只有真实 OCR 检测加识别、异常推理和 alpha 推理全部完成后才打印 `DEPLOYSHARP_VISUAL_OCR_ANOMALY_CONSUMER_OK`。

## Exact contracts / 精确合同

| Family / 模型族 | Named input / 具名输入 | Named output and semantics / 具名输出与语义 | Local CPU evidence / 本机 CPU 证据 |
| --- | --- | --- | --- |
| PP-OCRv5 mobile/server detection | `x:float32[1,3,H,W]`; BGR, stride-32 resize, ImageNet normalization | `fetch_name_0:float32[1,1,H,W]`; DB probability map, not boxes | Both ORT; mobile OpenVINO |
| PP-OCRv5 mobile/server recognition | `x:float32[B,3,48,W]`; BGR, keep-ratio crop, `(byte-127.5)/127.5` | `fetch_name_0:float32[B,T,18385]`; probabilities, class 0 blank, 18,384 dictionary/space tokens | Both ORT; mobile OpenVINO |
| Anomalib PaDiM/PatchCore | `input:float32[1,3,256,256]`; RGB `/255`; exported graph owns its remaining transform | `pred_score`, `pred_label`, `anomaly_map`, `pred_mask`; score/map are decoded into owned source-space anomaly results | Both ORT; PaDiM OpenVINO |
| BRIA RMBG 1.4 | `input:float32[1,3,1024,1024]`; RGB `(byte-127.5)/255` | `output:float32[1,1,1024,1024]`; probability alpha | ORT and OpenVINO |
| BRIA RMBG 2.0 local candidates | `pixel_values:float32[1,3,H,W]`; artifact-bound RGB `(byte-127.5)/127.5` | `alphas:float32[1,1,H,W]`; probability alpha | FP32 and dynamic-quantized ORT at 1024 |

The RMBG 2.0 metadata is spatially dynamic, but both inspected local ONNX artifacts fail an internal attention reshape at 256 and pass at 1024. DeploySharp records this as artifact evidence and does not claim arbitrary dynamic-size support. / RMBG 2.0 元数据声明空间动态，但两个已检查本地 ONNX 在 256 尺寸命中内部 attention reshape 失败，在 1024 尺寸通过。DeploySharp 将其记录为工件证据，不宣称支持任意动态尺寸。

## Decode fidelity / 解码保真

`PaddleDbTextDetectionDecoder` accepts only the declared probability map. It applies the profile's bitmap threshold, bounded connected candidates, fast rectangle or slow active-pixel scoring, box threshold, unclip ratio, deterministic reading order, quadrilateral/polygon ownership, and `ImageTransform` source restoration. The managed implementation is contract-compatible but does not claim pixel-identical parity with Paddle's OpenCV contour plus pyclipper path until an official golden suite is admitted. / `PaddleDbTextDetectionDecoder` 只接受声明的概率图。它应用 Profile 的位图阈值、有界连通候选、fast 矩形或 slow 活跃像素评分、box threshold、unclip ratio、确定性阅读顺序、四边形/多边形所有权以及 `ImageTransform` 源图恢复。托管实现满足合同，但在正式准入官方 golden 套件前，不宣称与 Paddle 的 OpenCV contour 加 pyclipper 路径逐像素一致。

`GreedyCtcDecoder` uses the artifact-bound dictionary, blank index zero, repeat collapse, blank removal, emitted-token provenance, and mean emitted confidence. Dictionary lines are non-empty Unicode tokens, not necessarily one scalar: PP-OCRv5 includes multi-scalar flag tokens. Attention decoders are not inferred from tensor rank. / `GreedyCtcDecoder` 使用工件绑定字典、blank 索引 0、重复折叠、blank 删除、发射 token 来源和已发射置信度均值。字典每行是非空 Unicode token，并不要求只有一个标量：PP-OCRv5 含多标量旗帜 token。不会根据 tensor rank 猜测 attention decoder。

Anomalib's image score and pixel map remain distinct. The four-output decoder validates the Boolean label/mask ports, decodes the probability map with no second normalization, applies the configured threshold, restores it with bilinear half-pixel sampling, and owns both the returned float map and binary mask. Dataset category, checkpoint training state, tiling policy, normalization statistics, and model thresholds remain artifact/checkpoint provenance and cannot be guessed from an ONNX graph. / Anomalib 的图像分数与像素图保持独立。四输出 Decoder 验证 Boolean label/mask 端口，不进行二次归一化，应用配置阈值，以双线性 half-pixel 采样恢复源图，并拥有返回的 float map 与二值 mask。数据集类别、checkpoint 训练状态、tiling 策略、归一化统计和模型阈值属于工件/checkpoint 来源信息，不能从 ONNX 图猜测。

BRIA outputs are semantic alpha probabilities, not categorical semantic-segmentation labels. `AlphaMattingDecoder` validates finite values in `[0,1]`, performs bilinear source restoration, and returns an owned `AlphaMask` that supports deterministic SHA256 and RGB background composition. No extra matting refinement is present in these inspected artifacts. RGBA input is explicitly reduced to RGB by the selected `OpenCvAlphaMode`; input alpha is not silently reused as output alpha. / BRIA 输出是语义 alpha 概率，不是离散语义分割标签。`AlphaMattingDecoder` 验证 `[0,1]` 内有限值，执行双线性源图恢复，并返回支持确定性 SHA256 与 RGB 背景合成的自有 `AlphaMask`。已检查工件不包含额外 matting refinement。RGBA 输入按所选 `OpenCvAlphaMode` 显式转换为 RGB，不会把输入 alpha 静默复用为输出 alpha。

## Backend parity and diagnostics / 后端对齐与诊断

On 2026-08-07, the authorized input `E:\Data\image\bus.jpg` had SHA256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`. The real ORT/OpenVINO field-level test compared eight OCR regions, region order, source indexes, vertices, detector scores, text, CTC token classes and confidence; anomaly image score, restored map and binary mask; and restored alpha pixels. OCR score tolerance was `0.001`, coordinate tolerance `0.25` source pixel, anomaly-map tolerance `0.001`, and alpha tolerance `0.001`. Observed maximum anomaly-map error was `5.9604645e-7`; observed maximum alpha error was `8.46386e-6`. / 2026-08-07 的授权输入 `E:\Data\image\bus.jpg` SHA256 如上。真实 ORT/OpenVINO 字段级测试比较了八个 OCR 区域、区域顺序、源索引、顶点、检测分数、文本、CTC token 类别和置信度；异常图像分数、恢复后的像素图和二值 mask；以及恢复后的 alpha 像素。OCR 分数容差为 `0.001`，源图坐标容差为 `0.25` 像素，异常图与 alpha 容差均为 `0.001`。实际异常图最大误差为 `5.9604645e-7`，alpha 最大误差为 `8.46386e-6`。

Canonical result hashes can differ across backends when finite floating-point values differ inside tolerance; admission compares declared fields and tolerances rather than treating hash equality as a numeric-parity rule. Test output records one-run timing only and is not a P50/P95, throughput, memory, or cross-machine performance claim. / 有限浮点值在容差内不同时，跨后端规范结果哈希可以不同；准入比较声明字段与容差，而不是把哈希相等当作数值一致规则。测试输出只记录单次时间，不构成 P50/P95、吞吐、内存或跨机器性能结论。

Missing names, element types, shapes, non-finite values, out-of-range probabilities, dictionary SHA mismatches, resource-budget excess, dynamic reshape failures, unavailable native runtimes, cancellation, timeout, and disposed-session access map to stable DeploySharp diagnostics. OpenVINO consumes the ONNX directly for the verified cases; no unverified XML/BIN sidecar or alias guess is generated. / 缺失名称、元素类型、shape、非有限值、越界概率、字典 SHA 不匹配、资源预算超限、动态 reshape 失败、native runtime 缺失、取消、超时和已释放 session 访问均映射到稳定 DeploySharp 诊断。已验证路径由 OpenVINO 直接消费 ONNX；不会生成未经核验的 XML/BIN sidecar 或猜测端口别名。

## TFM, RID, and native ownership / TFM、RID 与 native 所有权

| Component / 组件 | Managed TFM surface / 托管 TFM 面 | Native/runtime responsibility / native/runtime 责任 |
| --- | --- | --- |
| `JYPPX.DeploySharp.Visual` | Repository library matrix, including `net46`, `netcoreapp3.1`, and `net10.0` gate builds | None |
| `JYPPX.DeploySharp.Visual.OpenCV` | `net46` through `net481`, `netcoreapp3.1`, and `net5.0` through `net10.0` | Application selects a matching `JYPPX.OpenCV.runtime.<rid>`; verified here on Windows x64 |
| ONNX Runtime backend | `netstandard2.0`, `net8.0` managed adapter | Application installs an official matching `Microsoft.ML.OnnxRuntime` runtime package; verified CPU on Windows x64 |
| OpenVINO backend | `net46` through `net481`, `netcoreapp3.1`, and `net5.0` through `net10.0` | Application installs the matching OpenVINO runtime/device plugin; verified CPU on Windows x64 |

NuGet packages contain only declared managed DLL/XML assets, the English repository README and logo. They do not contain models, dictionaries, images, Python, native runtimes, or TensorRT. / NuGet 包只包含声明的 managed DLL/XML、英文仓库 README 与 logo，不包含模型、字典、图片、Python、native runtime 或 TensorRT。

## Provenance and admission / 来源与准入

The official PaddleOCR source snapshot is [PaddlePaddle/PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) commit `2661c7c0ef5c613e8f93c6e93b2e052399f0f854` (2026-07-22). It declares Apache-2.0 and provides PP-OCRv5 DB thresholds, BGR decode, ImageNet normalization, stride-32 resize, CTC decoding, dictionary path, space-class setting, and height-48 recognition configuration. The official Anomalib snapshot is [open-edge-platform/anomalib](https://github.com/open-edge-platform/anomalib) commit `ffde4cce3db38964f9cf627b524dd325401c6107` (2026-08-07); it declares Apache-2.0 and its exporter derives output names from the inference result, defaults ONNX to opset 14, and makes fixed versus dynamic input size explicit. / 官方 PaddleOCR 源码快照为上述 commit，声明 Apache-2.0，并提供 PP-OCRv5 DB 阈值、BGR 解码、ImageNet 归一化、stride-32 resize、CTC 解码、字典路径、空格类别设置和高度 48 的识别配置。官方 Anomalib 快照为上述 commit，声明 Apache-2.0；其 exporter 从推理结果派生输出名，ONNX 默认 opset 14，并显式区分固定与动态输入尺寸。

BRIA references the official [RMBG-1.4 model card](https://huggingface.co/briaai/RMBG-1.4) and [RMBG-2.0 model card](https://huggingface.co/briaai/RMBG-2.0). Exact local ONNX export provenance, checkpoint terms, processor revision, and redistribution authorization are not independently proven. Therefore every manifest under `eng/models/ocr-anomaly-rmbg/manifests` remains `External`, uses `redistributionAllowed:false`, and is excluded from the empty official catalog. Local execution is `ContractVerified + LocalBackendVerified`, not `AlgorithmVerified`. / BRIA 引用官方 RMBG-1.4 与 RMBG-2.0 模型卡。精确本地 ONNX 导出来源、checkpoint 条款、processor revision 与再分发授权尚未独立证明。因此 `eng/models/ocr-anomaly-rmbg/manifests` 下全部清单保持 `External`、`redistributionAllowed:false`，不进入空的官方 catalog。本地执行是 `ContractVerified + LocalBackendVerified`，不是 `AlgorithmVerified`。
