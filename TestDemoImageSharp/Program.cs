using System.Diagnostics;
using System;
using DeploySharp.Model;
using DeploySharp.Engine;
using DeploySharp.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace TestDemoImageSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            Yolov5DetConfig config = new Yolov5DetConfig(@"E:\Model\Yolo\yolov5s.onnx");
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            //config.SetTargetDeviceType(DeviceType.GPU0);
            Yolov5DetModel yolov5Model = new Yolov5DetModel(config);
            VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);

            // 1. 使用ImageSharp加载图像
            using (var image = Image.Load(@"E:\Data\image\demo_2.jpg"))
            {
                DetResult[] result = yolov5Model.Predict(image);
                Image<Rgb24> resultImg = Visualize.DrawDetResult(result, image as Image<Rgb24>, visualizeOptions);
                resultImg.Save(@"E:\Data\image\result.jpg");
            }
        }
    }
}
