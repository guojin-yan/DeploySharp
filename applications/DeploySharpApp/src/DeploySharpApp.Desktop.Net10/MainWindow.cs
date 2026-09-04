using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeploySharpApp.Application;
using DeploySharpApp.Infrastructure;

namespace DeploySharpApp.Desktop.Net10
{
    public sealed class MainWindow : Window
    {
        private readonly ExperienceViewModel _vm = AppComposition.CreateViewModel();
        private readonly TextBox _result = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 260 };
        private readonly ProgressBar _progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 18 };
        public MainWindow()
        {
            Title = "DeploySharpApp · WPF .NET 10"; Width = 1100; Height = 720; Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)); DataContext = _vm; Content = Build();
            _vm.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ExperienceViewModel.ResultText)) Dispatcher.Invoke(() => _result.Text = _vm.ResultText); if (args.PropertyName == nameof(ExperienceViewModel.Progress)) Dispatcher.Invoke(() => _progress.Value = _vm.Progress); };
        }
        private UIElement Build()
        {
            var root = new Grid { Margin = new Thickness(28) }; root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); root.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new StackPanel(); left.Children.Add(new TextBlock { Text = "DeploySharp", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(28, 39, 56)) }); left.Children.Add(new TextBlock { Text = "本地 AI 体验中心 · WPF / .NET 10", Margin = new Thickness(0, 4, 0, 24), Foreground = Brushes.Gray });
            var backend = new ComboBox { ItemsSource = _vm.Backends, DisplayMemberPath = "DisplayName", SelectedIndex = 0, Margin = new Thickness(0, 5, 0, 15) }; var model = new ComboBox { ItemsSource = _vm.Models, DisplayMemberPath = "DisplayName", SelectedIndex = 0, Margin = new Thickness(0, 5, 0, 15) }; var device = new ComboBox { ItemsSource = new[] { "cpu", "cuda" }, SelectedIndex = 0, Margin = new Thickness(0, 5, 0, 15) };
            left.Children.Add(new TextBlock { Text = "模型 Artifact" }); left.Children.Add(model); left.Children.Add(new TextBlock { Text = "后端插件" }); left.Children.Add(backend); left.Children.Add(new TextBlock { Text = "设备" }); left.Children.Add(device);
            var run = new Button { Content = "运行演示 →", Padding = new Thickness(18, 10, 18, 10), Background = new SolidColorBrush(Color.FromRgb(36, 112, 210)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 8) }; var cancel = new Button { Content = "取消", Padding = new Thickness(18, 8, 18, 8) }; run.Click += async (_, __) => { _vm.SelectedBackendId = _vm.Backends[backend.SelectedIndex].Id; _vm.SelectedModelId = _vm.Models[model.SelectedIndex].Id; _vm.SelectedDevice = device.SelectedItem?.ToString() ?? "cpu"; await _vm.RunAsync(); }; cancel.Click += (_, __) => _vm.Cancel(); left.Children.Add(run); left.Children.Add(cancel); left.Children.Add(_progress); left.Children.Add(new TextBlock { Text = "后端状态", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 24, 0, 6) }); foreach (var item in _vm.Backends) left.Children.Add(new TextBlock { Text = item.DisplayName + " · " + item.State, Margin = new Thickness(0, 2, 0, 2) }); Grid.SetColumn(left, 0); root.Children.Add(left);
            var right = new StackPanel(); right.Children.Add(new TextBlock { Text = "结果与诊断", FontSize = 22, FontWeight = FontWeights.Bold }); right.Children.Add(new TextBlock { Text = "统一 Application/ViewModel；可用后端由 manifest 和 probe 决定。", Foreground = Brushes.Gray, Margin = new Thickness(0, 5, 0, 18) }); right.Children.Add(_result); right.Children.Add(new TextBlock { Text = "性能区域", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 24, 0, 4) }); right.Children.Add(new TextBlock { Text = "基准报告区分 UI/编排、Worker IPC 与后端推理时间；缺少 native runtime 时保留 unavailable。", Foreground = Brushes.Gray }); Grid.SetColumn(right, 2); root.Children.Add(right); return root;
        }
    }
}
