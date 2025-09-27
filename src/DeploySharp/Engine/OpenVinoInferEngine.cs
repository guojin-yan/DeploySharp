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

namespace DeploySharp.Engine
{

    /// <summary>
    /// OpenVINO推理引擎实现类，实现IModelInferEngine接口
    /// </summary>
    public class OpenVinoInferEngine : IModelInferEngine
    {
        /// <summary>
        /// OpenVINO核心对象，负责管理推理引擎
        /// </summary>
        private Core core;

        /// <summary>
        /// 加载的模型对象
        /// </summary>
        private OpenVinoSharp.Model model;

        /// <summary>
        /// 编译后的模型
        /// </summary>
        private CompiledModel compiledModel;

        /// <summary>
        /// 推理请求列表
        /// </summary>
        private List<InferRequest> inferRequests;

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
        private List<OvType> inputNodeTypes;

        /// <summary>
        /// 输出节点类型列表
        /// </summary>
        private List<OvType> outputNodeTypes;

        private IConfig modelConfig;

        /// <summary>
        /// 构造函数，初始化OpenVINO推理引擎
        /// </summary>
        public OpenVinoInferEngine()
        {
            MyLogger.Log.Info("开始初始化OpenVINO推理引擎");
            core = new Core();

            inferRequests = new List<InferRequest>();
            inputNodeTypes = new List<OvType>();
            outputNodeTypes = new List<OvType>();
            MyLogger.Log.Debug("OpenVINO核心对象创建完成");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            MyLogger.Log.Info("开始释放OpenVINO推理引擎资源");
            try
            {
                foreach (var request in inferRequests)
                {
                    request.Dispose();
                    MyLogger.Log.Debug($"释放推理请求资源");
                }
                if (compiledModel != null)
                {
                    compiledModel.Dispose();
                    MyLogger.Log.Debug("释放编译模型资源");
                }
                if (model != null)
                {
                    model.Dispose();
                    MyLogger.Log.Debug("释放模型资源");
                }
                core.Dispose();
                MyLogger.Log.Info("OpenVINO核心对象已释放");
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("释放资源时发生异常", ex);
                throw;
            }
        }

        /// <summary>
        /// 加载模型
        /// </summary>
        /// <param name="config">模型配置</param>
        public void LoadModel(ref IConfig config)
        {
            MyLogger.Log.Info($"开始加载模型: {config.ModelPath}");
            try
            {
                model = core.read_model(config.ModelPath);
                MyLogger.Log.Debug($"模型加载成功: {config.ModelPath}");

                inputNodeSize = (int)model.get_inputs_size();
                outputNodeSize = (int)model.get_outputs_size();
                MyLogger.Log.Info($"模型输入节点数: {inputNodeSize}, 输出节点数: {outputNodeSize}");

                List<Input> inputs = model.inputs();
                foreach (Input input in inputs)
                {
                    string name = input.get_any_name();
                    config.InputNames.Add(name);
                    inputNodeTypes.Add(input.get_element_type());
                    if (input.get_partial_shape().is_dynamic())
                    {
                        config.DynamicInput = true;
                    }
                    else
                    {
                        foreach (var s in input.get_partial_shape().get_dimensions())
                        {
                            if(s.is_dynamic())
                            {
                                config.DynamicInput = true;
                                break;
                            }
                        }
                    }
                    if (!config.DynamicInput)
                    {
                        var shape = input.get_shape().Select(x => (int)x).ToArray();
                        config.InputSizes.Add(shape);
                        MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {input.get_element_type().to_string()}, 形状: [{string.Join(",", shape)}]");
                    }
                    else
                    { 
                        MyLogger.Log.Debug($"模型输入节点: {name}, 类型: {input.get_element_type().to_string()}"); 
                    }
                    
                }

                List<Output> outputs = model.outputs();
                foreach (Output output in outputs)
                {
                    string name = output.get_any_name();
                    config.OutputNames.Add(name);
                    outputNodeTypes.Add(output.get_element_type());

                    if (output.get_partial_shape().is_dynamic())
                    {
                        config.DynamicOutput = true;
                    }
                    else
                    {
                        foreach (var s in output.get_partial_shape().get_dimensions())
                        {
                            if (s.get_min()!=s.get_max())
                            {
                                config.DynamicOutput = true;
                                break;
                            }
                        }
                    }
                    if (!config.DynamicOutput)
                    {
                        var shape = output.get_shape().Select(x => (int)x).ToArray();
                        config.OutputSizes.Add(shape);
                        MyLogger.Log.Debug($"模型输出节点: {name}, 类型: {output.get_element_type().to_string()}, 形状: [{string.Join(",", shape)}]");
                    }
                    else
                    {
                        MyLogger.Log.Debug($"模型输出节点: {name}, 类型: {output.get_element_type().to_string()}");
                    }
                    
                }

                string deviceName = config.TargetDeviceType.GetDisplayName();
                MyLogger.Log.Info($"开始编译模型到设备: {deviceName}");
                compiledModel = core.compile_model(config.ModelPath, deviceName);
                MyLogger.Log.Info($"模型编译完成, 目标设备: {deviceName}");

                inferRequests.Add(compiledModel.create_infer_request());
                MyLogger.Log.Info("推理请求创建成功");

                modelConfig = config;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"加载模型失败: {config.ModelPath}", ex);
                throw;
            }
        }

