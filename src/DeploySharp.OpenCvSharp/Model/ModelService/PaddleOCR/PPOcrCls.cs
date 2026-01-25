//using DeploySharp.Data;
//using DeploySharp.Log;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;

//namespace DeploySharp.Model
//{
//    public class PPOcrCls : IPPOcrCls
//    {
//        public PPOcrCls(IConfig config) : base(config)
//        {
//        }
//        public Result[] Predict(Mat img)
//        {
//            return base.Predict(img) as Result[];
//        }
//        public List<Result[]> PredictBatch(List<Mat> imgs)
//        {
//            return base.PredictBatch(imgs.Cast<object>().ToList())
//                .Cast<Result[]>()
//                .ToList();
//        }
//        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
//        {
//            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");
//            PPOcrClsConfig detConfig = (PPOcrClsConfig)config;
//            try
//            {
//                return CvDataProcessor.ImageProcessToDataTensor(
//                    (Mat)img,
//                    config,
//                    out imageAdjustmentParam);
//            }
//            catch (Exception ex)
//            {
//                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
//                throw;
//            }
//        }

//        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
//        {
//            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");
//            PPOcrClsConfig clsConfig = (PPOcrClsConfig)config;
//            try
//            {
//                clsConfig.InferBatch = imgs.Count;
//                return CvDataProcessor.ImageListProcessToDataTensor(
//                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
//                    config,
//                    out imageAdjustmentParam);
//            }
//            catch (Exception ex)
//            {
//                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
//                throw;
//            }
//        }


//    }
//}



using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeploySharp.Model
{
    public class PPOcrCls : IPPOcrCls
    {
        public PPOcrCls(IConfig config) : base(config)
        {
            // 类型安全检查：确保传入的是 Cls 专用的配置
            if (!(config is PPOcrClsConfig))
            {
                throw new InvalidCastException($"PPOcrCls 必须使用 {nameof(PPOcrClsConfig)} 初始化");
            }
        }

        /// <summary>
        /// 单张图预测
        /// </summary>
        public Result[] Predict(Mat img)
        {
            if (img == null || img.Empty())
                return Array.Empty<Result>(); // 返回空数组而非 null，符合 C# 最佳实践

            return base.Predict(img) as Result[];
        }

        /// <summary>
        /// 批量预测
        /// </summary>
        public List<Result[]> PredictBatch(List<Mat> imgs)
        {
            if (imgs == null || imgs.Count == 0)
            {
                // 返回空列表，防止后续调用 foreach 报错
                return new List<Result[]>();
            }

            // 优化：使用 Cast<object>() 进行引用转换
            // 比 imgs.Cast<object>().ToList() 稍微慢一点点因为要多一次类型推断，但为了类型安全必须转
            // 如果基类签名能改为 List<Mat> 性能会更好，但在无法修改基类的前提下，这是最高效的写法
            var objectList = imgs.Cast<object>().ToList();

            var results = base.PredictBatch(objectList);

            // 假设结果非空
            return results.Cast<Result[]>().ToList();
        }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            // 移除了 Debug 日志，减少高频 I/O 开销
            // 如果确实需要调试，建议使用 ConditionalAttribute 或采样日志

            var mat = (Mat)img;
            try
            {
                // Cls 模型通常不需要动态调整尺寸，模型内部 config 已规定好尺寸 (如 48x192)
                return CvDataProcessor.ImageProcessToDataTensor(
                    mat,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = new ImageAdjustmentParam(); // 异常安全：确保 out 参数有值
                MyLogger.Log.Error($"PPOcrCls Preprocess 异常: {ex.Message}", ex);
                throw;
            }
        }

        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            var clsConfig = (PPOcrClsConfig)config;
            try
            {
                clsConfig.InferBatch = imgs.Count;

                // 性能优化：使用 Cast 替代 OfType
                // OfType 会过滤掉非 Mat 元素并产生新列表
                // Cast 只是强转引用，因为我们确定输入全是 Mat，所以 Cast 更快且无内存浪费
                var matList = imgs.Cast<Mat>().ToList();

                return CvDataProcessor.ImageListProcessToDataTensor(
                    matList,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = null; // 异常安全
                MyLogger.Log.Error($"PPOcrCls PreprocessBatch 异常: {ex.Message}", ex);
                throw;
            }
        }
    }
}

