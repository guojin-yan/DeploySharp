# DeploySharp V2.0

DeploySharp 是一套正在重构中的模块化 .NET 深度学习模型部署工具。

## 本次更新

当前 `2.0.0-alpha.1` 已在现有 Visual 包中加入完整 V1 YOLO 检测合同：工件绑定的 v5/v6/v7/v8/v9/v10/v11/v12/v13/v26 Profile、四类显式 raw/end-to-end 输出合同、官方风格 OpenCV letterbox 输入，以及十个本机 ONNX 工件的真实 ONNX Runtime/OpenVINO CPU 验证。精确来源、再分发许可和官方黄金审核完成前，候选清单不会进入仍为空的官方 catalog。详见 [Visual YOLO 检测指南](docs/articles/visual-yolo-detection.md)、[本版本说明](docs/releases/2.0.0-alpha.1.md)与[版本索引](docs/releases/README.md)。

阶段 25 新增工件绑定的 BLIP/BLIP-2/InstructBLIP 生成合同、精确 Processor/Tokenizer/Generation Identity 与有状态多次 Caption 生命周期。官方 BLIP Base Caption 已在同一获授权图片上通过官方 Predictor、ORT CPU、OpenVINO CPU 与 OpenCV 路径的 Token/EOS/文本及中间数值门控；BLIP VQA、BLIP-2 和 InstructBLIP 保持显式 External blocker。阶段 1-25 的模型已汇总到[开发模型总清单](docs/articles/development-model-inventory.md)，新获取/转换工件统一使用 `E:\DeploySharp-Models\<模型名>`；再分发未获准前不会上传模型。详见 [BLIP 模型族指南](docs/articles/visual-generative-vision-language.md)。

阶段 26 新增工件绑定的 LLaVA OneVision Vision/Projector、Managed Qwen2 Tokenizer/Embedding、Empty-past Prefill、24 层具名 KV Decode 与 Anyres OpenCV，并完成 ORT/OpenVINO CPU 真实执行。官方 Predictor 与当前 Runtime 的 Token/文本差异被如实保留；Qwen2.5-VL 与 Phi-3.5 Vision 保持精确 blocker。阶段 1-26 的[开发模型总清单](docs/articles/development-model-inventory.md)现有 60 条，全部新工件统一放在 `E:\DeploySharp-Models\<模型名>`；上传与可下载仍为 0。详见[原生多模态指南](docs/articles/visual-native-multimodal.md)。

阶段 28 新增音频语音工件绑定合同、Wav2Vec2 base-960h CTC 的 ORT/OpenVINO/OpenCV 与纯包证据、四份 Manifest，以及 Whisper/HuBERT/pyannote 的明确 blocker。阶段 1-28 的[开发模型总清单](docs/articles/development-model-inventory.md)现有 67 条，warehouse 有 21 个预期目录；Wav2Vec2 仍为 External，上传与下载仍为 0。详见[音频指南](docs/articles/visual-audio-speech.md)、[获取指南](docs/articles/model-acquisition-audio-speech.md)和[阶段 28 说明](docs/releases/stage-28.md)。


阶段 29 已记录不可变 LLM Profile/Bundle Identity 与 LLamaSharp 单写入取消语义。`E:\DeploySharp-Models` 没有精确 GGUF，`DEPLOYSHARP_LLAMA_MODEL` 未设置，因此 `llm/gguf/external-blocker` 保持 External，清单有 68 条记录和 55 份结构化 Manifest，上传/下载资产均为 0。详见 [LLM/GGUF 指南](docs/articles/llm-gguf-stage29.md) 与 [阶段 29 说明](docs/releases/stage-29.md)。

阶段 31 通过 `LLamaSharp.Backend.Cpu 0.27.0` 的真实 Generate、Stream、Cancel、Repeat、contention、Dispose 与 896 维 Embedding 矩阵，准入一个精确授权的 Qwen2.5 0.5B Instruct Q4_K_M GGUF。清单增至 69 条记录和 56 份结构化 Manifest；模型继续保持本地 External、非 AlgorithmVerified、未上传、不可下载且不进入空 official catalog。详见[运行实证](docs/articles/llm-gguf-stage31.md)、[获取记录](docs/articles/model-acquisition-llm-gguf.md)与[阶段 31 说明](docs/releases/stage-31.md)。

