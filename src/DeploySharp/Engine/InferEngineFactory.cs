using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{
    /// <summary>
    /// Factory class for creating inference engine instances based on backend type.
    /// 根据后端类型创建推理引擎实例的工厂类
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides a centralized creation point for different inference implementations,
    /// abstracting concrete engine instantiation from consuming code.
    /// 为不同的推理实现提供集中的创建点，将对具体引擎的实例化与使用代码解耦。
    /// </para>
    /// <para>
    /// This factory ensures proper initialization of backend-specific implementations
    /// following consistent patterns.
    /// 该工厂确保后端特定实现的正确初始化遵循一致的模式。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create an ONNX Runtime inference engine
    /// var engine = InferEngineFactory.Create(InferenceBackend.OnnxRuntime);
    /// 
    /// // Create an OpenVINO inference engine
    /// var openVinoEngine = InferEngineFactory.Create(InferenceBackend.OpenVINO);
    /// </code>
    /// </example>
    public class InferEngineFactory
    {
        /// <summary>
        /// Creates an inference engine instance for the specified backend type.
        /// 为指定的后端类型创建推理引擎实例
        /// </summary>
        /// <param name="backend">
        /// The inference backend type to create (OpenVINO, ONNX Runtime etc.)
        /// 要创建的推理后端类型(OpenVINO、ONNX Runtime等)
        /// </param>
        /// <returns>
        /// Initialized inference engine implementing <see cref="IModelInferEngine"/>.
        /// Initialized engine requires calling <see cref="IModelInferEngine.LoadModel"/>.
        /// 
        /// 实现了<see cref="IModelInferEngine"/>的初始化推理引擎。
        /// 初始化后的引擎需要调用<see cref="IModelInferEngine.LoadModel"/>。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when requesting an unsupported backend type.
        /// 当请求不受支持的后端类型时抛出。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when engine initialization fails.
        /// 当引擎初始化失败时抛出。
        /// </exception>
        /// <remarks>
        /// <para>
        /// The created engine is in unloaded state - LoadModel must be called before inference.
        /// 创建的引擎处于未加载状态 - 在推理前必须调用LoadModel。
        /// </para>
        /// <para>
        /// Implementations should optimize backend-specific initialization but defer
        /// heavy operations until LoadModel is called.
        /// 实现应优化特定后端的初始化，但推迟繁重操作直到调用LoadModel。
        /// </para>
        /// </remarks>
        public static IModelInferEngine Create(InferenceBackend backend)
        {
            return backend switch
            {
                InferenceBackend.OpenVINO => new OpenVinoInferEngine(),
                InferenceBackend.OnnxRuntime => new OnnxRuntimeInferEngine(),
                _ => throw new NotSupportedException(
                    $"Unsupported inference backend: {backend}. " +
                    $"Supported backends: {string.Join(", ", Enum.GetValues(typeof(InferenceBackend)))}")
            };
        }
    }

}
