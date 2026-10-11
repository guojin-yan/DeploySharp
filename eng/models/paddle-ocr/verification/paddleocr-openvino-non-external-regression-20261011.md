# PaddleOCR / PP-Structure OpenVINO non-external regression (2026-10-11)

This snapshot runs the OpenVINO backend contract suite without external-model gates. It is a runtime/API regression record, not a model-quality, backend-parity, throughput or soak claim.

- Host: Windows `10.0.26200`, `win-x64`
- .NET SDK: `10.0.401` (`10.0.12` runtime)
- Configuration: `Release`, target framework `net10.0`, `--no-restore`
- OpenVINO API package: `JYPPX.OpenVINO.CSharp.API 3.3.1`
- Windows runtime package: `OpenVINO.runtime.win 2026.2.1`
- Command:

  ```powershell
  dotnet test tests/DeploySharp.Backend.OpenVINO.Tests/DeploySharp.Backend.OpenVINO.Tests.csproj `
    -c Release -f net10.0 --no-restore `
    --filter 'TestCategory!=ExternalModels' `
    --logger 'console;verbosity=minimal'
  ```

Result: **25 passed / 0 skipped / 0 failed** (25 total).

The run includes the OpenVINO backend contracts and non-external regression cases. It does not promote every PaddleOCR or PP-Structure model to OpenVINO support: external assets, model-specific quality gates, TensorRT/OpenCV matrices, controlled P50/P95 performance and long-running stability remain separate evidence requirements. The legacy-target-framework compatibility fix covered by this snapshot replaces `OperatingSystem.IsWindows()` with `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` in the OpenVINO provider/factory so the declared older target frameworks can compile.

Machine-readable result: [`paddleocr-openvino-non-external-regression-20261011.json`](paddleocr-openvino-non-external-regression-20261011.json).
