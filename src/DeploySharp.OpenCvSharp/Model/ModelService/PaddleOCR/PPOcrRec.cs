//using DeploySharp.Data;
//using DeploySharp.Log;
//using DeploySharp.Model;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Linq;
//using System.Text;
//using static Org.BouncyCastle.Math.EC.ECCurve;

//namespace DeploySharp.Model
//{

//    public class PPOcrRec : IPPOcrRec
//    {
//        public PPOcrRec(IConfig config) : base(config)
//        {
//        }
//        public TextRecResult[] Predict(Mat img)
//        {
//            return base.Predict(img) as TextRecResult[];
//        }
//        public List<TextRecResult[]> PredictBatch(List<Mat> imgs)
//        {
//            return base.PredictBatch(imgs.Cast<object>().ToList())
//                .Cast<TextRecResult[]>()
//                .ToList();
//        }
//        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
//        {
//            ImageAdjustmentParam[] imageAdjustmentParams;
//             DataTensor data = PreprocessBatch(new List<object> { img }, out imageAdjustmentParams);
//            imageAdjustmentParam = imageAdjustmentParams[0];
//            return data;
//        }

//        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
//        {
//            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");
//            PPOcrRecConfig recConfig = (PPOcrRecConfig)config;
//            try
//            {
//                recConfig.InferBatch = imgs.Count;
//                if (recConfig.DynamicByInput)
//                {
//                    float max_wh_ratio = 0;
//                    foreach (var m in imgs)
//                    {
//                        int h = ((Mat)m).Rows;
//                        int w = ((Mat)m).Cols;
//                        float wh_ratio = (w * 1.0f) / h;
//                        max_wh_ratio = Math.Max(max_wh_ratio, wh_ratio);
//                    }
//                    int ww =( (max_wh_ratio * recConfig.InferImageHeight) > recConfig.MaxImageWidth ? recConfig.MaxImageWidth : (int)((max_wh_ratio * recConfig.InferImageHeight)));
//                    recConfig.InputSizes.Clear();
//                    recConfig.InputSizes.Add(new int[] { imgs.Count, 3, recConfig.InferImageHeight, ww  });
//                }



//                return CvDataProcessor.ImageListProcessToDataTensor(
//                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
//                    config,
//                    out imageAdjustmentParam);
//            }
//            catch (Exception ex)
//            {
//                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
//                throw;
//            }
//        }


//    }
//}


using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using OpenCvSharp;
using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;

namespace DeploySharp.Model
{
    public class PPOcrRec : IPPOcrRec
    {
        // 常量定义
        private const int DefaultImageChannels = 3;

        public PPOcrRec(IConfig config) : base(config)
        {
            // 预处理逻辑通常假设配置是 PPOcrRecConfig，如果类型不匹配尽早失败
            if (!(config is PPOcrRecConfig))
            {
                throw new InvalidCastException($"PPOcrRec 必须使用 {nameof(PPOcrRecConfig)} 初始化");
            }
        }

        // --- 1. 推理接口优化 (避免装箱) ---

        public TextRecResult[] Predict(Mat img)
        {
            // 避免传入 object 再转回 Mat，如果基类允许重载，建议修改基类签名。
            // 这里假设基类签名不可变，但内部处理尽可能高效。
            return base.Predict(img) as TextRecResult[];
        }

        public List<TextRecResult[]> PredictBatch(List<Mat> imgs)
        {
            if (imgs == null || imgs.Count == 0)
                return new List<TextRecResult[]>();

            var objectList = imgs.Cast<object>().ToList();

            var results = base.PredictBatch(objectList);

            // 优化：假设基类返回的 List<object> 里的元素确实是 TextRecResult[]，直接转换
            return results.Cast<TextRecResult[]>().ToList();
        }

        // --- 2. 预处理逻辑优化 ---

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            // 优化：单张图直接复用 Batch 逻辑，避免代码分裂，也统一了宽高比计算逻辑
            // 但为了极致性能，如果只有一张图，确实可以不用计算 List。
            // 考虑到 PaddleOCR Rec 主要是 Batch 推理，单张场景少，这里统一走 Batch 逻辑是可接受的。

            // 这里的 Cast 到 Mat 是安全的，因为 Predict 接口已经限制了输入类型
            var matList = new List<object>(1) { img };

            ImageAdjustmentParam[] paramsArray;
            var data = PreprocessBatch(matList, out paramsArray);

            imageAdjustmentParam = paramsArray[0];
            return data;
        }

        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            var recConfig = (PPOcrRecConfig)config;
            var sw = Stopwatch.StartNew();

