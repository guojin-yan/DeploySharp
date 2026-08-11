# ADR 0021: YOLO multitask export contracts and fidelity / YOLO 多任务导出合同与保真

- Status: Accepted for alpha.1 / 状态：alpha.1 接受
- Date: 2026-08-06
- Scope: `JYPPX.DeploySharp.Visual.Models.Yolo` and `JYPPX.DeploySharp.Visual.OpenCV` / 范围：YOLO 模型族与 OpenCV 输入边界

## Decision / 决策

The remaining V1 YOLO classification, segmentation, Pose, and OBB rows are represented as immutable, artifact-bound data Profiles in the existing Visual package. The profile records exporter-specific tensor names, layouts, candidate counts, model dimensions, labels/topology and preprocessing/postprocessing versions. A decoder is selected by the profile; it never infers a task from a tensor shape or model filename. / V1 剩余 YOLO 分类、实例分割、Pose 和 OBB 行在现有 Visual 包中以不可变、工件绑定的数据 Profile 表达。Profile 记录导出器特定的 tensor 名称、布局、候选数、模型尺寸、标签/拓扑和前后处理版本。Decoder 由 Profile 选择，绝不从 tensor shape 或模型文件名推断任务。

YOLOv5/v8/v9/v11 raw segment exports use explicit packed row contracts plus a 32-channel prototype tensor. YOLO26 segment, Pose and OBB exports are end-to-end rows and bypass managed NMS. YOLOv8/v11 Pose exports use decoded COCO-17 keypoints; YOLO26 uses its end-to-end decoded rows. YOLOv8/v11 OBB uses the official angle regularization and probabilistic IoU rotated NMS. / YOLOv5/v8/v9/v11 原始分割导出使用显式 packed 行合同和 32 通道 prototype tensor。YOLO26 分割、Pose 和 OBB 是端到端行，绕过托管 NMS。YOLOv8/v11 Pose 使用已解码 COCO-17 关键点；YOLO26 使用端到端解码行。YOLOv8/v11 OBB 使用官方角度规整和概率 IoU 旋转 NMS。

`Visual.OpenCV` is the only image input implementation in this stage. It translates the profile into RGB/NCHW/Float32 tensors, uses 114 letterbox for detection-family tasks and the audited 224 center crop for classification, and copies pixels into owned managed storage. ORT/OpenVINO remain interchangeable Core backend implementations. / `Visual.OpenCV` 是本阶段唯一图像输入实现。它把 Profile 转换为 RGB/NCHW/Float32 tensor，检测族任务使用 114 letterbox，分类使用已审核的 224 center crop，并把像素复制到自有托管存储。ORT/OpenVINO 仍是可互换的 Core 后端实现。

## Evidence and admission / 证据与准入

The 12 local artifacts were inspected with ONNX metadata and SHA256 on 2026-08-06 and executed through both CPU backends. The integration test is environment-gated and does not place local weights in source control. The rows remain `ContractVerified + LocalBackendVerified`; official predictor goldens, exact checkpoint provenance for locally exported YOLOv5/v9 rows, and redistribution permissions are required before ModelFactory can change them from `External`. / 12 个本机工件于 2026-08-06 通过 ONNX 元数据和 SHA256 检查，并通过两个 CPU 后端执行。集成测试由环境变量门控，本机权重不会进入源码管理。各行保持 `ContractVerified + LocalBackendVerified`；在 ModelFactory 将其从 `External` 改变状态前，仍需官方 predictor 黄金对照、本机导出 YOLOv5/v9 行的精确权重来源和再分发许可。

## Consequences / 后果

- One package contains the model family contracts without an Ultralytics product package. / 一个包内包含模型族合同，不引入 Ultralytics 产品包。
- Backend adapters receive named tensors and never see image-library or model-specific types. / 后端只接收命名 tensor，不接触图像库或模型专用类型。
- Raw/end-to-end, mask, keypoint and rotated-NMS semantics are explicit and testable. / raw/端到端、mask、关键点和旋转 NMS 语义显式且可测试。
- A local successful run cannot silently become a downloadable or official model asset. / 本地成功运行不会自动变成可下载或官方模型资产。
