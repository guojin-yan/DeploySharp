---
name: deploysharp-model-onboarding
description: Add or update a DeploySharp model family, task profile, decoder, catalog entry, sample, and support evidence. Use when integrating model files or enabling a new model task; do not use for backend-only fixes or ordinary application code.
---

# DeploySharp Model Onboarding

Integrate a model as an explicit, reproducible contract. Do not infer support from a model name, an ONNX load, or a nearby model variant.

## Start From Repository Facts

1. Read the relevant task implementation under `src/DeploySharp.Visual` or the owning feature project.
2. Read `docs/articles/visual-custom-model.md`, `docs/articles/model-support.md`, and the exact rows in `docs/model-backend-verification-matrix.md`.
3. Reuse an existing profile, preprocessor, decoder, result type, and lifecycle pattern when the tensor contract matches. Add a new abstraction only when the task semantics differ.

## Required Contract

Capture the exact model ID, revision, format, input/output names, element types, fixed/dynamic dimensions, preprocessing, output layout, labels/tokenizer, coordinate space, thresholds, batch limits, and sidecars. Keep resize, color, normalization, padding, and layout configurable when the model permits it; preserve the official defaults.

For custom user models, expose public composition points for preprocessing, inference inputs, decoder, and result types. Fail on ambiguous input/output binding instead of selecting the first port.

## Assets And Catalog

- Keep large weights and generated engines out of Git.
- Describe downloadable artifacts through ModelPack and the catalog under `eng/model-catalog` or the owning model manifest.
- Record size and SHA-256 for every required graph, external-data file, dictionary, tokenizer, label file, or processor sidecar.
- TensorRT engines are device/runtime/profile specific and are not portable model assets.
- Downloading or publishing Release assets is an external mutation and requires the user's explicit request.

## Completion

Add focused contract tests, a realistic sample, and user documentation. Validate at least one exact backend with a real model and input before marking it supported. Use `unverified` when evidence is absent, `unsupported` for an attempted and confirmed blocker, and `supported` only for real execution plus result-contract evidence. Never expand one representative result to an entire family.
