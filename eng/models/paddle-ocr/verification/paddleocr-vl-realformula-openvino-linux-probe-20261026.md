# PaddleOCR-VL-1.6 realFormula Linux backend probe

Date: 2026-10-26. This is a bounded quality and memory probe, not the complete 121-image OpenVINO evaluation or a controlled speed comparison.

## Scope and identity

- Host: Ubuntu 22.04.5 x64, 16 GiB RAM, no swap, .NET 10.0.9. The published test runner used ONNX Runtime 1.28.0, OpenVINO native 2026.3.1 and the Ubuntu 22.04 OpenCV 5.0.0 native package.
- Model: fixed `PaddlePaddle/PaddleOCR-VL-1.6` checkpoint `c5630abae1d940eafe0697512a0325494b02ab42`; the same four ONNX graphs and tokenizer were used for both backends. Every report records their SHA-256 values.
- Corpus: MathNet realFormula v1, 121 annotated images. The first five corpus entries were run on both backends; the five largest source images by pixel count were run on OpenVINO CPU as a separate memory probe. These are two subsets, not ten distinct randomly sampled images.
- Build: source baseline `22a9705`, with the opt-in Linux test dependency changes in this commit. Both compared runs used the same Visual, Visual.OpenCV and test assembly hashes recorded in the JSON reports.
- Protocol: one pass per image, `FormulaRecognition`, greedy generation with at most 512 new tokens, raw encoded pixel orientation, four persistent Sessions per process, `MALLOC_ARENA_MAX=2` and `DOTNET_GCHeapHardLimit=0x40000000`. No warm-up, clock control or accuracy claim from upstream benchmark scores.

| Corpus subset | Backend | Completed / EOS | Exact normalized LaTeX | Character CER | Raw report |
| --- | --- | ---: | ---: | ---: | --- |
| First five | ONNX Runtime CPU | 5 / 5 | 0 / 5 | 50.57% | [JSON](paddleocr-vl-realformula-ortcpu-linux-5-20261026.json) |
| First five | OpenVINO CPU | 5 / 5 | 0 / 5 | 50.57% | [JSON](paddleocr-vl-realformula-openvino-linux-5-20261026.json) |
| Five largest source images | OpenVINO CPU | 5 / 5 | 0 / 5 | 52.03% | [JSON](paddleocr-vl-realformula-openvino-linux-largest5-20261026.json) |

The [same-build comparison](paddleocr-vl-realformula-ortcpu-openvino-linux-5-comparison-20261026.json) checked all five paired source hashes and references: text 5/5, generated Token IDs 5/5, token counts 5/5 and finish reasons 5/5 matched. This is backend decision parity on five labelled inputs, not formula semantic correctness or 121-image parity. Whitespace-normalized character edit distance penalizes equivalent LaTeX spellings.

The first OpenVINO five-image process had a `/usr/bin/time -v` maximum RSS of 9,468,072 KiB; the largest-image process had 11,207,800 KiB (about 10.69 GiB). A separate `ps` observation during the latter reported about 11,040,536 KiB for the test host and 2.4 GiB system memory available. These are process observations from sequential runs, not a stable peak bound or a memory-leak test. The 16 GiB host has no swap, so the remaining 121-image OpenVINO evaluation should use bounded process shards and preserve per-image checkpoints.

## Reproduce

Publish the opt-in test variant from the repository root. The distro RID matters: publishing as portable `linux-x64` did not select `JYPPX.OpenCV.runtime.ubuntu.22.04-x64`'s native `.so`; the initial run stopped at OpenCV loading before image inference. `ubuntu.22.04-x64` with the RID graph selects the native OpenCV and OpenVINO files automatically.

```powershell
dotnet publish tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 -r ubuntu.22.04-x64 --self-contained false `
  -p:DeploySharpVlLinuxQuality=true -p:UseRidGraph=true -m:1 `
  -o E:/DeploySharpEvidence/paddleocr-vl-realformula-ubuntu-runner
```

On Ubuntu, set `LD_LIBRARY_PATH` to the published runner directory, `DOTNET_ROOT` to the local .NET installation, and the `DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT`, `DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT` and `DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT` paths to the pinned four-graph Bundle, tokenizer and realFormula data. Then run the same test once per backend:

```bash
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RUN=1
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_BACKEND=openvino-cpu # or onnxruntime-cpu
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT=5
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RECORD_TOKEN_IDS=1
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_REPORT_PATH=/path/to/new-report.json
export MALLOC_ARENA_MAX=2 DOTNET_GCHeapHardLimit=0x40000000
dotnet vstest /path/to/runner/DeploySharp.Visual.OpenCV.Tests.dll \
  --TestCaseFilter:FullyQualifiedName~EvaluateAnnotatedRealFormulaCorpus
```

For the largest-image probe, set `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_IMAGES` to the five image names in the raw OpenVINO report and unset `DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT`. Compare the paired reports with `Compare-PaddleOcrVlRealFormulaBackends.ps1`; it rejects different source or assembly hashes. The four JSON files are the committed raw evidence. Next: bounded 121-image OpenVINO CPU and same-build ORT CPU runs, then a full-corpus comparison. No TensorRT or OpenCV DNN formula result follows from this probe.
