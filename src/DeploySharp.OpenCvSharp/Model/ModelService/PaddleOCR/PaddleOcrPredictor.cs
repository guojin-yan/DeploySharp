//using DeploySharp.Common;
//using DeploySharp.Data;
//using DeploySharp.Log;
//using DeploySharp.Model;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//namespace DeploySharp.Model
//{
//    /// <summary>
//    /// PaddleOCR 预测器主类
//    /// 负责 OCR 全流程（检测、分类、识别）的编排与调度
//    /// </summary>
//    public class PaddleOcrPredictor : IDisposable
//    {
//        private readonly PaddleOCRConfig _config;

//        // 预测器实例
//        private PPOcrDet _ocrDetPredictor;
//        private PPOcrCls _ocrClsPredictor;
//        private PPOcrRec[] _ocrRecPredictor;
//        /// <summary>
//        /// 构造函数
//        /// </summary>
//        /// <param name="config">OCR 配置对象</param>
//        public PaddleOcrPredictor(PaddleOCRConfig config)
//        {
//            _config = config ?? throw new ArgumentNullException(nameof(config));

//            // 初始化前校验配置
//            _config.Validate();
//            // 按需初始化预测器
//            if (_config.UseDet) _ocrDetPredictor = new PPOcrDet(_config.DetConfig);
//            if (_config.UseCls) _ocrClsPredictor = new PPOcrCls(_config.ClsConfig);
//            if (_config.UseRec) 
//            {
//                _ocrRecPredictor = new PPOcrRec[4];
//                for (int i = 0; i < 4; i++)
//                {
//                    // 1. 将原配置序列化为字符串
//                    string configJson = JsonSerializer.Serialize(_config.RecConfig);
//                    // 2. 反序列化回一个新的对象
//                    // 这会生成一个全新的、属性值完全相同的 RecConfig 对象
//                    PPOcrRecConfig independentConfig = JsonSerializer.Deserialize<PPOcrRecConfig>(configJson);
//                    _ocrRecPredictor[i] = new PPOcrRec(independentConfig);
//                }
//            };

//        }
//        /// <summary>
//        /// 执行预测（使用配置中的默认设置）
//        /// </summary>
//        public OcrResult Predict(Mat img)
//        {
//            return Predict(img, _config.GlobalMaxBatchSize, _config.UseDet, _config.UseCls, _config.UseRec);
//        }
//        /// <summary>
//        /// 执行预测（覆盖部分流程开关，但必须符合逻辑依赖）
//        /// 例如：即使配置了 UseCls=true，也可以强制传 false 关闭
//        /// </summary>
//        public OcrResult Predict(Mat img, int? batchSize = null, bool? useDet = null, bool? useCls = null, bool? useRec = null)
//        {
//            if (img == null || img.Empty()) throw new ArgumentException("输入图像无效");
//            // 解析实际使用的开关，参数优先于配置
//            bool runDet = useDet ?? _config.UseDet;
//            bool runCls = useCls ?? _config.UseCls;
//            bool runRec = useRec ?? _config.UseRec;
//            int inferBatchSize = batchSize ?? _config.GlobalMaxBatchSize;
//            //// 逻辑一致性检查
//            //if (!runDet && runCls)
//            //    throw new InvalidOperationException("无法在未运行检测的情况下运行分类;");
//            var result = new OcrResult();
//            var totalSw = Stopwatch.StartNew();
//            // ---------------------------------------------------------
//            // 模式 A: 完整流程 (Det -> Cls -> Rec)
//            // ---------------------------------------------------------
//            if (runDet)
//            {
//                var detSw = Stopwatch.StartNew();

//                // 1. 检测
//                ObbResult[] detResults = _ocrDetPredictor.Predict(img);
//                MyLogger.Log.Info($"Detection Finished. Count: {detResults.Length}, Time: {detSw.ElapsedMilliseconds}ms");
//                if (detResults.Length == 0) return result;
//                result.TextAreas = detResults;
//                // 2. 并行裁剪
//                var cropSw = Stopwatch.StartNew();
//                var croppedMats = CropImages(img, detResults);
//                MyLogger.Log.Info($"Crop Finished. Time: {cropSw.ElapsedMilliseconds}ms");
//                // 3. 批处理分类与识别
//                // 注意：如果配置了 UseCls 但预测时强制不使用，则跳过
//                ProcessRecBatch(croppedMats, out TextRecResult[] textRecResults, out Result[] clsResults, runCls && _config.UseCls, inferBatchSize);

