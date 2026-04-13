using DeploySharp.Data;
using DeploySharp.Data.ResultData;
using DeploySharp.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IYoloClsModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv5 detector
        /// 初始化YOLOv5检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYoloClsModel(YoloClsConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        public ClsResult Predict(object img)
        {
            return (base.Predict(img) as ClsResult[])[0];
        }

        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// 对原始模型输出进行后处理以提取检测结果
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor/原始模型输出张量</param>
        /// <param name="imageAdjustmentParam">Image transformation parameters/图像变换参数</param>
        /// <returns>Array of processed detection results/处理后的检测结果数组</returns>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];

            var config = (YoloClsConfig)this.config;
            int oneResultLen = config.OutputSizes[0][1];
            // 使用结构体数组存储前10个结果
            // 初始化为最小值，确保第一个数据能进去
            var topItems = GetTop10(result0, 0, oneResultLen);

            ClsResult clsResult = new ClsResult();
            clsResult.SuspectedResults = new List<Result>();
            for (int i = 1; i < topItems.Length; i++)
            {
                int idx = topItems[i].Index;
                float confidence = topItems[i].Value;
                // 这里假设类别ID是通过索引计算得到的，具体计算方式根据模型输出格式调整
                int classID = idx % config.CategoryDict.Count; // 示例：根据索引映射到类别ID
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                clsResult.SuspectedResults.Add(new Result
                {
                    Id = classID,
                    Confidence = confidence,
                    Category = categoryFlag ? category : classID.ToString(),
                });
            }
            clsResult.Confidence = topItems[0].Value; // 最高置信度作为整体置信度
            clsResult.Id = topItems[0].Index % config.CategoryDict.Count; // 最高置信度对应的类别ID
            bool topCategoryFlag = config.CategoryDict.TryGetValue(clsResult.Id, out string topCategory);
            clsResult.Category = topCategoryFlag ? topCategory : clsResult.Id.ToString();
            return new ClsResult[] { clsResult};
        }
        /// <summary>
        /// 获取数组中指定范围内前10个最大值及其索引
        /// </summary>
        /// <param name="data">源数据数组</param>
        /// <param name="offset">起始位置</param>
        /// <param name="length">计算的长度</param>
        /// <returns>前10个最大值及其在数组中的真实索引</returns>
        public static (float Value, int Index)[] GetTop10(float[] data, int offset, int length)
        {
            int k = 10;
            var topItems = new (float Value, int Index)[k];

            // 1. 初始化堆
            for (int i = 0; i < k; i++)
            {
                topItems[i].Value = float.MinValue;
                topItems[i].Index = -1;
            }

            // 2. 边界检查 (建议保留，防止越界)
            if (data == null || data.Length == 0 || offset < 0 || length <= 0)
                return topItems;

            // 防止 length 超出数组实际范围
            int end = Math.Min(offset + length, data.Length);

            // 3. 遍历指定范围 [offset, end)
            // 注意：i 是真实索引，直接存入 topItems 中
            for (int i = offset; i < end; i++)
            {
                float val = data[i];

                // 如果当前值大于堆顶（前10名中最小值）
                if (val > topItems[0].Value)
                {
                    // 替换堆顶
                    topItems[0].Value = val;
                    topItems[0].Index = i; // 这里存储的是真实索引

                    // 下沉调整
                    int n = 0;
                    while (true)
                    {
                        int left = 2 * n + 1;
                        int right = 2 * n + 2;
                        int smallest = n;

                        // 找出父节点、左孩子、右孩子中最小的那个
                        if (left < k && topItems[left].Value < topItems[smallest].Value)
                            smallest = left;

                        if (right < k && topItems[right].Value < topItems[smallest].Value)
                            smallest = right;

                        if (smallest == n) break;

                        // 交换
                        var temp = topItems[n];
                        topItems[n] = topItems[smallest];
                        topItems[smallest] = temp;

                        n = smallest;
                    }
                }
            }

            // 4. 从大到小排序
            Array.Sort(topItems, (a, b) => b.Value.CompareTo(a.Value));

            return topItems;
        }
        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParam)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];

            var config = (Yolov5DetConfig)this.config;
            int oneResultLen = config.OutputSizes[0][1];
            int batchSize = config.InferBatch;
            int resultSizePerBatch = oneResultLen;

            Result[][] results = new Result[batchSize][];
            for (int i = 0; i < batchSize; i++)
            {
                // 计算当前批次的结果在 result0 中的起始位置
                int offset = i * resultSizePerBatch;
                // 获取当前批次的前10个结果
                var topItems = GetTop10(result0, offset, resultSizePerBatch);
                ClsResult clsResult = new ClsResult();
                clsResult.SuspectedResults = new List<Result>();
                for (int j = 1; j < topItems.Length; j++)
                {
                    int idx = topItems[j].Index;
                    float confidence = topItems[j].Value;
                    int classID = idx % config.CategoryDict.Count; // 示例：根据索引映射到类别ID
                    bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                    clsResult.SuspectedResults.Add(new Result
                    {
                        Id = classID,
                        Confidence = confidence,
                        Category = categoryFlag ? category : classID.ToString(),
                    });
                }
                clsResult.Confidence = topItems[0].Value; // 最高置信度作为整体置信度
                clsResult.Id = topItems[0].Index % config.CategoryDict.Count; // 最高置信度对应的类别ID
                bool topCategoryFlag = config.CategoryDict.TryGetValue(clsResult.Id, out string topCategory);
                clsResult.Category = topCategoryFlag ? topCategory : clsResult.Id.ToString();
                results[i] = new Result[] { clsResult };
            }

            return new List<Result[]>(results);
        }

    }


}