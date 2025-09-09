using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class IAnomalibSegModel : IModel
    {
        public IAnomalibSegModel(AnomalibConfig config) : base(config) { }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format</param>
        /// <returns>Detection results container</returns>
        public SegResult[] Predict(object img)
        {
            return base.Predict(img) as SegResult[];
        }
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];
            
            float[] mask = new float[result0.Length];
            var config = (AnomalibConfig)this.config;
            float d = config.MetaData.MaxValue - config.MetaData.MinValue;
            float confidenceThreshold = (config.MetaData.PixelThreshold - config.MetaData.MinValue) / d;
            float maxScore = result0.Max();
            if (maxScore < config.MetaData.ImageThreshold) 
            {
                return null;
            }
            float score = (maxScore - config.MetaData.MinValue) / d;
            for (int i = 0; i < result0.Length; ++i)
            {
                result0[i] = ((result0[i] - config.MetaData.MinValue) / d );
                mask[i] = result0[i] > confidenceThreshold ? 1 : 0;
            }

            var results = new AnomalySegResult[1];
            results[0] = new AnomalySegResult
            {
                RawMask = new ImageDataF(result0, dataTensor[0].Shape[2], dataTensor[0].Shape[3], 1, ImageDataF.DataFormat.CHW),
                Mask = new ImageDataF(mask, dataTensor[0].Shape[2], dataTensor[0].Shape[3], 1, ImageDataF.DataFormat.CHW),
                Confidence = score,
                Id = 0,
                Category = "anomaly",
            };

            //new AnomalySegResult
            //{
            //    Mask = new ImageDataF(targetMask, bounds.Width, bounds.Height, 1, ImageDataF.DataFormat.CHW),
            //    Score = score,
            //};

            //Mat dataMat = Mat.FromPixelData(224, 224, MatType.CV_32FC1, result0);
            //// 3. 归一化到0-255范围
            //Mat normalized = new Mat();
            //Cv2.Normalize(dataMat, normalized, 0, 255, NormTypes.MinMax);

            //// 4. 转换为8位无符号整型
            //Mat heatmap = new Mat();
            //normalized.ConvertTo(heatmap, MatType.CV_8UC1);

            //// 5. 应用JET色图
            //Mat colorHeatmap = new Mat();
            //Cv2.ApplyColorMap(heatmap, colorHeatmap, ColormapTypes.Jet);

            //// 6. 显示和保存结果
            //Cv2.ImShow("Heatmap", colorHeatmap);
            //Cv2.WaitKey();


            //var config = (AnomalibConfig)this.config;
            //float d = config.MetaData.MaxValue - config.MetaData.MinValue;
            //float c = (config.MetaData.PixelThreshold - config.MetaData.MinValue) / d;
            //for (int i = 0; i < result0.Length; ++i)
            //{
            //    result0[i] = ((result0[i] - config.MetaData.MinValue) / d > c ? 1 : 0);
            //}


            //Mat mat = Mat.FromPixelData(224, 224, MatType.CV_32FC1, result0);
            //Cv2.ImShow("mat", mat);
            //Cv2.WaitKey(0);

            //int rowResultNum = config.OutputSizes[0][1];
            //int oneResultLen = config.OutputSizes[0][2];
            return results;
        }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            throw new NotImplementedException();
        }
    }
}
