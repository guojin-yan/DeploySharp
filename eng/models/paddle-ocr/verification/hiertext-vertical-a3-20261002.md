# HierText natural vertical-line A3 evidence (2026-10-02)

This record uses the 18 non-empty HierText line crops whose official source annotation has `text_direction=vertical`. It is a natural, source-linked orientation label, not a rendered or rotated image. The crop text, parent image/instance IDs and source SHA-256 values are retained in the two backend JSON records.

## Protocol

- Source: the local HierText validation selections under `F:\OCRBenchmarkTesting`, pinned annotation revision `70b6620b2b112597d8219e11eee9773a1403827c`.
- Model: PP-OCRv6 Small REC; detector profile is loaded to construct the normal pipeline contract, while this test isolates `RecognizeOnlyAsync` on parent-linked text-line crops.
- Backends: ONNX Runtime CPU and OpenVINO CPU.
- Candidates per crop: `Degrees0`, `Clockwise90`, `CounterClockwise90`.
- Selection shown below is an **oracle diagnostic**: the candidate with the lowest ground-truth CER is selected only to expose the value of the correct orientation. It is not an automatic orientation classifier and must not be used as a production policy.
- No image or annotation is copied into a Release; the committed JSON contains text/SHA/provenance and no pixel data.

## Results

| Backend | Rows | Oracle exact (case-folded CER=0) | Mean oracle CER | Selected 0° | Selected clockwise 90° | Selected counter-clockwise 90° | Text-candidate parity |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 18 | 6/18 | 46.3356% | 7 | 10 | 1 | baseline |
| OpenVINO CPU | 18 | 6/18 | 46.3356% | 7 | 10 | 1 | 18/18 candidate rows identical |

The two backend records have identical candidate text, selected orientation and selected CER for all 18 rows. The model was able to recover the direction of the clearest vertical examples (for example `WORLD CUP` selects clockwise 90° with case-folded CER `11.11%`), but the aggregate oracle result is only `6/18` exact and therefore does not support an accuracy claim or an automatic routing default.

## Interpretation and remaining A3 gate

This closes a natural **vertical-label execution slice**: the pipeline accepts source-linked vertical crops, all three explicit orientation candidates execute on both CPU backends, and the result contract is reproducible. It does not close A3. HierText still has no upright-versus-inverted class, no continuous baseline-angle label, no perspective target and no automatic direction classifier ground truth. The earlier rotation-coverage audit also shows that the cached nonzero Open Images metadata has no intersection with the official HierText annotation IDs. A3 therefore remains open for a legally usable natural or explicitly labeled angle set plus a production selection policy, repair rate, CER/WER, rejection rate and coordinate-error evaluation.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL = '1'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL_MAX = '18'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL_OUTPUT = (Resolve-Path '.\artifacts\hiertext-vertical-20261002').Path
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrHierTextVerticalIntegrationTests'
```

Raw records:

- [ONNX Runtime JSON](hiertext-vertical-a3-20261002-onnxruntime.json)
- [OpenVINO JSON](hiertext-vertical-a3-20261002-openvino.json)

The test itself is [PaddleOcrHierTextVerticalIntegrationTests.cs](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrHierTextVerticalIntegrationTests.cs).
