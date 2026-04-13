using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using SixLabors.ImageSharp;

namespace bria_rmbg_v2._0_imagesharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\RMBG\RMBG-2.0.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Data\image\boy.jpg";


            BriaRmbgConfig config = new BriaRmbgConfig(BriaRmbgConfig.BriaRmbgVersion.V2_0, modelPath);
            //config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime); // 可选：指定推理后端，默认为OpenVINO
            //config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.DML); // 可选：指定ONNX Runtime推理设备，默认为CPU
            //config.SetTargetDeviceType(DeviceType.GPU0); // 可选：指定推理设备，默认为CPU
            config.InputSizes.Add(new int[] { 1, 3, 1024, 1024 }); // 可选：指定输入尺寸，默认为模型的默认输入尺寸
            config.OutputSizes.Add(new int[] { 1, 1, 1024, 1024 }); // 可选：指定输入尺寸，默认为模型的默认输入尺寸
            BriaRmbgModel model = new BriaRmbgModel(config);
            var img = Image.Load(imagePath);

            SegResult[] result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            //List<Mat> resultsMat = new List<Mat>();

            //var resultImg = Visualize.DrawDetResult(result, img, new VisualizeOptions(1.0f));
            //resultsMat.Add(resultImg);
            var resultImg = result[0].ByteMask.ToImage();
            resultImg.Save(@$"./result_{ModelType.BriaRmbg.ToString()}.jpg");

            var im = BriaRmbgModel.MergeWithMask(img, result[0]);
            im.Save(@$"./merge_result_{ModelType.BriaRmbg.ToString()}.jpg");
        }
    }
}
