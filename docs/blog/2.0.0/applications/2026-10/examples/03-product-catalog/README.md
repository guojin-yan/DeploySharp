# 智能素材工厂：RMBG + CLIP + OCR 商品图处理

对应[应用系列第 03 篇](../../2026-10-29-03-product-catalog.md)。正式 NuGet 2.0.0，使用 RMBG 1.4、CLIP 图文双编码器、PP-OCRv5 Mobile，输出透明 PNG、白底图、类别候选、OCR 文字、CSV/JSON 和可搜索静态图册。

需要 .NET 10 SDK、x64。与[文档归档工程](../02-document-archive/README.md)使用同组 ORT/OpenVINO 托管与平台原生包。`.csproj` 已配置 Windows、Ubuntu 22.04 原生文件，直接还原即可。

## 模型与输入

三张商品照片随工程提供，来源见 [demo-inputs](demo-inputs/README.md)。模型和词表保存到 `models`：

```powershell
New-Item -ItemType Directory -Force models | Out-Null
$visual = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1'
foreach ($file in @('bria-rmbg-1.4.onnx','clip-vit-b-32.image-encoder.onnx','clip-vit-b-32.text-encoder.onnx',
  'clip-vit-b-32.vocab.json','clip-vit-b-32.merges.txt','clip-vit-b-32.LICENSE')) {
  Invoke-WebRequest "$visual/$file" -OutFile "models/$file"
}
$ocr = 'https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
foreach ($file in @('ppocrv5-mobile-det.onnx','ppocrv5-mobile-rec.onnx','ppocrv5_dict.txt')) {
  Invoke-WebRequest "$ocr/$file" -OutFile "models/$file"
}
```

Ubuntu：

```bash
mkdir -p models
visual='https://github.com/guojin-yan/DeploySharp/releases/download/models-visual.1'
for file in bria-rmbg-1.4.onnx clip-vit-b-32.image-encoder.onnx clip-vit-b-32.text-encoder.onnx \
  clip-vit-b-32.vocab.json clip-vit-b-32.merges.txt clip-vit-b-32.LICENSE; do
  curl -fL "$visual/$file" -o "models/$file"
done
ocr='https://github.com/guojin-yan/DeploySharp/releases/download/models-paddleocr'
for file in ppocrv5-mobile-det.onnx ppocrv5-mobile-rec.onnx ppocrv5_dict.txt; do
  curl -fL "$ocr/$file" -o "models/$file"
done
```

已有文件可以直接复制。CLIP 使用附带 MIT 许可；PP-OCR 使用 Apache 2.0；BRIA RMBG 1.4 使用自己的模型条款，请按实际用途确认：https://huggingface.co/briaai/RMBG-1.4 。项目代码的 Apache 2.0 不替代模型和图片的许可。

## 运行

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release --no-build -- --backend ort-cpu --manifest products.json
dotnet run -c Release --no-build -- --backend openvino --manifest products.json
```

Ubuntu 构建后：

首次构建可能出现 `NETSDK1206`（旧 Ubuntu RID）警告；工程已显式复制原生文件，下面的搜索路径指向这些文件。

```bash
export LD_LIBRARY_PATH="$PWD/bin/Release/net10.0/native/opencv:$PWD/bin/Release/net10.0/native/openvino:${LD_LIBRARY_PATH:-}"
dotnet bin/Release/net10.0/ProductCatalog.dll --backend ort-cpu --manifest products.json
dotnet bin/Release/net10.0/ProductCatalog.dll --backend openvino --manifest products.json
```

其他参数：`--models models`、`--out results`。打开 `results/<backend>/index.html`。退出码：完成 `0`、运行失败 `1`、取消 `2`、有待复核项 `3`。默认鞋和腕表包含低分 OCR，因此返回 `3`，结果仍完整生成。

## 自己的商品配置

`products.json` 定义 `Candidates`、`MinimumTextConfidence` 和 `Items`：

- 候选 `Key/Text` 是英文类别和英文描述。示例 tokenizer 支持可打印 ASCII 英文；描述改变后候选组内分数会改变。
- `MinimumTextConfidence` 默认为 `0.8`，只将主体上的非空高分文字写入 `product.txt`。全部 OCR 原样保存在 JSON。
- 商品 `Id/Title/Image/ExpectedCategory` 指定唯一 ID、标题、原图和可选预期类别。图片路径相对配置文件目录解析，ID 使用小写字母、数字和短横线。

程序复用模型会话；候选文本编码一次。RMBG 的 Alpha 确定带少量边距的主体范围，CLIP 从原图该范围提取特征；OCR 读取原图，按文字框中心点的 Alpha 标记归属。该启发式对孔洞、阴影和复杂背景可能有误，全部文字均可复核。

每商品保存 `source.jpg/transparent.png/white.jpg/alpha.png/product.json/product.txt`，整批保存 `catalog.csv/catalog.json/index.html`。分类只是候选检索，OCR 不做品牌、价格或 SKU 解析。默认三个公开样例在 Windows 和 Ubuntu 的 ORT/OpenVINO CPU 跑通，三个商品的类别在四组运行中一致；不是商品数据集准确率评测。
