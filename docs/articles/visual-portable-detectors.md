# Visual RT-DETR family and portable detectors / Visual RT-DETR 模型族与便携检测器

Stages 18 and 21 implement artifact-bound DEIMv2, RF-DETR, Paddle RT-DETR, RT-DETRv2, and PP-YOLOE contracts in the existing `JYPPX.DeploySharp.Visual` package. Stage 21 closes the V1 `RTDETRDet` local execution gap without replacing the retained failing artifact or creating a vendor/single-model package. `JYPPX.DeploySharp.Visual.OpenCV` owns image decoding; Core named tensors cross backend boundaries; TensorRT remains unimplemented. / 阶段 18 与 21 在现有 `JYPPX.DeploySharp.Visual` 包中实现工件绑定的 DEIMv2、RF-DETR、Paddle RT-DETR、RT-DETRv2 与 PP-YOLOE 合同。阶段 21 在不替换既有失败工件、不创建厂商或单模型包的前提下关闭 V1 `RTDETRDet` 本机执行缺口。图像解码由 `Visual.OpenCV` 负责，后端边界只传递 Core 具名 tensor；TensorRT 仍未实现。

## Quick start / 快速开始

Bind every semantic choice to the inspected artifact. The profile below is for the exact decoded vector-count ONNX; it must not be reused for the scalar-count failure artifact, raw-query graph, or RT-DETRv2 export. / 所有语义选择必须绑定到已检查工件。下例只适用于精确的已解码 vector-count ONNX，不得复用于 scalar-count 失败工件、raw-query 图或 RT-DETRv2 导出。

```csharp
var options = new PortableDetectorProfileOptions(
    16,
    new VisualSize(640, 640),
    cocoLabels,
    inputName: "image",
    artifactSha256: "a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db",
    upstreamRepository: "https://github.com/PaddlePaddle/PaddleDetection",
    upstreamCommit: "b25522a0f4bde8c80603f3ba5e3472059972e3b5",
    exporterVersion: "PaddleDetection-export_model+paddle2onnx-local-artifact-unverified",
    license: "External; upstream code Apache-2.0; artifact chain unverified",
    scoreThreshold: .4f,
    boxesOutputName: "save_infer_model/scale_0.tmp_0",
    countOutputName: "save_infer_model/scale_1.tmp_0",
    hasDynamicBatchAxis: true,
    paddleCountShape: PortableDetectorCountShape.BatchVector);

PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETR(
    new ModelId("external/rt-detr-r50vd-decoded-vector"), options);

using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(
    new OpenCvVisualInputFactory(), imagePath, profile);
```

`OpenCvPortableDetectorPreprocessing.Create`, `CreateFromFile`, and `CreateFromBytes` decode once and create the primary image tensor plus all profile-declared auxiliary tensors. Register the immutable `VisualProfile`, create an artifact for either `onnxruntime` or `openvino`, and run the normal `VisualPipeline`; the result is the existing `DetectionResult`. Cancellation, async calls, concurrent session leases, disposal, stable backend errors, and output ownership therefore remain in the common pipeline. / 这些 OpenCV 入口只解码一次，并同时创建主图像 tensor 与 Profile 声明的全部辅助 tensor。注册不可变 `VisualProfile`，为 `onnxruntime` 或 `openvino` 创建工件后运行通用 `VisualPipeline`；结果仍是现有 `DetectionResult`。因此取消、异步、并发 session lease、释放、稳定后端错误与输出所有权均沿用通用管线。

## Exact contract matrix / 精确合同矩阵

