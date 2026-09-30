# TensorRT PP-Structure table-cell matrix

日期：2026-09-30  
设备：Windows 11，NVIDIA GeForce RTX 3060 Laptop GPU（compute_86）  
运行时：TensorRT 11.0.0.114-cu12、CUDA 12.9、cuDNN 9.22、DeploySharp TensorRT 11 bridge  
输入：`E:\Data\image\bus.jpg`  
协议：5 次预热、50 次计时；动态输入 profile 为 `im_shape=[1,2]`、`image=[1,3,640,640]`、`scale_factor=[1,2]`。

## 结果

| 工件 | ORT/TensorRT NMS | ORT 高置信候选 | TensorRT 高置信候选 | 最大 score 差 | 最小匹配 IoU | P50/P95 |
|---|---:|---:|---:|---:|---:|---:|
| `paddle-table/rt-detr-l-wired-cell-det` | 300/300 | 300 | 300 | 0.00895 | 0.9285 | 21.605/24.613 ms |
| `paddle-table/rt-detr-l-wireless-cell-det` | 300/300 | 294 | 295 | 0.00701 | 0.9179 | 21.689/25.661 ms |

高置信候选使用 score `>=0.05` 统计。wireless 的 ORT/TensorRT 计数相差一个候选，属于该阈值附近的数值边界；参与 parity 的 294 个候选均满足 score 差 `<=0.03`、IoU `>=0.85`。候选通过全局 IoU 降序一对一匹配，避免高度重叠 cell 被数组顺序误配。

Engine SHA、ONNX SHA、构建耗时和机器可读字段见同目录的 [`paddle-document-tensorrt-table-cell-matrix-20260930.json`](paddle-document-tensorrt-table-cell-matrix-20260930.json)。

## 复现

```powershell
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:JYPPX_NATIVE_BRIDGE_PATH = '<repo>\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '11'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj -c Debug -f net10.0 --no-restore --filter 'FullyQualifiedName~PaddleDocumentTensorRtTableCellExternalIntegrationTests'
```

该记录证明精确 ONNX 工件能够由 DeploySharp Builder 构建并通过 TensorRT 推理、Paddle NMS Decoder 和 ORT 语义对照；它不代表表格单元格召回率、表格结构准确率，也不外推到其它 GPU、TensorRT 版本或未列出的 PP-Structure 工件。Engine 与 GPU/驱动/厂商运行时绑定，换设备必须重新构建和验证。
