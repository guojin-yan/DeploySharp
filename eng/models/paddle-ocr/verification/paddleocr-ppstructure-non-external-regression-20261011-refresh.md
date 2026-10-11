# PaddleOCR / PP-Structure non-external regression refresh (2026-10-11)

Environment: Windows `10.0.26200.0`, .NET SDK `10.0.401`, Release/net10.0, `--no-restore`, `TestCategory!=ExternalModels`, source revision `1e80df5e9d0aa03b4b0ba32ebb690a74d028b884`. The working tree was dirty; this is a source snapshot, not a clean-checkout claim.

| Project | Passed | Skipped | Failed | Total |
| --- | ---: | ---: | ---: | ---: |
| Core | 33 | 0 | 0 | 33 |
| ModelFactory | 63 | 2 | 0 | 65 |
| ModelPack.Json | 29 | 3 | 0 | 32 |
| Visual | 513 | 5 | 0 | 518 |
| Visual.OpenCV | 99 | 4 | 0 | 103 |
| **Total** | **737** | **14** | **0** | **751** |

The skipped cases are explicit external-model, native-runtime or checkpoint gates (Qwen/Whisper/Chart2Table and platform-specific filesystem checks). They are not failures and must not be converted into support checkmarks. This run confirms the current non-external contracts after the OCR enhancement and formula structural-token increments; it does not close external model accuracy, full backend matrices, controlled 5/50 performance, or soak requirements.

Machine-readable record: [`paddleocr-ppstructure-non-external-regression-20261011-refresh.json`](paddleocr-ppstructure-non-external-regression-20261011-refresh.json).
