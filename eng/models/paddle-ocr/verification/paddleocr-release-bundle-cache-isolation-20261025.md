# PaddleOCR 多文件 Bundle 资产命名与缓存隔离

日期：2026-10-25。范围是 `PaddleOcrReleaseClient` 的 Bundle 下载、校验和离线缓存合同；本次没有上传模型或修改 GitHub Release 资产。

## 修复内容

同一个 GitHub Release 的资产名必须全局唯一，但 Chart2Table 与 PaddleOCR-VL 的本地运行时都需要 `tokenizer_config.json`、`tokenizer.json` 等逻辑文件名。旧实现把所有 Bundle 资产写到 `.deploysharp-paddleocr/<tag>/` 根目录，后下载的文件会覆盖前一个 Bundle，或因 SHA 不同导致之前的 Bundle 无法离线复用。

现在 catalog 中的 Bundle artifact 区分：

- `assetName`：Release 下载 URL 使用的全局唯一文件名，例如 `paddleocr-vl-1.6-tokenizer-config.json`；
- `localName`：Bundle 隔离目录内的逻辑文件名，例如 `tokenizer_config.json`。缺省时沿用 `assetName`，兼容已有 Chart2Table catalog。

Bundle 文件保存到：

```text
<cacheRoot>/.deploysharp-paddleocr/<tag>/bundles/<safe-bundle-id>-<sha-prefix>/
```

每个 Bundle 的 `TokenizerDirectoryPath` 和 `GetRequiredPath(role)` 都指向自己的目录。Catalog 加载时还会检查跨 Bundle 重用的远端 `assetName`：同名资产只有在 size/SHA 完全一致时才允许共享，否则直接拒绝并要求发布方改用 Bundle 前缀名。

VL 专用合同继续要求 `localName` 为 `vision.onnx`、`embedding.onnx`、`decoder.onnx`、`head.onnx`、四个 tokenizer 文件和 `paddleocr-vl-bundle.json`，所以远端资产可以安全加前缀而不改变 C# Bundle 入口。

## 验证

| 检查 | 结果 |
|---|---|
| PaddleOCR Release Client 定向测试 | `6 passed / 0 skipped / 0 failed` |
| ModelFactory 全部测试 | `66 passed / 2 optional skipped / 0 failed` |
| ModelFactory Release 编译 | `netstandard2.0/net8.0/net9.0/net10.0`，`0 warning / 0 error` |
| 缓存隔离测试 | Chart2Table 与 VL 使用不同 Bundle 目录；两份同名本地 tokenizer 配置内容保持独立；关闭网络后均可按原 SHA 复用 |
| 工作树格式检查 | `git diff --check` 通过 |

测试输出和 TRX 保存在外部目录：`E:\DeploySharpEvidence\paddleocr-release-cache-20261025`。新增回归使用伪资产，证明的是下载器/缓存合同；不代表 PaddleOCR-VL 四个 ONNX 图已上传或 Release 可下载。

本轮产物 SHA-256：netstandard2.0 `c2ba6459c32479a8778fca89a4428e687af53174692dc8b2c2d78b929792c90c`、net10.0 `1742c2b7aebee0a885e79643256b4e0fbaea69152dd71b1adae6be4543cdf5ee`。定向 TRX `1a4171e4566c70c13c207ca4571b1c77922f1eaff3b09f3e96203d067a39d8b9`，全量 TRX `4138a882859779ba95c2673b7995b36d81447596b97fb71c54a180e631d910d7`。

## 发布方约定

VL 资产进入 `models-paddleocr` 时，应在 catalog 使用带模型前缀的远端文件名，并为每个 artifact 写入 `localName`。例如：

```json
{
  "role": "tokenizer-config",
  "assetName": "paddleocr-vl-1.6-tokenizer-config.json",
  "localName": "tokenizer_config.json",
  "format": "json"
}
```

实际四图、tokenizer、manifest 的大小和 SHA 必须来自同一导出批次；代码合同不会把伪资产或只有 catalog 的记录当作模型执行证据。
