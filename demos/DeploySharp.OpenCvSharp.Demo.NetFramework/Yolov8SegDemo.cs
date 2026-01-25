using DeploySharp.Data;
using DeploySharp.Model;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class Yolov8SegDemo
{
    public static void Run()
    {
        // 模型和测试图片可以前往QQ群(945057948)下载
        // 将下面的模型路径替换为你自己的模型路径
        string modelPath = @"E:\Model\Yolo\yolov8s-seg.onnx";
        // 将下面的图片路径替换为你自己的图片路径
        string imagePath = @"E:\Data\image\bus.jpg";

        Yolov8SegConfig config = new Yolov8SegConfig(modelPath);
        //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
        Yolov8SegModel model = new Yolov8SegModel(config);
        Mat img = Cv2.ImRead(imagePath);
        var result = model.Predict(img);
        //result = model.Predict(img);
        //result = model.Predict(img);
        //result = model.Predict(img);
        model.ModelInferenceProfiler.PrintAllRecords();
        var resultImg = Visualize.DrawSegResult(result, img, new VisualizeOptions(1.0f));
        Cv2.ImShow("image", resultImg);
        Cv2.WaitKey();
    }
}