            try
            {
                int batchSize = imgs.Count;
                recConfig.InferBatch = batchSize;

                // --- 动态尺寸计算 ---
                if (recConfig.DynamicByInput)
                {
                    float maxWhRatio = 0f;

                    // 优化：减少显式的类型转换次数
                    for (int i = 0; i < batchSize; i++)
                    {
                        var mat = (Mat)imgs[i];
                        if (mat.Empty()) continue; // 跳过空图
                        float ratio = mat.Width / (float)mat.Height;
                        if (ratio > maxWhRatio) maxWhRatio = ratio;
                    }

                    // 计算目标宽度
                    int targetWidth = (int)(maxWhRatio * recConfig.InferImageHeight);

                    // 限制最大宽度
                    if (targetWidth > recConfig.MaxImageWidth)
                    {
                        targetWidth = recConfig.MaxImageWidth;
                    }

                    // 更新配置中的输入尺寸 [Batch, Channel, Height, Width]
                    recConfig.InputSizes.Clear();
                    recConfig.InputSizes.Add(new int[] { batchSize, DefaultImageChannels, recConfig.InferImageHeight, targetWidth });


                    // 直接输入图片
                    //recConfig.InputSizes.Add(new int[] { batchSize, recConfig.InferImageHeight, targetWidth, DefaultImageChannels });
                }
                // --- End 动态尺寸计算 ---

                // 优化：避免 OfType<Mat>().ToList() 产生的额外迭代和分配
                // 直接在循环中转换或利用 LINQ 的 Cast (在已知类型情况下较快)
                var matList = imgs.Cast<Mat>().ToList();

                // 调用底层图像处理器
                var tensor = CvDataProcessor.ImageListProcessToDataTensor(
                    matList,
                    config,
                    out imageAdjustmentParam);

                sw.Stop();
                // 只有在 Debug 模式或采样模式下才打印日志，避免高频日志拖慢速度
                MyLogger.Log.Debug($"Rec Preprocess Finished. Batch: {batchSize}, Time: {sw.ElapsedMilliseconds}ms");


                return tensor;









                //List<byte[]> normalizedDatas = new List<byte[]>();
                //List<ImageAdjustmentParam> imageAdjustmentParamList = new List<ImageAdjustmentParam>();
                //int dataLength = 0;
                //for (int i = 0; i < imgs.Count; i++)
                //{
                //    var image = (Mat)imgs[i];

                //    Mat im = CvDataProcessor.Resize((Mat)imgs[i],
                //        new Data.Size(config.InputSizes[0][2], config.InputSizes[0][1]), ImageResizeMode.Stretch, InterpolationFlags.Linear);
                //    //byte[] data = im.ToBytes(); ;
                //    //im.GetArray(out data);


                //    //byte[] data = new byte[im.Total() * im.ElemSize()];
                //    //// 2. 获取数据指针
                //    //IntPtr ptr = im.Data;
                //    //// 3. 从非托管内存拷贝到托管数组
                //    //// mat.DataStart 到 mat.DataEnd 之间可能包含对齐填充，
                //    //// 但这里我们直接按计算出的 expectedSize 拷贝，确保没有多余字节。
                //    //Marshal.Copy(ptr, data, 0, data.Length);

                //    byte[] data = MatToBytesSafe(im);

                //    normalizedDatas.Add(data);
                //    dataLength += data.Length;
                //    imageAdjustmentParamList.Add(ImageAdjustmentParam.CreateFromImageInfo(
                //        new Data.Size(config.InputSizes[0][2], config.InputSizes[0][1]),
                //        CvDataExtensions.ToCvSize(image.Size()),
                //        ((IImgConfig)config).DataProcessor.ResizeMode));


                //    MyLogger.Log.Debug($"创建ImageAdjustmentParam完成，" +
                //         $"原始尺寸: {image.Size()}, " +
                //         $"目标尺寸: {config.InputSizes[0][2]}x{config.InputSizes[0][1]}, " +
                //         $"缩放模式: {((IImgConfig)config).DataProcessor.ResizeMode}");
                //}
                //List<byte> imageDatas = new List<byte>(dataLength);
                //foreach (var item in normalizedDatas)
                //{
                //    imageDatas.AddRange(item);
                //}


                //DataTensor dataTensors = new DataTensor();
                //dataTensors.AddNode(
                //    config.InputNames[0],
                //    0,
                //    TensorType.Input,
                //    imageDatas.ToArray(),
                //    config.InputSizes[0],
                //    typeof(byte));

                //MyLogger.Log.Debug($"DataTensor构造完成，输入名称: {config.InputNames[0]}, " +
                //                 $"数据类型: {typeof(float)}, " +
                //                 $"数据长度: {imageDatas.Count}");

                //imageAdjustmentParam = imageAdjustmentParamList.ToArray();
                //return dataTensors;
            }
            catch (Exception ex)
            {
                // 修正：确保在抛出异常前，out 参数有一个默认值，防止调用方访问未初始化内存
                imageAdjustmentParam = null;
                MyLogger.Log.Error($"PPOcrRec Preprocess Exception: {ex.Message}", ex);
                throw;
            }
        }

        public byte[] MatToBytesSafe(Mat mat)
        {
            int width = mat.Cols;
            int height = mat.Rows;
            int channels = mat.Channels();
            byte[] data = new byte[width * height * channels];
            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 获取像素的3个通道值
                    Vec3b color = mat.Get<Vec3b>(y, x);

                    data[index++] = color.Item0; // B
                    data[index++] = color.Item1; // G
                    data[index++] = color.Item2; // R
                }
            }
            return data;
        }
    }
}
