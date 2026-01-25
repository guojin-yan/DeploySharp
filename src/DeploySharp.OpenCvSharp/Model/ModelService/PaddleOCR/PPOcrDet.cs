//using DeploySharp.Data;
//using DeploySharp.Log;
//using iTextSharp.text;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;

//namespace DeploySharp.Model
//{
//    public class PPOcrDet : IPPOcrDet
//    {
//        public PPOcrDet(IConfig config) : base(config)
//        {
//        }
//        public ObbResult[] Predict(Mat img)
//        {
//            return base.Predict(img) as ObbResult[];
//        }
//        public List<ObbResult[]> PredictBatch(List<Mat> imgs)
//        {
//            return base.PredictBatch(imgs.Cast<object>().ToList())
//                .Cast<ObbResult[]>()
//                .ToList();
//        }
//        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
//        {
//            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");
//            PPOcrDetConfig detConfig = (PPOcrDetConfig)config;
//            try
//            {
//                if (config.DynamicByInput)
//                {
//                    int maxSide = Math.Max(((Mat)img).Rows, ((Mat)img).Cols);
//                    // 2. 位运算取整到 32 的倍数
//                    // n & ~31 等同于将二进制的后 5 位清零，即结果是 32 的倍数
//                    int targetSize = maxSide & ~31;
//                    // 3. 限制最大值 (Min 语法糖)
//                    targetSize = Math.Min(targetSize, detConfig.LimitInputSize);
//                    // 4. 防止太小 (Min 语法糖)
//                    targetSize = Math.Max(targetSize, 32);

//                    config.InputSizes.Clear();
//                    config.InputSizes.Add(new int[] { 1, 3, targetSize, targetSize });
//                }


//                return CvDataProcessor.ImageProcessToDataTensor(
//                    (Mat)img,
//                    config,
//                    out imageAdjustmentParam);
//            }
//            catch (Exception ex)
//            {
//                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
//                throw;
//            }
//        }

//        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
//        {
//            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");
//            PPOcrDetConfig detConfig = (PPOcrDetConfig)config;
//            try
//            {

//                if (config.DynamicByInput)
//                {   // 1. 找出所有图片中最大的宽或高 (使用 LINQ Max)
//                    int maxSideInBatch = imgs.Max(img => Math.Max(((Mat)img).Rows, ((Mat)img).Cols));
//                    // 2. 计算目标尺寸逻辑
//                    int targetSize;
//                    if (maxSideInBatch > detConfig.LimitInputSize)
//                    {
//                        // 如果图片中有超过960的，目标设为960 (960是32的倍数)
//                        targetSize = detConfig.LimitInputSize;
//                    }
//                    else
//                    {
//                        // 如果都小于960，找出接近的32倍数
//                        // 向下取整： (800 / 32) * 32 = 768
//                        targetSize = (maxSideInBatch / 32) * 32;

//                        // 防御性编程：防止图片太小(如31x31)导致目标为0
//                        if (targetSize < 32) targetSize = 32;
//                    }
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

//        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
//        {
//            PPOcrDetConfig detConfig = (PPOcrDetConfig)config;
//            using (Mat pred_map = Mat.FromPixelData(dataTensor[0].Shape[2], dataTensor[0].Shape[3], MatType.CV_32FC1, dataTensor[0].DataBuffer))
//            {
//                // 3. 使用 OpenCV 原生操作进行阈值化和类型转换
//                // 逻辑：将 pred_map (float) 乘以 255，然后转为 8UC1，同时应用阈值。
//                // 这完全在 C++ 底层运行，极快。
//                Mat bit_map = new Mat();

//                // 将浮点概率图 (0.0-1.0) 转换为 (0-255)，并应用阈值
//                // m_det_db_thresh * 255: 将 0.5 的阈值转换为 127.5
//                Cv2.ConvertScaleAbs(pred_map, bit_map, 255.0, 0);       // float -> uchar (乘以 255)
//                Cv2.Threshold(bit_map, bit_map, detConfig.ConfidenceThreshold * 255, 255, ThresholdTypes.Binary);



