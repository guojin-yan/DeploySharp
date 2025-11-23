using DeploySharp.Data;
using DeploySharp.Log;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{

    public class PPYoloeDetModel : IPPYoloeDetModel
    {
        public PPYoloeDetModel(IConfig config) : base(config)
        {
        }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Image<Rgb24>)?.Size()}");

            try
            {
                DataTensor dataTensors = CvDataProcessor.ImageProcessToDataTensor(
                    (Image<Rgb24>)img,
                    config,
                    out imageAdjustmentParam);

                float[] data = new float[config.InputSizes[1][1]];
                data[1] = (float)imageAdjustmentParam.Ratio.First;
                data[0] = (float)imageAdjustmentParam.Ratio.Second;
                dataTensors.AddNode(
                    config.InputNames[1],
                    0,
                    TensorType.Input,
                    data,
                    config.InputSizes[1],
                    typeof(float));
                return dataTensors;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }
    }
}
