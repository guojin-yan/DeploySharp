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
using DeploySharp.Log;

namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv7 model for object detection
    /// Inherits from base IModel interface
    /// </summary>
    public class Yolov7DetModel : IYolov7DetModel
    {
        /// <summary>
        /// Constructor initializes with model configuration
        /// </summary>
        /// <param name="config">Model configuration parameters</param>
        public Yolov7DetModel(Yolov7DetConfig config) : base(config) { }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Image<Rgb24>)?.Size()}");

            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    (Image<Rgb24>)img,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }
    }

}
