# ModelPack.Json clean consumer / ModelPack.Json 纯包消费者

This project installs only `JYPPX.DeploySharp.ModelPack.Json` (Core arrives as its declared dependency), restores from the local package folder, writes a small ONNX-style manifest, verifies SHA256 and size, loads it, and converts the artifact to the Core contract. It has no project references and no backend/native runtime dependency. / 此项目只安装 `JYPPX.DeploySharp.ModelPack.Json`（Core 作为声明依赖传递安装），从本地包目录还原，写入一个小型 ONNX 风格清单，验证 SHA256 和大小，加载它并转换为 Core 契约。它没有项目引用，也没有后端/原生运行时依赖。
