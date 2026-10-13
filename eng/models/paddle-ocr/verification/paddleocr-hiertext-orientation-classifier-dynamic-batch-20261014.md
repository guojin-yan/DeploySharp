# PP-LCNet HierText 0/180 orientation dynamic-batch contract (2026-10-14)

This record runs the real PP-OCRv5 Mobile PP-LCNet text-line orientation classifier on 128 linked horizontal HierText crops. Each crop is submitted as one dynamic batch of two rows: the source crop (known `0°`) and an OpenCV-generated `180°` rotation (known by construction). The test is intended to verify preprocessing, `[2,3,80,160] → [2,2]` execution, row order and ORT/OpenVINO parity.

| Backend | Rows | 0°/180° constructed-label matches | Observed rate | Rejected | 0° predictions | 180° predictions |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 256 | 134 | 52.34% | 129 | 234 | 22 |
| OpenVINO CPU | 256 | 134 | 52.34% | 129 | 234 | 22 |

Input tensor digests, class indices and predictions matched across the two CPU backends for all 256 rows (`0` mismatches). The machine-readable aggregate is [`paddleocr-hiertext-orientation-classifier-dynamic-batch-20261014.json`](paddleocr-hiertext-orientation-classifier-dynamic-batch-20261014.json); the row-level JSON remains in the local external evidence directory used by the test.

## Boundary

The observed 52.34% rate is diagnostic only. HierText has no upright-versus-inverted labels, and many selected crops are very small; the 180° labels here are generated transformations, not natural ground truth. The result therefore closes only the PP-LCNet preprocessing, dynamic-batch decoder and CPU parity contract. It does not close natural 0/180 accuracy, vertical 90/270 handling, CER/WER, or the A3 automatic-orientation quality gate. The model itself exposes only two classes (`0°` and `180°`); vertical text still requires explicit geometric rotation/candidate handling or a separate four-class model.
