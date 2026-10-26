# PaddleOCR-VL 1.6 TensorRT logits reduction probe

Date: 2026-10-26. Windows 10.0.26200, .NET 10.0.12, RTX 3060 Laptop 6 GiB, TensorRT 10.11.0.33, CUDA 12.9, cuDNN 9.22. The matching `JYPPX.TensorRT.CSharp.API` 4.0.0 native bridge was used. GPU clocks were not locked and other device load was not controlled.

## Change and scope

`PaddleOcrVlTensorRtDeviceSession` can optionally run a CUDA argmax on the existing Decoder/Head stream. For each decode token it then reads back one token ID and one non-finite flag (8 bytes), instead of the full 103,424-class Float32 logits (413,696 bytes). The first Prefill/Head token still uses the host path. Token Embedding and Vision/Prefill remain host-owned; this is not fully device-resident generation. The existing CPU selection remains the default because the short paired test below did not show a speed benefit. The constructor's `useDeviceLogitsReduction: true` enables the optional path; `UsesDeviceLogitsReduction` confirms whether NVRTC initialization succeeded. Compilation/native failures during initialization fall back to CPU; a CUDA execution failure during generation remains an error.

The existing CTC trace kernel provides finite-value rejection and lowest-ID tie breaking. A real CUDA unit test covered all 103,424 classes, equal maxima, the last class and NaN rejection (1/1 passed). It used the same native preflight as the TensorRT provider.

## Fixed Table comparison

Both paths used the official `table-official-example` patch SHA `c66c539dd3c39ceab0c5eea3613091c099853d3396a689227e13a6e5e94dbcbb`, identical five Engine SHA values and the same reference SHA `aaa5ace39098c307e27107562045994aabdd952fcf8dc1cb388be87e6f58eb79`. Each path ran one correctness/warm-up generation and five measured generations. All 12 runs reached EOS and matched all 71 Token IDs and decoded text. Wall clock surrounds generation only; Engine loading and caller image preparation are excluded. P95 uses nearest rank.

| Path | Device reduction used | Five measured wall times (ms) | Wall P50 / P95 (ms) | Session P50 / P95 (ms) |
| --- | --- | --- | ---: | ---: |
| CPU logits | false | 11806.606, 12361.135, 11714.152, 11627.494, 11498.764 | 11714.152 / 12361.135 | 11689.776 / 12355.919 |
| GPU argmax | true | 13369.551, 12769.416, 11718.385, 12038.182, 13584.968 | 12769.416 / 13584.968 | 12764.493 / 13580.923 |

The GPU path's observed Wall P50 is about 9% higher in this short sequential run. This is not a controlled regression estimate: clocks/load were not fixed and only five samples were measured. It is sufficient reason not to enable the optimization by default. A future decision needs a reversed-order, locked-clock comparison with more samples and stage-level timings, followed by six-task Token parity.

Raw reports remain outside Git so the large local Engine/fixture files need not be published:

| Path | Raw report SHA-256 |
| --- | --- |
| `E:\DeploySharpEvidence\paddleocr-vl-tensorrt-logits-ab-host-20261026\table-official-example.json` | `f40412e8eb2a77af60ff7e0946f60c2a8ca0f09b6a07621681534560b5db0b0f` |
| `E:\DeploySharpEvidence\paddleocr-vl-tensorrt-logits-ab-gpu-20261026\table-official-example.json` | `7e2b4b5898bc37f9e60165efae8fa902a96d0fa1f2057f5eb6c25a857f78a540` |

The reports record source HEAD `d6d349c` plus the uncommitted implementation through test/source and loaded assembly SHA values. After commit, the source HEAD must be read together with those hashes; the old commit alone does not identify this test build. Reproduce with `Invoke-PaddleOcrVlTensorRtGeneration.ps1 -DeviceKv -DisableDeviceLogits -Latency -Warmups 1 -Measurements 5` and a fresh output directory for the CPU path, then replace `-DisableDeviceLogits` with `-RequireDeviceLogits` for the CUDA path. Supply the pinned Bundle, official fixture/reference, matching Engine profiles and native roots described in the [five-graph report](paddleocr-vl-tensorrt-five-graph-20261026.md).

## Six fixed tasks

The optional GPU reduction also completed all six pinned official fixtures. Every report records `deviceLogitsReduction=true`, EOS, exact Token IDs and exact text against the same reference. The Table run above is included in this total: **6/6 tasks, 765/765 generated tokens**. Each non-Table task is one correctness run in its own process, not a latency benchmark.

| Task | Generated tokens | Profile | Generation (ms) | Raw report SHA-256 |
| --- | ---: | --- | ---: | --- |
| OCR | 162 | baseline Vision, 1,024 Prefill | 39,699.766 | `02f071244f5356e5a5badef05155f1d655a91264f9744aa0b22bfd27917f5e3b` |
| Table | 71 | compact Vision, 1,024 Prefill | 11,948.185 | `7e2b4b5898bc37f9e60165efae8fa902a96d0fa1f2057f5eb6c25a857f78a540` |
| Formula | 80 | compact Vision, 1,024 Prefill | 31,973.953 | `31c40ed770c2558ece08a860e1f9a58b7467fd8ef34cf39da2ce9fa157f213f0` |
| Chart | 137 | baseline Vision, 1,024 Prefill | 38,257.213 | `e5ba91e73d72b70e461c58e27138c4e25bc74ebb7c7fe1442f9c702045f4dd1e` |
| Seal | 29 | baseline Vision, 1,024 Prefill | 40,436.999 | `7ec7ad9c3acfe7cb7b19b0dd7ca7fb6d5d52d0e33458b68b1dbc697747aec3cc` |
| Spotting | 286 | fixed 72x72 FP16 Vision, 2,048 Prefill | 94,020.796 | `ad4bcb76f28d100a03a364665490415b9d80b101d045b25b60153170c5a59a5a` |

The three baseline reports are under `E:\DeploySharpEvidence\paddleocr-vl-tensorrt-gpu-logits-baseline-20261026`; Formula and Spotting have corresponding `paddleocr-vl-tensorrt-gpu-logits-formula-20261026` and `paddleocr-vl-tensorrt-gpu-logits-spotting-20261026` directories. Their absolute latencies varied markedly from earlier device-KV runs and cannot be compared across runs without load/clock control. This six-task check closes fixed-fixture Token parity for the optional argmax only, not its performance or labelled quality.

This result does not expand the four-graph Release, arbitrary Engine profiles, TensorRT page composition or labelled task quality. PaddleOCR-VL assets are still absent from the remote `models-paddleocr` Release.
