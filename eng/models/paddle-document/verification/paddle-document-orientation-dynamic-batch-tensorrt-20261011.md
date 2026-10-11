# PP-LCNet 文档方向 TensorRT 11 动态 Batch

本记录完成 `paddle-doc/pp-lcnet-x1-0-doc-ori` 的真实 TensorRT 动态 Batch=2 合同。测试使用官方方向示例 `img_rot180_demo.jpg` 的上下两个 ROI，构成 `[2,3,224,224]`，由 ORT CPU 和 DeploySharp TensorRT 11 Builder/Provider 分别运行并交给同一个方向分类 Decoder。

| 项目 | 结果 |
| --- | --- |
| ONNX SHA-256 | `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0` |
| 输入 | `[2,3,224,224]`，上下两个 ROI |
| ORT/TensorRT 输出 | `[2,4]` |
| 两行标签 | 均为 `180_degree` |
| 最大 raw score drift | `9.8109245e-05`（合同容差 `0.01`） |
| Engine | DeploySharp Builder，SHA `02c1dbae8058ae6a41a8f63bbde57f6abbfbd5df3d92d741fbcc80d5cf5786a2`，`8,149,228` bytes |
| 测试 | `1 passed / 0 failed / 0 skipped` |

两行输入 SHA 不同，结果按 batch 顺序返回；这验证了动态 profile、行映射、方向 Decoder 和 ORT/TensorRT 的有界数值一致性。测试设备为 Windows 10.0.26200 x64、.NET 10.0.12、RTX 3060 Laptop、TensorRT 11.0.0.114-cu12、CUDA 12.9、cuDNN 9.22。

机器可读结果见 [JSON 报告](paddle-document-orientation-dynamic-batch-tensorrt-20261011.json)。复现时设置 `DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1` 并注入 `JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT` 和 `JYPPX_CUDNN_ROOT`，运行：

```powershell
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj -c Release --no-restore --framework net10.0 --filter FullyQualifiedName~DocumentOrientationRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows
```

该记录是一个精确 PP-LCNet 方向工件的执行/行隔离合同，不代表方向准确率、性能排名、其它 PP-Structure 模型或跨设备 TensorRT 支持。
