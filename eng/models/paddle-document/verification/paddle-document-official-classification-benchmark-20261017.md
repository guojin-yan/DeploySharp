# PP-Structure official classification benchmark (2026-10-17)

This report records the official document-orientation and table-classification examples on one Windows host. Each exact model/backend row uses 5 warm-ups followed by 50 measured synchronous calls with batch=1 and one session; model loading, image decode and hashing are excluded.

- Device: **JYPPX**; OS: Microsoft Windows NT 10.0.26200.0; runtime: .NET 10.0.12.
- Source revision: 5786d637a24391e3a7ebd90ba9b33608486df0ff; rows: **6** (2 models × 3 backends).
- Input contracts: `img_rot180_demo.jpg` for document orientation and `table_recognition.jpg` for table classification; image SHA-256 values are recorded in the JSON.
- All six original per-call reports are tracked in `paddle-document-official-classification-raw-20261017/`. The matrix audit checks each report's SHA-256, 50 samples, identity and recomputed P50/P95 against the summary; the original test-output directory is not needed to audit these numbers.

| Model | Backend | Score | Pre P50/P95 ms | Infer P50/P95 ms | Post P50/P95 ms | Total P50/P95 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| paddle-doc/pp-lcnet-x1-0-doc-ori | ort | 0.89236003 | 2.9922/5.4578 | 3.5657/4.4251 | 0.0163000000000002/0.0272000000000006 | 6.414/9.0939 |
| paddle-table/pp-lcnet-x1-0-table-cls | ort | 0.8442081 | 0.8845/1.2703 | 3.2605/3.8856 | 0.0133000000000001/0.0207999999999999 | 4.1979/5.0576 |
| paddle-doc/pp-lcnet-x1-0-doc-ori | openvino | 0.8923599 | 0.6928/1.2665 | 1.574/2.035 | 0.0116000000000001/0.0188999999999999 | 2.3169/3.2896 |
| paddle-table/pp-lcnet-x1-0-table-cls | openvino | 0.8442093 | 0.7675/1.2901 | 1.8093/2.4767 | 0.00970000000000004/0.0133999999999999 | 2.5544/3.589 |
| paddle-doc/pp-lcnet-x1-0-doc-ori | opencv | 0.8923601 | 0.9522/1.2938 | 4.6026/5.1468 | 0.0190999999999999/0.0234999999999994 | 5.567/6.3774 |
| paddle-table/pp-lcnet-x1-0-table-cls | opencv | 0.8442093 | 1.1/1.3395 | 4.9124/6.2669 | 0.00649999999999995/0.00850000000000062 | 6.0337/7.6141 |

All six rows passed their exact official-example class and score contracts. The OpenCV rows are CPU DNN execution; the table below is not a full PP-Structure support matrix and does not generalize to layout, table-cell, formula, seal, Chart2Table or SLANeXt artifacts.

Reproduction: set `DEPLOYSHARP_PADDLE_DOCUMENT_ACCURACY=1` and `DEPLOYSHARP_PADDLE_DOCUMENT_ROOT` to the local PaddleDocument root, then run `PaddleDocumentOfficialExampleTests.ClassificationMatchesOfficialExampleAndPreservesProbabilities` in `tests/DeploySharp.Visual.OpenCV.Tests` with Release/net10.0. The test source is [`PaddleDocumentOfficialExampleTests.cs`](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentOfficialExampleTests.cs).
