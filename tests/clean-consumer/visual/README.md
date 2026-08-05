# Visual package-only clean consumer / Visual 仅包引用消费者

This project references only `JYPPX.DeploySharp.Visual` from `artifacts/packages`; Core is resolved transitively and there are no project references. A local fake Core backend runs one classification and one detection pipeline without an image library, model file, native runtime, or network access. / 本项目仅从 `artifacts/packages` 引用 `JYPPX.DeploySharp.Visual`；Core 通过传递依赖解析，不含项目引用。一个本地 Fake Core 后端在无图像库、模型文件、原生运行时和网络访问的情况下运行一次分类与一次检测 Pipeline。

Pack Core and Visual before restoring this project. / 还原本项目前请先打包 Core 与 Visual。
