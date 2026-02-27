using DeploySharp.Common;
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using DeploySharp.Model;
using OpenCvSharp;
using System;
using System.Threading.Tasks;

namespace DeploySharp
{
    /// <summary>
    /// High-level inference pipeline for computer vision models with OpenCvSharp integration.
    /// 使用OpenCvSharp集成的高级计算机视觉模型推理管道。
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Pipeline class provides a simplified interface for:
    /// Pipeline类提供了一个简化的接口用于：
    /// - Model initialization and configuration
    ///   模型初始化和配置
    /// - Single and batch image inference
    ///   单张和批量图像推理
    /// - Automatic result visualization
    ///   自动结果可视化
    /// - Resource management
    ///   资源管理
    /// </para>
    /// <para>
    /// Supports various model types including YOLOv5-v13 (detection, segmentation, pose, OBB),
    /// Anomalib, and more.
    /// 支持多种模型类型，包括YOLOv5-v13(检测、分割、姿态、OBB)、Anomalib等。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create pipeline for YOLOv8 detection
    /// // 创建YOLOv8检测管道
    /// using (var pipeline = new Pipeline(
    ///     ModelType.YOLOv8Det, 
    ///     "yolov8n.onnx",
    ///     InferenceBackend.OpenVINO,
    ///     DeviceType.CPU))
    /// {
    ///     // Load image
    ///     // 加载图像
    ///     using (Mat image = Cv2.ImRead("photo.jpg"))
    ///     {
    ///         // Run inference and get results
    ///         // 运行推理并获取结果
    ///         var results = pipeline.Predict(image);
    ///         
    ///         // Or get visualized result directly
    ///         // 或直接获取可视化结果
    ///         Mat visualized = pipeline.PredictAndDrawing(image);
    ///         Cv2.ImWrite("output.jpg", visualized);
    ///     }
    /// }
    /// </code>
    /// </example>
    public class Pipeline : IDisposable
    {
        private IModel model;
        private VisualizeHandler visualizeHandler;

