# PP-Structure wireless table route, 2026-10-27

## Scope and inputs

Two independent PubTabNet demo images were taken from PaddleOCR commit
`dab3fe35379033fdcb2d0e9572fac0b36c9a9ebf`:

| Image | Upstream path | SHA-256 | Size |
| --- | --- | --- | --- |
| `pubtabnet-524509.png` | [`docs/datasets/images/table_PubTabNet_demo/PMC524509_007_00.png`](https://github.com/PaddlePaddle/PaddleOCR/blob/dab3fe35379033fdcb2d0e9572fac0b36c9a9ebf/docs/datasets/images/table_PubTabNet_demo/PMC524509_007_00.png) | `8ac6eb772c45be3f17f28ce565489b5895ab47002fdf5b34590d57f047041130` | 503 x 98 |
| `pubtabnet-535543.png` | [`docs/datasets/images/table_PubTabNet_demo/PMC535543_007_01.png`](https://github.com/PaddlePaddle/PaddleOCR/blob/dab3fe35379033fdcb2d0e9572fac0b36c9a9ebf/docs/datasets/images/table_PubTabNet_demo/PMC535543_007_01.png) | `9a92582a81a93937da737ef4b8d6980feede20b1acc4cc4db862bb16638c80ab` | 503 x 245 |

The Windows sample used local, SHA-recorded ONNX files. Its model directory contained NTFS hard links to the original files; no weights were rewritten.

| Model file | SHA-256 | Source |
| --- | --- | --- |
| `pp-lcnet-x1-0-table-cls.onnx` | `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45` | `E:\Model\PaddleDocument\onnx` |
| `rt-detr-l-wireless-cell-det.onnx` | `e141c8aa947ef0aea165c45d51caf6d2cccbca5f381ed9c8854f402570b296bf` | `E:\Model\PaddleDocument\onnx` |
| `slanext-wireless.onnx` (ORT) | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | `E:\Model\PaddleDocument\onnx` |
| `slanext-wireless-openvino-compat.onnx` (OpenVINO) | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | `E:\Model\PaddleDocument\onnx-normalized-rerun-20260929` |

`--table-type wireless` still executes PP-LCNet classification and rejects a wired prediction before loading the later models. The same `0.5` detector threshold was used throughout. The PP-OCRv5 Mobile REC model and dictionary were specified, but neither was loaded because alignment did not succeed.

## Focused runs

| Image | Backend | Classifier | Detected cells | Structure cells / tokens | Structure warning | Alignment / OCR |
| --- | --- | --- | ---: | ---: | --- | --- |
| `524509` | ORT CPU | `wireless_table` (0.9179115) | 44 | 45 / 67 | `table-cell-geometry-outside-source:41/45` | `detection-count-mismatch`; skipped |
| `524509` | OpenVINO CPU | `wireless_table` (0.9179114) | 44 | 45 / 67 | `table-cell-geometry-outside-source:41/45` | `detection-count-mismatch`; skipped |
| `535543` | ORT CPU | `wireless_table` (0.8991090) | 155 | 128 / 165 | `table-cell-geometry-outside-source:92/128` | `detection-count-mismatch`; skipped |

For `524509`, ORT and OpenVINO produced the same structure payload and byte-identical structure-only HTML, SHA-256 `ca2a3ec47f8108033caaef2b1947c325f7e3a9a724133dcfd5e56ad732d860e2`. The OpenVINO run used its separately identified compatibility graph, not the original Loop graph. The `535543` ORT HTML SHA-256 is `390570c6adb727b12c3843c4041ed0429633b3aec70509f3d239be9d61d71ca8`. Outputs remain in `E:\DeploySharpEvidence\paddle-structure-table-wireless-20261027\{ort-524509,openvino-524509,ort-535543}`.

## Focused detector-threshold diagnosis

The `524509` ORT route was inspected twice more to determine whether the `44` versus `45` mismatch was only a threshold-boundary issue. These were diagnostic runs, not a threshold-tuning benchmark:

| Detector threshold | Returned boxes | Alignment result | Interpretation |
| ---: | ---: | --- | --- |
| `0` | 300 | `detection-count-mismatch` | Exposes all decoder candidates and confirms that forcing an unfiltered list into the structure slots is invalid. |
| `0.4` | 45 | `overlapping-cells:0` | Count equals the 45 structure cells, but the first ordered cell has overlapping detection candidates, so one-to-one alignment remains ambiguous. |
| `0.5` (default) | 44 | `detection-count-mismatch` | Preserves the documented conservative default and skips cell OCR. |

At threshold `0`, candidate 44 has score `0.50865054` and spans `x=1.91, y=2.03, width=65.72, height=39.05`; it overlaps the top-left header geometry. Candidate 45 has score `0.4161623` and bounds `x=68.38, y=1.70, width=40.12, height=16.46`. Including the latter makes the raw counts equal but does not resolve the former overlap. The SLANeXt result is unchanged at 45 cells / 67 tokens with `table-cell-geometry-outside-source:41/45`.

The two additional output directories are `E:\DeploySharpEvidence\paddle-structure-table-wireless-20261027\ort-524509-threshold-zero` and `E:\DeploySharpEvidence\paddle-structure-table-wireless-20261027\ort-524509-threshold-040`. Their `table-cell-alignment.json` files record the exact refusal reasons. No recognizer Session was used because alignment did not succeed.

Reproduce the first ORT run with the linked model files above:

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release -f net8.0 --no-build --no-restore -- `
  --image E:\DeploySharpEvidence\paddle-table-wireless-samples-20261027\pubtabnet-524509.png `
  --output E:\DeploySharpEvidence\paddle-structure-table-wireless-20261027\ort-524509 `
  --model-dir E:\DeploySharpEvidence\paddle-structure-table-wireless-20261027\models `
  --recognizer-model E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_rec.onnx `
  --recognizer-dictionary E:\Model\paddleocr\PP-OCRv5\ppocrv5_dict.txt `
  --table-type wireless --backend ort
```

Change `--backend` to `openvino` and `--output` to `openvino-524509` for the second run; the model directory must also contain the named OpenVINO compatibility graph. The third run changes `--image` and `--output` to `535543`.

## Result boundary

This closes the independently classified wireless **three-stage execution** check on two real images and the ORT/OpenVINO structure comparison on one image. It does **not** validate wireless cell OCR, filled HTML, annotated table accuracy, CER/WER or steady-state latency. The strict aligner correctly refused to guess which detected box corresponds to a structure slot. The `0.4` diagnosis proves that equal counts alone are insufficient, so the default remains `0.5`; lowering the threshold or forcing positional assignment without annotated cell geometry would turn this into an unsupported OCR claim. The wired four-stage result remains documented separately in `paddle-structure-table-cell-ocr-20261027.md`.
