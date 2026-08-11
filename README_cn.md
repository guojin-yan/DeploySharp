# DeploySharp V2.0

DeploySharp 是一套正在重构中的模块化 .NET 深度学习模型部署工具。

## 本次更新

当前 `2.0.0-alpha.1` 已在现有 Visual 包中加入完整 V1 YOLO 检测合同：工件绑定的 v5/v6/v7/v8/v9/v10/v11/v12/v13/v26 Profile、四类显式 raw/end-to-end 输出合同、官方风格 OpenCV letterbox 输入，以及十个本机 ONNX 工件的真实 ONNX Runtime/OpenVINO CPU 验证。精确来源、再分发许可和官方黄金审核完成前，候选清单不会进入仍为空的官方 catalog。详见 [Visual YOLO 检测指南](docs/articles/visual-yolo-detection.md)、[本版本说明](docs/releases/2.0.0-alpha.1.md)与[版本索引](docs/releases/README.md)。

阶段 25 新增工件绑定的 BLIP/BLIP-2/InstructBLIP 生成合同、精确 Processor/Tokenizer/Generation Identity 与有状态多次 Caption 生命周期。官方 BLIP Base Caption 已在同一获授权图片上通过官方 Predictor、ORT CPU、OpenVINO CPU 与 OpenCV 路径的 Token/EOS/文本及中间数值门控；BLIP VQA、BLIP-2 和 InstructBLIP 保持显式 External blocker。阶段 1-25 的模型已汇总到[开发模型总清单](docs/articles/development-model-inventory.md)，新获取/转换工件统一使用 `E:\DeploySharp-Models\<模型名>`；再分发未获准前不会上传模型。详见 [BLIP 模型族指南](docs/articles/visual-generative-vision-language.md)。

阶段 26 新增工件绑定的 LLaVA OneVision Vision/Projector、Managed Qwen2 Tokenizer/Embedding、Empty-past Prefill、24 层具名 KV Decode 与 Anyres OpenCV，并完成 ORT/OpenVINO CPU 真实执行。官方 Predictor 与当前 Runtime 的 Token/文本差异被如实保留；Qwen2.5-VL 与 Phi-3.5 Vision 保持精确 blocker。阶段 1-26 的[开发模型总清单](docs/articles/development-model-inventory.md)现有 60 条，全部新工件统一放在 `E:\DeploySharp-Models\<模型名>`；上传与可下载仍为 0。详见[原生多模态指南](docs/articles/visual-native-multimodal.md)。

阶段 28 新增音频语音工件绑定合同、Wav2Vec2 base-960h CTC 的 ORT/OpenVINO/OpenCV 与纯包证据、四份 Manifest，以及 Whisper/HuBERT/pyannote 的明确 blocker。阶段 1-28 的[开发模型总清单](docs/articles/development-model-inventory.md)现有 67 条，warehouse 有 21 个预期目录；Wav2Vec2 仍为 External，上传与下载仍为 0。详见[音频指南](docs/articles/visual-audio-speech.md)、[获取指南](docs/articles/model-acquisition-audio-speech.md)和[阶段 28 说明](docs/releases/stage-28.md)。

当前阶段提示词统一保存在仓库外的 `E:\GitSpace\DeploySharp-V2.0\plan\prompts`；下一阶段扩大为 Stage 36 包来源与发布证据门，在不授权签名、发布、依赖升级或模型分发的前提下审计许可证闭包、SourceLink/符号复现、SBOM 输入、API 基线与 consumer 兼容性。

阶段 29 已记录不可变 LLM Profile/Bundle Identity 与 LLamaSharp 单写入取消语义。`E:\DeploySharp-Models` 没有精确 GGUF，`DEPLOYSHARP_LLAMA_MODEL` 未设置，因此 `llm/gguf/external-blocker` 保持 External，清单有 68 条记录和 55 份结构化 Manifest，上传/下载资产均为 0。详见 [LLM/GGUF 指南](docs/articles/llm-gguf-stage29.md) 与 [阶段 29 说明](docs/releases/stage-29.md)。

阶段 31 通过 `LLamaSharp.Backend.Cpu 0.27.0` 的真实 Generate、Stream、Cancel、Repeat、contention、Dispose 与 896 维 Embedding 矩阵，准入一个精确授权的 Qwen2.5 0.5B Instruct Q4_K_M GGUF。清单增至 69 条记录和 56 份结构化 Manifest；模型继续保持本地 External、非 AlgorithmVerified、未上传、不可下载且不进入空 official catalog。详见[运行实证](docs/articles/llm-gguf-stage31.md)、[获取记录](docs/articles/model-acquisition-llm-gguf.md)与[阶段 31 说明](docs/releases/stage-31.md)。

阶段 32 将该准入收紧为不可变边界：门禁读取 GGUF magic、校验全部模型/来源 sidecar 和结构化运行 evidence；证据写入拒绝覆盖，并验证并发 Dispose 与调用方持有的无 native/CPU 纯包资产图。本阶段没有新增模型或 evidence。详见[阶段 32 指南](docs/articles/llm-gguf-stage32.md)与[阶段 32 说明](docs/releases/stage-32.md)。

阶段 33 复验打包后的纯托管依赖边界与隔离 `net8.0` 的 skip/no-native/真实 CPU consumer 矩阵。模型、sidecar 和既有 evidence 的大小与哈希均无漂移，不需要修改实现或公共合同。详见[阶段 33 审计](docs/articles/llm-gguf-stage33.md)与[阶段 33 说明](docs/releases/stage-33.md)。

