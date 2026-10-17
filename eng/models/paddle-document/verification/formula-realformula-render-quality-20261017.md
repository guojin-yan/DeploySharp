# Formula realFormula MathJax render diagnostics (2026-10-13)

This report renders the 121 MathNet references and six checked-in ONNX Runtime CPU prediction sets with the locked MathJax SVG renderer, then compares paired grayscale rasters. It is a renderability and visual-structure diagnostic, not a mathematical-semantic score.

| Model | Samples | Reference rendered | Prediction rendered | Comparable pairs | MathML structural match | Exact text | Foreground IoU P05 / P50 | Pixel similarity P05 / P50 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `paddle-formula/pp-formulanet-plus-s` | 121 | 118/121 | 110/121 | 107/121 | 16/107 | 13/121 | 0.0263 / 0.2253 | 0.8432 / 0.9058 |
| `paddle-formula/pp-formulanet-plus-m` | 121 | 118/121 | 121/121 | 118/121 | 14/118 | 11/121 | 0.0231 / 0.2330 | 0.8403 / 0.9067 |
| `paddle-formula/pp-formulanet-plus-l` | 121 | 118/121 | 120/121 | 117/121 | 19/117 | 16/121 | 0.0324 / 0.2431 | 0.8415 / 0.9058 |
| `paddle-formula/pp-formulanet-s` | 121 | 118/121 | 111/121 | 108/121 | 16/108 | 12/121 | 0.0304 / 0.2285 | 0.8381 / 0.9076 |
| `paddle-formula/pp-formulanet-l` | 121 | 118/121 | 117/121 | 115/121 | 21/115 | 17/121 | 0.0257 / 0.2434 | 0.8397 / 0.9076 |
| `paddle-formula/unimernet` | 121 | 118/121 | 120/121 | 117/121 | 21/117 | 18/121 | 0.0360 / 0.2310 | 0.8347 / 0.9087 |

## Protocol

- Renderer: MathJax 4.1.3 `tex-svg`, local SVG font cache; Node v24.19.0.
- Rasterizer: sharp 0.35.5; each formula is fitted without distortion into 2048x128, centered on a shared white canvas, and compared as grayscale pixels.
- Dataset: MathNet realFormula, Zenodo 11296815 v1, 121 references; manifest SHA-256 `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`.
- The machine report retains per-image hashes, MathML structural hashes, render outcomes, foreground IoU and pixel similarity without copying reference or prediction text.

## Boundary

- A successful MathJax render only proves compatibility with this locked renderer and configuration. It does not prove compatibility with a full TeX distribution.
- Pixel similarity, foreground IoU and normalized MathML structure are diagnostics; they cannot establish mathematical equivalence or semantic correctness.
- The predictions come from the checked-in ONNX Runtime CPU reports. OpenVINO, OpenCV DNN and TensorRT quality are not represented.
- No release-quality threshold is inferred from these exploratory distributions.
