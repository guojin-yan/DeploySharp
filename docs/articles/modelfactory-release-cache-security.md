# ModelFactory 下载与缓存

本文说明 `JYPPX.DeploySharp.ModelFactory` 的下载、缓存、离线和清理行为，适用于 `2.0.0-alpha.1`。ModelFactory 只负责选择和物化模型工件，不执行模型推理，也不安装 CUDA、TensorRT、OpenVINO 或其他厂商原生运行时。

## 下载边界

ModelFactory 只接受目录中声明的绝对 Release 资产地址。下载完成后会检查 ModelPack 清单、文件是否存在、相对路径是否安全、字节大小和 SHA256；校验未完成的临时文件不会对其他调用可见。应用仍应根据[模型支持指南](model-support.md)选择与后端匹配的工件。

网络请求对超时、`408`、`429` 和 `5xx` 使用有界重试，并遵循服务端的 `Retry-After`。其他 `4xx`、重定向、路径错误和完整性错误不会重试。调用方取消只结束当前等待，不会破坏其他调用正在使用的已验证下载。

## 缓存位置与离线模式

~~~csharp
var options = new ModelFactoryOptions(
    cacheRoot: @"D:\DeploySharpCache",
    requestTimeout: TimeSpan.FromMinutes(10),
    maximumRetries: 3);
using var factory = new ModelFactoryClient(catalog, options);
MaterializedModel model = await factory.GetModelAsync(
    selection, progress, cancellationToken);
~~~

应用选择缓存父目录，ModelFactory 只管理其下的 `.deploysharp-model-factory/v1` 命名空间。下载采用同目录临时文件、刷新和原子重命名，完成标记写入后才可被离线查询读取。

CLI 使用 `--cache <path>` 指定缓存，使用 `--offline` 强制只从已验证缓存读取。离线命中前会重新检查清单、大小、哈希、路径和完成标记；缺失或损坏时返回稳定的 `model-factory.offline-cache-miss`，不会偷偷联网回退。

## 路径和清理

包内文件使用正斜杠相对路径。根路径、UNC/盘符路径、空段、`.`、`..`、控制字符、保留设备名以及尾随点或空格都会被拒绝；规范化后的路径在整个包内必须唯一。加载器会检查包根和每一级路径，防止符号链接或重解析点逃逸到缓存命名空间之外。

`CleanCacheAsync` 可以按非活动时长、字节预算、目录修订或 Release 标签清理，也支持 dry-run。清理只作用于 ModelFactory 自己的命名空间，不会删除应用缓存根目录、同级目录或其他应用文件。

## 自有目录和运行时

应用可以通过 `ModelCatalogJsonSerializer` 加载自有目录，或用 `ModelCatalogClient.LoadAsync` 获取严格快照。目录中的 `compatibleBackends` 只用于匹配能力，不负责安装后端；`portable` 只说明工件是否设计为可跨兼容设备移动。

模型下载后仍需由应用选择后端并创建 Session。ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT Engine 和 LLamaSharp GGUF 的原生依赖、设备插件、Tokenizer 以及额外 sidecar 都必须按对应后端文档部署。
