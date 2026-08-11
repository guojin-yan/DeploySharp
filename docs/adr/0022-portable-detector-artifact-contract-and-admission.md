# ADR 0022: Portable detector artifact contracts and admission / 便携检测器工件合同与准入

- Status: Accepted for alpha.1 / 状态：alpha.1 接受
- Date: 2026-08-07
- Scope: `JYPPX.DeploySharp.Visual.Models.Detr` and `JYPPX.DeploySharp.Visual.OpenCV` / 范围：DETR/PP-YOLOE 部署合同与 OpenCV 输入边界

## Decision / 决策

The non-YOLO detector rows are represented by immutable, artifact-bound profiles in the existing Visual assembly. A profile declares names, types, shapes, output semantics, exact preprocessing, upstream metadata, SHA256, bounded resource limits, and a decoder. It does not infer a family, task, no-object convention, NMS ownership, or auxiliary-input meaning from a model filename or observed tensor shape. / 非 YOLO 检测器行在现有 Visual 程序集中由不可变、工件绑定的 Profile 表达。Profile 声明名称、类型、shape、输出语义、精确前处理、上游元数据、SHA256、有界资源限制以及 Decoder。它不从模型文件名或观察到的 tensor shape 推断模型族、任务、no-object 约定、NMS 归属或辅助输入含义。

RF-DETR follows the audited official postprocessor: apply sigmoid to raw class logits, select a global top-k over foreground columns, restore normalized cxcywh boxes, and avoid a second NMS. The optional no-object column is an explicit local-artifact extension, not a family-wide default. RF-DETR masks use selected query logits, bilinear resizing and strict zero thresholding with owned source masks. / RF-DETR 遵循已审核的官方后处理器：对原始类别 logit 使用 sigmoid，在前景列上做全局 top-k，恢复归一化 cxcywh 框，并避免二次 NMS。可选 no-object 列是显式的本地工件扩展，不是模型族默认值。RF-DETR mask 使用已选 query logit、双线性缩放和严格零阈值，并返回自有源图 mask。

DEIMv2 uses centered black square letterbox with floor dimensions and sends the complete padded canvas as `orig_target_sizes`; direct resizing and `/255` are used for inspected PaddleDetection exports. OpenCV is the optional implementation of these image contracts and copies pixels into managed tensors before native objects are released. / DEIMv2 使用居中黑色正方形 letterbox 与向下取整，并把完整填充画布作为 `orig_target_sizes`；已检查的 PaddleDetection 导出使用直接缩放与 `/255`。OpenCV 是这些图像合同的可选实现，并在 native 对象释放前把像素复制到托管 tensor。

## Evidence and admission / 证据与准入

On 2026-08-07, the authorized local ONNX matrix executed DEIMv2, both RF-DETR contracts and PP-YOLOE through ORT CPU. RF-DETR detection and segmentation additionally executed through OpenVINO CPU. RT-DETR has a reproducible Tile failure; DEIMv2/RT-DETR OpenVINO auxiliary ports and PP-YOLOE OpenVINO dynamic-rank Squeeze remain blocked. All candidates therefore remain `External` with no redistribution authorization and no official catalog admission. / 在 2026-08-07，获授权本地 ONNX 矩阵通过 ORT CPU 执行了 DEIMv2、两个 RF-DETR 合同和 PP-YOLOE。RF-DETR 检测与分割还通过 OpenVINO CPU 执行。RT-DETR 存在可复现 Tile 失败；DEIMv2/RT-DETR 的 OpenVINO 辅助端口以及 PP-YOLOE 的 OpenVINO 动态 rank Squeeze 仍被阻断。因此全部候选保持 `External`，没有再分发授权，也不会准入官方 catalog。

## Consequences / 后果

- The implementation adds no vendor package, model weight, Python/PyTorch dependency, TensorRT backend, CUDA dependency, Release asset, tag, or workflow dispatch. / 实现不新增厂商包、模型权重、Python/PyTorch 依赖、TensorRT 后端、CUDA 依赖、Release asset、tag 或 workflow dispatch。
- Stable diagnostics are preferred over inferred aliases or fallback semantics. / 稳定诊断优先于推测端口别名或回退语义。
- `AlgorithmVerified` requires independent official predictor goldens, exact checkpoint provenance, redistribution approval, and field-level backend comparisons beyond a successful local inference run. / `AlgorithmVerified` 需要独立官方 predictor golden、精确 checkpoint 来源、再分发授权以及字段级后端对比，不能由一次本地成功推理替代。
