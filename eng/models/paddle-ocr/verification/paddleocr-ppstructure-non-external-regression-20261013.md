# PaddleOCR / PP-Structure current-revision regression (2026-10-13)

Environment: Windows `10.0.26200`, .NET SDK `10.0.401`, Release/net10.0, `--no-restore`, `TestCategory!=ExternalModels`, source revision `c7589d2`. The working tree was dirty; this is a source snapshot, not a clean-checkout claim.

| Project | Passed | Skipped | Failed | Total |
| --- | ---: | ---: | ---: | ---: |
| Core | 33 | 0 | 0 | 33 |
| Extensibility | 8 | 0 | 0 | 8 |
| ModelFactory | 63 | 2 | 0 | 65 |
| ModelPack.Json | 29 | 3 | 0 | 32 |
| Visual | 522 | 5 | 0 | 527 |
| Visual.OpenCV | 99 | 4 | 0 | 103 |
| Backend.OnnxRuntime | 33 | 0 | 0 | 33 |
| Backend.OpenVINO | 25 | 0 | 0 | 25 |
| Backend.OpenCV | 32 | 0 | 0 | 32 |
| Backend.TensorRT | 60 | 0 | 0 | 60 |
| Visual.TensorRT | 8 | 0 | 0 | 8 |
| **Total** | **912** | **14** | **0** | **926** |

The 14 skipped cases remain explicit external-checkpoint, native-runtime, network or platform gates. They are not failures, but they are also not support evidence.

## Build and documentation checks

- [Source project build](paddleocr-ppstructure-source-build-20261013.md): `14/14` source projects passed Release `--no-restore` builds.
- [Sample project build](paddleocr-ppstructure-samples-build-20261013.md): `10/10` sample projects passed using their declared target frameworks; one project emitted two warnings and no errors.
- [Documentation link audit](../../paddle-document/verification/document-link-audit-20261013-p7.json): `671` maintained local Markdown links checked, `0` broken.

## Boundary

This record refreshes P7 contracts on the current source snapshot. It does not close clean-checkout restore/build, external model quality, full backend matrices, controlled P50/P95 performance, packaging, or long-running soak. External-model skips must not be converted into support checkmarks.

Machine-readable record: [`paddleocr-ppstructure-non-external-regression-20261013.json`](paddleocr-ppstructure-non-external-regression-20261013.json).