        /// <summary>
        /// Creates a new inference pipeline with specified model type and path.
        /// 使用指定的模型类型和路径创建新的推理管道。
        /// </summary>
        /// <param name="modelType">Type of computer vision model / 计算机视觉模型类型</param>
        /// <param name="modelPath">Path to the model file / 模型文件路径</param>
        /// <param name="inferenceBackend">Inference backend (OpenVINO, ONNX Runtime, etc.) / 推理后端(OpenVINO、ONNX Runtime等)</param>
        /// <param name="deviceType">Target device (CPU, GPU, etc.) / 目标设备(CPU、GPU等)</param>
        /// <exception cref="DeploySharpException">Thrown when model type is not supported / 当模型类型不受支持时抛出</exception>
        /// <exception cref="FileNotFoundException">Thrown when model file is not found / 当模型文件未找到时抛出</exception>
        /// <remarks>
        /// The constructor automatically creates the appropriate model instance and visualization handler.
        /// 构造函数自动创建适当的模型实例和可视化处理程序。
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create pipeline for YOLOv8 segmentation on GPU
        /// // 在GPU上创建YOLOv8分割管道
        /// using (var pipeline = new Pipeline(
        ///     ModelType.YOLOv8Seg,
        ///     "yolov8n-seg.onnx",
        ///     InferenceBackend.OpenVINO,
        ///     DeviceType.GPU))
        /// {
        ///     // Use pipeline...
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Pipeline(ModelType, IConfig)"/>
        public Pipeline(ModelType modelType, string modelPath, InferenceBackend inferenceBackend = InferenceBackend.OpenVINO,
           DeviceType deviceType = DeviceType.CPU)
        {
            MyLogger.Log.Info($"初始化 Pipeline, ModelType: {modelType},  ModelPath: {modelPath}");

            try
            {
                MyLogger.Log.Debug("开始创建模型实例和可视化处理器...");

                switch (modelType)
                {
                    case ModelType.YOLOv5Det:
                        model = new Yolov5DetModel(new Yolov5DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv5Seg:
                        model = new Yolov5SegModel(new Yolov5SegConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv6Det:
                        model = new Yolov6DetModel(new Yolov6DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv7Det:
                        model = new Yolov7DetModel(new Yolov7DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv8Det:
                        model = new Yolov8DetModel(new Yolov8DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv8Seg:
                        model = new Yolov8SegModel(new Yolov8SegConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv8Obb:
                        model = new Yolov8ObbModel(new Yolov8ObbConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                        break;
                    case ModelType.YOLOv8Pose:
                        model = new Yolov8PoseModel(new Yolov8PoseConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                        break;
                    case ModelType.YOLOv9Det:
                        model = new Yolov9DetModel(new Yolov9DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv9Seg:
                        model = new Yolov9SegModel(new Yolov9SegConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv10Det:
                        model = new Yolov10DetModel(new Yolov10DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv11Det:
                        model = new Yolov11DetModel(new Yolov11DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv11Seg:
                        model = new Yolov11SegModel(new Yolov11SegConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv11Obb:
                        model = new Yolov11ObbModel(new Yolov11ObbConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                        break;
                    case ModelType.YOLOv11Pose:
                        model = new Yolov11PoseModel(new Yolov11PoseConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                        break;
                    case ModelType.YOLOv12Det:
                        model = new Yolov12DetModel(new Yolov12DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv13Det:
                        model = new Yolov13DetModel(new Yolov13DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.AnomalibSeg:
                        model = new AnomalibSegModel(new AnomalibSegConfig(modelPath: modelPath, inferenceBackend: inferenceBackend, deviceType: deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv26Det:
                        model = new Yolov26DetModel(new Yolov26DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv26Seg:
                        model = new Yolov26SegModel(new Yolov26SegConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv26Obb:
                        model = new Yolov26ObbModel(new Yolov26ObbConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                        break;
                    case ModelType.YOLOv26Pose:
                        model = new Yolov26PoseModel(new Yolov26PoseConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                        break;
                    case ModelType.RFDETRDet:
                        model = new RFDETRDetModel(new RFDETRDetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.RFDETRSeg:
                        model = new RFDETRSegModel(new RFDETRSegConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.RTDETRDet:
                        model = new RTDETRDetModel(new RTDETRDetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.DEIMv2Det:
                        model = new DEIMv2DetModel(new DEIMv2DetConfig(modelPath, inferenceBackend, deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.PaddleOcrCls:
                        model= new PPOcrCls (new PPOcrClsConfig(modelPath));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    default:
                        string errorMsg = $"{modelType.ToString()} model is currently not supported, please wait for further development support.";
                        MyLogger.Log.Error(errorMsg);
                        throw new DeploySharpException(errorMsg);
                }

                MyLogger.Log.Info($"成功创建 {modelType} 模型实例和可视化处理器");
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"初始化 Pipeline 时发生错误: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a new inference pipeline with custom configuration.
        /// 使用自定义配置创建新的推理管道。
        /// </summary>
        /// <param name="modelType">Type of computer vision model / 计算机视觉模型类型</param>
        /// <param name="config">Model configuration object / 模型配置对象</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <exception cref="DeploySharpException">Thrown when model type is not supported / 当模型类型不受支持时抛出</exception>
        /// <exception cref="InvalidCastException">Thrown when config type doesn't match model type / 当配置类型与模型类型不匹配时抛出</exception>
        /// <remarks>
        /// Use this constructor when you need fine-grained control over model configuration.
        /// 当您需要对模型配置进行细粒度控制时使用此构造函数。
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create custom configuration
        /// // 创建自定义配置
        /// var config = new Yolov8DetConfig("model.onnx")
        /// {
        ///     ConfidenceThreshold = 0.5f,
        ///     NmsThreshold = 0.45f,
        ///     InputSize = new Size(640, 640)
        /// };
        /// 
        /// using (var pipeline = new Pipeline(ModelType.YOLOv8Det, config))
        /// {
        ///     // Use pipeline...
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Pipeline(ModelType, string, InferenceBackend, DeviceType)"/>
        public Pipeline(ModelType modelType, IConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            MyLogger.Log.Info($"初始化 Pipeline, ModelType: {modelType},  ModelPath: {config.ModelPath}");

            try
            {
                MyLogger.Log.Debug("开始创建模型实例和可视化处理器...");

                switch (modelType)
                {
                    case ModelType.YOLOv5Det:
                        model = new Yolov5DetModel(config as Yolov5DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv5Seg:
                        model = new Yolov5SegModel(config as Yolov5SegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv6Det:
                        model = new Yolov6DetModel(config as Yolov6DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv7Det:
                        model = new Yolov7DetModel(config as Yolov7DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv8Det:
                        model = new Yolov8DetModel(config as Yolov8DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv8Seg:
                        model = new Yolov8SegModel(config as Yolov8SegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv8Obb:
                        model = new Yolov8ObbModel(config as Yolov8ObbConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                        break;
                    case ModelType.YOLOv8Pose:
                        model = new Yolov8PoseModel(config as Yolov8PoseConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                        break;
                    case ModelType.YOLOv9Det:
                        model = new Yolov9DetModel(config as Yolov9DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv9Seg:
                        model = new Yolov9SegModel(config as Yolov9SegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv10Det:
                        model = new Yolov10DetModel(config as Yolov10DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv11Det:
                        model = new Yolov11DetModel(config as Yolov11DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv11Seg:
                        model = new Yolov11SegModel(config as Yolov11SegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv11Obb:
                        model = new Yolov11ObbModel(config as Yolov11ObbConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                        break;
                    case ModelType.YOLOv11Pose:
                        model = new Yolov11PoseModel(config as Yolov11PoseConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                        break;
                    case ModelType.YOLOv12Det:
                        model = new Yolov12DetModel(config as Yolov12DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv13Det:
                        model = new Yolov13DetModel(config as Yolov13DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.AnomalibSeg:
                        model = new AnomalibSegModel(config as AnomalibSegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv26Det:
                        model = new Yolov26DetModel(config as Yolov26DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.YOLOv26Seg:
                        model = new Yolov26SegModel(config as Yolov26SegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.YOLOv26Obb:
                        model = new Yolov26ObbModel(config as Yolov26ObbConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                        break;
                    case ModelType.YOLOv26Pose:
                        model = new Yolov26PoseModel(config as Yolov26PoseConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                        break;
                    case ModelType.RFDETRDet:
                        model = new RFDETRDetModel(config as RFDETRDetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.RFDETRSeg:
                        model = new RFDETRSegModel(config as RFDETRSegConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                        break;
                    case ModelType.RTDETRDet:
                        model = new RTDETRDetModel(config as RTDETRDetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    case ModelType.DEIMv2Det:
                        model = new DEIMv2DetModel(config as DEIMv2DetConfig);
                        visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                        break;
                    default:
                        string errorMsg = $"{modelType.ToString()} model is currently not supported, please wait for further development support.";
                        MyLogger.Log.Error(errorMsg);
                        throw new DeploySharpException(errorMsg);
                }

                MyLogger.Log.Info($"成功创建 {modelType} 模型实例和可视化处理器");
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"初始化 Pipeline 时发生错误: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Releases all resources used by the pipeline.
        /// 释放管道使用的所有资源。
        /// </summary>
        /// <remarks>
        /// Disposes the underlying model and clears references.
        /// 释放底层模型并清除引用。
        /// </remarks>
        public void Dispose()
        {
            model?.Dispose();
            model = null;
            visualizeHandler = null;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// 终结器以确保资源被释放。
        /// </summary>
        ~Pipeline()
        {
            Dispose();
        }

        /// <summary>
        /// Runs synchronous inference on a single image.
        /// 对单张图像运行同步推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of detection results / 检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">Thrown when pipeline is disposed / 当管道已释放时抛出</exception>
        /// <exception cref="InferenceException">Thrown when inference fails / 当推理失败时抛出</exception>
        /// <remarks>
        /// This is a blocking call that waits for inference to complete.
        /// 这是一个阻塞调用，等待推理完成。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("input.jpg"))
        /// {
        ///     var results = pipeline.Predict(image);
        ///     
        ///     foreach (var result in results)
        ///     {
        ///         Console.WriteLine($"Detected: {result.Category} ({result.Confidence:P})");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictAsync"/>
        /// <seealso cref="PredictAndDrawing"/>
        public Result[] Predict(Mat img)
        {
            MyLogger.Log.Debug("开始执行 Predict 同步推理");
            try
            {
                var results = model.Predict(img);
                MyLogger.Log.Debug($"同步推理完成, 返回 {results.Length} 个结果");
                return results;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"执行 Predict 同步推理时发生错误: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Runs synchronous inference and returns visualized result.
        /// 运行同步推理并返回可视化结果。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Image with detection results drawn / 带有绘制检测结果的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">Thrown when pipeline is disposed / 当管道已释放时抛出</exception>
        /// <remarks>
        /// This is a convenience method that combines inference and visualization.
        /// 这是一个便捷方法，结合了推理和可视化。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("input.jpg"))
        /// {
        ///     Mat result = pipeline.PredictAndDrawing(image);
        ///     Cv2.ImWrite("output.jpg", result);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Predict"/>
        /// <seealso cref="PredictAsyncAndDrawing"/>
        public Mat PredictAndDrawing(Mat img)
        {
            MyLogger.Log.Debug("开始执行 PredictAndDrawing 同步推理与可视化");
            try
            {
                var result = visualizeHandler.ExecuteDrawing(model.Predict(img), img.Clone(), new VisualizeOptions(1.0f));
                MyLogger.Log.Debug("同步推理与可视化完成");
                return result;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"执行 PredictAndDrawing 时发生错误: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Runs asynchronous inference on a single image.
        /// 对单张图像运行异步推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Task representing the asynchronous inference operation / 表示异步推理操作的任务</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">Thrown when pipeline is disposed / 当管道已释放时抛出</exception>
        /// <remarks>
        /// <para>
        /// This method allows the calling thread to continue execution while inference runs.
        /// 此方法允许调用线程在推理运行时继续执行。
        /// </para>
        /// <para>
        /// Use await to get the results when ready.
        /// 使用await在结果准备好时获取它们。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Process multiple images concurrently
        /// // 并发处理多张图像
        /// var tasks = imagePaths.Select(path => 
        /// {
        ///     using (var img = Cv2.ImRead(path))
        ///         return pipeline.PredictAsync(img);
        /// });
        /// 
        /// var allResults = await Task.WhenAll(tasks);
        /// </code>
        /// </example>
        /// <seealso cref="Predict"/>
        /// <seealso cref="PredictAsyncAndDrawing"/>
        public async Task<Result[]> PredictAsync(Mat img)
        {
            MyLogger.Log.Debug("开始执行 PredictAsync 异步推理");
            try
            {
                var results = await model.PredictAsync(img);
                MyLogger.Log.Debug($"异步推理完成, 返回 {results.Length} 个结果");
                return results;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"执行 PredictAsync 异步推理时发生错误: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Runs asynchronous inference and returns visualized result.
        /// 运行异步推理并返回可视化结果。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Task representing the asynchronous inference and visualization operation / 表示异步推理和可视化操作的任务</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">Thrown when pipeline is disposed / 当管道已释放时抛出</exception>
        /// <remarks>
        /// Combines asynchronous inference with visualization for non-blocking UI updates.
        /// 结合异步推理和可视化，实现非阻塞的UI更新。
        /// </remarks>
        /// <example>
        /// <code>
        /// // In a UI application
        /// // 在UI应用程序中
        /// private async void OnProcessButtonClick(object sender, EventArgs e)
        /// {
        ///     using (Mat image = await LoadImageAsync())
        ///     {
        ///         Mat result = await pipeline.PredictAsyncAndDrawing(image);
        ///         DisplayImage(result);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictAndDrawing"/>
        /// <seealso cref="PredictAsync"/>
        public async Task<Mat> PredictAsyncAndDrawing(Mat img)
        {
            MyLogger.Log.Debug("开始执行 PredictAsyncAndDrawing 异步推理与可视化");
            try
            {
                var predictionResult = await model.PredictAsync(img).ConfigureAwait(false);
                MyLogger.Log.Debug($"异步推理完成, 开始可视化处理...");

                var visualizedResult = visualizeHandler.ExecuteDrawing(predictionResult, img, new VisualizeOptions(1.0f));

                MyLogger.Log.Debug("异步推理与可视化完成");
                return visualizedResult;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"执行 PredictAsyncAndDrawing 时发生错误: {ex.Message}", ex);
                throw;
            }
        }
    }
}
