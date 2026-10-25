# PaddleOCR-VL 1.6 TensorRT five-graph generation

Date: 2026-10-26. Host: Windows 10.0.26200, .NET 10.0.12, GeForce RTX 3060 Laptop GPU (6,144 MiB, compute capability 8.6), driver 576.02, TensorRT 10.11.0.33, CUDA 12.9 and cuDNN 9.22. The native bridge is the matching `JYPPX.TensorRT.CSharp.API` 4.0.0 TRT10.11/CUDA12.9/cuDNN9.22 package. The source HEAD recorded by the runs is `c54fec775735a53a153a61fbe121e433dc7d7348` **plus this uncommitted implementation**; the run reports also record test-source, Visual assembly, TensorRT assembly and test-assembly SHA-256 values. They must not be attributed to the old commit alone.

## Result and scope

The production `PaddleOcrVlOnnxSession` ran five distinct TensorRT sessions: `vision`, `embedding`, a derived two-input `prefill`, the original positive-past `decoder`, and `head`. The prefill graph replaces the original decoder's 36 zero-length past-KV runtime inputs with empty ONNX initializers. ONNX Runtime CPU gave exact elementwise parity between derived prefill and the original empty-KV decoder; the production TensorRT prefill session compared its hidden state and all 36 present-KV tensors with ORT CPU, with maximum absolute difference `0.000246048` and zero values outside the declared tolerance. This is followed by the **real complete generation** below, not just component checks.

The fixed official 1.6 six-task images and reference JSON are the same as [the upstream reference](paddleocr-vl-six-task-reference-linux-20261023.json). The fixture manifest SHA-256 is `ff6ffe322ac3a1373ce830ffc953e18173115b654f265591c0dd214c701db481`; reference SHA-256 is `aaa5ace39098c307e27107562045994aabdd952fcf8dc1cb388be87e6f58eb79`. Each task ran in a new process, loaded all five Engines, generated to EOS, and matched every reference token ID and the decoded text. In total, **6/6 tasks and 765/765 generated tokens passed**.

| Task | Prompt / generated tokens | Vision profile | Prefill maximum | Generation, one run |
| --- | ---: | --- | ---: | ---: |
| OCR | 337 / 162 | baseline FP32 | 1,024 | 46,672.5 ms |
| Table | 163 / 71 | compact FP32 | 1,024 | 19,017.3 ms |
| Formula | 173 / 80 | compact FP32 | 1,024 | 21,719.9 ms |
| Chart | 453 / 137 | baseline FP32 | 1,024 | 44,136.5 ms |
| Spotting | 1,309 / 286 | fixed 72x72 FP16 | 2,048 | 82,320.8 ms |
| Seal | 543 / 29 | baseline FP32 | 1,024 | 15,331.6 ms |

These generation times exclude Engine construction, Session loading and caller image preparation. They have no warm-up, repeated samples, P50/P95, locked clocks or competing-load control. They are correctness-run observations, **not a formal TensorRT performance comparison**. Spotting's FP16 vision path passed exact final token/text parity, but intermediate floating-point equality, arbitrary Spotting pages, multi-page quality and other GPUs are not established. One point-in-time GPU sample during an earlier Spotting run read 5,897/6,144 MiB; this is not a measured peak or stability guarantee. The generator still moves KV and logits through CPU-owned tensors on each step and does not implement a fully device-resident stream.

## Engine and report identity

The original four ONNX graph hashes remain those in the [local Bundle record](paddleocr-vl-release-bundle-20261025.md). The derived `prefill.onnx` SHA-256 is `9247b437a1551652572b3e25162d4d86fc969edf12ae44577aa6fc75b9fc5bdc`. The five-graph implementation is **not** the currently staged four-graph Release Bundle, and the derived prefill graph and device-specific Engines are not remotely downloadable assets.

