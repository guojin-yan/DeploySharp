using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Model;
using OpenCvSharp;
using System.Diagnostics;

namespace DeploySharp.OpenCvSharp_DemoPlatform
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Yolov5DetConfig config = new Yolov5DetConfig(@"E:\Model\Yolo\yolov5n.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            Yolov5DetModel yolov5Model = new Yolov5DetModel(config);
            VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            // 1. 使用ImageSharp加载图像
            using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            {
                Stopwatch sw = Stopwatch.StartNew();
                //Task<Result[]> result = yolov5Model.PredictAsync(image);
                Result[] result = yolov5Model.Predict(image);
                sw.Stop();
                textBox1.Text = sw.ElapsedMilliseconds.ToString();
                Mat resultImg = Visualize.DrawDetResult(result, image, visualizeOptions);

                pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ImageResizeMode.Pad));
            }



            //Yolov5SegConfig config = new Yolov5SegConfig(@"E:\Model\Yolo\yolov5s-seg.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov5SegModel yolov5Model = new Yolov5SegModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    SegResult[] result = yolov5Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawSegResult(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ResizeMode.Pad));
            //}


            //Yolov8DetConfig config = new Yolov8DetConfig(@"E:\Model\Yolo\yolov8s_b.onnx");
            ////config.SetTargetInferenceBackend(InferenceBackend.OnnxRuntime);
            //Yolov8DetModel yolov8Model = new Yolov8DetModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\bus.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    DetResult[] result = yolov8Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawDetResult(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ImageResizeMode.Pad));
            //}
            //yolov8Model.Dispose();

            //Yolov8SegConfig config = new Yolov8SegConfig(@"E:\Model\Yolo\yolov8s-seg.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov8SegModel yolov8Model = new Yolov8SegModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\demo_2.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    SegResult[] result = yolov8Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawSegResult(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ImageResizeMode.Pad));
            //}
            //yolov8Model.Dispose();



            //Yolov8ObbConfig config = new Yolov8ObbConfig(@"E:\Model\Yolo\yolov8s-obb.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov8ObbModel yolov8Model = new Yolov8ObbModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\plane.png"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    ObbResult[] result = yolov8Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawObbResult(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ResizeMode.Pad));
            //}
            //yolov8Model.Dispose();

            //Yolov8PoseConfig config = new Yolov8PoseConfig(@"E:\Model\Yolo\yolov8s-pose.onnx");
            //config.SetTargetDeviceType(DeviceType.CPU);
            //Yolov8PoseModel yolov8Model = new Yolov8PoseModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\demo_9.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    KeyPointResult[] result = yolov8Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawPoses(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ImageResizeMode.Pad));
            //}
            //yolov8Model.Dispose();


            //AnomalibConfig config = new AnomalibConfig(@"E:\Model\anomalib\modelseg\onnx\model.onnx", @"E:\Model\anomalib\modelseg\onnx\metadata.json");
            //config.SetTargetDeviceType(DeviceType.CPU);
            //AnomalibSegModel anmolibModel = new AnomalibSegModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Model\anomalib\modelseg\testimages\glue\002.png"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    SegResult[] result = anmolibModel.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawPoses(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ImageResizeMode.Pad));
            //}
            //anmolibModel.Dispose();
        }
    }
}
