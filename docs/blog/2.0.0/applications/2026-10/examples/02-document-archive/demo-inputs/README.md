# 文档输入来源

本例保存 9 张 PaddleX 官方公开示例图，配合 `documents.json` 复现 4 组混合资料。默认输入包含正常页、倒置页、复杂表格、公式和不易识别的曲线文字，既展示成功输出，也展示复核路径。

| 本地文件 | 原始文件名 | 场景 |
| --- | --- | --- |
| `news-page.png` | `pp_structure_v3_demo.png` | 多栏报刊 |
| `layout.jpg` | `layout.jpg` | 复杂图文与表格混排 |
| `demo_paper.png` | `demo_paper.png` | 含公式的论文页面 |
| `general_formula_rec_001.png` | `general_formula_rec_001.png` | 单独公式 |
| `expense-table.jpg` | `table_recognition.jpg` | 中英文混合表格 |
| `img_rot180_demo.jpg` | `img_rot180_demo.jpg` | 180 度倒置页 |
| `general_ocr_001.png` | `general_ocr_001.png` | 现场文字一 |
| `general_ocr_002.png` | `general_ocr_002.png` | 现场文字二 |
| `seal_text_det.png` | `seal_text_det.png` | 印章曲线文字 |

原始 URL 为以下前缀加上表中原始文件名：

```text
https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/
```

图片权利及使用条件归原来源方。本例未提供人工标注，不能用运行数量推导准确率。费用表原图本身包含中英文混排和不完整单词；印章在本例走普通 OCR，低分和空文字进入复核。

自己的图片直接替换 `documents.json` 中的路径，或增加页面记录；相对路径从清单所在目录解析。