阶段 32 将该准入收紧为不可变边界：门禁读取 GGUF magic、校验全部模型/来源 sidecar 和结构化运行 evidence；证据写入拒绝覆盖，并验证并发 Dispose 与调用方持有的无 native/CPU 纯包资产图。本阶段没有新增模型或 evidence。详见[阶段 32 指南](docs/articles/llm-gguf-stage32.md)与[阶段 32 说明](docs/releases/stage-32.md)。

阶段 33 复验打包后的纯托管依赖边界与隔离 `net8.0` 的 skip/no-native/真实 CPU consumer 矩阵。模型、sidecar 和既有 evidence 的大小与哈希均无漂移，不需要修改实现或公共合同。详见[阶段 33 审计](docs/articles/llm-gguf-stage33.md)与[阶段 33 说明](docs/releases/stage-33.md)。

阶段 34 将上述手工检查固化为贯通中央版本、项目引用、lock/assets、nuspec、严格 payload 和程序集引用的可复用只读包边界门。正向包、注入 native payload 的负向包、隔离 skip/missing-SHA/no-native/真实 CPU consumer、取消与并发 Dispose 均通过；两次独立包的语义 payload 一致，同时如实保留 NuGet 容器元数据导致的非位复现结论。详见[阶段 34 审计](docs/articles/llm-gguf-stage34.md)与[阶段 34 说明](docs/releases/stage-34.md)。

阶段 35 建立并由阶段 48 扩展的候选门禁现覆盖十个可打包项目、83 个 TFM 组、五类负向突变与全部 30 项纯包 consumer。当前候选的元数据、依赖闭包、DLL/XML payload 和程序集引用全部通过；原阶段 35 双包复现结论仍保留为语义 `9/9`、原始 ZIP `0/9`。未签名包、脏工作树、符号/源码策略和明确发布授权继续作为 blocker。详见[阶段 35 审计](docs/articles/release-candidate-governance-stage35.md)与[阶段 35 说明](docs/releases/stage-35.md)。

阶段 36 evidence 经阶段 48 扩展后覆盖十个包、83 个 TFM，保留机器可读的来源/许可证/SBOM、PDB/SourceLink 与公共 API 证据。八类独立突变均被拒绝；PDB 使用稳定的 `/_/` 映射路径而不保留本机物理路径，既有许可证人工复核、符号/签名策略和发布授权 blocker 继续保留。详见[阶段 36 审计](docs/articles/release-evidence-governance-stage36.md)与[阶段 36 说明](docs/releases/stage-36.md)。

阶段 37 审计本地缓存的 `JYPPX.TensorRT.CSharp.API 4.0.0`，确认 15 个 TFM、45 个托管 DLL、关键 API、repository/content hash 与无 native payload。由于包许可证元数据缺失、上游 Owner 许可证决策未完成且不可变 `v4.0.0` tag 无法核验，本阶段未创建 TensorRT 后端、engine、native probe、GPU 结论、包或公共 API。详见[阶段 37 审计](docs/articles/tensorrt-admission-stage37.md)与[阶段 37 说明](docs/releases/stage-37.md)。

阶段 38 确认 TensorRT 包身份和 Stage 37 三项 blocker 均未变化且没有 blocker 消失，并新增一项正式构建 lock/assets 未与包提交和 nupkg 绑定的进入证据 blocker。retained JSON 保持字节不变，负向套件扩展为八类，未开始适配器、native 或 GPU 实现。详见[阶段 38 复核](docs/articles/tensorrt-license-release-review-stage38.md)与[阶段 38 说明](docs/releases/stage-38.md)。

