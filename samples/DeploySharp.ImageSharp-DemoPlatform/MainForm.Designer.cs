namespace DeploySharp.ImageSharp_DemoPlatform
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tbModelPath = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonInfer = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.buttonSelectModelPath = new System.Windows.Forms.Button();
            this.comboEngineType = new System.Windows.Forms.ComboBox();
            this.comboBoxDeviceType = new System.Windows.Forms.ComboBox();
            this.comboBoxONNXType = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbImagePath = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.buttonSelectImagePath = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.comboBoxModelType = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.buttonLoadModel = new System.Windows.Forms.Button();
            this.tbInferTime = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.pictureBox1.Location = new System.Drawing.Point(450, 239);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(2);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(854, 618);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // tbModelPath
            // 
            this.tbModelPath.Font = new System.Drawing.Font("Microsoft YaHei UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tbModelPath.Location = new System.Drawing.Point(141, 97);
            this.tbModelPath.Margin = new System.Windows.Forms.Padding(2);
            this.tbModelPath.Name = "tbModelPath";
            this.tbModelPath.Size = new System.Drawing.Size(467, 32);
            this.tbModelPath.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(17, 103);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(109, 22);
            this.label1.TabIndex = 2;
            this.label1.Text = "Model Path:";
            // 
            // buttonInfer
            // 
            this.buttonInfer.Font = new System.Drawing.Font("Microsoft YaHei UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonInfer.Location = new System.Drawing.Point(141, 469);
            this.buttonInfer.Margin = new System.Windows.Forms.Padding(2);
            this.buttonInfer.Name = "buttonInfer";
            this.buttonInfer.Size = new System.Drawing.Size(155, 47);
            this.buttonInfer.TabIndex = 3;
            this.buttonInfer.Text = "Infer";
            this.buttonInfer.UseVisualStyleBackColor = true;
            this.buttonInfer.Click += new System.EventHandler(this.buttonInfer_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft YaHei UI", 26.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label2.Location = new System.Drawing.Point(187, 20);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(1054, 46);
            this.label2.TabIndex = 2;
            this.label2.Text = "DeploySharp : Model Deployment Demonstration Platform";
            // 
            // buttonSelectModelPath
            // 
            this.buttonSelectModelPath.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonSelectModelPath.Location = new System.Drawing.Point(664, 96);
            this.buttonSelectModelPath.Margin = new System.Windows.Forms.Padding(2);
            this.buttonSelectModelPath.Name = "buttonSelectModelPath";
            this.buttonSelectModelPath.Size = new System.Drawing.Size(80, 36);
            this.buttonSelectModelPath.TabIndex = 3;
            this.buttonSelectModelPath.Text = "Select";
            this.buttonSelectModelPath.UseVisualStyleBackColor = true;
            this.buttonSelectModelPath.Click += new System.EventHandler(this.buttonSelectModelPath_Click);
            // 
            // comboEngineType
            // 
            this.comboEngineType.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboEngineType.FormattingEnabled = true;
            this.comboEngineType.Location = new System.Drawing.Point(176, 304);
            this.comboEngineType.Margin = new System.Windows.Forms.Padding(2);
            this.comboEngineType.Name = "comboEngineType";
            this.comboEngineType.Size = new System.Drawing.Size(218, 32);
            this.comboEngineType.TabIndex = 4;
            // 
            // comboBoxDeviceType
            // 
            this.comboBoxDeviceType.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxDeviceType.FormattingEnabled = true;
            this.comboBoxDeviceType.Location = new System.Drawing.Point(176, 353);
            this.comboBoxDeviceType.Margin = new System.Windows.Forms.Padding(2);
            this.comboBoxDeviceType.Name = "comboBoxDeviceType";
            this.comboBoxDeviceType.Size = new System.Drawing.Size(218, 32);
            this.comboBoxDeviceType.TabIndex = 5;
            // 
            // comboBoxONNXType
            // 
            this.comboBoxONNXType.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxONNXType.FormattingEnabled = true;
            this.comboBoxONNXType.Location = new System.Drawing.Point(176, 408);
            this.comboBoxONNXType.Margin = new System.Windows.Forms.Padding(2);
            this.comboBoxONNXType.Name = "comboBoxONNXType";
            this.comboBoxONNXType.Size = new System.Drawing.Size(218, 32);
            this.comboBoxONNXType.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.Location = new System.Drawing.Point(30, 304);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(113, 22);
            this.label3.TabIndex = 2;
            this.label3.Text = "Engine Type:";
            // 
            // tbImagePath
            // 
            this.tbImagePath.Font = new System.Drawing.Font("Microsoft YaHei UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tbImagePath.Location = new System.Drawing.Point(141, 152);
            this.tbImagePath.Margin = new System.Windows.Forms.Padding(2);
            this.tbImagePath.Name = "tbImagePath";
            this.tbImagePath.Size = new System.Drawing.Size(467, 32);
            this.tbImagePath.TabIndex = 1;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.Location = new System.Drawing.Point(17, 158);
            this.label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(107, 22);
            this.label4.TabIndex = 2;
            this.label4.Text = "Image Path:";
            // 
            // buttonSelectImagePath
            // 
            this.buttonSelectImagePath.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonSelectImagePath.Location = new System.Drawing.Point(664, 148);
            this.buttonSelectImagePath.Margin = new System.Windows.Forms.Padding(2);
            this.buttonSelectImagePath.Name = "buttonSelectImagePath";
            this.buttonSelectImagePath.Size = new System.Drawing.Size(80, 36);
            this.buttonSelectImagePath.TabIndex = 3;
            this.buttonSelectImagePath.Text = "Select";
            this.buttonSelectImagePath.UseVisualStyleBackColor = true;
            this.buttonSelectImagePath.Click += new System.EventHandler(this.buttonSelectImagePath_Click);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.panel1.Location = new System.Drawing.Point(-4, 71);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1342, 4);
            this.panel1.TabIndex = 7;
            // 
            // comboBoxModelType
            // 
            this.comboBoxModelType.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxModelType.FormattingEnabled = true;
            this.comboBoxModelType.Location = new System.Drawing.Point(176, 247);
            this.comboBoxModelType.Margin = new System.Windows.Forms.Padding(2);
            this.comboBoxModelType.Name = "comboBoxModelType";
            this.comboBoxModelType.Size = new System.Drawing.Size(218, 32);
            this.comboBoxModelType.TabIndex = 4;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label5.Location = new System.Drawing.Point(32, 247);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(111, 22);
            this.label5.TabIndex = 2;
            this.label5.Text = "Model Type:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label6.Location = new System.Drawing.Point(30, 358);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(112, 22);
            this.label6.TabIndex = 2;
            this.label6.Text = "Device Type:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label7.Location = new System.Drawing.Point(30, 408);
            this.label7.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(111, 22);
            this.label7.TabIndex = 2;
            this.label7.Text = "ONNX Type:";
            // 
            // buttonLoadModel
            // 
            this.buttonLoadModel.Font = new System.Drawing.Font("Microsoft YaHei UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonLoadModel.Location = new System.Drawing.Point(823, 116);
            this.buttonLoadModel.Margin = new System.Windows.Forms.Padding(2);
            this.buttonLoadModel.Name = "buttonLoadModel";
            this.buttonLoadModel.Size = new System.Drawing.Size(170, 50);
            this.buttonLoadModel.TabIndex = 3;
            this.buttonLoadModel.Text = "Load Model";
            this.buttonLoadModel.UseVisualStyleBackColor = true;
            this.buttonLoadModel.Click += new System.EventHandler(this.buttonLoadModel_Click);
            // 
            // tbInferTime
            // 
            this.tbInferTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbInferTime.Location = new System.Drawing.Point(21, 555);
            this.tbInferTime.Multiline = true;
            this.tbInferTime.Name = "tbInferTime";
            this.tbInferTime.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbInferTime.Size = new System.Drawing.Size(410, 302);
            this.tbInferTime.TabIndex = 8;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1333, 886);
            this.Controls.Add(this.tbInferTime);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.comboBoxONNXType);
            this.Controls.Add(this.comboBoxDeviceType);
            this.Controls.Add(this.comboBoxModelType);
            this.Controls.Add(this.comboEngineType);
            this.Controls.Add(this.buttonSelectImagePath);
            this.Controls.Add(this.buttonSelectModelPath);
            this.Controls.Add(this.buttonLoadModel);
            this.Controls.Add(this.buttonInfer);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tbImagePath);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tbModelPath);
            this.Controls.Add(this.pictureBox1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.TextBox tbModelPath;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonInfer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonSelectModelPath;
        private System.Windows.Forms.ComboBox comboEngineType;
        private System.Windows.Forms.ComboBox comboBoxDeviceType;
        private System.Windows.Forms.ComboBox comboBoxONNXType;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbImagePath;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button buttonSelectImagePath;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ComboBox comboBoxModelType;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button buttonLoadModel;
        private System.Windows.Forms.TextBox tbInferTime;
    }
}

