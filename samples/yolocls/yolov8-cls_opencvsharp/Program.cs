using DeploySharp.Data.ResultData;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;

namespace yolov8_cls_opencvsharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\yolo\yolov8s-cls.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Data\image\demo_4.jpg";


            YoloClsConfig config = new YoloClsConfig(modelPath);

            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            //config.CategoryDict = ClassNames.ImageNetClassNames;
            YoloClsModel model = new YoloClsModel(config);
            Mat img = Cv2.ImRead(imagePath);

            ClsResult result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();

            Console.WriteLine(result.ToString());
        }
    }
}
