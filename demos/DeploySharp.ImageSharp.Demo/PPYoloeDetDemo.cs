using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.ImageSharp.Demo
{
    public class PPYoloeDetDemo
    {
        public static void Run()
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\ppyoloe_plus_crn_s_80e.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            //string imagePath = @"E:\Model\rf-detr\scratches_125.jpg";
            string imagePath = @"E:\Data\image\bus.jpg";

            PPYoloeDetConfig config = new PPYoloeDetConfig(modelPath);
            config.InputSizes.Add(new int[] { 1, 3, 640, 640 });
            config.InputSizes.Add(new int[] { 1, 2 });
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            PPYoloeDetModel model = new PPYoloeDetModel(config);
            var img = Image.Load(imagePath);
            var result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            var resultImg = Visualize.DrawDetResult(result, img as Image<Rgb24>, new VisualizeOptions(1.0f));
            resultImg.Save(@$"./result_{ModelType.PPYOLOETDet.ToString()}.jpg");
        }
    }
}
