using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yolov8ObbModelSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Yolov8ObbConfig config = new Yolov8ObbConfig(@"E:\Model\Yolo\yolov8s-obb.onnx");
            config.SetTargetInferenceBackend(InferenceBackend.OpenVINO);
            config.SetTargetDeviceType(DeviceType.CPU);

            Yolov8ObbModel yolov8Model = new Yolov8ObbModel(config);
            Mat img = Cv2.ImRead(@"E:\Data\image\plane.png");
            ObbResult result = (ObbResult)yolov8Model.Predict(img);
            Stopwatch sw = Stopwatch.StartNew();
            result = (ObbResult)yolov8Model.Predict(img);
            sw.Stop();
            Console.WriteLine($"The infer time : {sw.ElapsedMilliseconds} ms");
            Cv2.ImShow("image", Visualize.DrawObbResult(result, img));
            Cv2.WaitKey(0);
        }
    }
}
