using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;

namespace BriaRmbg_v1._4
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 模型和测试图片可以前往QQ群(945057948)下载
            // 将下面的模型路径替换为你自己的模型路径
            string modelPath = @"E:\Model\RMBG\bria-rmbg-1.4.onnx";
            // 将下面的图片路径替换为你自己的图片路径
            string imagePath = @"E:\Data\image\demo_2.jpg";


            BriaRmbgConfig config = new BriaRmbgConfig(BriaRmbgConfig.BriaRmbgVersion.V1_4, modelPath);
            config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime); // 可选：指定推理后端，默认为OpenVINO
            config.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.DML); // 可选：指定ONNX Runtime推理设备，默认为CPU
            config.SetTargetDeviceType(DeviceType.GPU0); // 可选：指定推理设备，默认为CPU
            config.InputSizes.Add(new int[] { 1, 3, 1024, 1024 });
            config.OutputSizes.Add(new int[] { 1, 1, 1024, 1024 }); 
            BriaRmbgModel model = new BriaRmbgModel(config);
            Mat img = Cv2.ImRead(imagePath);

            SegResult[] result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            result = model.Predict(img);
            model.ModelInferenceProfiler.PrintAllRecords();
            Mat resultImg = result[0].ByteMask.ToMat();
            Mat im = BriaRmbgModel.MergeWithMask(img, result[0]);
            Cv2.ImShow("image", resultImg);

            Cv2.ImShow("image1", im);
            Cv2.WaitKey(0);
        }
    }
}
