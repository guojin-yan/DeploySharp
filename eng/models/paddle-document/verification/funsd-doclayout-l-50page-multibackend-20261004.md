# PP-DocLayout-L FUNSD 50-page execution and parity

This record extends the earlier three-page FUNSD layout smoke without changing its accuracy boundary. The test selects up to 50 PNG pages when `DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT_MAX_PAGES=50`; the default remains the original three-page selection.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT_MAX_PAGES = '50'
dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  --configuration Release `
  --filter 'FullyQualifiedName~PaddleDocumentFunsdLayoutEvidenceTests'
```

The run used the local public FUNSD test pages under `F:\OCRBenchmarkTesting\data\images\funsd\test`, PP-DocLayout-L ONNX SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`, and the same 640x640 profile, score threshold `0.3`, and labels for both backends.

## Results

| Backend | Pages | Pages with regions | Region count | Min / max regions per page | Mean regions per page | Model-order top-left pages |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 50 | 50 | 1332 | 1 / 57 | 26.64 | 5 / 50 |
| OpenVINO CPU | 50 | 50 | 1332 | 1 / 57 | 26.64 | 5 / 50 |

The two raw reports contain the page image SHA-256, region labels, boxes, scores and model-order flags: [ONNX Runtime](funsd-doclayout-l-50page-onnxruntime-20261004.json) and [OpenVINO](funsd-doclayout-l-50page-openvino-20261004.json). Page detection counts and labels match `50/50`; 1332 corresponding regions have a maximum absolute difference of approximately `4.6e-4` across coordinates, sizes and scores. This is execution and numeric parity evidence for this exact host and model artifact.

## Boundary

FUNSD supplies word/entity annotations rather than aligned PP-DocLayout region labels. The run therefore does not calculate layout precision, recall, mAP or reading-order accuracy. Only `5/50` pages happen to be emitted in top-left order by the model; callers must apply an explicit reading-order policy or a downstream layout/document policy. This evidence does not close C3 or establish a general layout-quality claim.
