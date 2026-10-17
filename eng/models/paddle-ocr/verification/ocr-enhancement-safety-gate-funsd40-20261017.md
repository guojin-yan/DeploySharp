# PaddleOCR enhancement safety gate

Overall status: **blocked**.

Thresholds: records >= 200; CER/WER false-correction rate <= 0; all-run P95 <= 250 ms.

| Backend | Records | Selected | CER false-correction | WER false-correction | All P95 ms | Status |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| onnxruntime | 240 | 16 | 31.25% | 12.50% | 177.36 | blocked |
| openvino | 240 | 16 | 31.25% | 12.50% | 110.41 | blocked |

The current production default remains disabled. The controlled FUNSD evidence contains selected CER-worse candidates, so this gate is expected to remain blocked until an independent natural-quality split, business-field error budget, and a separately calibrated latency budget are available.

This is a fail-closed safety and cost gate over controlled parent-linked degraded-crop evidence. It is not page/line accuracy, robustness, or approval of a production enhancement default. A pass still requires an independent natural-quality split and business-field error budget.
