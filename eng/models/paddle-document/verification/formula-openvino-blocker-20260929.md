# FormulaNet / UniMERNet OpenVINO blocker (2026-09-29)

An external OpenVINO CPU probe was attempted against the six locally acquired formula exports using the official formula image and tokenizer setup. The probe was intentionally stopped after the first native crash.

## Observed boundary

- Runtime: OpenVINO C# API 3.3.1, OpenVINO runtime 2026.2.1, Windows x64, .NET 10.
- First attempted artifact: `pp-formulanet-plus-s.onnx`.
- Model SHA-256: `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d`.
- Input: `general_formula_rec_001.png`.
- Failure phase: `ov_core_read_model_utf8` while loading the ONNX graph, before session creation or inference.
- Process result: native access violation `0xC0000005`; the test host terminated, so managed exception handling cannot convert this into a normal per-model failure row.

The remaining five formula artifacts were not attempted in this run because continuing after a native importer crash would make the result unsafe and ambiguous. The model/backend matrix therefore keeps the aggregate formula OpenVINO cell as `△` (not all six artifacts have been attempted), while the precise `pp-formulanet-plus-s` + OpenVINO combination is recorded as a current native importer/runtime blocker pending an isolated repro or a compatible graph.

The existing ORT CPU evidence for all six models remains valid. This probe does not change the ORT result or claim formula accuracy.
