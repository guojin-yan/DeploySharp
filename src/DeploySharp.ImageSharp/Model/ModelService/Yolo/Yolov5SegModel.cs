using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Model;
using DeploySharp.Data;
using DeploySharp.ImageSharp.Data;
using Size = DeploySharp.Data.Size;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv5 model for object detection
    /// Inherits from base IModel interface
    /// </summary>
    public class Yolov5SegModel : IYolov5SegModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov5SegModel(Yolov5SegConfig config) : base(config) { }


        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];
            var image = (Image<Rgb24>)img;

            // 使用ImageSharp进行letterbox处理
            Image<Rgb24> processedImage = CvDataProcessor.Resize(image, new Size(config.InputSizes[0][2], config.InputSizes[0][3]), ((Yolov5SegConfig)config).ImgResizeMode);

            // 归一化处理 (0-255 to 0-1)
            float[] normalizedData = CvDataProcessor.Normalize(processedImage, true);


            imageAdjustmentParam = ImageAdjustmentParam.CreateFromImageInfo(
                new Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(image.Size()),
                ((Yolov5DetConfig)config).ImgResizeMode);

         

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
