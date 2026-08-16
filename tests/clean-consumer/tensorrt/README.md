# TensorRT package-only consumer

This consumer references only `JYPPX.DeploySharp.Backend.TensorRT`. It exercises managed engine and CUDA cache miss/hit paths plus explicit invalidation while keeping builder/compiler/native execution behind injected managed delegates. It does not compile, load, build, launch or infer through a native runtime and prints `gpu=not-run`.

该消费者只引用 `JYPPX.DeploySharp.Backend.TensorRT`，同时验证 engine 与 CUDA 本地缓存的 managed miss/hit 和显式失效路径；builder/compiler/native execution 均不执行。不运行 native/GPU，输出 `gpu=not-run`。
