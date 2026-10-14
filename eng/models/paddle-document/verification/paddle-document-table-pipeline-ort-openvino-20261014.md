# PP-Structure table task pipeline: ORT/OpenVINO benchmark (2026-10-14)

This record executes table classification → wired table-cell detection → SLANeXt structure decoding → HTML generation on one official table image. It is a fixed-host execution and repeated-latency report, not a quality score.

## Protocol and input

- Host: JYPPX, Microsoft Windows 10.0.26200, .NET 10.0.12, 16 logical processors.
- Input: `E:\Model\PaddleDocument\validation\table_recognition.jpg`; SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c`; size 1654×1300.
- Protocol: 5 warm-ups + 50 measured page runs, one page per invocation; exact output-contract assertions on every measured run.
- Each backend uses its own CPU pipeline/session. OpenVINO SLANeXt uses the separately derived, SHA-recorded compatibility graph; ORT uses the official graph.

## Results

| Backend | Total P50/P95 (ms) | Classification P50/P95 (ms) | Cell detector P50/P95 (ms) | SLANeXt P50/P95 (ms) | Table output contract |
|---|---:|---:|---:|---:|---|
| onnxruntime-cpu | 1532.50 / 1618.06 | 8.38 / 40.05 | 496.94 / 537.71 | 1016.58 / 1081.85 | wired_table, 300 candidates, 13 structure regions, 24 tokens, HTML SHA-256 `53de44f9af566b55f7a583489d157d4e75e5ba6a834a77f0245712b16e3d2557` |
| openvino-CPU | 1288.99 / 1558.58 | 4.96 / 6.18 | 376.57 / 486.10 | 903.54 / 1061.81 | wired_table, 300 candidates, 13 structure regions, 24 tokens, HTML SHA-256 `53de44f9af566b55f7a583489d157d4e75e5ba6a834a77f0245712b16e3d2557` |

Both backends returned the same classification label, candidate count, structure-region count, token count and exact HTML SHA-256. All 50 measured invocations per backend preserved its baseline output contract.

## Provenance and limits

- onnxruntime-cpu: [paddle-document-table-pipeline-20261014-onnxruntime.json](paddle-document-table-pipeline-20261014-onnxruntime.json) (SHA-256 `32bfe939aa1a15d31c8200589d79e110bb641f5e3c006f174afffe0b276fd1c4`). Models: table classification `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45`, cell detector `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6`, SLANeXt `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f`.
- openvino-CPU: [paddle-document-table-pipeline-20261014-openvino.json](paddle-document-table-pipeline-20261014-openvino.json) (SHA-256 `e9219278698886e49f92f5f28607fac4f5717b4896146a8cad4af2ca5d8f0944`). Models: table classification `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45`, cell detector `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6`, SLANeXt `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0`.
- The source image has no cell/table annotations. Candidate count uses the current zero score threshold, so it is not a precision/recall measurement.
- Timings are for this host and exact graph/runtime configuration; CPU frequency was not locked. They are not a cross-device ranking or a guarantee for other inputs.
- OpenVINO uses the derived SLANeXt compatibility graph; this does not make the original Loop graph directly importable. TensorRT and OpenCV are outside this paired report.

Reproduce from the repository root by setting `DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1`, `DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_TABLE_PIPELINE=1`, `DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_PIPELINE_BENCHMARK=1` and `DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_PIPELINE_EVIDENCE_PATH` to a path containing `{backend}`, then run `dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~PaddleDocumentTablePipelineIntegrationTests`. Summarize the paired outputs with `pwsh -File eng/models/paddle-document/scripts/Summarize-PaddleDocumentTablePipelineEvidence.ps1`.
