using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using RotatedRect = OpenCvSharp.RotatedRect;

namespace DeploySharp.Model
{
    public class Yolov8ObbModel : IModel
    {
        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov8ObbModel(ModelConfig config) : base(config) { }

        /// <summary>
        /// Main prediction method that processes input image
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results containing bounding boxes, class IDs and confidence scores</returns>
        public ObbResult Predict(Mat img)
        {
            return base.Predict(img) as ObbResult;
        }

        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// </summary>
        /// <param name="dataTensor">Raw output tensor from model</param>
        /// <returns>Processed detection results</returns>
        protected override BaseResult Postprocess(DataTensor dataTensor)
        {
            // Get raw output data from model
            float[] result = (float[])dataTensor[0].Buffer;

            // 初始化存储容器
            List<RotatedRect> positionBoxes = new List<RotatedRect>();  // 矩形框
            List<int> classIds = new List<int>();             // 类别ID
            List<float> confidences = new List<float>();      // 置信度
            List<float> rotations = new List<float>();        // 旋转角度
                                                              // Get output dimensions from config
            int outputSize = config.OutputSizes[0][2]; // Number of predictions (8400)
            int categNum = config.OutputSizes[0][1] - 5;// Number of classes
            // 解析模型输出（8400个预测框）
            for (int i = 0; i < outputSize; i++)
            {
                for (int j = 4; j < (categNum + 4); j++)  // 遍历每个类别
                {
                    float conf = result[outputSize * j + i];
                    int label = j - 4;
                    if (conf > 0.2)  // 置信度阈值过滤
                    {
                        // 解析中心点坐标、宽高和旋转角度
                        float cx = result[outputSize * 0 + i];
                        float cy = result[outputSize * 1 + i];
                        float ow = result[outputSize * 2 + i];
                        float oh = result[outputSize * 3 + i];
                        float rotation = result[outputSize * (categNum + 4) + i];

                        // 创建旋转矩形框（考虑预处理时的缩放）
                        RotatedRect box = new RotatedRect(
                            new Point2f(cx * scales.First, cy * scales.First),
                            new Size2f(ow * scales.First, oh * scales.First),
                            (float)(rotation * 180.0 / Math.PI));

                        // 存储检测结果
                        positionBoxes.Add(box);
                        classIds.Add(label);
                        confidences.Add(conf);
                        rotations.Add(rotation);
                    }
                }
            }

            // 执行非极大值抑制（NMS）
            int[] indexes = new int[positionBoxes.Count];
            CvDnn.NMSBoxes(positionBoxes, confidences, 0.5f, 0.3f, out indexes);


            // Package final results
            ObbResult results = new ObbResult();
            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i];
                results.Add(classIds[index], confidences[index], CvDataExtensions.ToCvRotatedRect(positionBoxes[index]));
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
            int inputSize = config.InputSizes[0][2]; ;  // Model input size

            // Convert color space (BGR→RGB)
            Mat mat = new Mat();
            Cv2.CvtColor(img as Mat, mat, ColorConversionCodes.BGR2RGB);

            // Resize image with letterbox (maintain aspect ratio)
            Mat mat1 = Resize.LetterboxImg(mat, inputSize, out scales.First);
            scales.Second = scales.First;

            // Normalize and permute image data
            mat = Normalize.Run(mat1, true);
            float[] array = Permute.Run(mat1);

            // Create input tensor
            DataTensor dataTensors = new DataTensor();
            dataTensors.AddNode(config.InputNames[0],
                0,
                TensorType.Input,
                array,
                config.InputSizes[0],
                typeof(float));

            return dataTensors;
        }
    }
}
