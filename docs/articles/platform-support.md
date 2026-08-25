# Platform and backend support / 平台与后端支持

This page is the detailed support statement for <code>2.0.0-alpha.1</code>. A check means the path has local Windows evidence; it is not a promise that every model works on every backend. / 本页是 <code>2.0.0-alpha.1</code> 的详细支持说明。通过表示已有 Windows 本机证据，不表示每个模型都能在每个后端运行。

## Operating-system scope / 操作系统范围

| Platform / 平台 | Alpha status / Alpha 状态 | Notes / 说明 |
| --- | --- | --- |
| Windows 10 x64 | Supported for this Alpha | Managed build, tests, samples, CPU backends, and local TensorRT GPU evidence |
| Windows 11 x64 | Supported for this Alpha | Same source and package path as Windows 10 x64 |
| Windows ARM64 | Deferred | No current release validation |
| Linux x64/ARM64 | Deferred | Planned follow-up; not a release blocker |
| macOS x64/ARM64 | Deferred | Planned follow-up; not a release blocker |
| Android/iOS | Deferred | No mobile runtime statement in this Alpha |
| NPU providers | Deferred | Provider-specific validation is not complete |

## Target-framework coverage / 目标框架

| Package group / 包组 | Target frameworks / 目标框架 | Current evidence / 当前证据 |
| --- | --- | --- |
| Core, Visual | <code>net46</code>-<code>net481</code>, <code>netstandard2.0</code>, <code>netcoreapp3.1</code>, <code>net5.0</code>-<code>net10.0</code> | Windows build/test matrix |
| ModelPack.Json, ModelFactory | <code>netstandard2.0</code>, <code>net8.0</code>, <code>net9.0</code>, <code>net10.0</code> | Windows build, package-only consumers, catalog checks |
| LLM, Multimodal | <code>netstandard2.0</code>, <code>netcoreapp3.1</code>, <code>net5.0</code>-<code>net10.0</code> | Managed contracts and samples |
| Backend.OnnxRuntime, Backend.LlamaSharp | Package-specific <code>netstandard2.0</code>/<code>net8.0</code> subsets | Windows managed and runtime checks |
| Backend.OpenVINO, Backend.OpenCV, Visual.OpenCV | <code>net46</code>-<code>net481</code>, <code>netcoreapp3.1</code>, <code>net5.0</code>-<code>net10.0</code> | Windows x64 CPU paths |
| Backend.TensorRT | <code>net8.0</code> | Windows TensorRT 11/CUDA 12.9 local GPU |

Target-framework compatibility is a build boundary, not a security-support promise for end-of-life runtimes. / 目标框架兼容只表示可构建范围，不表示已经结束生命周期的运行时仍获得安全支持。

## Backend status / 后端状态

| Backend | Device/runtime | Alpha result |
| --- | --- | --- |
| ONNX Runtime | Windows x64 CPU, <code>Microsoft.ML.OnnxRuntime 1.28.0</code> | Named-tensor model execution verified |
| OpenVINO | Windows x64 CPU, application-owned OpenVINO runtime | Named-tensor model execution verified |
| OpenCV DNN | Windows x64 CPU | 25/38 tested ONNX artifacts pass; unsupported operators and dynamic contracts are recorded |
| TensorRT | TensorRT 11, CUDA 12.9, RTX 3060 Laptop GPU | 37/38 tested ONNX artifacts pass; RMBG 2.0 dynamic-int8 is unsupported |
| LLamaSharp | Windows CPU GGUF, application-selected native backend | Managed contract and Qwen GGUF path available |

The [model/backend verification matrix](../model-backend-verification-matrix.md) is the source of truth for each model/backend cell.
