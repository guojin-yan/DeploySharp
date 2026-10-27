# PP-Structure table-cell OCR, 2026-10-27

## Scope

The Windows table sample now runs PP-LCNet classification, RT-DETR cell detection, SLANeXt structure decoding, and PP-OCRv5 Mobile recognition over independently detected cell boxes. The recognizer-only API creates no detector Session. An OpenCV submatrix view shares the already decoded page pixels; no second image decode or full-image copy is needed. Only a fully successful source/count/row-geometry alignment permits OCR text to enter HTML. `--structure-only` preserves the prior three-model path.

## Reproduction

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release --no-restore -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpEvidence\paddle-structure-table-cell-ocr-20261027 `
  --model-dir E:\Model\PaddleDocument\onnx `
  --recognizer-model E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_rec.onnx `
  --recognizer-dictionary E:\Model\paddleocr\PP-OCRv5\ppocrv5_dict.txt `
  --backend ort
```

- Input image SHA-256: `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` (`551x132`).
- Recognizer ONNX SHA-256: `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9`; dictionary SHA-256: `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b`.
- Table classification: `wired_table`; 13 high-score detector cells at threshold `0.5`; 24 structure tokens and 13 structure cells. SLANeXt still reports `table-cell-geometry-outside-source:12/13`, so its own boxes are not used for OCR crops.
- Alignment: four rows, 13/13 matches. The recognizer returned 13 entries, including one empty cell; filled HTML preserved the `colspan="4"` header and row order. The local `table-cell-ocr.json` SHA-256 is `64b31aecd9d7f9ca13b4dd4833f7c1ab7643dce725b68da079eabb7c3460053e`; `table-result.html` SHA-256 is `d1b13e0ea842eb4ba3a9e077b260304cffd701dfdabc86b7d1cd08957409708c`.
- Focused contracts: `PaddleDocumentTableCellAlignmentTests` plus recognizer-only constructor `7/7`; OpenCV shared-image view `1/1`. The sample Release/net8.0 build completed with zero warnings and errors using `SuppressTfmSupportBuildWarnings=true`.

## OpenVINO CPU follow-up

The same `table_recognition.jpg` was run once through all four stages with `--backend openvino`. The local model directory `E:\DeploySharpEvidence\paddle-structure-table-openvino-20261027\models` contains NTFS hard links, not model copies, to the following files:

| Role | Source SHA-256 |
| --- | --- |
| `pp-lcnet-x1-0-table-cls.onnx` | `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45` |
| `rt-detr-l-wired-cell-det.onnx` | `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` |
| `slanext-wired-openvino-compat.onnx` | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` |

The classifier and cell detector came from `E:\Model\PaddleDocument\onnx`; the independently derived SLANeXt compatibility graph came from `E:\Model\PaddleDocument\onnx-normalized-rerun-20260929`. The recognizer and dictionary paths and SHA-256 are the same as the ORT run above.

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release --no-build -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpEvidence\paddle-structure-table-openvino-20261027\result `
  --model-dir E:\DeploySharpEvidence\paddle-structure-table-openvino-20261027\models `
  --recognizer-model E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_rec.onnx `
  --recognizer-dictionary E:\Model\paddleocr\PP-OCRv5\ppocrv5_dict.txt `
  --backend openvino
```

OpenVINO returned `wired_table`, 13 cells, 24 structure tokens, the same `12/13` structure-box warning and successful 13/13 alignment. The 13 ordered OCR strings and detection indices match ORT exactly; maximum absolute confidence difference is `9.662e-5`. The final HTML is byte-identical, SHA-256 `d1b13e0ea842eb4ba3a9e077b260304cffd701dfdabc86b7d1cd08957409708c`. OpenVINO `table-cell-ocr.json` SHA-256 is `2b1631be67dccacc6cdcdd338226157cde69d4f88d02426de05527e3ab96a59a`. The JSON differs because it records the backend and raw confidence values.

These are two backend runs on one official wired image, not annotated table accuracy, CER/WER, a wireless-table result, or a 5+50 performance measurement. The alignment remains conservative: rowspan, ambiguous rows, count/source mismatch or invalid boxes do not trigger OCR filling. Varied table layouts and Release-download end-to-end execution remain unverified for this four-stage task; component evidence must not be promoted to those claims.

## Release catalog and offline cache path

The same wired image was run through the sample's Release-client path with `--offline`, using an isolated cache at `E:\DeploySharpEvidence\paddle-structure-table-release-fresh-20261027\cache`. The catalog was fetched by the client from the `models-paddleocr` Release; `SHA256SUMS` was fetched with `gh release download`. The five model/dictionary files were seeded as NTFS hard links to the already SHA-verified local originals, not downloaded by this run. The seven remote Release asset digests (catalog, checksum list, classifier, cell detector, SLANeXt, recognizer and dictionary) were checked with the GitHub Release API and matched their local SHA-256 values. This is an **offline Release materialization test**, not a fresh end-to-end asset download.

```powershell
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release -f net8.0 --no-build --no-restore -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpEvidence\paddle-structure-table-release-fresh-20261027\offline-result `
  --cache E:\DeploySharpEvidence\paddle-structure-table-release-fresh-20261027\cache `
  --offline --backend ort
```

The command exited `0`: 13 detected cells, 13 structure slots, 13/13 alignment and `table-cell-ocr.json` status `completed`. All 13 ordered texts and detection indices match the earlier local-model ORT run. The HTML SHA-256 is again `d1b13e0ea842eb4ba3a9e077b260304cffd701dfdabc86b7d1cd08957409708c`; the OCR JSON SHA-256 is again `64b31aecd9d7f9ca13b4dd4833f7c1ab7643dce725b68da079eabb7c3460053e`.

An earlier online run in that previously empty cache downloaded the catalog and 6,772,807-byte classifier, then was cancelled while the 129,311,709-byte cell detector was still incomplete. A separate 1,024-byte range GET returned HTTP 206, and the remote asset's SHA-256 metadata matched the local original; neither observation proves that the complete large-asset transfer works on this connection. Full fresh-cache download of all four stages remains open. The first offline attempt also correctly failed at the recognizer because `SHA256SUMS` had not yet been cached; after fetching that real Release metadata, the identical command completed. No model execution or production code change was needed for this cache-completeness issue.
