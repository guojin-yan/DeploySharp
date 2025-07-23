using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;
using System.Diagnostics;

namespace Yolov8ModelSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Yolov8Config config = new Yolov8Config(@"E:\Model\Yolo\yolov8s.onnx");
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            config.SetTargetDeviceType(DeviceType.CPU);
            config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.OpenVINO);

            Yolov8Model yolov8Model = new Yolov8Model(config);
            Mat img = Cv2.ImRead(@"E:\Data\image\bus.jpg");
            DetResult result = (DetResult)yolov8Model.Predict(img);
            Stopwatch sw = Stopwatch.StartNew();
            result = (DetResult)yolov8Model.Predict(img);
            sw.Stop();
            Console.WriteLine($"The infer time : {sw.ElapsedMilliseconds} ms");
            Cv2.ImShow("image", Visualize.DrawDetResult(result, img));
            Cv2.WaitKey(0);

        }
    }
}
