# Natural wide-line backend comparison: onnxruntime / opencv-dnn (2026-10-23)

Status: **decision-parity**. Both native runs use the same production Visual DLL and pinned source images, labels, model and dictionary hashes. Each backend executes Small/Medium × Clamp/SlidingWindow × 13 labelled quadrilaterals (52 calls). DET/CLS are bypassed, height is 48, maximum width is 3,200, overlap is 0.2 and batch/Session count is one. This comparison explicitly joins two real native reports; one-backend self-comparisons are not counted as backend pairs.

| Compared contract | Matched / compared |
| --- | ---: |
| textMatched | 52 / 52 |
| emittedClassesMatched | 52 / 52 |
| metricsMatched | 52 / 52 |
| seamDecisionsMatched | 52 / 52 |
| Raw window text | 52 / 52 |
| Raw window emitted classes | 52 / 52 |

Native runtimes: 1.28.0; 5.0.0. The [machine report](paddleocr-hiertext-natural-wide-opencv-parity-20261023.json) includes per-row hashes/metrics, exact input report SHA values, DLL identities and any freshly built engine identities. No raw annotation text or image is copied into this report.

Same 13 development-diagnostic HierText lines / 9 source images; 11 main-quality lines and two separate diagnostics. Same DLL, model/dictionary/source identities and preprocessing/window geometry required. Comparison covers text hashes, emitted classes, CER/WER and seam decisions, not native tensors/confidences bitwise parity, independent accuracy, page accuracy, performance, or other exact model/backend combinations. DET/CLS bypassed; CPU crop/CTC/merge also used with TensorRT. A2 stays open.

Reproduce with `Compare-PaddleOcrNaturalWideBackends.ps1 -ReferenceReportPath <real-reference.json> -CandidateReportPath <real-candidate.json> -ReferenceBackend onnxruntime -CandidateBackend opencv-dnn -OutputJsonPath <new-summary.json> -OutputMarkdownPath <new-summary.md>`. Identity, protocol, duplicate-key or geometry mismatch rejects comparison. Prediction differences are reported as differences rather than hidden or converted to a pass.
