# HierText natural repeated-number and single-character wide lines (2026-10-27)

The original A2 requirement concerns **normalized recognition width >3,200 pixels**, not 3,200 characters. This experiment uses original labelled quadrilaterals directly with `RecognizeOnlyAsync`, bypassing DET/CLS and detector-association misses. Source JPEGs remain outside the repository.

Pinned HierText train revision: `70b6620b2b112597d8219e11eee9773a1403827c`; annotation SHA-256: `bf7a271c7a016e66ad87556078f0371f8fb12beaa2e2e60fc33458ad57c3e4fd`. The fixed geometry audit contains 3 lines in 3 images. Main quality uses 3 officially legible lines with source bbox height >=2px. 0 officially illegible and 0 sub-2px-height lines are retained as separate diagnostic strata. No prediction-based exclusion is applied. Image/model/dictionary identities are recorded in the [JSON summary](paddleocr-hiertext-train-number-challenge-20261027.json).
Selection: Officially legible lines, source bbox height >=2px, projected width >3200px at height 48; exact preselected annotation keys; one line per image. Exact keys and the annotation-stratification flag are recorded in JSON. This is a deliberately chosen challenge cohort, not a random sample; its candidate SHA is checked against the native report.

Protocol: target height 48; maximum width **3,200**; Clamp versus SlidingWindow with overlap 0.2 and maximum 32 windows; batch 1; one independently created Session per stage; one untimed execution per line/mode/backend. Runtime(s): onnxruntime 1.28.0. The 32x32 prepared DET tensor is unused: recognition crop sampling always reads the original image. This is neither a latency benchmark nor a claim of statistical significance.

| Recognition backend | v6 recognizer | Mode | Main lines | Character edits / reference | CER | Word edits / reference | WER | Windows | Uncertain seams |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| onnxruntime | small | Clamp | 3 | 83 / 357 | 23.249% | 23 / 26 | 88.462% | 0 | 0 |
| onnxruntime | small | SlidingWindow | 3 | 86 / 357 | 24.09% | 21 / 26 | 80.769% | 10 | 6 |
| onnxruntime | medium | Clamp | 3 | 85 / 357 | 23.81% | 25 / 26 | 96.154% | 0 | 0 |
| onnxruntime | medium | SlidingWindow | 3 | 82 / 357 | 22.969% | 21 / 26 | 80.769% | 10 | 6 |

CER uses ordinal Unicode scalars and WER uses whitespace-token edit distance, aggregated as total edits / total reference units. Explicit invariant-lowercase results are recorded separately in JSON; they need not equal the raw metrics. Spaces and punctuation are not removed. These corpus/micro rates differ from historical detector-association macro means and should not be compared as if they used the same input or protocol.

All **12/12** calls pass width/source/CTC contracts: original source region and coordinates survive, each raw window is <=3,200 pixels, coverage is ordered and overlapping from 0 to 1, and retained emitted tokens reconstruct the final text.
Only one backend was run in this report: cross-backend parity is **not assessed**. Single-row groups do not count as matched backend pairs; use a separately validated reference comparison.

| Backend / model | Sliding vs Clamp: CER better / worse / equal lines | Sliding minus Clamp: character edits | Sliding minus Clamp: word edits |
| --- | ---: | ---: | ---: |
| onnxruntime/medium | 2 / 1 / 0 | -3 | -4 |
| onnxruntime/small | 2 / 1 / 0 | 3 | -2 |

Positive deltas mean more errors with SlidingWindow. The conservative merger keeps unmatched prefixes rather than silently deleting text; execution parity alone does not prove accurate seams. Clamp remains the default. This bounded selection does not justify changing overlap thresholds or enabling automatic correction.

The three exact lines were chosen from official annotations for repeated numeric groups, a URL containing repeated digits, and isolated numeric characters, respectively. None is in the earlier 12-line train holdout. On the repeated-number line (`a8efce4fa771414e/141/0`, normalized width 8,161), both recognizers used five windows: only the first of four seams suppressed eight tokens, while the other three kept their right prefixes as uncertain. The URL line (`c9e19593feae3aca/88/0`) removed 27 prefix tokens at one uncertain seam. The short numeric line (`6dde021e49284075/25/0`) has low raw recognizer quality under both modes; SlidingWindow adds 7/4 character edits for Small/Medium, which cannot be assigned solely to seam logic. These observations show the conservative fallback on actual repeated content, not repeat-number accuracy acceptance.

To recreate the candidate, run `Select-HierTextTrainNaturalWide.py` against the pinned train annotation with `--count 3 --keys a8efce4fa771414e/141/0,c9e19593feae3aca/88/0,6dde021e49284075/25/0`, then use the existing acquisition script and rerun the selector to pin downloaded image hashes. The native test additionally needs `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_SPLIT=train`, `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_EXPECTED_LINES=3`, `BACKENDS=onnxruntime`, and the candidate/output paths under the common `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_` prefix. The selector's `eligibleLinesWithinSelection=3` refers to the three exact keys, not the previously audited full train pool of 42 eligible lines. The raw report has `sourceRevision=unknown`; its Visual/test assembly hashes and the candidate/report/trace SHA values in JSON identify the exact executed build.

A2 remains open for repeat-number/single-character seam quality, remaining exact backend/model combinations and broader natural-width evidence. This 3-line geometry selection is not official HierText scoring, detector/page accuracy or a release accuracy gate. Source-model training overlap is unknown. Local optional traces provide seam diagnostics; raw text, labels and images are excluded from committed reports.

Reproduce with `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN=1`, optional `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_BACKENDS=onnxruntime,openvino,opencv-dnn,tensorrt` (default: the two CPU reference backends), the dataset/model/candidate/output variables documented by `PaddleOcrNaturalWideLineExternalIntegrationTests`, and test filter `FullyQualifiedName~PaddleOcrNaturalWideLineExternalIntegrationTests`. TensorRT additionally needs the matching JYPPX native bridge, TensorRT/CUDA/cuDNN roots and `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRT_API=11` (or 10); engines are built into a unique local artifacts directory. The required but unused detector Session stays on ORT CPU for OpenCV/TensorRT REC-only runs; no DET/CLS inference is performed. Set `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE=1` only for local raw seam traces. Summarize with `eng/models/paddle-ocr/scripts/Summarize-PaddleOcrNaturalWideLines.ps1 -ReportPath <local-test-json>` and explicit output filenames to preserve earlier reports. Text hashes/counts and recomputation inputs stay in committed JSON; detailed local report/trace SHA values identify ignored artifacts.