        /// <summary>
        /// 执行推理预测
        /// </summary>
        /// <param name="inputs">输入数据张量</param>
        /// <returns>输出数据张量</returns>
        public DataTensor Predict(DataTensor inputs)
        {
            MyLogger.Log.Info("开始执行推理预测");
            try
            {
                // 设置输入数据
                for (int i = 0; i < inputs.Count; i++)
                {
                    Tensor inputTensor = inferRequests[0].get_input_tensor((ulong)i);
                    NodeData data = inputs[i];
                    if (modelConfig.DynamicInput) 
                    {
                        inputTensor.set_shape( new Shape(modelConfig.InputSizes[0]));
                    }
                    if (data.DataType == typeof(float))
                    {
                        inputTensor.set_data<float>(data.DataBuffer as float[]);
                        MyLogger.Log.Debug($"设置输入张量{i}数据: float[{string.Join(",", data.Shape)}]");
                    }
                    else if (data.DataType == typeof(int))
                    {
                        inputTensor.set_data<int>(data.DataBuffer as int[]);
                        MyLogger.Log.Debug($"设置输入张量{i}数据: int[{string.Join(",", data.Shape)}]");
                    }
                }

                // 执行推理
                MyLogger.Log.Debug("开始执行推理");
                inferRequests[0].infer();
                MyLogger.Log.Info("推理执行完成");

                // 处理输出结果
                DataTensor dataTensor = new DataTensor();
                if (modelConfig.DynamicOutput)
                {
                    modelConfig.OutputSizes.Clear();
                }
                for (int i = 0; i < outputNodeSize; i++)
                {
                    Tensor outputTensor = inferRequests[0].get_output_tensor((ulong)i);
                    var shape = outputTensor.get_shape().Select(x => (int)x).ToArray();

                    if (modelConfig.DynamicOutput)
                    {
                        modelConfig.OutputSizes.Add(shape);
                    }
                    if (outputTensor.get_element_type().get_type() == ElementType.F16 || outputTensor.get_element_type().get_type() == ElementType.F32)
                    {
                        float[] data = outputTensor.get_data<float>((int)outputTensor.get_size());
                        dataTensor.AddNode(modelConfig.OutputNames[i],
                            0,
                            TensorType.Output,
                            data,
                            shape,
                            typeof(float));
                        MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: float[{string.Join(",", shape)}]");
                    }
                    else if (outputTensor.get_element_type().get_type() == ElementType.I32)
                    {
                        int[] data = outputTensor.get_data<int>((int)outputTensor.get_size());
                        dataTensor.AddNode(modelConfig.OutputNames[i],
                            0,
                            TensorType.Output,
                            data,
                            shape,
                            typeof(int));
                        MyLogger.Log.Debug($"获取输出节点张量{i}: {modelConfig.OutputNames[0]},形状: int[{string.Join(",", shape)}]");
                    }
                    else if (outputTensor.get_element_type().get_type() == ElementType.BOOLEAN)
                    {
                        ulong a = outputTensor.get_size();
                        byte[] data = outputTensor.get_data<byte>((int)outputTensor.get_size());
                        dataTensor.AddNode(modelConfig.OutputNames[i],
                            0,
                            TensorType.Output,
                            data,
                            shape,
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
