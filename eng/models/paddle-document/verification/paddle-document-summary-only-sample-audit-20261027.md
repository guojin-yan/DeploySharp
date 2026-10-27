# PP-Structure TensorRT summary-only timing audit (2026-10-27)

The [current matrix index](paddle-document-backend-matrix-current-20261017.md) maps 69 formal-protocol evidence bundles. Of these, 54 embed measured samples and 2 link tracked raw reports. The remaining 13 contain P50/P95 summaries but no tracked per-call samples. This audit does not change any artifact/backend execution status or rerun a model.

| Historical summary group | Bundles | Where the old samples were lost | Future report change |
| --- | ---: | --- | --- |
| TensorRT 10 table classification | 1 | Test logged P50/P95 without its `samples` list; saved TRX has no per-call array | Console JSON includes `samplesMs` |
| TensorRT 11 static layout/NMS | 1 | Test logged per-model percentiles without the lists | Console JSON includes `samplesMs` |
| TensorRT 10/11 dynamic layout Batch=2 | 7 | Saved report `timing` objects omitted measured calls | Report `timing.samplesMs` is written |
| TensorRT 11 static PicoDet layout | 1 | Test logged per-model percentiles without the lists | Console JSON includes `samplesMs` |
| TensorRT table-cell detection | 1 | Saved report model rows omitted measured calls | Each passed model row includes `samplesMs` |
| TensorRT 11 seal Batch=2 | 2 | Saved report `timing` objects omitted measured calls | Report `timing.samplesMs` is written |

The existing reports declare at least 5 warm-ups and 50 measured calls, but the historical P50/P95 values in these 13 bundles cannot be independently recomputed from their registered JSON. Existing test-result attachments and local evidence directories were inspected; that is not proof that no separate raw copy exists elsewhere. Do not synthesize samples from percentiles or mark these combinations untested. The updated test code retains the actual measured list on future runs; it does not retroactively populate the historical reports. Only rerun an exact model/backend combination when a product decision requires independently auditable timing under a stated input and device protocol.

Percentile conventions also differ across existing bundles: 36 P50/P95 pairs in the multipage, table-pipeline and Chart2Table reports exactly reproduce linear interpolation over their embedded 50 samples, while the recovered TensorRT orientation run uses nearest rank. Both conventions are internally reproducible; do not combine their percentiles into a purported unified ranking without stating the method and aligning the test protocol.

Sources: [TensorRT layout, classification and PicoDet tests](../../../../tests/DeploySharp.Visual.TensorRT.Tests/PaddleDocumentTensorRtExternalIntegrationTests.cs), [table-cell tests](../../../../tests/DeploySharp.Visual.TensorRT.Tests/PaddleDocumentTensorRtTableCellExternalIntegrationTests.cs), and [seal tests](../../../../tests/DeploySharp.Visual.TensorRT.Tests/PaddleDocumentTensorRtServerSealExternalIntegrationTests.cs). The new report fields compile in Release/net10.0; no external model test was run for this audit.
