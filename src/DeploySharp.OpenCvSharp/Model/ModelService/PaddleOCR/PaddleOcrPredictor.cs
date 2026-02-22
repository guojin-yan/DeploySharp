using DeploySharp.Common;
using DeploySharp.Data;
using DeploySharp.Log;
using DeploySharp.Model;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Main PaddleOCR predictor class responsible for orchestrating the complete OCR pipeline
    /// (detection, classification, recognition).
    /// PaddleOCR预测器主类，负责编排完整的OCR流程(检测、分类、识别)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optimized solution: Uses "task partitioning" strategy, strictly dividing inference tasks
    /// into N parts, each corresponding to an independent inference thread and device.
    /// This completely solves device load imbalance issues caused by thread ID hash conflicts.
    /// 优化方案：采用 "任务分区" 策略，将推理任务列表严格切分为 N 份，每份对应一个独立的推理线程和设备。
    /// 从而彻底解决线程 ID 哈希冲突导致的设备负载不均问题。
    /// </para>
    /// <para>
    /// The OCR pipeline consists of three stages:
    /// OCR流程包含三个阶段：
    /// 1. Text Detection (DBNet) - Finds text regions in the image
    ///    文本检测(DBNet) - 在图像中找到文本区域
    /// 2. Text Direction Classification (Optional) - Corrects rotated text
    ///    文本方向分类(可选) - 校正旋转的文本
    /// 3. Text Recognition (CRNN) - Converts text regions to characters
    ///    文本识别(CRNN) - 将文本区域转换为字符
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Initialize PaddleOCR with all three stages
    /// // 初始化包含三个阶段的PaddleOCR
    /// var config = new PaddleOCRConfig
    /// {
    ///     UseDet = true,   // Enable detection / 启用检测
    ///     UseCls = true,   // Enable classification / 启用分类
    ///     UseRec = true,   // Enable recognition / 启用识别
    ///     DetConfig = new PPOcrDetConfig("ch_PP-OCRv4_det.onnx"),
    ///     ClsConfig = new PPOcrClsConfig("ch_ppocr_mobile_v2.0_cls.onnx"),
    ///     RecConfig = new PPOcrRecConfig("ch_PP-OCRv4_rec.onnx")
    /// };
    /// 
    /// using (var ocr = new PaddleOcrPredictor(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("document.jpg"))
    ///     {
    ///         // Run full OCR pipeline
    ///         // 运行完整OCR流程
    ///         OcrResult result = ocr.Predict(image);
    ///         
    ///         foreach (var text in result.TextContents)
    ///         {
    ///             Console.WriteLine($"Text: {text.Text}, Score: {text.Confidence}");
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public class PaddleOcrPredictor : IDisposable
    {
        private readonly PaddleOCRConfig _config;
        private readonly int _maxConcurrency;

        // Predictor instances
        private PPOcrDet _ocrDetPredictor;
        private PPOcrCls[] _ocrClsPredictors;
        private PPOcrRec[] _ocrRecPredictors;

        /// <summary>
        /// Creates a new PaddleOCR predictor with specified configuration.
        /// 使用指定配置创建新的PaddleOCR预测器。
        /// </summary>
        /// <param name="config">OCR configuration object / OCR配置对象</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="InvalidOperationException">Thrown when configuration validation fails / 当配置验证失败时抛出</exception>
        /// <remarks>
        /// <para>
        /// The constructor initializes predictor instances based on configuration settings.
        /// 构造函数根据配置设置初始化预测器实例。
        /// </para>
        /// <para>
        /// Each enabled stage (detection, classification, recognition) creates independent
        /// predictor instances for thread-safe parallel processing.
        /// 每个启用的阶段(检测、分类、识别)创建独立的预测器实例以实现线程安全的并行处理。
        /// </para>
        /// </remarks>
        public PaddleOcrPredictor(PaddleOCRConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.Validate();

            // Determine max concurrency based on GPU count or CPU core count
            _maxConcurrency = config.MaxConcurrency;

            // 1. Initialize detection
            if (_config.UseDet) _ocrDetPredictor = new PPOcrDet(_config.DetConfig);

            // 2. Initialize classifier instance pool (each instance binds to a thread/device)
            if (_config.UseCls)
            {
                _ocrClsPredictors = new PPOcrCls[_maxConcurrency];
                for (int i = 0; i < _maxConcurrency; i++)
                {
                    string configJson = JsonSerializer.Serialize(_config.ClsConfig);
                    PPOcrClsConfig independentConfig = JsonSerializer.Deserialize<PPOcrClsConfig>(configJson);
                    _ocrClsPredictors[i] = new PPOcrCls(independentConfig);
                }
            }

            // 3. Initialize recognizer instance pool
            if (_config.UseRec)
            {
                _ocrRecPredictors = new PPOcrRec[_maxConcurrency];
                for (int i = 0; i < _maxConcurrency; i++)
                {
                    string configJson = JsonSerializer.Serialize(_config.RecConfig);
                    PPOcrRecConfig independentConfig = JsonSerializer.Deserialize<PPOcrRecConfig>(configJson);
                    _ocrRecPredictors[i] = new PPOcrRec(independentConfig);
                }
            }
        }

        /// <summary>
        /// Performs OCR prediction using default configuration settings.
        /// 使用默认配置设置执行OCR预测。
        /// </summary>
        /// <param name="img">Input image containing text / 包含文本的输入图像</param>
        /// <returns>OCR result containing detected text areas and recognized content / 包含检测到的文本区域和识别内容的OCR结果</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">Thrown when predictor is disposed / 当预测器已释放时抛出</exception>
        /// <remarks>
        /// This overload uses the configuration settings from constructor.
        /// 此重载使用构造函数中的配置设置。
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = ocr.Predict(image);
        /// foreach (var textArea in result.TextAreas)
        /// {
        ///     Console.WriteLine($"Found text at: {textArea.Bounds}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Predict(Mat, int?, bool?, bool?, bool?)"/>
        public OcrResult Predict(Mat img)
        {
            return Predict(img, _config.GlobalMaxBatchSize, _config.UseDet, _config.UseCls, _config.UseRec);
        }

        /// <summary>
        /// Performs OCR prediction with optional stage overrides.
        /// 使用可选的阶段覆盖执行OCR预测。
        /// </summary>
        /// <param name="img">Input image containing text / 包含文本的输入图像</param>
        /// <param name="batchSize">Override batch size (null uses config default) / 覆盖批处理大小(null使用配置默认值)</param>
        /// <param name="useDet">Override detection stage (null uses config default) / 覆盖检测阶段(null使用配置默认值)</param>
        /// <param name="useCls">Override classification stage (null uses config default) / 覆盖分类阶段(null使用配置默认值)</param>
        /// <param name="useRec">Override recognition stage (null uses config default) / 覆盖识别阶段(null使用配置默认值)</param>
        /// <returns>OCR result / OCR结果</returns>
        /// <exception cref="ArgumentException">Thrown when invalid combination of stages is specified / 当指定了无效的阶段组合时抛出</exception>
        /// <remarks>
        /// <para>
        /// You can selectively enable/disable stages per prediction call.
        /// 您可以为每次预测调用选择性地启用/禁用阶段。
        /// </para>
        /// <para>
        /// Note: Classification requires detection to be enabled (or use rec-only mode).
        /// 注意：分类需要启用检测(或使用仅识别模式)。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Run detection only (fast preview)
        /// // 仅运行检测(快速预览)
        /// var preview = ocr.Predict(image, useDet: true, useCls: false, useRec: false);
        /// 
        /// // Run recognition only (on pre-cropped text images)
        /// // 仅运行识别(在预裁剪的文本图像上)
        /// var textOnly = ocr.Predict(croppedImage, useDet: false, useRec: true);
        /// </code>
        /// </example>
        public OcrResult Predict(Mat img, int? batchSize = null, bool? useDet = null, bool? useCls = null, bool? useRec = null)
        {
            if (img == null || img.Empty()) throw new ArgumentException("输入图像无效");

            bool runDet = useDet ?? _config.UseDet;
            bool runCls = useCls ?? _config.UseCls;
            bool runRec = useRec ?? _config.UseRec;
            int inferBatchSize = batchSize ?? _config.GlobalMaxBatchSize;

            var result = new OcrResult();
            var totalSw = Stopwatch.StartNew();

            // Mode A: Full pipeline (Det -> Cls -> Rec)
            if (runDet)
            {
                var detSw = Stopwatch.StartNew();

                // 1. Detection
                ObbResult[] detResults = _ocrDetPredictor.Predict(img);
                MyLogger.Log.Info($"Detection Finished. Count: {detResults.Length}, Time: {detSw.ElapsedMilliseconds}ms");

                if (detResults.Length == 0) return result;
                result.TextAreas = detResults;

                // 2. Parallel cropping
                var cropSw = Stopwatch.StartNew();
                var croppedMats = CropImages(img, detResults);
                MyLogger.Log.Info($"Crop Finished. Time: {cropSw.ElapsedMilliseconds}ms");

                // 3. Batch processing for classification and recognition
                ProcessRecBatch(croppedMats, out TextRecResult[] textRecResults, out Result[] clsResults, runCls && _config.UseCls, inferBatchSize);

                result.TextContents = textRecResults;
                if (runCls && _config.UseCls)
                {
                    result.TextOrientations = clsResults;
                }

                // 4. Clean up cropped images
                ReleaseMats(croppedMats);
            }
            // Mode B: Recognition only mode (Rec Only / Rec + Cls)
            else if (runRec)
            {
                result.TextAreas = new ObbResult[] { new ObbResult()
                {
                    Bounds = new Data.RotatedRect(new PointF(img.Cols / 2f, img.Rows / 2f),
                    new SizeF(img.Cols, img.Rows),
                    0),
                    Confidence = 1.0f,
                    Id = 0
                } };

                var singleImageList = new Mat[1] { img };
                ProcessRecBatch(singleImageList, out TextRecResult[] textRecResults, out Result[] clsResults, runCls && _config.UseCls, inferBatchSize);

                result.TextContents = textRecResults;
                if (runCls && _config.UseCls)
                {
                    result.TextOrientations = clsResults;
                }
            }

            MyLogger.Log.Info($"Total Predict Time: {totalSw.ElapsedMilliseconds}ms");
            return result;
        }

        /// <summary>
        /// Crops detected text regions from the source image.
        /// 从源图像中裁剪检测到的文本区域。
        /// </summary>
        /// <param name="srcImg">Source image / 源图像</param>
        /// <param name="detResults">Detection results containing text regions / 包含文本区域的检测结果</param>
        /// <returns>Array of cropped text region images / 裁剪的文本区域图像数组</returns>
        /// <remarks>
        /// <para>
        /// Uses parallel processing for cropping multiple regions.
        /// 使用并行处理裁剪多个区域。
        /// </para>
        /// <para>
        /// Failed crops return empty Mats instead of throwing exceptions.
        /// 失败的裁剪返回空Mat而不是抛出异常。
        /// </para>
        /// </remarks>
        private Mat[] CropImages(Mat srcImg, ObbResult[] detResults)
        {
            Mat[] croppedMats = new Mat[detResults.Length];
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency };

            Parallel.For(0, detResults.Length, parallelOptions, i =>
            {
                try
                {
                    croppedMats[i] = CvPPOcrDataProcessor.GetRotateCropImageByRect(srcImg, detResults[i].Bounds);
                }
                catch (Exception ex)
                {
                    MyLogger.Log.Info($"Crop image at index {i} failed: {ex.Message}");
                    croppedMats[i] = new Mat();
                }
            });
            return croppedMats;
        }

        /// <summary>
        /// Batch processes recognition and classification with task partitioning.
        /// 使用任务分区批量处理识别和分类。
        /// </summary>
        /// <param name="imgList">Array of cropped text images / 裁剪的文本图像数组</param>
        /// <param name="recResults">Output recognition results / 输出识别结果</param>
        /// <param name="clsResults">Output classification results / 输出分类结果</param>
        /// <param name="enableCls">Whether to enable classification / 是否启用分类</param>
        /// <param name="batchSize">Batch size for processing / 批处理大小</param>
        /// <remarks>
        /// <para>
        /// Key fix: Uses task partitioning with Parallel.For over worker IDs.
        /// 关键修复：使用基于工作器ID的Parallel.For进行任务分区。
        /// </para>
        /// <para>
        /// This ensures each thread only operates on its assigned predictor instance,
        /// eliminating race conditions and ensuring 1:1 binding.
        /// 这确保每个线程只操作其分配的预测器实例，消除竞争条件并确保1:1绑定。
        /// </para>
        /// </remarks>
        private void ProcessRecBatch(Mat[] imgList, out TextRecResult[] recResults, out Result[] clsResults, bool enableCls, int batchSize)
        {
            int imgCount = imgList.Length;
            TextRecResult[] recResultss = new TextRecResult[imgCount];
            Result[] clsResultss = new Result[imgCount];

            if (imgCount == 0) 
            {
                recResults = recResultss;
                clsResults = clsResultss;
                return;
            }

            // Preprocessing: Calculate aspect ratios and sort (serial step for batch optimization)
            List<float> widthList = new List<float>();
            for (int i = 0; i < imgCount; i++)
            {
                widthList.Add((float)(imgList[i].Cols) / imgList[i].Rows);
            }
            List<int> indices = argsort(widthList);

            // Key fix: Use Parallel.For over instance indices
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency };

            Parallel.For(0, _maxConcurrency, parallelOptions, workerId =>
            {
                // 1. Get predictor instance exclusive to this thread
                var currentRecPredictor = _ocrRecPredictors[workerId];
                PPOcrCls currentClsPredictor = null;
                if (enableCls && _ocrClsPredictors != null)
                {
                    currentClsPredictor = _ocrClsPredictors[workerId];
                }

                // 2. Calculate data range for this thread
                int chunkSize = (int)Math.Ceiling((float)imgCount / _maxConcurrency);
                int startIndex = workerId * chunkSize;
                int endIndex = Math.Min(imgCount, startIndex + chunkSize);

                if (startIndex >= imgCount) return;

                // 3. Process batches within this slice
                for (int i = startIndex; i < endIndex; i += batchSize)
                {
                    int batchEnd = Math.Min(endIndex, i + batchSize);
                    int currentBatchCount = batchEnd - i;

                    var batchMats = new List<Mat>(currentBatchCount);
                    var originalIndices = new List<int>(currentBatchCount);

                    for (int k = i; k < batchEnd; k++)
                    {
                        int sortedIndex = indices[k];
                        batchMats.Add(imgList[sortedIndex]);
                        originalIndices.Add(sortedIndex);
                    }

                    // Classification stage
                    if (enableCls && currentClsPredictor != null)
                    {
                        var clsSw = Stopwatch.StartNew();
                        var clsResult = currentClsPredictor.PredictBatch(batchMats);

                        for (int k = 0; k < currentBatchCount; k++)
                        {
                            var res = clsResult[k][0];
                            int realIndex = originalIndices[k];
                            clsResultss[realIndex] = res;

                            // Rotate 180 degrees if needed
                            if (res.Id == 1 && res.Confidence > _config.ClsConfig.ConfidenceThreshold)
                            {
                                Cv2.Rotate(batchMats[k], batchMats[k], RotateFlags.Rotate180);
                            }
                        }
                    }

                    // Recognition stage
                    if (currentRecPredictor != null)
                    {
                        var recSw = Stopwatch.StartNew();
                        var recResult = currentRecPredictor.PredictBatch(batchMats);

                        for (int k = 0; k < currentBatchCount; k++)
                        {
                            int realIndex = originalIndices[k];
                            if (recResult[k] != null && recResult[k].Length > 0)
                            {
                                recResultss[realIndex] = recResult[k][0];
                            }
                        }
                    }
                }
            });
            recResults = recResultss;
            clsResults = clsResultss;
        }

        /// <summary>
        /// Returns sorted indices for a list of float values (ascending order).
        /// 返回浮点值列表的排序索引(升序)。
        /// </summary>
        /// <param name="array">List of float values / 浮点值列表</param>
        /// <returns>List of indices sorted by value / 按值排序的索引列表</returns>
        /// <remarks>
        /// Used for sorting images by aspect ratio for efficient batch processing.
        /// 用于按宽高比排序图像以实现高效的批处理。
        /// </remarks>
        public static List<int> argsort(List<float> array)
        {
            int array_len = array.Count;
            List<float[]> new_array = new List<float[]> { };
            for (int i = 0; i < array_len; i++)
            {
                new_array.Add(new float[] { array[i], i });
            }
            new_array.Sort((a, b) => a[0].CompareTo(b[0]));
            List<int> array_index = new List<int>();
            foreach (float[] item in new_array)
            {
                array_index.Add((int)item[1]);
            }
            return array_index;
        }

        /// <summary>
        /// Prints timing profiling information for all stages.
        /// 打印所有阶段的时间分析信息。
        /// </summary>
        /// <returns>Profiling information as string / 分析信息字符串</returns>
        /// <remarks>
        /// Useful for performance analysis and optimization.
        /// 用于性能分析和优化。
        /// </remarks>
        public string PrintTimeProfiling()
        {
            string msg = "---- Detection ----\n";
            Console.WriteLine("---- Detection ----");
            msg += _ocrDetPredictor?.ModelInferenceProfiler.PrintAllRecords();

            Console.WriteLine("---- Classification ----");
            msg += "---- Classification ----\n";
            if (_ocrClsPredictors != null)
            {
                for (int i = 0; i < _ocrClsPredictors.Length; i++)
                {
                    msg += $"Device/Worker {i}:\n" + _ocrClsPredictors[i]?.ModelInferenceProfiler.PrintAllRecords();
                }
            }

            Console.WriteLine("---- Recognition ----");
            msg += "---- Recognition ----\n";
            if (_ocrRecPredictors != null)
            {
                for (int i = 0; i < _ocrRecPredictors.Length; i++)
                {
                    msg += $"Device/Worker {i}:\n" + _ocrRecPredictors[i]?.ModelInferenceProfiler.PrintAllRecords();
                }
            }
            return msg;
        }

        /// <summary>
        /// Releases all Mats in the array.
        /// 释放数组中的所有Mat。
        /// </summary>
        /// <param name="mats">Array of Mats to release / 要释放的Mat数组</param>
        private void ReleaseMats(Mat[] mats)
        {
            foreach (var mat in mats)
            {
                try { mat?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Releases all resources used by the predictor.
        /// 释放预测器使用的所有资源。
        /// </summary>
        /// <remarks>
        /// Disposes all predictor instances and clears references.
        /// 释放所有预测器实例并清除引用。
        /// </remarks>
        public void Dispose()
        {
            _ocrDetPredictor?.Dispose();

            if (_ocrClsPredictors != null)
            {
                foreach (var pred in _ocrClsPredictors)
                {
                    pred?.Dispose();
                }
            }

            if (_ocrRecPredictors != null)
            {
                foreach (var pred in _ocrRecPredictors)
                {
                    pred?.Dispose();
                }
            }
            MyLogger.Log.Info("PaddleOcrPredictor Disposed.");
        }
    }
}
