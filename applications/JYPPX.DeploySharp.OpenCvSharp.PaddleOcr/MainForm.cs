using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using DeploySharp.Model;
using Microsoft.VisualBasic.Logging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;

namespace PaddleOcr.Demo
{
    public partial class MainForm : Form
    {

        PaddleOcrPredictor paddleOcrPredictor = null;
        InferenceBackend[] enumInferenceBackendValues = (InferenceBackend[])Enum.GetValues(typeof(InferenceBackend));
        DeviceType[] enumDeviceTypeValues = (DeviceType[])Enum.GetValues(typeof(DeviceType));
        OnnxRuntimeDeviceType[] enumOnnxRuntimeDeviceTypeValues = (OnnxRuntimeDeviceType[])Enum.GetValues(typeof(OnnxRuntimeDeviceType));

        public MainForm()
        {
            InitializeComponent();
        }

        private void ButtonSelectDetModel_Click(object sender, EventArgs e)
        {
            // 1. 创建 OpenFileDialog 对象
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // 2. 设置对话框标题
            openFileDialog.Title = "请选择模型文件 (ONNX 或 TensorRT Engine)";
            // 3. 设置文件过滤器 (关键步骤)
            // 语法: "显示名称|扩展名1;扩展名2"
            openFileDialog.Filter = "模型文件 (*.onnx;*.engine)|*.onnx;*.engine|" +
                                    "ONNX 文件 (*.onnx)|*.onnx|" +
                                    "TensorRT Engine 文件 (*.engine)|*.engine|" +
                                    "所有文件 (*.*)|*.*";
            // 4. 设置默认过滤器索引 (默认选中第一个: "模型文件")
            openFileDialog.FilterIndex = 1;
            // 5. 恢复上次打开的目录 (可选)
            openFileDialog.RestoreDirectory = true;
            // 6. 显示对话框并检查用户是否点击了"确定"
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 7. 获取选择的文件路径
                TextBoxDetModelPath.Text = openFileDialog.FileName;


            }
        }

