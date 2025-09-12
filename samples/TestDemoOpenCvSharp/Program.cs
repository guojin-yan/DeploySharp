using OpenCvSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using DeploySharp.Model;
using DeploySharp.Data;
using DeploySharp.Engine;
namespace TestDemoOpenCvSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            //Yolov5DetConfig config = new Yolov5DetConfig(@"E:\Model\Yolo\yolov5s.onnx");
            //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            ////config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov5DetModel yolov5Model = new Yolov5DetModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            //{
            //    Result[] result = yolov5Model.Predict(image);
            //    Stopwatch sw = Stopwatch.StartNew();
            //    sw.Start();
            //    //Task<Result[]> result = yolov5Model.PredictAsync(image);
            //    result = yolov5Model.Predict(image);
            //    sw.Stop();
            //    Console.WriteLine($"Yolov5DetConfig run {sw.ElapsedMilliseconds.ToString()}");

            //    Mat resultImg = Visualize.DrawDetResult(result, image, visualizeOptions);
            //    Cv2.ImShow("result", resultImg);
            //    Cv2.WaitKey(0);
            //}

            //Yolov5SegConfig config = new Yolov5SegConfig(@"E:\Model\Yolo\yolov5s-seg.onnx");
            ////config.InputSizes.Add(new int[4] { 1,3,640,640});
            ////config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            ////config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov5SegModel yolov8Model = new Yolov5SegModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            //{
            //    Result[] result = yolov8Model.Predict(image);
            //    Stopwatch sw = Stopwatch.StartNew();
            //    sw.Start();
            //    //Task<Result[]> result = yolov5Model.PredictAsync(image);
            //    result = yolov8Model.Predict(image);
            //    sw.Stop();
            //    Console.WriteLine($"Yolov5DetConfig run {sw.ElapsedMilliseconds.ToString()}");

            //    Mat resultImg = Visualize.DrawSegResult(result, image, visualizeOptions);
            //    Cv2.ImShow("result", resultImg);
            //    Cv2.WaitKey(0);
            //}

            Yolov8DetConfig config = new Yolov8DetConfig(@"E:\Model\Yolo\yolov8n.onnx");
            //config.InputSizes.Add(new int[4] { 1,3,640,640});
            //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            //config.SetTargetDeviceType(DeviceType.GPU0);
            Yolov8DetModel yolov8Model = new Yolov8DetModel(config);
            VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            // 1. 使用ImageSharp加载图像
            using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            {
                Result[] result = yolov8Model.Predict(image);
                for (int i = 0; i < 50; i++)
                {
                    result = yolov8Model.Predict(image);
                }
                //Task<Result[]> result = yolov5Model.PredictAsync(image);



                yolov8Model.ModelInferenceProfiler.PrintAllRecords();
                yolov8Model.ModelInferenceProfiler.PrintSummary();
                yolov8Model.ModelInferenceProfiler.PrintStatistics();
                Mat resultImg = Visualize.DrawDetResult(result, image, visualizeOptions);
                Cv2.ImShow("result", resultImg);
                Cv2.WaitKey(0);
            }

            //Yolov8SegConfig config = new Yolov8SegConfig(@"E:\Model\Yolo\yolov8s-seg.onnx");
            ////config.InputSizes.Add(new int[4] { 1,3,640,640});
            ////config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            ////config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov8SegModel yolov8Model = new Yolov8SegModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            //{
            //    SegResult[] result = yolov8Model.Predict(image);
            //    for(int i = 0; i < 10; i++)
            //    {
            //        result = yolov8Model.Predict(image);
            //    }
            //    //Task<Result[]> result = yolov5Model.PredictAsync(image);
                


            //    yolov8Model.ModelInferenceProfiler.PrintAllRecords();
            //    yolov8Model.ModelInferenceProfiler.PrintSummary();
            //    yolov8Model.ModelInferenceProfiler.PrintStatistics();
            //    Mat resultImg = Visualize.DrawSegResult(result, image, visualizeOptions);

            //    Mat m = Mat.FromPixelData(result[0].Mask.Height, result[0].Mask.Width, MatType.CV_32FC1, result[0].Mask.GetRawFloatData());
            //    Cv2.ImShow("result", resultImg);
            //    Cv2.WaitKey(0);
            //}
        }
    }
}
