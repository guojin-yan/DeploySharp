# 模型支持指南

本页说明 DeploySharp 当前模型目录的公开边界。模型支持分为“已收录 Preview”“可执行但未收录”和“暂不支持”三类。`Preview` 表示已有可复现的 ModelPack 和发布身份，不代表所有后端、所有设备或算法精度均已完成验证。

## 状态定义

| 状态 | 含义 |
| --- | --- |
| `ContractVerified` | Profile、输入输出张量和确定性解码合同已通过测试。 |
| `LocalBackendVerified` | 精确工件已在声明的本机后端和设备上完成真实推理。 |
| `Preview` | ModelPack 具有固定版本、大小、SHA-256 和发布身份，可按目录规则获取。 |
| `External` | 仅保留合同或外部工件记录，不属于可下载的官方目录。 |
| `Planned` | 已有任务方向，但当前版本没有可交付的完整工件或流水线。 |

## 已收录 Preview

| 模型族 | 当前条目 | 主要任务 | 说明 |
| --- | --- | --- |
| YOLO | v5、v6、v7、v8、v9、v10、v11、v12、v13、v26 | 检测、分类、分割、姿态、OBB | 以目录中的精确 ONNX/Engine 工件为准。 |
| DETR | DEIMv2、PP-YOLOE、RF-DETR、RT-DETR | 检测、实例分割 | 不同导出图的输入输出合同不同。 |
| PaddleOCR | PP-OCRv4/v5 mobile/server、PP-OCRv6 tiny/small/medium | 检测、识别、方向分类 | 核心 CTC ONNX 资产见 [`models-paddleocr` Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr)；模型与字典按需下载，后端支持按精确工件验证。VL 的实验性本地 Bundle 见下方“可执行但未收录”。 |
| PaddleOCR / PP-Structure（新增 Release 资产） | PP-OCRv4/v5/v6 核心 DET/REC/CLS、版面、表格、公式、矫正、印章 | OCR 与文档结构 | 使用 `PaddleOcrReleaseClient` 从 `models-paddleocr` 按单模型下载并校验；Profile 分别由 `PaddleOcrModelCatalog` 与 `PaddleDocumentModelCatalog` 提供。 |
| 异常与抠图 | PaDiM、BRIA RMBG 1.4/2.0 | 异常分数/掩码、前景 alpha | RMBG 2.0 的精度和后端按工件分别记录。 |
| 视觉语言 | CLIP、BLIP Caption Base | 图文相似度、图像描述 | 仅完整的双图/生成工件可复现。 |
| 可提示分割 | SAM v1 | 点、框、掩码提示 | SAM2/SAM3 不属于当前公开 Preview。 |
| 本地 LLM | Qwen2.5 0.5B Instruct GGUF | 文本生成、流式、Embedding | 需要应用提供 LLamaSharp 原生运行时。 |

精确模型 ID、下载入口、工件格式和发布版本见[官方模型目录](model-catalog.md)；每个模型与后端的真实验证结果见[模型 × 后端验证矩阵](../model-backend-verification-matrix.md)。

## 如何阅读一个模型条目

模型族名称只用于导航，真正的运行身份由 `modelId + artifact + revision + SHA-256` 组成。选择模型时还要核对：

- 任务和 decoder 是否匹配，例如检测、实例分割、OBB 不能互换；
- 输入尺寸、动态维度、颜色空间、归一化和 resize 模式；
- 输出名称、布局、是否已做 NMS/激活以及标签/词表版本；
- 后端格式是否匹配：ONNX、OpenVINO IR、TensorRT Engine、GGUF 不是同一种资产；
- 目录状态是 Preview、External 还是 Planned。

目录可下载、合同测试通过和目标设备实测是三份不同证据。发布说明应分别表达，不用一个“支持”覆盖全部阶段。

## 可执行但未收录

以下路径具有部分合同或本机实验，但当前不作为官方可下载模型：

