//using DeploySharp.Data;
//using DeploySharp.Log;
//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace DeploySharp.Model
//{
//    public abstract class IPPOcrCls : IModel
//    {
//        public IPPOcrCls(IConfig config) : base(config)
//        {
//            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
//        }
//        public Result[] Predict(object img)
//        {
//            return base.Predict(img);
//        }


//        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
//        {
//            float[] data = dataTensor[0].DataBuffer as float[];
//            int lable = data[0] > data[1] ? 0 : 1;
//            return new Result[] { new Result { Id = lable, Confidence = data[lable] } };
//        }

//        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
//        {
//            float[] data = dataTensor[0].DataBuffer as float[];
//            PPOcrClsConfig clsConfig = (PPOcrClsConfig)config;
//            List<Result[]> results = new List<Result[]>();
//            for (int b = 0; b < clsConfig.InferBatch; b++) 
//            {
//                int offset = b * 2;
//                int lable = data[offset] > data[offset + 1] ? 0 : 1;
//                results.Add(new Result[] { new Result { Id = lable, Confidence = data[offset + lable] } });
//            }
//            return results;;
//        }
//    }
//}



using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Generic;

namespace DeploySharp.Model
{
    public abstract class IPPOcrCls : IModel
    {
        // 常量定义，避免魔法数字
        private const int ClassCount = 2;

        public IPPOcrCls(IConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        public Result[] Predict(object img)
        {
            return base.Predict(img);
        }

        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            // 单图预测可以复用 Batch 的逻辑，减少代码重复
            var batchResults = PostprocessBatch(dataTensor, new ImageAdjustmentParam[] { imageAdjustmentParam });
            return batchResults[0];
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            // 1. 安全获取数据
            // 假设 dataTensor[0] 是 [Batch, 2] 的 float 数组
            // 建议在基类或工具类中实现 GetData() 方法来处理非托管到托管的转换
            float[] data = dataTensor[0].DataBuffer as float[];

            var clsConfig = (PPOcrClsConfig)config;
            int batchSize = clsConfig.InferBatch;

            // 预分配 List 容量，避免动态扩容
            var results = new List<Result[]>(batchSize);

            // 2. 遍历 Batch
            for (int b = 0; b < batchSize; b++)
            {
                // 计算当前样本在数组中的起始索引
                // 假设 Tensor Layout 为 [Batch, ClassCount]，即 data 是连续的：[img0_cls0, img0_cls1, img1_cls0, img1_cls1, ...]
                int offset = b * ClassCount;

                // 获取两个类别的概率
                float probClass0 = data[offset];
                float probClass1 = data[offset + 1];

                // 比较大小决定标签 (0: 正常, 1: 需旋转)
                // 通常 PaddleOCR Cls 输出：index 0 代表正向，index 1 代表需要旋转 180 度
                int label = probClass0 > probClass1 ? 0 : 1;
                float confidence = (label == 0) ? probClass0 : probClass1;

                results.Add(new Result[] { new Result
                {
                    Id = label,
                    Confidence = confidence
                }});
            }

            return results;
        }
    }
}
