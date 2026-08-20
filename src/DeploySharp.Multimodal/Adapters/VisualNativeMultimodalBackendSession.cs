using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Results.Multimodal;
using JYPPX.DeploySharp.Visual;

namespace JYPPX.DeploySharp.Multimodal.Adapters
{
    /// <summary>Bridges the audited Visual native VLM session into the neutral multimodal contract. / 将经审计的 Visual 原生 VLM 会话桥接到中立多模态合同。</summary>
    public sealed class VisualNativeMultimodalBackendSession : IMultimodalBackendSession
    {
        private readonly NativeMultimodalSession _session;
        private readonly INativeMultimodalTokenizer _tokenizer;
        private readonly Func<MultimodalMediaInput, NativeMultimodalPreparedImage> _imageFactory;
        private readonly bool _ownsSession;
        private bool _disposed;

        /// <summary>Initializes a single-image adapter; the image factory remains application-owned. / 初始化单图适配器；图像工厂仍由应用持有。</summary>
        public VisualNativeMultimodalBackendSession(
            NativeMultimodalSession session,
            INativeMultimodalTokenizer tokenizer,
            Func<MultimodalMediaInput, NativeMultimodalPreparedImage> imageFactory,
            ModelId modelId,
            string version,
            bool ownsSession = false)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _imageFactory = imageFactory ?? throw new ArgumentNullException(nameof(imageFactory));
            _ownsSession = ownsSession;
            Descriptor = new MultimodalBackendDescriptor(
                "visual-native-vlm",
                version,
                modelId,
                MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Cancellation,
                1,
                new MultimodalAvailability(MultimodalAvailabilityState.Available, "The caller supplied a loaded Visual native VLM session.", "caller-owned-native-and-model-bundle"));
        }

        /// <summary>Gets the exact single-image adapter boundary. / 获取精确的单图适配器边界。</summary>
        public MultimodalBackendDescriptor Descriptor { get; }

        /// <summary>Runs the existing Visual native VLM CPU/GPU path without retaining caller media. / 运行现有 Visual 原生 VLM CPU/GPU 路径且不保留调用方媒体。</summary>
        public Task<GenerationResult> GenerateAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (request.Media.Count != 1 || request.Media[0].Kind != MediaKind.Image || request.Media[0].Region != null)
            {
                throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, "The Visual native VLM adapter is audited only for one whole image.", modelId: Descriptor.ModelId);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (NativeMultimodalPreparedImage image = _imageFactory(request.Media[0]))
            {
                _session.SetImage(image, cancellationToken: cancellationToken);
                GenerativeVisionLanguageRequest visualRequest = request.Task == MultimodalTask.Captioning
                    ? GenerativeVisionLanguageRequest.Caption()
                    : GenerativeVisionLanguageRequest.Question(request.Prompt);
                NativeMultimodalResult result = _session.Generate(visualRequest, _tokenizer, cancellationToken: cancellationToken);
                return Task.FromResult(result.Generation.Generation);
            }
        }

        /// <summary>Rejects streaming because the current Visual native path has no asynchronous stream contract. / 拒绝流式调用，因为当前 Visual 原生路径没有异步流合同。</summary>
        public IAsyncEnumerable<GenerationChunk> StreamAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => throw new MultimodalException(MultimodalErrorCodes.CapabilityUnavailable, "The Visual native VLM adapter does not declare streaming support.", modelId: Descriptor.ModelId);

        /// <summary>Releases the Visual session only when ownership was explicitly transferred. / 仅在明确转移所有权时释放 Visual 会话。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsSession) _session.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisualNativeMultimodalBackendSession));
        }
    }
}
