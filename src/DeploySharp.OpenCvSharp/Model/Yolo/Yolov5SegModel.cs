using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DeploySharp.Model
{
    /// <summary>
    /// YOLOv5 segmentation model implementation for instance segmentation tasks
    /// </summary>
    public class Yolov5SegModel : IModel
    {
        // Stores original image dimensions (width, height)
        protected List<int> m_image_size = new List<int>();

        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov5SegModel(ModelConfig config) : base(config) { }

        /// <summary>
        /// Main prediction method that processes input image
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Segmentation results containing masks and detection info</returns>
        public SegResult Predict(Mat img)
        {
            return base.Predict(img) as SegResult;
        }

        /// <summary>
        /// Sigmoid activation function implementation
        /// </summary>
        /// <param name="a">Input value</param>
        /// <returns>Sigmoid-activated output between 0 and 1</returns>
        protected static float Sigmoid(float a)
        {
            float b = 1.0f / (1.0f + (float)Math.Exp(-a));
            return b;
        }

        /// <summary>
        /// Preprocesses input image for model inference
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Processed tensor ready for model input</returns>
        protected override DataTensor Preprocess(object img)
        {
            Mat img1 = img as Mat;
            // Store original image dimensions
            m_image_size = new List<int> { (int)img1.Size().Width, (int)img1.Size().Height };

            // Get model input size from configuration
            int inputSize = config.InputSizes[0][2];

            // Create temporary Mat and convert color space (BGR→RGB)
            Mat mat = new Mat();
            Cv2.CvtColor(img1, mat, ColorConversionCodes.BGR2RGB);

            // Resize image with letterbox (maintain aspect ratio)
            Mat mat1 = Resize.LetterboxImg(mat, inputSize, out scales.First);
            scales.Second = scales.First;

            // Normalize pixel values and permute dimensions
            mat = Normalize.Run(mat1, true);
            float[] array = Permute.Run(mat1);

            // Create input tensor with proper shape and type
            DataTensor dataTensors = new DataTensor();
            dataTensors.AddNode(config.InputNames[0],
                0,
                TensorType.Input,
                array,
                config.InputSizes[0],
                typeof(float));

            return dataTensors;
        }
        protected override BaseResult Postprocess(DataTensor dataTensor)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            float[] result0 = (float[])dataTensor[0].Buffer;

            Mat result1 = Mat.FromPixelData(dataTensor[1].Shape[1], dataTensor[1].Shape[2] * dataTensor[1].Shape[3], MatType.CV_32FC1, dataTensor[1].Buffer);

            // 初始化存储容器
            List<OpenCvSharp.Rect> positionBoxes = new List<OpenCvSharp.Rect>();  // 矩形框
            List<int> classIds = new List<int>();             // 类别ID
            List<float> confidences = new List<float>();      // 置信度
            List<Mat> masks = new List<Mat>();
            int maskLen = dataTensor[1].Shape[1];
            int outputSize = config.OutputSizes[0][1]; // 25200
            int outputSize1 = config.OutputSizes[0][2];  // 117
            // 解析模型输出（8400个预测框）
            for (int i = 0; i < outputSize; i++)
            {
                float conf = result0[outputSize1 * i + 4];
                if (conf > 0.25)  // 置信度阈值过滤
                {
                    for (int j = 5; j < outputSize1 - maskLen; j++)  // 遍历每个类别
                    {
                        float conf1 = result0[outputSize1 * i + j];
                        int label = j - 5;
                        if (conf1 > 0.2)  // 置信度阈值过滤
                        {
                            // 解析中心点坐标、宽高和旋转角度
                            float cx = result0[outputSize1 * i + 0];
                            float cy = result0[outputSize1 * i + 1];
                            float ow = result0[outputSize1 * i + 2];
                            float oh = result0[outputSize1 * i + 3];
                            int x = (int)((cx - 0.5 * ow) * scales.First);
                            int y = (int)((cy - 0.5 * oh) * scales.First);
                            int width = (int)(ow * scales.First);
                            int height = (int)(oh * scales.First);
                            OpenCvSharp.Rect box = new OpenCvSharp.Rect(x, y, width, height);

                            // 存储检测结果
                            positionBoxes.Add(box);
                            classIds.Add(label);
                            confidences.Add(conf1);
                            masks.Add(Mat.FromPixelData(1, maskLen, MatType.CV_32FC1, Marshal.UnsafeAddrOfPinnedArrayElement(result0, (outputSize1 * i + outputSize1 - maskLen))));
                        }
                    }
                }

            }

            // 执行非极大值抑制（NMS）
            int[] indexes = new int[positionBoxes.Count];
            CvDnn.NMSBoxes(positionBoxes, confidences, config.ConfidenceThreshold, config.NmsThreshold, out indexes);

            SegResult result = new SegResult();
            Mat rgb_mask = Mat.Zeros(new OpenCvSharp.Size(m_image_size[0], m_image_size[1]), MatType.CV_8UC3);
            Random rd = new Random(); // Generate Random Numbers

            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i];
                // Division scope
                OpenCvSharp.Rect box = positionBoxes[index];
                int box_x1 = Math.Max(0, box.X);
                int box_y1 = Math.Max(0, box.Y);
                int box_x2 = Math.Max(0, box.BottomRight.X);
                int box_y2 = Math.Max(0, box.BottomRight.Y);

                // Segmentation results
                Mat original_mask = masks[index] * result1;
                for (int col = 0; col < original_mask.Cols; col++)
                {
                    original_mask.Set<float>(0, col, Sigmoid(original_mask.At<float>(0, col)));
                }
                // 1x25600 -> 160x160 Convert to original size
                Mat reshape_mask = original_mask.Reshape(1, dataTensor[1].Shape[2]);

                //Console.WriteLine("m1.size = {0}", m1.Size());

                // Split size after scaling
                int mx1 = Math.Max(0, (int)((box_x1 / scales.First) * 0.25));
                int mx2 = Math.Min(dataTensor[1].Shape[2], (int)((box_x2 / scales.First) * 0.25));
                int my1 = Math.Max(0, (int)((box_y1 / scales.First) * 0.25));
                int my2 = Math.Min(dataTensor[1].Shape[2], (int)((box_y2 / scales.First) * 0.25));
                // Crop Split Region
                Mat mask_roi = new Mat(reshape_mask, new OpenCvSharp.Range(my1, my2), new OpenCvSharp.Range(mx1, mx2));
                // Convert the segmented area to the actual size of the image
                Mat actual_maskm = new Mat();
                Cv2.Resize(mask_roi, actual_maskm, new OpenCvSharp.Size(box_x2 - box_x1, box_y2 - box_y1));
                // Binary segmentation region
                for (int r = 0; r < actual_maskm.Rows; r++)
                {
                    for (int c = 0; c < actual_maskm.Cols; c++)
                    {
                        float pv = actual_maskm.At<float>(r, c);
                        if (pv > 0.5)
                        {
                            actual_maskm.Set<float>(r, c, 1.0f);
                        }
                        else
                        {
                            actual_maskm.Set<float>(r, c, 0.0f);
                        }
                    }
                }

                // 预测
                Mat bin_mask = new Mat();
                actual_maskm = actual_maskm * 200;
                actual_maskm.ConvertTo(bin_mask, MatType.CV_8UC1);
                if ((box_y1 + bin_mask.Rows) >= m_image_size[1])
                {
                    box_y2 = m_image_size[1] - 1;
                }
                if ((box_x1 + bin_mask.Cols) >= m_image_size[0])
                {
                    box_x2 = m_image_size[0] - 1;
                }
                // Obtain segmentation area
                Mat mask = Mat.Zeros(new OpenCvSharp.Size(m_image_size[0], m_image_size[1]), MatType.CV_8UC1);
                bin_mask = new Mat(bin_mask, new OpenCvSharp.Range(0, box_y2 - box_y1), new OpenCvSharp.Range(0, box_x2 - box_x1));
                OpenCvSharp.Rect roi1 = new OpenCvSharp.Rect(box_x1, box_y1, box_x2 - box_x1, box_y2 - box_y1);
                bin_mask.CopyTo(new Mat(mask, roi1));
                // Color segmentation area
                Cv2.Add(rgb_mask, new Scalar(rd.Next(0, 255), rd.Next(0, 255), rd.Next(0, 255)), rgb_mask, mask);
                result.Add(classIds[index], confidences[index], CvDataExtensions.ToCvRect(positionBoxes[index]), CvDataExtensions.ToImageDataB(rgb_mask.Clone()));
            }

            stopwatch.Stop();
            Console.WriteLine("时间为： " + stopwatch.ElapsedMilliseconds);
            return result;

        }
    }

}
