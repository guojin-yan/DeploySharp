# OCR enhancement safety and cost summary

This report is a bounded diagnostic over parent-linked degraded crops. A false correction means a selected candidate has a higher case-folded CER or WER than the original row. It is not a release accuracy or robustness claim.

| Evidence | Backend | Records | Selected | CER-worse selected | CER false-correction rate | WER-worse selected | All P50/P95 ms | Selected P50/P95 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `paddleocr-degraded-enhancement-onnxruntime` | onnxruntime | 240 | 16 | 5 | 0.3125 | 2 | 8.40/177.36 | 123.54/225.75 |
| `paddleocr-degraded-enhancement-openvino` | openvino | 240 | 16 | 5 | 0.3125 | 2 | 10.56/110.41 | 81.02/197.14 |

The current policy remains opt-in. Any production threshold must be selected from a larger, quality-controlled labeled set and must include a maximum false-correction rate and an explicit latency budget.
