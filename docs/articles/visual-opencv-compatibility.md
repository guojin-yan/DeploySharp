# Visual.OpenCV Compatibility / Visual.OpenCV 兼容性

## Verified matrix / 已核验矩阵

| Layer / 层 | Verified value / 已核验值 |
| --- | --- |
| Managed wrapper / 托管包装器 | `JYPPX.OpenCV.CSharp.API 5.0.0-preview.1` |
| Native package / Native 包 | `JYPPX.OpenCV.runtime.win-x64 5.0.0-preview.1` |
| Native library / Native 库 | `JYPPX.OpenCV.Native.dll` |
| OpenCV line / OpenCV 版本线 | `5.0.0` |
| DeploySharp package / DeploySharp 包 | Managed-only; no `runtimes/` or native files / 仅托管；无 `runtimes/` 或 native 文件 |
| Supported TFM / 支持 TFM | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` |
| Unsupported TFM / 不支持 TFM | `netstandard2.0` for this adapter because upstream has no asset / 因上游无资产，本适配器不支持 |
| Verified device / 已核验设备 | Windows x64 CPU image preprocessing / Windows x64 CPU 图像预处理 |

The preview wrapper has no GitHub Release at the time of this stage. Consumers must use the exact NuGet preview version and explicitly select a matching runtime package; no stable download link is advertised. / 本阶段执行时 preview 包尚无 GitHub Release。用户必须使用精确 NuGet preview 版本并显式选择匹配 runtime；文档不提供稳定下载链接。

OpenCV `Mat` is an owned native object. The adapter disposes decoded, converted, cropped, resized and padded Mats before returning. It copies every row using `Data` and `Step`, so non-contiguous input does not create an invalid managed view. / OpenCV `Mat` 是拥有 native 资源的对象。适配器在返回前释放解码、转换、裁剪、缩放和填充 Mat，并使用 `Data` 和 `Step` 复制每一行，因此非连续输入不会产生无效托管视图。

Only image input is in scope. OpenCV DNN, camera/video capture, GPU/NPU plugins and automatic runtime download are not part of this package. / 本包只负责图像输入。OpenCV DNN、摄像头/视频采集、GPU/NPU 插件和 runtime 自动下载不在范围内。
