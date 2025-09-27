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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace DeploySharp.Engine
{
    public class OnnxRuntimeInferEngine : IModelInferEngine
    {
        private SessionOptions sessionOptions;
        private InferenceSession inferenceSession;

        /// <summary>
        /// 输入节点数量
        /// </summary>
        private int inputNodeSize;

        /// <summary>
        /// 输出节点数量
        /// </summary>
        private int outputNodeSize;
        /// <summary>
        /// 输如节点类型列表
        /// </summary>
        private List<Type> inputNodeTypes;

        /// <summary>
        /// 输出节点类型列表
        /// </summary>
        private List<Type> outputNodeTypes;

        private IConfig modelConfig;
        public void Dispose()
        {
            inferenceSession.Dispose();
            sessionOptions.Dispose();
        }
        /// <summary>
        /// 构造函数，初始化OpenVINO推理引擎
        /// </summary>
        public OnnxRuntimeInferEngine()
        { 
            MyLogger.Log.Info("开始初始化 ONNX Runtime 推理引擎");
            sessionOptions = new SessionOptions();
            sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;
            MyLogger.Log.Debug("ONNX Runtime 核心对象创建完成");
            inputNodeTypes = new List<Type>();
            outputNodeTypes = new List<Type>();
        }
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
                    throw new DeploySharpException("\"ModelConfig.TargetDeviceType == DeviceType.AUTO\" can only be used when \"ModelConfig.TargetOnnxRuntimeDeviceType==OnnxRuntimeDeviceType.OpenVINO\" ");
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
                    throw new DeploySharpException("\"ModelConfig.TargetDeviceType == DeviceType.NPU\" can only be used when \"ModelConfig.TargetOnnxRuntimeDeviceType==OnnxRuntimeDeviceType.OpenVINO\" ");
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

        public DataTensor Predict(DataTensor inputs)
        {
            MyLogger.Log.Info("开始执行推理预测");
            try
            {
                var inputsData = new Dictionary<string, OrtValue>();
                // 设置输入数据
                for (int i = 0; i < inputs.Count; i++)
                {
                    NodeData data = inputs[i];

                    if (data.DataType == typeof(float))
                    {
                        var inputOrtValue = OrtValue.CreateTensorValueFromMemory(data.DataBuffer as float[], data.Shape.Select(x => (long)x).ToArray());
                        inputsData.Add(data.Name, inputOrtValue);
                        MyLogger.Log.Debug($"设置输入张量{i}数据: float[{string.Join(",", data.Shape)}]");
                    }
                    else if (data.DataType == typeof(int))
                    {
                        var inputOrtValue = OrtValue.CreateTensorValueFromMemory(data.DataBuffer as int[], data.Shape.Select(x => (long)x).ToArray());
                        inputsData.Add(data.Name, inputOrtValue);
                        MyLogger.Log.Debug($"设置输入张量{i}数据: int[{string.Join(",", data.Shape)}]");
                    }
                }
               
                var runOptions = new RunOptions();
                // 执行推理
                MyLogger.Log.Debug("开始执行推理");
                var outputs = inferenceSession.Run(runOptions, inputsData, modelConfig.OutputNames);
                MyLogger.Log.Info("推理执行完成");

                // 处理输出结果
                DataTensor dataTensor = new DataTensor();
                if (modelConfig.DynamicOutput)
                {
                    modelConfig.OutputSizes.Clear();
                }
                for (int i = 0; i < outputs.Count; i++)
                {
                    var resultData = outputs[i];
                    var shape = resultData.GetTensorTypeAndShape().Shape;
                    if (modelConfig.DynamicOutput) 
                    {
                        modelConfig.OutputSizes.Add(shape.Select(item => (int)item).ToArray());
                    }
                    TensorElementType type = resultData.GetTensorTypeAndShape().ElementDataType;
                    if (type == TensorElementType.Float)
                    {
                        var data = resultData.GetTensorDataAsSpan<float>().ToArray();

                        dataTensor.AddNode(modelConfig.OutputNames[i],
                            0,
                            TensorType.Output,
                            data,
                            shape.Select(x => (int)x).ToArray(),
                            typeof(float));
                        MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: float[{string.Join(",", shape)}]");
                    }
                    else if (type == TensorElementType.Int32)
                    {
                        var data = resultData.GetTensorDataAsSpan<int>().ToArray();
                        dataTensor.AddNode(modelConfig.OutputNames[i],
                            0,
                            TensorType.Output,
                            data,
                            shape.Select(x => (int)x).ToArray(),
                            typeof(int));
                        MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
                    }
                    else if (type == TensorElementType.Bool)
                    {
                        var data = resultData.GetTensorDataAsSpan<byte>().ToArray();
                        dataTensor.AddNode(modelConfig.OutputNames[i],
                            0,
                            TensorType.Output,
                            data,
                            shape.Select(x => (int)x).ToArray(),
                            typeof(byte));
                        MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
                    }
                }

                MyLogger.Log.Info("推理结果处理完成");
                return dataTensor;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("推理过程中发生异常", ex);
                throw;
            }
        }
    }
}