阶段 39 发现上游新 HEAD `3107d2f...` 已记录 Owner-approved `Apache-2.0` policy，但字节不变的 nupkg 仍绑定旧提交 `be2e507...`，其 Owner 决策仍未完成。没有重建持许可证包、不可变 `v4.0.0` 正式身份或 release-bound lock/assets，因此四项 blocker 全部保留，未开始 TensorRT 实现或 GPU 工作。详见[阶段 39 复核](docs/articles/tensorrt-formal-admission-input-review-stage39.md)与[阶段 39 说明](docs/releases/stage-39.md)。

阶段 40 确认候选集合仍为上游 20 个 artifact 路径加 NuGet cache 1 个副本；全部缺少包许可证/签名，也没有候选声明获批 HEAD。上游 `4.0.0` 发布说明不能替代不可变 proof：没有 `v4.0.0` tag，当前 HEAD 包数为 0，Owner approval pending，public publish disabled，且没有包绑定 lock/assets。详见[阶段 40 复核](docs/articles/tensorrt-approved-rebuild-package-review-stage40.md)与[阶段 40 说明](docs/releases/stage-40.md)。

阶段 41 发现真实公开 `v4.0.0` Release：tag 指向 `673e120...`，新 managed asset 为 15,595,749 bytes、SHA256 `58add436...`。但 Release 仍为 `immutable=false`，没有 SHA512/contentHash 或包绑定 provenance，新包也未在本地交付以复核 nuspec/API/payload/signature；四项 blocker 全部保留，未开始适配器或 GPU 工作。详见[阶段 41 复核](docs/articles/tensorrt-formal-release-asset-review-stage41.md)与[阶段 41 说明](docs/releases/stage-41.md)。

阶段 42 从 NuGet.org 复核精确 `4.0.0` repository-signed 包，确认 `Apache-2.0`、`673e120...`、15 TFM/45 managed DLL、关键 PE/XML 合同、无 native/model/engine payload，以及 net8 的 311 个导出类型与 4,374 个 public declared methods。package license 与 Owner decision 两项 blocker 已消失，仅保留 Release 跨渠道不可变绑定与同次构建 provenance；未开始适配器或 GPU 工作。详见[阶段 42 复核](docs/articles/tensorrt-nuget-org-package-review-stage42.md)与[阶段 42 说明](docs/releases/stage-42.md)。

阶段 43 再次核对公开 Release，并在新的隔离 cache 恢复精确 NuGet.org 包。Release 仍为 `immutable=false`，资产仍只有 19 个 nupkg 与 source ZIP，没有 manifest/provenance；两项剩余 blocker 均 retained，包与 API 身份不变。详见[阶段 43 复核](docs/articles/tensorrt-release-binding-admission-stage43.md)与[阶段 43 说明](docs/releases/stage-43.md)。

阶段 44 不重新打开已消失的许可证问题，只复核两项最终发布证明。Release ID `368273346` 仍为 mutable，20 个资产没有跨渠道 manifest 或同次构建 provenance；精确 NuGet.org 包、API 与 Repository signature 均未变化。两项当前 blocker 继续 retained，不开始适配器、native 或 GPU 工作。详见[阶段 44 复核](docs/articles/tensorrt-immutable-proof-admission-stage44.md)与[阶段 44 说明](docs/releases/stage-44.md)。

阶段 45 确认 Release 自发布后仍未变化：`immutable=false`、20 个原资产且 proof asset 为 0。tag tree 没有 lock/assets 或通用 release manifest/provenance/attestation，精确 NuGet.org 包及全部 managed/API/signature 检查也未变化。两项 blocker 继续 retained，不开始适配器或 GPU 工作。详见[阶段 45 复核](docs/articles/tensorrt-formal-proof-convergence-stage45.md)与[阶段 45 说明](docs/releases/stage-45.md)。

阶段 46 再次确认同一公开状态：Release ID `368273346` 仍为 mutable 且未更新，proof asset 仍为 0，完整 tag tree 仍没有 lock/assets 或通用 release provenance；精确 NuGet.org 包及全部 managed/API/signature 检查不变。两项 blocker 继续 retained，不开始适配器或 GPU 工作。详见[阶段 46 复核](docs/articles/tensorrt-immutable-release-proof-stage46.md)与[阶段 46 说明](docs/releases/stage-46.md)。

