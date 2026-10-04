# PP-Structure concurrent page regression (2026-10-05)

This is an incremental regression record for the bounded page-concurrency API added to `PaddleDocumentPipeline`. It is intentionally separate from the five-project 2026-10-04 release gate because only the Visual project was rerun at this revision.

## Environment

- Repository revision: `b1c8803` (`DeploySharpV2.0`)
- Runtime: `net10.0`
- Configuration: Debug (the focused local contract run)
- External model gates: disabled
- Working directory: `E:\GitSpace\DeploySharp-V2.0\DeploySharp`

## Change under test

`PaddleDocumentPipeline.RunManyConcurrentAsync` now executes independent pages with a caller-selected hard concurrency limit while preserving input order and page provenance. The existing `RunManyAsync` remains sequential. The new contract requires the caller to provide thread-safe stage adapters and native Sessions; if a stage is not thread-safe, pages must be sharded across separately created Pipeline instances outside this method. It does not turn a batch-one model into a tensor Batch.

## Results

| Scope | Passed | Skipped | Failed | Total |
|---|---:|---:|---:|---:|
| `PaddleDocumentPipelineTests` focused filter | 9 | 0 | 0 | 9 |
| `DeploySharp.Visual.Tests` non-external filter | 494 | 5 | 0 | 499 |

The focused tests cover:

- concurrent pages retain caller order and each page's `PageIndex`;
- the observed stage concurrency never exceeds `maxDegreeOfParallelism`;
- cancellation reaches both active and queued pages;
- non-positive concurrency is rejected;
- existing dependency ordering, export, ownership and provenance contracts remain green.

The five skips are existing opt-in Whisper/Chart2Table checkpoint gates. No external model or backend support claim is derived from this run.

## Reproduction

```powershell
dotnet test .\tests\DeploySharp.Visual.Tests\DeploySharp.Visual.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentPipelineTests'

dotnet test .\tests\DeploySharp.Visual.Tests\DeploySharp.Visual.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'TestCategory!=ExternalModels'
```

The build emits the repository's known dependency target-framework support warnings for older target frameworks; the tested `net10.0` assets completed with zero failures. Real multi-page throughput, native Session-pool sizing, and cross-backend performance remain separate device measurements.
