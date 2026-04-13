using DeploySharp.Data;
using DeploySharp.Log;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeploySharp.Model
{

    public class BriaRmbgModel : IBriaRmbgModel
    {

        public BriaRmbgModel(BriaRmbgConfig config) : base(config) { }


        public SegResult[] Predict(Image<Rgb24> img)
        {
            return base.Predict(img) as SegResult[];
        }

        public List<SegResult[]> PredictBatch(List<Image<Rgb24>> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<SegResult[]>()
                .ToList();
        }

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
                    imgs.OfType<Image<Rgb24>>().ToList(),
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }


        public static Image MergeWithMask(Image colorImg, SegResult segResult)
        {
            // 1. 获取 Mask 数据
            byte[] maskBytes = segResult.ByteMask.GetRawByteData();
            int maskWidth = segResult.ByteMask.Width;
            int maskHeight = segResult.ByteMask.Height;
            // 2. 将 colorImg 克隆一份进行操作 (避免修改原图)
            // 注意：Image 是抽象类，必须转为泛型 Image<TPixel> 才能操作像素
            // 这里克隆为 Rgb24 格式，既能兼容大部分场景，又能节省内存（比 Argb32 少 1 字节）
            // 如果你的原图本身带透明通道且需要保留，请将 Rgb24 改为 Bgra32
            Image<Rgb24> resultImg = colorImg.CloneAs<Rgb24>();
            // 3. 构建 Mask 图像
            // 使用 L8 (8位灰度) 对应 byte[] 数据
            using (var maskImage = Image.LoadPixelData<L8>(maskBytes, maskWidth, maskHeight))
            {
                // 如果尺寸不一致，调整 Mask 大小以匹配原图
                if (resultImg.Width != maskWidth || resultImg.Height != maskHeight)
                {
                    // 使用 NearestNeighbor 插值防止二值图边缘模糊
                    maskImage.Mutate(x => x.Resize(resultImg.Width, resultImg.Height, KnownResamplers.NearestNeighbor));
                }
                // 执行合并逻辑
                ApplyMaskToImage(resultImg, maskImage);
            }
            return resultImg;
        }
        // 核心：通用的掩模处理方法
        private static void ApplyMaskToImage<TPixel>(Image<TPixel> resultImg, Image<L8> maskImg)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // 提前创建白色像素，避免在循环中重复创建
            TPixel whitePixel = Color.White.ToPixel<TPixel>();
            // 高性能遍历像素行
            resultImg.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    // 获取结果图的一行像素
                    Span<TPixel> pixelRow = accessor.GetRowSpan(y);

                    // 获取 Mask 的一行像素
                    Span<L8> maskRow = maskImg.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        // 如果 Mask 中的像素值是 0 (黑色)，则将该位置设为白色
                        if (maskRow[x].PackedValue == 0)
                        {
                            pixelRow[x] = whitePixel;
                        }
                        // 如果 Mask 是白色 (>0)，什么都不做，保留 resultImg 克隆时的原图像素
                    }
                }
            });
        }


    }
}
