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
            //Yolov5DetConfig config = new Yolov5DetConfig(@"E:\Model\Yolo\yolov5s.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov5DetModel yolov5Model = new Yolov5DetModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Cv2.ImRead(@"E:\Data\image\bus.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    DetResult[] result = yolov5Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Mat resultImg = Visualize.DrawDetResult(result, image, visualizeOptions);

            //    pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ResizeMode.Pad));
            //}



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


            Yolov8DetConfig config = new Yolov8DetConfig(@"E:\Model\Yolo\yolov8s.onnx");
            config.SetTargetDeviceType(DeviceType.GPU0);
            Yolov8DetModel yolov8Model = new Yolov8DetModel(config);
            VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            // 1. 使用ImageSharp加载图像
            using (var image = Cv2.ImRead(@"E:\Data\image\bus.jpg"))
            {
                Stopwatch sw = Stopwatch.StartNew();
                DetResult[] result = yolov8Model.Predict(image);
                sw.Stop();
                textBox1.Text = sw.ElapsedMilliseconds.ToString();
                Mat resultImg = Visualize.DrawDetResult(result, image, visualizeOptions);

                pictureBox1.BackgroundImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(CvDataProcessor.Resize(resultImg, new Data.Size(pictureBox1.Width, pictureBox1.Height), ResizeMode.Pad));
            }
            yolov8Model.Dispose();



        }
    }
}
