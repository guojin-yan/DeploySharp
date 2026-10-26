# Independent HierText test natural wide-line GT: ORT CPU (2026-10-26)

The original A2 requirement concerns **normalized recognition width >3,200 pixels**, not 3,200 characters. This experiment uses original labelled quadrilaterals directly with `RecognizeOnlyAsync`, bypassing DET/CLS and detector-association misses. Source JPEGs remain outside the repository.

Pinned HierText test revision: `70b6620b2b112597d8219e11eee9773a1403827c`; annotation SHA-256: `8a8f6cb6dd706b457b822634e9f19b7f51cc9813729d57401a3bb6ea45abd438`. The fixed geometry audit contains 17 lines in 11 images. Main quality uses 12 officially legible lines with source bbox height >=2px. 5 officially illegible and 0 sub-2px-height lines are retained as separate diagnostic strata. No prediction-based exclusion is applied. Image/model/dictionary identities are recorded in the [JSON summary](paddleocr-hiertext-test-natural-wide-a2-ort-20261026.json).

Protocol: target height 48; maximum width **3,200**; Clamp versus SlidingWindow with overlap 0.2 and maximum 32 windows; batch 1; one independently created Session per stage; one untimed execution per line/mode/backend. Runtime(s): onnxruntime 1.28.0. The 32x32 prepared DET tensor is unused: recognition crop sampling always reads the original image. This is neither a latency benchmark nor a claim of statistical significance.

| Recognition backend | v6 recognizer | Mode | Main lines | Character edits / reference | CER | Word edits / reference | WER | Windows | Uncertain seams |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| onnxruntime | small | Clamp | 12 | 56 / 1945 | 2.879% | 26 / 315 | 8.254% | 0 | 0 |
| onnxruntime | small | SlidingWindow | 12 | 101 / 1945 | 5.193% | 34 / 315 | 10.794% | 25 | 3 |
| onnxruntime | medium | Clamp | 12 | 45 / 1945 | 2.314% | 21 / 315 | 6.667% | 0 | 0 |
| onnxruntime | medium | SlidingWindow | 12 | 134 / 1945 | 6.889% | 32 / 315 | 10.159% | 25 | 5 |

CER uses ordinal Unicode scalars and WER uses whitespace-token edit distance, aggregated as total edits / total reference units. Explicit invariant-lowercase results are recorded separately in JSON; they need not equal the raw metrics. Spaces and punctuation are not removed. These corpus/micro rates differ from historical detector-association macro means and should not be compared as if they used the same input or protocol.

All **68/68** calls pass width/source/CTC contracts: original source region and coordinates survive, each raw window is <=3,200 pixels, coverage is ordered and overlapping from 0 to 1, and retained emitted tokens reconstruct the final text.
Only one backend was run in this report: cross-backend parity is **not assessed**. Single-row groups do not count as matched backend pairs; use a separately validated reference comparison.

| Backend / model | Sliding vs Clamp: CER better / worse / equal lines | Sliding minus Clamp: character edits | Sliding minus Clamp: word edits |
| --- | ---: | ---: | ---: |
| onnxruntime/medium | 0 / 4 / 8 | 89 | 11 |
| onnxruntime/small | 1 / 3 / 8 | 45 | 8 |

Positive deltas mean more errors with SlidingWindow. The conservative merger keeps unmatched prefixes rather than silently deleting text; execution parity alone does not prove accurate seams. Clamp remains the default. This bounded selection does not justify changing overlap thresholds or enabling automatic correction.

A2 remains open for repeat-number/single-character seam quality, remaining exact backend/model combinations and broader natural-width evidence. This 17-line geometry selection is not official HierText scoring, detector/page accuracy or a release accuracy gate. Source-model training overlap is unknown. Local optional traces provide seam diagnostics; raw text, labels and images are excluded from committed reports.

Reproduce with `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN=1`, optional `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_BACKENDS=onnxruntime,openvino,opencv-dnn,tensorrt` (default: the two CPU reference backends), the dataset/model/candidate/output variables documented by `PaddleOcrNaturalWideLineExternalIntegrationTests`, and test filter `FullyQualifiedName~PaddleOcrNaturalWideLineExternalIntegrationTests`. TensorRT additionally needs the matching JYPPX native bridge, TensorRT/CUDA/cuDNN roots and `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRT_API=11` (or 10); engines are built into a unique local artifacts directory. The required but unused detector Session stays on ORT CPU for OpenCV/TensorRT REC-only runs; no DET/CLS inference is performed. Set `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE=1` only for local raw seam traces. Summarize with `eng/models/paddle-ocr/scripts/Summarize-PaddleOcrNaturalWideLines.ps1 -ReportPath <local-test-json>` and explicit output filenames to preserve earlier reports. Text hashes/counts and recomputation inputs stay in committed JSON; detailed local report/trace SHA values identify ignored artifacts.
