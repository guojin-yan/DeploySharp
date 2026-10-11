# PP-OCRv4 core TensorRT 11 build evidence

在显式配置匹配的 TRT11 bridge、TensorRT 11.0.0.114、CUDA 12.9 和 cuDNN 9.22 后，DeploySharp `TensorRtOnnxEngineBuilder` 从 ONNX 重新创建并加载 PP-OCRv4 Server DET、Server REC、legacy CLS 三个 Engine；测试 `1/1` 通过，三项均返回非空输出。

| 模型 | Engine SHA-256 | 输入 | 输出元素 |
| --- | --- | --- | ---: |
| `paddleocr/ppocrv4/server-det` | `cdef038a0c1196286deb01081e7a6bd7ea69d22725829f2ccba65adaed1fd006` | `x[1,3,736,736]` | `541696` |
| `paddleocr/ppocrv4/server-rec` | `1c7fb688f5039873430d0872c3ce773d7d6776fc7bc53e1a06fa755e46439ff6` | `x[1,3,48,160]` | `132500` |
| `paddleocr/ppocrv4/legacy-cls` | `ba3dc5ff7c90c7645d7c22df2d15ee2591ac915b808db02fc0c97cfd942ec7b7` | `x[1,3,48,192]` | `2` |

复现：设置 `DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1`、`JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT`、`JYPPX_CUDNN_ROOT` 后运行：

```powershell
dotnet test .\tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj `
  -f net10.0 -c Release --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrV4OnnxModelsBuildAndRunTensorRt11Engines'
```

这关闭的是三项精确 ONNX→Engine→provider smoke 缺口；不代表完整 `det → crop → cls → rec` 质量、5/50 性能、跨设备 Engine 可移植性或发布准确率。

机器详情见 [`paddleocr-v4-core-tensorrt11-build-20261011.json`](paddleocr-v4-core-tensorrt11-build-20261011.json)。