阶段 47 确认 Release 仍为 mutable 且未更新，proof asset 仍为 0，完整 tag tree 仍没有 lock/assets 或通用 provenance；精确 NuGet.org 包及 managed/API/signature 检查不变。两项 blocker 继续 retained，不开始适配器或 GPU 工作。详见[阶段 47 复核](docs/articles/tensorrt-release-proof-recheck-stage47.md)与[阶段 47 说明](docs/releases/stage-47.md)。

阶段 48 将两项缺失证明降级为仅阻止正式发布，并实现隔离的 `JYPPX.DeploySharp.Backend.TensorRT` net8 managed adapter。适配器只校验和加载调用方持有的 External `.engine/.plan`，通过精确 NuGet.org 4.0.0 API 映射 Core 命名张量，不打包 native runtime、engine、模型或 TensorRT-LLM。纯包 consumer、4 项适配器测试、Stage 35/36 门禁和全解决方案均通过；真实 GPU 推理仍需另行授权精确 plan/model 与 runtime identity。详见[阶段 48 指南](docs/articles/tensorrt-managed-adapter-stage48.md)与[阶段 48 说明](docs/releases/stage-48.md)。

阶段 51 在同一隔离包中新增 `TensorRtOnnxEngineBuilder`：校验并 hash 单文件 ONNX，应用 workspace、精度/优化策略与动态 min/opt/max profile，原子写入调用方 External `.engine/.plan`。推理 provider 继续只接收 engine；native runtime 和生成 engine 继续由 consumer 持有。CUDA/RTC 前后处理是下一层独立 managed 能力，本阶段不加入占位 API。详见[阶段 51 指南](docs/articles/tensorrt-onnx-engine-builder-stage51.md)与[阶段 51 说明](docs/releases/stage-51.md)。

阶段 52 新增独立 managed CUDA/RTC 执行层，提供显式 compiler、stream、device-buffer、launch 与同步合同；它不改变 provider 默认行为，也不隐式持久化 PTX/CUBIN。详见[阶段 52 指南](docs/articles/tensorrt-cuda-rtc-managed-stage52.md)与[阶段 52 说明](docs/releases/stage-52.md)。


阶段 53 确认上游 package/Release identity 未变化，并保留 215-member inference/builder/CUDA-RTC public contract；正式发布仍只受两项不可变 proof 阻断。focused managed tests 通过，真实 GPU 继续 skip/blocked；在禁止下载的边界下，因精确上游 nupkg 本地不存在，当前纯包 consumer 复验如实记录为 blocked，未用旧缓存包替代。详见[阶段 53 审计](docs/articles/tensorrt-cuda-rtc-gpu-proof-stage53.md)与[阶段 53 说明](docs/releases/stage-53.md)。

阶段 55 在固定身份的 RTX 3060/CUDA 12.9/TensorRT 10.11 matrix 上真实通过 CUDA/RTC 前后处理、同步错误传播、ONNX build 与 TensorRT inference；随后提供的精确 NuGet.org repository-signed 包也通过本地纯包 consumer。215-member public contract 与 consumer ownership 不变，正式发布仍缺两项 immutable proof。详见[阶段 55 审计](docs/articles/tensorrt-cuda-rtc-gpu-proof-stage55.md)与[阶段 55 说明](docs/releases/stage-55.md)。

阶段 61 提供有界本地 PTX/CUBIN 与 engine/plan 存储及显式 `TensorRtLocalSessionFactory` 门面，可使用稳定的每用户 local-data root 或调用方绝对路径。key 绑定 ONNX/build、managed/native runtime、driver 与 GPU 兼容输入，但不绑定物理 GPU UUID。同进程按 root/kind/key 去重，不同进程不得并发写同一 root；手工复制的 engine 仍可由现有 provider 直接加载，最终兼容性由 TensorRT native deserialize 决定。缓存、native runtime、engine、plan、PTX 与 CUBIN 继续由 consumer 持有且不进入包。详见[本地缓存指南](docs/articles/tensorrt-external-cache-stage61.md)与[阶段 61 说明](docs/releases/stage-61.md)。

