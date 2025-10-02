using DeploySharp.Common;
using DeploySharp.Data;
using DeploySharp.Log;
using DeploySharp.Model;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Threading.Tasks;
using static DeploySharp.Data.Visualize;
using DeploySharp.Engine;

namespace DeploySharp
{
    /// <summary>
    /// Main pipeline for computer vision model inference and visualization
    /// 计算机视觉模型推理和可视化的主流水线
    /// </summary>
    /// <remarks>
    /// <para>
    /// Encapsulates model inference operations with built-in visualization capabilities.
    /// Supports multiple YOLO versions (v5-v13) and tasks (detection, segmentation, pose etc.).
    /// 封装了模型推理操作并内置可视化能力。
    /// 支持多个YOLO版本(v5-v13)和任务(检测、分割、姿态等)。
    /// </para>
    /// <para>
    /// Implements both synchronous and asynchronous operations with proper resource cleanup.
    /// 实现了同步和异步操作，并包含正确的资源清理。
    /// </para>
    /// </remarks>
    public class Pipeline : IDisposable
    {
        private IModel model;
        private VisualizeHandler visualizeHandler;

        /// <summary>
        /// Initializes pipeline with model path and configuration
        /// 使用模型路径和配置初始化流水线
        /// </summary>
        /// <param name="modelType">Type of YOLO model/YOLO模型类型</param>
        /// <param name="modelPath">Path to model file/模型文件路径</param>
        /// <param name="inferenceBackend">Inference backend (default: OpenVINO)/推理后端(默认:OpenVINO)</param>
        /// <param name="deviceType">Device type (default: CPU)/设备类型(默认:CPU)</param>
        /// <exception cref="DeploySharpException">
        /// Thrown when model type is not supported
        /// 当模型类型不受支持时抛出
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when initialization fails
        /// 当初始化失败时抛出
        /// </exception>
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
                        model = new AnomalibSegModel(new AnomalibSegConfig(modelPath:modelPath, inferenceBackend:inferenceBackend, deviceType:deviceType));
                        visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
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
        /// Initializes pipeline with existing configuration
        /// 使用现有配置初始化流水线
        /// </summary>
        /// <param name="modelType">Type of YOLO model/YOLO模型类型</param>
        /// <param name="config">Model configuration object/模型配置对象</param>
        public Pipeline(ModelType modelType, IConfig config)
        {
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
        /// Releases all resources
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            model?.Dispose();
            model = null;
            visualizeHandler = null;
            GC.SuppressFinalize(this);
        }
        ~Pipeline()
        {
            Dispose();
        }
        /// <summary>
        /// Performs synchronous inference
        /// 执行同步推理
        /// </summary>
        /// <param name="img">Input image/输入图像</param>
        /// <returns>Array of inference results/推理结果数组</returns>
        public Result[] Predict(Image<Rgb24> img)
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
        /// Performs synchronous inference with visualization
        /// 执行带可视化的同步推理
        /// </summary>
        public Image<Rgb24> PredictAndDrawing(Image<Rgb24> img)
        {
            MyLogger.Log.Debug("开始执行 PredictAndDrawing 同步推理与可视化");
            try
            {
                var result = visualizeHandler.ExecuteDrawing(model.Predict(img), img, new VisualizeOptions(1.0f));
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
        /// Performs asynchronous inference
        /// 执行异步推理
        /// </summary>
        public async Task<Result[]> PredictAsync(Image<Rgb24> img)
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
        /// Performs asynchronous inference with visualization
        /// 执行带可视化的异步推理
        /// </summary>
        public async Task<Image<Rgb24>> PredictAsyncAndDrawing(Image<Rgb24> img)
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
