# 模型 × 后端验证矩阵

本页是当前公开模型工件的后端验证总表。验证范围为 Windows x64；`✓` 表示该精确工件在对应后端完成加载、推理和结果解码，`△` 表示已有代码合同和已发布资产但尚无该后端的真实模型证据，`✗` 表示已尝试但当前后端不能完成该工件，`—` 表示没有适用工件或该格式不属于该后端。表格不把“可构建”或“存在适配器”当作推理通过。

模型目录中的 `External` 条目不纳入本表的通过统计。SAM2/SAM3 视频、Whisper 完整发布 Bundle、Donut 原生多页/TensorRT、BLIP VQA/BLIP-2/InstructBLIP、Qwen2.5-VL、Phi Vision、SigLIP 2，以及部分 LayoutLMv3/Pix2Struct 任务头仍处于合同或局部实验阶段，不能宣称全部后端可用。模型目录、下载边界和状态语义见[模型支持指南](articles/model-support.md)；可复现性能结果见[设备性能实测](articles/device-performance-benchmarks.md)。

## 当前工件矩阵

| 模型工件 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LLamaSharp |
|---|:---:|:---:|:---:|:---:|:---:|
| `yolo/v5/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v6/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v7/detect/base` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v9/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v10/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v12/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v13/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/classify/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v5/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/segment/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v9/segment/c` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `deim/v2/detect` | ✓ | — | ✗ | ✓ | — |
| `pp-yoloe/plus-crn-l` | ✓ | — | ✗ | ✓ | — |
| `rf-detr/detect` | ✓ | ✗ | ✗ | ✓ | — |
| `rf-detr/segment` | ✓ | ✗ | ✗ | ✓ | — |
| `rt-detr/r50vd-decoded-vector-ir` | — | ✓ | — | — | — |
| `rt-detr/r50vd-decoded-vector-onnx` | ✓ | — | ✗ | ✓ | — |
| `rt-detr/r50vd-raw-query` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/legacy-cls` | ✓ | ✓ | △ | △ | — |
| `paddleocr/ppocrv4/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/server-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/server-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/tiny-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/tiny-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/small-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/small-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/medium-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/medium-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `anomalib/padim/mvtec-bottle` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-1.4` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-2.0 (onnx.fp32)` | ✓ | — | ✗ | ✓ | — |
| `bria/rmbg-2.0 (onnx.dynamic-int8)` | ✓ | — | ✗ | ✗ | — |
| `llm/qwen2.5-0.5b-instruct-q4-k-m` | — | — | — | — | — |
| `vision-language/clip-vit-b-32` | — | — | — | — | — |
| `segmentation/sam-v1-vit-b` | — | — | — | — | — |
| `generative-vision-language/blip-caption-base` | — | — | — | — | — |

## PP-Structure 模型状态

PP-Structure 共收录 29 个官方模型合同，其中 28 个标准模型有独立 ONNX Release 资产；`PP-Chart2Table` 另以 `paddle-chart/pp-chart2table` 四图 ONNX + tokenizer Bundle 发布在同一个 `models-paddleocr` Release。Chart2Table 官方源包仍不是标准 `inference.json + inference.pdiparams`，因此单文件 Paddle archive 转换目录仍保留 `conversion-blocked`，这不影响已发布派生 Bundle 的下载和运行。下表按模型族分组，普通模型组内只要仍有未逐一执行的工件就保持 `△`；Chart2Table 则以真实完整流水线证据单独标记。PP-Structure 其它模型尚未全部逐一验证每个后端，不能把 Chart2Table 的结果外推到其它模型。

本机已有 `E:\Model\PaddleDocument\onnx-smoke.json` 和 `semantic-smoke-selected.json`：28 个已转换 ONNX 均完成 ONNX Runtime CPU 图级 smoke（加载、输入绑定和原始输出形状）。`PaddleDocumentSemanticIntegrationTests` 使用真实 `E:\Data\image\bus.jpg` 对已接入专用 Decoder 的工件执行语义 smoke；版面导出当前按官方 23 类标签解码，阈值 0 时 `pp-doclayout-l` 返回 300 个候选。`PaddleDocumentPipelineSemanticIntegrationTests` 还验证了真实 ORT CPU 方向→版面统一编排。UVDoc 另有同一 `bus.jpg`、同一 `[1,3,640,640]` 准备张量的 ORT/OpenVINO CPU 对照：两边均返回有限 `640x640x3` 张量，均值 `115.8434269/115.8434069`，最大/平均绝对差 `0.0730591/0.00176066`。这是可运行性和数值范围证据；当前输出存在有界数值差异，不能写成像素等价或视觉质量通过。完整记录见 [`uvdoc-ort-openvino-parity-20260924.json`](../eng/models/paddle-document/verification/uvdoc-ort-openvino-parity-20260924.json)。Paddle NMS Profile 已明确接受 `Int32` 和 `Int64` 计数，并按每张图的累计计数处理不等长 batch，避免把合法的 Paddle2ONNX 类型差异误判为后端失败。TensorRT 现已有 `PaddleDocumentTensorRtExternalIntegrationTests` 外部入口；本机已确认 `D:\Program Files\TensorRT-11.0.0.114-cu12`、CUDA 12.9、cuDNN 9.22 和 TRT 11 bridge 可以共同加载，并完成 PP-LCNet 文档方向、PP-LCNet 表格分类以及 `pp-doclayout-l` 的 ONNX→Engine→推理 smoke。此前的失败来自 TensorRT 根目录、bridge API 和动态 profile 未显式配置，不是缺少 vendor DLL。

UVDoc OpenCV DNN 精确组合已尝试但失败：OpenCV 5.0 importer 在 `PaddingLayerImpl::forward` 抛出 `inputs[0].dims == 4`，矩阵标记为 `✗`。TensorRT 11 + DeploySharp Provider/decoder 已从同一 ONNX 构建并加载 engine，DisableTf32 对照输出 max/mean abs diff `0.0778809/0.00172731`，输出 `640x640x3`；该记录证明 TensorRT engine 可执行且存在有界数值差异，不代表像素等价或视觉质量通过。GPU trtexec mean/P95 `8.55564/9.9389 ms`。

| 模块 | 代码合同 | 独立 ONNX Release | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| 文档方向：`paddle-doc/pp-lcnet-x1-0-doc-ori` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 文档矫正：`paddle-doc/uvdoc` | ✓ | ✓ | ✓ | ✓ | ✗ | △ |
| 版面分析：`paddle-doc/pp-doclayout-l` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓† |
| 版面分析：`paddle-doc/pp-doclayout-plus-l`、`pp-doclayout-m`、`pp-doclayout-s`、`pp-docblocklayout` | ✓ | ✓ | ✓ | △ | △ | △ |
| 版面分析：`paddle-doc/picodet-layout-1x`、`picodet-layout-1x-table`、`picodet-s-layout-3cls`、`picodet-l-layout-3cls`、`rt-detr-h-layout-3cls` | ✓ | ✓ | ✓ | △ | △ | △ |
| 版面分析：`paddle-doc/picodet-s-layout-17cls`、`picodet-l-layout-17cls`、`rt-detr-h-layout-17cls` | ✓ | ✓ | ✓ | △ | △ | △ |
| 表格结构（原始 Release ONNX）：`paddle-table/slanext-wired`、`paddle-table/slanext-wireless` | ✓ | ✓ | ✓ | ✗ | △ | △ |
| 表格结构（本地 alpha-renamed 兼容图）：`slanext-wired`、`slanext-wireless` | ✓ | — | ✓ | ✓** | △ | △ |
| 表格分类：`paddle-table/pp-lcnet-x1-0-table-cls` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓‡ |
| 表格单元格：`paddle-table/rt-detr-l-wired-cell-det`、`rt-detr-l-wireless-cell-det` | ✓ | ✓ | ✓ | △ | △ | △ |
| 公式：`paddle-formula/pp-formulanet-plus-s/m/l`、`pp-formulanet-s/l`、`unimernet` | ✓ | ✓ | ✓ | △ | △ | △ |
| 印章：`paddle-seal/ppocrv4-mobile` | ✓ | ✓ | ✓ | ✓ | △ | ✓§ |
| 印章：`paddle-seal/ppocrv4-server` | ✓ | ✓ | ✓ | ✓ | △ | ✓§ |
| 图表（四图生成 Bundle）：`paddle-chart/pp-chart2table` | ✓ | ✓ | ✓ | ✓ | △ | ✓¶ |

### PP-Chart2Table 四图生成 Bundle 验证

验证复用了现有官方 checkpoint 和示例图片，没有重复下载。四张图分别是 Vision/Projector、动态 Token Embedding、固定 286-token 官方 Prompt Prefill（含 `lm_head` 和 KV）、单 token 动态 past Decode（含 `lm_head` 和 KV）。Tokenizer 直接读取官方 `qwen.tiktoken`、`tokenizer_config.json`、`added_tokens.json`；Prompt 为 286 token，含 256 个连续 `<imgpad>`。

ORT CPU、OpenVINO CPU 与 TensorRT CUDA 在同一官方图片上均运行到 EOS，返回 141 个 token（含 EOS `151645`）及相同的 2018–2023 六行表格。此前三 token ORT 参考相对 Paddle 的逐步最大 logit 误差为 `4.29e-5`、`4.82e-5`、`6.77e-5`；三个后端的完整表格文本哈希相同。OpenCV/Pillow-compatible 输入与官方 PaddleX 图像处理器最大绝对差为 `0.01501`、平均绝对差 `2.31e-7`。原始 TensorRT 通用 host-KV 路径耗时 `83.42 s`；加入几何缓冲区复用、显存驻留 KV ping-pong、精简动态 shape/binding 调用，并使用 FP32+TF32 语言 plans 后，TensorRT device 专用路径观测为 `12.64/12.12/10.12 s`，最好一次 140 个 Decode 步骤 P50/P95 `66.47/73.48 ms`。ORT CPU 为 `39.76 s`、Decode P50/P95 `94.69/123.92 ms`；OpenVINO CPU 为 `46.40 s`、`146.63/197.52 ms`。测试设备为 Ryzen 7 5800H / Windows 11 / .NET 10 + RTX 3060 Laptop / TensorRT 10.11 / CUDA 12.9 / cuDNN 9.22。未锁定 GPU 时钟，也没有完整负载下时钟轨迹；结果是单样例观测，不是受控多轮基准或数据集精度结论。严格 FP32 host-cache TensorRT 路径仍可通过原 `PaddleChart2TableOnnxSession` 使用；优化路径用 `PaddleChart2TableTensorRtDeviceSession`。详见验证 JSON。

TensorRT 设备为 RTX 3060 Laptop 6GB、TensorRT 10.11.0、CUDA 12.9、cuDNN 9.22。四图使用 FP16 Vision/Embedding 与 FP32 Prefill/Decode；FP16 文本图曾产生全零 `lm_head` logits，改用 FP32 后正确生成。之前的空 Engine 是回归测试使用了错误 Decoder 图和不匹配的 attention-mask profile（past KV optimum 为 512 时 mask 应为 513），不是 `TensorRtOnnxEngineBuilder` 的通用限制。选用与 Release 一致的 epsilon 图、修正 mask profile 后，库 Builder 成功创建动态 KV Decode Engine；库 Builder 创建的四张 plan 通过完整 EOS 生成回归。三后端完整生成耗时与 P50/P95 已记录在上一段。该设备未锁定 GPU 时钟；测试仅一张图、单次运行，不可视为泛化性能结论。OpenCV DNN 自回归流水线和数据集级准确率仍未验证。精确合同、plan 哈希与全部阶段数值见 [`chart2table-component-validation.json`](../eng/models/paddle-document/verification/chart2table-component-validation.json)。

### 已完成的精确 PP-Structure Decoder 语义 smoke

| 精确工件 | DeploySharp Decoder 结果 | ORT CPU | OpenVINO CPU |
|---|---|:---:|:---:|
| `paddle-doc/pp-lcnet-x1-0-doc-ori` | 官方 `img_rot180_demo.jpg`：180°，score 约 `0.89236`，不重复 Softmax | ✓ | ✓ |
| `paddle-doc/pp-doclayout-l` | Paddle NMS 区域结果：300 个候选（阈值 0），阈值 0.5 时 1 个区域 | ✓ | ✓ |
| `paddle-doc/uvdoc` | 矫正张量：`640x640x3`，全部有限值 | ✓ | ✓ |
| `paddle-table/pp-lcnet-x1-0-table-cls` | 官方 `table_recognition.jpg`：`wired_table`，score 约 `0.844209` | ✓ | ✓ |
| `paddle-table/rt-detr-l-wired-cell-det` | Paddle NMS 单元格结果：300 个区域 | ✓ | ✓ |
| `paddle-table/slanext-wired` | 结构序列：专用表格 Decoder；派生兼容图通过 OpenVINO | ✓ | ✓** |
| `paddle-table/slanext-wireless` | 结构序列：专用表格 Decoder；派生兼容图通过 OpenVINO | ✓ | ✓** |
| `paddle-seal/ppocrv4-mobile` | 概率图掩码：`224x224`，专用印章 Decoder | ✓ | ✓ |
| `paddle-seal/ppocrv4-server` | 概率图掩码：`224x224`，专用印章 Decoder | ✓ | ✓ |

### TensorRT 精确工件证据

| 精确工件 | Engine / 运行时 | 一致性结果 | 稳态耗时 |
|---|---|---|---:|
| `paddle-table/pp-lcnet-x1-0-table-cls` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；静态 `x=[1,3,224,224]`；Engine SHA `c9464c22f5d1d11ac4e2f35fb3d437a782278d9a85f697bdd3f8ef050ad065f2`（7,350,700 bytes） | ORT `wired_table` / `0.844208`，TensorRT `wired_table` / `0.851693`，绝对分数差 `0.007485` ≤ `0.01` | P50 `1.212 ms` / P95 `1.331 ms` |
| `paddle-doc/pp-doclayout-l` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态 `image`、`im_shape`、`scale_factor` profile；Engine SHA `c6acaedc9f3ed2996e012d1fd6b45c5eb061877d2ac3aa0f77bde34f65e9a0ba` | 300 个候选中 9 个 score ≥ 0.05；与 ORT CPU 的高置信度 score 误差 ≤ 0.01、源坐标误差 ≤ 1.5 px | P50 `19.414 ms` / P95 `25.876 ms` |
| `paddle-seal/ppocrv4-mobile` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态输入 profile 固定为 `image=[1,3,224,224]`；Engine SHA `13772da4a34bd9fb3ea1e1c3cfedabf6a2fb1aace0762c6e5253904e374c9953`（7,045,964 bytes） | `demo_1.jpg` 上 ORT/TensorRT 均返回 `224x224` 掩码、区域数 `0/0`；原始掩码最大/平均绝对误差 `9.42e-8`/`1.36e-8`；该图片无印章，仅作执行和输出一致性 | P50 `3.710 ms` / P95 `4.558 ms` |
| `paddle-seal/ppocrv4-server` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态输入 profile 固定为 `image=[1,3,224,224]`；显式 `DisableTf32=true`；Engine SHA `d89b4c5cbfde1707bd486007bdf65afe481e3fcdb85c1cdf11d7c98f88332d03`（140,877,596 bytes） | `demo_1.jpg` 上 ORT/TensorRT 均返回 1 个区域；掩码平均绝对误差 `2.4136e-6`，最大绝对误差 `8.6451e-4`，逐元素误差通过当前合同 | P50 `12.221 ms` / P95 `24.648 ms` |

上述 TensorRT 证据均使用 5 次预热、50 次测量；表格分类由 TensorRT CUDA 预处理、推理和分类 Decoder 完成，`pp-doclayout-l` 使用 OpenCV 在 CPU 上准备输入并由 TensorRT 负责模型执行和图内 Paddle NMS，印章 mobile/server 使用 OpenCV 在 CPU 上准备输入并由 TensorRT 执行概率图和专用 Decoder。server 印章在 TRT11 强类型网络下显式关闭 TF32，当前样本的原始概率图逐元素误差通过测试合同；这仍不替代数据集级精度评估。所有结果只对表中精确工件成立，不外推到同组其它模型。Engine 与 TensorRT/CUDA/cuDNN 版本及 GPU 架构绑定，换设备或运行时后必须重新构建并验证。

公式语义解码的精确证据：`FormulaExportsDecodeWithOfficialTokenizerOnRealOrtCpu` 对 6 个公式工件逐一使用官方 `general_formula_rec_001.png`、专用去白边/缩放/归一化和各自官方 BPE tokenizer 运行。最新结果为：Plus-S `1022/1226`、Plus-M `2/6`、Plus-L `49/62`、FormulaNet-S `1023/1036`、FormulaNet-L `31/37`、UniMERNet `1022/1252`（分别为 token 数/LaTeX 字符数），全部 warnings=0 并遇到 EOS。Plus-S/M/L 的 LaTeX 忽略排版空白后与官方示例一致；FormulaNet-S/L、UniMERNet 仍有符号或格式差异。原先 `bus.jpg` 的输出长度只算旧 smoke，不能视为公式精度。当前 ORT `✓` 表示精确工件可运行，不代表数据集准确率通过。

ORT 的 PP-Structure 语义 smoke 由 `tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentSemanticIntegrationTests.cs` 完成，方向→版面真实统一编排由 `PaddleDocumentPipelineSemanticIntegrationTests.cs` 完成；OpenVINO 的代表性模块和 SLANeXt 派生兼容图由 `PaddleDocumentOpenVinoSemanticIntegrationTests.cs` 完成；OpenCV DNN 的 `pp-doclayout-l` 由 `PaddleDocumentOpenCvSemanticIntegrationTests.cs` 完成，采用单 batch、无 `bbox_num` 输出合同并返回有效版面区域；TensorRT 的方向、表格分类、版面和 mobile/server 印章精确工件由 `PaddleDocumentTensorRtExternalIntegrationTests.cs` 与 `PaddleDocumentTensorRtServerSealExternalIntegrationTests.cs` 完成 ORT 对照和 P50/P95 测量。测试在 Windows x64 执行：版面/矫正沿用 `bus.jpg`，印章使用 `E:\Data\ocr\demo_1.jpg`，分类、表格结构和公式已切换到官方对应任务示例；SLANeXt 两个模型均返回 24 tokens、13 个单元格。原始 SLANeXt Release 图仍会触发 OpenVINO `Loop` importer 阻断；`normalize_loop_parameters.py` 仅对循环体形式参数做 alpha-renaming，生成的 wired/wireless 派生图已经与原始 ORT 输出逐元素（容差 0.001）及表格 Decoder 结果对齐。上述结果是可复现的精确工件语义证据，不代表其余 PP-Structure 工件或 OpenCV DNN/TensorRT 已全部通过。

## 阅读规则

补充说明：上一版记录中的 OpenCV/TensorRT 阻断描述已经过复核。当前 OpenCV DNN 的 `pp-doclayout-l` 单 batch 测试已通过（不请求被 importer 裁掉的整型 `bbox_num` 输出）；TensorRT 已在本机 TRT 11 + CUDA 12.9 + cuDNN 9.22 组合下完成 PP-LCNet 文档方向、PP-LCNet 表格分类、`pp-doclayout-l` 和 mobile/server 印章的 ONNX→Engine→推理 smoke。其余工件仍按表格中的精确证据状态处理。

- 通过只对表中精确工件成立；更换导出文件、输入尺寸、引擎或运行时版本后需要重新验证。
- TensorRT 列仅表示 CUDA 引擎路径通过；引擎必须与本机 TensorRT/CUDA 版本和输入 profile 匹配。
- PaddleOCR 的单图完整流水线、batch 和并发通道组合单独记录在[设备性能实测](articles/device-performance-benchmarks.md)；这里的单元格只表达阶段工件是否可执行。
- OpenCV DNN 的 `✗` 是当前 importer、动态 shape 或辅助输入限制，不代表其他后端的结果。
- `✓**` 表示使用 `eng/models/paddle-document/scripts/normalize_loop_parameters.py` 生成的派生 ONNX；原始 Release 文件保持未修改，派生文件必须使用自己的 SHA-256 注册。原始 SLANeXt Release 图在当前 OpenVINO `Loop` importer 下标记为 `✗`，不能直接部署。
- `✓†` 表示精确工件 `pp-doclayout-l` 已完成 TensorRT 11 动态输入和图内 Paddle NMS 推理，并与 ORT CPU 结果比较；输入预处理仍在 CPU，不能解释为 CUDA 端到端流水线。
- `✓‡` 表示精确工件 `pp-lcnet-x1-0-table-cls` 已完成 TensorRT 11 静态输入、CUDA 预处理、分类 Decoder 和 ORT 标签/置信度一致性比较；置信度允许明确记录的绝对误差 `0.01`，不能外推为数据集精度结论。
- `✓§` 表示精确工件 `ppocrv4-mobile/server-seal-det` 已完成 TensorRT 11 概率图推理、印章 Decoder 和 ORT 尺寸/区域数一致性比较；server 构建显式关闭 TF32，当前样本的原始掩码逐元素误差通过合同，但不能外推为数据集召回率。
- `✓¶` 表示 Chart2Table 四图 Bundle 在 ORT CPU、OpenVINO CPU 和 TensorRT CUDA 均有完整 EOS 文本证据；TensorRT 四张 plan 已由 DeploySharp `TensorRtOnnxEngineBuilder` 构建并完成完整生成回归。此符号仍不表示数据集级准确率验证。
- `△` 表示存在适用的后端执行合同，但本机尚无该精确组合的真实运行证据；部署时应先在目标设备完成 smoke test。
- `✗` 表示已尝试并确认当前精确组合不支持或无法运行；原始 SLANeXt 图在当前 OpenVINO `Loop` importer 下即属此类。只有尚无真实执行证据（包括 Chart2Table/OpenCV DNN 自回归）时应标 `△`，不能将“未验证”写成“已证实失败”。此状态不会被相近模型或派生图的结果覆盖。
- 未验证的模型不再使用 `—` 混淆“未验证”和“不适用”；不会用相近模型、脚本退出码或合同测试代替真实推理证据。

## PaddleOCR 完整流水线证据

当前已完成真实 `det → crop → cls/orientation → rec → merge` 的核心组合包括：PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 组。`PaddleOcrAllCorePipelineOrtIntegrationTests` 在 Windows x64、`E:\Data\ocr\demo_1.jpg`、ONNX Runtime CPU 上逐组返回 16 个区域且全部产生识别结果；单次冷启动端到端耗时为 v4 mobile `676.431 ms`、v4 server `2840.955 ms`、v5 mobile `911.419 ms`、v5 server `1890.864 ms`、v6 tiny `356.106 ms`、v6 small `1044.636 ms`、v6 medium `2286.589 ms`。v6 没有独立 CLS 归档，测试明确复用 PP-OCRv5 mobile text-line CLS。OpenCV DNN 七组短诊断的机器可读结果见 [`paddleocr-core-opencv-20260924.json`](../eng/models/paddle-ocr/verification/paddleocr-core-opencv-20260924.json)。

PP-OCRv5 mobile 的真实 OpenCV DNN 全流程已与 ORT 在同一准备张量上复核：原先记录的 8/16 差异来自外部测试合同把全分辨率概率图错误声明为 `[1,1,128,-1]`，OpenCV 因而把相同元素数重解释成 `[1,1,128,2048]`；不是 OpenCV importer 或 DB 解码器漏检。合同改为从输入张量绑定输出高度后，两边均返回 16 个区域，逐区域识别文本一致，坐标误差 ≤0.5 px、分数误差 ≤0.001；原始概率图最大/平均绝对差 `4.2915344e-5` / `8.6187186e-8`。输入张量 SHA-256 为 `dbbdb3938fa7a880aec0403f74d064e125826238abf16b12c6d5d58b0612f553`，最终输出 SHA-256 为 OpenCV `dd55ffdab9b595f016083c32ac53b61f964d378958409825473d65b6b4d798c1`、ORT `bcd67a98e08ee65c6fd51dd49f50a1bb3828cbc4037a57d73af64ddba416a2eb`。两次单次 OpenCV `pipeline.Run` 观察分别为 `5655.803 ms` 和 `4117.808 ms`，包含裁剪、分类、识别等流水线开销且不是稳定性能基准；此结果不替代其它后端的逐工件性能基线，也不能外推到其它 OCR 模型。

上述 7 组覆盖当前本机全部 DET/REC 核心工件；v4/v5 的每个 CLS 工件也已分别完成阶段级验证，v6 的 CLS 复用策略在测试和目录中显式记录。`PaddleOcrAllCorePipelineOpenVinoIntegrationTests` 已在 `demo_1.jpg` 上对 7 组完成真实 `det → crop → cls → rec → merge`，新增三图入口又在 `demo_1.jpg`、`demo_2.jpg`、`demo_3.jpg` 上重复跑通 7 组 OpenVINO CPU 流水线；这是一条多图片执行覆盖证据，不把 ORT 结果自动外推到 OpenVINO。OpenCV DNN 的 v4 server、v5 mobile/server 全流程和 TensorRT 的 v4/v5 server 全流程已有精确工件证据；v4 server 重新构建 mobile CLS engine 后，DeploySharp bridge det/rec/CLS 三个 engine 均能加载并推理。v4 server TensorRT `demo_1.jpg`、batch 8、2 个阶段 Session 的 P50/P95 为 `125.254/132.029 ms`；v5 server 带遥测结果为 `145.284/156.248 ms`。OpenCV 这三组本轮使用同一 `demo_1.jpg`、单 channel、batch 1 的诊断协议，耗时分别为 v4 server P50/P95 `6655.278/7877.100 ms`、v5 server `5087.234/5557.888 ms`；这组 CPU 数值不能与 TensorRT 的 GPU 记录直接排序。`pp-doclayout-l` 已有 TensorRT NMS 级实测，但不能把它自动外推到其它 PP-Structure 模型。PP-Structure 目前已具备模块级合同、专用 Decoder、按需模型资产、统一编排层和 TensorRT 外部验证入口；真实多模型、多后端文档智能端到端证据仍待各运行时完成。

本轮补充的本机证据：`E:\Model\paddleocr\paddle-ocr-onnx-smoke.json` 对 17 个核心 ONNX 图均记录了 ONNX Runtime CPU 图级 smoke；`PaddleOcrAllCorePipelineOrtIntegrationTests` 对 7 组完整流水线完成真实 ORT CPU 运行；新增 `PaddleOcrAllCorePipelineOpenVinoIntegrationTests` 对同样 7 组完成真实 OpenVINO CPU `det → crop → cls → rec → merge` 运行；阶段 19/20 集成测试补测了 PP-OCRv5 mobile/server 的 ORT CPU、OpenVINO CPU 以及 v4 legacy/v5 mobile/server CLS；基准工具在同一 `demo_1.jpg` 上补测了 OpenCV DNN 的 v4 mobile/server、v5 mobile/server、v6 tiny/small/medium 七组完整流水线，均返回 16 个区域并产生识别结果。测试使用的模型路径已经统一为 `E:\Model\paddleocr\PP-OCRv4`、`PP-OCRv5`、`PP-OCRv6`，不再依赖旧的 `E:\Model\ocr` 路径。
