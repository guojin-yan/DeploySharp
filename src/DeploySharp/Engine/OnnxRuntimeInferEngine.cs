using DeploySharp.Common;
using DeploySharp.Data;
using DeploySharp.Log;
using DeploySharp.Model;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenVinoSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace DeploySharp.Engine
{
    /// <summary>
    /// ONNX Runtime inference engine implementation
    /// ONNX Runtime推理引擎实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides ONNX Runtime-specific implementation of the IModelInferEngine interface,
    /// supporting multiple execution providers (CPU, CUDA, TensorRT, OpenVINO, etc.)
    /// 该类提供IModelInferEngine接口的ONNX Runtime特定实现，支持多种执行提供程序(CPU、CUDA、TensorRT、OpenVINO等)
    /// </para>
    /// <para>
    /// The engine handles model loading, session management, and tensor data conversions.
    /// 该引擎处理模型加载、会话管理和张量数据转换
    /// </para>
    /// </remarks>
    public class OnnxRuntimeInferEngine : IModelInferEngine
    {
        /// <summary>
        /// ONNX Runtime session options
        /// ONNX Runtime会话选项
        /// </summary>
        private SessionOptions sessionOptions;

        /// <summary>
        /// ONNX Runtime inference session
        /// ONNX Runtime推理会话
        /// </summary>
        private InferenceSession inferenceSession;

        /// <summary>
        /// Count of input nodes
        /// 输入节点数量
        /// </summary>
        private int inputNodeSize;

        /// <summary>
        /// Count of output nodes
        /// 输出节点数量
        /// </summary>
        private int outputNodeSize;

        /// <summary>
        /// List of input node data types
        /// 输入节点类型列表
        /// </summary>
        private List<Type> inputNodeTypes;

        /// <summary>
        /// List of output node data types
        /// 输出节点类型列表
        /// </summary>
        private List<Type> outputNodeTypes;

        /// <summary>
        /// Model configuration reference
        /// 模型配置引用
        /// </summary>
        private IConfig modelConfig;
        /// <summary>
        /// Releases all resources used by the inference engine
        /// 释放推理引擎使用的所有资源
        /// </summary>
        /// <remarks>
        /// Implements IDisposable pattern to clean up both managed and native resources
        /// 实现IDisposable模式来清理托管和原生资源
        /// </remarks>
        public void Dispose()
        {
            inferenceSession?.Dispose();
            sessionOptions?.Dispose();
        }

        /// <summary>
        /// Initializes a new instance of the ONNX Runtime inference engine
        /// 初始化ONNX Runtime推理引擎的新实例
        /// </summary>
        /// <remarks>
        /// Creates base session configuration with warning-level logging
        /// 创建具有警告级别日志记录的基础会话配置
        /// </remarks>
        public OnnxRuntimeInferEngine()
        {
            MyLogger.Log.Info("开始初始化 ONNX Runtime 推理引擎");
            sessionOptions = new SessionOptions();
            sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;
            MyLogger.Log.Debug("ONNX Runtime 核心对象创建完成");
            inputNodeTypes = new List<Type>();
            outputNodeTypes = new List<Type>();
        }
        /// <summary>
        /// Loads and configures the ONNX model based on the provided configuration
        /// 根据提供的配置加载和配置ONNX模型
        /// </summary>
        /// <param name="config">
        /// Reference to model configuration containing:
        /// - Target device type (CPU, GPU0, GPU1, AUTO, NPU)
        /// - ONNX Runtime execution provider type
        /// - Model path
        /// - Input/output specifications
        /// 
        /// 引用模型配置，包含：
        /// - 目标设备类型(CPU、GPU0、GPU1、AUTO、NPU)
        /// - ONNX Runtime执行提供程序类型
        /// - 模型路径
        /// - 输入/输出规范
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when config is null or ModelPath is empty
        /// 当config为null或ModelPath为空时抛出
        /// </exception>
        /// <exception cref="DeploySharpException">
        /// Thrown for:
        /// - Invalid device/execution provider combinations
        /// - Unsupported device types
        /// - Failed model loading
        /// 
        /// 以下情况抛出：
        /// - 无效的设备/执行提供程序组合
        /// - 不支持的设备类型
        /// - 模型加载失败
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when model analysis fails
        /// 当模型分析失败时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Performs three main operations:
        /// 1. Configures appropriate execution provider
        /// 2. Loads ONNX model session
        /// 3. Analyzes model metadata
        /// 
        /// 执行三个主要操作：
        /// 1. 配置适当的执行提供程序
        /// 2. 加载ONNX模型会话
        /// 3. 分析模型元数据
        /// </para>
        /// <para>
        /// Modifies the configuration object by adding:
        /// - Input/output names and shapes
        /// - Dynamic input/output detection
        /// - Category labels (if available)
        /// 
        /// 修改配置对象，添加：
        /// - 输入/输出名称和形状
        /// - 动态输入/输出检测
        /// - 类别标签(如果可用)
        /// </para>
        /// </remarks>
        public void LoadModel(ref IConfig config)
        {
            if (config.TargetDeviceType == DeviceType.CPU)
            {

                if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.OpenVINO)
                {
                    sessionOptions.AppendExecutionProvider_OpenVINO("CPU");
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.Dnnl)
                {
                    sessionOptions.AppendExecutionProvider_Dnnl();
                }
                else
                {
                    sessionOptions.AppendExecutionProvider_CPU();
                }
            }
            else if (config.TargetDeviceType == DeviceType.GPU0)
            {
                if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.TensorRT)
                {
                    sessionOptions.AppendExecutionProvider_Tensorrt(0);
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.OpenVINO)
                {
                    sessionOptions.AppendExecutionProvider_OpenVINO("GPU.0");
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.DML)
                {
                    sessionOptions.AppendExecutionProvider_DML(0);
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.ROCm)
                {
                    sessionOptions.AppendExecutionProvider_ROCm(0);
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.MIGraphX)
                {
                    sessionOptions.AppendExecutionProvider_MIGraphX(0);
                }
                else
                {
                    sessionOptions.AppendExecutionProvider_CUDA(0);
                }

            }
            else if (config.TargetDeviceType == DeviceType.GPU1)
            {
                if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.TensorRT)
                {
                    sessionOptions.AppendExecutionProvider_Tensorrt(1);
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.OpenVINO)
                {
                    sessionOptions.AppendExecutionProvider_OpenVINO("GPU.1");
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.DML)
                {
                    sessionOptions.AppendExecutionProvider_DML(1);
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.ROCm)
                {
                    sessionOptions.AppendExecutionProvider_ROCm(1);
                }
                else if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.MIGraphX)
                {
                    sessionOptions.AppendExecutionProvider_MIGraphX(1);
                }
                else
                {
                    sessionOptions.AppendExecutionProvider_CUDA(1);
                }
            }
            else if (config.TargetDeviceType == DeviceType.AUTO) 
            {
                if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.OpenVINO)
                {
                    sessionOptions.AppendExecutionProvider_OpenVINO("AUTO");
                }
                else 
                {
                    throw CreateInvalidProviderException("AUTO", "OpenVINO");
                }
                
            }
            else if (config.TargetDeviceType == DeviceType.NPU)
            {
                if (config.TargetOnnxRuntimeDeviceType == OnnxRuntimeDeviceType.OpenVINO)
                {
                    sessionOptions.AppendExecutionProvider_OpenVINO("NPU");
                }
                else
                {
                    throw CreateInvalidProviderException("NPU", "OpenVINO");
                }

            }

            inferenceSession = new InferenceSession(config.ModelPath, sessionOptions);
            if (inferenceSession.ModelMetadata.CustomMetadataMap.TryGetValue("names", out var labes)) 
            {
                config.CategoryDict = OnnxParamParse.ParseLabelString(labes);
            }
            
            foreach (var input in inferenceSession.InputMetadata)
            {
                string name = input.Key;
                config.InputNames.Add(name);
                inputNodeTypes.Add(input.Value.ElementType);
                var shape = input.Value.Dimensions;
                foreach (var dim in shape) 
                {
                    if(dim <= 0)
                    {
                        config.DynamicInput = true;
                        break;
                    }
                }
                if (!config.DynamicInput)
                {
                   
                    config.InputSizes.Add(shape);
                    MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {input.Value.ElementType.ToString()}, 形状: [{string.Join(",", shape)}]");
                }
                else
                {
                    MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {input.Value.ElementType.ToString()}");
                }
            }

            foreach (var output in inferenceSession.OutputMetadata)
            {
                string name = output.Key;
                config.OutputNames.Add(name);
                outputNodeTypes.Add(output.Value.ElementType);
                var shape = output.Value.Dimensions;
                foreach (var dim in shape)
                {
                    if (dim <= 0)
                    {
                        config.DynamicOutput = true;
                        break;
                    }
                }
                if (!config.DynamicOutput)
                {
     
                    config.OutputSizes.Add(shape);
                    MyLogger.Log.Debug($"模型输出节点: {name}, 类型: {output.Value.ElementType.ToString()}, 形状: [{string.Join(",", shape)}]");
                }
                else
                {
                    MyLogger.Log.Debug($"模型输出节点: {name}, 类型:{output.Value.ElementType.ToString()}");
                }

            }

            modelConfig = config;


        }

        /// <summary>
        /// Creates standardized exception for invalid execution provider combinations
        /// 为无效的执行提供程序组合创建标准化异常
        /// </summary>
        private DeploySharpException CreateInvalidProviderException(string deviceType, string requiredProvider)
        {
            var msg = $"DeviceType.{deviceType} can only be used with OnnxRuntimeDeviceType.{requiredProvider}";
            MyLogger.Log.Error(msg);
            return new DeploySharpException(msg);
        }

        //public DataTensor Predict(DataTensor inputs)
        //{
        //    MyLogger.Log.Info("开始执行推理预测");
        //    try
        //    {
        //        var inputsData = new Dictionary<string, OrtValue>();
        //        // 设置输入数据
        //        for (int i = 0; i < inputs.Count; i++)
        //        {
        //            NodeData data = inputs[i];

        //            if (data.DataType == typeof(float))
        //            {
        //                var inputOrtValue = OrtValue.CreateTensorValueFromMemory(data.DataBuffer as float[], data.Shape.Select(x => (long)x).ToArray());
        //                inputsData.Add(data.Name, inputOrtValue);
        //                MyLogger.Log.Debug($"设置输入张量{i}数据: float[{string.Join(",", data.Shape)}]");
        //            }
        //            else if (data.DataType == typeof(int))
        //            {
        //                var inputOrtValue = OrtValue.CreateTensorValueFromMemory(data.DataBuffer as int[], data.Shape.Select(x => (long)x).ToArray());
        //                inputsData.Add(data.Name, inputOrtValue);
        //                MyLogger.Log.Debug($"设置输入张量{i}数据: int[{string.Join(",", data.Shape)}]");
        //            }
        //        }

        //        var runOptions = new RunOptions();
        //        // 执行推理
        //        MyLogger.Log.Debug("开始执行推理");
        //        var outputs = inferenceSession.Run(runOptions, inputsData, modelConfig.OutputNames);
        //        MyLogger.Log.Info("推理执行完成");

        //        // 处理输出结果
        //        DataTensor dataTensor = new DataTensor();
        //        if (modelConfig.DynamicOutput)
        //        {
        //            modelConfig.OutputSizes.Clear();
        //        }
        //        for (int i = 0; i < outputs.Count; i++)
        //        {
        //            var resultData = outputs[i];
        //            var shape = resultData.GetTensorTypeAndShape().Shape;
        //            if (modelConfig.DynamicOutput) 
        //            {
        //                modelConfig.OutputSizes.Add(shape.Select(item => (int)item).ToArray());
        //            }
        //            TensorElementType type = resultData.GetTensorTypeAndShape().ElementDataType;
        //            if (type == TensorElementType.Float)
        //            {
        //                var data = resultData.GetTensorDataAsSpan<float>().ToArray();

        //                dataTensor.AddNode(modelConfig.OutputNames[i],
        //                    0,
        //                    TensorType.Output,
        //                    data,
        //                    shape.Select(x => (int)x).ToArray(),
        //                    typeof(float));
        //                MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: float[{string.Join(",", shape)}]");
        //            }
        //            else if (type == TensorElementType.Int32)
        //            {
        //                var data = resultData.GetTensorDataAsSpan<int>().ToArray();
        //                dataTensor.AddNode(modelConfig.OutputNames[i],
        //                    0,
        //                    TensorType.Output,
        //                    data,
        //                    shape.Select(x => (int)x).ToArray(),
        //                    typeof(int));
        //                MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
        //            }
        //            else if (type == TensorElementType.Bool)
        //            {
        //                var data = resultData.GetTensorDataAsSpan<byte>().ToArray();
        //                dataTensor.AddNode(modelConfig.OutputNames[i],
        //                    0,
        //                    TensorType.Output,
        //                    data,
        //                    shape.Select(x => (int)x).ToArray(),
        //                    typeof(byte));
        //                MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
        //            }
        //        }

        //        MyLogger.Log.Info("推理结果处理完成");
        //        return dataTensor;
        //    }
        //    catch (Exception ex)
        //    {
        //        MyLogger.Log.Error("推理过程中发生异常", ex);
        //        throw;
        //    }
        //}

        /// <summary>
        /// Executes model inference/prediction using the provided input tensor
        /// 使用提供的输入张量执行模型推理/预测
        /// </summary>
        /// <param name="inputs">Input data tensor containing all required model inputs 包含所有必需模型输入的输入数据张量</param>
        /// <returns>Output tensor containing model predictions 包含模型预测结果的输出张量</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input tensor is null or empty 当输入张量为null或空时抛出
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when:
        /// - Model is not loaded
        /// - Input shape/type mismatch
        /// - Output processing fails
        /// 
        /// 以下情况抛出：
        /// - 模型未加载
        /// - 输入形状/类型不匹配
        /// - 输出处理失败
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown for unsupported tensor data types 当遇到不支持的张量数据类型时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Supports the following tensor data types:
        /// - Float32 (System.Single)
        /// - Int32 (System.Int32)
        /// - Bool (System.Boolean converted to byte)
        /// 
        /// 支持以下张量数据类型：
        /// - Float32 (System.Single)
        /// - Int32 (System.Int32)
        /// - Bool (System.Boolean转为byte)
        /// </para>
        /// <para>
        /// Handles dynamic output shapes automatically when configured
        /// 在配置时自动处理动态输出形状
        /// </para>
        /// <para>
        /// Typical inference workflow:
        /// 1. Validate inputs
        /// 2. Convert input tensors to ONNX Runtime format
        /// 3. Execute session.Run()
        /// 4. Process and convert outputs
        /// 
        /// 典型的推理工作流程：
        /// 1. 验证输入
        /// 2. 将输入张量转换为ONNX Runtime格式
        /// 3. 执行session.Run()
        /// 4. 处理和转换输出
        /// </para>
        /// </remarks>
        public DataTensor Predict(DataTensor inputs)
        {
            // Parameter validation
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));
            if (inputs.Count == 0)
                throw new ArgumentNullException(nameof(inputs), "Input tensor cannot be empty");
            if (inferenceSession == null)
                throw new InvalidOperationException("Model not loaded - call LoadModel first");

            MyLogger.Log.Info("Starting model inference");
            try
            {
                // Step 1: Prepare input tensors
                var inputsData = PrepareInputTensors(inputs);

                // Step 2: Execute inference
                var outputs = ExecuteModelInference(inputsData);

                // Step 3: Process output tensors
                return ProcessOutputTensors(outputs);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("Inference execution failed", ex);
                throw;  // Preserve stack trace
            }
        }

        #region Private Implementation Details

        /// <summary>
        /// Converts input DataTensor to ONNX Runtime format
        /// 将输入DataTensor转换为ONNX Runtime格式
        /// </summary>
        private Dictionary<string, OrtValue> PrepareInputTensors(DataTensor inputs)
        {
            var inputsData = new Dictionary<string, OrtValue>();

            for (int i = 0; i < inputs.Count; i++)
            {
                var data = inputs[i];

                // Validate input name exists in model
                if (!modelConfig.InputNames.Contains(data.Name))
                {
                    throw new InvalidOperationException($"Input name '{data.Name}' not found in model");
                }

                switch (data.DataType)
                {
                    case Type t when t == typeof(float):
                        inputsData[data.Name] = OrtValue.CreateTensorValueFromMemory(
                            (float[])data.DataBuffer,
                            data.Shape.Select(x => (long)x).ToArray());
                        break;

                    case Type t when t == typeof(int):
                        inputsData[data.Name] = OrtValue.CreateTensorValueFromMemory(
                            (int[])data.DataBuffer,
                            data.Shape.Select(x => (long)x).ToArray());
                        break;

                    case Type t when t == typeof(bool):
                        // Convert bool to byte as ONNX represents bool as byte
                        var byteData = ((bool[])data.DataBuffer).Select(b => b ? (byte)1 : (byte)0).ToArray();
                        inputsData[data.Name] = OrtValue.CreateTensorValueFromMemory(
                            byteData,
                            data.Shape.Select(x => (long)x).ToArray());
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported input data type: {data.DataType}");
                }

                MyLogger.Log.Debug($"Configured input tensor {i}: {data.Name}, type: {data.DataType.Name}, shape: [{string.Join(",", data.Shape)}]");
            }

            return inputsData;
        }

        /// <summary>
        /// Executes model inference session
        /// 执行模型推理会话
        /// </summary>
        private IDisposableReadOnlyCollection<OrtValue> ExecuteModelInference(Dictionary<string, OrtValue> inputsData)
        {
            using var runOptions = new RunOptions();
            MyLogger.Log.Debug("Executing model inference");

            var timer = Stopwatch.StartNew();
            var outputs = inferenceSession.Run(runOptions, inputsData, modelConfig.OutputNames);

            MyLogger.Log.Info($"Inference completed in {timer.ElapsedMilliseconds}ms");
            return outputs;
        }

        /// <summary>
        /// Converts ONNX Runtime outputs to DataTensor format
        /// 将ONNX Runtime输出转换为DataTensor格式
        /// </summary>
        private DataTensor ProcessOutputTensors(IDisposableReadOnlyCollection<OrtValue> outputs)
        {
            var dataTensor = new DataTensor();

            // Clear dynamic output sizes if needed
            if (modelConfig.DynamicOutput)
            {
                modelConfig.OutputSizes.Clear();
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                var resultData = outputs[i];
                var tensorInfo = resultData.GetTensorTypeAndShape();
                var shape = tensorInfo.Shape;
                string outputName = modelConfig.OutputNames[i];

                // Update dynamic output sizes
                if (modelConfig.DynamicOutput)
                {
                    modelConfig.OutputSizes.Add(shape.Select(x => (int)x).ToArray());
                }

                // Process based on output type
                switch (tensorInfo.ElementDataType)
                {
                    case TensorElementType.Float:
                        ProcessFloatOutput(dataTensor, outputName, resultData, shape);
                        break;

                    case TensorElementType.Int32:
                        ProcessIntOutput(dataTensor, outputName, resultData, shape);
                        break;

                    case TensorElementType.Bool:
                        ProcessBoolOutput(dataTensor, outputName, resultData, shape);
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported output type: {tensorInfo.ElementDataType}");
                }
            }

            MyLogger.Log.Info("Output processing completed");
            return dataTensor;
        }
        /// <summary>
        /// Processes float32 output tensor and adds to result collection
        /// 处理float32输出张量并添加到结果集合
        /// </summary>
        /// <param name="dataTensor">Target data tensor collection 目标数据张量集合</param>
        /// <param name="outputName">Name of the output node 输出节点名称</param>
        /// <param name="ortValue">ONNX Runtime tensor value ONNX Runtime张量值</param>
        /// <param name="shape">Tensor shape dimensions 张量形状维度</param>
        private void ProcessFloatOutput(DataTensor dataTensor, string outputName, OrtValue ortValue, long[] shape)
        {
            var floatData = ortValue.GetTensorDataAsSpan<float>().ToArray();
            dataTensor.AddNode(
                outputName,
                0,
                TensorType.Output,
                floatData,
                shape.Select(x => (int)x).ToArray(),
                typeof(float));

            MyLogger.Log.Debug($"Processed float output: {outputName}, shape: [{string.Join(",", shape)}]");
        }

        /// <summary>
        /// Processes int32 output tensor and adds to result collection
        /// 处理int32输出张量并添加到结果集合
        /// </summary>
        private void ProcessIntOutput(DataTensor dataTensor, string outputName, OrtValue ortValue, long[] shape)
        {
            var intData = ortValue.GetTensorDataAsSpan<int>().ToArray();
            dataTensor.AddNode(
                outputName,
                0,
                TensorType.Output,
                intData,
                shape.Select(x => (int)x).ToArray(),
                typeof(int));

            MyLogger.Log.Debug($"Processed int output: {outputName}, shape: [{string.Join(",", shape)}]");
        }

        /// <summary>
        /// Processes bool output tensor (converted to byte) and adds to result collection
        /// 处理bool输出张量(转换为byte)并添加到结果集合
        /// </summary>
        private void ProcessBoolOutput(DataTensor dataTensor, string outputName, OrtValue ortValue, long[] shape)
        {
            var byteData = ortValue.GetTensorDataAsSpan<byte>().ToArray();
            dataTensor.AddNode(
                outputName,
                0,
                TensorType.Output,
                byteData,
                shape.Select(x => (int)x).ToArray(),
                typeof(byte));

            MyLogger.Log.Debug($"Processed bool output: {outputName}, shape: [{string.Join(",", shape)}]");
        }
        #endregion
    }
}
