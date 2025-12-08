using Clipper2Lib;
using DeploySharp.Common;
using DeploySharp.Data;
using DeploySharp.Log;
using DeploySharp.Model;
using JYPPX.TensorRtSharp.Cuda;
using JYPPX.TensorRtSharp.Nvinfer;
using Microsoft.ML.OnnxRuntime;
using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{
    public class TensorRtInferEngine : IModelInferEngine
    {

        Runtime runtime;

        CudaEngine cudaEngine;

        /// <summary>
        /// Pool of inference requests for parallel processing
        /// 推理请求池（用于并行处理）
        /// </summary>
        private List<Triplet<int, ExecutionContext, bool>> executionContexts = new List<Triplet<int, ExecutionContext, bool>>();

        /// <summary>
        /// Lock object for synchronizing inference request access
        /// 用于同步推理请求访问的锁定对象
        /// </summary>
        private readonly object executionContextLock = new object();

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
        private List<TrtDataType> inputNodeTypes = new List<TrtDataType>();

        /// <summary>
        /// List of output node element types
        /// 输出节点类型列表
        /// </summary>
        private List<TrtDataType> outputNodeTypes = new List<TrtDataType>();

        public List<ulong> inputLengths = new List<ulong>();
        public List<ulong> outputLengths = new List<ulong>();
        private List<List<object>> inputCuda1DMemorys = new List<List<object>>();
        private List<List<object>> outputCuda1DMemorys = new List<List<object>>();


        private List<CudaStream> cudaStreams = new List<CudaStream>();
        /// <summary>
        /// Current model configuration
        /// 当前模型配置
        /// </summary>
        private IConfig modelConfig;

        List<object> dataMems;
        /// <summary>
        /// Initializes a new instance of TensorRt inference engine
        /// 初始化TensorRt推理引擎
        /// </summary>
        public TensorRtInferEngine()
        {
            MyLogger.Log.Info("Initializing TensorRt inference engine");
            runtime = new Runtime();
            MyLogger.Log.Debug("TensorRt runtime instance created");
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void LoadModel(ref IConfig config)
        {
            using (FileStream fileStream = new FileStream(config.ModelPath, FileMode.Open, FileAccess.Read)) 
            {
                byte[] data = new byte[fileStream.Length];
                BinaryReader binaryReader = new BinaryReader(fileStream);
                data = binaryReader.ReadBytes((int)fileStream.Length); // 读取整个文件到byte数组
                cudaEngine = runtime.deserializeCudaEngineByBlob(data, (ulong)fileStream.Length);

                int ioCount = cudaEngine.getNbIOTensors();
                List<int[]> outputSizes = new List<int[]>();
                List<int[]> inputSizes = new List<int[]>();
                for (int i = 0; i < ioCount; ++i) 
                {
                    string name = cudaEngine.getIOTensorName(i);
                    if (cudaEngine.getTensorIOMode(name) == TrtTensorIOMode.kINPUT)
                    {
                        InputNodeCount++;
                        inputNodeTypes.Add(cudaEngine.getTensorDataType(name));
                        config.InputNames.Add(name);
                        Dims shape = cudaEngine.getTensorShape(name);
                        inputSizes.Add(DimsToArray(shape));
                        MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {cudaEngine.getTensorDataType(name).ToString()}, 形状: [{string.Join(",", shape.d)}]");

                    }
                    else
                    {
                        OutputNodeCount++;
                        outputNodeTypes.Add(cudaEngine.getTensorDataType(name));
                        config.OutputNames.Add(name);

                        Dims shape = cudaEngine.getTensorShape(name);
                        outputSizes.Add(DimsToArray(shape));
                        MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {cudaEngine.getTensorDataType(name).ToString()}, 形状: [{string.Join(",", shape.d)}]");

                    }
                }



                foreach (var input in inputSizes)
                {

                    foreach (var dim in input)
                    {
                        if (dim <= 0)
                        {
                            config.DynamicInput = true;
                            break;
                        }
                    }
                    if (!config.DynamicInput)
                    {
                        config.InputSizes.Add(input);
                        config.InputShapeType = config.InputShapeType > IOShapeType.StaticShape ? config.InputShapeType : IOShapeType.StaticShape;
                    }
                    else
                    {

                        for (int i = 0; i < input.Length; ++i)
                        {
                            if (input[i] <= 0)
                            {
                                if (i == 0)
                                {
                                    config.InputShapeType = config.InputShapeType > IOShapeType.BatchDynamicShape ? config.InputShapeType : IOShapeType.BatchDynamicShape;
                                }
                                else
                                {
                                    config.InputShapeType = config.InputShapeType > IOShapeType.PartiallyDynamicShape ? config.InputShapeType : IOShapeType.PartiallyDynamicShape;
                                }
                                continue;
                            }

                        }
                        
                    }
                }

                if (config.InputShapeType == IOShapeType.BatchDynamicShape)
                {
                    config.InputSizes.Clear();
                }
                if (config.InputShapeType >= IOShapeType.BatchDynamicShape)
                {
                    foreach (var input in inputSizes)
                    {
                        List<int> newShape = new List<int>();
                        if (config.InputShapeType == IOShapeType.BatchDynamicShape)
                        {
                            for (int i = 0; i < input.Length; ++i)
                            {
                                newShape.Add(config.MaxBatchSize);
                            }
                            for (int i = 0; i < input.Length; ++i)
                            {
                                if (input[i] <= 0)
                                {
                                    continue;
                                }
                                newShape[i] = (int)input[i];
                            }
                            config.InputSizes.Add(newShape.ToArray());
                        }
                        if (config.InputShapeType >= IOShapeType.PartiallyDynamicShape)
                        {
                            if (config.InputSizes.Count != inputSizes.Count)
                            {
                                throw new DeploySharpException("The model attribute shape is fully dynamic and requires setting the input shape.");
                            }
                        }
                    }
                }



                foreach (var outnput in outputSizes)
                {

                    foreach (var dim in outnput)
                    {
                        if (dim <= 0)
                        {
                            config.DynamicInput = true;
                            break;
                        }
                    }
                    if (!config.DynamicInput)
                    {
                        config.OutputSizes.Add(outnput);
                        config.OutputShapeType = config.OutputShapeType > IOShapeType.StaticShape ? config.OutputShapeType : IOShapeType.StaticShape;
                    }
                    else
                    {

                        for (int i = 0; i < outnput.Length; ++i)
                        {
                            if (outnput[i] <= 0)
                            {
                                if (i == 0)
                                {
                                    config.OutputShapeType = config.OutputShapeType > IOShapeType.BatchDynamicShape ? config.OutputShapeType : IOShapeType.BatchDynamicShape;
                                }
                                else
                                {
                                    config.OutputShapeType = config.OutputShapeType > IOShapeType.PartiallyDynamicShape ? config.OutputShapeType : IOShapeType.PartiallyDynamicShape;
                                }
                                continue;
                            }

                        }

                    }
                }

                if (config.OutputShapeType == IOShapeType.BatchDynamicShape)
                {
                    config.OutputSizes.Clear();
                }
                if (config.OutputShapeType >= IOShapeType.BatchDynamicShape)
                {
                    foreach (var output in outputSizes)
                    {
                        List<int> newShape = new List<int>();
                        if (config.OutputShapeType == IOShapeType.BatchDynamicShape)
                        {
                            for (int i = 0; i < output.Length; ++i)
                            {
                                newShape.Add(config.MaxBatchSize);
                            }
                            for (int i = 0; i < output.Length; ++i)
                            {
                                if (output[i] <= 0)
                                {
                                    continue;
                                }
                                newShape[i] = (int)output[i];
                            }
                            config.OutputSizes.Add(newShape.ToArray());
                        }
                        if (config.OutputShapeType >= IOShapeType.PartiallyDynamicShape)
                        {
                            if (config.OutputSizes.Count != outputSizes.Count)
                            {
                                throw new DeploySharpException("The model attribute shape is fully dynamic and requires setting the input shape.");
                            }
                        }
                    }
                }




                MyLogger.Log.Info("Initializing execution context");
                for (int i = 0; i < config.MaxInferRequests; ++i)
                {
                    executionContexts.Add(new Triplet<int, ExecutionContext, bool>(i, 
                        cudaEngine.createExecutionContext(TrtExecutionContextAllocationStrategy.kSTATIC), true));

                    CudaStream cudaStream = new CudaStream();
                    cudaStreams.Add(cudaStream);

                    List<object> inputCuda1DMemory = new List<object>();
                    List<object> outputCuda1DMemory = new List<object>();
                    for (int j = 0; j < config.InputSizes.Count; ++j) 
                    {
                        long totalSize = 1;
                        foreach (var dim in config.InputSizes[j]) 
                        {
                            totalSize *= dim;
                        }
                        inputLengths.Add((ulong)totalSize);
                        TrtDataType dataType = inputNodeTypes[j];
                        
                        if (dataType == TrtDataType.kFLOAT)
                        {
                            Cuda1DMemory<float> deviceMemory = new Cuda1DMemory<float>((ulong)totalSize);
                            inputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setInputTensorAddress(config.InputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kINT32)
                        {

                            Cuda1DMemory<int> deviceMemory = new Cuda1DMemory<int>((ulong)totalSize);
                            inputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setInputTensorAddress(config.InputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kINT8)
                        {
                            Cuda1DMemory<sbyte> deviceMemory = new Cuda1DMemory<sbyte>((ulong)totalSize);
                            inputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setInputTensorAddress(config.InputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kINT64)
                        {
                            Cuda1DMemory<long> deviceMemory = new Cuda1DMemory<long>((ulong)totalSize);
                            inputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setInputTensorAddress(config.InputNames[j], deviceMemory.get());
                        }
                        else if(dataType == TrtDataType.kBOOL)
                        {
                            Cuda1DMemory<byte> deviceMemory = new Cuda1DMemory<byte>((ulong)totalSize);
                            inputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setInputTensorAddress(config.InputNames[j], deviceMemory.get());
                        }
                        else
                        {
                            throw new DeploySharpException($"Unsupported input data type: {dataType.ToString()}");
                        }
                        
                    }

                    inputCuda1DMemorys.Add(inputCuda1DMemory);


                    for (int j = 0; j < config.OutputSizes.Count; ++j)
                    {
                        long totalSize = 1;
                        foreach (var dim in config.OutputSizes[j])
                        {
                            totalSize *= dim;
                        }
                        outputLengths.Add((ulong)totalSize);
                        TrtDataType dataType = outputNodeTypes[j];

                        if (dataType == TrtDataType.kFLOAT)
                        {
                            Cuda1DMemory<float> deviceMemory = new Cuda1DMemory<float>((ulong)totalSize);
                            outputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setOutputTensorAddress(config.OutputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kINT32)
                        {

                            Cuda1DMemory<int> deviceMemory = new Cuda1DMemory<int>((ulong)totalSize);
                            outputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setOutputTensorAddress(config.OutputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kINT8)
                        {
                            Cuda1DMemory<sbyte> deviceMemory = new Cuda1DMemory<sbyte>((ulong)totalSize);
                            outputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setOutputTensorAddress(config.OutputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kINT64)
                        {
                            Cuda1DMemory<long> deviceMemory = new Cuda1DMemory<long>((ulong)totalSize);
                            outputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setOutputTensorAddress(config.OutputNames[j], deviceMemory.get());
                        }
                        else if (dataType == TrtDataType.kBOOL)
                        {
                            Cuda1DMemory<byte> deviceMemory = new Cuda1DMemory<byte>((ulong)totalSize);
                            outputCuda1DMemory.Add(deviceMemory);
                            executionContexts[i].Second.setOutputTensorAddress(config.OutputNames[j], deviceMemory.get());
                        }
                        else
                        {
                            throw new DeploySharpException($"Unsupported input data type: {dataType.ToString()}");
                        }

                    }
                    outputCuda1DMemorys.Add(outputCuda1DMemory);
                }

                MyLogger.Log.Info($"Created {executionContexts.Count} execution context");
            }

            this.modelConfig = config;
        }

        private int[] DimsToArray(Dims dims) 
        {
            List<int> shape = new List<int>();
            for (int i = 0; i < dims.nbDims; ++i)
            {
                shape.Add((int)dims.d[i]);
            }
            return shape.ToArray();
        }

        public DataTensor Predict(DataTensor input)
        {


            int availableRequestIndex = -1;
            lock (executionContextLock)
            {
                while (availableRequestIndex == -1)
                {
                    for (int i = 0; i < modelConfig.MaxInferRequests; ++i)
                    {
                        if (executionContexts[i].Third)
                        {
                            availableRequestIndex = i;
                            executionContexts[i].Third = false;
                            break;
                        }
                    }
                    if (availableRequestIndex == -1)
                    {
                        MyLogger.Log.Error("Unable to obtain inference request object, repeat attempts, allocate more inference request objects in the gap.");
                    }
                }

            }
            try
            {
                // Step 1: Prepare input tensors
                SetInputTensors(executionContexts[availableRequestIndex].Second, availableRequestIndex, input);

                // Step 2: Execute inference
                ExecuteInference(executionContexts[availableRequestIndex].Second, availableRequestIndex);

                // Step 3: Process output tensors
                return ProcessOutputs(availableRequestIndex);
            }
            finally
            {
                executionContexts[availableRequestIndex].Third = true;
            }
            throw new NotImplementedException();
        }


        /// <summary>
        /// Sets input tensors for inference
        /// 为推理设置输入张量
        /// </summary>
        private void SetInputTensors(ExecutionContext executionContext, int availableRequestIndex, DataTensor inputs)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                NodeData data = inputs[i];

                if (modelConfig.InputShapeType != IOShapeType.StaticShape)
                {
                    for (int j = 0; j < modelConfig.InputNames.Count; ++j) 
                    {
                        executionContext.setinputShape(modelConfig.InputNames[j], new Dims(modelConfig.InputSizes[i]));
                    }
                    
                }
                    

                switch (data.DataType)
                {
                    case Type t when t == typeof(float):
                        (inputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<float>).copyFromHostAsync(data.DataBuffer as float[], cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        MyLogger.Log.Debug($"Set input tensor {i}: float[{string.Join(",", data.Shape)}]");
                        break;

                    case Type t when t == typeof(int):
                        (inputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<int>).copyFromHostAsync(data.DataBuffer as int[], cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        MyLogger.Log.Debug($"Set input tensor {i}: int[{string.Join(",", data.Shape)}]");
                        break;
                    case Type t when t == typeof(long):
                        (inputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<long>).copyFromHostAsync(data.DataBuffer as long[], cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        MyLogger.Log.Debug($"Set input tensor {i}: long[{string.Join(",", data.Shape)}]");
                        break;

                    case Type t when t == typeof(byte):
                        (inputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<byte>).copyFromHostAsync(data.DataBuffer as byte[], cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        MyLogger.Log.Debug($"Set input tensor {i}: byte[{string.Join(",", data.Shape)}]");
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
        private void ExecuteInference(ExecutionContext executionContext, int availableRequestIndex)
        {
            MyLogger.Log.Debug("Executing inference");
            executionContext.executeV3(cudaStreams[availableRequestIndex]);
            cudaStreams[availableRequestIndex].Synchronize();
            MyLogger.Log.Debug("Inference execution completed");
        }
        /// <summary>
        /// Processes and collects all output tensors
        /// 处理并收集所有输出张量
        /// </summary>
        private DataTensor ProcessOutputs(int availableRequestIndex)
        {
            DataTensor result = new DataTensor();

            if (modelConfig.InputShapeType != IOShapeType.StaticShape)
                modelConfig.OutputSizes.Clear();

            for (int i = 0; i < OutputNodeCount; i++)
            {
                int[] shape = DimsToArray(executionContexts[availableRequestIndex].Second.getTensorShape(modelConfig.OutputNames[availableRequestIndex]));

                if (modelConfig.InputShapeType != IOShapeType.StaticShape)
                    modelConfig.OutputSizes.Add(shape);
                switch (outputNodeTypes[i])
                {
                    case TrtDataType.kFLOAT:
                        float[] floatData = new float[outputLengths[i]];
                        (outputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<float>).copyToHostAsync(floatData, cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        result.AddNode(modelConfig.OutputNames[i], 0, TensorType.Output,
                            floatData, shape, typeof(float));
                        MyLogger.Log.Debug($"Processed output {i}: float[{string.Join(",", shape)}]");
                        break;
                    case TrtDataType.kINT32:
                        int[] intData = new int[outputLengths[i]];
                        (outputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<int>).copyToHostAsync(intData, cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        result.AddNode(modelConfig.OutputNames[i], 0, TensorType.Output,
                            intData, shape, typeof(int));
                        MyLogger.Log.Debug($"Processed output {i}: float[{string.Join(",", shape)}]");
                        break;
                    case TrtDataType.kINT8:
                        sbyte[] sbyteData = new sbyte[outputLengths[i]];
                        (outputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<sbyte>).copyToHostAsync(sbyteData, cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        result.AddNode(modelConfig.OutputNames[i], 0, TensorType.Output,
                            sbyteData, shape, typeof(sbyte));
                        MyLogger.Log.Debug($"Processed output {i}: float[{string.Join(",", shape)}]");
                        break;
                    case TrtDataType.kINT64:
                        long[] longData = new long[outputLengths[i]];
                        (outputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<long>).copyToHostAsync(longData, cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        result.AddNode(modelConfig.OutputNames[i], 0, TensorType.Output,
                            longData, shape, typeof(long));
                        MyLogger.Log.Debug($"Processed output {i}: float[{string.Join(",", shape)}]");
                        break;
                    case TrtDataType.kBOOL:
                        byte[] byteData = new byte[outputLengths[i]];
                        (outputCuda1DMemorys[availableRequestIndex][i] as Cuda1DMemory<byte>).copyToHostAsync(byteData, cudaStreams[availableRequestIndex]);
                        cudaStreams[availableRequestIndex].Synchronize();
                        result.AddNode(modelConfig.OutputNames[i], 0, TensorType.Output,
                            byteData, shape, typeof(byte));
                        MyLogger.Log.Debug($"Processed output {i}: float[{string.Join(",", shape)}]");
                        break;
                    
                    default:
                        throw new NotSupportedException(
                            $"Unsupported output type: {outputNodeTypes[i].ToString()}");
                }

            }

            return result;
        }
    

    }
}
