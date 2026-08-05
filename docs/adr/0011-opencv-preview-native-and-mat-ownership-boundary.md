# ADR 0011: OpenCV preview, native separation and Mat ownership / OpenCV preview、native 分离与 Mat 所有权

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

`JYPPX.OpenCV.CSharp.API 5.0.0-preview.1` is an official preview package and now has a matching GitHub prerelease published on 2026-08-05, but no stable release. Its managed package publishes exact desktop TFMs and does not carry native files. The verified Windows x64 runtime is a separate package. / `JYPPX.OpenCV.CSharp.API 5.0.0-preview.1` 是项目官方 preview 包，现已有 2026-08-05 发布的匹配 GitHub prerelease，但仍无稳定版。托管包发布精确桌面 TFM，不携带 native 文件；已核验的 Windows x64 runtime 是独立包。

The wrapper exposes owned `Mat` objects and `Data`/`Step` pointers. The current public color conversion enum was verified for `BGR2GRAY`; RGB reorder and alpha composition do not have a verified public enum in this audit. / 包装器暴露拥有资源的 `Mat` 以及 `Data`/`Step` 指针。本次审计核验到的公开颜色转换枚举为 `BGR2GRAY`；RGB 重排和 alpha 合成没有已核验的公开枚举。

## Decision / 决策

`JYPPX.DeploySharp.Visual.OpenCV` references only the managed wrapper and `JYPPX.DeploySharp.Visual`. Applications choose the native runtime/RID explicitly. The package publishes the 15 exact upstream TFMs and does not claim `netstandard2.0`. CPU is the only verified execution environment. / `JYPPX.DeploySharp.Visual.OpenCV` 只引用托管包装器和 `JYPPX.DeploySharp.Visual`。应用显式选择 native runtime/RID。本包发布上游的 15 个精确 TFM，不声明支持 `netstandard2.0`。当前仅核验 CPU 环境。

`OpenCvImageSource` owns copied stream/byte data and rejects unsafe path boundaries. `OpenCvImageLoader` owns the decoded Mat. The factory may create temporary conversion, crop, resize and padding Mats, but copies rows into a DeploySharp tensor and disposes all native objects before returning. Core and Visual never see a Mat, pointer or vendor exception. / `OpenCvImageSource` 拥有复制后的流/字节数据并拒绝不安全路径边界。`OpenCvImageLoader` 拥有解码 Mat。工厂可以创建临时转换、裁剪、缩放和填充 Mat，但会把每行复制到 DeploySharp 张量，并在返回前释放全部 native 对象。Core 与 Visual 永远看不到 Mat、指针或 vendor 异常。

Cancellation is boundary-observed because the wrapper has no cancellable image operation; no `Task.Run` fallback is advertised. Three-channel grayscale calls the wrapper `BGR2GRAY`; other channel operations use a managed copy after stride validation. / 由于包装器没有可取消图像操作，取消只在边界观察，不声明 `Task.Run` fallback。三通道灰度调用包装器 `BGR2GRAY`；其他通道操作在 stride 校验后使用托管复制。

## Consequences / 影响

The package remains small and backend-neutral, while a user explicitly installing the native runtime gets real decoding. Preview status, native version diagnostics, unsupported RIDs, and the empty official ModelFactory catalog remain visible rather than being inferred from a package dependency. / 包保持小型且与后端无关，显式安装 native runtime 的用户可以真实解码。Preview 状态、native 版本诊断、不支持的 RID 以及空的官方 ModelFactory 目录都明确可见，不从传递依赖中暗示。
