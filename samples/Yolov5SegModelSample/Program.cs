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
            Yolov5SegConfig config = new Yolov5SegConfig(@"E:\Model\Yolo\yolov5s-seg-1.onnx");
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            config.SetTargetDeviceType(DeviceType.CPU);
            //config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.OpenVINO);

            Yolov5SegModel yolov5Model = new Yolov5SegModel(config);
            Mat img = Cv2.ImRead(@"E:\Data\image\demo_12.jpg");
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
