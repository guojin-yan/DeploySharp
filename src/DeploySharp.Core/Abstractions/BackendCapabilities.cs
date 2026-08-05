using System;

namespace JYPPX.DeploySharp
{
    /// <summary>
    /// Describes independently selectable capabilities exposed by a backend. / 描述后端公开的可独立选择能力。
    /// </summary>
    [Flags]
    public enum BackendCapabilities
    {
        /// <summary>No capability is declared. / 未声明任何能力。</summary>
        None = 0,

        /// <summary>General named-tensor inference. / 通用命名张量推理。</summary>
        TensorInference = 1 << 0,

        /// <summary>Streaming or non-streaming text generation. / 流式或非流式文本生成。</summary>
        TextGeneration = 1 << 1,

        /// <summary>Embedding generation. / 嵌入向量生成。</summary>
        Embeddings = 1 << 2,

        /// <summary>Image and text vision-language inference. / 图像与文本的视觉语言推理。</summary>
        VisionLanguage = 1 << 3,

        /// <summary>Backend-side asynchronous execution. / 后端侧异步执行。</summary>
        AsynchronousExecution = 1 << 4,

        /// <summary>Dynamic input shapes. / 动态输入形状。</summary>
        DynamicShapes = 1 << 5
    }
}
