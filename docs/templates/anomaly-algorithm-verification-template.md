# Anomaly AlgorithmVerified admission template / 异常模型 AlgorithmVerified 准入模板

Complete every field before a production anomaly model enters the official catalog. Missing evidence means `ContractVerified` or Preview, never `AlgorithmVerified`. / 正式异常模型进入官方目录前必须填写全部字段；证据缺失时只能标记 `ContractVerified` 或 Preview，绝不能标记 `AlgorithmVerified`。

## Identity and legal provenance / 身份与合法来源

- Model/family/version and intended task / 模型、家族、版本及目标任务：
- Upstream repository, immutable commit, release and model URL / 上游仓库、不可变 commit、Release 与模型地址：
- Training dataset, split and license / 训练数据集、划分与许可证：
- Model, test-image, mask and golden-result licenses / 模型、测试图、mask 与黄金结果许可证：
- Redistribution approval, attribution, file sizes and SHA256 / 再分发批准、归属、文件大小与 SHA256：
- Original/exported/converted artifacts and reproducible commands / 原始、导出、转换工件及可复现命令：

## Official preprocessing and output semantics / 官方前处理与输出语义

- Reference source files and immutable lines / 参考源码及不可变行号：
- Orientation, color, resize/crop/letterbox rounding, padding and interpolation / 方向、颜色、resize/crop/letterbox 取整、padding 与插值：
- Mean/std/scale, dtype, layout, dynamic axes and batch / mean/std/scale、dtype、layout、动态轴与 batch：
- Image-score output name, activation, reduction and range / 图像分数输出名、激活、归约与范围：
- Map output name, layout, channels, coordinate space and value semantics / 异常图输出名、layout、通道、坐标空间与数值语义：
- Channel aggregation, normalization, resize, threshold and comparison rule / 通道聚合、归一化、resize、阈值与比较规则：
- Calibration set and threshold provenance / 校准集与阈值来源：

## Golden accuracy comparison / 黄金精度对照

- Runner OS/hardware and backend/runtime/package versions / Runner 操作系统/硬件及 backend/runtime/package 版本：
- Test image IDs/licenses/sizes/SHA256 / 测试图 ID、许可证、大小与 SHA256：
- Prepared tensor SHA256 and selected samples / 已准备 tensor SHA256 与关键采样：
- Raw image score/map, normalized map and mask SHA256 / 原始图像分数/异常图、归一化图与 mask SHA256：
- Source restoration dimensions and pixel-coordinate convention / 源图恢复尺寸与像素坐标约定：
- Image-level AUROC/AP/F1 and pixel-level AUROC/AUPRO/IoU/Dice implementation / 图像级与像素级指标及实现：
- Official baseline, observed result, delta, tolerance and explanation / 官方基线、实测结果、差异、容差与解释：
- ONNX Runtime/OpenVINO and applicable device cross-check / ONNX Runtime/OpenVINO 及适用设备交叉验证：

## Performance / 性能

- Release commit/configuration, warmup, sample count and concurrency / Release commit/配置、预热、样本数与并发：
- Decode/preprocess, backend, postprocess and end-to-end P50/P95 / 解码/前处理、backend、后处理与端到端 P50/P95：
- Image/model/map sizes, channels, interpolation and batch / 图像/模型/异常图尺寸、通道、插值与 batch：
- Images/pixels per second, managed/native allocation and peak memory / 图像/像素吞吐、托管/native 分配与峰值内存：

## Review / 审核

- Reviewer and date / 审核人及日期：
- Accepted backend/format/device matrix / 接受的 backend/格式/设备矩阵：
- Known limitations / 已知限制：
- ModelPack manifest and ModelFactory immutable Release approved / ModelPack 清单与 ModelFactory 不可变 Release 已批准：
