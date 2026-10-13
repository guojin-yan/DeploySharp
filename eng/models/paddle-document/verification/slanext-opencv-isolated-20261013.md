# SLANeXt OpenCV DNN isolated boundary (2026-10-13)

Four exact SLANeXt artifacts were run one per `dotnet test` process through the production OpenCV DNN provider. The pass gate required session creation, inference on the official `table_recognition.jpg`, and successful `PaddleDocumentTableStructureDecoder` output with more than ten tokens and at least one cell.

## Protocol

| Item | Value |
| --- | --- |
| Source base | `143d49e0b73781229e16616e78897814a416e8a1` plus the probe in this evidence change |
| OS | Windows `10.0.26200` |
| .NET SDK / target | `10.0.401` / `net10.0` |
| OpenCV | JYPPX API `5.0.0`, native OpenCV `5.0.0` |
| Input | `table_recognition.jpg`, SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Input contract | `x [1,3,512,512]` |
| Specialized output contract | `fetch_name_0 [1,sequence,8]`; `fetch_name_1 [1,sequence,50]` |

The output contract fixes the known Batch=1 axis and leaves only sequence length dynamic. This avoids stopping at DeploySharp's deliberate multiple-wildcard guard and lets each probe reach the OpenCV importer.

## Results

| Exact artifact | Kind | SHA-256 | Result |
| --- | --- | --- | --- |
| `paddle-table/slanext-wired` | original Release | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` | unsupported |
| `paddle-table/slanext-wireless` | original Release | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | unsupported |
| `paddle-table/slanext-wired-openvino-compat` | derived compatibility graph | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` | unsupported |
| `paddle-table/slanext-wireless-openvino-compat` | derived compatibility graph | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | unsupported |

All four fail at `Loop.0` during specialized session creation, before inference and before the Decoder:

```text
OpenCV(5.0.0) ONNXImporter: DNN/ONNX/Attribute[body]: 'Graph' is not supported
(-213: The function/feature is not implemented)
```

The OpenVINO compatibility transformation only alpha-renames Loop-body parameters, so it does not remove the `Loop` or its graph-valued body. The identical OpenCV result for original and compatibility graphs is expected from that limited transformation.

## Boundary

These four exact OpenCV 5.0 combinations are now `unsupported`, not `unverified`. This does not change their ORT evidence, the derived graphs' OpenVINO evidence, or claim a universal result for future OpenCV versions. Per the project policy for third-party importer blockers, no deeper OpenCV importer rewrite is attempted here.

Machine-readable paths, sizes, hashes and exception details are in [`slanext-opencv-isolated-20261013.json`](slanext-opencv-isolated-20261013.json).
