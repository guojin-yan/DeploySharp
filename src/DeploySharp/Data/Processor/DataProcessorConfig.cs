using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class DataProcessorConfig
    {
        public DataProcessorConfig() { }
        public DataProcessorConfig(ImageResizeMode resizeMode, ImageNormalizationType normalizationType, NormalizationParams normalizationParams = null)
        {
            NormalizationType = normalizationType;
            ResizeMode = resizeMode;
        }
        public ImageNormalizationType NormalizationType { get; set; } = ImageNormalizationType.None;
        public NormalizationParams CustomNormalizationParams { get; set; } = null;
        public ImageResizeMode ResizeMode { get; set; } = ImageResizeMode.Stretch;

        //public float[] ProcessToFloat(object input, Size size, NormalizationParams customParams = null)
        //{
        //    return NormalizeToFloat(Resize(input, size), customParams);
        //}
        //protected virtual float[] NormalizeToFloat(object image, NormalizationParams customParams = null) 
        //{
        //    return null;
        //}
        //protected virtual object Resize(object img, Size size)
        //{
        //    return null;
        //}
    }
}
