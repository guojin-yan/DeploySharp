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
    public abstract class IModel
    {
        protected readonly ILogger _logger;
        protected ModelConfig config;          // Model configuration/模型配置
        protected IModelInferEngine engine;    // Inference engine instance/推理引擎实例
        protected Pair<float, float> scales = new Pair<float, float>(); // Image scaling factors/图像缩放比例

        /// <summary>
        /// Constructor initializes model with configuration
        /// 使用配置初始化模型的构造函数
        /// </summary>
        protected IModel(ModelConfig config)
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

        /// <summary>
        /// Perform prediction on single input
        /// 单张输入预测方法
        /// </summary>
        public BaseResult Predict(object img)
        {

            try
            {
                var input = Preprocess(img);
                var output = engine.Predict(input);


                var result = Postprocess(output);


                return result;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error("[异常] 预测过程出错", ex);
                throw;
            }
        }

        /// <summary>
        /// Batch prediction for multiple inputs
        /// 批量输入预测方法
        /// </summary>
        public List<BaseResult> Predict(List<object> imgs)
        {

            var results = new List<BaseResult>();
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
        protected abstract DataTensor Preprocess(object img);

        /// <summary>
        /// Abstract method for output postprocessing
        /// 输出后处理的抽象方法
        /// </summary>
        protected abstract BaseResult Postprocess(DataTensor result);
    }

}
