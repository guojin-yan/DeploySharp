namespace DeploySharpApp.Desktop.Net48
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label brandLabel;
        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.TableLayoutPanel configTableLayoutPanel;
        private System.Windows.Forms.Label pageTitleLabel;
        private System.Windows.Forms.Label modelLabel;
        private System.Windows.Forms.ComboBox modelComboBox;
        private System.Windows.Forms.Label backendLabel;
        private System.Windows.Forms.ComboBox backendComboBox;
        private System.Windows.Forms.Label deviceLabel;
        private System.Windows.Forms.ComboBox deviceComboBox;
        private System.Windows.Forms.FlowLayoutPanel actionFlowLayoutPanel;
        private System.Windows.Forms.Button runButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label healthTitleLabel;
        private System.Windows.Forms.ListView healthListView;
        private System.Windows.Forms.ColumnHeader backendColumnHeader;
        private System.Windows.Forms.ColumnHeader stateColumnHeader;
        private System.Windows.Forms.ColumnHeader detailColumnHeader;
        private System.Windows.Forms.TableLayoutPanel outputTableLayoutPanel;
        private System.Windows.Forms.Label outputTitleLabel;
        private System.Windows.Forms.Label outputDescriptionLabel;
        private System.Windows.Forms.TextBox resultTextBox;
        private System.Windows.Forms.Label benchmarkDescriptionLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.headerPanel = new System.Windows.Forms.Panel();
            this.brandLabel = new System.Windows.Forms.Label();
            this.mainSplitContainer = new System.Windows.Forms.SplitContainer();
            this.configTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.pageTitleLabel = new System.Windows.Forms.Label();
            this.modelLabel = new System.Windows.Forms.Label();
            this.modelComboBox = new System.Windows.Forms.ComboBox();
            this.backendLabel = new System.Windows.Forms.Label();
            this.backendComboBox = new System.Windows.Forms.ComboBox();
            this.deviceLabel = new System.Windows.Forms.Label();
            this.deviceComboBox = new System.Windows.Forms.ComboBox();
            this.actionFlowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.runButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.healthTitleLabel = new System.Windows.Forms.Label();
            this.healthListView = new System.Windows.Forms.ListView();
            this.backendColumnHeader = new System.Windows.Forms.ColumnHeader();
            this.stateColumnHeader = new System.Windows.Forms.ColumnHeader();
            this.detailColumnHeader = new System.Windows.Forms.ColumnHeader();
            this.outputTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.outputTitleLabel = new System.Windows.Forms.Label();
            this.outputDescriptionLabel = new System.Windows.Forms.Label();
            this.resultTextBox = new System.Windows.Forms.TextBox();
            this.benchmarkDescriptionLabel = new System.Windows.Forms.Label();
            this.headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).BeginInit();
            this.mainSplitContainer.Panel1.SuspendLayout();
            this.mainSplitContainer.Panel2.SuspendLayout();
            this.mainSplitContainer.SuspendLayout();
            this.configTableLayoutPanel.SuspendLayout();
            this.actionFlowLayoutPanel.SuspendLayout();
            this.outputTableLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // headerPanel
            //
            this.headerPanel.BackColor = System.Drawing.Color.FromArgb(28, 39, 56);
            this.headerPanel.Controls.Add(this.brandLabel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Padding = new System.Windows.Forms.Padding(24, 18, 24, 12);
            this.headerPanel.Size = new System.Drawing.Size(1050, 92);
            this.headerPanel.TabIndex = 0;
            //
            // brandLabel
            //
            this.brandLabel.AutoSize = true;
            this.brandLabel.Font = new System.Drawing.Font("Segoe UI", 17F, System.Drawing.FontStyle.Bold);
            this.brandLabel.ForeColor = System.Drawing.Color.White;
            this.brandLabel.Location = new System.Drawing.Point(24, 18);
            this.brandLabel.Name = "brandLabel";
            this.brandLabel.Size = new System.Drawing.Size(205, 62);
            this.brandLabel.TabIndex = 0;
            this.brandLabel.Text = "DeploySharp\r\n本地 AI 体验中心";
            //
            // mainSplitContainer
            //
            this.mainSplitContainer.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.mainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplitContainer.Location = new System.Drawing.Point(0, 92);
            this.mainSplitContainer.Name = "mainSplitContainer";
            this.mainSplitContainer.Padding = new System.Windows.Forms.Padding(20);
            //
            // mainSplitContainer.Panel1
            //
            this.mainSplitContainer.Panel1.Controls.Add(this.configTableLayoutPanel);
            //
            // mainSplitContainer.Panel2
            //
            this.mainSplitContainer.Panel2.Controls.Add(this.outputTableLayoutPanel);
            this.mainSplitContainer.Size = new System.Drawing.Size(1050, 628);
            this.mainSplitContainer.SplitterDistance = 380;
            this.mainSplitContainer.TabIndex = 1;
            //
            // configTableLayoutPanel
            //
            this.configTableLayoutPanel.AutoScroll = true;
            this.configTableLayoutPanel.ColumnCount = 1;
            this.configTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.configTableLayoutPanel.Controls.Add(this.pageTitleLabel, 0, 0);
            this.configTableLayoutPanel.Controls.Add(this.modelLabel, 0, 1);
            this.configTableLayoutPanel.Controls.Add(this.modelComboBox, 0, 2);
            this.configTableLayoutPanel.Controls.Add(this.backendLabel, 0, 3);
            this.configTableLayoutPanel.Controls.Add(this.backendComboBox, 0, 4);
            this.configTableLayoutPanel.Controls.Add(this.deviceLabel, 0, 5);
            this.configTableLayoutPanel.Controls.Add(this.deviceComboBox, 0, 6);
            this.configTableLayoutPanel.Controls.Add(this.actionFlowLayoutPanel, 0, 7);
            this.configTableLayoutPanel.Controls.Add(this.progressBar, 0, 8);
            this.configTableLayoutPanel.Controls.Add(this.healthTitleLabel, 0, 9);
            this.configTableLayoutPanel.Controls.Add(this.healthListView, 0, 10);
            this.configTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.configTableLayoutPanel.Location = new System.Drawing.Point(20, 20);
            this.configTableLayoutPanel.Name = "configTableLayoutPanel";
            this.configTableLayoutPanel.Padding = new System.Windows.Forms.Padding(4);
            this.configTableLayoutPanel.RowCount = 11;
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.configTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.configTableLayoutPanel.Size = new System.Drawing.Size(336, 588);
            this.configTableLayoutPanel.TabIndex = 0;
            //
            // pageTitleLabel
            //
            this.pageTitleLabel.AutoSize = true;
            this.pageTitleLabel.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.pageTitleLabel.Location = new System.Drawing.Point(7, 4);
            this.pageTitleLabel.Name = "pageTitleLabel";
            this.pageTitleLabel.Size = new System.Drawing.Size(191, 25);
            this.pageTitleLabel.TabIndex = 0;
            this.pageTitleLabel.Text = "体验首页 / 视觉推理";
            //
            // modelLabel
            //
            this.modelLabel.AutoSize = true;
            this.modelLabel.ForeColor = System.Drawing.Color.DimGray;
            this.modelLabel.Location = new System.Drawing.Point(7, 42);
            this.modelLabel.Name = "modelLabel";
            this.modelLabel.Size = new System.Drawing.Size(88, 13);
            this.modelLabel.TabIndex = 1;
            this.modelLabel.Text = "模型 Artifact";
            //
            // modelComboBox
            //
            this.modelComboBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.modelComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.modelComboBox.FormattingEnabled = true;
            this.modelComboBox.Location = new System.Drawing.Point(7, 69);
            this.modelComboBox.Name = "modelComboBox";
            this.modelComboBox.Size = new System.Drawing.Size(322, 21);
            this.modelComboBox.TabIndex = 2;
            //
            // backendLabel
            //
            this.backendLabel.AutoSize = true;
            this.backendLabel.ForeColor = System.Drawing.Color.DimGray;
            this.backendLabel.Location = new System.Drawing.Point(7, 108);
            this.backendLabel.Name = "backendLabel";
            this.backendLabel.Size = new System.Drawing.Size(55, 13);
            this.backendLabel.TabIndex = 3;
            this.backendLabel.Text = "后端插件";
            //
            // backendComboBox
            //
            this.backendComboBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.backendComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.backendComboBox.FormattingEnabled = true;
            this.backendComboBox.Location = new System.Drawing.Point(7, 135);
            this.backendComboBox.Name = "backendComboBox";
            this.backendComboBox.Size = new System.Drawing.Size(322, 21);
            this.backendComboBox.TabIndex = 4;
            //
            // deviceLabel
            //
            this.deviceLabel.AutoSize = true;
            this.deviceLabel.ForeColor = System.Drawing.Color.DimGray;
            this.deviceLabel.Location = new System.Drawing.Point(7, 174);
            this.deviceLabel.Name = "deviceLabel";
            this.deviceLabel.Size = new System.Drawing.Size(31, 13);
            this.deviceLabel.TabIndex = 5;
            this.deviceLabel.Text = "设备";
            //
            // deviceComboBox
            //
            this.deviceComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.deviceComboBox.FormattingEnabled = true;
            this.deviceComboBox.Location = new System.Drawing.Point(7, 201);
            this.deviceComboBox.Name = "deviceComboBox";
            this.deviceComboBox.Size = new System.Drawing.Size(150, 21);
            this.deviceComboBox.TabIndex = 6;
            //
            // actionFlowLayoutPanel
            //
            this.actionFlowLayoutPanel.AutoSize = true;
            this.actionFlowLayoutPanel.Controls.Add(this.runButton);
            this.actionFlowLayoutPanel.Controls.Add(this.cancelButton);
            this.actionFlowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.actionFlowLayoutPanel.Location = new System.Drawing.Point(7, 243);
            this.actionFlowLayoutPanel.Name = "actionFlowLayoutPanel";
            this.actionFlowLayoutPanel.Size = new System.Drawing.Size(322, 36);
            this.actionFlowLayoutPanel.TabIndex = 7;
            //
            // runButton
            //
            this.runButton.AutoSize = true;
            this.runButton.BackColor = System.Drawing.Color.FromArgb(36, 112, 210);
            this.runButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.runButton.ForeColor = System.Drawing.Color.White;
            this.runButton.Location = new System.Drawing.Point(3, 3);
            this.runButton.Name = "runButton";
            this.runButton.Size = new System.Drawing.Size(75, 25);
            this.runButton.TabIndex = 0;
            this.runButton.Text = "运行演示";
            this.runButton.UseVisualStyleBackColor = false;
            this.runButton.Click += new System.EventHandler(this.RunButton_Click);
            //
            // cancelButton
            //
            this.cancelButton.AutoSize = true;
            this.cancelButton.Enabled = false;
            this.cancelButton.Location = new System.Drawing.Point(84, 3);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "取消";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            //
            // progressBar
            //
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressBar.Location = new System.Drawing.Point(7, 285);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(322, 23);
            this.progressBar.TabIndex = 8;
            //
            // healthTitleLabel
            //
            this.healthTitleLabel.AutoSize = true;
            this.healthTitleLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.healthTitleLabel.Location = new System.Drawing.Point(7, 314);
            this.healthTitleLabel.Name = "healthTitleLabel";
            this.healthTitleLabel.Size = new System.Drawing.Size(125, 20);
            this.healthTitleLabel.TabIndex = 9;
            this.healthTitleLabel.Text = "后端状态 / 诊断";
            //
            // healthListView
            //
            this.healthListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.backendColumnHeader, this.stateColumnHeader, this.detailColumnHeader });
            this.healthListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.healthListView.FullRowSelect = true;
            this.healthListView.HideSelection = false;
            this.healthListView.Location = new System.Drawing.Point(7, 349);
            this.healthListView.Name = "healthListView";
            this.healthListView.Size = new System.Drawing.Size(322, 232);
            this.healthListView.TabIndex = 10;
            this.healthListView.UseCompatibleStateImageBehavior = false;
            this.healthListView.View = System.Windows.Forms.View.Details;
            //
            // columns
            //
            this.backendColumnHeader.Text = "后端";
            this.backendColumnHeader.Width = 145;
            this.stateColumnHeader.Text = "状态";
            this.stateColumnHeader.Width = 95;
            this.detailColumnHeader.Text = "说明";
            this.detailColumnHeader.Width = 240;
            //
            // outputTableLayoutPanel
            //
            this.outputTableLayoutPanel.ColumnCount = 1;
            this.outputTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.outputTableLayoutPanel.Controls.Add(this.outputTitleLabel, 0, 0);
            this.outputTableLayoutPanel.Controls.Add(this.outputDescriptionLabel, 0, 1);
            this.outputTableLayoutPanel.Controls.Add(this.resultTextBox, 0, 2);
            this.outputTableLayoutPanel.Controls.Add(this.benchmarkDescriptionLabel, 0, 3);
            this.outputTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outputTableLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.outputTableLayoutPanel.Name = "outputTableLayoutPanel";
            this.outputTableLayoutPanel.Padding = new System.Windows.Forms.Padding(4);
            this.outputTableLayoutPanel.RowCount = 4;
            this.outputTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.outputTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.outputTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.outputTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.outputTableLayoutPanel.Size = new System.Drawing.Size(626, 588);
            this.outputTableLayoutPanel.TabIndex = 0;
            //
            // outputTitleLabel
            //
            this.outputTitleLabel.AutoSize = true;
            this.outputTitleLabel.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.outputTitleLabel.Location = new System.Drawing.Point(7, 4);
            this.outputTitleLabel.Name = "outputTitleLabel";
            this.outputTitleLabel.Size = new System.Drawing.Size(107, 25);
            this.outputTitleLabel.TabIndex = 0;
            this.outputTitleLabel.Text = "结果与证据";
            //
            // outputDescriptionLabel
            //
            this.outputDescriptionLabel.AutoSize = true;
            this.outputDescriptionLabel.ForeColor = System.Drawing.Color.DimGray;
            this.outputDescriptionLabel.Location = new System.Drawing.Point(7, 42);
            this.outputDescriptionLabel.Name = "outputDescriptionLabel";
            this.outputDescriptionLabel.Size = new System.Drawing.Size(508, 26);
            this.outputDescriptionLabel.TabIndex = 1;
            this.outputDescriptionLabel.Text = "结果、耗时、运行模式和结构化诊断集中呈现；原生 DLL 只由应用/Worker 负责。";
            //
            // resultTextBox
            //
            this.resultTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultTextBox.Location = new System.Drawing.Point(7, 93);
            this.resultTextBox.Multiline = true;
            this.resultTextBox.Name = "resultTextBox";
            this.resultTextBox.ReadOnly = true;
            this.resultTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.resultTextBox.Size = new System.Drawing.Size(612, 424);
            this.resultTextBox.TabIndex = 2;
            //
            // benchmarkDescriptionLabel
            //
            this.benchmarkDescriptionLabel.AutoSize = true;
            this.benchmarkDescriptionLabel.ForeColor = System.Drawing.Color.DimGray;
            this.benchmarkDescriptionLabel.Location = new System.Drawing.Point(7, 524);
            this.benchmarkDescriptionLabel.Name = "benchmarkDescriptionLabel";
            this.benchmarkDescriptionLabel.Size = new System.Drawing.Size(588, 26);
            this.benchmarkDescriptionLabel.TabIndex = 3;
            this.benchmarkDescriptionLabel.Text = "性能基准入口：使用同一模型和输入比较 P50/P95；缺少 CUDA/TensorRT 时显示 unavailable，不以 0 代替。";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.ClientSize = new System.Drawing.Size(1050, 720);
            this.Controls.Add(this.mainSplitContainer);
            this.Controls.Add(this.headerPanel);
            this.MinimumSize = new System.Drawing.Size(800, 560);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DeploySharpApp · 本地 AI 体验中心 (.NET Framework 4.8)";
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.mainSplitContainer.Panel1.ResumeLayout(false);
            this.mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).EndInit();
            this.mainSplitContainer.ResumeLayout(false);
            this.configTableLayoutPanel.ResumeLayout(false);
            this.configTableLayoutPanel.PerformLayout();
            this.actionFlowLayoutPanel.ResumeLayout(false);
            this.actionFlowLayoutPanel.PerformLayout();
            this.outputTableLayoutPanel.ResumeLayout(false);
            this.outputTableLayoutPanel.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
