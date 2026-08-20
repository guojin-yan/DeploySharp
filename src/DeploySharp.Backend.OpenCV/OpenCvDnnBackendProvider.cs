using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Dnn;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Creates CPU OpenCV DNN sessions for exact static vision contracts. / 为精确静态视觉合同创建 CPU OpenCV DNN 会话。</summary>
    public sealed class OpenCvDnnBackendProvider : IBackendProvider
    {
        private readonly OpenCvDnnOptions _options;
        private bool _disposed;

        /// <summary>Gets the stable OpenCV DNN backend identifier. / 获取稳定的 OpenCV DNN 后端标识符。</summary>
        public static BackendId BackendId { get; } = new BackendId("opencv-dnn");

        /// <summary>Initializes a provider bound to one exact model contract. / 初始化绑定到一个精确模型合同的 Provider。</summary>
        public OpenCvDnnBackendProvider(OpenCvDnnOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            Descriptor = new BackendDescriptor(BackendId, "OpenCV DNN CPU", OpenCvSharpBuildInfo.PackageVersion, BackendCapabilities.TensorInference, new[] { "onnx" });
        }

        /// <summary>Gets backend identity and the intentionally synchronous CPU capability set. / 获取后端身份和有意保持同步的 CPU 能力集。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether the artifact and request match this provider's exact contract. / 判断工件和请求是否匹配此 Provider 的精确合同。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            return artifact.ModelId == _options.Contract.ModelId
                && string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)
                && (!request.BackendId.HasValue || request.BackendId.Value == BackendId)
                && (request.RequiredCapabilities & ~Descriptor.Capabilities) == 0
                && IsCpu(request.Device);
        }

        /// <summary>Loads the ONNX graph, selects OpenCV CPU, and returns a single-writer session. / 加载 ONNX 图、选择 OpenCV CPU 并返回单写入会话。</summary>
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request))
            {
                if (!IsCpu(request.Device)) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ConfigurationInvalid, "OpenCV DNN v1 is verified only for the CPU target.", modelId: artifact.ModelId, operation: "configure", technicalDetails: "device=" + request.Device);
                throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            }
            string path = OpenCvDnnModelArtifactValidator.Validate(artifact);
            Net? network = null;
            try
            {
                string nativeVersion = OpenCvSharpBuildInfo.GetNativeOpenCvVersion();
                if (!OpenCvSharpBuildInfo.IsNativeOpenCvVersionCompatible()) throw new OpenCvDnnBackendException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "The OpenCV native runtime version is incompatible with the managed API.", modelId: artifact.ModelId, operation: "preflight", technicalDetails: "managed=" + OpenCvSharpBuildInfo.OpenCvVersion + ";native=" + nativeVersion);
                network = Net.ReadNetFromOnnx(path, DnnEngine.Classic);
                if (network.Empty) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "OpenCV DNN loaded an empty network.", modelId: artifact.ModelId, operation: "load");
                network.SetPreferableBackend(DnnBackend.OpenCV).SetPreferableTarget(DnnTarget.Cpu).EnableFusion(_options.EnableFusion).EnableWinograd(_options.EnableWinograd);
                var session = new OpenCvDnnSession(artifact, network, _options.Contract);
                network = null;
                return session;
            }
            catch (OpenCvDnnBackendException) { throw; }
            catch (DllNotFoundException exception) { throw NativeUnavailable(artifact, exception); }
            catch (BadImageFormatException exception) { throw NativeUnavailable(artifact, exception); }
            catch (EntryPointNotFoundException exception) { throw NativeUnavailable(artifact, exception); }
            catch (Exception exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "OpenCV DNN could not load the ONNX model.", exception, artifact.ModelId, operation: "load", technicalDetails: exception.Message); }
            finally { network?.Dispose(); }
        }

        /// <summary>Disposes the provider without disposing sessions already returned to callers. / 释放 Provider，但不释放已返回给调用方的 Session。</summary>
        public void Dispose() => _disposed = true;

        private static bool IsCpu(string? device) => string.IsNullOrWhiteSpace(device) || string.Equals(device!.Trim(), "cpu", StringComparison.OrdinalIgnoreCase);
        private static OpenCvDnnBackendException NativeUnavailable(ModelArtifact artifact, Exception exception) => new OpenCvDnnBackendException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "The OpenCV native runtime is unavailable or ABI-incompatible. Install a matching JYPPX.OpenCV.runtime package for the current RID.", exception, artifact.ModelId, operation: "preflight", technicalDetails: exception.Message);
        private void ThrowIfDisposed() { if (_disposed) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ObjectDisposed, "The OpenCV DNN provider has been disposed.", operation: "provider"); }
    }
}
