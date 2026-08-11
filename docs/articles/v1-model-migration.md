# DeploySharp V1 Model Capability Migration / DeploySharp V1 模型能力迁移

DeploySharp V2 does not provide source, binary, configuration, type, or behavioral compatibility with V1. It does, however, require feature coverage for every model/task combination present in the V1 `ModelType` inventory. V1 code is a migration inventory, not an API design template. / DeploySharp V2 不提供 V1 的源码、二进制、配置、类型或行为兼容；但 V1 `ModelType` 清单中的每个模型/任务组合都必须在 V2 中恢复功能覆盖。V1 代码是迁移清单，不是 V2 API 设计模板。

## Stage 23 open-vocabulary migration / 阶段 23 开放词汇迁移

V1 has no precise artifact-bound open-vocabulary equivalent. Do not map a YOLO-World filename to ordinary YOLO labels or assume runtime text. Fixed exports require `OpenVocabularyDetectionProfile` plus exact vocabulary/tokenizer/embedding identity; detector-to-SAM flows use `GroundedSamImageSession` and must not manually restore boxes or masks. Grounding DINO/YOLOE stay blocked until an exact official native bundle is supplied. / V1 没有精确开放词汇对应项。不得按文件名映射普通 YOLO 标签或假定运行时文本；固定导出须绑定完整 Identity，组合须使用 Grounded-SAM 会话且不能重复恢复坐标。

## Completion rule / 完成规则

A row is complete only when the exact model family has a versioned Visual profile, official or authoritative preprocessing/export/postprocessing evidence, a reproducible model SHA256, real image inference through at least one production backend, golden accuracy comparisons, tests, documentation, and a ModelPack/ModelFactory admission record. A generic task decoder or a synthetic backend fixture does not complete a model row. / 只有精确模型族具备版本化 Visual Profile、官方或权威的前处理/导出/后处理证据、可复现模型 SHA256、至少一个生产后端的真实图像推理、精度黄金对照、测试、文档以及 ModelPack/ModelFactory 准入记录时，该行才算完成。通用任务 Decoder 或合成后端夹具不能代表具体模型已迁移。

The inventory below was read from `origin/DeploySharpV1.0:src/DeploySharp/Model/ModelType.cs` on 2026-08-06. Stages 16-20 provide local backend paths for 31 rows. Stage 21 closes the remaining `RTDETRDet` execution row with explicitly bound Paddle decoded vector-count and raw-query artifacts while retaining the old Tile failure. These rows remain `ContractVerified + LocalBackendVerified`, not complete `AlgorithmVerified`, because independently reproducible official goldens, exact artifact provenance, and redistribution review are still blocked. The strict completed count therefore remains **0/32**, while local backend coverage is **32/32**. / 阶段 16-20 提供 31 行本机后端路径；阶段 21 通过显式绑定 Paddle 已解码 vector-count 与 raw-query 工件关闭剩余 `RTDETRDet` 执行行，同时保留旧 Tile 失败。严格完成数仍为 **0/32**，本机真实后端覆盖为 **32/32**。

## YOLO inventory / YOLO 清单