- PaddleOCR-VL-1.6：四图 Bundle 已在 Linux ORT CPU 和 OpenVINO CPU 完成 OCR、表格、公式、图表、Spotting、印章六任务生成，765 Token/文本与官方一致；[OpenVINO 验证](../../eng/models/paddle-ocr/verification/paddleocr-vl-openvino-linux-20261024.md)明确记录了进程内存配置、约 12.05 GiB 峰值和 OOM 边界。区域、混合类别和多页 API 可复用解码底图；真实版面模型/页面联合证据目前来自 ORT，不外推到 OpenVINO。其它后端和独立标注质量尚未完成；
- Wav2Vec2、Whisper tiny.en：有音频预处理或局部图执行证据，但完整可下载 Bundle 和发布边界尚未闭合；
- Donut CORD-v2：单页 Encoder/Prefill/KV 路径已有局部执行证据，多页和 TensorRT 尚未收录；
- LLaVA OneVision：单图 ORT/OpenVINO CPU 路径可执行，完整原生多模态包仍由应用提供；
- YOLO-World、Grounding DINO、YOLOE：提示词/词表/完整导出工件尚未形成统一发布包；
- SigLIP：本地双编码器合同存在，当前公开目录只收录 CLIP Preview。

PaddleOCR-VL 的本地 Bundle 校验和按需下载接口已经实现；实际四张 ONNX 图和 tokenizer 资产加入 `models-paddleocr` Release 后，才会把下载状态从“待加入 Release”更新为可下载。RGB/图像文件可自动准备官方 Patch。表格 OTSL 已支持逻辑单元格/HTML，Spotting 已支持严格标记解析、源裁剪像素与显式页面像素映射；其它任务的语义结构及跨后端支持不能据此外推。使用方式和各自边界见[VL 用法](visual-ocr.md#paddleocr-vl-是否已经可用于模型推理)及[表格与 Spotting 教程](visual-paddle-structure.md#vl-spotting文字四边形与页面坐标)。

## 暂不支持的模型族

以下条目不能因“已有接口”而推断为可用：

- SAM2/SAM3 完整图像/视频记忆与跟踪流程；
- BLIP VQA、BLIP-2、InstructBLIP 完整 Q-Former/投影/语言模型 Bundle；
- Qwen2.5-VL、Phi Vision 完整视觉处理和 KV 推理 Bundle；
- SigLIP 2 完整双编码器导出；
- LayoutLMv3 任务头、Pix2Struct DocVQA 完整原生 Bundle；
- HuBERT 下游任务头、pyannote 完整说话人分离 Bundle。

这些模型只有在完整工件、输入输出合同、运行时验证和发布资产齐备后，才会加入公开目录。

## 后端与性能

同一模型在 ONNX Runtime、OpenVINO、OpenCV DNN 和 TensorRT 上的结果不能互相替代。TensorRT Engine 与 GPU、CUDA、TensorRT 版本和输入 profile 绑定；OpenCV DNN 的动态 shape、辅助输入和 importer 限制按工件记录。PaddleOCR 单图完整流水线的最佳 batch/并发组合和设备耗时见[设备性能实测](device-performance-benchmarks.md)。

本页只表达模型支持状态，不承诺固定延迟、吞吐量或跨平台一致性。部署前请同时阅读[平台与后端支持](platform-support.md)、[安装指南](installation.md)和对应模型教程。

## 新模型进入公开目录的门槛

新条目至少需要：不可变工件及完整 sidecar、ModelPack 校验、Profile/decoder 合同测试、一张固定测试输入、一个后端的真实端到端结果、明确的失败/取消/Dispose 行为以及可执行示例。希望声明多个后端时，每个后端都要单独保留证据；从 ONNX 成功不能推断 OpenCV 或 TensorRT 成功。

后续发现模型合同错误时，不应原地替换 Release 资产。应提升 revision/变体、更新 SHA-256 和验证矩阵，并在迁移说明中记录输入输出变化。
