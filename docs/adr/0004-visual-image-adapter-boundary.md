# ADR 0004: Visual image adapters and the default OpenCV path

- Status: Accepted
- Date: 2026-08-04

## Decision

`JYPPX.DeploySharp.Visual` remains image-library agnostic. It defines visual workflows, model registration, preprocessing contracts, image-frame abstractions, coordinate transforms, and decoders, but it does not reference OpenCV or expose OpenCV types as a cross-library contract.

`JYPPX.DeploySharp.Visual.OpenCV` is the official default image-input and image-processing adapter. It depends on `Visual`, `Core`, and `JYPPX.OpenCV.CSharp.API`, and is the package used by official visual examples.

Additional image libraries are added as separate adapters such as `JYPPX.DeploySharp.Visual.ImageSharp` or `JYPPX.DeploySharp.Visual.SkiaSharp`. Image adapters and inference backends remain orthogonal; packages such as `Visual.OpenCV.OnnxRuntime` are prohibited.

## User-facing installation

An OpenCV + ONNX Runtime application directly installs only:

```text
JYPPX.DeploySharp.Visual.OpenCV
JYPPX.DeploySharp.Backend.OnnxRuntime
```

`Visual` and `Core` arrive transitively. Native OpenCV and ONNX Runtime packages remain separately selected according to platform and device requirements.

## Consequences

- Core stays free of OpenCV, native handles, and image codecs.
- The default path uses the project-owned OpenCV-CSharp-API without forcing it on LLM-only or tensor-only applications.
- New image libraries add one adapter package instead of duplicating model workflows.
- The package graph grows additively rather than as an image-library-by-backend product matrix.
