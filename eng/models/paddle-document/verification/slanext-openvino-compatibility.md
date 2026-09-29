# SLANeXt OpenVINO compatibility artifact

The official `slanext-wired.onnx` and `slanext-wireless.onnx` files remain the source assets. The current OpenVINO importer rejects their `Loop` graphs because Loop-body formal parameter names collide with captured outer values. DeploySharp keeps the source graphs unchanged and creates explicit compatibility graphs by alpha-renaming only those local parameters.

Reproduce both files and the machine-readable manifest with:

```powershell
& .\eng\models\paddle-document\scripts\Build-PaddleDocumentOpenVinoCompatibility.ps1 `
  -ModelRoot E:\Model\PaddleDocument\onnx `
  -OutputRoot E:\Model\PaddleDocument\onnx-normalized `
  -Python E:\Model\PaddleDocument\paddle3\python.exe `
  -ManifestPath E:\Model\PaddleDocument\onnx-normalized\slanext-openvino-compatibility.json
```

The wrapper never overwrites source files, runs `onnx.checker`, records source and derived SHA-256 values, and assigns stable compatibility IDs. The checked-in manifest is [slanext-openvino-compatibility.json](slanext-openvino-compatibility.json).

| Variant | Source SHA-256 | Derived SHA-256 | Planned Release asset |
| --- | --- | --- | --- |
| wired | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` | `slanext-wired-openvino-compat.onnx` |
| wireless | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | `slanext-wireless-openvino-compat.onnx` |

The two derived graphs were executed through OpenVINO and compared with the original ONNX Runtime outputs. Output shapes, values within absolute tolerance `0.001`, decoded table tokens and table markup matched for both variants. This is backend compatibility evidence for the supplied table image; it is not a table dataset accuracy score.

The original ONNX files remain the public source artifacts. The derived files are intended to be uploaded as separate assets in the `models-paddleocr` Release. Until that upload occurs, their manifest status is `planned-separate-assets` and consumers should generate them locally and pass the derived path and its own SHA-256 to `ModelArtifact`.
