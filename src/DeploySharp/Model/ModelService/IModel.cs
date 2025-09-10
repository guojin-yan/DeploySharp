using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base class for AI model inference
    /// 模型推理的抽象基类
    /// </summary>
    public abstract class IModel : IDisposable
    {
        protected IConfig config;          // Model configuration/模型配置
        protected IModelInferEngine engine;    // Inference engine instance/推理引擎实例

        /// <summary>
        /// Constructor initializes model with configuration
        /// 使用配置初始化模型的构造函数
        /// </summary>
        protected IModel(IConfig config)
        {
            MyLogger.Log.Info("[初始化] 开始创建模型实例");

            try
            {
                this.config = config;
                engine = InferEngineFactory.Create(config.TargetInferenceBackend);
                engine.LoadModel(ref this.config);

            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("[错误] 模型初始化失败", ex);
                throw;
            }
        }

        //public ResultSet<Result> Predict(object img) 
        //{
        //    try
        //    {
        //        var input = Preprocess(img, out var imageAdjustmentParam);
        //        var output = engine.Predict(input);


        //        var result = Postprocess(output, imageAdjustmentParam);


        //        return new ResultSet<Result>(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        MyLogger.Log.Error("[异常] 预测过程出错", ex);
        //        throw;
        //    }
        //}

        /// <summary>
        /// Perform prediction on single input
        /// 单张输入预测方法
        /// </summary>
        public Result[] Predict(object img)
        {

            try
            {
                var input = Preprocess(img, out var imageAdjustmentParam);
                var output = engine.Predict(input);
                var result = Postprocess(output, imageAdjustmentParam);
                return result;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("[异常] 预测过程出错", ex);
                throw;
            }
        }

        public Task<Result[]> PredictAsync(object img)
        {
            return Task.Run(() => Predict(img));
        }

        /// <summary>
        /// Batch prediction for multiple inputs
        /// 批量输入预测方法
        /// </summary>
        public List<Result[]> Predict(List<object> imgs)
        {

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

                    throw;
                }
            }
            return results;
        }

        /// <summary>
        /// Abstract method for input preprocessing
        /// 输入预处理的抽象方法
        /// </summary>
        protected abstract DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam);

        /// <summary>
        /// Abstract method for output postprocessing
        /// 输出后处理的抽象方法
        /// </summary>
        protected abstract Result[] Postprocess(DataTensor result, ImageAdjustmentParam imageAdjustmentParam);

        public void Dispose()
        {
            engine.Dispose();
        }
    }

}