| Role / profile | Engine SHA-256 |
| --- | --- |
| vision baseline FP32, grid 24..64 | `cd94f62e4c3582920eec37fcd916c597f77e5c0eda105428dbada5b58bcbdfd7` |
| vision compact FP32, grid 10..64 | `d0d8c1ddf9eb5dd451444d3b65084e42ef20a38bb807d9802f441b5061e2214f` |
| vision Spotting fixed 72x72 FP16 | `87d1a1d01787d0514fccfb7414872edfc648fde85a67bb7e6637b2347ea7a416` |
| embedding | `cf9f4cd171addd4735b207293e08656d7b22162204322cbc21ead6198245e2f9` |
| decoder, positive-length past KV | `928fbbfa54686a8f3a0b7f830a08c009dedd64041f20e83c0e8f23862b50e9db` |
| head | `4478473aae4fea9a9a3f72f06da30979fa0f9dab4ca49de08794b048a02e103d` |
| prefill, maximum 1,024 | `7d2630c9802a7fbc8c11f8cbaa8cb8641e03e7047419d1051a20ba320d802034` |
| prefill, maximum 2,048 | `7aab64293b06c2158716cebc6642c35092fecdf673f919bba127185ce1243819` |

Raw reports and summary SHA-256 values:

| Local directory under `E:\DeploySharpEvidence` | Summary SHA-256 | Tasks |
| --- | --- | --- |
| `paddleocr-vl-five-graph-final-baseline-20261026` | `3008fa93b2accbddc726264f5ba1de085eb471b803969df845a9705b601fbfa2` | OCR, Chart, Seal |
| `paddleocr-vl-five-graph-final-compact-20261026` | `fd3102888ccfeb2798215e26b910ba1f336b7be89e61c35758412cc0493dab62` | Table, Formula |
| `paddleocr-vl-five-graph-final-spotting-20261026` | `7de6b803b3bde2285531889c242b52b0a9c62aa59b4bb5eee41a91be546d18e6` | Spotting |

## Reproduce and remaining gates

1. Verify the pinned four-graph Bundle, tokenizer files, reference and prepared-RGB fixture manifest. Derive `prefill.onnx` with `eng/models/paddle-ocr/scripts/Derive-PaddleOcrVlPrefill.py`; it checks the original decoder SHA and compares all 37 outputs with ORT CPU. Retain its derivation report.
2. Use `Invoke-PaddleOcrVlBackendProbe.ps1` to build the four original roles and `prefill`. Select `-ProfilePreset compact-official-tasks` for Table/Formula vision, `-ProfilePreset spotting-fixed -Precision Float16 -WorkspaceGiB 4` for Spotting vision, and `-ProfilePreset all-official-tasks` for the 2,048-token prefill. A wide FP32 vision profile up to 72x72 failed on this 6 GB GPU because TensorRT could not find an implementation within the available tactic memory; it must not be presented as a working general profile.
3. Run `Invoke-PaddleOcrVlTensorRtGeneration.ps1` with the Bundle/model/fixture/reference roots, Engine root, selected `-VisionEngine` and `-PrefillEngine`, native runtime paths and a fresh output root. The runner builds the current test assembly and isolates each selected task in a new process. Its six exact case IDs are in the script's `-CaseId` validation set. Check every JSON status is `eos-token-text-exact`, not merely that the Builder returned a plan.

The ONNX four-graph API remains available for ORT and OpenVINO. The new optional fifth Session avoids zero-sized runtime KV on TensorRT while preserving the same decoder and CPU fallback. This evidence admits the fixed six real tasks on the stated Windows TensorRT host, not arbitrary image dimensions or profiles. OpenCV DNN 5.0 still fails to import the original dynamic `vision` and `decoder` graphs. Labelled task quality, page-level TensorRT composition, repeated P50/P95, device-resident KV/logits, long-run memory and another GPU/runtime combination remain open. A [verified local five-graph ONNX Bundle](paddleocr-vl-five-graph-bundle-20261026.md) now exists; no VL assets have been uploaded to the remote Release, and TensorRT Engines remain device-specific.
