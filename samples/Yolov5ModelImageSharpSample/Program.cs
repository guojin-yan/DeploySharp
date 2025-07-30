using System.Diagnostics;
using System;
using DeploySharp.Model;

using DeploySharp.Engine;
using DeploySharp.Data;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Yolov5ModelImageSharpSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Yolov5Config config = new Yolov5Config(@"E:\Model\Yolo\yolov5s.onnx");
            //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            //config.SetTargetDeviceType(DeviceType.CPU);
            //config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.OpenVINO);

            Yolov5Model yolov5Model = new Yolov5Model(config);
            Image<Rgb24> img = Image.Load<Rgb24>(@"E:\Data\image\bus.jpg");
            DetResult result = (DetResult)yolov5Model.Predict(img);
            Stopwatch sw = Stopwatch.StartNew();
            result = (DetResult)yolov5Model.Predict(img);
            sw.Stop();
            Console.WriteLine($"The infer time : {sw.ElapsedMilliseconds} ms");
            //Cv2.ImShow("image", Visualize.DrawDetResult(result, img));
            //Cv2.WaitKey(0);
        }


    }
}
