# PP-OCR core seven-model three-image CPU backend check (2026-10-23)

## Result

All 21 model/image pairs completed the full `det → crop → cls/orientation → rec → merge` pipeline on both ONNX Runtime CPU and OpenVINO CPU. The image and model artifact identities match for `21/21`; detected and recognized region counts match for `21/21`; ordered text SHA-256 fingerprints match for `21/21`.
Canonical result hashes are comparable for `21/21` pairs; bit-exact hashes match for `0/21`. All geometry/confidence tolerance checks and CTC token decisions pass, so the status is `bitwise-hash-differences-within-tolerance` rather than a semantic output mismatch.

Exact component fingerprints: geometry `0/21` match; recognition (confidence, character-set provenance and CTC token trace) `0/21` match. Exact CTC token decisions: `21/21`. Numeric tolerances are `0.01` source pixels for coordinates, `0.001` for detector scores and `0.001` for recognition/token confidence; geometry is within tolerance for `21/21` and recognition for `21/21`. Maximum absolute deltas: coordinates `0.00384999999999991`, detector score `0.000157709999999978`, recognition confidence `1.34499999999704E-05`, token confidence `0.000379600000000035`.

## Numeric drift attribution

Across comparable matched records, exact numeric changes were coordinates `8/2216` scalar components, detector scores `248/277` regions, recognition confidences `245/277` regions, and CTC token confidences `12366/15937` tokens. Each cell below is `changed/comparable; largest absolute delta@location` (zero means compared values were bit-equal; n/a means not comparable). Location indexes are zero-based source-region, vertex, or CTC timestep. JSON records the two backend values for each maximum. This attributes output fields only; it does not prove the internal runtime cause of the small numeric differences.

| Model | Image | Coordinates (changed/comp; max@loc) | Detector score | Recognition confidence | CTC token confidence |
| --- | --- | --- | --- | --- | --- |
| v4-mobile | demo_1.jpg | 0/128; 0 | 13/16; 6.4E-07@s6 | 15/16; 4.86E-06@s1 | 1110/1124; 0.0003796@s1/t25 |
| v4-mobile | demo_2.jpg | 0/112; 0 | 11/14; 1.03E-06@s6 | 13/14; 3.5E-06@s4 | 484/487; 8.4E-06@s11/t60 |
| v4-mobile | demo_3.jpg | 0/80; 0 | 9/10; 3E-07@s2 | 10/10; 1.03E-06@s7 | 832/870; 8.26E-06@s8/t115 |
| v4-server | demo_1.jpg | 0/128; 0 | 14/16; 1.16E-06@s7 | 16/16; 1.3E-05@s2 | 1082/1098; 0.00019954@s2/t48 |
| v4-server | demo_2.jpg | 0/104; 0 | 9/13; 5E-07@s11 | 12/13; 3.22E-06@s10 | 489/495; 1.095E-05@s10/t66 |
| v4-server | demo_3.jpg | 8/80; 0.00385@s0/v1X | 10/10; 0.00015771@s0 | 10/10; 8.46E-06@s0 | 748/808; 7.565E-05@s0/t34 |
| v5-mobile | demo_1.jpg | 0/128; 0 | 14/16; 6.6E-07@s10 | 16/16; 1.75E-06@s10 | 983/1005; 4.205E-05@s4/t77 |
| v5-mobile | demo_2.jpg | 0/112; 0 | 13/14; 6E-07@s1 | 14/14; 3.23E-06@s6 | 419/435; 3.95E-05@s2/t36 |
| v5-mobile | demo_3.jpg | 0/80; 0 | 10/10; 7.4E-07@s1 | 9/10; 3.2E-06@s7 | 733/737; 4.07E-05@s2/t32 |
| v5-server | demo_1.jpg | 0/128; 0 | 14/16; 7E-07@s13 | 15/16; 3.3E-06@s13 | 913/929; 5.09E-05@s3/t55 |
| v5-server | demo_2.jpg | 0/112; 0 | 9/14; 3E-07@s10 | 12/14; 1.345E-05@s4 | 461/463; 3.338E-05@s12/t49 |
| v5-server | demo_3.jpg | 0/80; 0 | 7/10; 2E-07@s0 | 10/10; 5.55E-06@s0 | 771/775; 3.604E-05@s7/t88 |
| v6-medium | demo_1.jpg | 0/128; 0 | 16/16; 4.4E-07@s0 | 13/16; 1.61E-06@s6 | 362/1072; 3.127E-05@s4/t85 |
| v6-medium | demo_2.jpg | 0/104; 0 | 13/13; 6.5E-07@s8 | 11/13; 1.37E-06@s12 | 266/483; 2.02E-05@s3/t0 |
| v6-medium | demo_3.jpg | 0/80; 0 | 10/10; 1.16E-06@s1 | 10/10; 8E-07@s4 | 213/806; 0.0001554@s3/t52 |
| v6-small | demo_1.jpg | 0/128; 0 | 15/16; 4.6E-07@s6 | 11/16; 1.14E-06@s6 | 438/975; 2.68E-05@s6/t4 |
| v6-small | demo_2.jpg | 0/104; 0 | 13/13; 3.4E-07@s3 | 13/13; 1.33E-06@s3 | 262/458; 2.234E-05@s3/t1 |
| v6-small | demo_3.jpg | 0/80; 0 | 10/10; 1.1E-06@s5 | 10/10; 2.5E-07@s0 | 315/760; 2.023E-05@s4/t89 |
| v6-tiny | demo_1.jpg | 0/128; 0 | 14/16; 7.7E-07@s6 | 8/16; 2.64E-06@s3 | 638/969; 3.503E-05@s3/t41 |
| v6-tiny | demo_2.jpg | 0/112; 0 | 14/14; 7E-07@s3 | 11/14; 6.4E-07@s3 | 337/467; 6.46E-06@s11/t58 |
| v6-tiny | demo_3.jpg | 0/80; 0 | 10/10; 9.3E-07@s0 | 6/10; 9.6E-07@s2 | 510/721; 1.117E-05@s2/t39 |