| Contract / 合同 | Named inputs / 具名输入 | Named outputs and decode / 具名输出与解码 | Coordinates, threshold, NMS / 坐标、阈值、NMS | Local CPU evidence / 本机 CPU 证据 |
| --- | --- | --- | --- | --- |
| Paddle decoded scalar-count failure | `image:float32[1,3,640,640]`, `im_shape:float32[1,2]`, `scale_factor:float32[1,2]` | `reshape2_95.tmp_0:float32[N,6]`, `tile_3.tmp_0:int32[]`; rows are class, score, xyxy | Source pixels; strict `score > 0.4`; exported graph owns decode/NMS | Retained reproducible ORT failure at `p2o.Tile.3`; not a runnable row |
| Paddle decoded vector-count ONNX | `image:float32[-1,3,640,640]`, `im_shape:float32[-1,2]`, `scale_factor:float32[-1,2]` | `save_infer_model/scale_0.tmp_0:float32[N,6]`, `save_infer_model/scale_1.tmp_0:int32[-1]` | Source pixels; strict `> 0.4`; no second NMS | ORT CPU |
| Paddle decoded vector-count IR | Fixed-batch equivalents of the three Paddle inputs | `save_infer_model/scale_0.tmp_0:float32[300,6]`, `cast_5.tmp_0:int32[1]` | Same decoded contract; IR output alias is artifact-bound | OpenVINO CPU |
| Paddle raw query | `image:float32[-1,3,640,640]` | `stack_7.tmp_0_slice_0:float32[-1,300,4]` normalized cxcywh; `stack_8.tmp_0_slice_0:float32[-1,300,80]` logits | Sigmoid, global top-k, source restoration; strict `> 0.4`; no NMS | ORT and OpenVINO CPU |
| Official PyTorch RT-DETRv2 deploy | `images:float32[-1,3,640,640]`, `orig_target_sizes:int64[-1,2]` | `labels:int64[-1,N]`, `boxes:float32[-1,N,4]`, `scores:float32[-1,N]` | Graph emits source-pixel xyxy; no second restore/NMS; application threshold, official demo default `0.6` | Contract tests pass; real backend test skips until `DEPLOYSHARP_RTDETRV2_ONNX` is set |

The `RTDETRRawDet` and `RTDETRv2Det` family values are distinct from `RTDETRDet`; tensor rank is never used to select one. Raw semantics were corroborated against the official focal postprocessor and same-image parity with the decoded artifact. RT-DETRv2 deploy labels are the official contiguous class indices produced by `index % num_classes` with `num_classes: 80`; Paddle decoded `class_id` follows the export's inference label list. Applications must bind that exact label order rather than infer labels from a filename. / 三个 family value 明确分离，绝不按 tensor rank 选择。raw 语义由官方 focal 后处理与同图已解码工件对齐共同确认。RT-DETRv2 deploy 标签是官方 `index % num_classes` 生成的连续索引，配置为 80 类；Paddle 已解码 `class_id` 遵循导出 inference label list。应用必须绑定该精确顺序，不得从文件名猜测标签。

## Auxiliary tensor and geometry ownership / 辅助 tensor 与几何所有权

`PortableDetectorAuxiliaryInputContract` is the single source of truth. It binds name, element type, size space, pair order, and generation rule; `PreparedVisualInput.AuxiliaryInputs` owns the resulting managed tensors. Adapters and backends validate/consume these tensors and never recalculate geometry. / 该 typed contract 是唯一事实源，绑定名称、元素类型、尺寸空间、二元顺序与生成规则；生成的托管 tensor 由 `PreparedVisualInput.AuxiliaryInputs` 拥有。adapter/backend 只验证和消费，绝不重复计算几何。

| Auxiliary / 辅助输入 | Rule / 规则 | Resize or letterbox effect / resize 或 letterbox 影响 |
| --- | --- | --- |
| Paddle `im_shape` | Float32 `[modelHeight, modelWidth]` | Direct resize reports `640,640`; source size is not used here |
| Paddle `scale_factor` | Float32 `[modelHeight/sourceHeight, modelWidth/sourceWidth]` | Direct-resize Y then X scale; a future letterbox profile must declare a different rule |
| RT-DETRv2 `orig_target_sizes` | Int64 `[sourceWidth, sourceHeight]` | Preserves pre-resize source size; graph scales xyxy to source space |
| DEIM `orig_target_sizes` | Int64 model/padded canvas height then width | The DEIM profile deliberately differs from RT-DETRv2 |

