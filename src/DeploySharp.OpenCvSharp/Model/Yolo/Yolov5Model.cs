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
        public DetResult Predict(Mat img)
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
            // Get raw output buffer from tensor
            float[] result = (float[])dataTensor[0].Buffer;

            // Initialize containers for detection components
            List<Rect> positionBoxes = new List<Rect>();  // Bounding boxes
            List<int> classIds = new List<int>();         // Class IDs
            List<float> confidences = new List<float>();  // Confidence scores

            // Get output dimensions from config
            int outputSize = config.OutputSizes[0][1]; // Number of predictions (8400)
            int outputSize1 = config.OutputSizes[0][2];// Elements per prediction (class count + 5)


            // Parse each prediction in model output
            for (int i = 0; i < outputSize; i++)
            {
                float conf = result[outputSize1 * i + 4]; // Objectness score
                if (conf > 0.25)  // Filter by objectness threshold
                {
                    // Check each class probability
                    for (int j = 5; j < outputSize1; j++)
                    {
                        float conf1 = result[outputSize1 * i + j]; // Class confidence
                        int label = j - 5;                         // Class index
                        if (conf1 > 0.2)  // Filter by class confidence threshold
                        {
                            // Decode box coordinates (cx,cy,width,height format)
                            float cx = result[outputSize1 * i + 0]; // Center x
                            float cy = result[outputSize1 * i + 1]; // Center y
                            float ow = result[outputSize1 * i + 2]; // Width
                            float oh = result[outputSize1 * i + 3]; // Height

                            // Convert to absolute coordinates
                            int x = (int)((cx - 0.5 * ow) * scales.First);
                            int y = (int)((cy - 0.5 * oh) * scales.First);
                            int width = (int)(ow * scales.First);
                            int height = (int)(oh * scales.First);
                            Rect box = new Rect(x, y, width, height);

                            // Store detection components
                            positionBoxes.Add(box);
                            classIds.Add(label);
                            confidences.Add(conf1);
                        }
                    }
                }
            }

            // Apply Non-Maximum Suppression to filter overlapping boxes
            int[] indexes = new int[positionBoxes.Count];
            CvDnn.NMSBoxes(positionBoxes, confidences,
                          config.ConfidenceThreshold,
                          config.NmsThreshold,
                          out indexes);

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
            int inputSize = config.InputSizes[0][2]; ; // Model input dimension

            // Convert BGR to RGB color space
            Mat mat = new Mat();
            Cv2.CvtColor(img as Mat, mat, ColorConversionCodes.BGR2RGB);

            // Resize with letterboxing (maintain aspect ratio)
            Mat mat1 = Resize.LetterboxImg(mat, inputSize, out scales.First);
            scales.Second = scales.First;

            // Normalize pixel values (0-255 to 0-1)
            mat = Normalize.Run(mat1, true);

            // Convert HWC to CHW format (OpenCV to PyTorch format)
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