//                result.TextContents = textRecResults; // 假设 ObbResult 里有 RecResult 属性，或者你用单独的列表管理
//                if (runCls && _config.UseCls)
//                {
//                    result.TextOrientations = clsResults;
//                }

//                // 4. 清理裁剪内存
//                ReleaseMats(croppedMats);
//            }
//            // ---------------------------------------------------------
//            // 模式 B: 仅识别模式 (Rec Only / Rec + Cls)
//            // 用于用户已经做好了裁剪，或者只需要识别整张图
//            // ---------------------------------------------------------
//            else if (runRec)
//            {
//                result.TextAreas = new ObbResult[] { new ObbResult() 
//                {
//                    Bounds = new Data.RotatedRect(new PointF(img.Cols / 2f, img.Rows / 2f), 
//                    new SizeF(img.Cols, img.Rows),
//                    0),
//                    Confidence = 1.0f,
//                    Id = 0
//                } };
//                // 将整张图作为一个 Batch 处理
//                var singleImageList = new Mat[1] { img };
//                // 伪造一个 detResults 结构用于回填，或者修改 ProcessRecBatch 接口
//                // 这里假设我们只需要文字内容，不关心位置
//                // 简单处理：直接调用 Rec 预测

//                ProcessRecBatch(singleImageList, out TextRecResult[] textRecResults, out Result[] clsResults, runCls && _config.UseCls, inferBatchSize);

//                result.TextContents = textRecResults; // 假设 ObbResult 里有 RecResult 属性，或者你用单独的列表管理
//                if (runCls && _config.UseCls)
//                {
//                    result.TextOrientations = clsResults;
//                }
//            }
//            MyLogger.Log.Info($"Total Predict Time: {totalSw.ElapsedMilliseconds}ms");
//            return result;
//        }
//        /// <summary>
//        /// 裁剪图像：使用并行加速，并使用 try-catch 保证单个失败不影响整体
//        /// </summary>
//        private Mat[] CropImages(Mat srcImg, ObbResult[] detResults)
//        {
//            Mat[] croppedMats = new Mat[detResults.Length];

//            // 使用 ParallelOptions 限制最大并行度，防止内存爆炸
//            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
//            Parallel.For(0, detResults.Length, parallelOptions, i =>
//            {
//                try
//                {
//                    // 假设 GetRotateCropImageByRect 是线程安全的（不修改共享状态）
//                    croppedMats[i] = CvPPOcrDataProcessor.GetRotateCropImageByRect(srcImg, detResults[i].Bounds);
//                }
//                catch (Exception ex)
//                {
//                    MyLogger.Log.Info($"Crop image at index {i} failed: {ex.Message}");
//                    // 即使失败也分配一个空 Mat 占位，防止后续空引用
//                    croppedMats[i] = new Mat();
//                }
//            });
//            return croppedMats;
//        }
//        /// <summary>
//        /// 批量处理识别与分类
//        /// </summary>
//        private void ProcessRecBatch(Mat[] imgList, out TextRecResult[] recResults, out Result[] clsResults,bool enableCls, int batchSize)
//        {
//            int imgCount = imgList.Length;
//            TextRecResult[] recResultss = new TextRecResult[imgCount];
//            Result[] clsResultss = new Result[imgCount];

//            List<float> widthList = new List<float>();
//            for (int i = 0; i < imgCount; i++)
//            {
//                widthList.Add((float)(imgList[i].Cols) / imgList[i].Rows);
//            }
//            List<int> indices = argsort(widthList);

//            int bs = (int)Math.Ceiling((float)imgCount / batchSize);

//            Parallel.For(0, bs, b =>
//            {
//                int beg = b * batchSize;
//                int end = Math.Min(imgCount, beg + batchSize);
//                int currentBatchSize = end - beg;

