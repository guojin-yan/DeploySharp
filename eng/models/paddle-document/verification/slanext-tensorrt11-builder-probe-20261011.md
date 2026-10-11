# SLANeXt TensorRT 11 builder probe (2026-10-11)

The original `paddle-table/slanext-wired` and `paddle-table/slanext-wireless` Release ONNX files were tested with TensorRT `11.0.0.114-cu12` using `trtexec --skipInference --verbose`. TensorRT parsed both graphs, but neither could create an Engine:

```text
IRecurrenceLayer must have inputs with same shape.
First input ... has shape [] and second input ... has shape [-1,8].
```

Both commands exited with code `1` during `IBuilder::buildSerializedNetwork`; no serialized Engine was produced and no DeploySharp TensorRT session or table Decoder was entered. The exact model SHA-256 values, runtime command and machine-readable error are in [`slanext-tensorrt11-builder-probe-20261011.json`](slanext-tensorrt11-builder-probe-20261011.json). Full native logs remain local under `artifacts/` and are not required for reproduction.

This is a precise current-runtime blocker for the two original SLANeXt graphs, not a claim that every TensorRT release rejects them. The separately generated alpha-renamed OpenVINO compatibility graphs have a different purpose and remain unverified for TensorRT. The next viable implementation path is a fixed-state/encoder-decoder export or a TensorRT plugin/graph rewrite that preserves recurrence shapes; local surgery must not be published as a supported model without token and HTML parity evidence.
