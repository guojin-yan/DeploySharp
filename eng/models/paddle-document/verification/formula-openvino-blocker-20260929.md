# FormulaNet / UniMERNet OpenVINO blocker (2026-09-29)

An external OpenVINO CPU probe was first attempted against the six locally acquired formula exports using the official formula image and tokenizer setup. The initial single-process probe stopped after a native crash; a follow-up isolated matrix ran each model in its own process. The complete per-model result is in [`formula-openvino-isolated-20260929.json`](formula-openvino-isolated-20260929.json).

## Observed boundary

- Runtime: OpenVINO C# API 3.3.1, OpenVINO runtime 2026.2.1, Windows x64, .NET 10.
- First attempted artifact: `pp-formulanet-plus-s.onnx`.
- Model SHA-256: `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d`.
- Input: `general_formula_rec_001.png`.
- Failure phase: `ov_core_read_model_utf8` while loading the ONNX graph, before session creation or inference.
- Process result: native access violation `0xC0000005`; the test host terminated, so managed exception handling cannot convert this into a normal per-model failure row.

The isolated follow-up attempted all six artifacts. Plus-S/M/L and FormulaNet-S/L all fail during the same OpenVINO `Loop-18` importer check; UniMERNet reaches the native `ov_core_read_model_utf8` access violation. The exact matrix rows are therefore marked `✗` for this OpenVINO runtime. This is a backend admission result, not a formula accuracy result.

The existing ORT CPU evidence for all six models remains valid. This probe does not change the ORT result or claim formula accuracy.