| V1 model type / V1 模型类型 | Task / 任务 | V2 reusable contract / V2 可复用合同 | Migration state / 迁移状态 |
| --- | --- | --- | --- |
| `YOLOCls` | Classification / 分类 | Artifact-bound YOLOv8 classification probabilities | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确 YOLOv8 分类概率 Profile 与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv5Det` | Detection / 检测 | YOLO candidate-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv5Seg` | Instance segmentation / 实例分割 | YOLO packed candidate-major + prototype masks | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确 packed/prototype 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv6Det` | Detection / 检测 | YOLO candidate-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv7Det` | Detection / 检测 | YOLO batched end-to-end Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv8Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv8Seg` | Instance segmentation / 实例分割 | YOLO packed attribute-major + prototype masks | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确 packed/prototype 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv8Obb` | Oriented detection / 旋转框检测 | YOLO DOTA-15 packed rotated boxes | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确旋转框合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv8Pose` | Pose / 姿态 | YOLO COCO-17 packed pose | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确 COCO-17 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv9Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv9Seg` | Instance segmentation / 实例分割 | YOLO packed attribute-major + prototype masks | ContractVerified + LocalBackendVerified; exact checkpoint provenance remains External / 精确合同与双 CPU 后端已验证，权重来源仍为 External |
| `YOLOv10Det` | Detection / 检测 | YOLO end-to-end Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv11Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv11Seg` | Instance segmentation / 实例分割 | YOLO packed attribute-major + prototype masks | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确 packed/prototype 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv11Obb` | Oriented detection / 旋转框检测 | YOLO DOTA-15 packed rotated boxes | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确旋转框合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv11Pose` | Pose / 姿态 | YOLO COCO-17 packed pose | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确 COCO-17 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv12Det` | Detection / 检测 | YOLO attribute-major raw Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv13Det` | Detection / 检测 | YOLO attribute-major raw Profile pinned to iMoonLab reference | ContractVerified + LocalBackendVerified; exact artifact provenance/golden/license blocked / 合同与本机后端已验证；精确工件来源/黄金/许可阻断 |
| `YOLOv26Det` | Detection / 检测 | YOLO end-to-end Profile | ContractVerified + LocalBackendVerified; official golden/license blocked / 合同与本机后端已验证；官方黄金/许可阻断 |
| `YOLOv26Seg` | Instance segmentation / 实例分割 | YOLO end-to-end rows + prototype masks | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确端到端/prototype 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv26Obb` | Oriented detection / 旋转框检测 | YOLO DOTA-15 end-to-end rotated boxes | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确端到端旋转框合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |
| `YOLOv26Pose` | Pose / 姿态 | YOLO COCO-17 end-to-end packed pose | ContractVerified + LocalBackendVerified; official golden/license blocked / 精确端到端 COCO-17 合同与双 CPU 后端已验证，官方黄金/许可仍阻断 |

## Other V1 model inventory / V1 其他模型清单

| V1 model type / V1 模型类型 | Task / 任务 | V2 reusable contract / V2 可复用合同 | Migration state / 迁移状态 |
| --- | --- | --- | --- |
| `AnomalibSeg` | Image anomaly and anomaly segmentation / 图像异常与异常分割 | Artifact-bound PaDiM/PatchCore four-output anomaly export | ContractVerified + LocalBackendVerified (ORT; PaDiM OpenVINO); checkpoint category/transform/threshold/tiling/golden/license blocked / 工件绑定 PaDiM/PatchCore 四输出异常导出与本机后端已验证；checkpoint 类别/transform/阈值/tiling/golden/许可证阻断 |
| `DEIMv2Det` | Detection / 检测 | Artifact-bound decoded DEIMv2 rows | ContractVerified + LocalBackendVerified (ORT); OpenVINO auxiliary alias and admission evidence blocked / 工件绑定 DEIMv2 解码行与 ORT 已验证；OpenVINO 辅助别名和准入证据阻断 |
| `RFDETRDet` | Detection / 检测 | Artifact-bound RF-DETR raw query logits | ContractVerified + LocalBackendVerified (ORT/OpenVINO); provenance/golden/release blocked / 工件绑定 RF-DETR 原始 query logit 与双后端已验证；来源/golden/发布阻断 |
| `RFDETRSeg` | Instance segmentation / 实例分割 | Artifact-bound RF-DETR raw query masks | ContractVerified + LocalBackendVerified (ORT/OpenVINO); provenance/golden/release blocked / 工件绑定 RF-DETR 原始 query mask 与双后端已验证；来源/golden/发布阻断 |
| `RTDETRDet` | Detection / 检测 | Artifact-bound Paddle decoded scalar/vector counts, raw queries, typed auxiliary geometry, and RT-DETRv2 deploy triplet | ContractVerified + LocalBackendVerified on ORT/OpenVINO; old Tile failure retained; v2 artifact/provenance/golden/release blocked / 合同与 ORT/OpenVINO 本机后端已验证；保留旧 Tile 失败；v2 工件/来源/golden/发布阻断 |
| `PPYOLOETDet` | Detection / 检测 | Artifact-bound Paddle decoded rows | ContractVerified + LocalBackendVerified (ORT); OpenVINO dynamic-rank Squeeze and admission evidence blocked; preserve V1 enum spelling only here / 工件绑定 Paddle 解码行与 ORT 已验证；OpenVINO 动态 rank Squeeze 和准入证据阻断；仅在此保留 V1 枚举拼写 |
| `PaddleOcrDet` | Text detection / 文本检测 | Artifact-bound PP-OCRv5 DB probability map | ContractVerified + LocalBackendVerified (mobile/server ORT; mobile OpenVINO); exact official contour/unclip golden and provenance blocked / 工件绑定 PP-OCRv5 DB 概率图与本机后端已验证；精确官方 contour/unclip golden 与来源阻断 |
| `PaddleOcrCls` | Text-line orientation classification / 文本行方向分类 | Artifact-bound legacy and PP-LCNet `0/180` orientation with explicit whole-image/per-region strategies | ContractVerified + LocalBackendVerified (ORT/OpenVINO and three-model parity); exact export/license/official golden blocked / 工件绑定 legacy 与 PP-LCNet `0/180`，显式整图/逐区域策略；双后端和三模型对齐已验证，精确导出/许可证/官方 golden 阻断 |
| `PaddleOcrRec` | Text recognition / 文本识别 | Artifact-bound PP-OCRv5 CTC probabilities and dictionary | ContractVerified + LocalBackendVerified (mobile/server ORT; mobile OpenVINO); export/dictionary provenance and official text golden blocked / 工件绑定 PP-OCRv5 CTC 概率与字典并完成本机后端验证；导出/字典来源与官方文本 golden 阻断 |
| `BriaRmbg` | Foreground/background segmentation / 前景背景分割 | Owned continuous semantic alpha mask | ContractVerified + LocalBackendVerified (RMBG 1.4 ORT/OpenVINO; RMBG 2.0 ORT at 1024); export/processor/license/golden blocked / 自有连续语义 alpha mask 与本机后端已验证；导出/processor/许可证/golden 阻断 |

