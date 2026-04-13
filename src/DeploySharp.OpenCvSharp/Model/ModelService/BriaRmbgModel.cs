using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeploySharp.Model
{

    public class BriaRmbgModel : IBriaRmbgModel
    {
        
        public BriaRmbgModel(BriaRmbgConfig config) : base(config) { }

    
        public SegResult[] Predict(Mat img)
        {
            return base.Predict(img) as SegResult[];
        }

        public List<SegResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<SegResult[]>()
                .ToList();
        }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");
            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    (Mat)img,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses batch of images for YOLOv5 detection inference.
        /// 对批量图像进行预处理以进行YOLOv5检测推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");

            try
            {
                return CvDataProcessor.ImageListProcessToDataTensor(
                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }


        public static Mat MergeWithMask(Mat colorImg, SegResult segResult)
        {
            Mat maskImg = segResult.ByteMask.ToMat();
            // 1. 安全校验：确保二值图是单通道
            if (maskImg.Channels() > 1)
            {
                Cv2.CvtColor(maskImg, maskImg, ColorConversionCodes.BGR2GRAY);
            }
            // 2. 安全校验：确保尺寸一致 (防止报错)
            if (colorImg.Size() != maskImg.Size())
            {
                Cv2.Resize(maskImg, maskImg, colorImg.Size());
            }
            // 3. 创建纯白背景
            // 颜色设为 255,255,255 (白色)
            Mat whiteBackground = new Mat(colorImg.Size(), colorImg.Type(), new Scalar(255, 255, 255));
            // 4. 核心步骤：复制
            // 只有 maskImg 中像素值 > 0 的地方，才会把 colorImg 的像素复制到 whiteBackground 上
            colorImg.CopyTo(whiteBackground, maskImg);
            return whiteBackground;
        }



    }
}
