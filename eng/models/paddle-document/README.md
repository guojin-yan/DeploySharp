# Paddle document model acquisition

This directory is the reproducible acquisition entry point for PP-StructureV3 document models. The checked-in manifest contains official Paddle inference archive URLs; the model bytes remain outside the repository.

```powershell
# Convert one small classifier first.
& .\scripts\Acquire-PaddleDocumentModels.ps1 `
  -ModelId pp-lcnet-x1-0-doc-ori `
  -ModelRoot E:\Model\PaddleDocument `
  -Python C:\Users\guoji\.conda\envs\PaddleOCR\python.exe

# After validating the toolchain, acquire all catalogued modules.
& .\scripts\Acquire-PaddleDocumentModels.ps1 `
  -All `
  -ModelRoot E:\Model\PaddleDocument `
  -Python C:\Users\guoji\.conda\envs\PaddleOCR\python.exe
```

The converter requires a Python environment with a PaddlePaddle build compatible with the installed `paddle2onnx`. `paddle2onnx` 2.x uses `python -m paddle2onnx.command`; the script handles that entry point and adds the Paddle DLL directories to the child process search path. Every source archive and converted ONNX file receives a size/SHA-256 record. A failed conversion is retained as `conversion-blocked` and must not be advertised as a runtime-supported model.

The official PP-Structure models are Paddle Inference artifacts. They are not automatically interchangeable with the existing PP-OCR ONNX release assets. A converted model is admitted to DeploySharp only after its exact input/output names, preprocessing, decoder, backend execution and result parity are recorded.

## Current local evidence

On 2026-09-18 the repository manifest was acquired into `E:\Model\PaddleDocument` with Python 3.11, PaddlePaddle `3.0.0.dev20250613`, Paddle2ONNX `2.0.2rc3`, ONNX `1.17`, and ONNX Runtime CPU. Twenty-eight standard inference archives converted successfully and passed the structural smoke tool. The generated report is outside Git at `E:\Model\PaddleDocument\onnx-smoke.json`.

To repeat the graph check with a real image (the script uses a stride-friendly 640x640 canvas for dynamic graphs), run:

```powershell
& E:\Model\PaddleDocument\paddle3\python.exe .\scripts\Verify-PaddleDocumentOnnx.py `
  --onnx-root E:\Model\PaddleDocument\onnx `
  --image E:\Data\ocr\demo_1.jpg `
  --output E:\Model\PaddleDocument\semantic-smoke.json
```

Use one or more `--model <onnx-file-stem>` arguments to avoid running the heavyweight FormulaNet/UniMERNet graphs while iterating.

The report proves that each graph can be loaded and executed with selected synthetic inputs. It does not prove semantic parity or OpenVINO/TensorRT/OpenCV DNN support. `PP-Chart2Table` is intentionally recorded as `conversion-blocked`: its official archive contains generative model weights and tokenizer files rather than a standard `inference.json`/`inference.pdiparams` graph.

For a converted model, inspect the ONNX graph before constructing a profile. Layout, cell and seal exports use Paddle post-NMS rows `[class, score, x1, y1, x2, y2]`; use `PaddleDocumentProfiles.CreatePaddleNmsRegions`. SLANeXt uses two outputs and `CreateTableStructure`. UVDoc uses `CreateUnwarping`. FormulaNet/UniMERNet return integer token IDs and require an explicit `PaddleDocumentFormulaSchema`.
