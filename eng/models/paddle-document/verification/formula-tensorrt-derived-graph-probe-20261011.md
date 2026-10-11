# FormulaNet derived TensorRT graph probe (2026-10-11)

This is a diagnostic-only experiment on `paddle-formula/pp-formulanet-plus-s`. It creates independent ONNX copies and never replaces the official Release artifact. Passing `onnx.checker` is not a TensorRT, decoder, quality or performance admission.

| Host | Value |
|---|---|
| Device | Windows 10.0.26200, RTX 3060 Laptop, compute capability 8.6 |
| Runtime | Driver 576.02, CUDA 12.9, cuDNN 9.22.0, TensorRT 11.0.0.114-cu12 |
| Official ONNX | 231,878,904 bytes, SHA-256 `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d` |
| Profile | `x:1x1x384x384` for min/opt/max |

## Results

1. `polygraphy surgeon sanitize --fold-constants --cleanup --toposort` preserved the 388-node graph and passed `onnx.checker`, but TensorRT failed in the Loop parser at `checkBitwiseAnd`.
2. Replacing the integer mask operators with boolean `Not`/`And` allowed TensorRT 11 to finish parsing. The Builder then returned a **zero-byte/empty Engine** because one Loop recurrence changed shape from `[3]` to `[1]` (`arange` versus `scale.15`).
3. Removing one unused scalar carry did not change that recurrence boundary.
4. Broadcasting that scalar carry to `[3]` moved the failure to the next state: `Identity.149 [-1,3]` versus `concat.4 [-1,6]`. This exposes a growing autoregressive Loop state rather than a simple importer typo.

All four derived files passed `onnx.checker`, but none produced an Engine. There was no Decoder invocation, no token parity result and no timing measurement. The experiment therefore does **not** add any TensorRT check mark to the six-model formula matrix and does not justify publishing a rewritten FormulaNet graph.

The exact model hashes, transformations, diagnostics and reproduction entry point are in the [machine-readable report](formula-tensorrt-derived-graph-probe-20261011.json). Generate the diagnostic copies with [`Prepare-FormulaTensorRtCompatibilityVariants.py`](../scripts/Prepare-FormulaTensorRtCompatibilityVariants.py); use a separate TensorRT installation and keep all generated files outside Git.

The remaining work is a real TensorRT-compatible export or a manually split encoder/decoder with fixed-shape recurrence state. Ordinary graph sanitization is insufficient; any future rewrite must be validated against ORT tokens and the FormulaNet decoder before it can be considered.
