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
            label11 = new Label();
            CheckBoxUseDet = new CheckBox();
            CheckBoxUseCls = new CheckBox();
            CheckBoxUseRec = new CheckBox();
            label12 = new Label();
            label13 = new Label();
            ComboBoxConcurrency = new ComboBox();
            ((System.ComponentModel.ISupportInitialize)PictureBoxResult).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label1.Location = new Point(37, 109);
            label1.Name = "label1";
            label1.Size = new Size(96, 27);
            label1.TabIndex = 0;
            label1.Text = "Det 模型:";
            // 
            // TextBoxDetModelPath
            // 
            TextBoxDetModelPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxDetModelPath.Location = new Point(139, 106);
            TextBoxDetModelPath.Name = "TextBoxDetModelPath";
            TextBoxDetModelPath.Size = new Size(495, 33);
            TextBoxDetModelPath.TabIndex = 1;
            TextBoxDetModelPath.Text = "./test_demo/PP-OCRv5_mobile_det_onnx.onnx";
            // 
            // ButtonSelectDetModel
            // 
            ButtonSelectDetModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectDetModel.Location = new Point(652, 102);
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
            label2.Location = new Point(37, 166);
            label2.Name = "label2";
            label2.Size = new Size(90, 27);
            label2.TabIndex = 0;
            label2.Text = "Cls 模型:";
            // 
            // TextBoxClsModelPath
            // 
            TextBoxClsModelPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxClsModelPath.Location = new Point(139, 163);
            TextBoxClsModelPath.Name = "TextBoxClsModelPath";
            TextBoxClsModelPath.Size = new Size(495, 33);
            TextBoxClsModelPath.TabIndex = 1;
            TextBoxClsModelPath.Text = "./test_demo/PP-OCRv5_mobile_cls_onnx.onnx";
            // 
            // ButtonSelectClsModel
            // 
            ButtonSelectClsModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectClsModel.Location = new Point(652, 159);
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
            label3.Location = new Point(37, 227);
            label3.Name = "label3";
            label3.Size = new Size(91, 27);
            label3.TabIndex = 0;
            label3.Text = "Rec模型:";
            // 
            // TextBoxRecModelPath
            // 
            TextBoxRecModelPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxRecModelPath.Location = new Point(139, 224);
            TextBoxRecModelPath.Name = "TextBoxRecModelPath";
            TextBoxRecModelPath.Size = new Size(495, 33);
            TextBoxRecModelPath.TabIndex = 1;
            TextBoxRecModelPath.Text = "./test_demo/PP-OCRv5_mobile_rec_onnx.onnx";
            // 
            // ButtonSelectRecModel
            // 
            ButtonSelectRecModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectRecModel.Location = new Point(652, 220);
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
            ComboEngineType.Location = new Point(919, 104);
            ComboEngineType.Name = "ComboEngineType";
            ComboEngineType.Size = new Size(199, 35);
            ComboEngineType.TabIndex = 3;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label4.Location = new Point(816, 109);
            label4.Name = "label4";
            label4.Size = new Size(97, 27);
            label4.TabIndex = 0;
            label4.Text = "推理工具:";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label5.Location = new Point(816, 163);
            label5.Name = "label5";
            label5.Size = new Size(97, 27);
            label5.TabIndex = 0;
            label5.Text = "推理设备:";
            // 
            // ComboBoxDeviceType
            // 
            ComboBoxDeviceType.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxDeviceType.FormattingEnabled = true;
            ComboBoxDeviceType.Location = new Point(919, 158);
            ComboBoxDeviceType.Name = "ComboBoxDeviceType";
            ComboBoxDeviceType.Size = new Size(199, 35);
            ComboBoxDeviceType.TabIndex = 3;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label6.Location = new Point(795, 222);
            label6.Name = "label6";
            label6.Size = new Size(118, 27);
            label6.TabIndex = 0;
            label6.Text = "ONNX工具:";
            // 
            // ComboBoxONNXType
            // 
            ComboBoxONNXType.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxONNXType.FormattingEnabled = true;
            ComboBoxONNXType.Location = new Point(919, 219);
            ComboBoxONNXType.Name = "ComboBoxONNXType";
            ComboBoxONNXType.Size = new Size(199, 35);
            ComboBoxONNXType.TabIndex = 3;
            // 
            // ButtonLoadModel
            // 
            ButtonLoadModel.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonLoadModel.Location = new Point(1245, 109);
            ButtonLoadModel.Name = "ButtonLoadModel";
            ButtonLoadModel.Size = new Size(151, 55);
            ButtonLoadModel.TabIndex = 2;
            ButtonLoadModel.Text = "加载模型";
            ButtonLoadModel.UseVisualStyleBackColor = true;
            ButtonLoadModel.Click += ButtonLoadModel_Click;
            // 
            // ButtonInferImage
            // 
            ButtonInferImage.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonInferImage.Location = new Point(1245, 194);
            ButtonInferImage.Name = "ButtonInferImage";
            ButtonInferImage.Size = new Size(151, 55);
            ButtonInferImage.TabIndex = 2;
            ButtonInferImage.Text = "推理图片";
            ButtonInferImage.UseVisualStyleBackColor = true;
            ButtonInferImage.Click += ButtonInferImage_Click;
            // 
            // PictureBoxResult
            // 
            PictureBoxResult.BackColor = SystemColors.ActiveCaption;
            PictureBoxResult.BackgroundImageLayout = ImageLayout.Zoom;
            PictureBoxResult.Location = new Point(37, 449);
            PictureBoxResult.Name = "PictureBoxResult";
            PictureBoxResult.Size = new Size(705, 616);
            PictureBoxResult.TabIndex = 4;
            PictureBoxResult.TabStop = false;
            // 
            // TextBoxResult
            // 
            TextBoxResult.Location = new Point(779, 443);
            TextBoxResult.Name = "TextBoxResult";
            TextBoxResult.Size = new Size(639, 348);
            TextBoxResult.TabIndex = 5;
            TextBoxResult.Text = "";
            // 
            // TextBoxTime
            // 
            TextBoxTime.Location = new Point(779, 845);
            TextBoxTime.Name = "TextBoxTime";
            TextBoxTime.Size = new Size(639, 220);
            TextBoxTime.TabIndex = 5;
            TextBoxTime.Text = "";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label7.Location = new Point(37, 290);
            label7.Name = "label7";
            label7.Size = new Size(94, 27);
            label7.TabIndex = 0;
            label7.Text = "Dict字典:";
            // 
            // TextBoxDictPath
            // 
            TextBoxDictPath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxDictPath.Location = new Point(139, 287);
            TextBoxDictPath.Name = "TextBoxDictPath";
            TextBoxDictPath.Size = new Size(495, 33);
            TextBoxDictPath.TabIndex = 1;
            TextBoxDictPath.Text = "./test_demo/ppocrv5_dict.txt";
            // 
            // ButtonSelectDict
            // 
            ButtonSelectDict.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectDict.Location = new Point(652, 283);
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
            label8.Location = new Point(37, 349);
            label8.Name = "label8";
            label8.Size = new Size(97, 27);
            label8.TabIndex = 0;
            label8.Text = "测试图片:";
            // 
            // TextBoxImagePath
            // 
            TextBoxImagePath.Font = new Font("Microsoft YaHei UI", 12F);
            TextBoxImagePath.Location = new Point(139, 346);
            TextBoxImagePath.Name = "TextBoxImagePath";
            TextBoxImagePath.Size = new Size(495, 33);
            TextBoxImagePath.TabIndex = 1;
            TextBoxImagePath.Text = "./test_demo/demo_1.jpg";
            // 
            // ButtonSelectImage
            // 
            ButtonSelectImage.Font = new Font("Microsoft YaHei UI", 12F);
            ButtonSelectImage.Location = new Point(652, 342);
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
            ButtonTimeTest.Location = new Point(1245, 283);
            ButtonTimeTest.Name = "ButtonTimeTest";
            ButtonTimeTest.Size = new Size(151, 55);
            ButtonTimeTest.TabIndex = 2;
            ButtonTimeTest.Text = "时间测试";
            ButtonTimeTest.UseVisualStyleBackColor = true;
            ButtonTimeTest.Click += ButtonTimeTest_Click;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label9.Location = new Point(805, 347);
            label9.Name = "label9";
            label9.Size = new Size(108, 27);
            label9.TabIndex = 0;
            label9.Text = "BatchSize:";
            // 
            // ComboBoxBatchSize
            // 
            ComboBoxBatchSize.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxBatchSize.FormattingEnabled = true;
            ComboBoxBatchSize.Location = new Point(919, 342);
            ComboBoxBatchSize.Name = "ComboBoxBatchSize";
            ComboBoxBatchSize.Size = new Size(199, 35);
            ComboBoxBatchSize.TabIndex = 3;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label10.Location = new Point(1013, 403);
            label10.Name = "label10";
            label10.Size = new Size(153, 37);
            label10.TabIndex = 0;
            label10.Text = "推 理 结 果";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new Font("Microsoft YaHei UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, 134);
            label11.Location = new Point(1013, 805);
            label11.Name = "label11";
            label11.Size = new Size(153, 37);
            label11.TabIndex = 0;
            label11.Text = "推 理 时 间";
            // 
            // CheckBoxUseDet
            // 
            CheckBoxUseDet.AutoSize = true;
            CheckBoxUseDet.Checked = true;
            CheckBoxUseDet.CheckState = CheckState.Checked;
            CheckBoxUseDet.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            CheckBoxUseDet.Location = new Point(51, 403);
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
            CheckBoxUseCls.Location = new Point(240, 403);
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
            CheckBoxUseRec.Location = new Point(461, 403);
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
            label12.Location = new Point(222, 28);
            label12.Name = "label12";
            label12.Size = new Size(1055, 50);
            label12.TabIndex = 0;
            label12.Text = "JYPPX.DeploySharp.OpenCvSharp  PaddleOCR 测试案例 ";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label13.Location = new Point(816, 287);
            label13.Name = "label13";
            label13.Size = new Size(97, 27);
            label13.TabIndex = 0;
            label13.Text = "并发数量:";
            // 
            // ComboBoxConcurrency
            // 
            ComboBoxConcurrency.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ComboBoxConcurrency.FormattingEnabled = true;
            ComboBoxConcurrency.Location = new Point(919, 282);
            ComboBoxConcurrency.Name = "ComboBoxConcurrency";
            ComboBoxConcurrency.Size = new Size(199, 35);
            ComboBoxConcurrency.TabIndex = 3;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1482, 1074);
            Controls.Add(CheckBoxUseRec);
            Controls.Add(CheckBoxUseCls);
            Controls.Add(CheckBoxUseDet);
            Controls.Add(TextBoxTime);
            Controls.Add(TextBoxResult);
            Controls.Add(PictureBoxResult);
            Controls.Add(ComboBoxConcurrency);
            Controls.Add(ComboBoxBatchSize);
            Controls.Add(ComboBoxONNXType);
            Controls.Add(ComboBoxDeviceType);
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
            Controls.Add(label11);
            Controls.Add(label12);
            Controls.Add(label13);
            Controls.Add(label10);
            Controls.Add(label9);
            Controls.Add(label6);
            Controls.Add(label2);
            Controls.Add(label5);
            Controls.Add(TextBoxDetModelPath);
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
        private Label label11;
        private CheckBox CheckBoxUseDet;
        private CheckBox CheckBoxUseCls;
        private CheckBox CheckBoxUseRec;
        private Label label12;
        private Label label13;
        private ComboBox ComboBoxConcurrency;
    }
}