        private void ButtonSelectClsModel_Click(object sender, EventArgs e)
        {
            // 1. 创建 OpenFileDialog 对象
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // 2. 设置对话框标题
            openFileDialog.Title = "请选择模型文件 (ONNX 或 TensorRT Engine)";
            // 3. 设置文件过滤器 (关键步骤)
            // 语法: "显示名称|扩展名1;扩展名2"
            openFileDialog.Filter = "模型文件 (*.onnx;*.engine)|*.onnx;*.engine|" +
                                    "ONNX 文件 (*.onnx)|*.onnx|" +
                                    "TensorRT Engine 文件 (*.engine)|*.engine|" +
                                    "所有文件 (*.*)|*.*";
            // 4. 设置默认过滤器索引 (默认选中第一个: "模型文件")
            openFileDialog.FilterIndex = 1;
            // 5. 恢复上次打开的目录 (可选)
            openFileDialog.RestoreDirectory = true;
            // 6. 显示对话框并检查用户是否点击了"确定"
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 7. 获取选择的文件路径
                TextBoxClsModelPath.Text = openFileDialog.FileName;


            }
        }

        private void ButtonSelectRecModel_Click(object sender, EventArgs e)
        {
            // 1. 创建 OpenFileDialog 对象
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // 2. 设置对话框标题
            openFileDialog.Title = "请选择模型文件 (ONNX 或 TensorRT Engine)";
            // 3. 设置文件过滤器 (关键步骤)
            // 语法: "显示名称|扩展名1;扩展名2"
            openFileDialog.Filter = "模型文件 (*.onnx;*.engine)|*.onnx;*.engine|" +
                                    "ONNX 文件 (*.onnx)|*.onnx|" +
                                    "TensorRT Engine 文件 (*.engine)|*.engine|" +
                                    "所有文件 (*.*)|*.*";
            // 4. 设置默认过滤器索引 (默认选中第一个: "模型文件")
            openFileDialog.FilterIndex = 1;
            // 5. 恢复上次打开的目录 (可选)
            openFileDialog.RestoreDirectory = true;
            // 6. 显示对话框并检查用户是否点击了"确定"
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 7. 获取选择的文件路径
                TextBoxRecModelPath.Text = openFileDialog.FileName;
            }
        }


        private void ButtonSelectDict_Click(object sender, EventArgs e)
        {
            // 1. 创建 OpenFileDialog 对象
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // 2. 设置对话框标题
            openFileDialog.Title = "请选择模型文件 (ONNX 或 TensorRT Engine)";
            // 3. 设置文件过滤器 (关键步骤)
            // 语法: "显示名称|扩展名1;扩展名2"
            openFileDialog.Filter = "文本文件 (*.txt)|*.txt|" +
                                    "文本文件 (*.txt)|*.txt|" +
                                    "所有文件 (*.*)|*.*";
            // 4. 设置默认过滤器索引 (默认选中第一个: "模型文件")
            openFileDialog.FilterIndex = 1;
            // 5. 恢复上次打开的目录 (可选)
            openFileDialog.RestoreDirectory = true;
            // 6. 显示对话框并检查用户是否点击了"确定"
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 7. 获取选择的文件路径
                TextBoxDictPath.Text = openFileDialog.FileName;
            }
        }

        private void ButtonSelectImage_Click(object sender, EventArgs e)
        {
            // 1. 创建 OpenFileDialog 对象
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // 2. 设置对话框标题 (修改)
            openFileDialog.Title = "请选择图像文件";

            // 3. 设置文件过滤器 (修改为图片格式)
            // 常见的图片格式包括 jpg, jpeg, png, bmp, gif 等
            openFileDialog.Filter = "图像文件 (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|" +
                                    "JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                                    "PNG 图片 (*.png)|*.png|" +
                                    "位图 (*.bmp)|*.bmp|" +
                                    "所有文件 (*.*)|*.*";

            // 4. 设置默认过滤器索引
            openFileDialog.FilterIndex = 1;

            // 5. 恢复上次打开的目录
            openFileDialog.RestoreDirectory = true;

            // 6. 显示对话框并检查用户是否点击了"确定"
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 7. 获取选择的文件路径并显示在文本框中
                TextBoxImagePath.Text = openFileDialog.FileName;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            string[] enumInferenceBackendStrings = Enum.GetNames(typeof(InferenceBackend));
            foreach (var enumString in enumInferenceBackendStrings)
            {
                ComboEngineType.Items.Add(enumString);
            }
            ComboEngineType.SelectedIndex = 1;

            string[] enumDeviceTypeStrings = Enum.GetNames(typeof(DeviceType));
            foreach (var enumString in enumDeviceTypeStrings)
            {
                ComboBoxDeviceType.Items.Add(enumString);
            }
            ComboBoxDeviceType.SelectedIndex = 2;

            string[] enumOnnxRuntimeDeviceTypeStrings = Enum.GetNames(typeof(OnnxRuntimeDeviceType));
            foreach (var enumString in enumOnnxRuntimeDeviceTypeStrings)
            {
                ComboBoxONNXType.Items.Add(enumString);
            }
            ComboBoxONNXType.SelectedIndex = 3;


            for (int i = 1; i < 9; ++i)
            {
                ComboBoxConcurrency.Items.Add(i.ToString());
            }
            ComboBoxConcurrency.SelectedIndex = 3;

            for (int i = 1; i < 17; ++i) 
            {
                ComboBoxBatchSize.Items.Add(i.ToString());
            }
            ComboBoxBatchSize.SelectedIndex = 3;
        }

        private void ButtonLoadModel_Click(object sender, EventArgs e)
        {
            MyLogger.SetLevel(DeploySharp.Log.LogLevel.ERROR);
            // 1. 获取路径并进行非空检查
            // 逻辑：如果文本框不为空且去除空格后不为空，则使用文本框的值；否则传入 null
            string detPath = string.IsNullOrWhiteSpace(TextBoxDetModelPath.Text) ? null : TextBoxDetModelPath.Text;
            string clsPath = string.IsNullOrWhiteSpace(TextBoxClsModelPath.Text) ? null : TextBoxClsModelPath.Text;
            string recPath = string.IsNullOrWhiteSpace(TextBoxRecModelPath.Text) ? null : TextBoxRecModelPath.Text;
            string dictPath = string.IsNullOrWhiteSpace(TextBoxDictPath.Text) ? null : TextBoxDictPath.Text;
            // 2. 实例化配置对象
            // 注意：这里我们将处理后的变量（可能为 null）传入构造函数
            PaddleOCRConfig oCRConfig = new PaddleOCRConfig(
                detModelPath: detPath,
                clsModelPath: clsPath,
                recModelPath: recPath,
                recDictPath: dictPath
            );
            oCRConfig.GlobalMaxBatchSize = ComboBoxBatchSize.SelectedIndex + 1;
            oCRConfig.MaxConcurrency = ComboBoxConcurrency.SelectedIndex + 1;
            InferenceBackend inferenceBackend = enumInferenceBackendValues[ComboEngineType.SelectedIndex];
            DeviceType deviceType = enumDeviceTypeValues[ComboBoxDeviceType.SelectedIndex];
            OnnxRuntimeDeviceType onnxType = enumOnnxRuntimeDeviceTypeValues[ComboBoxONNXType.SelectedIndex];
            oCRConfig.GlobalInferenceBackend = inferenceBackend;
            oCRConfig.GlobalDeviceType = deviceType;
            oCRConfig.GlobalOnnxRuntimeDeviceType = onnxType;

            if (detPath == null) 
            {
                CheckBoxUseDet.Checked = false;
            }
            if (clsPath == null)
            {
                CheckBoxUseCls.Checked = false;
            }
            if (recPath == null || dictPath == null)
            {
                CheckBoxUseRec.Checked = false;
            }

            if (paddleOcrPredictor != null)
            {
                paddleOcrPredictor.Dispose();
                paddleOcrPredictor = null;
            }
            // 3. 创建预测器
            paddleOcrPredictor = new PaddleOcrPredictor(oCRConfig);
            if (paddleOcrPredictor != null)
            {
                MessageBox.Show("模型加载成功！", "模型提醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        private void ButtonInferImage_Click(object sender, EventArgs e)
        {
            if (paddleOcrPredictor != null)
            {
                // 1. 读取图像
                string imagePath = TextBoxImagePath.Text;
                Mat img = Cv2.ImRead(imagePath);
                // 2. 进行预测
                Stopwatch sw = new Stopwatch();
                OcrResult result = paddleOcrPredictor.Predict(
                    img, 
                    ComboBoxBatchSize.SelectedIndex + 1,
                    CheckBoxUseDet.Checked,
                    CheckBoxUseCls.Checked,
                    CheckBoxUseRec.Checked
                    );
                sw.Start();
                result = paddleOcrPredictor.Predict(
                    img,
                    ComboBoxBatchSize.SelectedIndex + 1,
                    CheckBoxUseDet.Checked,
                    CheckBoxUseCls.Checked,
                    CheckBoxUseRec.Checked
                    );
                sw.Stop();
                // 3. 显示结果
                TextBoxResult.Text = result.ToString();
                Mat resultMat = Visualize.DrawOcrResult(img, result, new VisualizeOptions(1.0f));
                TextBoxTime.Text = $"Inference time: {sw.ElapsedMilliseconds} ms\n" + paddleOcrPredictor.PrintTimeProfiling();
                PictureBoxResult.BackgroundImage = BitmapConverter.ToBitmap(resultMat);
            }
            else
            {
                MessageBox.Show("请先加载模型！", "模型提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ButtonTimeTest_Click(object sender, EventArgs e)
        {
            if (paddleOcrPredictor != null)
            {
                // 1. 读取图像
                string imagePath = TextBoxImagePath.Text;
                Mat img = Cv2.ImRead(imagePath);
                // 2. 进行预测
                Stopwatch sw = new Stopwatch();
                OcrResult result = paddleOcrPredictor.Predict(
                    img,
                    ComboBoxBatchSize.SelectedIndex + 1,
                    CheckBoxUseDet.Checked,
                    CheckBoxUseCls.Checked,
                    CheckBoxUseRec.Checked
                    );
                sw.Start();
                for (int i = 0; i < 10; i++)
                {
                    result = paddleOcrPredictor.Predict(
                     img,
                     ComboBoxBatchSize.SelectedIndex + 1,
                     CheckBoxUseDet.Checked,
                     CheckBoxUseCls.Checked,
                     CheckBoxUseRec.Checked
                     );
                }

                sw.Stop();
                // 3. 显示结果
                TextBoxResult.Text = result.ToString();
                Mat resultMat = Visualize.DrawOcrResult(img, result, new VisualizeOptions(1.0f));
                TextBoxTime.Text = $"Inference time: {sw.ElapsedMilliseconds / 10} ms\n" + paddleOcrPredictor.PrintTimeProfiling();
                PictureBoxResult.BackgroundImage = BitmapConverter.ToBitmap(resultMat);

            }
            else
            {
                MessageBox.Show("请先加载模型！", "模型提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (paddleOcrPredictor != null)
            {
                paddleOcrPredictor.Dispose();
                paddleOcrPredictor = null;
            }
        }
    }
}
