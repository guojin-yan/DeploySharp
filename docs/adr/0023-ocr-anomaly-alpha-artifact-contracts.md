# ADR 0023: OCR, anomaly, and alpha artifact contracts / OCR、异常与 Alpha 工件合同

- Status: Accepted for alpha.1 / 状态：alpha.1 接受
- Date: 2026-08-07
- Scope: PaddleOCR detection/recognition, Anomalib exports, BRIA RMBG, and OpenCV preparation / 范围：PaddleOCR 检测/识别、Anomalib 导出、BRIA RMBG 与 OpenCV 前处理

## Decision / 决策

The four V1 rows are implemented as immutable artifact-bound profiles in the existing Visual assembly. A profile declares exact names, types, shapes, opset, model and dictionary SHA256, upstream/exporter evidence, processing versions, bounded candidate/pixel/character budgets, and the decoder. Family, CTC/attention semantics, alpha semantics, normalization ownership, and source restoration are never inferred from filenames or tensor rank. / 四个 V1 行在现有 Visual 程序集中实现为不可变、工件绑定的 Profile。Profile 声明精确名称、类型、shape、opset、模型与字典 SHA256、上游/导出证据、处理版本、有界候选/像素/字符预算以及 Decoder。模型族、CTC/attention 语义、alpha 语义、归一化归属和源图恢复绝不从文件名或 tensor rank 推断。

PaddleOCR detection consumes a probability map and returns owned ordered text polygons through bounded managed DB processing. Recognition reuses the existing perspective-crop OCR pipeline and greedy CTC decoder with an artifact-bound Unicode token dictionary. Anomalib validates all four exported outputs but reuses the common anomaly result. BRIA is represented by a dedicated owned alpha mask because a continuous foreground probability is not a categorical semantic-segmentation label. / PaddleOCR 检测消费概率图，并通过有界托管 DB 处理返回自有有序文本 polygon。识别复用既有透视裁剪 OCR pipeline 与贪心 CTC Decoder，并绑定 Unicode token 字典。Anomalib 验证全部四个导出输出，但复用通用异常结果。BRIA 使用专用自有 alpha mask 表达，因为连续前景概率不是离散语义分割标签。

OpenCV is optional and transfers image data into managed tensors before native objects are released. Backend outputs are decoded into managed owned results. Application code owns native runtime selection. / OpenCV 是可选组件，并在 native 对象释放前把图像数据转移到托管 tensor。后端输出被解码为托管自有结果。native runtime 选择由应用拥有。

## Evidence and admission / 证据与准入

Authorized local ONNX artifacts executed on ORT CPU for both PP-OCRv5 detector/recognizer sizes, PaDiM, PatchCore, RMBG 1.4, and both RMBG 2.0 candidates at 1024. OpenVINO CPU covered the mobile OCR, PaDiM, and RMBG 1.4 output contracts. A same-image parity test compared OCR fields, anomaly map/mask, and alpha pixels within declared tolerances. This is local contract/backend evidence only. / 获授权本地 ONNX 工件已在 ORT CPU 执行两个 PP-OCRv5 检测/识别规格、PaDiM、PatchCore、RMBG 1.4，以及两个 1024 尺寸 RMBG 2.0 候选。OpenVINO CPU 覆盖移动 OCR、PaDiM 与 RMBG 1.4 输出合同。同图对齐测试在声明容差内比较 OCR 字段、异常图/mask 与 alpha 像素。这只构成本地合同/后端证据。

All manifests remain `External` and `redistributionAllowed:false`. Exact checkpoint/export provenance, weight/dictionary/image licensing, official predictor goldens, category/threshold/tiling evidence, arbitrary RMBG 2.0 dynamic shapes, and independent redistribution approval remain blockers for `AlgorithmVerified`. No IR sidecar is invented when no audited conversion exists. / 全部清单保持 `External` 与 `redistributionAllowed:false`。精确 checkpoint/导出来源、权重/字典/图片许可证、官方 predictor golden、类别/阈值/tiling 证据、任意 RMBG 2.0 动态尺寸与独立再分发授权仍是 `AlgorithmVerified` blocker。缺少已审核转换时不伪造 IR sidecar。

## Consequences / 后果

- No vendor-specific NuGet, model, dictionary, image, native runtime, Python dependency, TensorRT backend, Release asset, tag, or workflow dispatch is added. / 不新增厂商专用 NuGet、模型、字典、图片、native runtime、Python 依赖、TensorRT 后端、Release asset、tag 或 workflow dispatch。
- Field-level tolerance evidence is authoritative for cross-backend numeric parity; canonical hash equality is not required when floating-point values remain within tolerance. / 字段级容差证据是跨后端数值对齐的权威依据；浮点值在容差内时不要求规范哈希相等。
- Generic OCR and anomaly contracts remain reusable, while exact model-family profiles carry supply-chain and preprocessing semantics. / 通用 OCR 与异常合同继续可复用，精确模型族 Profile 承载供应链与前处理语义。
