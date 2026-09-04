using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Application
{
    public sealed class ExperienceViewModel : INotifyPropertyChanged
    {
        private readonly DeploySharpAppService _service;
        private CancellationTokenSource? _operationCancellation;
        private string _selectedBackendId = string.Empty;
        private string _selectedModelId = string.Empty;
        private string _selectedDevice = "cpu";
        private string _prompt = string.Empty;
        private string _resultText = "选择模型和后端后运行一次演示操作。";
        private double _progress;
        private bool _isBusy;

        public ExperienceViewModel(DeploySharpAppService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Backends = new ObservableCollection<AppBackendInfo>(_service.Backends);
            Models = new ObservableCollection<AppModelInfo>(_service.Models);
            if (Backends.Count > 0) _selectedBackendId = Backends[0].Id;
            if (Models.Count > 0) _selectedModelId = Models[0].Id;
        }

        public ObservableCollection<AppBackendInfo> Backends { get; }
        public ObservableCollection<AppModelInfo> Models { get; }
        public string SelectedBackendId { get => _selectedBackendId; set => Set(ref _selectedBackendId, value); }
        public string SelectedModelId { get => _selectedModelId; set => Set(ref _selectedModelId, value); }
        public string SelectedDevice { get => _selectedDevice; set => Set(ref _selectedDevice, value); }
        public string Prompt { get => _prompt; set => Set(ref _prompt, value); }
        public string ResultText { get => _resultText; private set => Set(ref _resultText, value); }
        public double Progress { get => _progress; private set => Set(ref _progress, value); }
        public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
        public event PropertyChangedEventHandler? PropertyChanged;

        public async Task RunAsync(AppOperationKind operation = AppOperationKind.Vision)
        {
            if (IsBusy) return;
            _operationCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            IsBusy = true; Progress = 0;
            try
            {
                var progress = new Progress<double>(value => Progress = value);
                var result = await _service.RunAsync(new ModelRunRequest(operation, SelectedModelId, SelectedBackendId, SelectedDevice, prompt: Prompt), progress, _operationCancellation.Token).ConfigureAwait(false);
                string status = result.RuntimeStatus == null ? string.Empty : Environment.NewLine + "运行时状态：" + result.RuntimeStatus.State;
                string diagnostics = result.Diagnostics.Count == 0 ? string.Empty : Environment.NewLine + "诊断：" + string.Join(", ", result.Diagnostics.Select(item => item.Code));
                ResultText = "运行模式：" + result.RunMode + Environment.NewLine + "结果状态：" + (result.Succeeded ? "Succeeded" : result.ErrorCode.ToString()) + status + Environment.NewLine + result.Message + (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : Environment.NewLine + result.Output) + diagnostics + Environment.NewLine + "端到端耗时：" + result.TotalMs.ToString("F1") + " ms";
            }
            catch (OperationCanceledException) { ResultText = "操作已取消。"; }
            catch (Exception exception) { ResultText = "操作失败：" + exception.Message; }
            finally { _operationCancellation.Dispose(); _operationCancellation = null; IsBusy = false; }
        }

        public void Cancel() => _operationCancellation?.Cancel();

        public void Refresh()
        {
            _service.RefreshCatalog();
            Backends.Clear(); foreach (var item in _service.Backends) Backends.Add(item);
            OnPropertyChanged(nameof(Backends)); OnPropertyChanged(nameof(Models));
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (Equals(field, value)) return; field = value; OnPropertyChanged(name); }
        private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
