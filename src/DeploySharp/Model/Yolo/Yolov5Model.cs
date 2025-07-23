using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;

namespace DeploySharp.Model
{
    public class Yolov5Model : IModel
    {
        public Yolov5Model(ModelConfig config) : base(config) { }
        protected override BaseResult Postprocess(DataTensor dataTensor)
        {
            float[] result = (float[])dataTensor[0].Buffer;
            // 初始化存储容器
            List<Rect> positionBoxes = new List<Rect>();  // 矩形框
            List<int> classIds = new List<int>();             // 类别ID
            List<float> confidences = new List<float>();      // 置信度

            int outputSize = config.OutputSizes[0][1];
            int outputSize1 = config.OutputSizes[0][2];
            // 解析模型输出（8400个预测框）
            for (int i = 0; i < outputSize; i++)
            {
                float conf = result[outputSize1 * i + 4];
                if (conf > 0.25)  // 置信度阈值过滤
                {
                    for (int j = 5; j < outputSize1; j++)  // 遍历每个类别
                    {
                        float conf1 = result[outputSize1 * i + j];
                        int label = j - 5;
                        if (conf1 > 0.2)  // 置信度阈值过滤
                        {
                            // 解析中心点坐标、宽高和旋转角度
                            float cx = result[outputSize1 * i + 0];
                            float cy = result[outputSize1 * i + 1];
                            float ow = result[outputSize1 * i + 2];
                            float oh = result[outputSize1 * i + 3];
                            int x = (int)((cx - 0.5 * ow) * scales.X);
                            int y = (int)((cy - 0.5 * oh) * scales.X);
                            int width = (int)(ow * scales.X);
                            int height = (int)(oh * scales.X);
                            Rect box = new Rect(x, y, width, height);

                            // 存储检测结果
                            positionBoxes.Add(box);
                            classIds.Add(label);
                            confidences.Add(conf1);
                        }
                    }
                }
                   
            }

            // 执行非极大值抑制（NMS）
            int[] indexes = new int[positionBoxes.Count];
            CvDnn.NMSBoxes(positionBoxes, confidences, config.ConfidenceThreshold, config.NmsThreshold, out indexes);

            // 封装最终结果
            DetResult results = new DetResult();
            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i];

                results.Add(classIds[index], confidences[index], positionBoxes[index]);
            }
            return results;

        }

        protected override DataTensor Preprocess(Mat img)
        {
            int inputSize = config.InputSizes[0][2];
            // 创建临时Mat对象并转换颜色空间（BGR→RGB）
            Mat mat = new Mat();
            Cv2.CvtColor(img, mat, ColorConversionCodes.BGR2RGB);

            Mat mat1 = Resize.LetterboxImg(mat, inputSize, out scales.X);
            scales.Y = scales.X;
            mat = Normalize.Run(mat1, true);
            float[] array = Permute.Run(mat1);
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
