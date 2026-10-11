# OCR enhancement safety and cost summary

This report is a bounded diagnostic over parent-linked degraded crops. A false correction means a selected candidate has a higher case-folded CER or WER than the original row. It is not a release accuracy or robustness claim.

| Backend | Records | Selected | CER-worse selected | CER false-correction rate | WER-worse selected | All P50/P95 ms | Selected P50/P95 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| onnxruntime | 60 | 22 | 0 | 0.0000 | 0 | 99.03/154.96 | 117.38/164.07 |
| openvino | 60 | 22 | 0 | 0.0000 | 0 | 37.27/84.48 | 42.84/69.01 |
| onnxruntime | 192 | 14 | 5 | 0.3571 | 2 | 7.23/119.99 | 114.79/168.72 |
| openvino | 192 | 14 | 5 | 0.3571 | 2 | 3.69/41.37 | 35.07/97.15 |

The current policy remains opt-in. Any production threshold must be selected from a larger, quality-controlled labeled set and must include a maximum false-correction rate and an explicit latency budget.
