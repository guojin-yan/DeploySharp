using DeploySharp.Data;
using DeploySharp.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{
    /// <summary>
    /// Defines the core interface for model inference engines.
    /// 定义模型推理引擎的核心接口
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface provides standardization for different inference implementations 
    /// (ONNX Runtime, TensorRT, OpenVINO etc.) to ensure consistent behavior.
    /// 该接口为不同的推理实现(ONNX Runtime、TensorRT、OpenVINO等)提供标准化，确保行为一致。
    /// </para>
    /// <para>
    /// All implementing classes must be thread-safe for Predict operations and properly
    /// manage native resources through IDisposable pattern.
    /// 所有实现类必须保证Predict操作的线程安全性，并通过IDisposable模式正确管理原生资源。
    /// </para>
    /// </remarks>
    public interface IModelInferEngine : IDisposable
    {
        /// <summary>
        /// Performs model prediction/inference on the input tensor.
        /// 对输入张量执行模型预测/推理
        /// </summary>
        /// <param name="input">
        /// Input data tensor containing properly formatted model inputs.
        /// Must match the model's expected input shape and data type.
        /// 包含正确格式化模型输入的输入数据张量。
        /// 必须与模型预期的输入形状和数据类型匹配。
        /// </param>
        /// <returns>
        /// Output data tensor containing model predictions/results.
        /// The shape and type depends on model architecture.
        /// 包含模型预测/结果的输出数据张量。
        /// 其形状和类型取决于模型架构。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input tensor is null.
        /// 当输入张量为null时抛出。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when model is not loaded or initialized.
        /// 当模型未加载或初始化时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when input tensor doesn't match model requirements.
        /// 当输入张量不符合模型要求时抛出。
        /// </exception>
        /// <example>
        /// <code>
        /// using var engine = new OnnxRuntimeEngine();
        /// engine.LoadModel(ref config);
        /// var output = engine.Predict(inputTensor);
        /// </code>
        /// </example>
        DataTensor Predict(DataTensor input);

        /// <summary>
        /// Loads and initializes the model with specified configuration.
        /// 使用指定配置加载并初始化模型。
        /// </summary>
        /// <param name="config">
        /// Reference to model configuration object containing:
        /// - Model path/bytes
        /// - Hardware/execution provider settings
        /// - Input/output specifications
        /// Optional preprocessing/postprocessing parameters
        /// 
        /// 引用模型配置对象，包含：
        /// - 模型路径/字节数据
        /// - 硬件/执行提供程序设置
        /// - 输入/输出规范
        /// - 可选的预处理/后处理参数
        /// </param>
        /// <remarks>
        /// <para>
        /// The configuration object may be modified during loading to reflect:
        /// - Actual model input/output shapes
        /// - Supported precision modes
        /// - Available execution providers
        /// - Optimized parameters
        /// 
        /// 配置对象可能在加载过程中被修改以反映：
        /// - 实际模型输入/输出形状
        /// - 支持的精度模式
        /// - 可用的执行提供程序
        /// - 优化参数
        /// </para>
        /// <para>
        /// Implementations should validate configuration parameters before loading.
        /// 实现应在加载前验证配置参数。
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when config is null.
        /// 当config为null时抛出。
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when model file cannot be found.
        /// 当找不到模型文件时抛出。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when model initialization fails.
        /// 当模型初始化失败时抛出。
        /// </exception>
        void LoadModel(ref IConfig config);

        /// <summary>
        /// Releases all resources used by the inference engine.
        /// 释放推理引擎使用的所有资源。
        /// </summary>
        /// <remarks>
        /// <para>
        /// Implementations should:
        /// - Release model memory and session handles
        /// - Dispose any native resources
        /// - Cleanup temporary files
        /// - Set disposed state
        /// 
        /// 实现应该：
        /// - 释放模型内存和会话句柄
        /// - 处理任何原生资源
        /// - 清理临时文件
        /// - 设置disposed状态
        /// </para>
        /// <para>
        /// Subsequent calls to Predict after disposal should throw ObjectDisposedException.
        /// 在dispose后调用Predict应抛出ObjectDisposedException。
        /// </para>
        /// </remarks>
        new void Dispose();
    }


}
