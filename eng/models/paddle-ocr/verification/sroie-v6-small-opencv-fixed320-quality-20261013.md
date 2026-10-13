# PP-OCRv6 Small OpenCV fixed-width quality check (2026-10-13)

This record compares dynamic recognition width with the explicit fixed width
`320` on the same ten SROIE receipt pages. Both runs use PP-OCRv6 Small,
OpenCV DNN, batch `1`, two independent inference channels, one warm-up and one
measured call per page, plus the same `SlidingWindow` overflow contract. Width
mode is the only intended pipeline setting difference.

## Result

| Width mode | Pages | Regions | IoU@0.5 TP/FP/FN | Matched CER/WER | End-to-end CER/WER | Total P50/P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|
| Dynamic | 10/10 | 531 | 489/42/53 | 30.10% / 45.44% | 37.39% / 55.37% | 5279.78 / 7671.21 |
| Fixed 320 | 10/10 | 531 | 489/42/53 | 30.97% / 47.60% | 38.09% / 56.93% | 1690.07 / 2362.80 |

Fixed width reduced the diagnostic total P50/P95 by `68.0%/69.2%` and
recognition P50/P95 by `73.4%/73.6%`. Detection output did not change: all ten
pages retained the same region count, the indexed polygons were identical and
the aggregate IoU@0.5 counts remained `489/42/53`.

The speed-up has a measurable quality cost on this sample. Fixed width raised
matched CER by `0.87` percentage points and matched WER by `2.16` points;
end-to-end CER/WER rose by `0.70/1.56` points. Exact matched regions fell from
`258` to `251`. `101/531` indexed region texts differed, affecting all ten page
text sequences, even though many changes were spacing or single-character
differences.

## Decision

Dynamic width remains the quality-first default. Fixed width `320` remains an
explicit opt-in performance setting through `TextCropProfile.WithFixedWidth`.
Applications should select a width only after measuring representative labelled
data, especially long lines, dense text and scripts used in production. This
record does not establish `320` as a universal optimum.

## Reproduction and evidence boundary

Use `Invoke-PaddleOcrPublicDataset.ps1` twice with the same manifest and
protocol. Omit `-FixedRecognitionWidth` for the dynamic run and pass
`-FixedRecognitionWidth 320` for the fixed run, then score both with
`Evaluate-DeploySharpPublicOcrDataset.py`. Compare the runs with
`Compare-PaddleOcrBackendQualityEvidence.py --allow-protocol-difference
fixedRecognitionWidth`; all other provenance and protocol fields remain strict.
The comparator uses a canonical JSON selection hash, so harmless JSONL key
ordering and whitespace changes do not masquerade as different data. The machine-readable
[JSON record](sroie-v6-small-opencv-fixed320-quality-20261013.json) retains the
source/runtime/model hashes, aggregate results and raw run hashes.

The source images, annotations and per-region predictions remain local and are
not committed. SROIE supplies word boxes while PP-OCR emits line polygons, so
the values are smoke diagnostics rather than official SROIE line accuracy. The
sample has no natural line at or above 128 or 3,200 characters, and the one-shot
latency values do not replace the formal `5+50` performance report.
