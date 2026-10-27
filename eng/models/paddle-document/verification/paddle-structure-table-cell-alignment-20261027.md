# PP-Structure table-cell alignment contract (2026-10-27)

The traditional table sample now exports an optional structure-slot to detector-region mapping separately from the raw pipeline JSON and structural HTML. The mapper parses SLANeXt's empty table tags with DTD and external resolution disabled, checks page/source provenance and exact cell counts, then orders independently detected boxes by row top and horizontal position. It rejects malformed/incomplete structure, uncertain row grouping, overlapping cells, invalid/out-of-source detector boxes and row spans instead of silently assigning partial text positions. It does not use the SLANeXt location polygons, which are out of source bounds on the official thin table example.

Focused contract tests: `PaddleDocumentTableCellAlignmentTests`, 5/5 passed. The official `table_recognition.jpg` (`acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c`) was run once with the sample's default 0.5 detector threshold, ONNX Runtime CPU and local original ONNX models. The model SHA-256 values are `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45` (table classifier), `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` (wired RT-DETR), and `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` (wired SLANeXt). It returned `wired_table`, four structure rows, 13 cells and 13 detected regions. All 13 slots were associated in row order with detector indexes `0,8,7,1,3,9,5,2,4,10,12,6,11`. The alignment JSON SHA-256 is `96f1226a74231b5d82859e55c2638d99fe5018fdb3526e4aa33dee3ec2896130`; the raw output remains outside Git under `E:\DeploySharpEvidence\paddle-structure-table-alignment-20261027`. The structure decoder still warns `table-cell-geometry-outside-source:12/13`.

After the row-span rejection was added, the focused contracts remained 5/5. `DeploySharp.Visual` built for `net46` and `netstandard2.0`, and the sample built for `net8.0`, all with zero warnings and errors. The official-image inference was not needlessly repeated after that contract-only change.

```powershell
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~PaddleDocumentTableCellAlignmentTests
dotnet run --project samples/02-visual/paddle-structure-table/PaddleStructureTable.csproj -c Release -- `
  --image E:\Model\PaddleDocument\validation\table_recognition.jpg `
  --output E:\DeploySharpResults\table `
  --table-type auto --backend ort --model-dir E:\Model\PaddleDocument\onnx
```

This is a single-image, unannotated alignment candidate, not measured cell matching accuracy, OCR transcription, wired/wireless generalization, or latency evidence. It does not close full table text reconstruction; independently labeled cell geometry and text, row-span handling and a genuine OCR stage are still required before HTML text filling can be claimed. Historic zero-threshold `5+50` benchmark reports remain unchanged.
