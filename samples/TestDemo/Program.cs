using OpenCvSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using DeploySharp.Model;
using DeploySharp.Data;

namespace TestDemo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            Yolov5DetConfig config = new Yolov5DetConfig(@"E:\Model\Yolo\yolov5n.onnx", normalizationType: ImageNormalizationType.ImageNetStandard);
            //config.SetTargetDeviceType(DeviceType.GPU0);
            Yolov5DetModel yolov5Model = new Yolov5DetModel(config);
            VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            // 1. 使用ImageSharp加载图像
            using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            {
                Result[] result = yolov5Model.Predict(image);
                Stopwatch sw = Stopwatch.StartNew();
                sw.Start();
                //Task<Result[]> result = yolov5Model.PredictAsync(image);
                result = yolov5Model.Predict(image);
                sw.Stop();
                Console.WriteLine($"Yolov5DetConfig run {sw.ElapsedMilliseconds.ToString()}");
            }

        }
    }
}
