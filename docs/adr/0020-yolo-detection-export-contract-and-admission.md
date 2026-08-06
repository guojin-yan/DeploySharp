# ADR 0020: YOLO detection export contracts and admission / YOLO 检测导出合同与准入

- Status: Accepted for alpha.1 / 状态：alpha.1 接受
- Date: 2026-08-06
- Scope: `JYPPX.DeploySharp.Visual.Models.Yolo` and the existing Visual/OpenCV boundary / 范围：`JYPPX.DeploySharp.Visual.Models.Yolo` 与现有 Visual/OpenCV 边界

## Decision / 决策

YOLO versions are represented as data-bound Profiles in the existing Visual package. The package never references Ultralytics, Python, OpenCV, ONNX Runtime, OpenVINO, or a model-weight package. `JYPPX.DeploySharp.Visual.OpenCV` translates the common RGB/NCHW/letterbox/114/255 preprocessing contract; a backend remains selected by Core's registry. / YOLO 版本在现有 Visual 包中以绑定数据的 Profile 表达。该包不引用 Ultralytics、Python、OpenCV、ONNX Runtime、OpenVINO 或模型权重包。`JYPPX.DeploySharp.Visual.OpenCV` 转换通用 RGB/NCHW/letterbox/114/255 前处理合同；后端仍由 Core registry 选择。

The ten V1 detection families use four explicit export contracts: v5/v6 candidate-major raw heads with objectness, v7 batched end-to-end rows, v8/v9/v11/v12/v13 attribute-major raw heads, and v10/v26 end-to-end rows. The decoder never infers a family from a tensor shape and never applies NMS to an exporter-declared end-to-end output. Raw NMS uses model coordinates before source restoration. / V1 十个检测族使用四种显式导出合同：v5/v6 带 objectness 的 candidate-major raw head、v7 batched end-to-end 行、v8/v9/v11/v12/v13 attribute-major raw head、v10/v26 end-to-end 行。Decoder 不从形状猜版本，也不对声明为端到端的输出重复 NMS；raw NMS 在源图恢复前使用模型坐标。

Every Profile records family, upstream repository/commit, exporter version, opset, dynamic/static shape flag, artifact SHA256, tensor names/shapes, preprocessing version, postprocessing version, labels and decoder limits. The factory rejects missing artifact-specific options and provenance. / 每个 Profile 记录模型族、上游仓库/commit、导出器版本、opset、动态/静态形状标志、工件 SHA256、张量名称/形状、前处理版本、后处理版本、标签和 Decoder 限制。工厂拒绝缺失工件级选项和来源信息。

## Admission boundary / 准入边界

Local files in `E:\Model` and images in `E:\Data` are read-only validation inputs. The ten candidates have real ORT CPU and OpenVINO CPU evidence, but the exact weight provenance, official predictor golden comparison, and redistribution permission are not complete. Their ModelPack records therefore use `External`/blocked admission, and the embedded official ModelFactory catalog remains empty. / `E:\Model` 中的本机文件和 `E:\Data` 中的图片仅是只读验证输入。十个候选已有真实 ORT CPU 和 OpenVINO CPU 证据，但精确权重来源、官方 predictor 黄金对照和再分发许可尚未完成。因此它们的 ModelPack 记录使用 `External`/阻断准入，内置 ModelFactory 官方 catalog 继续为空。

This prevents a successful local run from silently becoming a downloadable product asset. A later release review must attach the exact checkpoint/license notices, a reproducible official reference environment, prepared-tensor and canonical-result goldens, and an immutable Release tag before changing an entry to Preview or Supported. / 这样可以防止一次本机运行悄然变成可下载产品资产。后续发布审核必须补齐精确 checkpoint/许可证声明、可复现官方参考环境、prepared tensor 与 canonical result 黄金文件以及不可变 Release tag，才能将条目改为 Preview 或 Supported。

## Consequences / 后果

- One `Visual` package supports all model sizes through Profile data; no `Yolo11n`/`Yolo11s` type explosion. / 一个 `Visual` 包通过 Profile 数据支持所有权重尺寸，不产生 `Yolo11n`/`Yolo11s` 类型爆炸。
- Raw and end-to-end postprocessing differences are visible and testable. / Raw 与端到端后处理差异显式且可测试。
- Applications install only the backend and native runtime they need. / 应用只安装需要的后端和 native runtime。
- AlgorithmVerified remains a strict, evidence-backed state rather than a name-based promise. / AlgorithmVerified 保持为严格、有证据支撑的状态，而不是按模型名承诺。
