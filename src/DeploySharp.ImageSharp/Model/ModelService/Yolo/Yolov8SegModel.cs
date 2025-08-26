using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Model;
using DeploySharp.Data;


namespace DeploySharp.Model
{

    public class Yolov8SegModel : IYolov8SegModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov8SegModel(Yolov8SegConfig config) : base(config) { }


        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            int inputSize = config.InputSizes[0][2];
            var image = (Image<Rgb24>)img;

            // 使用ImageSharp进行letterbox处理
            Image<Rgb24> processedImage = CvDataProcessor.Resize(image, new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]), ((Yolov8SegConfig)config).ImgResizeMode);

            // 归一化处理 (0-255 to 0-1)
            float[] normalizedData = CvDataProcessor.Normalize(processedImage, true);


            imageAdjustmentParam = ImageAdjustmentParam.CreateFromImageInfo(
                new Data.Size(config.InputSizes[0][2], config.InputSizes[0][3]),
                CvDataExtensions.ToCvSize(image.Size()),
                ((Yolov8SegConfig)config).ImgResizeMode);

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

