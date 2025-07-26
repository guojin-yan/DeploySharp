using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Runtime.InteropServices;

namespace DeploySharp.Model
{
    /// <summary>
    /// YOLOv8 segmentation model implementation for instance segmentation tasks
    /// </summary>
    public class Yolov8SegModel : IModel
    {
        // Stores original image dimensions (width, height)
        protected List<int> m_image_size = new List<int>();

        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov8SegModel(ModelConfig config) : base(config) { }

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
            Mat img1 = (Mat)img;
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
            float[] result0 = (float[])dataTensor[0].Buffer;

            Mat result1 = Mat.FromPixelData(dataTensor[1].Shape[1], dataTensor[1].Shape[2] * dataTensor[1].Shape[3], MatType.CV_32FC1, dataTensor[1].Buffer);

            // 初始化存储容器
            List<Rect> positionBoxes = new List<Rect>();  // 矩形框
            List<int> classIds = new List<int>();             // 类别ID
            List<float> confidences = new List<float>();      // 置信度
            List<Mat> masks = new List<Mat>();
       

            // Get output dimensions from config
            int outputSize = config.OutputSizes[0][2]; // Number of predictions (8400)
            int maskLen = dataTensor[1].Shape[1];
            int categNum = config.OutputSizes[0][1] - 4 - maskLen;// Number of classes
     

            // Parse model output (8400 predictions)
            for (int i = 0; i < outputSize; i++)
            {
                for (int j = 4; j < (categNum + 4); j++)  // Iterate through each class
                {
                    float conf = result0[outputSize * j + i];
                    int label = j - 4;
                    if (conf > 0.2)  // Confidence threshold filtering
                    {
                        // Parse center coordinates, width and height
                        float cx = result0[outputSize * 0 + i];
                        float cy = result0[outputSize * 1 + i];
                        float ow = result0[outputSize * 2 + i];
                        float oh = result0[outputSize * 3 + i];

                        // Convert to absolute coordinates
                        int x = (int)((cx - 0.5 * ow) * scales.First);
                        int y = (int)((cy - 0.5 * oh) * scales.First);
                        int width = (int)(ow * scales.First);
                        int height = (int)(oh * scales.First);
                        Rect box = new Rect(x, y, width, height);

                        // Store detection results
                        positionBoxes.Add(box);
                        classIds.Add(label);
                        confidences.Add(conf);
                        
                        float[] data = new float[maskLen];
                        for (int l = categNum + 4; l < config.OutputSizes[0][1]; ++l) 
                        {
                            data[l- categNum-4] = result0[outputSize * l + i];
                        }
                        masks.Add(Mat.FromPixelData(1, dataTensor[1].Shape[1], MatType.CV_32FC1, data));
                    }
                }
            }

            // 执行非极大值抑制（NMS）
            int[] indexes = new int[positionBoxes.Count];
            CvDnn.NMSBoxes(positionBoxes, confidences, config.ConfidenceThreshold, config.NmsThreshold, out indexes);

            SegResult result = new SegResult();
            Mat rgb_mask = Mat.Zeros(new Size(m_image_size[0], m_image_size[1]), MatType.CV_8UC3);
            Random rd = new Random(); // Generate Random Numbers

            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i];
                // Division scope
                Rect box = positionBoxes[index];
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
                Cv2.Resize(mask_roi, actual_maskm, new Size(box_x2 - box_x1, box_y2 - box_y1));
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
                Mat mask = Mat.Zeros(new Size(m_image_size[0], m_image_size[1]), MatType.CV_8UC1);
                bin_mask = new Mat(bin_mask, new OpenCvSharp.Range(0, box_y2 - box_y1), new OpenCvSharp.Range(0, box_x2 - box_x1));
                Rect roi1 = new Rect(box_x1, box_y1, box_x2 - box_x1, box_y2 - box_y1);
                bin_mask.CopyTo(new Mat(mask, roi1));
                // Color segmentation area
                Cv2.Add(rgb_mask, new Scalar(rd.Next(0, 255), rd.Next(0, 255), rd.Next(0, 255)), rgb_mask, mask);
                result.Add(classIds[index], confidences[index], positionBoxes[index], rgb_mask.Clone());
            }
            return result;

        }
    }

}
