# RF-DETR 分割与关键点真实推理

本工程通过 `ProjectReference` 使用**当前源码/未发布版本新增**的 API，目标为 .NET 10、Windows x64。它执行完整的图片读取、前处理、后端推理、内置解码和结果保存。基础库仍按各自 `.csproj` 的多个 TFM 构建；本样例的设备实测范围不代表所有平台。

## 先加载本地模型

先按[模型工具说明](../../../eng/models/rf-detr/README.md)导出，或者准备同一次导出的 `model.onnx`、`rfdetr.json`、`modelpack.json` 和许可证。从 Git 仓库根目录运行：

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/keypoint/rfdetr.json --backend ort --output ../results/rf-detr/keypoint

dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/segment-nano/rfdetr.json --backend ort --output ../results/rf-detr/segment-nano
```

未指定 `--image` 时，复用现有 `TestImageResolver`，自动下载并校验默认 `bus.jpg`；也可以设置 `DEPLOYSHARP_TEST_IMAGE_ROOT` 指向已有缓存。默认图上关键点得到 4 个 COCO-17 人体，分割得到 5 个实例。传入 `--image <图片路径>` 替换输入。`--model <onnx路径>` 可以改变模型位置，但必须匹配配置中的大小和 SHA-256；更换模型内容时应重新导出其合同。

样例验证 ONNX 身份；同目录有 `modelpack.json` 时，还通过库的 `ModelPackageLoader.Load` 核对所有部署文件。输出包括 `result.json` 和 `result.jpg`；分割额外保存每个实例的 `mask-XX.png` 与源图大小的 `mask-XX.u8`。关键点图只绘制 findability 分数大于 0.4 的点与边，完整 17 点仍保存在 JSON 中。Ctrl+C 触发取消，任何加载或验证失败返回非零退出码。

本工程固定 Managed/native ORT 1.23.2 的 CUDA 12 组合。构建命令的 `-p:DeploySharpPaddleOcrCuda12=true` 是仓库已有的依赖切换开关，需保留以将同一版本传递到后端项目；默认库依赖不因此升级或降级。还原锁文件：

```powershell
dotnet restore samples/06-models/rf-detr/RfDetrInference.csproj --locked-mode -p:DeploySharpPaddleOcrCuda12=true
```

## 切换后端与复现参考

`--backend` 接受 `ort`、`cuda`、`openvino`、`opencv`、`tensorrt`。ORT CUDA 必须有 CUDA 12 / cuDNN 9 DLL，并显式请求 CUDA provider；样例关闭 TF32。OpenVINO 使用 CPU。当前精确导出在 OpenCV DNN 5.0 导入 `Greater` 时失败，请用[已验证后端](../../../docs/model-backend-verification-matrix.md#rf-detr-1112-本地工件)。

```powershell
# 将本机安装目录中的 CUDA/cuDNN bin 加到当前进程 PATH 后运行。
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/keypoint/rfdetr.json --backend cuda --output ../results/rf-detr/keypoint-cuda --validate ../models/rf-detr-1.11.2/keypoint

dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/segment-nano/rfdetr.json --backend openvino --output ../results/rf-detr/segment-openvino --validate ../models/rf-detr-1.11.2/segment-nano --concurrency 2
```

`--validate` 要求导出时的同一张图片：自动检查图片大小/SHA-256、浮点输入、全部原始输出的 shape/有限值/容差，再比较官方 PyTorch 参考的数量、类别、分数、框、关键点或 mask。原始基线来自同一个 ONNX 的 ORT CPU。`--raw-input <input.f32>` 仅用于跳过图片前处理、隔离同张量误差；普通使用应保留默认图像入口。容差与实测最大误差见矩阵。

`--concurrency 2` 创建两个独立原生 Session，并用三份独立输入验证有序异步调度与预取消。每个 ONNX 的真实 Batch 仍固定为 1。

TensorRT 样例绑定 10.11 / CUDA 12.9 / cuDNN 9.22 的 Windows bridge。将本机安装根目录写入 `JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT`、`JYPPX_CUDNN_ROOT`，将对应 `bin` 加入 `PATH`；RTX 3060 的 `DEPLOYSHARP_CUDA_ARCHITECTURE` 为 `sm_86`，其它设备按实际架构设置：

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/segment-nano/rfdetr.json --backend tensorrt --output ../results/rf-detr/segment-tensorrt --validate ../models/rf-detr-1.11.2/segment-nano
```

未传 `--engine` 时，通过库的 `TensorRtOnnxEngineBuilder` 在目标 GPU 创建 FP32 Engine，关闭 TF32，workspace 256 MiB、optimization level 0；静态输入不设置动态 Shape Profile。`--engine <本机生成的model.engine>` 可复用同设备/同运行时缓存。Engine 不作为跨设备可移植模型分发。

## 独立模型案例

- [RF-DETR Keypoint Preview](../local-cases/rf-detr--keypoint-preview/README.md)
- [RF-DETR Seg Nano 1.11.2](../local-cases/rf-detr--segment--nano/README.md)
- [既有公开 RF-DETR Segment](../cases/rf-detr--segment/README.md)

库内 API、官方解码语义和 ROI 接法见[RF-DETR 教程](../../../docs/articles/visual-rf-detr.md)。新工件当前可从官方权重导出并本地加载；DeploySharp Release 自动下载尚未发布。
