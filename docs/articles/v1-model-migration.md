# DeploySharp V1 Model Capability Migration / DeploySharp V1 模型能力迁移

DeploySharp V2 does not provide source, binary, configuration, type, or behavioral compatibility with V1. It does, however, require feature coverage for every model/task combination present in the V1 `ModelType` inventory. V1 code is a migration inventory, not an API design template. / DeploySharp V2 不提供 V1 的源码、二进制、配置、类型或行为兼容；但 V1 `ModelType` 清单中的每个模型/任务组合都必须在 V2 中恢复功能覆盖。V1 代码是迁移清单，不是 V2 API 设计模板。

## Completion rule / 完成规则

A row is complete only when the exact model family has a versioned Visual profile, official or authoritative preprocessing/export/postprocessing evidence, a reproducible model SHA256, real image inference through at least one production backend, golden accuracy comparisons, tests, documentation, and a ModelPack/ModelFactory admission record. A generic task decoder or a synthetic backend fixture does not complete a model row. / 只有精确模型族具备版本化 Visual Profile、官方或权威的前处理/导出/后处理证据、可复现模型 SHA256、至少一个生产后端的真实图像推理、精度黄金对照、测试、文档以及 ModelPack/ModelFactory 准入记录时，该行才算完成。通用任务 Decoder 或合成后端夹具不能代表具体模型已迁移。

The inventory below was read from `origin/DeploySharpV1.0:src/DeploySharp/Model/ModelType.cs` on 2026-08-06. Stage 16 implements exact Profiles and real ORT/OpenVINO CPU paths for all ten YOLO detection rows. Those rows remain `ContractVerified + LocalBackendVerified`, not complete `AlgorithmVerified`, because independently reproducible official golden comparisons and exact redistribution review are still blocked. The strict completed count therefore remains **0/32**, while local backend coverage is **10/32**. / 下表于 2026-08-06 从 `origin/DeploySharpV1.0:src/DeploySharp/Model/ModelType.cs` 读取。阶段 16 已为十个 YOLO 检测行实现精确 Profile 和真实 ORT/OpenVINO CPU 路径；但独立可复现官方黄金对照与精确再分发审核仍受阻，因此状态是 `ContractVerified + LocalBackendVerified`，不是完整 `AlgorithmVerified`。严格完成数仍为 **0/32**，本机真实后端覆盖为 **10/32**。

## YOLO inventory / YOLO 清单

| V1 model type / V1 模型类型 | Task / 任务 | V2 reusable contract / V2 可复用合同 | Migration state / 迁移状态 |
| --- | --- | --- | --- |
| `YOLOCls` | Classification / 分类 | Classification | Planned: exact version/export profile required / 已规划：需精确版本与导出 Profile |
| `YOLOv5Det` | Detection / 检测 | YOLO candidate-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv5Seg` | Instance segmentation / 实例分割 | Instance segmentation | Planned / 已规划 |
| `YOLOv6Det` | Detection / 检测 | YOLO candidate-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv7Det` | Detection / 检测 | YOLO batched end-to-end Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv8Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv8Seg` | Instance segmentation / 实例分割 | Instance segmentation | Planned / 已规划 |
| `YOLOv8Obb` | Oriented detection / 旋转框检测 | Oriented detection | Planned / 已规划 |
| `YOLOv8Pose` | Pose / 姿态 | Pose | Planned / 已规划 |
| `YOLOv9Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv9Seg` | Instance segmentation / 实例分割 | Instance segmentation | Planned / 已规划 |
| `YOLOv10Det` | Detection / 检测 | YOLO end-to-end Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv11Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv11Seg` | Instance segmentation / 实例分割 | Instance segmentation | Planned / 已规划 |
| `YOLOv11Obb` | Oriented detection / 旋转框检测 | Oriented detection | Planned / 已规划 |
| `YOLOv11Pose` | Pose / 姿态 | Pose | Planned / 已规划 |
| `YOLOv12Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv13Det` | Detection / 检测 | YOLO attribute-major raw Profile pinned to iMoonLab reference | ContractVerified + LocalBackendVerified; exact artifact provenance/golden/license blocked / 合同与本机后端已验证；精确工件来源/黄金/许可阻断 |
| `YOLOv26Det` | Detection / 检测 | YOLO end-to-end Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv26Seg` | Instance segmentation / 实例分割 | Instance segmentation | Planned / 已规划 |
| `YOLOv26Obb` | Oriented detection / 旋转框检测 | Oriented detection | Planned / 已规划 |
| `YOLOv26Pose` | Pose / 姿态 | Pose | Planned / 已规划 |

## Other V1 model inventory / V1 其他模型清单

| V1 model type / V1 模型类型 | Task / 任务 | V2 reusable contract / V2 可复用合同 | Migration state / 迁移状态 |
| --- | --- | --- | --- |
| `AnomalibSeg` | Image anomaly and anomaly segmentation / 图像异常与异常分割 | Anomaly | Planned: exact Anomalib export families required / 已规划：需精确 Anomalib 导出族 |
| `DEIMv2Det` | Detection / 检测 | Detection | Planned / 已规划 |
| `RFDETRDet` | Detection / 检测 | Detection | Planned / 已规划 |
| `RFDETRSeg` | Instance segmentation / 实例分割 | Instance segmentation | Planned / 已规划 |
| `RTDETRDet` | Detection / 检测 | Detection | Planned / 已规划 |
| `PPYOLOETDet` | Detection / 检测 | Detection | Planned; preserve the V1 enum spelling only in this inventory / 已规划；V1 枚举拼写只保留在清单中 |
| `PaddleOcrDet` | Text detection / 文本检测 | OCR detection | Planned / 已规划 |
| `PaddleOcrCls` | Text orientation classification / 文本方向分类 | OCR orientation | Planned / 已规划 |
| `PaddleOcrRec` | Text recognition / 文本识别 | OCR recognition | Planned / 已规划 |
| `BriaRmbg` | Foreground/background segmentation / 前景背景分割 | Semantic segmentation | Planned / 已规划 |

## Required migration order / 必须迁移顺序

1. Close all V1 YOLO detection rows: v5, v6, v7, v8, v9, v10, v11, v12, v13, and v26. / 关闭 V1 全部 YOLO 检测行。
2. Close V1 YOLO classification, segmentation, Pose, and OBB rows. / 关闭 V1 的 YOLO 分类、分割、Pose 与 OBB 行。
3. Close DEIMv2, RF-DETR, RT-DETR, PP-YOLOE, PaddleOCR, Anomalib, and BRIA-RMBG rows. / 关闭其余检测、OCR、异常检测与抠图模型行。
4. Add newer model families and tasks without weakening the same admission gate. / 在不降低相同准入门禁的前提下继续新增模型族和任务。

V2 model coverage must not be declared complete, and no stable V2 release may be prepared, while any row in this inventory remains incomplete. This gate does not require retaining any V1 public type or implementation. / 本清单仍有未完成项时，不得宣称 V2 模型覆盖完成，也不得准备 V2 稳定版。该门禁不要求保留任何 V1 公共类型或实现。
