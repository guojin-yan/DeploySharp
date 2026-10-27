# SLANeXt OpenVINO compatibility Release delivery (2026-10-27)

The existing `models-paddleocr` Release now contains two additional, individually downloadable derived ONNX files. The original `slanext-wired.onnx` and `slanext-wireless.onnx` assets were not replaced. The checked-in [compatibility manifest](slanext-openvino-compatibility.json) records source and derived SHA-256 values, sizes, transformation and stable compatibility IDs.

| Model ID for `PaddleOcrReleaseClient.GetModelAsync` | Release asset | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `paddle-table/slanext-wired-openvino-compat` | `slanext-wired-openvino-compat.onnx` | 367743498 | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` |
| `paddle-table/slanext-wireless-openvino-compat` | `slanext-wireless-openvino-compat.onnx` | 367743498 | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` |

The GitHub Release API returned the same size and `sha256:` digest for both assets. `paddleocr-release-catalog.json` changed from 28 to 30 PP-Structure rows, retaining all previous rows and both generative Bundles; its published digest is `b1f2760ad91b555e8698cc0f82c8937980a5793937723700c66a18fad92495e2`. `SHA256SUMS` retained all 71 previous entries and added only these two files; its published digest is `6da6a7a7a37ef4a402ae4ab250e0bc8d81aa54e5c5fb11ce3953b0c32c0a2e3b`. Release notes identify the date and separate-asset policy. The prior catalog/checksum files remain in the local evidence folder `E:\DeploySharpEvidence\slanext-release-compat-20261027\original` for recovery.

Recheck metadata without downloading the approximately 736 MB of models again:

```powershell
gh release view models-paddleocr --repo guojin-yan/DeploySharp --json assets `
  --jq '.assets[] | select(.name=="slanext-wired-openvino-compat.onnx" or .name=="slanext-wireless-openvino-compat.onnx") | {name,size,digest}'
```

This is a delivery and catalog check. The earlier [OpenVINO/ORT output and table-decoder comparison](slanext-openvino-compatibility.md) remains the runtime evidence; publishing files does not establish table accuracy, TensorRT compatibility, new device support or improved speed. No model inference or performance matrix was rerun for this delivery.
