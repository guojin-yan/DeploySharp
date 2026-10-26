# PaddleOCR-VL 1.6 ONNX CPU example

This example runs the pinned PaddleOCR-VL-1.6 Bundle through `PaddleOcrVlOnnxSession` and ONNX Runtime CPU. The [PaddleOCR Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) provides five ONNX graphs, four tokenizer/template files and a manifest as ten individual assets. The download client checks each size/SHA-256 and caches this Bundle separately from other PaddleOCR models. `CreateVerified` checks the manifest and all local files again before creating inference Sessions.

From the repository root, run OCR on your own image:

```powershell
dotnet run --project samples/02-visual/paddleocr-vl-onnx/PaddleOcrVlOnnx.csproj -c Release -- `
  --release E:\DeploySharpCache E:\Data\ocr\demo_1.jpg TextRecognition
```

The first run downloads approximately 4.66 GB of Bundle files and needs additional free space for the temporary downloads; model loading also requires substantially more RAM than the file size alone suggests. Later runs reuse the verified cache. To forbid network access, append `--offline`. To check the published assets and manifest without loading the large inference graphs or supplying an image, run:

```powershell
dotnet run --project samples/02-visual/paddleocr-vl-onnx/PaddleOcrVlOnnx.csproj -c Release -- `
  --release E:\DeploySharpCache --verify-only --offline
```

Other task names are `TableRecognition`, `FormulaRecognition`, `ChartRecognition`, `TextSpotting` and `SealRecognition`. The program prints raw generated text, token count, termination reason and generation time. It does not evaluate recognition accuracy or produce a complete page layout. `--bundle <bundle-root> <image-path> <task>` uses an already materialized four- or five-graph Bundle with `paddleocr-vl-bundle.json`; add `--verify-only` to validate its files without inference. Encoded image decoding and official patch preparation use `OpenCvPaddleOcrVlInputFactory`.

For a pinned upstream patch fixture, the original six-argument mode remains available without a Bundle manifest:

```powershell
dotnet run --project samples/02-visual/paddleocr-vl-onnx/PaddleOcrVlOnnx.csproj -c Release -- `
  E:\Model\PaddleOCR-VL\1.6\c5630abae1d940eafe0697512a0325494b02ab42 `
  E:\Model\PaddleOCR-VL\exports\dynamic-fp32-20261023 `
  E:\Model\PaddleOCR-VL\inputs\ocr-demo-1-pixel_values.bin `
  672 448 TextRecognition
```

The raw file must be little-endian `float32` patches in `[grid_h,grid_w,3,14,14]` order from the fixed official Processor. This mode preserves the original four-graph export-directory workflow; it does not imply Release provenance for arbitrary files.

The [live Release check](../../../eng/models/paddle-ocr/verification/paddleocr-vl-release-live-20261026.md) proves download, SHA and offline reuse, but did not rerun generation from its fresh cache. Separate [ORT CPU](../../../eng/models/paddle-ocr/verification/paddleocr-vl-windows-ort-20261025.md), [OpenVINO CPU](../../../eng/models/paddle-ocr/verification/paddleocr-vl-windows-openvino-20261025.md), [ORT CUDA](../../../eng/models/paddle-ocr/verification/paddleocr-vl-cuda-20261025.md) and [TensorRT](../../../eng/models/paddle-ocr/verification/paddleocr-vl-tensorrt-five-graph-20261026.md) reports state their exact fixed-task evidence and resource boundaries. This sample itself selects ORT CPU. Its ONNX Bundle does not contain portable TensorRT Engines; OpenCV DNN 5.0 cannot import the original dynamic vision/decoder graphs. Labelled quality, arbitrary Engine profiles, page-level composition and long-running stability remain open.

For image/region routing and multi-page output, see the [document example](../paddleocr-vl-document/README.md).