## Required migration order / 必须迁移顺序

1. Close all V1 YOLO detection rows: v5, v6, v7, v8, v9, v10, v11, v12, v13, and v26. / 关闭 V1 全部 YOLO 检测行。
2. Close V1 YOLO classification, segmentation, Pose, and OBB rows. / 关闭 V1 的 YOLO 分类、分割、Pose 与 OBB 行。
3. Close DEIMv2, RF-DETR, RT-DETR, PP-YOLOE, PaddleOCR, Anomalib, and BRIA-RMBG local execution rows. After Stage 21, all 32 rows have local backend coverage; independent `AlgorithmVerified` and release-admission evidence remain outstanding. / 关闭其余模型的本机执行行；阶段 21 后 32 行均有本机后端覆盖，独立 AlgorithmVerified 与发布准入证据仍待补齐。
4. Add newer model families and tasks without weakening the same admission gate. / 在不降低相同准入门禁的前提下继续新增模型族和任务。

V2 model coverage must not be declared complete, and no stable V2 release may be prepared, while any row in this inventory remains incomplete. This gate does not require retaining any V1 public type or implementation. / 本清单仍有未完成项时，不得宣称 V2 模型覆盖完成，也不得准备 V2 稳定版。该门禁不要求保留任何 V1 公共类型或实现。

## Stage 22 promptable segmentation migration / 阶段 22 可提示分割迁移

V1 had no reusable multi-session SAM contract to preserve. Migrate custom SAM integrations to `PromptableSegmentationProfile` plus `PromptableSegmentationArtifactBundle`, `OpenCvPromptableSegmentationInputFactory`, and `PromptableSegmentationImageSession`: call set-image once, issue one or more typed point/box/mask-feedback predictions, then clear or dispose. Do not pass raw embeddings or low-resolution logits across images; identity mismatch is a stable error. Canonical source masks and RLE use the existing instance-segmentation result types. / V1 没有需要保留的通用多 Session SAM API。自定义集成应迁移到 Profile/Bundle/OpenCV Factory/ImageSession：一次 set-image，多次 typed Prompt，再 clear/dispose。不得跨图复用原始 Embedding 或低分辨率 Logit；规范源图 Mask/RLE 复用现有实例分割结果。

