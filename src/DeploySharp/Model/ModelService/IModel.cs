using DeploySharp.Common;
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base class for AI model inference
    /// AI模型推理的抽象基类
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides common infrastructure for model loading, inference and lifecycle management.
    /// 提供模型加载、推理和生命周期管理的公共基础设施。
    /// </para>
    /// <para>
    /// Key responsibilities:
    /// 主要职责:
    /// - Model initialization and configuration
    ///   模型初始化和配置
    /// - Inference pipeline execution
    ///   推理流程执行
    /// - Performance profiling
    ///   性能分析
    /// - Resource management
    ///   资源管理
    /// </para>
    /// </remarks>
    public abstract class IModel : IDisposable
    {
        /// <summary>
        /// Model configuration parameters
        /// 模型配置参数
        /// </summary>
        protected IConfig config;

        /// <summary>
        /// Inference engine instance
        /// 推理引擎实例
        /// </summary>
        protected IModelInferEngine engine;

        /// <summary>
        /// Performance profiler recording timing metrics
        /// 记录时间指标的性能分析器
        /// </summary>
        /// <value>
        /// Profiler collecting preprocessing, inference and postprocessing times
        /// 收集预处理、推理和后处理时间的分析器
        /// </value>
        public ModelInferenceProfiler ModelInferenceProfiler { get; } = new ModelInferenceProfiler();

        /// <summary>
        /// Timer measuring different prediction phases
        /// 测量不同预测阶段的计时器
        /// </summary>
        protected PredictorTimer predictorTimer = new PredictorTimer();

        /// <summary>
        /// Initializes model with specified configuration
        /// 使用指定配置初始化模型
        /// </summary>
        /// <param name="config">
        /// Model configuration parameters
        /// 模型配置参数
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when config is null
        /// 当config为null时抛出
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when model loading fails
        /// 当模型加载失败时抛出
        /// </exception>
        protected IModel(IConfig config)
        {
            MyLogger.Log.Info("[Initialization] Starting model instance creation");
            MyLogger.Log.Info("[初始化] 开始创建模型实例");

            try
            {
                this.config = config ?? throw new ArgumentNullException(nameof(config));
                engine = InferEngineFactory.Create(config.TargetInferenceBackend);
                engine.LoadModel(ref this.config);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("[Error] Model initialization failed", ex);
                MyLogger.Log.Error("[错误] 模型初始化失败", ex);
                throw;
            }
        }

        /// <summary>
        /// Performs prediction on single input
        /// 对单个输入执行预测
        /// </summary>
        /// <param name="img">
        /// Input data (typically image)
        /// 输入数据(通常是图像)
        /// </param>
        /// <returns>
        /// Array of detection/prediction results
        /// 检测/预测结果数组
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input is null
        /// 当输入为null时抛出
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when prediction fails
        /// 当预测失败时抛出
        /// </exception>
        public Result[] Predict(object img)
        {
            if (img == null)
            {
                throw new ArgumentNullException(nameof(img));
            }

            try
            {
                // Execute prediction pipeline stages
                // 执行预测流程的各个阶段
                predictorTimer.StartPreprocess();
                var input = Preprocess(img, out var imageAdjustmentParam);

                predictorTimer.StartInference();
                var output = engine.Predict(input);

                predictorTimer.StartPostprocess();
                var result = Postprocess(output, imageAdjustmentParam);

                ModelInferenceProfiler.Record(predictorTimer.Stop());
                return result;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("[Exception] Prediction process failed", ex);
                MyLogger.Log.Error("[异常] 预测过程出错", ex);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously performs prediction on single input
        /// 异步执行单个输入预测
        /// </summary>
        /// <param name="img">
        /// Input data (typically image)
        /// 输入数据(通常是图像)
        /// </param>
        /// <returns>
        /// Task containing array of detection results
        /// 包含检测结果数组的任务
        /// </returns>
        public Task<Result[]> PredictAsync(object img)
        {
            return Task.Run(() => Predict(img));
        }

        /// <summary>
        /// Performs batch prediction on multiple inputs
        /// 对多个输入执行批量预测
        /// </summary>
        /// <param name="imgs">
        /// List of input data items
        /// 输入数据项列表
        /// </param>
        /// <returns>
        /// List of result arrays corresponding to each input
        /// 对应每个输入的结果数组列表
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input list is null
        /// </exception>
        public List<Result[]> Predict(List<object> imgs)
        {
            if (imgs == null)
            {
                throw new ArgumentNullException(nameof(imgs));
            }

            var results = new List<Result[]>();
            var timer = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < imgs.Count; i++)
            {
                try
                {
                    results.Add(Predict(imgs[i]));
                }
                catch (Exception ex)
                {
                    MyLogger.Log.Error($"[Batch Prediction] Failed processing item {i}", ex);
                    MyLogger.Log.Error($"[批量预测] 处理项 {i} 失败", ex);
                    throw;
                }
            }

            MyLogger.Log.Info($"[Batch Prediction] Completed {imgs.Count} items in {timer.ElapsedMilliseconds}ms");
            MyLogger.Log.Info($"[批量预测] 完成 {imgs.Count} 项，耗时 {timer.ElapsedMilliseconds}ms");
            return results;
        }

        /// <summary>
        /// Abstract method for input preprocessing
        /// 输入预处理的抽象方法
        /// </summary>
        /// <param name="img">
        /// Input data to preprocess
        /// 要预处理的输入数据
        /// </param>
        /// <param name="imageAdjustmentParam">
        /// Output parameters for image adjustments
        /// 图像调整的输出参数
        /// </param>
        /// <returns>
        /// Processed tensor ready for inference
        /// 准备好用于推理的处理后张量
        /// </returns>
        protected abstract DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam);

        /// <summary>
        /// Abstract method for output postprocessing
        /// 输出后处理的抽象方法
        /// </summary>
        /// <param name="result">
        /// Raw inference result tensor
        /// 原始推理结果张量
        /// </param>
        /// <param name="imageAdjustmentParam">
        /// Image adjustment parameters from preprocessing
        /// 来自预处理的图像调整参数
        /// </param>
        /// <returns>
        /// Processed detection/prediction results
        /// 处理后的检测/预测结果
        /// </returns>
        protected abstract Result[] Postprocess(DataTensor result, ImageAdjustmentParam imageAdjustmentParam);

        /// <summary>
        /// Releases model resources
        /// 释放模型资源
        /// </summary>
        public void Dispose()
        {
            engine?.Dispose();
            GC.SuppressFinalize(this);
        }
    }


}
