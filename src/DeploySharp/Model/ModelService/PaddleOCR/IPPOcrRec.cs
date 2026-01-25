//using DeploySharp.Data;
//using DeploySharp.Log;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;

//namespace DeploySharp.Model
//{

//    public abstract class IPPOcrRec : IModel
//    {
//        public IPPOcrRec(IConfig config) : base(config)
//        {
//            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
//        }
//        public Result[] Predict(object img)
//        {
//            return base.Predict(img);
//        }


//        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
//        {

//            return PostprocessBatch(dataTensor, new ImageAdjustmentParam[] { imageAdjustmentParam })[0];
//        }

//        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
//        {
//            PPOcrRecConfig recConfig = (PPOcrRecConfig)config;
//            float[] results = dataTensor[0].DataBuffer as float[];
//            int oneResultSize = dataTensor[0].Shape[2];
//            int resultCount = dataTensor[0].Shape[1];
//            int oneBatchSize = oneResultSize * resultCount;
//            List<Result[]> textRecResults = new List<Result[]>();
//            for (int b = 0; b < recConfig.InferBatch; b++)
//            {
//                var resultChars = new (float maxVal, int maxIndex)[resultCount];
//                // 使用 Parallel.For 并行处理每一行
//                Parallel.For(0, resultCount, r =>
//                {
//                    float currentMax = float.MinValue;
//                    int currentMaxIndex = -1;
//                    int rowStartIndex = r * oneResultSize;
//                    for (int c = 0; c < oneResultSize; c++)
//                    {
//                        float val = results[rowStartIndex + c + b * oneBatchSize];
//                        if (val > currentMax)
//                        {
//                            currentMax = val;
//                            currentMaxIndex = c;
//                        }
//                    }

//                    // 并行写入结果数组的指定位置是线程安全的（因为每个线程写入不同的索引位）
//                    resultChars[r] = (currentMax, currentMaxIndex);
//                });

//                int last_index = resultChars[0].maxIndex;
//                float score = 0;
//                int count = 0;
//                string str_res = "";
//                for (int c = 1; c < resultCount; ++c)
//                {
//                    if (resultChars[c].maxIndex > 0 && (resultChars[c].maxIndex != last_index))
//                    {
//                        score += resultChars[c].maxVal;
//                        count += 1;
//                        str_res += recConfig.DictChars[resultChars[c].maxIndex];
//                    }
//                    last_index = resultChars[c].maxIndex;
//                }
//                if (count != 0)
//                {
//                    score /= count;
//                }

//                var recResult = new TextRecResult
//                {
//                    Text = str_res,
//                    Confidence = score
//                };
//                textRecResults.Add(new TextRecResult[] { recResult });
//            }
//            return textRecResults;
//        }
//    }
//}



using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IPPOcrRec : IModel
    {
        // 定义 CTC Blank 字符的索引，通常为 0
        private const int CTCBlankIndex = 0;

        public IPPOcrRec(IConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        public Result[] Predict(object img)
        {
            return base.Predict(img);
        }

        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            return PostprocessBatch(dataTensor, new ImageAdjustmentParam[] { imageAdjustmentParam })[0];
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            var recConfig = (PPOcrRecConfig)config;
            var results = new List<Result[]>();

            // 1. 数据安全获取

            float[] rawData = dataTensor[0].DataBuffer as float[];

            // 获取维度信息 [Batch, NumSteps, NumClasses] (PaddleOCR Rec通常是 T*N，这里代码逻辑暗示 Shape 是 [Batch, NumSteps, NumClasses])
            int batchSize = dataTensor[0].Shape[0]; // 通常是 recConfig.InferBatch
            int timeSteps = dataTensor[0].Shape[1]; // 代码中用的 resultCount
            int numClasses = dataTensor[0].Shape[2]; // 代码中用的 oneResultSize
            int stepSize = numClasses;
            int batchSizeStride = timeSteps * stepSize;

            for (int b = 0; b < batchSize; b++)
            {
                // 2. Argmax 计算 (移除 Parallel.For，改用普通循环，因为 T 通常很小，线程切换开销 > 计算开销)
                // 用于存储每个时间步的 argmax 结果
                var maxIndices = new int[timeSteps];
                var maxValues = new float[timeSteps];

                int batchOffset = b * batchSizeStride;

                for (int t = 0; t < timeSteps; t++)
                {
                    float maxVal = float.MinValue;
                    int maxIdx = -1;
                    int stepOffset = batchOffset + (t * stepSize);

                    // 手动展开或简单的循环查找最大值
                    for (int c = 0; c < numClasses; c++)
                    {
                        float val = rawData[stepOffset + c];
                        if (val > maxVal)
                        {
                            maxVal = val;
                            maxIdx = c;
                        }
                    }
                    maxIndices[t] = maxIdx;
                    maxValues[t] = maxVal;
                }

                // 3. CTC 解码与字符串构建
                // 使用 StringBuilder 优化字符串拼接性能
                StringBuilder sb = new StringBuilder();
                float scoreSum = 0;
                int validCharCount = 0;
                int lastIndex = -1;

                // 从第0个时间步开始遍历（原代码从1开始，建议从0开始防止丢失首字符，除非业务确定第一帧总是无意义的）
                for (int t = 0; t < timeSteps; t++)
                {
                    int charIndex = maxIndices[t];

                    // CTC 过滤逻辑：
                    // 1. 忽略 CTC_Blank (通常是 0)
                    // 2. 忽略重复字符 (如果当前字符 == 上一个字符)
                    if (charIndex > 0 && charIndex != lastIndex)
                    {
                        // 字符索引必须在字典范围内
                        if (charIndex < recConfig.DictChars.Count)
                        {
                            sb.Append(recConfig.DictChars[charIndex]);
                            scoreSum += maxValues[t];
                            validCharCount++;
                        }
                    }

                    // 更新上一次的有效字符索引
                    // 只有当当前字符不是 Blank 时，才更新 lastIndex，否则重复字符逻辑会出错
                    if (charIndex > 0)
                    {
                        lastIndex = charIndex;
                    }
                }

                float finalScore = validCharCount > 0 ? scoreSum / validCharCount : 0f;

                var recResult = new TextRecResult
                {
                    Text = sb.ToString(),
                    Confidence = finalScore
                };

                // 优化：避免 new TextRecResult[] { ... } 这种临时数组的创建
                // 如果接口允许，直接返回 List<TextRecResult> 会更好。
                // 这里为了兼容接口，必须包装。
                results.Add(new TextRecResult[] { recResult });
            }

            return results;
        }
    }
}
