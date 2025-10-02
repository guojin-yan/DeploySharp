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
using System.Threading;
using DeploySharp.Log;
using static DeploySharp.Data.Visualize;
using DeploySharp.Common;

namespace DeploySharp.ImageSharp_DemoPlatform
{
    public partial class MainForm : Form
    {


        private IModel model;
        private VisualizeHandler visualizeHandler;

        ModelType[] enumModelTypeValues = (ModelType[])Enum.GetValues(typeof(ModelType));
        InferenceBackend[] enumInferenceBackendValues = (InferenceBackend[])Enum.GetValues(typeof(InferenceBackend));
        DeviceType[] enumDeviceTypeValues = (DeviceType[])Enum.GetValues(typeof(DeviceType));
        OnnxRuntimeDeviceType[] enumOnnxRuntimeDeviceTypeValues = (OnnxRuntimeDeviceType[])Enum.GetValues(typeof(OnnxRuntimeDeviceType));

        public MainForm()
        {
            InitializeComponent();
        }
      
        private void buttonInfer_Click(object sender, EventArgs e)
        {
            if (model is null) 
            {
                MessageBox.Show("Please load model first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string imagePath = tbImagePath.Text.Trim();
            if (!File.Exists(imagePath))
            {
                MessageBox.Show("Please select a valid image file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var image = Image.Load(imagePath))
            {
                Result[] result = model.Predict(image);
                Image<Rgb24> resultImg = visualizeHandler.ExecuteDrawing(result, image as Image<Rgb24>, new VisualizeOptions(1.0f));
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
                tbInferTime.AppendText(model.ModelInferenceProfiler.PrintStatistics());
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        { 
            string[] enumModelTypeStrings = Enum.GetNames(typeof(ModelType));
            foreach (var enumString in enumModelTypeStrings) 
            {
                comboBoxModelType.Items.Add(enumString);
            }
            comboBoxModelType.SelectedIndex = 0;

            string[] enumInferenceBackendStrings = Enum.GetNames(typeof(InferenceBackend));
            foreach (var enumString in enumInferenceBackendStrings)
            {
                comboEngineType.Items.Add(enumString);
            }
            comboEngineType.SelectedIndex = 0;

            string[] enumDeviceTypeStrings = Enum.GetNames(typeof(DeviceType));
            foreach (var enumString in enumDeviceTypeStrings)
            {
                comboBoxDeviceType.Items.Add(enumString);
            }
            comboBoxDeviceType.SelectedIndex = 0;

            string[] enumOnnxRuntimeDeviceTypeStrings = Enum.GetNames(typeof(OnnxRuntimeDeviceType));
            foreach (var enumString in enumOnnxRuntimeDeviceTypeStrings)
            {
                comboBoxONNXType.Items.Add(enumString);
            }
            comboBoxONNXType.SelectedIndex = 0;
            
        }

        private void buttonSelectModelPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.InitialDirectory = "E:\\Model\\yolo";
            dlg.Title = "Select inference model file.";
            dlg.Filter = "Model file(*.pdmodel,*.onnx,*.xml,*.engine)|*.pdmodel;*.onnx;*.xml;*.engine";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbModelPath.Text = dlg.FileName;
            }
        }

        private void buttonSelectImagePath_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.InitialDirectory = "E:\\Data\\image";
            dlg.Title = "Select the test input file.";
            dlg.Filter = "Input file(*.png,*.jpg,*.jepg,*.mp4,*.mov)|*.png;*.jpg;*.jepg;*.mp4;*.mov";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbImagePath.Text = dlg.FileName;
            }
        }

        private void buttonLoadModel_Click(object sender, EventArgs e)
        {
            model?.Dispose();

            string modelPath = tbModelPath.Text.Trim();
            if(!File.Exists(modelPath))
            {
                MessageBox.Show("Please select a valid model file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ModelType modelType = enumModelTypeValues[comboBoxModelType.SelectedIndex];

            InferenceBackend inferenceBackend = enumInferenceBackendValues[comboEngineType.SelectedIndex];
            DeviceType deviceType = enumDeviceTypeValues[comboBoxDeviceType.SelectedIndex];
            OnnxRuntimeDeviceType onnxType = enumOnnxRuntimeDeviceTypeValues[comboBoxONNXType.SelectedIndex];
            switch (modelType)
            {
                case ModelType.YOLOv5Det:
                    model = new Yolov5DetModel(new Yolov5DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov5DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv5Seg:
                    model = new Yolov5SegModel(new Yolov5SegConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov5SegConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                    break;
                case ModelType.YOLOv6Det:
                    model = new Yolov6DetModel(new Yolov6DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov6DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv7Det:
                    model = new Yolov7DetModel(new Yolov7DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov7DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv8Det:
                    model = new Yolov8DetModel(new Yolov8DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov8DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv8Seg:
                    model = new Yolov8SegModel(new Yolov8SegConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov8SegConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                    break;
                case ModelType.YOLOv8Obb:
                    model = new Yolov8ObbModel(new Yolov8ObbConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov8ObbConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                    break;
                case ModelType.YOLOv8Pose:
                    model = new Yolov8PoseModel(new Yolov8PoseConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov8PoseConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                    break;
                case ModelType.YOLOv9Det:
                    model = new Yolov9DetModel(new Yolov9DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov9DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv9Seg:
                    model = new Yolov9SegModel(new Yolov9SegConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov9SegConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                    break;
                case ModelType.YOLOv10Det:
                    model = new Yolov10DetModel(new Yolov10DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov10DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv11Det:
                    model = new Yolov11DetModel(new Yolov11DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov11DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv11Seg:
                    model = new Yolov11SegModel(new Yolov11SegConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov11SegConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                    break;
                case ModelType.YOLOv11Obb:
                    model = new Yolov11ObbModel(new Yolov11ObbConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov11ObbConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawObbResult);
                    break;
                case ModelType.YOLOv11Pose:
                    model = new Yolov11PoseModel(new Yolov11PoseConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov11PoseConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawPoses);
                    break;
                case ModelType.YOLOv12Det:
                    model = new Yolov12DetModel(new Yolov12DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov12DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.YOLOv13Det:
                    model = new Yolov13DetModel(new Yolov13DetConfig(modelPath, inferenceBackend, deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as Yolov13DetConfig);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawDetResult);
                    break;
                case ModelType.AnomalibSeg:
                    AnomalibSegConfig config = new AnomalibSegConfig(modelPath: modelPath, inferenceBackend: inferenceBackend, deviceType: deviceType).SetTargetOnnxRuntimeDeviceType(onnxType) as AnomalibSegConfig;
                    config.InputSizes.Add(new int[4] { 1, 3, 256, 256 });
                    model = new AnomalibSegModel(config);
                    visualizeHandler = new VisualizeHandler(Visualize.DrawSegResult);
                    break;
                default:
                    string errorMsg = $"{modelType.ToString()} model is currently not supported, please wait for further development support.";
     
                    throw new DeploySharpException(errorMsg);
            }

            MessageBox.Show("Model load success!.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
