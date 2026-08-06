# Supported model roadmap / 支持模型路线表

This page separates DeploySharp's implemented tensor contracts from production model admission. `Contract surface` means the task can be represented by current Visual contracts; `AlgorithmVerified` requires an exact model artifact, official preprocessing/export/postprocessing parity, golden comparisons, performance evidence, and a legal ModelFactory asset. / 本页区分 DeploySharp 已实现的张量合同与正式模型准入。`Contract surface` 表示现有 Visual 合同能够表达该任务；`AlgorithmVerified` 还要求精确模型工件、官方前处理/导出/后处理一致性、黄金对照、性能证据以及合法的 ModelFactory 资产。

The upstream snapshot was verified on 2026-08-06 against the [Ultralytics official model index](https://docs.ultralytics.com/models/) and release `v8.4.115`. Ultralytics evolves independently, so every release must regenerate or review this matrix. / 上游快照于 2026-08-06 根据 [Ultralytics 官方模型索引](https://docs.ultralytics.com/models/)和 `v8.4.115` 核验。Ultralytics 独立演进，因此每次发布都必须重新生成或审阅本矩阵。

| Upstream family / 上游模型族 | Official tasks / 官方任务 | DeploySharp contract surface / DeploySharp 合同面 | Admission status / 准入状态 |
| --- | --- | --- | --- |
| YOLO26 | Detect, Segment, Semantic, Depth, Classify, Pose, OBB | Detect, instance/semantic segmentation, classify, Pose and OBB exist; depth needs a dedicated contract. / 已有检测、实例/语义分割、分类、Pose 与 OBB；Depth 需要专用合同。 | Planned; no family Profile is AlgorithmVerified. / 已规划；尚无模型族 Profile 达到 AlgorithmVerified。 |
| YOLO11 | Detect, Segment, Classify, Pose, OBB | All task-level contracts exist. / 全部任务级合同已存在。 | Planned / 已规划 |
| YOLO12 | Detect, Segment, Classify, Pose, OBB; official pretrained coverage currently differs by task. / 官方预训练权重覆盖因任务而异。 | All task-level contracts exist. / 全部任务级合同已存在。 | Planned / 已规划 |
| YOLOv10 | Detect | Detection contract exists. / 已有检测合同。 | Planned / 已规划 |
| YOLOv9 | Detect, Segment | Detection and instance-segmentation contracts exist. / 已有检测与实例分割合同。 | Planned / 已规划 |
| YOLOv8 | Detect, Segment, Classify, Pose, OBB | All task-level contracts exist. / 全部任务级合同已存在。 | Planned; local ONNX exports are available for read-only validation. / 已规划；本机已有可只读验证的 ONNX 导出。 |
| YOLOv7 | Predict from compatible ONNX or TensorRT export / 通过兼容 ONNX 或 TensorRT 导出推理 | Detection contract exists. / 已有检测合同。 | Planned; portable ONNX is the catalog candidate. / 已规划；目录候选仅使用可移植 ONNX。 |
| YOLOv6 | Detect | Detection contract exists. / 已有检测合同。 | Planned / 已规划 |
| YOLOv5 | Detect | Detection contract exists. / 已有检测合同。 | Planned / 已规划 |
| YOLOv4 | No supported upstream mode / 上游无受支持模式 | None / 无 | Not a required Ultralytics compatibility target while upstream reports `None`. / 上游标记为 `None` 时不属于 Ultralytics 兼容目标。 |
| YOLOv3 | Detect | Detection contract exists. / 已有检测合同。 | Planned / 已规划 |
| SAM 3, SAM 2, SAM, MobileSAM, FastSAM | Promptable Segment | Existing instance/semantic masks do not yet express prompt encoder/decoder sessions. / 现有实例/语义掩码尚不能完整表达提示编码器/解码器会话。 | Dedicated multimodel module required; local SAM2/SAM3 artifacts are read-only candidates. / 需要专用多模型模块；本机 SAM2/SAM3 工件仅作只读候选。 |
| YOLO-NAS | Detect | Detection contract exists. / 已有检测合同。 | Planned / 已规划 |
| RT-DETR | Detect | Detection contract exists. / 已有检测合同。 | Planned; local ONNX/IR candidates exist. / 已规划；本机存在 ONNX/IR 候选。 |
| YOLO-World | Open-vocabulary Detect | Detection geometry exists; text/prompt binding is missing. / 已有检测几何，缺少文本/提示绑定。 | Dedicated multimodal contract required. / 需要专用多模态合同。 |
| YOLOE | Open-vocabulary Detect, Segment | Detection/mask geometry exists; prompt binding is missing. / 已有检测/掩码几何，缺少提示绑定。 | Dedicated multimodal contract required. / 需要专用多模态合同。 |

## ModelFactory asset policy / ModelFactory 资产策略

- Prefer reproducibly exported portable ONNX and OpenVINO IR artifacts. TensorRT `.engine`/`.plan` remains device, CUDA, TensorRT and builder-version bound and is never a universal catalog asset. / 优先收录可复现导出的可移植 ONNX 与 OpenVINO IR；TensorRT `.engine`/`.plan` 与设备、CUDA、TensorRT 和 builder 版本绑定，绝不作为通用目录资产。
- Each accepted artifact records the upstream repository/release/commit, original checkpoint, exporter version and arguments, opset, tensor names/shapes, precision, size, SHA256, license, test image license, golden result and verified backends. / 每个准入工件必须记录上游仓库/Release/commit、原始 checkpoint、导出器版本与参数、opset、张量名称/形状、精度、大小、SHA256、许可证、测试图片许可证、黄金结果和已验证后端。
- The Ultralytics repository currently declares AGPL-3.0. Weight and dataset terms can differ. A model is not mirrored into a DeploySharp GitHub Release until its exact asset terms permit redistribution and the required notices/source obligations are prepared. / Ultralytics 仓库当前声明 AGPL-3.0；权重和数据集条款可能不同。只有精确资产条款允许再分发，且所需声明/源码义务已准备完成后，模型才能镜像到 DeploySharp GitHub Release。
- `E:\Model` is an authorized read-only validation source. Its 2026-08-06 inventory contains 176 ONNX files plus OpenVINO IR, checkpoints and device-bound engines. Local presence is not proof of provenance, license or catalog approval. / `E:\Model` 是获准使用的只读验证源；2026-08-06 清单包含 176 个 ONNX 及 OpenVINO IR、checkpoint 和设备绑定 engine。本机存在不代表来源、许可证或目录准入已通过。

The embedded official catalog remains empty until the first reviewed asset batch is explicitly published. Model support is added by family-sized complete stages, and every newly implemented deployment interface must add or update its row here and its ModelFactory admission record. / 在首批审核资产明确发布前，内置官方目录保持为空。模型支持按模型族规模的完整阶段加入；每个新部署接口都必须同步新增或更新本表行及 ModelFactory 准入记录。
