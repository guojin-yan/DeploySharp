# PaddleOCR-VL 1.6 realFormula ORT CPU/CUDA 121-image comparison

Date: 2026-10-26. This compares one fixed PaddleOCR-VL-1.6 four-graph FP32 ONNX export, official tokenizer and the 121 labelled realFormula v1 images on Windows 10.0.26200 / .NET 10.0.12 / ONNX Runtime 1.23.2. CPU and CUDA runs each used a separate process, the production raw-EXIF OpenCV input factory, greedy FormulaRecognition with at most 512 new tokens, one Session and one pass per image. CUDA used per-run GPU arena shrinkage. The comparison tool rejects different dataset, graph/tokenizer, source-commit, assembly or protocol identities before comparing rows.

| Run | Completed / EOS | Cross-backend decoded text | Token count / finish / character edits |
| --- | ---: | ---: | ---: |
| ORT CPU vs default ORT CUDA | 121/121 on each | 120/121 | 120/121 / 121/121 / 120/121 |
| ORT CPU vs ORT CUDA with `use_tf32=0` | 121/121 on each | 121/121 | 121/121 / 121/121 / 121/121 |

The default-CUDA difference is image `211206815-44.png`. A separate same-build, single-image Token-ID probe locates the first difference at zero-based generation index 15: CPU ID `93989`, default CUDA ID `14731`. With `use_tf32=0`, that single image has exact Token IDs and text across CPU/CUDA. The 121-image reports intentionally omit Token IDs to keep the external reports small; **121/121 Token-ID equality has not been measured**. The full TF32-off comparison establishes decoded-text, count, finish-state and annotation-edit agreement only. Both full runs have 0/121 strict whitespace-normalized matches to annotations and approximately 55.17% corpus character CER. Different LaTeX notation may represent the same formula, so neither metric is mathematical semantic accuracy.

The two full TF32-off runs share Visual assembly SHA `0d0f7cc9d3f64eab8e990113a1034f29992908bb7846a856b9986a853f245ed3`, OpenCV assembly SHA `0639b598fbc8441bdc4a375cc08c0858e25b064689c4bf07cd4c2b930cd00fd4` and test assembly SHA `09a6c24a2bcb3a8904606757deb8f4393e2d72e64bc6530cf06c3fd4b7d8fc6e`. Their report source marker is `f003abe+tf32-probe`: the suffix identifies this test-only, not-yet-committed TF32 switch; assembly hashes are the executable identity. The pinned dataset manifest SHA is `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76` and annotation CSV SHA is `b7a4679f7d9203f1040c3945aaceca5b73c3d110a70dd35487d4ec5462d8d6f1`.

Per-image reports, labels and predictions remain outside Git in `E:\DeploySharpEvidence\paddleocr-vl-realformula-20261026`:

| File | SHA-256 |
| --- | --- |
| `realformula-ort-cpu-121-tf32-probe-build.json` | `51df2700a1ac55f48df468b62dfd24e445f3a93589aef0473b2fc44e14b329e9` |
| `realformula-ort-cuda-121-tf32-off.json` | `19f1705824ea95f50089bad80d29e8cc242daed438b030cfc8358a14522b76c0` |
| `realformula-cpu-cuda-121-tf32-off-comparison.json` | `81fcc594f7cf145f0a867d68190a9167508847578da9be32c29c87f5e3e33975` |
| `realformula-cpu-cuda-121-comparison-v2.json` (default CUDA) | `6c9e2d50f68d12d8f6a05aec9071a492465870770193d043a53fbe4683c9f87d` |
| `realformula-divergence-tokenids-comparison.json` | `68f8ad69d8c23cd36b352c9e2515a9bf3e5d3a9393f0cdf826f6e2697d111a7c` |
| `realformula-divergence-tf32-off-comparison.json` | `5fd626937d0a0dd92cf25a04586cca2d69ef9895a26da1d88e1b52f71464b236` |

To reproduce, set the model, Bundle and realFormula dataset paths as in the [original CUDA quality report](paddleocr-vl-realformula-ort-cuda-20261026.md), clear any image/limit selection, and run `EvaluateAnnotatedRealFormulaCorpus` once per backend from the same test build. Use a distinct `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_REPORT_PATH` each time; set `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_CUDA_TF32=0` for the CUDA parity run. Set `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RECORD_TOKEN_IDS=1` only for a bounded Token-ID diagnostic. Compare the completed JSON files with `eng/models/paddle-ocr/scripts/Compare-PaddleOcrVlRealFormulaBackends.ps1`; it checks identities and writes a per-image comparison. The switch is opt-in in the test runner, not a change to the library's default CUDA provider.

These runs are not controlled latency benchmarks: they have no warm-up, repeated measurement, GPU clock control or load isolation. The comparison does not establish formula semantic quality, page-level quality, OpenVINO/TensorRT parity, arbitrary model revisions, general input robustness or long-run stability.
