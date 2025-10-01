using DeploySharp.Data;
using DeploySharp.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenVinoSharp;
using DeploySharp.Log;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace DeploySharp.Engine
{

    ///// <summary>
    ///// OpenVINO推理引擎实现类，实现IModelInferEngine接口
    ///// </summary>
    //public class OpenVinoInferEngine : IModelInferEngine
    //{
    //    /// <summary>
    //    /// OpenVINO核心对象，负责管理推理引擎
    //    /// </summary>
    //    private Core core;

    //    /// <summary>
    //    /// 加载的模型对象
    //    /// </summary>
    //    private OpenVinoSharp.Model model;

    //    /// <summary>
    //    /// 编译后的模型
    //    /// </summary>
    //    private CompiledModel compiledModel;

    //    /// <summary>
    //    /// 推理请求列表
    //    /// </summary>
    //    private List<InferRequest> inferRequests;

    //    /// <summary>
    //    /// 输入节点数量
    //    /// </summary>
    //    private int inputNodeSize;

    //    /// <summary>
    //    /// 输出节点数量
    //    /// </summary>
    //    private int outputNodeSize;
    //    /// <summary>
    //    /// 输如节点类型列表
    //    /// </summary>
    //    private List<OvType> inputNodeTypes;

    //    /// <summary>
    //    /// 输出节点类型列表
    //    /// </summary>
    //    private List<OvType> outputNodeTypes;

    //    private IConfig modelConfig;

    //    /// <summary>
    //    /// 构造函数，初始化OpenVINO推理引擎
    //    /// </summary>
    //    public OpenVinoInferEngine()
    //    {
    //        MyLogger.Log.Info("开始初始化OpenVINO推理引擎");
    //        core = new Core();

    //        inferRequests = new List<InferRequest>();
    //        inputNodeTypes = new List<OvType>();
    //        outputNodeTypes = new List<OvType>();
    //        MyLogger.Log.Debug("OpenVINO核心对象创建完成");
    //    }

    //    /// <summary>
    //    /// 释放资源
    //    /// </summary>
    //    public void Dispose()
    //    {
    //        MyLogger.Log.Info("开始释放OpenVINO推理引擎资源");
    //        try
    //        {
    //            foreach (var request in inferRequests)
    //            {
    //                request.Dispose();
    //                MyLogger.Log.Debug($"释放推理请求资源");
    //            }
    //            if (compiledModel != null)
    //            {
    //                compiledModel.Dispose();
    //                MyLogger.Log.Debug("释放编译模型资源");
    //            }
    //            if (model != null)
    //            {
    //                model.Dispose();
    //                MyLogger.Log.Debug("释放模型资源");
    //            }
    //            core.Dispose();
    //            MyLogger.Log.Info("OpenVINO核心对象已释放");
    //        }
    //        catch (Exception ex)
    //        {
    //            MyLogger.Log.Error("释放资源时发生异常", ex);
    //            throw;
    //        }
    //    }

    //    /// <summary>
    //    /// 加载模型
    //    /// </summary>
    //    /// <param name="config">模型配置</param>
    //    public void LoadModel(ref IConfig config)
    //    {
    //        MyLogger.Log.Info($"开始加载模型: {config.ModelPath}");
    //        try
    //        {
    //            model = core.read_model(config.ModelPath);
    //            MyLogger.Log.Debug($"模型加载成功: {config.ModelPath}");

    //            inputNodeSize = (int)model.get_inputs_size();
    //            outputNodeSize = (int)model.get_outputs_size();
    //            MyLogger.Log.Info($"模型输入节点数: {inputNodeSize}, 输出节点数: {outputNodeSize}");

    //            List<Input> inputs = model.inputs();
    //            foreach (Input input in inputs)
    //            {
    //                string name = input.get_any_name();
    //                config.InputNames.Add(name);
    //                inputNodeTypes.Add(input.get_element_type());
    //                if (input.get_partial_shape().is_dynamic())
    //                {
    //                    config.DynamicInput = true;
    //                }
    //                else
    //                {
    //                    foreach (var s in input.get_partial_shape().get_dimensions())
    //                    {
    //                        if(s.is_dynamic())
    //                        {
    //                            config.DynamicInput = true;
    //                            break;
    //                        }
    //                    }
    //                }
    //                if (!config.DynamicInput)
    //                {
    //                    var shape = input.get_shape().Select(x => (int)x).ToArray();
    //                    config.InputSizes.Add(shape);
    //                    MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {input.get_element_type().to_string()}, 形状: [{string.Join(",", shape)}]");
    //                }
    //                else
    //                { 
    //                    MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {input.get_element_type().to_string()}"); 
    //                }

    //            }

    //            List<Output> outputs = model.outputs();
    //            foreach (Output output in outputs)
    //            {
    //                string name = output.get_any_name();
    //                config.OutputNames.Add(name);
    //                outputNodeTypes.Add(output.get_element_type());

    //                if (output.get_partial_shape().is_dynamic())
    //                {
    //                    config.DynamicOutput = true;
    //                }
    //                else
    //                {
    //                    foreach (var s in output.get_partial_shape().get_dimensions())
    //                    {
    //                        if (s.get_min()!=s.get_max())
    //                        {
    //                            config.DynamicOutput = true;
    //                            break;
    //                        }
    //                    }
    //                }
    //                if (!config.DynamicOutput)
    //                {
    //                    var shape = output.get_shape().Select(x => (int)x).ToArray();
    //                    config.OutputSizes.Add(shape);
    //                    MyLogger.Log.Debug($"模型输出节点: {name}, 类型: {output.get_element_type().to_string()}, 形状: [{string.Join(",", shape)}]");
    //                }
    //                else
    //                {
    //                    MyLogger.Log.Debug($"模型输出节点: {name}, 类型: {output.get_element_type().to_string()}");
    //                }

    //            }

    //            string deviceName = config.TargetDeviceType.GetDisplayName();
    //            MyLogger.Log.Info($"开始编译模型到设备: {deviceName}");
    //            compiledModel = core.compile_model(config.ModelPath, deviceName);
    //            MyLogger.Log.Info($"模型编译完成, 目标设备: {deviceName}");

    //            inferRequests.Add(compiledModel.create_infer_request());
    //            MyLogger.Log.Info("推理请求创建成功");

    //            modelConfig = config;
    //        }
    //        catch (Exception ex)
    //        {
    //            MyLogger.Log.Error($"加载模型失败: {config.ModelPath}", ex);
    //            throw;
    //        }
    //    }

    //    /// <summary>
    //    /// 执行推理预测
    //    /// </summary>
    //    /// <param name="inputs">输入数据张量</param>
    //    /// <returns>输出数据张量</returns>
    //    public DataTensor Predict(DataTensor inputs)
    //    {
    //        MyLogger.Log.Info("开始执行推理预测");
    //        try
    //        {
    //            // 设置输入数据
    //            for (int i = 0; i < inputs.Count; i++)
    //            {
    //                Tensor inputTensor = inferRequests[0].get_input_tensor((ulong)i);
    //                NodeData data = inputs[i];
    //                if (modelConfig.DynamicInput) 
    //                {
    //                    inputTensor.set_shape( new Shape(modelConfig.InputSizes[0]));
    //                }
    //                if (data.DataType == typeof(float))
    //                {
    //                    inputTensor.set_data<float>(data.DataBuffer as float[]);
    //                    MyLogger.Log.Debug($"设置输入张量{i}数据: float[{string.Join(",", data.Shape)}]");
    //                }
    //                else if (data.DataType == typeof(int))
    //                {
    //                    inputTensor.set_data<int>(data.DataBuffer as int[]);
    //                    MyLogger.Log.Debug($"设置输入张量{i}数据: int[{string.Join(",", data.Shape)}]");
    //                }
    //            }

    //            // 执行推理
    //            MyLogger.Log.Debug("开始执行推理");
    //            inferRequests[0].infer();
    //            MyLogger.Log.Info("推理执行完成");

    //            // 处理输出结果
    //            DataTensor dataTensor = new DataTensor();
    //            if (modelConfig.DynamicOutput)
    //            {
    //                modelConfig.OutputSizes.Clear();
    //            }
    //            for (int i = 0; i < outputNodeSize; i++)
    //            {
    //                Tensor outputTensor = inferRequests[0].get_output_tensor((ulong)i);
    //                var shape = outputTensor.get_shape().Select(x => (int)x).ToArray();

    //                if (modelConfig.DynamicOutput)
    //                {
    //                    modelConfig.OutputSizes.Add(shape);
    //                }
    //                if (outputTensor.get_element_type().get_type() == ElementType.F16 || outputTensor.get_element_type().get_type() == ElementType.F32)
    //                {
    //                    float[] data = outputTensor.get_data<float>((int)outputTensor.get_size());
    //                    dataTensor.AddNode(modelConfig.OutputNames[i],
    //                        0,
    //                        TensorType.Output,
    //                        data,
    //                        shape,
    //                        typeof(float));
    //                    MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: float[{string.Join(",", shape)}]");
    //                }
    //                else if (outputTensor.get_element_type().get_type() == ElementType.I32)
    //                {
    //                    int[] data = outputTensor.get_data<int>((int)outputTensor.get_size());
    //                    dataTensor.AddNode(modelConfig.OutputNames[i],
    //                        0,
    //                        TensorType.Output,
    //                        data,
    //                        shape,
    //                        typeof(int));
    //                    MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
    //                }
    //                else if (outputTensor.get_element_type().get_type() == ElementType.BOOLEAN)
    //                {
    //                    ulong a = outputTensor.get_size();
    //                    byte[] data = outputTensor.get_data<byte>((int)outputTensor.get_size());
    //                    dataTensor.AddNode(modelConfig.OutputNames[i],
    //                        0,
    //                        TensorType.Output,
    //                        data,
    //                        shape,
    //                        typeof(byte));
    //                    MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
    //                }
    //            }

    //            MyLogger.Log.Info("推理结果处理完成");
    //            return dataTensor;
    //        }
    //        catch (Exception ex)
    //        {
    //            MyLogger.Log.Error("推理过程中发生异常", ex);
    //            throw;
    //        }
    //    }
    //}


    /// <summary>
    /// OpenVINO inference engine implementation conforming to IModelInferEngine interface
    /// OpenVINO推理引擎实现类，遵循IModelInferEngine接口
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Model loading and compilation
    /// - Synchronous inference execution
    /// - Dynamic shape support
    /// - Multi-device execution (CPU, GPU, NPU)
    /// 
    /// 本类提供：
    /// - 模型加载和编译
    /// - 同步推理执行
    /// - 动态形状支持
    /// - 多设备执行（CPU、GPU、NPU）
    /// </remarks>
    public class OpenVinoInferEngine : IModelInferEngine
    {
        /// <summary>
        /// OpenVINO Core instance responsible for managing inference engine
        /// OpenVINO核心对象，负责管理推理引擎
        /// </summary>
        private Core core;

        /// <summary>
        /// Loaded OpenVINO model representation
        /// 加载的模型对象
        /// </summary>
        private OpenVinoSharp.Model model;

        /// <summary>
        /// Compiled model ready for inference
        /// 编译后的模型
        /// </summary>
        private CompiledModel compiledModel;

        /// <summary>
        /// Pool of inference requests for parallel processing
        /// 推理请求池（用于并行处理）
        /// </summary>
        private List<InferRequest> inferRequests = new List<InferRequest>();

        /// <summary>
        /// Number of input nodes in the model
        /// 输入节点数量
        /// </summary>
        public int InputNodeCount { get; private set; }

        /// <summary>
        /// Number of output nodes in the model
        /// 输出节点数量
        /// </summary>
        public int OutputNodeCount { get; private set; }

        /// <summary>
        /// List of input node element types
        /// 输入节点类型列表
        /// </summary>
        private List<OvType> inputNodeTypes = new List<OvType>();

        /// <summary>
        /// List of output node element types
        /// 输出节点类型列表
        /// </summary>
        private List<OvType> outputNodeTypes = new List<OvType>();

        /// <summary>
        /// Current model configuration
        /// 当前模型配置
        /// </summary>
        private IConfig modelConfig;

        /// <summary>
        /// Initializes a new instance of OpenVINO inference engine
        /// 初始化OpenVINO推理引擎
        /// </summary>
        public OpenVinoInferEngine()
        {
            MyLogger.Log.Info("Initializing OpenVINO inference engine");
            core = new Core();
            MyLogger.Log.Debug("OpenVINO core instance created");
        }

        /// <summary>
        /// Releases all resources used by the inference engine
        /// 释放推理引擎使用的所有资源
        /// </summary>
        public void Dispose()
        {
            MyLogger.Log.Info("Releasing OpenVINO resources");
            try
            {
                // Dispose inference requests in parallel
                Parallel.ForEach(inferRequests, request =>
                {
                    request.Dispose();
                    MyLogger.Log.Debug("Inference request disposed");
                });

                compiledModel?.Dispose();
                MyLogger.Log.Debug("Compiled model disposed");

                model?.Dispose();
                MyLogger.Log.Debug("Model disposed");

                core.Dispose();
                MyLogger.Log.Info("OpenVINO core disposed");
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("Resource disposal failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Loads and compiles the model with specified configuration
        /// 加载并使用指定配置编译模型
        /// </summary>
        /// <param name="config">Model configuration 模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null 当config为null时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file not found 当模型文件不存在时抛出</exception>
        /// <exception cref="InvalidOperationException">Thrown when model loading fails 当模型加载失败时抛出</exception>
        public void LoadModel(ref IConfig config)
        {
            ValidateConfig(ref config);
            LoadModelInternal(config.ModelPath);
            AnalyzeModelStructure(ref config);
            CompileModel(config);
            InitializeInferenceRequests();

            modelConfig = config;
            MyLogger.Log.Info($"Model loaded successfully: {config.ModelPath}");
        }

        /// <summary>
        /// Executes inference using the provided input tensor
        /// 使用提供的输入张量执行推理
        /// </summary>
        /// <param name="inputs">Input data tensor 输入数据张量</param>
        /// <returns>Output data tensor containing inference results 包含推理结果的输出数据张量</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when:
        /// - Input tensor doesn't match model requirements
        /// - Inference execution fails
        /// - Output processing fails
        /// 
        /// 以下情况抛出：
        /// - 输入张量与模型要求不匹配
        /// - 推理执行失败
        /// - 输出处理失败
        /// </exception>
        public DataTensor Predict(DataTensor inputs)
        {
            ValidateInputTensor(inputs);

            MyLogger.Log.Info("Starting inference execution");
            var timer = Stopwatch.StartNew();

            try
            {
                // Step 1: Prepare input tensors
                SetInputTensors(inputs);

                // Step 2: Execute inference
                ExecuteInference();

                // Step 3: Process output tensors
                return ProcessOutputs();
            }
            finally
            {
                MyLogger.Log.Info($"Inference completed in {timer.ElapsedMilliseconds}ms");
            }
        }

        #region Private Implementation Details
        /// <summary>
        /// Validates the model configuration before loading
        /// 在加载前验证模型配置
        /// </summary>
        /// <param name="config">Model configuration reference 模型配置引用</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when config is null or contains invalid parameters
        /// 当配置为空或包含无效参数时抛出
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when specified model file does not exist
        /// 当指定的模型文件不存在时抛出
        /// </exception>
        private void ValidateConfig(ref IConfig config)
        {
            // Parameter null check
            if (config == null)
                throw new ArgumentNullException(nameof(config),
                    "Model configuration cannot be null");

            // Model path validation
            if (string.IsNullOrWhiteSpace(config.ModelPath))
                throw new ArgumentException(
                    "Model path cannot be empty or whitespace",
                    nameof(config.ModelPath));

            // File existence check with detailed error
            if (!File.Exists(config.ModelPath))
            {
                string absolutePath = Path.GetFullPath(config.ModelPath);
                throw new FileNotFoundException(
                    $"Model file not found at: {absolutePath}",
                    config.ModelPath);
            }
        }

        /// <summary>
        /// Internal method to load OpenVINO model from specified path
        /// 从指定路径加载OpenVINO模型的内部方法
        /// </summary>
        /// <param name="modelPath">Path to the model file 模型文件路径</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when model loading fails
        /// 当模型加载失败时抛出
        /// </exception>
        private void LoadModelInternal(string modelPath)
        {
            try
            {
                MyLogger.Log.Info($"Loading model from: {modelPath}");
                var loadTimer = Stopwatch.StartNew();

                model = core.read_model(modelPath);

                // Cache model structure information
                InputNodeCount = (int)model.get_inputs_size();
                OutputNodeCount = (int)model.get_outputs_size();

                loadTimer.Stop();
                MyLogger.Log.Info($"Model loaded successfully - Inputs: {InputNodeCount}, " +
                                 $"Outputs: {OutputNodeCount} in {loadTimer.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"Failed to load model from {modelPath}", ex);
                throw new InvalidOperationException("Model loading failed", ex);
            }
        }
        /// <summary>
        /// Analyzes complete model structure including inputs and outputs
        /// 分析完整的模型结构（包括输入和输出）
        /// </summary>
        /// <param name="config">Reference to configuration object 配置对象引用</param>
        private void AnalyzeModelStructure(ref IConfig config)
        {
            AnalyzeInputNodes(ref config);
            AnalyzeOutputNodes(ref config);
        }
        /// <summary>
        /// Analyzes and records model input node properties
        /// 分析并记录模型输入节点属性
        /// </summary>
        private void AnalyzeInputNodes(ref IConfig config)
        {
            config.InputNames.Clear();
            inputNodeTypes.Clear();

            foreach (Input input in model.inputs())
            {
                string name = input.get_any_name();
                config.InputNames.Add(name);
                inputNodeTypes.Add(input.get_element_type());

                bool isDynamic = IsShapeDynamic(input.get_partial_shape());
                config.DynamicInput |= isDynamic;

                if (!isDynamic)
                {
                    var shape = input.get_shape().Select(x => (int)x).ToArray();
                    config.InputSizes.Add(shape);
                    MyLogger.Log.Debug($"Input node: {name}, Type: {input.get_element_type()}, Shape: [{string.Join(",", shape)}]");
                }
                else
                {
                    MyLogger.Log.Debug($"Dynamic input node: {name}, Type: {input.get_element_type()}");
                }
            }
        }
        /// <summary>
        /// Analyzes and records model output node properties
        /// 分析并记录模型输出节点属性
        /// </summary>
        private void AnalyzeOutputNodes(ref IConfig config)
        {
            config.OutputNames.Clear();
            outputNodeTypes.Clear();

            foreach (Output output in model.outputs())
            {
                string name = output.get_any_name();
                config.OutputNames.Add(name);
                outputNodeTypes.Add(output.get_element_type());

                bool isDynamic = IsShapeDynamic(output.get_partial_shape());
                config.DynamicOutput |= isDynamic;

                if (!isDynamic)
                {
                    var shape = output.get_shape().Select(x => (int)x).ToArray();
                    config.OutputSizes.Add(shape);
                    MyLogger.Log.Debug($"Output node: {name}, Type: {output.get_element_type()}, Shape: [{string.Join(",", shape)}]");
                }
                else
                {
                    MyLogger.Log.Debug($"Dynamic output node: {name}, Type: {output.get_element_type()}");
                }
            }
        }
        /// <summary>
        /// Determines if a PartialShape contains dynamic dimensions
        /// 判断PartialShape是否包含动态维度
        /// </summary>
        private bool IsShapeDynamic(PartialShape shape)
        {
            if (shape.is_dynamic()) return true;

            foreach (var dim in shape.get_dimensions())
            {
                if (dim.get_min() != dim.get_max())
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Compiles the loaded model for target device
        /// 为指定设备编译已加载的模型
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when compilation fails
        /// 当编译失败时抛出
        /// </exception>
        private void CompileModel(IConfig config)
        {
            string deviceName = config.TargetDeviceType.GetDisplayName();
            MyLogger.Log.Info($"Compiling model for device: {deviceName}");

            compiledModel = core.compile_model(model, deviceName);
            MyLogger.Log.Info($"Model compiled successfully for {deviceName}");
        }

        private void InitializeInferenceRequests()
        {
            MyLogger.Log.Info("Initializing inference requests");
            inferRequests.Add(compiledModel.create_infer_request());
            MyLogger.Log.Info($"Created {inferRequests.Count} inference requests");
        }
        /// <summary>
        /// Validates input tensor against model requirements
        /// 根据模型要求验证输入张量
        /// </summary>
        private void ValidateInputTensor(DataTensor inputs)
        {
            if (inputs.Count != InputNodeCount)
                throw new InvalidOperationException(
                    $"Input tensor count mismatch. Expected: {InputNodeCount}, Received: {inputs.Count}");
        }
        /// <summary>
        /// Sets input tensors for inference
        /// 为推理设置输入张量
        /// </summary>
        private void SetInputTensors(DataTensor inputs)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                Tensor inputTensor = inferRequests[0].get_input_tensor((ulong)i);
                NodeData data = inputs[i];

                if (modelConfig.DynamicInput)
                    inputTensor.set_shape(new Shape(modelConfig.InputSizes[0]));

                switch (data.DataType)
                {
                    case Type t when t == typeof(float):
                        inputTensor.set_data(data.DataBuffer as float[]);
                        MyLogger.Log.Debug($"Set input tensor {i}: float[{string.Join(",", data.Shape)}]");
                        break;

                    case Type t when t == typeof(int):
                        inputTensor.set_data(data.DataBuffer as int[]);
                        MyLogger.Log.Debug($"Set input tensor {i}: int[{string.Join(",", data.Shape)}]");
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported input type: {data.DataType}");
                }
            }
        }
        /// <summary>
        /// Executes synchronous inference
        /// 执行同步推理
        /// </summary>
        private void ExecuteInference()
        {
            MyLogger.Log.Debug("Executing inference");
            inferRequests[0].infer();
            MyLogger.Log.Debug("Inference execution completed");
        }
        /// <summary>
        /// Processes and collects all output tensors
        /// 处理并收集所有输出张量
        /// </summary>
        private DataTensor ProcessOutputs()
        {
            DataTensor result = new DataTensor();

            if (modelConfig.DynamicOutput)
                modelConfig.OutputSizes.Clear();

            for (int i = 0; i < OutputNodeCount; i++)
            {
                Tensor outputTensor = inferRequests[0].get_output_tensor((ulong)i);
                var shape = outputTensor.get_shape().Select(x => (int)x).ToArray();

                if (modelConfig.DynamicOutput)
                    modelConfig.OutputSizes.Add(shape);

                ProcessOutputTensor(result, outputTensor, i, shape);
            }

            return result;
        }
        /// <summary>
        /// Processes a single output tensor based on its type
        /// 根据类型处理单个输出张量
        /// </summary>
        private void ProcessOutputTensor(DataTensor result, Tensor outputTensor, int index, int[] shape)
        {
            switch (outputTensor.get_element_type().get_type())
            {
                case ElementType.F16:
                case ElementType.F32:
                    float[] floatData = outputTensor.get_data<float>((int)outputTensor.get_size());
                    result.AddNode(modelConfig.OutputNames[index], 0, TensorType.Output,
                        floatData, shape, typeof(float));
                    MyLogger.Log.Debug($"Processed output {index}: float[{string.Join(",", shape)}]");
                    break;

                case ElementType.I32:
                    int[] intData = outputTensor.get_data<int>((int)outputTensor.get_size());
                    result.AddNode(modelConfig.OutputNames[index], 0, TensorType.Output,
                        intData, shape, typeof(int));
                    MyLogger.Log.Debug($"Processed output {index}: int[{string.Join(",", shape)}]");
                    break;

                case ElementType.BOOLEAN:
                    byte[] boolData = outputTensor.get_data<byte>((int)outputTensor.get_size());
                    result.AddNode(modelConfig.OutputNames[index], 0, TensorType.Output,
                        boolData, shape, typeof(byte));
                    MyLogger.Log.Debug($"Processed output {index}: byte[{string.Join(",", shape)}]");
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported output type: {outputTensor.get_element_type().get_type()}");
            }
        }

        #endregion
    }

}
