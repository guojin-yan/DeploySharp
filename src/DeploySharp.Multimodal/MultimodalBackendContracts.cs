using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Results.Multimodal;

namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Contains a no-throw runtime capability probe result. / 包含不抛异常的运行时能力探测结果。</summary>
    public sealed class MultimodalAvailability
    {
        /// <summary>Initializes an availability result. / 初始化可用性结果。</summary>
        public MultimodalAvailability(MultimodalAvailabilityState state, string reason, string? runtimeIdentity = null)
        {
            if (!Enum.IsDefined(typeof(MultimodalAvailabilityState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An availability reason is required.", nameof(reason));
            State = state;
            Reason = reason.Trim();
            RuntimeIdentity = string.IsNullOrWhiteSpace(runtimeIdentity) ? null : runtimeIdentity!.Trim();
        }

        /// <summary>Gets the availability state. / 获取可用性状态。</summary>
        public MultimodalAvailabilityState State { get; }
        /// <summary>Gets the stable human-readable reason. / 获取稳定的可读原因。</summary>
        public string Reason { get; }
        /// <summary>Gets optional copied runtime identity. / 获取可选的复制运行时身份。</summary>
        public string? RuntimeIdentity { get; }
        /// <summary>Gets whether execution may be attempted. / 获取是否可以尝试执行。</summary>
        public bool IsAvailable => State == MultimodalAvailabilityState.Available;
    }

    /// <summary>Describes one backend-neutral multimodal adapter. / 描述一个后端中立的多模态适配器。</summary>
    public sealed class MultimodalBackendDescriptor
    {
        /// <summary>Initializes immutable adapter metadata. / 初始化不可变适配器元数据。</summary>
        public MultimodalBackendDescriptor(
            string id,
            string version,
            ModelId modelId,
            MultimodalCapabilities capabilities,
            int maximumMedia,
            MultimodalAvailability availability)
        {
            Id = MultimodalValidation.Identifier(id, nameof(id));
            Version = string.IsNullOrWhiteSpace(version) ? throw new ArgumentException("A version is required.", nameof(version)) : version.Trim();
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            if ((capabilities & MultimodalCapabilities.TextGeneration) == 0) throw new ArgumentException("A multimodal adapter must support completed text generation.", nameof(capabilities));
            if (maximumMedia <= 0) throw new ArgumentOutOfRangeException(nameof(maximumMedia));
            ModelId = modelId;
            Capabilities = capabilities;
            MaximumMedia = maximumMedia;
            Availability = availability ?? throw new ArgumentNullException(nameof(availability));
        }

        /// <summary>Gets the stable adapter identifier. / 获取稳定的适配器标识符。</summary>
        public string Id { get; }
        /// <summary>Gets the adapter/runtime version identity. / 获取适配器或运行时版本身份。</summary>
        public string Version { get; }
        /// <summary>Gets the exact model identity. / 获取精确模型身份。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets declared capabilities. / 获取声明的能力。</summary>
        public MultimodalCapabilities Capabilities { get; }
        /// <summary>Gets the maximum ordered media count. / 获取最大有序媒体数量。</summary>
        public int MaximumMedia { get; }
        /// <summary>Gets the runtime probe result. / 获取运行时探测结果。</summary>
        public MultimodalAvailability Availability { get; }
    }

    /// <summary>Executes multimodal requests without exposing vendor-native types. / 执行多模态请求且不公开厂商原生类型。</summary>
    public interface IMultimodalBackendSession : IDisposable
    {
        /// <summary>Gets immutable adapter metadata. / 获取不可变适配器元数据。</summary>
        public MultimodalBackendDescriptor Descriptor { get; }
        /// <summary>Generates one completed language result. / 生成一个完整语言结果。</summary>
        public Task<GenerationResult> GenerateAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>Streams ordered chunks ending with exactly one terminal chunk. / 流式返回有序片段并以一个终止片段结束。</summary>
        public IAsyncEnumerable<GenerationChunk> StreamAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken));
    }
}
