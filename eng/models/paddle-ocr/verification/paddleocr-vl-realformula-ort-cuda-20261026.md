# PaddleOCR-VL 1.6 realFormula labelled Formula evaluation

Date: 2026-10-26. Source commit: `808aef7`. Model: pinned PaddleOCR-VL-1.6 four-graph FP32 ONNX export and official tokenizer at checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`. Backend: ONNX Runtime 1.23.2 CUDA on a Windows RTX 3060 Laptop 6 GB. This uses the production OpenCV encoded-image adapter with raw EXIF pixels, the production Visual session, greedy decoding and a 512-token output limit. The test uses one session and one pass per image; model loading is excluded from per-image timing.

The public realFormula v1 selection has 121 manually annotated formula images. The pinned manifest SHA-256 is `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`; annotation CSV SHA-256 is `b7a4679f7d9203f1040c3945aaceca5b73c3d110a70dd35487d4ec5462d8d6f1`. Every image is checked against its manifest SHA-256 before inference. Source: MathNet realFormula, Zenodo record [11296815](https://zenodo.org/records/11296815), CC-BY-4.0.

| Result | Observed |
| --- | ---: |
| Completed / selected images | 121 / 121 |
| End-of-sequence / output-limit / empty prediction | 121 / 0 / 0 |
| Exact match after removing Unicode whitespace | 0 / 121 |
| Corpus character error rate after removing Unicode whitespace | 55.1489% |
| Mean per-image character error rate | 60.0264% |

The character error rate uses Levenshtein distance on whitespace-stripped UTF-16 text divided by the reference length. It is a **string diagnostic, not formula semantic accuracy**: equivalent notation such as plain `P` versus `\mathbb{P}`, `1` versus `\mathbb{1}`, `\le` versus `\leq`, and optional `\left`/`\right` contributes edits. A CER above 100% is possible for an individual image when insertions exceed the reference length. The zero exact matches cannot be interpreted as zero mathematically correct formulas; nor can 121 EOS results be interpreted as recognition quality approval. Training-set overlap is unknown. This source run itself does not evaluate formula rendering or mathematical equivalence.

A subsequent [MathJax/MathML and restricted numeric diagnostic](paddleocr-vl-realformula-structure-20261026.md) evaluates this exact prediction report without changing its raw text or the CER above. It adds renderability and a small fail-closed numeric subset; it still does not establish general formula semantic accuracy.

The default CUDA provider run completed nine images and then failed on image 10 (`221008524-212.png`) while the Vision `/Softmax` node requested 1,625,702,400 bytes from ONNX Runtime's BFC arena. That image passed alone with EOS, so this is a continuous dynamic-shape memory boundary, not an unsupported image. The same first ten images passed `10/10` after the quality runner enabled the existing per-run provider configuration `memory.enable_memory_arena_shrinkage=gpu:0`; the complete 121-image run used that configuration. It releases unused CUDA arena regions after a run, but does not cap active allocations or guarantee a lower peak. A large input was observed with only about 12 MiB free during the run. This setting is opt-in at the library level, not a new global default or a general 6 GB GPU guarantee.

The complete per-image local report (including source image SHA, reference/prediction, termination and timings) is `E:\DeploySharpEvidence\paddleocr-vl-realformula-20261026\realformula-cuda-121-shrink.json`, SHA-256 `c991d56972625ff8688300a0671e9591025c0c9d6103434eab7797add98d9bd7`, 104,706 bytes. It records the Visual/OpenCV/test assembly SHA-256 values and four ONNX graph hashes. Images, annotations, predictions and the per-image report are not copied to Git or the model Release. The failed default run and the first-ten shrink probe are preserved beside it as separate local reports.

To reproduce on a compatible Windows CUDA 12.x host with the fixed four-graph export and official tokenizer, restore the CUDA test variant, then run the opt-in test:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_BACKEND = 'onnxruntime-cuda'
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT = 'F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public'
$env:DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT = 'E:\Model\PaddleOCR-VL\exports\dynamic-fp32-20261023'
$env:DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT = 'E:\Model\PaddleOCR-VL\1.6\c5630abae1d940eafe0697512a0325494b02ab42'
$env:DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT = '808aef7'
$env:DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_REPORT_PATH = 'E:\DeploySharpEvidence\paddleocr-vl-realformula-20261026\realformula-cuda-121-shrink.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -p:DeploySharpFormulaOrtCuda=true `
  --filter 'FullyQualifiedName~PaddleOcrVlRealFormulaQualityIntegrationTests.EvaluateAnnotatedRealFormulaCorpus'
```

The test defaults to arena shrink for CUDA. Set `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_CUDA_ARENA_SHRINK=0` only to reproduce the original failure. For a bounded probe, set `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT=10`, or select an exact comma-separated image list with `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_IMAGES`. These subsets must not be reported as corpus quality. A later [same-build ORT CPU/CUDA 121-image comparison](paddleocr-vl-realformula-ort-cpu-cuda-121-20261026.md) found one default-CUDA decoded-text difference; with explicit `use_tf32=0`, CPU and CUDA text matched on all 121. OpenVINO, TensorRT, OpenCV DNN inference, page-level quality, performance percentiles and long-run resource stability remain outside this evaluation.