仓库外的 `E:\GitSpace\DeploySharp-V2.0\plan\开发计划-轮次收口清单.md` 现已固定每轮的临时文件清理、模型仓库存放、转换文章、清单校验、Git 提交/推送状态、阻塞请求和下一阶段提示词检查。

DeploySharp 将算法速度和官方模型保真作为产品要求。后端微型合同夹具只能证明适配器行为；只有预处理、张量解释和后处理通过可复现黄金对照与官方实现一致后，具体模型才会标记为支持。性能测量会拆分预处理、主机/设备传输、后端执行、后处理和端到端耗时，并在保留旧框架兼容的同时为现代 TFM 提供经过测量的优化路径。

V2 将稳定契约、领域流程、托管后端适配器和平台原生运行时完全拆分，不提供与 DeploySharp V1 的源码、二进制、配置或行为兼容。

The TensorRT package loads caller-owned engines, builds ONNX engines through the explicit builder, exposes CUDA/RTC stream/buffer contracts, and offers configurable local engine/PTX/CUBIN cache. Native runtimes, models, generated artifacts and cache roots remain consumer-owned; independent processes must not write the same cache root concurrently. / TensorRT 包加载调用方持有的 engine，通过显式 builder 构建 ONNX engine，提供 CUDA/RTC stream/buffer 合同，并提供可配置的本地 engine/PTX/CUBIN 缓存。native runtime、模型、生成工件和 cache root 继续由 consumer 持有；独立进程不得并发写入同一 cache root。

请先阅读 [本地 LLM 快速开始](docs/articles/llm-getting-started.md)、[LLamaSharp 原生后端指南](docs/articles/llamasharp-native-backends.md) 和 [LLM 兼容性与生命周期](docs/articles/llamasharp-compatibility.md)。
视觉流程请阅读 [Visual 已准备张量快速开始](docs/articles/visual-getting-started.md)、[Visual 坐标与解码](docs/articles/visual-coordinate-decoding.md)、[Visual YOLO 检测](docs/articles/visual-yolo-detection.md)、[Visual 语义分割](docs/articles/visual-semantic-segmentation.md)、[Visual Pose](docs/articles/visual-pose-estimation.md)、[Visual 实例分割](docs/articles/visual-instance-segmentation.md)、[Visual OBB 旋转框](docs/articles/visual-oriented-detection.md)、[Visual OCR](docs/articles/visual-ocr.md)、[OCR 方向与自动纠正](docs/articles/visual-ocr-orientation.md)、[Visual 异常检测](docs/articles/visual-anomaly-detection.md) 和 [Visual 生命周期与兼容性](docs/articles/visual-lifecycle-compatibility.md)。项目级门禁见 [性能与模型保真](docs/articles/performance-and-model-fidelity.md)，Ultralytics 等模型族的实现/准入状态见[支持模型路线表](docs/articles/supported-models.md)。

模型清单和下载流程请阅读 [开发模型总清单](docs/articles/development-model-inventory.md)、[ModelPack JSON 快速开始](docs/articles/modelpack-json-getting-started.md)、[ModelFactory 快速开始](docs/articles/modelfactory-getting-started.md) 与 [官方模型目录](docs/articles/model-catalog.md)。官方目录在获得真实 Release 和模型再分发授权前保持为空。

ONNX CPU 推理请阅读 [ONNX Runtime 快速开始](docs/articles/onnxruntime-getting-started.md) 与 [兼容性和生命周期](docs/articles/onnxruntime-compatibility.md)。

OpenVINO CPU 推理请阅读 [OpenVINO 快速开始](docs/articles/openvino-getting-started.md) 与 [兼容性和生命周期](docs/articles/openvino-compatibility.md)。

## 当前状态

`2.0.0-alpha.1` 是早期架构基线，在首个 RC 版本前公共 API 仍可能调整。

## 许可证

Apache-2.0。
