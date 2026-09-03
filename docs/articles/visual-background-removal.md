# 背景移除（RMBG）

BRIA RMBG 返回连续的 Alpha 蒙版，而不是类别标签或二值分割结果。RMBG 1.4 和 RMBG 2.0 的输入尺寸、归一化、输出名称和动态 shape 不同，必须分别使用 Profile，不能用普通语义分割 decoder 替代。

## 版本合同

| 模型 | 输入合同 | 输出合同 | 典型边界 |
| --- | --- | --- | --- |
| RMBG 1.4 | 固定 NCHW、`1024x1024` | `output`，单通道 Alpha | 固定空间尺寸，适合静态 Engine |
| RMBG 2.0 | 动态 NCHW，空间尺寸受 Profile 限制 | `alphas`，单通道 Alpha | FP32 与 dynamic-int8 必须分别绑定 |

运行前应从实际工件确认输入/输出名称、opset、SHA-256 和输出布局。目录中的 `bria/rmbg-1.4`、`bria/rmbg-2.0` 条目只代表对应 ModelPack 可下载和完整性可校验，不代表两个精度变体可以互换。

## 最小推理

```csharp
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg14(
    new ModelId("bria/rmbg-1.4"),
    new BriaRmbgProfileOptions(
        opset: 11, modelSize: new VisualSize(1024, 1024),
        inputName: "input", outputName: "output",
        artifactSha256: modelSha256, upstreamCommit: upstreamCommit,
        exporterVersion: exporterVersion, license: licenseId));
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();
var artifact = profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId);
using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");
using var pipeline = new VisualPipeline(
    backends, profiles.Select(artifact, backends, request, VisualTaskId.ForegroundMatting), request);
using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
    imagePath, profile.VisualProfile.Input.Name,
    OpenCvStage19Preprocessing.CreateBriaRmbgOptions(profile));
BackgroundRemovalResult result = pipeline.Run(input).GetValue<BackgroundRemovalResult>();
byte[] alpha = result.Alpha.ToArray();
```

`RMBG 2.0` 使用 `BriaRmbgProfiles.CreateRmbg20`，并将输入/输出名称和实际 opset 改为工件声明的值。仓库 `samples/06-models/release-inference` 提供 ModelFactory 下载、绑定、推理和 PGM 输出的完整流程。

## Alpha 结果与合成

`BackgroundRemovalResult.Alpha` 是已还原到源图尺寸、范围为 `[0,1]` 的自有 `AlphaMask`。它可以用于前景合成、PNG 写出或后续抠图算法。`CompositeRgb` 会创建新的 RGB 缓冲；如果应用已有目标缓冲，应在业务层复用目标内存，避免每帧产生多份完整图像副本。`Transform` 保存缩放和填充信息，不能在结果外再次做几何变换。

## Batch、并发与后端

只有 Profile 的 `MaximumBatch` 大于 1 且输入、输出空间维度均声明动态时，才可使用真正的 `BackgroundRemovalBatchResult`。静态 RMBG 1.4 Engine 通常使用 batch-one；批量图片应创建有限的独立 Session 池，通过 `RunManyAsync` 排队。Session 数量应按显存和 Alpha 输出大小设置，不能无界增加。

TensorRT 视觉路径可在满足静态 Engine 合同的情况下执行设备侧归一化和 Alpha 后处理；不满足条件时会回退到标准 CPU decoder。ONNX Runtime、OpenVINO 和 OpenCV DNN 仍使用各自的后端路径，业务代码不应依赖 TensorRT 专属类型。

## 性能与内存

稳态测试应复用解码后的输入和 Session，分别记录预处理、后端推理、Alpha 还原和结果分配。RMBG 2.0 FP32 输出可能占用较大内存，建议设置 `AlphaDecoderOptions.MaximumPixels` 和 `MaximumWorkspaceBytes`，在进入 native 调用前拒绝超限输入。动态 batch 的总像素数、在途 Session 数量和输出缓存必须一起估算。

## 支持边界

当前公开目录包含 RMBG 1.4 和 2.0。dynamic-int8、OpenCV DNN importer 以及特定 TensorRT Engine 的状态以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。不同设备上的实测数据见[设备性能实测](device-performance-benchmarks.md)。