阶段 34 将上述手工检查固化为贯通中央版本、项目引用、lock/assets、nuspec、严格 payload 和程序集引用的可复用只读包边界门。正向包、注入 native payload 的负向包、隔离 skip/missing-SHA/no-native/真实 CPU consumer、取消与并发 Dispose 均通过；两次独立包的语义 payload 一致，同时如实保留 NuGet 容器元数据导致的非位复现结论。详见[阶段 34 审计](docs/articles/llm-gguf-stage34.md)与[阶段 34 说明](docs/releases/stage-34.md)。

阶段 35 将门禁扩大到九个可打包项目、82 个 TFM 组、五类负向突变与全部 30 项纯包 consumer。包元数据、依赖闭包、DLL/XML payload 和程序集引用全部通过；两次全包打包的语义 payload 为 `9/9` 一致，原始 ZIP 位复现仍为 `0/9`。未签名 `NU3004`、脏工作树、符号/源码策略和明确发布授权继续作为 blocker。详见[阶段 35 审计](docs/articles/release-candidate-governance-stage35.md)与[阶段 35 说明](docs/releases/stage-35.md)。

仓库外的 `E:\GitSpace\DeploySharp-V2.0\plan\开发计划-轮次收口清单.md` 现已固定每轮的临时文件清理、模型仓库存放、转换文章、清单校验、Git 提交/推送状态、阻塞请求和下一阶段提示词检查。

DeploySharp 将算法速度和官方模型保真作为产品要求。后端微型合同夹具只能证明适配器行为；只有预处理、张量解释和后处理通过可复现黄金对照与官方实现一致后，具体模型才会标记为支持。性能测量会拆分预处理、主机/设备传输、后端执行、后处理和端到端耗时，并在保留旧框架兼容的同时为现代 TFM 提供经过测量的优化路径。

V2 将稳定契约、领域流程、托管后端适配器和平台原生运行时完全拆分，不提供与 DeploySharp V1 的源码、二进制、配置或行为兼容。

当前基础包包括 `JYPPX.DeploySharp.Core`、`JYPPX.DeploySharp.Visual`、`JYPPX.DeploySharp.LLM`、`JYPPX.DeploySharp.Backend.LlamaSharp`、`JYPPX.DeploySharp.Backend.OnnxRuntime`、`JYPPX.DeploySharp.Backend.OpenVINO`、`JYPPX.DeploySharp.ModelPack.Json` 和 `JYPPX.DeploySharp.ModelFactory`。Core 不依赖推理框架和图像库，提供推理契约、张量、模型元数据、统一结果 DTO、诊断、错误和显式后端注册机制；Visual 提供不绑定图像库的已准备张量流程、可逆几何、分类、稠密/旋转检测、图像级/像素级异常检测、双模型 polygon + greedy CTC OCR、语义分割、Direct/Prototype 实例分割、direct/heatmap Pose 解码、确定性 polygon IoU、RLE、NMS 和 OKS，不引用 OpenCV 或具体推理后端；LLM 提供聊天、流式生成、取消和 Embedding；LLamaSharp 后端加载 GGUF，但不打包原生运行时；ONNX Runtime 与 OpenVINO 后端执行真实命名张量 CPU 推理，同时由应用持有原生运行时包；ModelPack.Json 提供严格的模型清单与本地完整性校验；ModelFactory 提供经过审核的目录、不可变 GitHub Release 下载、内容寻址缓存和离线复用，但不打包模型权重。

请先阅读 [本地 LLM 快速开始](docs/articles/llm-getting-started.md)、[LLamaSharp 原生后端指南](docs/articles/llamasharp-native-backends.md) 和 [LLM 兼容性与生命周期](docs/articles/llamasharp-compatibility.md)。
视觉流程请阅读 [Visual 已准备张量快速开始](docs/articles/visual-getting-started.md)、[Visual 坐标与解码](docs/articles/visual-coordinate-decoding.md)、[Visual YOLO 检测](docs/articles/visual-yolo-detection.md)、[Visual 语义分割](docs/articles/visual-semantic-segmentation.md)、[Visual Pose](docs/articles/visual-pose-estimation.md)、[Visual 实例分割](docs/articles/visual-instance-segmentation.md)、[Visual OBB 旋转框](docs/articles/visual-oriented-detection.md)、[Visual OCR](docs/articles/visual-ocr.md)、[OCR 方向与自动纠正](docs/articles/visual-ocr-orientation.md)、[Visual 异常检测](docs/articles/visual-anomaly-detection.md) 和 [Visual 生命周期与兼容性](docs/articles/visual-lifecycle-compatibility.md)。项目级门禁见 [性能与模型保真](docs/articles/performance-and-model-fidelity.md)，Ultralytics 等模型族的实现/准入状态见[支持模型路线表](docs/articles/supported-models.md)。

模型清单和下载流程请阅读 [开发模型总清单](docs/articles/development-model-inventory.md)、[ModelPack JSON 快速开始](docs/articles/modelpack-json-getting-started.md)、[ModelFactory 快速开始](docs/articles/modelfactory-getting-started.md) 与 [官方模型目录](docs/articles/model-catalog.md)。官方目录在获得真实 Release 和模型再分发授权前保持为空。

ONNX CPU 推理请阅读 [ONNX Runtime 快速开始](docs/articles/onnxruntime-getting-started.md) 与 [兼容性和生命周期](docs/articles/onnxruntime-compatibility.md)。

OpenVINO CPU 推理请阅读 [OpenVINO 快速开始](docs/articles/openvino-getting-started.md) 与 [兼容性和生命周期](docs/articles/openvino-compatibility.md)。

## 当前状态

`2.0.0-alpha.1` 是早期架构基线，在首个 RC 版本前公共 API 仍可能调整。

## 许可证

Apache-2.0。