Profiles currently declare a dynamic batch axis when the graph does, but the executable Visual image contract is validated as batch one (`minimumBatch=maximumBatch=1`). Fixed and dynamic metadata cannot be silently exchanged. / Profile 可声明图中的动态 batch 轴，但当前可执行 Visual 图像合同严格验证为 batch 1；固定与动态元数据不得静默互换。

## Official supply chain / 官方供应链

The 2026-08-08 audit pins PaddleDetection `release/2.9`/`v2.9.0` at `b25522a0f4bde8c80603f3ba5e3472059972e3b5`, Apache-2.0. The official checkpoint URI is `https://bj.bcebos.com/v1/paddledet/models/rtdetr_r50vd_6x_coco.pdparams`; the documented export is: / 2026-08-08 审核固定如下版本与官方命令：

```text
python tools/export_model.py -c configs/rtdetr/rtdetr_r50vd_6x_coco.yml -o weights=https://bj.bcebos.com/v1/paddledet/models/rtdetr_r50vd_6x_coco.pdparams trt=True --output_dir=output_inference
```

Paddle's deployment documentation defines `im_shape:[None,2]`, decoded `bbox:[N,6]`, and per-image `bbox_num`; the RT-DETR reader uses direct 640x640 resize with `keep_ratio:false` and normalization mean 0/std 1 (`/255`). Paddle recommends ONNX 1.13.0 and Paddle2ONNX 1.0.5 for that documented conversion path. Exact commands and dependency versions that produced the local files were not recoverable, so the manifests do not claim that chain. / Paddle 官方文档定义上述输入输出与前处理；但本机文件的精确生成命令/依赖版本不可恢复，因此清单不声称这条转换链已验证。

The official RT-DETRv2 repository is pinned at `1c8ac3f7ba84f14bd5651ab7b1b70d69a5f55f47`, Apache-2.0. Its `tools/export_onnx.py` uses opset 16, a fixed 640x640 image shape, dynamic batch axes only, inputs `images`/`orig_target_sizes`, and outputs `labels`/`boxes`/`scores`. The R18 checkpoint URI is `https://github.com/lyuwenyu/storage/releases/download/v0.2/rtdetrv2_r18vd_120e_coco_rerun_48.1.pth` (release metadata size 81,198,974 bytes). The attempted isolated dependency/checkpoint acquisition did not complete, so no checkpoint SHA, ONNX SHA, executable v2 manifest, or backend success is asserted. / 官方 RT-DETRv2 固定在上述 commit。隔离依赖与 checkpoint 获取未完成，因此本阶段不伪造 checkpoint/ONNX SHA、可执行 manifest 或后端成功记录。

The audit also reviewed the official ONNX Runtime execution-provider/runtime documentation, OpenVINO model input/output metadata behavior, and Microsoft's current .NET TFM/RID documentation. Repository links are [PaddleDetection](https://github.com/PaddlePaddle/PaddleDetection), [RT-DETR/RT-DETRv2](https://github.com/lyuwenyu/RT-DETR), [ONNX Runtime](https://onnxruntime.ai/docs/), [OpenVINO](https://docs.openvino.ai/), and [.NET RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog). / 同日还核验了 ORT、OpenVINO 与 Microsoft TFM/RID 官方文档。

## Artifact and real-image evidence / 工件与真实图片证据

All local assets remain outside Git. The exact records are: / 所有本机资产仍位于 Git 外部，精确记录如下：

| Artifact / 工件 | Size / 大小 | SHA256 | Status / 状态 |
| --- | ---: | --- | --- |
| Failed decoded scalar ONNX | 169,225,428 | `6769a122fd045ab68e427f6651326dac8cac8d2983d43cd512a5e243fb13e94b` | Retained `Tile` failure evidence |
| Decoded vector-count ONNX | 169,228,279 | `a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db` | ORT CPU passed |
| Decoded vector-count IR XML | 1,556,096 | `9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d` | OpenVINO CPU passed with paired BIN |
| Decoded vector-count IR BIN | 85,633,634 | `c4f2ea6021314c23d691e5d6911da0804191202d049f3927cfa242f181600455` | Required sidecar |
| Raw-query ONNX | 169,218,784 | `544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad` | ORT/OpenVINO CPU passed |

