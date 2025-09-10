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
using System.Configuration;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Buffers;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv6 model for object detection
    /// Inherits from base IModel interface
    /// </summary>
    public class Yolov6DetModel : IYolov6DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov6DetModel(Yolov6DetConfig config) : base(config) { }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];
            var image = (Image<Rgb24>)img;
            // 归一化处理 (0-255 to 0-1)
            float[] normalizedData = CvDataProcessor.ProcessToFloat(image, new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]), ((YoloConfig)config).DataProcessor);

            imageAdjustmentParam = ImageAdjustmentParam.CreateFromImageInfo(
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(image.Size()),
                 ((YoloConfig)config).DataProcessor.ResizeMode);


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
    }

}
