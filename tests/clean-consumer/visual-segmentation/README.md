# Visual semantic segmentation clean consumer

This package-only consumer installs `JYPPX.DeploySharp.Visual`, `JYPPX.DeploySharp.Backend.OnnxRuntime`, and the application-selected `Microsoft.ML.OnnxRuntime` CPU package. It runs the reproducible semantic segmentation fixture and verifies the dense mask, canonical SHA256, and DeploySharp row-major RLE round-trip.

The fixture is an adapter contract graph, not an official algorithm model or ModelFactory catalog asset.
