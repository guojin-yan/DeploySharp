# SLANeXt table-cell geometry check (2026-10-27)

This is a targeted check of the existing official wired table example, not a new backend matrix or accuracy benchmark.

| Input / artifact | SHA-256 |
| --- | --- |
| `table_recognition.jpg` (551x132) | `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Official `SLANeXt_wired_infer/inference.pdiparams` | `ff0362a30f707fa7d27d8453c4396b992fca287377a42d01515bca6eeb3908f5` |
| Release `slanext-wired.onnx` | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` |

The installed PaddleX `TableRunnerPredictor` was run once on the official static weights and image with `device="cpu"`, `batch_size=1`. Its `TableLabelDecode` multiplies the normalized eight-point location output by the padded 512-pixel size divided by the longest-side resize ratio. DeploySharp's `Transform.ToSource` applies the same scale. PaddleX returned 13 structure polygons; the first was `[6,4,381,5,380,92,6,89]`, while DeploySharp's already-recorded ONNX result had clipped bounds approximately `(6.2,4.5,375.2,88.0)`. The subpixel difference is consistent with PaddleX's final integer truncation. Thus this is **not a confirmed coordinate-transform defect**.

The official PaddleX polygons themselves extend far below the 132-pixel source image: the final polygon has Y coordinates `262,278,524,522`; 12 of 13 polygons have at least one out-of-source vertex. DeploySharp previously clipped those values silently, producing several zero-height regions. The decoder now preserves the existing clipping contract but adds `metadata.geometry-outside-source=true` to affected regions and one `table-cell-geometry-outside-source:12/13` result warning. A focused synthetic contract test and the exact official ONNX/image integration test guard this behavior.

The RT-DETR cell detector's high-score header box on the same image is approximately `(2.2,0,544.5,30.7)`, whereas the first SLANeXt structure box spans Y `4..92`. Equal counts of 13 do not establish cell correspondence or table accuracy. The sample continues to emit HTML structure only; it must not merge these SLANeXt boxes with detected cells or crop them for OCR. A future text-binding feature needs a separately labeled geometry test or an explicit matching strategy based on validated detection boxes.

Reproduction from the repository root, with the official model and image at the paths above:

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' -c "from paddlex.inference.models import create_predictor; p=create_predictor('SLANeXt_wired',model_dir=r'E:\Model\PaddleDocument\source\slanext-wired\SLANeXt_wired_infer',device='cpu',batch_size=1); r=list(p.predict(r'E:\Model\PaddleDocument\validation\table_recognition.jpg')); print(r[0]['bbox'])"
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj --filter FullyQualifiedName~SlanetDecoderFlagsCellsOutsideTheSourceBeforeClipping
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj --filter FullyQualifiedName~TableStructureDecoderRunsOnRealOrtCpu
```

No other model or performance suite was rerun for this geometry check.
The new synthetic decoder test passed `1/1`. The external-model filter matched both wired and wireless table-structure tests, and both passed `2/2` on ONNX Runtime CPU. `git diff --check` passed for the scoped tracked files.
