# Natural wide-line seam repair: same-token replay and native follow-up (2026-10-23)

This follow-up preserves the [original native baseline](paddleocr-hiertext-natural-wide-gt-20261023.md). It changes seam merging only: exact matching/substitution fallback remain, leading left whitespace shares the edit budget, and an unambiguous exact right-prefix subsequence may skip bounded left tokens while retaining all left text. Right-only tokens are never skipped by this subsequence rule. Multiple alignments retain the prefix; non-exact fallback remains uncertain. Clamp and all public thresholds are unchanged. This is a heuristic using approximate CTC positions, not a proof of character correspondence.

Inputs: the same 13 labelled HierText lines / 9 cached images, with 11 main-quality lines (1,535 Unicode scalars / 207 whitespace words) and two separate diagnostics. Fixed 3,200px maximum, height 48, overlap 0.2, batch 1, single Session; GT quadrilateral recognition bypasses DET/CLS. ORT CPU 1.28.0 / OpenVINO CPU 2026.2.1 each reran Small/Medium Clamp and SlidingWindow, 104 calls total. Source SHA and assembly identities are in the [machine report](paddleocr-natural-wide-seam-comparison-20261023.json).

| CPU backend / v6 model | Character edits before → after | CER before → after | Word edits before → after | WER before → after | CER better / worse / equal lines |
| --- | ---: | ---: | ---: | ---: | ---: |
| onnxruntime/medium | 99 → 29 | 6.45% → 1.889% | 23 → 18 | 11.111% → 8.696% | 3 / 0 / 8 |
| onnxruntime/small | 68 → 31 | 4.43% → 2.02% | 19 → 19 | 9.179% → 9.179% | 2 / 0 / 9 |
| openvino/medium | 99 → 29 | 6.45% → 1.889% | 23 → 18 | 11.111% → 8.696% | 3 / 0 / 8 |
| openvino/small | 68 → 31 | 4.43% → 2.02% | 19 → 19 | 9.179% → 9.179% | 2 / 0 / 9 |

The pinned 52-case raw-token replay matches the native rerun's final text, metrics and seam decisions **52/52**. All **52/52** Clamp rows are unchanged. All **104** raw windows keep their geometry, text hash and emitted-class hash. Native cross-backend text, emitted-class and seam-decision matches are **52/52, 52/52, 52/52**. Thus these observed quality changes are attributable to merging, not new model/crop/CTC outputs; native confidences/tensors are not claimed bitwise equal.

| Merge-only phase | P50 / P95 per 52 lines | Managed allocation min / max per 52 lines |
| --- | ---: | ---: |
| baseline | 6.54 / 14.23 ms | 2169848 / 2175624 bytes |
| candidate | 5.338 / 9.088 ms | 2168232 / 2171784 bytes |

Each phase uses 5 warm-up sweeps + 50 measured sweeps of 52 preloaded lines. Allocations use `GC.GetAllocatedBytesForCurrentThread`; samples include result arrays/merge results, and exclude fingerprint/metric/JSON work. Phases ran baseline then candidate with no CPU clock control or randomized pairing. These are cost observations, **not a speedup claim**, native-backend comparison or complete OCR latency. The candidate uses two small bounded rolling arrays only on the fallback path; it introduces no image/tensor copying.

The development corpus improves, but Clamp still has fewer character edits (Small 23, Medium 27) than sliding (31, 29). This corpus was used to diagnose the bug and is not independent validation. A2 remains open for broader natural lines, repeated numbers/single-character seams and remaining exact backends. Six new controlled contracts cover left punctuation, whitespace plus substitution, right-only text, ambiguous numbers, budget/disable behavior and out-of-overlap geometry. The current Visual suite passes 531 tests (5 unrelated optional-asset tests skip); replay and native external tests each pass 1/1, and all 16 Visual target frameworks build with zero warnings/errors. The independent PowerShell metric helper also matches all 104 rerun pairs / 832 C# fields; its 11 fixed metric contracts pass.

Reproduce: run `OcrRecognitionWindowTraceReplayTests` with `DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_RUN=1` and the pinned local trace. The baseline phase must use the recorded old assembly, not candidate code with a different label. Run `PaddleOcrNaturalWideLineExternalIntegrationTests` for actual native reruns. Use `Compare-PaddleOcrNaturalWideSeams.ps1` with `-BaselineReplayPath`, `-CandidateReplayPath`, `-NativeReportPath` and `-BaselineNativeReportPath` to validate identities/results and regenerate this report. The [native follow-up](paddleocr-hiertext-natural-wide-seam-followup-20261023.md) records Clamp/sliding quality and diagnostics separately. Local raw reports/traces remain under ignored artifacts, referenced by filename and SHA; images/annotations are not redistributed.
