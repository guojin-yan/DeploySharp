using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
using DeploySharpApp.Infrastructure;

namespace DeploySharpApp.Desktop.Net48
{
    public partial class MainForm : Form
    {
        private readonly ExperienceViewModel _viewModel = AppComposition.CreateViewModel();

        public MainForm()
        {
            InitializeComponent();
            Bind();
        }

        private void Bind()
        {
            backendComboBox.Items.AddRange(_viewModel.Backends.Select(item => item.DisplayName + " · " + item.State).ToArray());
            modelComboBox.Items.AddRange(_viewModel.Models.Select(item => item.DisplayName + " · " + item.Format).ToArray());
            deviceComboBox.Items.AddRange(new object[] { "cpu", "cuda" });
            if (backendComboBox.Items.Count > 0) backendComboBox.SelectedIndex = 0;
            if (modelComboBox.Items.Count > 0) modelComboBox.SelectedIndex = 0;
            deviceComboBox.SelectedIndex = 0;

            foreach (var status in _viewModel.Backends.Select(item => new { item.DisplayName, item.State, item.Detail }))
                healthListView.Items.Add(new ListViewItem(new[] { status.DisplayName, status.State.ToString(), status.Detail ?? "等待探测" }));

            _viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ExperienceViewModel.Progress))
                    BeginInvoke((Action)(() => progressBar.Value = Math.Min(100, Math.Max(0, (int)(_viewModel.Progress * 100)))));
                if (args.PropertyName == nameof(ExperienceViewModel.ResultText))
                    BeginInvoke((Action)(() => resultTextBox.Text = _viewModel.ResultText));
                if (args.PropertyName == nameof(ExperienceViewModel.IsBusy))
                    BeginInvoke((Action)(() => { runButton.Enabled = !_viewModel.IsBusy; cancelButton.Enabled = _viewModel.IsBusy; }));
            };
        }

        private async void RunButton_Click(object sender, EventArgs e)
        {
            if (backendComboBox.SelectedIndex < 0 || modelComboBox.SelectedIndex < 0) return;
            _viewModel.SelectedBackendId = _viewModel.Backends[backendComboBox.SelectedIndex].Id;
            _viewModel.SelectedModelId = _viewModel.Models[modelComboBox.SelectedIndex].Id;
            _viewModel.SelectedDevice = deviceComboBox.SelectedItem?.ToString() ?? "cpu";
            await _viewModel.RunAsync().ConfigureAwait(true);
        }

        private void CancelButton_Click(object sender, EventArgs e) => _viewModel.Cancel();
    }
}
