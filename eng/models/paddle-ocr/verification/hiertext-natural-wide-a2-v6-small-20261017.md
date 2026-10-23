# HierText natural wide-line A2 execution slice — PP-OCRv6 Small (2026-10-17)

Metric correction, 2026-10-23: the previous WER of 291.20% used character distance divided by word count and is invalid. The table below is recomputed from unchanged original predictions using actual word-token distance. No model execution or timing was repeated. The original A2 requirement is normalized recognition width **>3,200 pixels**, not 3,200 characters; previous character-count gate wording is superseded.

This report used nine source-linked images and 13 annotated geometry candidates. Actual maximum recognition window width was **320 pixels**, not the library default 3,200. Protocol: SlidingWindow overlap 0.2, maximum windows 32, batch 16, stage channels 2, association bbox IoU threshold 0.3, 1 warm-up + 1 measured page run.

| Backend | Candidate lines | Matched | Match rate | Mean bbox IoU | Mean CER | Corrected mean WER | Total windows | Max natural width | Max managed allocation | P50/P95 page ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| onnxruntime-cpu | 13 | 8 | 61.54% | 0.697 | 23.57% | 44.36% | 79 | 3233 | 449849464 | 1478.08 / 2165.53 |
| openvino-cpu | 13 | 8 | 61.54% | 0.697 | 23.57% | 44.36% | 79 | 3233 | 449877104 | 975.81 / 1493.3 |

Cross-backend text matches among associated candidates: **8/8**; text mismatches: **0**. **5** candidate pairs are unmatched and excluded. The former 13/13 claim incorrectly included five pairs of empty placeholders.

CER/WER are macro means over associated candidates with ordinal, case-sensitive text and no implicit normalization. Official illegibility and one-pixel-height annotations remain in this historical association diagnostic and are explicitly identified in JSON; use the newer GT-quadrilateral report for stratified recognition quality. Bbox IoU is not the official polygon metric and managed allocation is not RSS. A2 repeat/single-character seam quality, additional model/backend evidence, formal 5+50 performance and general dataset accuracy remain open.