//                // 4. 将处理好的 bit_map 和原始的 pred_map 传入后处理
//                // 注意：pred_map 没有做 "乘以255" 的操作，因为它本身就是概率分数 (0.0-1.0)，
//                // 后处理计算平均分时需要的是 0.0-1.0 的值，而不是 0-255。
//                // 这样我们就不需要创建那个转换后的 "大浮点数数组" 了。
//                //Stopwatch sw = Stopwatch.StartNew();
//                List<(OpenCvSharp.RotatedRect, float)> boxes = CvPPOcrDataProcessor.BoxesFromBitmap(pred_map, bit_map, detConfig.DBBoxThresh, 
//                    detConfig.DBUnclipRatio, detConfig.DBScoreMode);
//                //sw.Stop();
//                //Console.WriteLine($"PPOcrDet Postprocess BoxesFromBitmap time: {sw.ElapsedMilliseconds} ms");
//                List<ObbResult> ocrResults = new List<ObbResult>();

//                foreach (var r in boxes)
//                {
//                    ObbResult ocrResult = new ObbResult
//                    {
//                        Type = ResultType.OcrResult,
//                        Id = 0,
//                        Confidence = r.Item2,
//                        Bounds = imageAdjustmentParam.AdjustRotatedRect(CvDataExtensions.ToCvRotatedRect(r.Item1))
//                    };
//                    ocrResults.Add(ocrResult);

//                }
//                //boxes = PostProcessor.filter_tag_det_res(boxes, ratio_w, ratio_h, image);
//                //Cv2.ImShow("bit", bit_map);
//                //Cv2.WaitKey(0);
//                // bit_map 离开 using 块自动释放，无需手动 Dispose

//                return ocrResults.ToArray();
//            }
//        }
//    }
//}


