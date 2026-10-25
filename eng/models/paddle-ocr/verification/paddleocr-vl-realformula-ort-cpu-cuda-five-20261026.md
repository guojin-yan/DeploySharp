# PaddleOCR-VL 1.6 realFormula ORT CPU/CUDA five-image comparison

Date: 2026-10-26. Source commit: `3e54ba0`. This is a **five-image backend consistency slice**, not an evaluation of the 121-image corpus. The first five entries of the pinned realFormula v1 manifest were executed once each with the production raw-EXIF OpenCV input factory and `PaddleOcrVlOnnxSession`. Both runs used the same four FP32 ONNX graphs, official 1.6 tokenizer, labels, .NET 10.0.12 test build and ONNX Runtime 1.23.2. The CUDA run explicitly enabled per-run `memory.enable_memory_arena_shrinkage=gpu:0`; CPU did not use that GPU setting. The test process was relaunched for each backend.

| Check | Result |
| --- | ---: |
| Images completed / EOS / truncated / empty | CPU 5/5, CUDA 5/5; 5/5 EOS on each |
| Exact generated text across backends | 5/5 |
| Generated Token count / finish state across backends | 5/5 each |
| Character edit count against the same annotation | 5/5 |
| Strict whitespace-stripped formula exact match with annotation | 0/5 on either backend |
| Five-image aggregate character CER | 50.5721% on either backend |

The comparison validates identical **decoded text and Token counts**, not individual Token ID parity or intermediate tensor equality; those fields are not stored in the quality reports. It checks source image SHA, annotation text, data manifest/CSV SHA, graph/tokenizer hashes, source commit, Visual/OpenCV/test assembly SHA and relevant protocol fields before joining results. The independent second-image diagnostic already identified a semantic difference (`+1` outside rather than inside a square root); both backends produce that same prediction. Backend agreement does not make the prediction correct.

| Image | Generated Tokens | CPU generation (ms) | CUDA generation (ms) |
| --- | ---: | ---: | ---: |
| `220808471-63.png` | 95 | 55,637.1 | 31,724.4 |
| `220312373-25.png` | 25 | 9,599.5 | 1,140.1 |
| `230110053-1.png` | 78 | 8,699.1 | 3,102.4 |
| `210508113-24.png` | 80 | 23,976.8 | 4,190.1 |
| `220511982-14.png` | 37 | 6,429.2 | 1,130.3 |

These are one-pass generation observations, excluding model load and caller preprocessing. There was no warm-up, clock control or repeated sampling; they must not be used as P50/P95 or a backend speed ranking. The first image is substantially larger and the CPU/CUDA processes were run sequentially under uncontrolled machine load.

The raw reports stay outside Git at `E:\DeploySharpEvidence\paddleocr-vl-realformula-20261026`:

| File | SHA-256 |
| --- | --- |
| `realformula-ort-cpu-5.json` | `44679b33c5ca8db50edd260401aa6b82143ba50690071e7248c74490771b4073` |
| `realformula-ort-cuda-5-current.json` | `fd547d1b5b31874ac505072912e7bc637515861c309ee6bc9aafbed070849946` |
| `realformula-cpu-cuda-5-comparison.json` | `5ba19fd4ef7d0b0901b9f6d69f38424d135c374052b8a078ad92b3902587e4eb` |

The two runs share Visual assembly SHA `cc3f5fdb51f8f40cd8aed1136bb70dd01fcd878524dc7e98a7a9f424bbc7f96e`, OpenCV assembly SHA `a1a2a15ac28ff04c0f32e57e4c374ea35b31f419e8e6174a240dd8f14c517496`, and test assembly SHA `9c6acb87805ecfc9e7b3ebf6986a6f0c4c294b89b146828717003a4f6630e2f7`. The comparison script reports `decision-parity`; it rejects same-backend input and an older 121-image CUDA report with a different selection/build. It does not copy images, labels or prediction text into the repository.

To reproduce, set `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RUN=1`, `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT=5`, the dataset/model/Bundle paths and a distinct `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_REPORT_PATH` for each run as in the [full CUDA report](paddleocr-vl-realformula-ort-cuda-20261026.md). Run the opt-in `EvaluateAnnotatedRealFormulaCorpus` test with `-p:DeploySharpFormulaOrtCuda=true` once for `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_BACKEND=onnxruntime-cpu` and once for `onnxruntime-cuda`. Then compare the two completed JSON reports:

```powershell
& eng/models/paddle-ocr/scripts/Compare-PaddleOcrVlRealFormulaBackends.ps1 `
  -ReferenceReportPath <cpu-report.json> `
  -CandidateReportPath <cuda-report.json> `
  -OutputJsonPath <comparison.json>
```

The later [121-image ORT CPU/CUDA comparison](paddleocr-vl-realformula-ort-cpu-cuda-121-20261026.md) supersedes this five-image slice for cross-backend decoded-text coverage; default CUDA differed on one image, while the explicit TF32-off run matched CPU on all 121. OpenVINO CPU/TensorRT full-corpus parity, formula semantic quality, page-level quality and controlled performance remain open. OpenVINO was not run on this 16 GB host for the annotated slice: prior six-task runs approached 12 GiB process peak while current free physical memory was only about 4 GiB. No unsupported-backend conclusion follows from that resource decision.
