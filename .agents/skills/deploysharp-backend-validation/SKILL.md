---
name: deploysharp-backend-validation
description: Validate or diagnose an exact DeploySharp model and inference backend combination, including native runtime, dynamic shapes, parity, and support-matrix updates. Use for ONNX Runtime, OpenVINO, OpenCV DNN, TensorRT, or LlamaSharp backend work.
---

# DeploySharp Backend Validation

Treat support as an exact tuple: model artifact, task, backend/provider, runtime version, device, input contract, and decoder.

## Validation Flow

1. Read `docs/articles/platform-support.md` and the target row in `docs/model-backend-verification-matrix.md`.
2. Record the model and input SHA-256, runtime/native versions, OS, architecture, device, and source revision before testing.
3. Use ONNX Runtime CPU as the numerical baseline when applicable, then bind every target-backend input by name, type, rank, and concrete shape.
4. Separate model import, Session creation, input binding, execution, output copy, and decoder failures.
5. Compare raw output shape and finite values before comparing decoded results. Use declared tolerances for scores and coordinates; compare discrete labels, tokens, text, and region order exactly when required.

Dynamic input support does not prove dynamic Batch. For flat NMS output, verify row separation through the count tensor. A Session pool must create independent native Sessions or execution contexts; never claim concurrency from queued calls on one stateful object.

## Native Runtime Rules

- Do not silently fall back from CUDA/TensorRT/OpenVINO GPU to CPU.
- Keep managed and native package versions, RID, architecture, CUDA/cuDNN/TensorRT, and driver details in the report.
- Build TensorRT engines on the target device with the tested profile.
- When a third-party importer rejects a precise operator/shape, preserve the original diagnostic and mark that combination unsupported. Do not change decoder thresholds to hide import failures.

## Evidence And Status

Contract/unit tests prove API behavior, not real model support. A smoke test proves execution, not accuracy or performance. Update the matrix only for the exact artifact tested: supported for real execution and result contract, unverified when not tested, unsupported when attempted and blocked, and not-applicable only when the combination has no valid meaning.

Keep raw logs, TRX, generated engines, and repeated JSON outside public docs. Public documentation should contain the current conclusion, environment, reproducible command, and limitation.