The authorized input `E:\Data\image\bus.jpg` has SHA256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`. At strict threshold `0.4`, both decoded and raw contracts returned five ordered detections. ORT/OpenVINO category, score, box, order, threshold decision, source coordinates, and canonical fields matched within score tolerance `0.002` and box tolerance `0.25`. Canonical hashes are recorded in the manifests. One-run timings were decoded ORT `558.734 ms`, decoded OpenVINO `405.842 ms`, raw ORT `524.985 ms`, and raw OpenVINO `847.403 ms`; these are diagnostics, not P50/P95, throughput, memory, accuracy, or cross-machine claims. / 真实图在声明容差内完成字段级对齐；上述时间仅为单次诊断，不构成性能或精度结论。

## Diagnostics, TFM, RID, and native runtime / 诊断、TFM、RID 与 native runtime

Missing/duplicate/wrong-type named tensors, malformed scalar/vector counts, non-finite values, budget overflow, fixed/dynamic metadata mismatch, cancellation, use-after-dispose, and backend native-load failures map through existing stable Core/Visual diagnostics. The old `p2o.Tile.3` error remains an artifact-specific runtime failure; selecting a different explicit artifact profile is the repair, not shape/name guessing. / 具名 tensor、count、数值、容量、动态 shape、取消、释放与 native load 错误均沿用稳定诊断。旧 `p2o.Tile.3` 是工件特定失败；修复方式是选择另一个显式工件 Profile，而不是猜测 shape/name。

| Layer / 层 | Declared TFM / 声明 TFM | Verified stage-21 runtime / 本阶段运行 |
| --- | --- | --- |
| Core + Visual | `net46` through `net481`, `netstandard2.0`, `netcoreapp3.1`, `net5.0` through `net10.0` | Builds across declared matrix; domain contract is backend-neutral |
| Visual.OpenCV | Same except `netstandard2.0` | Windows x64 with `JYPPX.OpenCV.runtime.win-x64 5.0.0-preview.1` |
| ONNX Runtime adapter | `netstandard2.0`, `net8.0` | Windows x64 CPU, app installs `Microsoft.ML.OnnxRuntime 1.28.0` |
| OpenVINO adapter | Same executable TFM set as Visual.OpenCV | Windows x64 CPU, managed 3.3.0 plus application-selected runtime 2026.2.1 |

Other RIDs/devices remain unverified for this model family. The managed packages do not embed model, image, OpenCV, ORT, or OpenVINO native binaries. The package-only consumer at `tests/clean-consumer/visual-rtdetr` skips stably without external files and prints `DEPLOYSHARP_VISUAL_RTDETR_CONSUMER_OK` only after a real ORT CPU result. / 其他 RID/设备尚未核验；managed 包不内置模型、图片或 native。仅包消费者缺文件时稳定 skip，真实成功后才打印成功标记。

## Admission / 准入

Eight portable-detector manifests exist under `eng/models/detr/manifests`, including the retained failure record and three stage-21 runnable RT-DETR records. Every source has `redistributionAllowed:false`, every entry remains `External`, and the official catalog remains empty. Local execution closes V1 backend coverage but does not establish exact checkpoint/export provenance, artifact license chain, official predictor golden, redistribution permission, or `AlgorithmVerified`. No Release, tag, asset upload, catalog admission, or Actions dispatch occurred. / 八份清单均保持 External 与禁止再分发；本机执行关闭 V1 后端覆盖缺口，但不等于来源、许可、官方 golden、再分发或 `AlgorithmVerified` 已完成。未执行 Release、tag、上传、官方 catalog 写入或 Actions。
