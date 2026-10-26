# PaddleOCR-VL-1.6: two preselected FUNSD Spotting pages

Date: 2026-10-26. This extends the [first labelled-page diagnostic](paddleocr-vl-funsd-spotting-quality-20261026.md) with one additional page selected **by manifest row before inference**, not by model output. The fixed `funsd-test-full-001.jsonl` SHA-256 is `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`. Page 0 was the existing `funsd-test-82504862`; the new nonempty row 20 is `funsd-test-86220490` (754x1000, 75 non-ignored word labels, source-image SHA-256 `391c29fe036015e1c52d9ed0f065e35fc5d2054ab4285aa1123f8a269b79fef9`). Index 20 is a position within the 40-page manifest, not a random or representative selection.

Both pages ran through the production C# four-graph ORT CPU Spotting path with the fixed PaddleOCR-VL-1.6 checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`. Each labelled word centroid was assigned to the smallest containing predicted quadrilateral; unmatched truth and predictions contribute deletions and insertions. NFKC, whitespace collapse and invariant uppercase precede per-region character and word edit distance. The first-page report documents the complete protocol and its limitations. The rates below include all three edit categories, not only text edits within associated regions.

| Manifest row | Labelled words | EOS Token / regions | Covered / missed words | Unmatched predictions | Protocol-total CER | Protocol-total WER |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 63 | 411 / 26 | 45 / 18 | 5 | `263/406 = 64.78%` | `47/63 = 74.60%` |
| 20 | 75 | 362 / 25 | 73 / 2 | 0 | `14/416 = 3.37%` | `12/75 = 16.00%` |

On row 20, the two uncovered word annotations are literal short hyphens at the starts of two bullet lines. The model output contains `--` in the corresponding text lines; this is a localization/annotation-granularity diagnostic, not proof that the content was absent from the output. The page-to-page difference is large and is precisely why the first page cannot stand for general Spotting accuracy. Do not pool these two rows into a corpus score or tune a threshold on them.

## Error decomposition from saved predictions

The completed C# reports were rescored without loading the model. The evaluator checked the pinned manifest/image identity and reproduced every original word, region and edit total before splitting errors by association outcome. This distinguishes empty-region insertions and uncovered-word deletions from edits inside associated regions; the latter still mix transcription, reading order and imperfect word-to-region assignment.

| Manifest row | Character edits: associated / unmatched prediction / uncovered truth | Word edits: associated / unmatched prediction / uncovered truth | Associated reference characters / words |
| ---: | ---: | ---: | ---: |
| 0 | 108 / 44 / 111 = 263 | 24 / 5 / 18 = 47 | 295 / 45 |
| 20 | 12 / 0 / 2 = 14 | 10 / 0 / 2 = 12 | 414 / 73 |

The two derived reports remain outside Git beside their source reports: `onnxruntime-cpu-page0-breakdown-v1.json` (1,224 bytes, SHA-256 `7b5547da8bec4812ee801ba73b3039bee35b233aef44141bd57b4167e15b6edb`) and `onnxruntime-cpu-page20-breakdown-v1.json` (1,218 bytes, SHA-256 `6e4e87cca48443252e9d2e994bb4f1c99e017abb12397eebea0b198688409c2f`). Reproduce with `PaddleOcrVlFunsdSpottingQualityIntegrationTests.ReevaluateCompletedReportWithoutModelInference` and its `DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_SOURCE_REPORT` / `DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_BREAKDOWN_REPORT` paths. The first page has both association-text edits and substantial coverage/unmatched-region error; the second page is dominated by associated edits, but these two cases do not estimate a dataset-wide mix.

## Official-weight parity on row 20

The pinned original weights ran with the official PIL Processor, Transformers `5.20.2`, CPU FP32, four threads and the same 2,048-token greedy cap on the separate Ubuntu host. The image SHA and checkpoint revision match the C# report. Both processors produced 4,769,856 Float32 Patch values with SHA-256 `5ebbb6999c6a969022e1fca628a7211a3b63494a2c2800cb0d5c533190eb54e2`; all 362 generated Token IDs, decoded text and EOS agree. Row 0's separate 411/411 Token and text comparison is recorded in the linked first-page report. Neither parity result grades localization/text quality against the annotations.

The new C# raw report is `E:\DeploySharpEvidence\paddleocr-vl-funsd-spotting-20261026\onnxruntime-cpu-page20.json` (30,929 bytes, SHA-256 `76497ae5a8fbd2702c1ba303259ca7fac11b8d192c3f69e688fd3be3436d7080`, test assembly SHA-256 `94f496f9203c83b016be6ccef651adb0cf599b196ac89aec32483695ac01489c`). The official raw report is `official-result-page20.json` in the same external directory (90,397 bytes, SHA-256 `4ec20ce5a1ed657f9ced926ca441a95c1eaa7a9cb91bb31cb9f7d6b1300853e9`); its fixed input manifest SHA-256 is `060f7667b544b3fb0183dafb8ab0a024bdc96a94747858478f7b811e26496527`. Source images, labels, transcripts and per-region associations are not committed or uploaded to the model Release.

The selection test's `DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_PAGE_INDEX=20` reproduces the C# row. It rejects invalid indices and pre-existing report paths. The synthetic selection/association tests passed `3/3`; the external row-20 test passed `1/1`. The official reference script SHA-256 is `160ebec6e6d9c60e1c23bfd7ad7abbb72671e6931328df3eeb29332ce5a67ad6`. The observed C# and official durations are single runs on different hosts and **not** a controlled speed comparison.

This bounded result still does not establish official FUNSD F1/CER, representative Spotting quality, reading-order quality, multi-page composition, other PaddleOCR-VL weights or additional backend support. Continue with a larger predeclared selection and separate localization versus transcription error analysis before proposing a quality threshold.
