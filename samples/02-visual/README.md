# Visual workflow / Visual 工作流

VisualProfileInspection composes profile creation, exact named input/output contracts, task registration, decoder ownership, and registry freezing. It is a complete profile lifecycle case rather than a single constructor call.

```powershell
dotnet run --project samples/02-visual/VisualProfileInspection.csproj -c Release
```

The real image workflows are under `samples/06-models/release-inference`, with managed and runtime contracts covered by the corresponding projects under `tests/`. The [ROI workflow](roi-workflow/README.md) combines normalized geometry, JSON snapshot replacement, Detection filtering, video events, and bounded lazy-frame execution in one runnable case.

The [official SAM video adapter](roi-official-sam/README.md) / [官方 SAM 视频适配案例](roi-official-sam/README_cn.md) connects the ROI planner to Meta's Python predictor, decodes a bounded real video, exports source-space masks, and checks correction/reset consistency. It is not a native ONNX/TensorRT SAM backend.

The [PaddleOCR-VL ONNX sample](paddleocr-vl-onnx/README.md) downloads and verifies the fixed PaddleOCR-VL-1.6 five-graph Release Bundle, prepares a user image with OpenCV and runs ORT CPU generation. It also retains the original low-level mode for official float32 patch fixtures and four-graph export directories.

The [PaddleOCR-VL document sample](paddleocr-vl-document/README.md) accepts actual images and caller-supplied page regions, decodes each page once, runs the real crops through the same VL generator, preserves page/region provenance, and exports JSON/Markdown with cancellation and bounded page concurrency. Its `Auto` mode selects OCR/table/formula/chart/seal tasks by the supplied layout category, supports custom mappings, and preserves skipped regions and original indices.

The [PP-Structure table sample](paddle-structure-table/README.md) runs table classification, cell detection, SLANeXt structure, and PP-OCRv5 cell recognition on a user image with ONNX Runtime CPU or OpenVINO CPU. It selects wired or wireless table assets after classification, downloads the recognizer only when cell alignment succeeds, and exports page JSON, alignment/OCR diagnostics, and HTML. `--structure-only` retains the three-model path.
