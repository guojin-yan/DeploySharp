using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Collections.Concurrent;
using System.Numerics;
using System.Diagnostics;
using System.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv5 model for object detection
    /// Inherits from base IModel interface
    /// </summary>
    public class Yolov5Model : IModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov5Model(ModelConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public DetResult Predict(Image<Rgb24> img)
        {
            return base.Predict(img) as DetResult;
        }

        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor</param>
        /// <returns>Processed detection results</returns>
        protected override BaseResult Postprocess(DataTensor dataTensor)
        {
            float[] result = (float[])dataTensor[0].Buffer;
            List<SixLabors.ImageSharp.Rectangle> positionBoxes = new List<SixLabors.ImageSharp.Rectangle>();
            List<int> classIds = new List<int>();
            List<float> confidences = new List<float>();

            int outputSize = config.OutputSizes[0][1];
            int outputSize1 = config.OutputSizes[0][2];

            for (int i = 0; i < outputSize; i++)
            {
                float conf = result[outputSize1 * i + 4];
                if (conf > 0.25)
                {
                    for (int j = 5; j < outputSize1; j++)
                    {
                        float conf1 = result[outputSize1 * i + j];
                        int label = j - 5;
                        if (conf1 > 0.2)
                        {
                            float cx = result[outputSize1 * i];
                            float cy = result[outputSize1 * i + 1];
                            float ow = result[outputSize1 * i + 2];
                            float oh = result[outputSize1 * i + 3];

                            int x = (int)((cx - 0.5 * ow) * scales.First);
                            int y = (int)((cy - 0.5 * oh) * scales.First);
                            int width = (int)(ow * scales.First);
                            int height = (int)(oh * scales.First);

                            positionBoxes.Add(new SixLabors.ImageSharp.Rectangle(x, y, width, height));
                            classIds.Add(label);
                            confidences.Add(conf1);
                        }
                    }
                }
            }

            // 模拟NMS (OpenCV的NMSBoxes需要自己实现)
            var indexes = NonMaximumSuppression(positionBoxes, confidences,
                config.ConfidenceThreshold, config.NmsThreshold);

            DetResult results = new DetResult();
            foreach (int index in indexes)
            {
                results.Add(classIds[index], confidences[index], CvDataExtensions.ToCvRect( positionBoxes[index]));
            }

            return results;
        }


        /// <summary>
        /// Preprocesses input image for model inference
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Processed tensor ready for model input</returns>
        protected override DataTensor Preprocess(object img)
        {
            int inputSize = config.InputSizes[0][2];
            var image = (Image<Rgb24>)img;

            // 使用ImageSharp进行letterbox处理
            Image<Rgb24> processedImage = Resize.LetterboxImg(image, inputSize, out scales.First);
            scales.Second = scales.First;

            // 归一化处理 (0-255 to 0-1)
            float[] normalizedData = Normalize.Run(processedImage, true);


            DataTensor dataTensors = new DataTensor();
            dataTensors.AddNode(
                config.InputNames[0],
                0,
                TensorType.Input,
                normalizedData,
                config.InputSizes[0],
                typeof(float));

            return dataTensors;
        }

        /// <summary>
        /// 实现非极大值抑制算法 (NMS)
        /// </summary>
        /// <param name="boxes">检测框列表</param>
        /// <param name="scores">置信度分数列表</param>
        /// <param name="scoreThreshold">置信度阈值</param>
        /// <param name="nmsThreshold">NMS重叠阈值</param>
        /// <returns>保留的检测框索引列表</returns>
        private List<int> NonMaximumSuppression(
            IList<SixLabors.ImageSharp.Rectangle> boxes,
            IList<float> scores,
            float scoreThreshold,
            float nmsThreshold)
        {
            // 筛选出高于置信度阈值的框
            var indices = Enumerable.Range(0, boxes.Count)
                .Where(i => scores[i] >= scoreThreshold)
                .ToList();

            // 按分数降序排序
            indices.Sort((a, b) => scores[b].CompareTo(scores[a]));

            var selectedIndices = new List<int>();

            while (indices.Count > 0)
            {
                // 选取当前分数最高的框
                int current = indices[0];
                selectedIndices.Add(current);

                // 计算与剩余框的IoU(交并比)
                for (int i = indices.Count - 1; i >= 1; i--)
                {
                    float iou = CalculateIoU(boxes[current], boxes[indices[i]]);

                    // 移除IoU超过阈值的框
                    if (iou > nmsThreshold)
                    {
                        indices.RemoveAt(i);
                    }
                }

                // 移除当前框
                indices.RemoveAt(0);
            }

            return selectedIndices;
        }

        /// <summary>
        /// 计算两个矩形的IoU(交并比)
        /// </summary>
        /// <param name="a">矩形A</param>
        /// <param name="b">矩形B</param>
        /// <returns>IoU值(0-1)</returns>
        private float CalculateIoU(SixLabors.ImageSharp.Rectangle a, SixLabors.ImageSharp.Rectangle b)
        {
            // 计算交集区域
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);

            // 计算交集面积
            int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);

            // 计算并集面积
            int areaA = a.Width * a.Height;
            int areaB = b.Width * b.Height;
            int unionArea = areaA + areaB - intersectionArea;

            // 避免除以零
            return unionArea > 0 ? (float)intersectionArea / unionArea : 0f;
        }
    }

}
