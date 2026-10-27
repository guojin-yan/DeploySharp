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

This is one official image on ORT CPU, not an annotated table accuracy or CER/WER measurement. The alignment remains conservative: rowspan, ambiguous rows, count/source mismatch or invalid boxes do not trigger OCR filling. OpenVINO, wireless tables, varied table layouts, and Release-download end-to-end execution were not rerun here; their component evidence cannot be silently promoted to this four-stage task. This run is not a 5+50 performance result.
