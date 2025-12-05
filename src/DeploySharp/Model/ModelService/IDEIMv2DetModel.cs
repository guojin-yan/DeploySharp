using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IDEIMv2DetModel : IModel
    {
        public IDEIMv2DetModel(IConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }
        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
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
            var config = (DEIMv2DetConfig)this.config;
            float[] resultScores = dataTensor[2].DataBuffer as float[];
            long[] resultLables = dataTensor[0].DataBuffer as long[];
            float[] resultBoxes = dataTensor[1].DataBuffer as float[];
            // 获取有效检测（分数高于阈值）
            List<DetResult> detResults = new List<DetResult>();
            for (int i = 0; i < resultScores.Length; i++)
            {
                if (resultScores[i] < config.ConfidenceThreshold)
                {
                    continue;
                }
                int s = 4 * i;
                // Parse bounding box coordinates (x1,y1,x2,y2 format)
                // 解析边界框坐标(x1,y1,x2,y2格式)
                float cx = resultBoxes[s + 0];
                float cy = resultBoxes[s + 1];
                float dx = resultBoxes[s + 2];
                float dy = resultBoxes[s + 3];

                // Convert to width/height format
                // 转换为宽/高格式
                int width = (int)((dx - cx));
                int height = (int)((dy - cy));

                Rect box = new Rect
                {
                    X = (int)cx,
                    Y = (int)cy,
                    Width = width,
                    Height = height
                };
                int classID = (int)resultLables[i];
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                // Create detection result with adjusted coordinates
                // 创建带有调整坐标的检测结果
                detResults.Add(new DetResult
                {
                    Id = classID,                               // Class ID/类别ID
                    Bounds = (box), // Adjusted rectangle/调整后的矩形
                    Confidence = resultScores[i],                // Detection confidence/检测置信度
                    Category = categoryFlag ? category : classID.ToString() // Fallback to ID if category not found/如果类别不存在则回退到ID
                });
            }
            return detResults.ToArray();
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            var config = (DEIMv2DetConfig)this.config;
            float[] resultScores = dataTensor[2].DataBuffer as float[];
            long[] resultLables = dataTensor[0].DataBuffer as long[];
            float[] resultBoxes = dataTensor[1].DataBuffer as float[];
            int batchSize = config.InferBatch;

            Result[][] results = new Result[batchSize][];

            for (var b = 0; b < batchSize; b++)
            {
                // 获取有效检测（分数高于阈值）
                List<DetResult> detResults = new List<DetResult>();
                for (int i = 0; i < resultScores.Length / batchSize; i++)
                {
                    if (resultScores[i + 300 * b] < config.ConfidenceThreshold)
                    {
                        continue;
                    }
                    int s = 4 * i;
                    // Parse bounding box coordinates (x1,y1,x2,y2 format)
                    // 解析边界框坐标(x1,y1,x2,y2格式)
                    float cx = resultBoxes[s + 0 + 4 * 300 * b];
                    float cy = resultBoxes[s + 1 + 4 * 300 * b];
                    float dx = resultBoxes[s + 2 + 4 * 300 * b];
                    float dy = resultBoxes[s + 3 + 4 * 300 * b];

                    // Convert to width/height format
                    // 转换为宽/高格式
                    int width = (int)((dx - cx));
                    int height = (int)((dy - cy));

                    Rect box = new Rect
                    {
                        X = (int)cx,
                        Y = (int)cy,
                        Width = width,
                        Height = height
                    };
                    int classID = (int)resultLables[i +  300 * b];
                    bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                    // Create detection result with adjusted coordinates
                    // 创建带有调整坐标的检测结果
                    detResults.Add(new DetResult
                    {
                        Id = classID,                               // Class ID/类别ID
                        Bounds = (box), // Adjusted rectangle/调整后的矩形
                        Confidence = resultScores[i + 300 * b],                // Detection confidence/检测置信度
                        Category = categoryFlag ? category : classID.ToString() // Fallback to ID if category not found/如果类别不存在则回退到ID
                    });
                }

                results[b] = detResults.ToArray();

            }
            return new List<Result[]>(results);
        }

    }
}
