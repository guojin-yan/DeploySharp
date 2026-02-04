namespace PaddleOcr.Demo
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            label1 = new Label();
            TextBoxDetModelPath = new TextBox();
            ButtonSelectDetModel = new Button();
            label2 = new Label();
            TextBoxClsModelPath = new TextBox();
            ButtonSelectClsModel = new Button();
            label3 = new Label();
            TextBoxRecModelPath = new TextBox();
            ButtonSelectRecModel = new Button();
            ComboEngineType = new ComboBox();
            label4 = new Label();
            label5 = new Label();
            ComboBoxDeviceType = new ComboBox();
            label6 = new Label();
            ComboBoxONNXType = new ComboBox();
            ButtonLoadModel = new Button();
            ButtonInferImage = new Button();
            PictureBoxResult = new PictureBox();
            TextBoxResult = new RichTextBox();
            TextBoxTime = new RichTextBox();
            label7 = new Label();
            TextBoxDictPath = new TextBox();
            ButtonSelectDict = new Button();
            label8 = new Label();
            TextBoxImagePath = new TextBox();
            ButtonSelectImage = new Button();
            ButtonTimeTest = new Button();
            label9 = new Label();
            ComboBoxBatchSize = new ComboBox();
            label10 = new Label();
            CheckBoxUseDet = new CheckBox();
            CheckBoxUseCls = new CheckBox();
            CheckBoxUseRec = new CheckBox();
            label12 = new Label();
            label13 = new Label();
            ComboBoxConcurrency = new ComboBox();
            groupBox1 = new GroupBox();
            label14 = new Label();
            TextBoxDetMaxSize = new TextBox();
            groupBox2 = new GroupBox();
            label16 = new Label();
            label15 = new Label();
            TextBoxClsInputHeight = new TextBox();
            TextBoxClsInputWidth = new TextBox();
            groupBox3 = new GroupBox();
            label17 = new Label();
            label18 = new Label();
            TextBoxRecInputHeight = new TextBox();
            TextBoxRecMaxWidth = new TextBox();
            groupBox4 = new GroupBox();
            label19 = new Label();
            ComboBoxModelVersion = new ComboBox();
            panel1 = new Panel();
            panel2 = new Panel();
            TextBoxResultText = new RichTextBox();
            label20 = new Label();
            label21 = new Label();
            label11 = new Label();
            TextBoxInferTime = new TextBox();
            label22 = new Label();
            ((System.ComponentModel.ISupportInitialize)PictureBoxResult).BeginInit();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            groupBox4.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label1.Location = new Point(22, 142);
            label1.Name = "label1";
            label1.Size = new Size(96, 27);
            label1.TabIndex = 0;
            label1.Text = "Det 模型:";
            // 
            // TextBoxDetModelPath
            // 
            TextBoxDetModelPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxDetModelPath.Location = new Point(124, 139);
            TextBoxDetModelPath.Name = "TextBoxDetModelPath";
            TextBoxDetModelPath.Size = new Size(495, 33);
            TextBoxDetModelPath.TabIndex = 1;
            TextBoxDetModelPath.Text = "./test_demo/PP-OCRv5_mobile_det_onnx.onnx";
            // 
            // ButtonSelectDetModel
            // 
            ButtonSelectDetModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectDetModel.Location = new Point(637, 135);
            ButtonSelectDetModel.Name = "ButtonSelectDetModel";
            ButtonSelectDetModel.Size = new Size(106, 41);
            ButtonSelectDetModel.TabIndex = 2;
            ButtonSelectDetModel.Text = "选择模型";
            ButtonSelectDetModel.UseVisualStyleBackColor = true;
            ButtonSelectDetModel.Click += ButtonSelectDetModel_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label2.Location = new Point(22, 199);
            label2.Name = "label2";
            label2.Size = new Size(90, 27);
            label2.TabIndex = 0;
            label2.Text = "Cls 模型:";
            // 
            // TextBoxClsModelPath
            // 
            TextBoxClsModelPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxClsModelPath.Location = new Point(124, 196);
            TextBoxClsModelPath.Name = "TextBoxClsModelPath";
            TextBoxClsModelPath.Size = new Size(495, 33);
            TextBoxClsModelPath.TabIndex = 1;
            TextBoxClsModelPath.Text = "./test_demo/PP-OCRv5_mobile_cls_onnx.onnx";
            // 
            // ButtonSelectClsModel
            // 
            ButtonSelectClsModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectClsModel.Location = new Point(637, 192);
            ButtonSelectClsModel.Name = "ButtonSelectClsModel";
            ButtonSelectClsModel.Size = new Size(106, 41);
            ButtonSelectClsModel.TabIndex = 2;
            ButtonSelectClsModel.Text = "选择模型";
            ButtonSelectClsModel.UseVisualStyleBackColor = true;
            ButtonSelectClsModel.Click += ButtonSelectClsModel_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label3.Location = new Point(22, 260);
            label3.Name = "label3";
            label3.Size = new Size(91, 27);
            label3.TabIndex = 0;
            label3.Text = "Rec模型:";
            // 
            // TextBoxRecModelPath
            // 
            TextBoxRecModelPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxRecModelPath.Location = new Point(124, 257);
            TextBoxRecModelPath.Name = "TextBoxRecModelPath";
            TextBoxRecModelPath.Size = new Size(495, 33);
            TextBoxRecModelPath.TabIndex = 1;
            TextBoxRecModelPath.Text = "./test_demo/PP-OCRv5_mobile_rec_onnx.onnx";
            // 
            // ButtonSelectRecModel
            // 
            ButtonSelectRecModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectRecModel.Location = new Point(637, 253);
            ButtonSelectRecModel.Name = "ButtonSelectRecModel";
            ButtonSelectRecModel.Size = new Size(106, 41);
            ButtonSelectRecModel.TabIndex = 2;
            ButtonSelectRecModel.Text = "选择模型";
            ButtonSelectRecModel.UseVisualStyleBackColor = true;
            ButtonSelectRecModel.Click += ButtonSelectRecModel_Click;
            // 
            // ComboEngineType
            // 
            ComboEngineType.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboEngineType.FormattingEnabled = true;
            ComboEngineType.Location = new Point(1240, 193);
            ComboEngineType.Name = "ComboEngineType";
            ComboEngineType.Size = new Size(199, 35);
            ComboEngineType.TabIndex = 3;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label4.Location = new Point(1123, 198);
            label4.Name = "label4";
            label4.Size = new Size(97, 27);
            label4.TabIndex = 0;
            label4.Text = "推理工具:";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label5.Location = new Point(1123, 260);
            label5.Name = "label5";
            label5.Size = new Size(97, 27);
            label5.TabIndex = 0;
            label5.Text = "推理设备:";
            // 
            // ComboBoxDeviceType
            // 
            ComboBoxDeviceType.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxDeviceType.FormattingEnabled = true;
            ComboBoxDeviceType.Location = new Point(1240, 255);
            ComboBoxDeviceType.Name = "ComboBoxDeviceType";
            ComboBoxDeviceType.Size = new Size(199, 35);
            ComboBoxDeviceType.TabIndex = 3;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label6.Location = new Point(1102, 317);
            label6.Name = "label6";
            label6.Size = new Size(118, 27);
            label6.TabIndex = 0;
            label6.Text = "ONNX工具:";
            // 
            // ComboBoxONNXType
            // 
            ComboBoxONNXType.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxONNXType.FormattingEnabled = true;
            ComboBoxONNXType.Location = new Point(1240, 314);
            ComboBoxONNXType.Name = "ComboBoxONNXType";
            ComboBoxONNXType.Size = new Size(199, 35);
            ComboBoxONNXType.TabIndex = 3;
            // 
            // ButtonLoadModel
            // 
            ButtonLoadModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonLoadModel.Location = new Point(1545, 120);
            ButtonLoadModel.Name = "ButtonLoadModel";
            ButtonLoadModel.Size = new Size(216, 106);
            ButtonLoadModel.TabIndex = 2;
            ButtonLoadModel.Text = "加载模型";
            ButtonLoadModel.UseVisualStyleBackColor = true;
            ButtonLoadModel.Click += ButtonLoadModel_Click;
            // 
            // ButtonInferImage
            // 
            ButtonInferImage.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonInferImage.Location = new Point(1545, 244);
            ButtonInferImage.Name = "ButtonInferImage";
            ButtonInferImage.Size = new Size(216, 106);
            ButtonInferImage.TabIndex = 2;
            ButtonInferImage.Text = "推理图片";
            ButtonInferImage.UseVisualStyleBackColor = true;
            ButtonInferImage.Click += ButtonInferImage_Click;
            // 
            // PictureBoxResult
            // 
            PictureBoxResult.BackColor = SystemColors.ActiveCaption;
            PictureBoxResult.BackgroundImageLayout = ImageLayout.Zoom;
            PictureBoxResult.Location = new Point(22, 563);
            PictureBoxResult.Name = "PictureBoxResult";
            PictureBoxResult.Size = new Size(679, 591);
            PictureBoxResult.TabIndex = 4;
            PictureBoxResult.TabStop = false;
            // 
            // TextBoxResult
            // 
            TextBoxResult.Location = new Point(1274, 563);
            TextBoxResult.Name = "TextBoxResult";
            TextBoxResult.Size = new Size(584, 299);
            TextBoxResult.TabIndex = 5;
            TextBoxResult.Text = "";
            // 
            // TextBoxTime
            // 
            TextBoxTime.Location = new Point(1274, 917);
            TextBoxTime.Name = "TextBoxTime";
            TextBoxTime.Size = new Size(584, 237);
            TextBoxTime.TabIndex = 5;
            TextBoxTime.Text = "";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label7.Location = new Point(22, 323);
            label7.Name = "label7";
            label7.Size = new Size(94, 27);
            label7.TabIndex = 0;
            label7.Text = "Dict字典:";
            // 
            // TextBoxDictPath
            // 
            TextBoxDictPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxDictPath.Location = new Point(124, 320);
            TextBoxDictPath.Name = "TextBoxDictPath";
            TextBoxDictPath.Size = new Size(495, 33);
            TextBoxDictPath.TabIndex = 1;
            TextBoxDictPath.Text = "./test_demo/ppocrv5_dict.txt";
            // 
            // ButtonSelectDict
            // 
            ButtonSelectDict.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectDict.Location = new Point(637, 316);
            ButtonSelectDict.Name = "ButtonSelectDict";
            ButtonSelectDict.Size = new Size(106, 41);
            ButtonSelectDict.TabIndex = 2;
            ButtonSelectDict.Text = "选择文件";
            ButtonSelectDict.UseVisualStyleBackColor = true;
            ButtonSelectDict.Click += ButtonSelectDict_Click;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label8.Location = new Point(22, 382);
            label8.Name = "label8";
            label8.Size = new Size(97, 27);
            label8.TabIndex = 0;
            label8.Text = "测试图片:";
            // 
            // TextBoxImagePath
            // 
            TextBoxImagePath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxImagePath.Location = new Point(124, 379);
            TextBoxImagePath.Name = "TextBoxImagePath";
            TextBoxImagePath.Size = new Size(495, 33);
            TextBoxImagePath.TabIndex = 1;
            TextBoxImagePath.Text = "./test_demo/demo_1.jpg";
            // 
            // ButtonSelectImage
            // 
            ButtonSelectImage.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectImage.Location = new Point(637, 375);
            ButtonSelectImage.Name = "ButtonSelectImage";
            ButtonSelectImage.Size = new Size(106, 41);
            ButtonSelectImage.TabIndex = 2;
            ButtonSelectImage.Text = "选择图片";
            ButtonSelectImage.UseVisualStyleBackColor = true;
            ButtonSelectImage.Click += ButtonSelectImage_Click;
            // 
            // ButtonTimeTest
            // 
            ButtonTimeTest.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonTimeTest.Location = new Point(1545, 365);
            ButtonTimeTest.Name = "ButtonTimeTest";
            ButtonTimeTest.Size = new Size(216, 106);
            ButtonTimeTest.TabIndex = 2;
            ButtonTimeTest.Text = "时间测试";
            ButtonTimeTest.UseVisualStyleBackColor = true;
            ButtonTimeTest.Click += ButtonTimeTest_Click;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label9.Location = new Point(1112, 436);
            label9.Name = "label9";
            label9.Size = new Size(108, 27);
            label9.TabIndex = 0;
            label9.Text = "BatchSize:";
            // 
            // ComboBoxBatchSize
            // 
            ComboBoxBatchSize.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxBatchSize.FormattingEnabled = true;
            ComboBoxBatchSize.Location = new Point(1240, 431);
            ComboBoxBatchSize.Name = "ComboBoxBatchSize";
            ComboBoxBatchSize.Size = new Size(199, 35);
            ComboBoxBatchSize.TabIndex = 3;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label10.Location = new Point(1472, 523);
            label10.Name = "label10";
            label10.Size = new Size(225, 37);
            label10.TabIndex = 0;
            label10.Text = "详 细 推 理 结 果";
            // 
            // CheckBoxUseDet
            // 
            CheckBoxUseDet.AutoSize = true;
            CheckBoxUseDet.Checked = true;
            CheckBoxUseDet.CheckState = CheckState.Checked;
            CheckBoxUseDet.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            CheckBoxUseDet.Location = new Point(33, 39);
            CheckBoxUseDet.Name = "CheckBoxUseDet";
            CheckBoxUseDet.Size = new Size(125, 31);
            CheckBoxUseDet.TabIndex = 6;
            CheckBoxUseDet.Text = "Detection";
            CheckBoxUseDet.UseVisualStyleBackColor = true;
            // 
            // CheckBoxUseCls
            // 
            CheckBoxUseCls.AutoSize = true;
            CheckBoxUseCls.Checked = true;
            CheckBoxUseCls.CheckState = CheckState.Checked;
            CheckBoxUseCls.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            CheckBoxUseCls.Location = new Point(222, 39);
            CheckBoxUseCls.Name = "CheckBoxUseCls";
            CheckBoxUseCls.Size = new Size(156, 31);
            CheckBoxUseCls.TabIndex = 6;
            CheckBoxUseCls.Text = "Classification";
            CheckBoxUseCls.UseVisualStyleBackColor = true;
            // 
            // CheckBoxUseRec
            // 
            CheckBoxUseRec.AutoSize = true;
            CheckBoxUseRec.Checked = true;
            CheckBoxUseRec.CheckState = CheckState.Checked;
            CheckBoxUseRec.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            CheckBoxUseRec.Location = new Point(443, 39);
            CheckBoxUseRec.Name = "CheckBoxUseRec";
            CheckBoxUseRec.Size = new Size(132, 31);
            CheckBoxUseRec.TabIndex = 6;
            CheckBoxUseRec.Text = "Recognize";
            CheckBoxUseRec.UseVisualStyleBackColor = true;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Font = new Font("Microsoft YaHei UI", 22.2F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label12.Location = new Point(334, 25);
            label12.Name = "label12";
            label12.Size = new Size(1055, 50);
            label12.TabIndex = 0;
            label12.Text = "JYPPX.DeploySharp.OpenCvSharp  PaddleOCR 测试案例 ";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label13.Location = new Point(1123, 379);
            label13.Name = "label13";
            label13.Size = new Size(97, 27);
            label13.TabIndex = 0;
            label13.Text = "并发数量:";
            // 
            // ComboBoxConcurrency
            // 
            ComboBoxConcurrency.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxConcurrency.FormattingEnabled = true;
            ComboBoxConcurrency.Location = new Point(1240, 374);
            ComboBoxConcurrency.Name = "ComboBoxConcurrency";
            ComboBoxConcurrency.Size = new Size(199, 35);
            ComboBoxConcurrency.TabIndex = 3;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(label14);
            groupBox1.Controls.Add(TextBoxDetMaxSize);
            groupBox1.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            groupBox1.Location = new Point(799, 132);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(271, 70);
            groupBox1.TabIndex = 7;
            groupBox1.TabStop = false;
            groupBox1.Text = "Det模型";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label14.Location = new Point(48, 32);
            label14.Name = "label14";
            label14.Size = new Size(102, 27);
            label14.TabIndex = 0;
            label14.Text = "Max Size:";
            // 
            // TextBoxDetMaxSize
            // 
            TextBoxDetMaxSize.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxDetMaxSize.Location = new Point(162, 29);
            TextBoxDetMaxSize.Name = "TextBoxDetMaxSize";
            TextBoxDetMaxSize.Size = new Size(89, 33);
            TextBoxDetMaxSize.TabIndex = 1;
            TextBoxDetMaxSize.Text = "960";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(label16);
            groupBox2.Controls.Add(label15);
            groupBox2.Controls.Add(TextBoxClsInputHeight);
            groupBox2.Controls.Add(TextBoxClsInputWidth);
            groupBox2.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            groupBox2.Location = new Point(799, 222);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(271, 115);
            groupBox2.TabIndex = 7;
            groupBox2.TabStop = false;
            groupBox2.Text = "Cls模型";
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label16.Location = new Point(15, 75);
            label16.Name = "label16";
            label16.Size = new Size(136, 27);
            label16.TabIndex = 0;
            label16.Text = "Input Height:";
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label15.Location = new Point(20, 36);
            label15.Name = "label15";
            label15.Size = new Size(130, 27);
            label15.TabIndex = 0;
            label15.Text = "Input Width:";
            // 
            // TextBoxClsInputHeight
            // 
            TextBoxClsInputHeight.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxClsInputHeight.Location = new Point(162, 72);
            TextBoxClsInputHeight.Name = "TextBoxClsInputHeight";
            TextBoxClsInputHeight.Size = new Size(89, 33);
            TextBoxClsInputHeight.TabIndex = 1;
            TextBoxClsInputHeight.Text = "80";
            // 
            // TextBoxClsInputWidth
            // 
            TextBoxClsInputWidth.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxClsInputWidth.Location = new Point(162, 32);
            TextBoxClsInputWidth.Name = "TextBoxClsInputWidth";
            TextBoxClsInputWidth.Size = new Size(89, 33);
            TextBoxClsInputWidth.TabIndex = 1;
            TextBoxClsInputWidth.Text = "160";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(label17);
            groupBox3.Controls.Add(label18);
            groupBox3.Controls.Add(TextBoxRecInputHeight);
            groupBox3.Controls.Add(TextBoxRecMaxWidth);
            groupBox3.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            groupBox3.Location = new Point(799, 356);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(271, 115);
            groupBox3.TabIndex = 7;
            groupBox3.TabStop = false;
            groupBox3.Text = "Rec模型";
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label17.Location = new Point(15, 75);
            label17.Name = "label17";
            label17.Size = new Size(136, 27);
            label17.TabIndex = 0;
            label17.Text = "Input Height:";
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label18.Location = new Point(30, 35);
            label18.Name = "label18";
            label18.Size = new Size(121, 27);
            label18.TabIndex = 0;
            label18.Text = "Max Width:";
            // 
            // TextBoxRecInputHeight
            // 
            TextBoxRecInputHeight.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxRecInputHeight.Location = new Point(162, 72);
            TextBoxRecInputHeight.Name = "TextBoxRecInputHeight";
            TextBoxRecInputHeight.Size = new Size(89, 33);
            TextBoxRecInputHeight.TabIndex = 1;
            TextBoxRecInputHeight.Text = "48";
            // 
            // TextBoxRecMaxWidth
            // 
            TextBoxRecMaxWidth.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxRecMaxWidth.Location = new Point(162, 32);
            TextBoxRecMaxWidth.Name = "TextBoxRecMaxWidth";
            TextBoxRecMaxWidth.Size = new Size(89, 33);
            TextBoxRecMaxWidth.TabIndex = 1;
            TextBoxRecMaxWidth.Text = "1024";
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(CheckBoxUseCls);
            groupBox4.Controls.Add(CheckBoxUseDet);
            groupBox4.Controls.Add(CheckBoxUseRec);
            groupBox4.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            groupBox4.Location = new Point(22, 428);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(610, 82);
            groupBox4.TabIndex = 7;
            groupBox4.TabStop = false;
            groupBox4.Text = "加载情况";
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label19.Location = new Point(1123, 135);
            label19.Name = "label19";
            label19.Size = new Size(97, 27);
            label19.TabIndex = 0;
            label19.Text = "推理模型:";
            // 
            // ComboBoxModelVersion
            // 
            ComboBoxModelVersion.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxModelVersion.FormattingEnabled = true;
            ComboBoxModelVersion.Location = new Point(1240, 132);
            ComboBoxModelVersion.Name = "ComboBoxModelVersion";
            ComboBoxModelVersion.Size = new Size(199, 35);
            ComboBoxModelVersion.TabIndex = 3;
            ComboBoxModelVersion.SelectedIndexChanged += ComboBoxModelVersion_SelectedIndexChanged;
            // 
            // panel1
            // 
            panel1.BackColor = SystemColors.ControlLight;
            panel1.Location = new Point(0, 89);
            panel1.Name = "panel1";
            panel1.Size = new Size(1898, 10);
            panel1.TabIndex = 8;
            // 
            // panel2
            // 
            panel2.BackColor = SystemColors.ControlLight;
            panel2.Location = new Point(0, 504);
            panel2.Name = "panel2";
            panel2.Size = new Size(1892, 10);
            panel2.TabIndex = 8;
            // 
            // TextBoxResultText
            // 
            TextBoxResultText.Location = new Point(743, 563);
            TextBoxResultText.Name = "TextBoxResultText";
            TextBoxResultText.Size = new Size(516, 591);
            TextBoxResultText.TabIndex = 5;
            TextBoxResultText.Text = "";
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Font = new Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label20.Location = new Point(917, 523);
            label20.Name = "label20";
            label20.Size = new Size(153, 37);
            label20.TabIndex = 0;
            label20.Text = "推 理 结 果";
            // 
            // label21
            // 
            label21.AutoSize = true;
            label21.Font = new Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label21.Location = new Point(1436, 877);
            label21.Name = "label21";
            label21.Size = new Size(297, 37);
            label21.TabIndex = 0;
            label21.Text = "模 型 推 理 性 能 记 录";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new Font("Microsoft YaHei UI", 13.8F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label11.Location = new Point(124, 523);
            label11.Name = "label11";
            label11.Size = new Size(113, 31);
            label11.TabIndex = 0;
            label11.Text = "推理时间:";
            // 
            // TextBoxInferTime
            // 
            TextBoxInferTime.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxInferTime.Location = new Point(244, 524);
            TextBoxInferTime.Name = "TextBoxInferTime";
            TextBoxInferTime.Size = new Size(89, 33);
            TextBoxInferTime.TabIndex = 1;
            TextBoxInferTime.Text = "0";
            // 
            // label22
            // 
            label22.AutoSize = true;
            label22.Font = new Font("Microsoft YaHei UI", 13.8F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label22.Location = new Point(339, 526);
            label22.Name = "label22";
            label22.Size = new Size(46, 30);
            label22.TabIndex = 0;
            label22.Text = "ms";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1872, 1175);
            Controls.Add(panel2);
            Controls.Add(label22);
            Controls.Add(label11);
            Controls.Add(panel1);
            Controls.Add(TextBoxInferTime);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(TextBoxTime);
            Controls.Add(TextBoxResultText);
            Controls.Add(TextBoxResult);
            Controls.Add(PictureBoxResult);
            Controls.Add(ComboBoxConcurrency);
            Controls.Add(ComboBoxBatchSize);
            Controls.Add(ComboBoxONNXType);
            Controls.Add(ComboBoxDeviceType);
            Controls.Add(ComboBoxModelVersion);
            Controls.Add(ComboEngineType);
            Controls.Add(ButtonTimeTest);
            Controls.Add(ButtonInferImage);
            Controls.Add(ButtonLoadModel);
            Controls.Add(ButtonSelectImage);
            Controls.Add(ButtonSelectDict);
            Controls.Add(ButtonSelectRecModel);
            Controls.Add(TextBoxImagePath);
            Controls.Add(ButtonSelectClsModel);
            Controls.Add(TextBoxDictPath);
            Controls.Add(label8);
            Controls.Add(ButtonSelectDetModel);
            Controls.Add(label7);
            Controls.Add(TextBoxRecModelPath);
            Controls.Add(label3);
            Controls.Add(TextBoxClsModelPath);
            Controls.Add(label21);
            Controls.Add(label12);
            Controls.Add(label13);
            Controls.Add(label20);
            Controls.Add(label10);
            Controls.Add(label9);
            Controls.Add(label6);
            Controls.Add(label2);
            Controls.Add(label5);
            Controls.Add(TextBoxDetModelPath);
            Controls.Add(label19);
            Controls.Add(label4);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.Fixed3D;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "JYPPX.DeploySharp.OpenCvSharp  PaddleOCR 测试案例  作者：椒颜皮皮虾   QQ群：945057948";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            ((System.ComponentModel.ISupportInitialize)PictureBoxResult).EndInit();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox TextBoxDetModelPath;
        private Button ButtonSelectDetModel;
        private Label label2;
        private TextBox TextBoxClsModelPath;
        private Button ButtonSelectClsModel;
        private Label label3;
        private TextBox TextBoxRecModelPath;
        private Button ButtonSelectRecModel;
        private ComboBox ComboEngineType;
        private Label label4;
        private Label label5;
        private ComboBox ComboBoxDeviceType;
        private Label label6;
        private ComboBox ComboBoxONNXType;
        private Button ButtonLoadModel;
        private Button ButtonInferImage;
        private PictureBox PictureBoxResult;
        private RichTextBox TextBoxResult;
        private RichTextBox TextBoxTime;
        private Label label7;
        private TextBox TextBoxDictPath;
        private Button ButtonSelectDict;
        private Label label8;
        private TextBox TextBoxImagePath;
        private Button ButtonSelectImage;
        private Button ButtonTimeTest;
        private Label label9;
        private ComboBox ComboBoxBatchSize;
        private Label label10;
        private CheckBox CheckBoxUseDet;
        private CheckBox CheckBoxUseCls;
        private CheckBox CheckBoxUseRec;
        private Label label12;
        private Label label13;
        private ComboBox ComboBoxConcurrency;
        private GroupBox groupBox1;
        private Label label14;
        private TextBox TextBoxDetMaxSize;
        private GroupBox groupBox2;
        private Label label16;
        private Label label15;
        private TextBox TextBoxClsInputHeight;
        private TextBox TextBoxClsInputWidth;
        private GroupBox groupBox3;
        private Label label17;
        private Label label18;
        private TextBox TextBoxRecInputHeight;
        private TextBox TextBoxRecMaxWidth;
        private GroupBox groupBox4;
        private Label label19;
        private ComboBox ComboBoxModelVersion;
        private Panel panel1;
        private Panel panel2;
        private RichTextBox TextBoxResultText;
        private Label label20;
        private Label label21;
        private Label label11;
        private TextBox TextBoxInferTime;
        private Label label22;
    }
}
