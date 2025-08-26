using DeploySharp.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using DeploySharp.Data;
using SixLabors.ImageSharp.PixelFormats;
using ResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using System.Diagnostics;
using DeploySharp.Engine;

namespace DeploySharp.ImageSharp_DemoPlatform
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void buttonInfer_Click(object sender, EventArgs e)
        {
            //Yolov5DetConfig config = new Yolov5DetConfig(@"E:\Model\Yolo\yolov5s.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov5DetModel yolov5Model = new Yolov5DetModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Image.Load(@"E:\Data\image\demo_2.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    DetResult[] result = yolov5Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Image<Rgb24> resultImg = Visualize.DrawDetResult(result, image as Image<Rgb24>, visualizeOptions);
            //    // 2. 可选：调整图像大小以适应PictureBox（保持比例）
            //    var resizeOptions = new ResizeOptions
            //    {
            //        Size = new Size(pictureBox1.Width, pictureBox1.Height),
            //        Mode = ResizeMode.Pad
            //    };
            //    resultImg.Mutate(x => x.Resize(resizeOptions));

            //    // 3. 将ImageSharp图像转换为Windows Forms可用的Bitmap
            //    using (var memoryStream = new MemoryStream())
            //    {
            //        resultImg.SaveAsBmp(memoryStream); // 保存为BMP格式流
            //        memoryStream.Position = 0;     // 重置流位置

            //        // 4. 创建Bitmap并显示在PictureBox中
            //        pictureBox1.Image?.Dispose();   // 释放旧图像（如果存在）
            //        pictureBox1.Image = new System.Drawing.Bitmap(memoryStream);
            //    }
            //}



            //Yolov5SegConfig config = new Yolov5SegConfig(@"E:\Model\Yolo\yolov5s-seg.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov5SegModel yolov5Model = new Yolov5SegModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //// 1. 使用ImageSharp加载图像
            //using (var image = Image.Load(@"E:\Data\image\bus.jpg"))
            //{
            //    Stopwatch sw = Stopwatch.StartNew();
            //    SegResult[] result = yolov5Model.Predict(image);
            //    sw.Stop();
            //    textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //    Image<Rgb24> resultImg = Visualize.DrawSegResult(result, image as Image<Rgb24>, visualizeOptions);
            //    // 2. 可选：调整图像大小以适应PictureBox（保持比例）
            //    var resizeOptions = new ResizeOptions
            //    {
            //        Size = new Size(pictureBox1.Width, pictureBox1.Height),
            //        Mode = ResizeMode.Pad
            //    };
            //    resultImg.Mutate(x => x.Resize(resizeOptions));

            //    // 3. 将ImageSharp图像转换为Windows Forms可用的Bitmap
            //    using (var memoryStream = new MemoryStream())
            //    {
            //        resultImg.SaveAsBmp(memoryStream); // 保存为BMP格式流
            //        memoryStream.Position = 0;     // 重置流位置

            //        // 4. 创建Bitmap并显示在PictureBox中
            //        pictureBox1.Image?.Dispose();   // 释放旧图像（如果存在）
            //        pictureBox1.Image = new System.Drawing.Bitmap(memoryStream);
            //    }
            //}




            //Yolov8DetConfig config = new Yolov8DetConfig(@"E:\Model\Yolo\yolov8s.onnx");
            //config.SetTargetDeviceType(DeviceType.GPU0);
            //Yolov8DetModel yolov8Model = new Yolov8DetModel(config);
            //VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
            //for (int i = 0; i < 100; ++i)
            //{ 
            //    // 1. 使用ImageSharp加载图像
            //    using (var image = Image.Load(@"E:\Data\image\demo_2.jpg"))
            //    {
            //        Stopwatch sw = Stopwatch.StartNew();
            //        DetResult[] result = yolov8Model.Predict(image);
            //        sw.Stop();
            //        textBox1.Text = sw.ElapsedMilliseconds.ToString();
            //        Image<Rgb24> resultImg = Visualize.DrawDetResult(result, image as Image<Rgb24>, visualizeOptions);
            //        // 2. 可选：调整图像大小以适应PictureBox（保持比例）
            //        var resizeOptions = new ResizeOptions
            //        {
            //            Size = new Size(pictureBox1.Width, pictureBox1.Height),
            //            Mode = ResizeMode.Pad
            //        };
            //        resultImg.Mutate(x => x.Resize(resizeOptions));

            //        // 3. 将ImageSharp图像转换为Windows Forms可用的Bitmap
            //        using (var memoryStream = new MemoryStream())
            //        {
            //            resultImg.SaveAsBmp(memoryStream); // 保存为BMP格式流
            //            memoryStream.Position = 0;     // 重置流位置

            //            // 4. 创建Bitmap并显示在PictureBox中
            //            pictureBox1.Image?.Dispose();   // 释放旧图像（如果存在）
            //            pictureBox1.Image = new System.Drawing.Bitmap(memoryStream);
            //        }
            //    }
            //}

            //yolov8Model.Dispose();


            Yolov8SegConfig config = new Yolov8SegConfig(@"E:\Model\Yolo\yolov8s-seg.onnx");
            config.SetTargetDeviceType(DeviceType.GPU0);
            Yolov8SegModel yolov8Model = new Yolov8SegModel(config);
            VisualizeOptions visualizeOptions = new VisualizeOptions(1.0f);
     
            // 1. 使用ImageSharp加载图像
            using (var image = Image.Load(@"E:\Data\image\demo_2.jpg"))
            {
                Stopwatch sw = Stopwatch.StartNew();
                SegResult[] result = yolov8Model.Predict(image);
                sw.Stop();
                textBox1.Text = sw.ElapsedMilliseconds.ToString();
                Image<Rgb24> resultImg = Visualize.DrawSegResult(result, image as Image<Rgb24>, visualizeOptions);
                // 2. 可选：调整图像大小以适应PictureBox（保持比例）
                var resizeOptions = new ResizeOptions
                {
                    Size = new Size(pictureBox1.Width, pictureBox1.Height),
                    Mode = ResizeMode.Pad
                };
                resultImg.Mutate(x => x.Resize(resizeOptions));

                // 3. 将ImageSharp图像转换为Windows Forms可用的Bitmap
                using (var memoryStream = new MemoryStream())
                {
                    resultImg.SaveAsBmp(memoryStream); // 保存为BMP格式流
                    memoryStream.Position = 0;     // 重置流位置

                    // 4. 创建Bitmap并显示在PictureBox中
                    pictureBox1.Image?.Dispose();   // 释放旧图像（如果存在）
                    pictureBox1.Image = new System.Drawing.Bitmap(memoryStream);
                }
            }
            

            yolov8Model.Dispose();
        }

        private void MainForm_Load(object sender, EventArgs e)
        { 
            string[] enumStrings = Enum.GetNames(typeof(ModelList));
       
            ModelList[] enumValues = (ModelList[])Enum.GetValues(typeof(ModelList));

            foreach (var enumString in enumValues) 
            {
                comboBoxModelType.Items.Add(enumString);
            }
            comboBoxModelType.SelectedIndex = 0;
        }
    }
}
