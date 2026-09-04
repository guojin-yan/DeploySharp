using System;
using JYPPX.DeploySharp.Extensibility;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Describes user-selected TensorRT, CUDA, cuDNN, and bridge roots. / 描述用户选择的 TensorRT、CUDA、cuDNN 和 bridge 根目录。</summary>
    public sealed class TensorRtInstallationDescriptor
    {
        /// <summary>Initializes an installation descriptor. / 初始化安装描述。</summary>
        public TensorRtInstallationDescriptor(string cudaRoot, string cudnnRoot, string tensorRtRoot, string? bridgePath = null, string? runtimeIdentifier = null, string? processArchitecture = null)
        {
            CudaRoot = RequirePath(cudaRoot, nameof(cudaRoot));
            CudnnRoot = RequirePath(cudnnRoot, nameof(cudnnRoot));
            TensorRtRoot = RequirePath(tensorRtRoot, nameof(tensorRtRoot));
            BridgePath = NormalizeOptionalPath(bridgePath, nameof(bridgePath));
            RuntimeIdentifier = string.IsNullOrWhiteSpace(runtimeIdentifier) ? "win-x64" : ValidateIdentifier(runtimeIdentifier.ToLowerInvariant(), nameof(runtimeIdentifier));
            ProcessArchitecture = string.IsNullOrWhiteSpace(processArchitecture) ? "x64" : ValidateIdentifier(processArchitecture.ToLowerInvariant(), nameof(processArchitecture));
        }

        /// <summary>Gets the CUDA Toolkit root. / 获取 CUDA Toolkit 根目录。</summary>
        public string CudaRoot { get; }
        /// <summary>Gets the cuDNN root, which may differ from CUDA root. / 获取 cuDNN 根目录，可与 CUDA 根目录不同。</summary>
        public string CudnnRoot { get; }
        /// <summary>Gets the TensorRT root. / 获取 TensorRT 根目录。</summary>
        public string TensorRtRoot { get; }
        /// <summary>Gets the optional bridge DLL path. / 获取可选 bridge DLL 路径。</summary>
        public string? BridgePath { get; }
        /// <summary>Gets the target runtime identifier. / 获取目标运行时标识。</summary>
        public string RuntimeIdentifier { get; }
        /// <summary>Gets the process architecture. / 获取进程架构。</summary>
        public string ProcessArchitecture { get; }

        private static string RequirePath(string value, string parameterName)
        {
            string path = RequireText(value, parameterName);
            if (path.IndexOf('\0') >= 0) throw new ArgumentException("Paths cannot contain a null character.", parameterName);
            return System.IO.Path.GetFullPath(path);
        }

        private static string RequireText(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
            return value!.Trim();
        }

        private static string ValidateIdentifier(string? value, string parameterName)
        {
            string identifier = RequireText(value, parameterName);
            for (int index = 0; index < identifier.Length; index++)
            {
                char c = identifier[index];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_' || c == '/'))
                    throw new ArgumentException("Identifiers contain only letters, numbers, '.', '-', '_', or '/'.", parameterName);
            }
            return identifier;
        }

        private static string? NormalizeOptionalPath(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string path = value!.Trim();
            if (path.IndexOf('\0') >= 0) throw new ArgumentException("Paths cannot contain a null character.", parameterName);
            return System.IO.Path.GetFullPath(path);
        }
    }
}
