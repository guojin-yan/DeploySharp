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
    public class Pipeline : IDisposable
    {

    
        private IModel model;
        //private ModelType modelType;
        private VisualizeHandler visualizeHandler;
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