//                // 准备当前 Batch 的数据
//                var batchMats = new List<Mat>(currentBatchSize);
//                for (int i = beg; i < end; i++) batchMats.Add(imgList[indices[i]]);
//                var clsSw = Stopwatch.StartNew();
//                // --- 方向分类 ---
//                if (enableCls && _ocrClsPredictor != null)
//                {
//                    // 假设 PredictBatch 返回 List<Result[]>，Result包含 Label/Score
//                    var clsResult = _ocrClsPredictor.PredictBatch(batchMats);
//                    for (int i = 0; i < currentBatchSize; i++)
//                    {
//                        // 判断是否需要旋转 (假设 Label 1 或 '180' 表示需要旋转)
//                        var res = clsResult[i][0];
//                        clsResultss[beg + i] = res;
//                        if (res.Id == 1 && res.Confidence > _config.ClsConfig.ConfidenceThreshold)
//                        {
//                            Cv2.Rotate(batchMats[i], batchMats[i], RotateFlags.Rotate180);
//                        }
//                    }
//                    MyLogger.Log.Info($"Cls Batch ({beg}-{end}) Time: {clsSw.ElapsedMilliseconds}ms");
//                }
//                var recSw = Stopwatch.StartNew();
//                // --- 文字识别 ---
//                if (_ocrRecPredictor != null)
//                {
//                    var recResult = _ocrRecPredictor[b].PredictBatch(batchMats);
//                    // 回填结果
//                    for (int i = 0; i < currentBatchSize; i++)
//                    {
//                        // 将识别结果关联到对应的检测结果对象上
//                        // 假设 ObbResult 类有 RecText 和 RecScore 属性
//                        if (recResult[i] != null && recResult[i].Length > 0)
//                        {
//                            recResultss[indices[beg + i]] = recResult[i][0];
//                            // detResults[beg + i].Text = recResults[i][0].Str; // 根据实际数据结构调整
//                        }
//                    }
//                    MyLogger.Log.Error($"Rec Batch ({beg}-{end}) Time: {recSw.ElapsedMilliseconds}ms");
//                }
//            });
//            recResults = recResultss;
//            clsResults = clsResultss;

//        }
//        public static List<int> argsort(List<float> array)
//        {
//            int array_len = array.Count;

//            //生成值和索引的列表
//            List<float[]> new_array = new List<float[]> { };
//            for (int i = 0; i < array_len; i++)
//            {
//                new_array.Add(new float[] { array[i], i });
//            }
//            //对列表按照值小到大进行排序
//            new_array.Sort((a, b) => a[0].CompareTo(b[0]));
//            //获取排序后的原索引
//            List<int> array_index = new List<int>();
//            foreach (float[] item in new_array)
//            {
//                array_index.Add((int)item[1]);
//            }
//            return array_index;
//        }

//        public string PrintTimeProfiling()
//        {

//            string msg = "---- Detection ----\n";
//            Console.WriteLine("---- Detection ----");
//            msg += _ocrDetPredictor?.ModelInferenceProfiler.PrintAllRecords();
//            msg += "---- Classification ----\n";
//            Console.WriteLine("---- Classification ----");
//            msg += _ocrClsPredictor?.ModelInferenceProfiler.PrintAllRecords();
//            msg += "---- Recognition ----\n";
//            Console.WriteLine("---- Recognition ----");
//            msg += _ocrRecPredictor[0].ModelInferenceProfiler.PrintAllRecords();
//            msg += _ocrRecPredictor[1].ModelInferenceProfiler.PrintAllRecords();
//            msg += _ocrRecPredictor[2].ModelInferenceProfiler.PrintAllRecords();
//            msg += _ocrRecPredictor[3].ModelInferenceProfiler.PrintAllRecords();
//            return msg;
//        }

//        private void ReleaseMats(Mat[] mats)
//        {
//            foreach (var mat in mats)
//            {
//                try { mat?.Dispose(); } catch { }
//            }
//        }
//        public void Dispose()
//        {
//            _ocrDetPredictor?.Dispose();
//            _ocrClsPredictor?.Dispose();
//            _ocrRecPredictor[0].Dispose();
//            _ocrRecPredictor[1].Dispose();
//            _ocrRecPredictor[2].Dispose();
//            _ocrRecPredictor[3].Dispose();
//            MyLogger.Log.Info("PaddleOcrPredictor Disposed.");
//        }
//    }

