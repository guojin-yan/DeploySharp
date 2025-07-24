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
            Console.WriteLine("Hello, World,This is a test!");
            Yolov8Config config = new Yolov8Config(@"C:\Users\G56827\Desktop\models\detect\yolov8n.onnx");
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            config.SetTargetDeviceType(DeviceType.CPU);
            config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.OpenVINO);

            Yolov8Model yolov8Model = new Yolov8Model(config);
            Mat img = Cv2.ImRead(@"C:\Users\G56827\Desktop\models\detect\test.jpg");
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
