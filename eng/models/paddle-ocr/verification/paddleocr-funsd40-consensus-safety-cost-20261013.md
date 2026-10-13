# OCR enhancement safety and cost summary

This report is a bounded diagnostic over parent-linked degraded crops. A false correction means a selected candidate has a higher case-folded CER or WER than the original row. It is not a release accuracy or robustness claim.

| Evidence | Backend | Mode | Records | Consensus | Selected | CER before/after | WER before/after | CER-worse selected | CER false-correction rate | All P50/P95 ms |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `paddleocr-funsd40-consensus-20261013-enhancement-consensus-recognize-only-onnxruntime` | onnxruntime | ground-truth-full-crop-recognize-only | 240 | 177 | 115 | 0.3060/0.3067 | 0.4264/0.4181 | 4 | 0.0348 | 76.21/144.57 |
| `paddleocr-funsd40-consensus-20261013-enhancement-consensus-recognize-only-openvino` | openvino | ground-truth-full-crop-recognize-only | 240 | 177 | 115 | 0.3060/0.3067 | 0.4264/0.4181 | 4 | 0.0348 | 34.87/51.64 |

The current policy remains opt-in. Any production threshold must be selected from a larger, quality-controlled labeled set and must include a maximum false-correction rate and an explicit latency budget.

## Cross-backend agreement

| Pair | Common | Decision | Selection flag | Final text | Selected index | Consensus count |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| onnxruntime / openvino | 240 | 240/240 | 240/240 | 240/240 | 240/240 | 240/240 |

## Consensus threshold diagnostic: onnxruntime

| Minimum gain | Selected | CER better | CER worse | CER equal | Mean final CER/WER |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0.000 | 115 | 2 | 4 | 109 | 0.3067/0.4181 |
| 0.005 | 35 | 2 | 2 | 31 | 0.3059/0.4181 |
| 0.010 | 31 | 1 | 2 | 28 | 0.3063/0.4222 |
| 0.020 | 25 | 1 | 2 | 22 | 0.3063/0.4222 |
| 0.030 | 18 | 1 | 2 | 15 | 0.3063/0.4222 |
| 0.050 | 10 | 0 | 0 | 10 | 0.3060/0.4264 |
| 0.075 | 9 | 0 | 0 | 9 | 0.3060/0.4264 |
| 0.100 | 5 | 0 | 0 | 5 | 0.3060/0.4264 |
| 0.150 | 2 | 0 | 0 | 2 | 0.3060/0.4264 |
| 0.200 | 2 | 0 | 0 | 2 | 0.3060/0.4264 |

Observed zero-worse threshold with at least one CER improvement: none

Post-hoc diagnostic on this fixed evidence set; it must not be reused as a production threshold without an independent validation split.

## Consensus threshold diagnostic: openvino

| Minimum gain | Selected | CER better | CER worse | CER equal | Mean final CER/WER |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0.000 | 115 | 2 | 4 | 109 | 0.3067/0.4181 |
| 0.005 | 35 | 2 | 2 | 31 | 0.3059/0.4181 |
| 0.010 | 31 | 1 | 2 | 28 | 0.3063/0.4222 |
| 0.020 | 25 | 1 | 2 | 22 | 0.3063/0.4222 |
| 0.030 | 18 | 1 | 2 | 15 | 0.3063/0.4222 |
| 0.050 | 10 | 0 | 0 | 10 | 0.3060/0.4264 |
| 0.075 | 9 | 0 | 0 | 9 | 0.3060/0.4264 |
| 0.100 | 5 | 0 | 0 | 5 | 0.3060/0.4264 |
| 0.150 | 2 | 0 | 0 | 2 | 0.3060/0.4264 |
| 0.200 | 2 | 0 | 0 | 2 | 0.3060/0.4264 |

Observed zero-worse threshold with at least one CER improvement: none

Post-hoc diagnostic on this fixed evidence set; it must not be reused as a production threshold without an independent validation split.
