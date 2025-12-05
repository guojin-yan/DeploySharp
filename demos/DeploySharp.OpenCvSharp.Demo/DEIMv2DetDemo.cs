using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;

namespace DeploySharp.ImageSharp.Demo
{
    public class DEIMv2DetDemo
    {
        public static void Run()
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\DEIMv2\DEIMv2.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Model\DEIMv2\170.jpg";

            DEIMv2DetConfig config = new DEIMv2DetConfig(modelPath);
            //config.InputSizes.Add(new int[] { 2, 3, 640, 640 });
            //config.InputSizes.Add(new int[] { 1, 2 });
            //config.SetTargetDeviceType(DeviceType.GPU0);
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            DEIMv2DetModel model = new DEIMv2DetModel(config);
            Mat img = Cv2.ImRead(imagePath);
            var result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            var resultImg = Visualize.DrawDetResult(result, img, new VisualizeOptions(1.0f));

            Cv2.ImShow("image", resultImg);
            Cv2.WaitKey();
        }
    }
}
