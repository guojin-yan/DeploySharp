# Next model publication wave / 下一批模型发布波次

The prioritized candidates are tracked in `eng/models/next-publication-candidates.json`. This file is an admission queue, not a catalog: its historical external rows remain separate from the six published PP-OCRv5 release entries in the embedded official catalog.

| Priority | Area | Candidate | Current evidence | Why it is not public yet |
|---:|---|---|---|---|
| 1 | OCR | `paddleocr/ppocrv5/mobile-det/external` | ContractVerified | Official DB contour/pyclipper parity, export provenance, redistribution approval, and image golden are incomplete. |
| 2 | OCR | `paddleocr/ppocrv5/mobile-rec/external` | ContractVerified | Checkpoint/dictionary redistribution approval and official recognition goldens are incomplete. |
| 3 | Audio | `audio/wav2vec2-base-960h/external` | ExternalRuntimeCompared | The exact checkpoint and sidecars are local evidence only; the manifest explicitly disallows redistribution. |
| 4 | Vision-language | `vision-language/siglip-base-patch16-224/external` | ExternalOfficialGoldenVerified | The converted dual encoder and large sidecars have no approved immutable public release. |

These blockers are deliberate. `ModelFactoryClient` rejects `External` entries, and changing a manifest flag without the missing evidence would turn a provenance record into an unsupported public promise. The next release wave can start as soon as a candidate has an approved source/notice bundle, exact release-bound hashes, official golden comparisons, and a successful immutable Release/catalog audit.

The current local OCR baseline is reproducible without copying any model into the repository:

```powershell
pwsh -File eng\models\ocr-anomaly-rmbg\Test-PaddleOcrExternalEvidence.ps1
```

The command validates the pinned local files, runs the ORT/OpenVINO three-model parity test on `bus.jpg`, and emits `DEPLOYSHARP_PADDLE_OCR_EXTERNAL_EVIDENCE_OK` with `assetSource=public-release` when pointed at the downloaded Release assets. The public asset and catalog audit can be repeated with `eng\\models\\ocr-anomaly-rmbg\\Test-PaddleOcrPublishedRelease.ps1`.

The current public catalog remains the source of truth for downloadable models. Use the CLI to inspect it:

```powershell
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- list --preview
```
