# HierText natural wide-line A2 execution slice — PP-OCRv6 Small (2026-10-17)

This report runs the nine source-linked HierText images whose annotated lines have projected recognition width ≥3,200 at target height 48. It exercises real SlidingWindow execution and source provenance; it does **not** close the natural continuous 3,200-character gate because the longest annotated candidate is 182 characters.

Protocol: SlidingWindow overlap 0.2, maximum windows 32, batch 16, stage channels 2, association bbox IoU threshold 0.3, 1 warm-up + 1 measured page run.

| Backend | Candidate lines | Matched | Match rate | Mean bbox IoU | Mean CER | Mean WER | Total windows | Max width | Max managed allocation | P50/P95 page ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| onnxruntime-cpu | 13 | 8 | 61.54% | 0.697 | 23.57% | 291.2% | 79 | 3233 | 449849464 | 1478.08 / 2165.53 |
| openvino-cpu | 13 | 8 | 61.54% | 0.697 | 23.57% | 291.2% | 79 | 3233 | 449877104 | 975.81 / 1493.3 |

Cross-backend candidate text matches: **13/13**; mismatches: **0**. Both backends matched the same candidate geometry in 8/13 pairs.

The report uses bounding-box IoU only for candidate association and records managed allocation rather than RSS. CER/WER are not official HierText scores because this selection is geometry-based and detector output is compared to individual line annotations. The natural continuous ≥3,200-character A2 gate, formal 5+50 performance, and broader quality thresholds remain open.
