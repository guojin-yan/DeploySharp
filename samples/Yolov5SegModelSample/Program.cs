using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;
using System.Diagnostics;

namespace Yolov5SegModelSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Yolov8SegConfig config = new Yolov8SegConfig(@"E:\Model\Yolo\yolov5s-seg.onnx");
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            config.SetTargetDeviceType(DeviceType.CPU);
            //config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.OpenVINO);

            Yolov5SegModel yolov5Model = new Yolov5SegModel(config);
            Mat img = Cv2.ImRead(@"E:\Data\image\bus.jpg");
            SegResult result = (SegResult)yolov5Model.Predict(img);
            Stopwatch sw = Stopwatch.StartNew();
            result = (SegResult)yolov5Model.Predict(img);
            sw.Stop();
            Console.WriteLine($"The infer time : {sw.ElapsedMilliseconds} ms");
            Cv2.ImShow("image", Visualize.DrawSegResult(result, img));
            Cv2.WaitKey(0);
        }
    }
}
