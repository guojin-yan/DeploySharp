# HierText natural wide-line GT recognition — OpenCV DNN (2026-10-23)

The original A2 requirement concerns **normalized recognition width >3,200 pixels**, not 3,200 characters. This experiment uses original labelled quadrilaterals directly with `RecognizeOnlyAsync`, bypassing DET/CLS and detector-association misses. All source JPEGs were already cached; none was downloaded again or added to the repository.

Pinned HierText validation revision: `70b6620b2b112597d8219e11eee9773a1403827c`; annotation SHA-256: `ce7085d8bf24c2f13df852715c34494e6748cafcabb4bfc7a6e2bb320f58d23a`. The fixed geometry audit contains 13 lines in 9 images. Main quality uses 11 officially legible lines with source bbox height >=2px (1,535 reference Unicode scalars / 207 whitespace words). One officially illegible line and one officially legible but 1px-high line are retained as separate diagnostic strata. No prediction-based exclusion is applied. Image/model/dictionary identities are recorded in the [JSON summary](paddleocr-hiertext-natural-wide-opencv-20261023.json).

Protocol: target height 48; maximum width **3,200**; Clamp versus SlidingWindow with overlap 0.2 and maximum 32 windows; batch 1; one independently created Session per stage; one untimed execution per line/mode/backend. Runtime(s): opencv-dnn 5.0.0. The 32x32 prepared DET tensor is unused: recognition crop sampling always reads the original image. This is neither a latency benchmark nor a claim of statistical significance.

| Recognition backend | v6 recognizer | Mode | Main lines | Character edits / reference | CER | Word edits / reference | WER | Windows | Uncertain seams |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| opencv-dnn | small | Clamp | 11 | 23 / 1535 | 1.498% | 17 / 207 | 8.213% | 0 | 0 |
| opencv-dnn | small | SlidingWindow | 11 | 31 / 1535 | 2.02% | 19 / 207 | 9.179% | 22 | 3 |
| opencv-dnn | medium | Clamp | 11 | 27 / 1535 | 1.759% | 15 / 207 | 7.246% | 0 | 0 |
| opencv-dnn | medium | SlidingWindow | 11 | 29 / 1535 | 1.889% | 18 / 207 | 8.696% | 22 | 4 |

CER uses ordinal Unicode scalars and WER uses whitespace-token edit distance, aggregated as total edits / total reference units. Explicit invariant-lowercase results are also recorded and happen to equal the raw metrics on this selection; spaces and punctuation are not removed. These corpus/micro rates differ from the historical detector-association report's macro means and should not be compared as if they used the same input or protocol.

All **52/52** calls pass width/source/CTC contracts: original source region and coordinates survive, each raw window is <=3,200 pixels, coverage is ordered and overlapping from 0 to 1, and retained emitted tokens reconstruct the final text.
Only one backend was run in this report: cross-backend parity is **not assessed**. Single-row groups do not count as matched backend pairs; use a separately validated reference comparison.

| Backend / model | Sliding vs Clamp: CER better / worse / equal lines | Sliding minus Clamp: character edits | Sliding minus Clamp: word edits |
| --- | ---: | ---: | ---: |
| opencv-dnn/medium | 0 / 2 / 9 | 2 | 3 |
| opencv-dnn/small | 0 / 3 / 8 | 8 | 2 |

Positive deltas mean more errors with SlidingWindow. The conservative merger keeps unmatched prefixes rather than silently deleting text; execution parity alone does not prove accurate seams. Clamp remains the default. This bounded selection does not justify changing overlap thresholds or enabling automatic correction.

A2 remains open for repeat-number/single-character seam quality, remaining exact backend/model combinations and broader independent natural-width evidence. The 13-line geometry selection is not official HierText scoring, detector/page accuracy or a release accuracy gate. Source-model training overlap is unknown. Local optional traces provide seam diagnostics; raw text, labels and images are excluded from committed reports.

Reproduce with `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN=1`, optional `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_BACKENDS=onnxruntime,openvino,opencv-dnn,tensorrt` (default: the two CPU reference backends), the dataset/model/candidate/output variables documented by `PaddleOcrNaturalWideLineExternalIntegrationTests`, and test filter `FullyQualifiedName~PaddleOcrNaturalWideLineExternalIntegrationTests`. TensorRT additionally needs the matching JYPPX native bridge, TensorRT/CUDA/cuDNN roots and `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRT_API=11` (or 10); engines are built into a unique local artifacts directory. The required but unused detector Session stays on ORT CPU for OpenCV/TensorRT REC-only runs; no DET/CLS inference is performed. Set `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE=1` only for local raw seam traces. Summarize with `eng/models/paddle-ocr/scripts/Summarize-PaddleOcrNaturalWideLines.ps1 -ReportPath <local-test-json>` and explicit output filenames to preserve earlier reports. Text hashes/counts and recomputation inputs stay in committed JSON; detailed local report/trace SHA values identify ignored artifacts.
