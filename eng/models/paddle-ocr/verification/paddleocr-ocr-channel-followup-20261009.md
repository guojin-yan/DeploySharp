# PaddleOCR channel tuning and contract-drift follow-up (2026-10-09)

This report records a deliberately narrow ORT CUDA tuning experiment on one JYPPX Windows host. It does not change the library default channel count and does not promote a channel count from one model to every model.

## Protocol

- Dataset: HierText `sample-002`, three selected validation pages (`5c4d5de59518fe4d`, `97a0add3f8f47b65`, `d14658b78cec2cf7`).
- Backend: `onnxruntime-cuda`; batch `16`; `5` warm-up and `50` measured iterations; model loading excluded; input reuse disabled.
- Page P50/P95 are calculated by the benchmark over the repeated calls for one page. The group median is a summary of those page-level values, not a pooled dataset percentile.
- The original three-page runs were collected from a dirty worktree at source revision `c1dcb4123c9d720852e4c3251ab24a4ac39b67ae`. The contract-diagnostic rerun uses the semantic-contract instrumentation introduced after that run.

## Three-page tuning results

| Model | Channels | Pages pass/fail | Mean of page P50 (ms) | Median page P50 (ms) | Median page P95 (ms) | Strict contract variants across pages |
|---|---:|---:|---:|---:|---:|---|
| v5/mobile | 1 | 3/0 | 115.029 | 67.276 | 80.656 | [1] |
| v5/mobile | 2 | 3/0 | 67.777 | 66.371 | 75.777 | [1] |
| v6/medium | 1 | 3/0 | 267.112 | 277.229 | 329.914 | [1] |
| v6/medium | 2 | 2/1 | 127.817 | 127.817 | 192.724 | [1] |

### Interpretation

- v6 Tiny averaged approximately `125.48 / 96.10 / 110.21 ms` page-P50 for channels `1 / 2 / 4`; channels=2 was the fastest in this three-page selection, while channels=4 was slower and more variable.
- v5 Mobile channels=1 and channels=2 both completed all three pages; the page-level results must be considered together with the recognition-batch count and should not be generalized beyond this model/device.
- v6 Medium channels=2 did not complete all three pages under the original strict contract gate. The failure was on `97a0add3f8f47b65`, so it is not a valid universal recommendation.

## v6 Medium strict-contract diagnostic

The same `v6/medium`, page `97a0add3f8f47b65`, batch=16/channels=2 workload was rerun with `DEPLOYSHARP_PADDLEOCR_ALLOW_NUMERIC_CONTRACT_DRIFT=1`. The diagnostic completed `5 + 50` calls with `result_text_sha256` and the new semantic contract SHA stable, while the full floating-point-inclusive contract had two variants. This isolates the observed drift to fields intentionally excluded from the semantic fingerprint (scores, polygon coordinates, or confidence values); it does not prove geometric tolerance or accuracy parity.

- `v6/medium` `F:\OCRBenchmarkTesting\data\images\hiertext\validation\97a0add3f8f47b65.jpg`: status `pass`, total P50/P95 `218.539`/`298.816` ms, strict contract variants `3`, maximum absolute numeric drift `0.000228047371`, semantic SHA `6b3c4e4d3aaafe4c8e07391b82898f4b08af33715b08ef6d90e35d647b1de052`, text SHA `5f9cdd082f3ed8574cc2c20ee6b36783c839b9329b9087d395f62a675d4eaea9`.

The benchmark still rejects a contract change by default. The diagnostic switch is opt-in and only permits a run when the text and semantic contract remain stable; a semantic change remains a failure. The switch must not be used to turn a quality or accuracy run into a pass.

## Decision and next work

- Keep the production/default `inferenceChannels` unchanged. Do not write channels=2 into a global default based on this sample.
- Treat v6 Tiny channels=2 and v5 Mobile channels=2 as candidates for a larger multi-page confirmation, not as a library-wide recommendation.
- Before using channels>1 for v6 Medium, collect a larger diagnostic set and quantify coordinate/score/confidence drift against an explicit tolerance. A semantic drift or text drift remains a blocking failure.
- This report is a performance/concurrency diagnostic only; it does not close the PP-OCR accuracy gate, TensorRT/OpenVINO/OpenCV matrix, or cross-device evidence requirements.

## Multi-page contract follow-up

These rows are direct `5 warmup + 50 measured` reruns with ORT CUDA, batch `16`, channels `2`, `MAX_REGIONS=64` for the diagnostic page. The three v6 rows in a single CSV are split by model here so the report does not hide per-variant outcomes.

| Model | Pages pass/fail | Classification | Page P50 median (ms) | Page P95 median (ms) | Strict-stable pages | Semantic-stable pages | Max numeric drift |
|---|---:|---|---:|---:|---:|---:|---:|
| v5/mobile / channels=2 | 3/0 | `strict-stable` | 86.575 | 138.261 | 3 | 3 | `0` |
| v6/medium / channels=2 | 2/1 | `mixed-or-blocked` | 145.172 | 194.055 | 2 | 2 | `0` |
| v6/small / channels=2 | 3/0 | `strict-stable` | 73.292 | 115.387 | 3 | 3 | `0` |
| v6/tiny / channels=2 | 3/0 | `strict-stable` | 73.472 | 86.378 | 3 | 3 | `0` |

The v5 Mobile and v6 Tiny groups are strict-stable across all three pages. The v6 Medium group contains one page rejected by the default 32-region budget; rerunning that page with `MAX_REGIONS=64` kept text and semantic contracts stable but produced three strict-contract variants and a maximum numeric drift of approximately `2.28047371e-4`. This is diagnostic evidence only; it does not define a release tolerance or promote channels=2 to a global default.
