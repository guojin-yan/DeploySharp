using DeploySharp.Data;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static OpenCvSharp.FileStorage;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv8 model for object detection
    /// </summary>
    public class Yolov8Model : IModel
    {
        /// <summary>
        /// Constructor that initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov8Model(ModelConfig config) : base(config) { }

        /// <summary>
        /// Main prediction method that processes input image
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results containing bounding boxes, class IDs and confidence scores</returns>
        public DetResult Predict(Mat img)
        {
            return base.Predict(img) as DetResult;
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

            // Initialize containers for detection results
            List<Rect> positionBoxes = new List<Rect>();  // Bounding box coordinates
            List<int> classIds = new List<int>();         // Detected class IDs
            List<float> confidences = new List<float>();  // Confidence scores

            // Get output dimensions from config
            int outputSize = config.OutputSizes[0][2]; // Number of predictions (8400)
            int categNum = config.OutputSizes[0][1] - 4;// Number of classes
   

            // Parse model output (8400 predictions)
            for (int i = 0; i < outputSize; i++)
            {
                for (int j = 4; j < (categNum + 4); j++)  // Iterate through each class
                {
                    float conf = result[outputSize * j + i];
                    int label = j - 4;
                    if (conf > 0.2)  // Confidence threshold filtering
                    {
                        // Parse center coordinates, width and height
                        float cx = result[outputSize * 0 + i];
                        float cy = result[outputSize * 1 + i];
                        float ow = result[outputSize * 2 + i];
                        float oh = result[outputSize * 3 + i];

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
                    }
                }
            }

            // Apply Non-Maximum Suppression (NMS) to filter overlapping boxes
            int[] indexes = new int[positionBoxes.Count];
            CvDnn.NMSBoxes(positionBoxes, confidences, config.ConfidenceThreshold, config.NmsThreshold, out indexes);

            // Package final results
            DetResult results = new DetResult();
            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i];
                results.Add(classIds[index], confidences[index], positionBoxes[index]);
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