There is no V2 SAM 2/SAM 3 video compatibility shim. The official PyTorch predictors mutate memory/tracker state that is not available as a complete audited native bundle, so applications must remain on their upstream predictor until that blocker is closed rather than receiving an incomplete fallback. / V2 不提供 SAM 2/3 视频兼容 shim。官方 PyTorch Predictor 的 Memory/Tracker 状态尚无完整可审计 native Bundle，因此应用应继续使用上游 Predictor，而不是获得不完整回退。

## Stage 24 vision-language migration / 阶段 24 视觉语言迁移

V1 had no reusable artifact-bound CLIP/SigLIP dual-encoder contract. Migrate custom integrations to `VisionLanguageEmbeddingProfile`, `VisionLanguageArtifactBundle`, `VisionLanguageEmbeddingSession`, an exact upstream tokenizer producing `TextTokenBatch`, and `VisionLanguageScorer`. Do not reuse token arrays or embeddings across Profiles/artifact hashes, and do not apply CLIP softmax to SigLIP independent pair scores. / V1 没有可复用的工件绑定 CLIP/SigLIP 双编码器合同。自定义集成应迁移到上述 Profile/Bundle/Session、精确上游 Tokenizer 与 Scorer；不得跨 Profile/工件复用 Token 或 Embedding，也不得把 CLIP Softmax 用于 SigLIP 独立配对分数。

## Stage 25 generative vision-language migration / 阶段 25 生成式视觉语言迁移

V1 had no reusable artifact-bound Caption/VQA contract. Migrate a supported BLIP Caption integration to `GenerativeVisionLanguageProfile`, `GenerativeVisionLanguageArtifactBundle`, `BlipBertTokenizer`, `OpenCvGenerativeVisionLanguageInputFactory`, and `GenerativeVisionLanguageSession`: set one image, generate one or more owned results, then clear/dispose. Do not pass encoder tensors, prompts, token arrays, or cache state across profile/artifact/processor/tokenizer/generation identities. BLIP VQA, BLIP-2, and InstructBLIP applications must remain on their official upstream predictors until their exact native blockers close; Caption is not a VQA fallback. / V1 没有可复用的 Caption/VQA 工件合同。可执行 BLIP Caption 迁移到上述 Profile/Bundle/Tokenizer/OpenCV/Session；不得跨完整 Identity 复用状态。其余应用应继续使用官方上游 Predictor，Caption 不是 VQA 回退。

## Stage 26 native multimodal migration / 阶段 26 原生多模态迁移

V1 had no equivalent LLaVA/Qwen-VL/Phi Vision stateful contract. Stage 26 therefore adds no compatibility shim: applications explicitly construct `NativeMultimodalProfile`/Bundle, provide external tokenizer/model/image-newline files, prepare and set one image, generate VQA/Caption results, then clear/dispose. Common `GenerationResult` is reused, but image/KV identity, token budget, single-writer concurrency, and runtime-specific fidelity evidence are new V2 behavior. / V1 没有等价的原生多模态有状态合同，因此不提供兼容 Shim；应用必须显式构造 Profile/Bundle、提供外部资产并管理 Set-image/Generate/Clear/Dispose。通用生成结果复用，但图像/KV Identity、Token Budget、Single-writer 并发与 Runtime 保真属于新的 V2 行为。

## Stage 27 document intelligence migration / 阶段 27 文档智能迁移

V1 had no artifact-bound page/layout/schema contract. A Donut migration explicitly constructs `DocumentUnderstandingProfile`/Bundle, supplies the external Encoder/Prefill/Decode graphs and managed tokenizer, prepares one page through `OpenCvDocumentUnderstandingInputFactory`, calls `SetDocument` and structured extraction, then clears/disposes. Do not pass OCR words into an OCR-free Donut/Pix2Struct profile, and do not pass a LayoutLMv3 caller box/token alignment into a Donut/Pix2Struct graph. Page order, source identity, Processor/OCR/Schema identity, raw tokens, parse status, field provenance, and KV are new V2 behavior; there is no V1 shim or fixed-JSON fallback. / V1 没有工件绑定的页/版面/Schema 合同；迁移必须显式管理 Profile/Bundle、外部图、Tokenizer、SetDocument/Generate/Clear/Dispose。OCR、Box、Token 对齐不能跨家族复用，不提供固定 JSON 回退。