| Model | demo_1 regions | demo_2 regions | demo_3 regions | Total per backend | Text | Geometry tolerance | Token decisions | Recognition tolerance | Canonical SHA |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| v4-mobile | 16 | 14 | 10 | 40 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| v4-server | 16 | 13 | 10 | 39 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| v5-mobile | 16 | 14 | 10 | 40 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| v5-server | 16 | 14 | 10 | 40 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| v6-medium | 16 | 13 | 10 | 39 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| v6-small | 16 | 13 | 10 | 39 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| v6-tiny | 16 | 14 | 10 | 40 | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |

## Device and runtime

Host OS: `Microsoft Windows 10.0.26200`; CPU: `AMD Ryzen 7 5800H with Radeon Graphics` (`16` logical processors); process architecture: `X64`; .NET: `10.0.12`. Both runs used CPU execution only. `ONNX Runtime CPU` native runtime: `1.23.2`; `OpenVINO CPU` runtime: `2026.2.1-21919-ede283a88e3-releases/2026/2`. No GPU execution, frequency tuning, or GPU performance was tested.

Each model/image pair was run once with zero warmups. Timing starts at `OcrPipeline.Run` after image input preparation and backend Session creation; reported elapsed values are diagnostic only and must not be used as a latency ranking or steady-state benchmark.

Source commits: ORT `a5ca36c`; OpenVINO `a5ca36c`. Source-tree dirty: ORT `true`; OpenVINO `true`. Test-source SHA-256, input and model artifact SHA-256 values, and per-pair component summaries are in the machine-readable JSON evidence.

## Parity boundary

The text fingerprint is SHA-256 over the ordered recognized strings joined with newline separators. Matching text and region counts do not prove polygon, confidence, orientation, or token equality. The historical Linux reports used backend-specific character-set IDs (`external.<model>` for ORT and `external.openvino.<model>` for OpenVINO), which participate in the canonical result SHA; those reports also lack matching character-set provenance fields and cannot be compared canonically. The new Windows reports record matching character-set ID/version/SHA metadata, so their canonical result hashes are comparable. The demo images have no manually verified transcription annotations, so this run does not calculate CER/WER, detection recall, or accuracy.

A canonical result SHA is a bit-exact fingerprint, not a tolerance-based parity verdict: tiny backend floating-point differences change the hash. Read it together with the explicit geometry/confidence tolerances and token-decision comparison above. In particular, a hash mismatch alongside all tolerance checks passing means numerically close component parity, not identical binary outputs.

The historical Linux RTX 2060 pair is a separate run: its three image SHA-256 values differ from this Windows set, and its per-region character-set identity/version/hash fields are absent. The comparator therefore marks those canonical hashes not comparable; do not interpret them as an ORT-versus-OpenVINO output difference or as a cross-device parity result.

## Reproduction

On a machine with the listed models and images, set `DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL=1` and/or `DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL=1`, configure `DEPLOYSHARP_PADDLEOCR_ROOT` and the backend-specific `*_IMAGES` semicolon-delimited list, then run the corresponding `PaddleOcrAllCorePipeline*IntegrationTests` opt-in test. Configure `DEPLOYSHARP_PADDLEOCR_ORT_REPORT` or `DEPLOYSHARP_PADDLEOCR_OPENVINO_REPORT` to emit raw evidence JSON. Compare a new report pair with `eng/models/paddle-ocr/scripts/Compare-PaddleOcrCorePipelineReports.ps1`.

Machine-readable pair report: [paddleocr-core-seven-models-ort-openvino-component-parity-windows-20261023.json](paddleocr-core-seven-models-ort-openvino-component-parity-windows-20261023.json). Source reports: [ORT](paddleocr-core-seven-models-ort-three-image-windows-20261023.json) and [OpenVINO](paddleocr-core-seven-models-openvino-three-image-windows-20261023.json).
