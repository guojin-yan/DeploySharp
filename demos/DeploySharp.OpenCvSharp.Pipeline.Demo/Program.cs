using OpenCvSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using DeploySharp.Model;
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp;
namespace TestDemoOpenCvSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            //Pipeline YOLOv5DetPipeline = new Pipeline(ModelType.YOLOv5Det, @"E:\Model\Yolo\yolov5s.onnx");
            //Pipeline YOLOv5SegPipeline = new Pipeline(ModelType.YOLOv5Seg, @"E:\Model\Yolo\yolov5s-seg.onnx");
            //Pipeline YOLOv6DetPipeline = new Pipeline(ModelType.YOLOv6Det, @"E:\Model\Yolo\yolov6s.onnx");
            //Pipeline YOLOv7DetPipeline = new Pipeline(ModelType.YOLOv7Det, @"E:\Model\Yolo\yolov7.onnx");
            //Pipeline YOLOv8DetPipeline = new Pipeline(ModelType.YOLOv8Det, @"E:\Model\Yolo\yolov8s.onnx");
            //Pipeline YOLOv8SegPipeline = new Pipeline(ModelType.YOLOv8Seg, @"E:\Model\Yolo\yolov8s-seg.onnx");
            //Pipeline YOLOv8ObbPipeline = new Pipeline(ModelType.YOLOv8Obb, @"E:\Model\Yolo\yolov8s-obb.onnx");
            //Pipeline YOLOv8PosePipeline = new Pipeline(ModelType.YOLOv8Pose, @"E:\Model\Yolo\yolov8s-pose.onnx");

            //Pipeline YOLOv9DetPipeline = new Pipeline(ModelType.YOLOv9Det, @"E:\Model\Yolo\yolov9s.onnx");
            //Pipeline YOLOv9SegPipeline = new Pipeline(ModelType.YOLOv9Seg, @"E:\Model\Yolo\yolov9-c-seg.onnx");
            //Pipeline YOLOv10DetPipeline = new Pipeline(ModelType.YOLOv10Det, @"E:\Model\Yolo\yolov10s.onnx");
            //Pipeline YOLOv11DetPipeline = new Pipeline(ModelType.YOLOv11Det, @"E:\Model\Yolo\yolo11s.onnx");
            //Pipeline YOLOv11SegPipeline = new Pipeline(ModelType.YOLOv11Seg, @"E:\Model\Yolo\yolov8s-seg.onnx");
            //Pipeline YOLOv11ObbPipeline = new Pipeline(ModelType.YOLOv11Obb, @"E:\Model\Yolo\yolov8s-obb.onnx");
            //Pipeline YOLOv11PosePipeline = new Pipeline(ModelType.YOLOv11Pose, @"E:\Model\Yolo\yolov8s-pose.onnx");
            //Pipeline YOLOv12DetPipeline = new Pipeline(ModelType.YOLOv12Det, @"E:\Model\Yolo\yolo12s.onnx");
            //Pipeline YOLOv13DetPipeline = new Pipeline(ModelType.YOLOv13Det, @"E:\Model\Yolo\yolov13n.onnx");



            Pipeline YOLOv5DetPipeline = new Pipeline(ModelType.YOLOv5Det, @"E:\Model\Yolo\yolov5s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv5SegPipeline = new Pipeline(ModelType.YOLOv5Seg, @"E:\Model\Yolo\yolov5s-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv6DetPipeline = new Pipeline(ModelType.YOLOv6Det, @"E:\Model\Yolo\yolov6s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv7DetPipeline = new Pipeline(ModelType.YOLOv7Det, @"E:\Model\Yolo\yolov7.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8DetPipeline = new Pipeline(ModelType.YOLOv8Det, @"E:\Model\Yolo\yolov8s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8SegPipeline = new Pipeline(ModelType.YOLOv8Seg, @"E:\Model\Yolo\yolov8s-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8ObbPipeline = new Pipeline(ModelType.YOLOv8Obb, @"E:\Model\Yolo\yolov8s-obb.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv8PosePipeline = new Pipeline(ModelType.YOLOv8Pose, @"E:\Model\Yolo\yolov8s-pose.onnx", InferenceBackend.OnnxRuntime);

            Pipeline YOLOv9DetPipeline = new Pipeline(ModelType.YOLOv9Det, @"E:\Model\Yolo\yolov9s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv9SegPipeline = new Pipeline(ModelType.YOLOv9Seg, @"E:\Model\Yolo\yolov9-c-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv10DetPipeline = new Pipeline(ModelType.YOLOv10Det, @"E:\Model\Yolo\yolov10s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11DetPipeline = new Pipeline(ModelType.YOLOv11Det, @"E:\Model\Yolo\yolo11s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11SegPipeline = new Pipeline(ModelType.YOLOv11Seg, @"E:\Model\Yolo\yolov8s-seg.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11ObbPipeline = new Pipeline(ModelType.YOLOv11Obb, @"E:\Model\Yolo\yolov8s-obb.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv11PosePipeline = new Pipeline(ModelType.YOLOv11Pose, @"E:\Model\Yolo\yolov8s-pose.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv12DetPipeline = new Pipeline(ModelType.YOLOv12Det, @"E:\Model\Yolo\yolo12s.onnx", InferenceBackend.OnnxRuntime);
            Pipeline YOLOv13DetPipeline = new Pipeline(ModelType.YOLOv13Det, @"E:\Model\Yolo\yolov13n.onnx", InferenceBackend.OnnxRuntime);


            // 1. 使用ImageSharp加载图像
            using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            {
                Mat YOLOv5DetResult = YOLOv5DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv5Det.ToString()}.jpg", YOLOv5DetResult);
                Mat YOLOv5SegResult = YOLOv5SegPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv5Seg.ToString()}.jpg", YOLOv5SegResult);
                Mat YOLOv6DetResult = YOLOv6DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv6Det.ToString()}.jpg", YOLOv6DetResult);
                Mat YOLOv7DetResult = YOLOv7DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv7Det.ToString()}.jpg", YOLOv7DetResult);
                Mat YOLOv8DetResult = YOLOv8DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv8Det.ToString()}.jpg", YOLOv8DetResult);
                Mat YOLOv8SegResult = YOLOv8SegPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv8Seg.ToString()}.jpg", YOLOv8SegResult);
                Mat YOLOv9DetResult = YOLOv9DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv9Det.ToString()}.jpg", YOLOv9DetResult);
                Mat YOLOv9SegResult = YOLOv9SegPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv9Seg.ToString()}.jpg", YOLOv9SegResult);
                Mat YOLOv10DetResult = YOLOv10DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv10Det.ToString()}.jpg", YOLOv10DetResult);
                Mat YOLOv11DetResult = YOLOv11DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv11Det.ToString()}.jpg", YOLOv11DetResult);
                Mat YOLOv11SegResult = YOLOv11SegPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv11Seg.ToString()}.jpg", YOLOv11SegResult);
                Mat YOLOv12DetResult = YOLOv12DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv12Det.ToString()}.jpg", YOLOv12DetResult);
                Mat YOLOv13DetResult = YOLOv13DetPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv13Det.ToString()}.jpg", YOLOv13DetResult);
            }

            using (var image = Cv2.ImRead(@"E:\Data\image\plane.png"))
            {
                Mat YOLOv8ObbResult = YOLOv8ObbPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv8Obb.ToString()}.jpg", YOLOv8ObbResult);
                Mat YOLOv11ObbResult = YOLOv11ObbPipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv11Obb.ToString()}.jpg", YOLOv11ObbResult);
            }

            // 1. 使用ImageSharp加载图像
            using (var image = Cv2.ImRead(@"E:\Data\image\demo_9.jpg"))
            {
                Mat YOLOv8PoseResult = YOLOv8PosePipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv8Pose.ToString()}.jpg", YOLOv8PoseResult);
                Mat YOLOv11PoseResult = YOLOv11PosePipeline.PredictAndDrawing(image as Mat);
                Cv2.ImWrite(@$"./result/result_{ModelType.YOLOv11Pose.ToString()}.jpg", YOLOv8PoseResult);


            }
        }
    }
}
