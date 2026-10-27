# Table sample cell-score threshold (2026-10-27)

The user-facing table sample previously passed `scoreThreshold: 0` to the RT-DETR cell decoder. On the official `table_recognition.jpg`, this produced all 300 post-NMS candidates, including 287 with scores below `0.5`. The sample now defaults to `0.5` and accepts `--cell-score-threshold 0..1`; the existing diagnostic and benchmark records made with threshold zero remain unchanged.

| Input or model | SHA-256 |
| --- | --- |
| `table_recognition.jpg` (551x132) | `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| `pp-lcnet-x1-0-table-cls.onnx` | `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45` |
| `rt-detr-l-wired-cell-det.onnx` | `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` |
| `slanext-wired.onnx` | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` |

With local ONNX files and ONNX Runtime CPU, the default `--table-type auto` route classified the image as wired and returned 13 detector regions; the minimum retained score was `0.9646963`. The SLANeXt output remained 24 tokens, 13 structure regions and 165 HTML characters. The decoder reported `table-cell-geometry-outside-source:12/13`, so these structure polygons are not suitable for cell OCR binding. An invalid threshold of `1.2` was rejected before model execution. No OpenVINO, wireless, accuracy, or performance matrix run was repeated for this change.

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpResults\table `
  --table-type auto --backend ort --model-dir E:\Model\PaddleDocument\onnx
```

The `0.5` default is an example filtering choice, not a calibrated recall or accuracy threshold. Use labeled tables to choose a threshold for a deployment.
