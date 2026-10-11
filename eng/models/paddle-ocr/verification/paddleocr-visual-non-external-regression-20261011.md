# Visual non-external regression (2026-10-11)

- Source snapshot: `d01a48d` with a dirty working tree; this record intentionally does not claim a clean checkout.
- Command: `dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release --no-restore --filter TestCategory!=ExternalModels`
- Target: `net10.0`, Release.
- Result: **513 passed / 5 skipped / 0 failed** (518 total).

The five skipped cases are checkpoint-gated Whisper, Chart2Table and Qwen tokenizer integrations. They are external-model/environment skips, not failures and not support evidence. The run validates current Visual contracts, including OCR enhancement consensus and formula structural-token diagnostics; it does not close external model quality, OpenVINO/TensorRT/OpenCV matrices, controlled 5/50 performance, or soak gates.

Machine-readable result: [`paddleocr-visual-non-external-regression-20261011.json`](paddleocr-visual-non-external-regression-20261011.json).