//}

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
    /// PaddleOCR 预测器主类
    /// 负责 OCR 全流程（检测、分类、识别）的编排与调度
    /// 优化方案：采用 "任务分区" 策略，将推理任务列表严格切分为 N 份，每份对应一个独立的推理线程和设备。
    /// 从而彻底解决线程 ID 哈希冲突导致的设备负载不均问题。
    /// </summary>
    public class PaddleOcrPredictor : IDisposable
    {
        private readonly PaddleOCRConfig _config;
        private readonly int _maxConcurrency; // 最大并发度（对应设备数或推理实例数）

        // 预测器实例
        private PPOcrDet _ocrDetPredictor;
        private PPOcrCls[] _ocrClsPredictors;
        private PPOcrRec[] _ocrRecPredictors;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">OCR 配置对象</param>
        public PaddleOcrPredictor(PaddleOCRConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.Validate();

            // 确定最大并发数，通常建议根据 GPU 数量或 CPU 核心数设置
            // 这里的 4 应该对应你拥有的物理设备数量（例如 4 张 GPU 或 4 个 CPU 核心组）
            _maxConcurrency = config.MaxConcurrency; // 建议通过配置传入，例如 Environment.ProcessorCount 或 GPU 数量

            // 1. 初始化检测
            if (_config.UseDet) _ocrDetPredictor = new PPOcrDet(_config.DetConfig);

            // 2. 初始化分类器实例池（每个实例绑定一个线程/设备）
            if (_config.UseCls)
            {
                _ocrClsPredictors = new PPOcrCls[_maxConcurrency];
                for (int i = 0; i < _maxConcurrency; i++)
                {
                    // 深度拷贝配置，确保每个实例资源独立（尤其是设备 ID 配置）
                    string configJson = JsonSerializer.Serialize(_config.ClsConfig);
                    PPOcrClsConfig independentConfig = JsonSerializer.Deserialize<PPOcrClsConfig>(configJson);
                    // 注意：这里假设 independentConfig 中包含了指定 DeviceId 的逻辑，
                    // 或者每个 PPOcrCls 实例底层会自动绑定不同的资源。
                    _ocrClsPredictors[i] = new PPOcrCls(independentConfig);
                }
            }

            // 3. 初始化识别器实例池
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
        /// 执行预测（使用配置中的默认设置）
        /// </summary>
        public OcrResult Predict(Mat img)
        {
            return Predict(img, _config.GlobalMaxBatchSize, _config.UseDet, _config.UseCls, _config.UseRec);
        }

        /// <summary>
        /// 执行预测（覆盖部分流程开关）
        /// </summary>
        public OcrResult Predict(Mat img, int? batchSize = null, bool? useDet = null, bool? useCls = null, bool? useRec = null)
        {
            if (img == null || img.Empty()) throw new ArgumentException("输入图像无效");

            bool runDet = useDet ?? _config.UseDet;
            bool runCls = useCls ?? _config.UseCls;
            bool runRec = useRec ?? _config.UseRec;
            int inferBatchSize = batchSize ?? _config.GlobalMaxBatchSize;

            var result = new OcrResult();
            var totalSw = Stopwatch.StartNew();

            // ---------------------------------------------------------
            // 模式 A: 完整流程 (Det -> Cls -> Rec)
            // ---------------------------------------------------------
            if (runDet)
            {
                var detSw = Stopwatch.StartNew();

                // 1. 检测
                ObbResult[] detResults = _ocrDetPredictor.Predict(img);
                MyLogger.Log.Info($"Detection Finished. Count: {detResults.Length}, Time: {detSw.ElapsedMilliseconds}ms");

                if (detResults.Length == 0) return result;
                result.TextAreas = detResults;

                // 2. 并行裁剪
                var cropSw = Stopwatch.StartNew();
                var croppedMats = CropImages(img, detResults);
                MyLogger.Log.Info($"Crop Finished. Time: {cropSw.ElapsedMilliseconds}ms");

                // 3. 批处理分类与识别
                ProcessRecBatch(croppedMats, out TextRecResult[] textRecResults, out Result[] clsResults, runCls && _config.UseCls, inferBatchSize);

                result.TextContents = textRecResults;
                if (runCls && _config.UseCls)
                {
                    result.TextOrientations = clsResults;
                }

                // 4. 清理裁剪内存
                ReleaseMats(croppedMats);
            }
            // ---------------------------------------------------------
            // 模式 B: 仅识别模式 (Rec Only / Rec + Cls)
            // ---------------------------------------------------------
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
        /// 裁剪图像
        /// </summary>
        private Mat[] CropImages(Mat srcImg, ObbResult[] detResults)
        {
            Mat[] croppedMats = new Mat[detResults.Length];
            // 裁剪阶段是 CPU 密集型，可以使用 Parallel.For
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
        /// 批量处理识别与分类
        /// 修复策略：使用任务分区
        /// 将 imgList 根据索引范围严格切分为 _maxConcurrency 个区间。
        /// 例如：如果有 4 个实例，索引 0-10 归线程0处理，11-20 归线程1处理，以此类推。
        /// 这样能确保每个线程只操作属于自己的那个 Predictor 实例，保证 1:1 绑定，杜绝竞争。
        /// </summary>
        private void ProcessRecBatch(Mat[] imgList, out TextRecResult[] recResults, out Result[] clsResults, bool enableCls, int batchSize)
        {
            int imgCount = imgList.Length;
            //recResults = new TextRecResult[imgCount];
            //clsResults = new Result[imgCount];

            TextRecResult[] recResultss = new TextRecResult[imgCount];
            Result[] clsResultss = new Result[imgCount];

            if (imgCount == 0) 
            {
                recResults = recResultss;
                clsResults = clsResultss;

                return;
            }
            

            // 预处理：计算宽高比并排序（这一步是串行的，用于优化后续 Batch 填充率）
            List<float> widthList = new List<float>();
            for (int i = 0; i < imgCount; i++)
            {
                widthList.Add((float)(imgList[i].Cols) / imgList[i].Rows);
            }
            // 获取排序后的索引映射
            List<int> indices = argsort(widthList);

            // -------------------------------------------------------------
            // 关键修复：使用 Parallel.For 循环实例索引（0 到 maxConcurrency-1）
            // -------------------------------------------------------------
            // 我们不再循环 "Batch"，而是直接循环 "设备/线程槽位"。
            // 每个槽位负责处理 imgList 中属于自己那部分的数据。

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency };

            Parallel.For(0, _maxConcurrency, parallelOptions, workerId =>
            {
                // 1. 获取当前线程专属的推理实例
                // workerId 范围是 [0, 1, 2, 3]，这保证了每个分支拿到不同的对象
                var currentRecPredictor = _ocrRecPredictors[workerId];
                PPOcrCls currentClsPredictor = null;
                if (enableCls && _ocrClsPredictors != null)
                {
                    currentClsPredictor = _ocrClsPredictors[workerId];
                }

                // 2. 计算当前线程负责处理的数据范围
                // 简单的分区策略：将总图片数切分为 N 份
                // 例如：100张图，4个线程 -> 线程0处理 0-24，线程1处理 25-49...
                // 注意：这里是基于排序后的 indices 进行切片
                int chunkSize = (int)Math.Ceiling((float)imgCount / _maxConcurrency);
                int startIndex = workerId * chunkSize;
                int endIndex = Math.Min(imgCount, startIndex + chunkSize);

                // 如果当前线程分配到的范围内没有数据（例如图片数少于线程数），直接返回
                if (startIndex >= imgCount) return;

                // 3. 在当前分片内进行批次处理
                // 我们需要把 indices[startIndex] 到 indices[endIndex] 之间的图片取出来处理
                for (int i = startIndex; i < endIndex; i += batchSize)
                {
                    int batchEnd = Math.Min(endIndex, i + batchSize);
                    int currentBatchCount = batchEnd - i;

                    // 准备当前批次的数据
                    var batchMats = new List<Mat>(currentBatchCount);
                    var originalIndices = new List<int>(currentBatchCount); // 记录这些图在原始数组中的真实索引

                    for (int k = i; k < batchEnd; k++)
                    {
                        int sortedIndex = indices[k]; // 获取排序后的位置
                        batchMats.Add(imgList[sortedIndex]);
                        originalIndices.Add(sortedIndex);
                    }

                    // --- 方向分类 ---
                    if (enableCls && currentClsPredictor != null)
                    {
                        var clsSw = Stopwatch.StartNew();
                        var clsResult = currentClsPredictor.PredictBatch(batchMats);

                        for (int k = 0; k < currentBatchCount; k++)
                        {
                            var res = clsResult[k][0];
                            int realIndex = originalIndices[k];
                            clsResultss[realIndex] = res;

                            // 如果需要旋转，修改 batchMats 中对应的图（因为后续 Rec 还要用）
                            if (res.Id == 1 && res.Confidence > _config.ClsConfig.ConfidenceThreshold)
                            {
                                Cv2.Rotate(batchMats[k], batchMats[k], RotateFlags.Rotate180);
                            }
                        }
                        // MyLogger.Log.Debug($"Worker {workerId} Cls Batch Time: {clsSw.ElapsedMilliseconds}ms");
                    }

                    // --- 文字识别 ---
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
                        // MyLogger.Log.Debug($"Worker {workerId} Rec Batch Time: {recSw.ElapsedMilliseconds}ms");
                    }
                }
            });
            recResults = recResultss;
            clsResults = clsResultss;
        }

        /// <summary>
        /// 对列表进行升序排序，返回排序后的索引列表
        /// </summary>
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

        private void ReleaseMats(Mat[] mats)
        {
            foreach (var mat in mats)
            {
                try { mat?.Dispose(); } catch { }
            }
        }

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