using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DeploySharp.Model
{
    public class PPOcrDet : IPPOcrDet
    {
        private const int DefaultChannels = 3;
        private const int AlignBase = 32;

        public PPOcrDet(IConfig config) : base(config)
        {
            // 类型安全检查
            if (!(config is PPOcrDetConfig))
                throw new InvalidCastException($"PPOcrDet 必须使用 {nameof(PPOcrDetConfig)} 初始化");
        }

        public ObbResult[] Predict(Mat img)
        {
            // 基类调用
            return base.Predict(img) as ObbResult[];
        }

        public List<ObbResult[]> PredictBatch(List<Mat> imgs)
        {
            if (imgs == null || imgs.Count == 0)
                return new List<ObbResult[]>();

            // 优化：直接 Cast 避免类型检查开销，且不产生新内存（如果基类接受 List<object>）
            // 这里为了匹配基类签名，必须进行转换，但 Cast 比 OfType 快
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<ObbResult[]>()
                .ToList();
        }

        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            // 虽然可以调用 PreprocessBatch(new List<object>{img}...)，
            // 但 Det 单图预处理极其常见，为了避免 List 分配开销，保留单图优化路径

            var detConfig = (PPOcrDetConfig)config;
            var mat = (Mat)img;

            try
            {
                // 动态计算尺寸
                if (config.DynamicByInput)
                {
                    int targetSize = CalculateDetTargetSize(mat.Width, mat.Height, detConfig.LimitInputSize);
                    UpdateInputShape(1, targetSize);
                }

                return CvDataProcessor.ImageProcessToDataTensor(mat, config, out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = new ImageAdjustmentParam(); // 异常安全
                MyLogger.Log.Error($"PPOcrDet Preprocess Exception: {ex.Message}", ex);
                throw;
            }
        }

        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            var detConfig = (PPOcrDetConfig)config;
            try
            {
                int batchSize = imgs.Count;

                if (config.DynamicByInput)
                {
                    // 优化：LINQ Max 在少量数据时够快，大量数据时可考虑手动循环减少委托开销
                    // 这里为了代码清晰保留 LINQ，Cast<object> 到 Mat 的转换是引用转换，很快
                    int maxSideInBatch = imgs.Max(img => Math.Max(((Mat)img).Rows, ((Mat)img).Cols));

                    int targetSize = CalculateDetTargetSize(maxSideInBatch, maxSideInBatch, detConfig.LimitInputSize);
                    UpdateInputShape(batchSize, targetSize);
                }

                // 优化：使用 Cast 替代 OfType，因为我们确定类型，避免运行时类型检查
                // ToList 仍然会生成新的 List，因为基类需要 List<object>，这是不可避免的
                var matList = imgs.Cast<Mat>().ToList();

                return CvDataProcessor.ImageListProcessToDataTensor(matList, config, out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                imageAdjustmentParam = null;
                MyLogger.Log.Error($"PPOcrDet PreprocessBatch Exception: {ex.Message}", ex);
                throw;
            }
        }

        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            var detConfig = (PPOcrDetConfig)config;

            // 获取 Tensor 形状 [Batch, Channel, Height, Width]
            int h = dataTensor[0].Shape[2];
            int w = dataTensor[0].Shape[3];

            // 使用 using 确保 Mat 析构，防止底层内存泄漏
            using (Mat predMap = Mat.FromPixelData(h, w, MatType.CV_32FC1, dataTensor[0].DataBuffer))
            using (Mat bitMap = new Mat())
            {
                // 1. 将概率图 (0.0 - 1.0) 转换为灰度图 (0 - 255)
                // ConvertScaleAbs 是高性能操作，通常由 OpenCV 底层优化
                Cv2.ConvertScaleAbs(predMap, bitMap, 255.0, 0);

                // 2. 二值化 (应用阈值)
                double threshValue = detConfig.ConfidenceThreshold * 255.0;
                Cv2.Threshold(bitMap, bitMap, threshValue, 255, ThresholdTypes.Binary);

                // 3. 提取框
                // 注意：predMap 传入的是原始概率 (float)，用于计算框的平均分
                // bitMap 传入的是二值图 (byte)，用于确定轮廓位置
                var boxes = CvPPOcrDataProcessor.BoxesFromBitmap(
                    predMap,
                    bitMap,
                    detConfig.DBBoxThresh,
                    detConfig.DBUnclipRatio,
                    detConfig.DBScoreMode);

                var ocrResults = new List<ObbResult>(boxes.Count);

                foreach (var boxItem in boxes)
                {
                    // boxItem.Item1 是 RotatedRect，boxItem.Item2 是 Score
                    ocrResults.Add(new ObbResult
                    {
                        Type = ResultType.OcrResult,
                        Id = 0,
                        Confidence = boxItem.Item2,
                        Bounds = imageAdjustmentParam.AdjustRotatedRect(CvDataExtensions.ToCvRotatedRect(boxItem.Item1))
                    });
                }

                return ocrResults.ToArray();
            }
        }

        // --- 私有辅助方法 ---

        /// <summary>
        /// 计算检测模型的动态输入尺寸，需对齐到 32 的倍数
        /// </summary>
        private int CalculateDetTargetSize(int width, int height, int limitSize)
        {
            int maxSide = Math.Max(width, height);

            // 限制最大边长
            if (maxSide > limitSize)
            {
                return limitSize; // limitSize 通常也是 32 的倍数 (如 960)
            }

            // 向下取整到 32 的倍数 (位运算优化: x & ~31)
            // 等同于: (maxSide / 32) * 32
            int alignedSize = maxSide & ~(AlignBase-1);

            // 防御性检查：防止图片过小导致尺寸为 0
            return Math.Max(alignedSize, AlignBase);
        }

        /// <summary>
        /// 更新配置中的输入形状
        /// </summary>
        private void UpdateInputShape(int batchSize, int targetSize)
        {
            config.InputSizes.Clear();
            // NCHW 格式: [Batch, Channel, Height, Width]
            config.InputSizes.Add(new int[] { batchSize, DefaultChannels, targetSize, targetSize });
        }
    }
}
