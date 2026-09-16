using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Converts one application input into a prepared visual tensor for a selected profile. / 将一个应用输入转换为所选 Profile 的已准备视觉张量。</summary>
    /// <typeparam name="TInput">Application input type. / 应用输入类型。</typeparam>
    public interface IVisualInputPreprocessor<in TInput>
    {
        /// <summary>Prepares one input. The returned input is owned by the caller. / 准备一个输入；返回值由调用方拥有。</summary>
        public PreparedVisualInput Prepare(TInput input, VisualModelProfile profile, CancellationToken cancellationToken);
    }

    /// <summary>Provides a delegate-based input preprocessor for small custom integrations. / 为小型自定义接入提供基于委托的输入预处理器。</summary>
    public sealed class DelegateVisualInputPreprocessor<TInput> : IVisualInputPreprocessor<TInput>
    {
        private readonly Func<TInput, VisualModelProfile, CancellationToken, PreparedVisualInput> _prepare;

        /// <summary>Initializes the preprocessor. / 初始化预处理器。</summary>
        public DelegateVisualInputPreprocessor(Func<TInput, VisualModelProfile, CancellationToken, PreparedVisualInput> prepare)
        {
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        }

        /// <inheritdoc />
        public PreparedVisualInput Prepare(TInput input, VisualModelProfile profile, CancellationToken cancellationToken)
            => _prepare(input, profile, cancellationToken) ?? throw new VisualException(VisualErrorCodes.InputInvalid, "The custom preprocessor returned null.", profileId: profile.ProfileId);
    }

    /// <summary>Strongly types custom result decoding while the core pipeline retains its non-generic decoder ABI. / 在核心流水线保留非泛型 Decoder ABI 的同时，为自定义结果解码提供强类型。</summary>
    /// <typeparam name="TResult">Decoded result type. / 解码结果类型。</typeparam>
    public interface IVisualResultDecoder<out TResult> where TResult : class
    {
        /// <summary>Gets the produced task. / 获取生成的任务。</summary>
        public VisualTaskId Task { get; }
        /// <summary>Decodes validated named outputs. / 解码已经校验的命名输出。</summary>
        public TResult Decode(VisualDecodeContext context);
    }

    /// <summary>Provides a delegate-based strongly typed result decoder. / 提供基于委托的强类型结果 Decoder。</summary>
    public sealed class DelegateVisualResultDecoder<TResult> : IVisualResultDecoder<TResult> where TResult : class
    {
        private readonly Func<VisualDecodeContext, TResult> _decode;

        /// <summary>Initializes the result decoder. / 初始化结果 Decoder。</summary>
        public DelegateVisualResultDecoder(VisualTaskId task, Func<VisualDecodeContext, TResult> decode)
        {
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            Task = task;
            _decode = decode ?? throw new ArgumentNullException(nameof(decode));
        }

        /// <inheritdoc />
        public VisualTaskId Task { get; }
        /// <inheritdoc />
        public TResult Decode(VisualDecodeContext context) => _decode(context) ?? throw new VisualException(VisualErrorCodes.TensorInvalid, "The custom decoder returned null.", profileId: context.Profile.ProfileId);
    }

    /// <summary>Combines one immutable profile with its application-facing input preprocessor and typed result. / 将不可变 Profile、面向应用的输入预处理器和强类型结果组合在一起。</summary>
    public sealed class VisualModelDefinition<TInput, TResult> where TResult : class
    {
        internal VisualModelDefinition(VisualModelProfile profile, IVisualInputPreprocessor<TInput> preprocessor)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        }

        /// <summary>Gets the backend-neutral profile. / 获取后端无关 Profile。</summary>
        public VisualModelProfile Profile { get; }
        /// <summary>Gets the application input preprocessor. / 获取应用输入预处理器。</summary>
        public IVisualInputPreprocessor<TInput> Preprocessor { get; }

        /// <summary>Creates a typed runner and its owned backend session. The backend registry remains caller-owned. / 创建强类型 Runner 及其拥有的后端 Session；后端注册中心仍由调用方拥有。</summary>
        public VisualModelRunner<TInput, TResult> CreateRunner(BackendRegistry backendRegistry, ModelArtifact artifact, BackendRequest request, SessionOptions? sessionOptions = null)
        {
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            var profiles = new VisualProfileRegistry();
            profiles.Register(Profile);
            profiles.Freeze();
            VisualProfileSelection selection = profiles.Select(artifact, backendRegistry, request, Profile.Task);
            return new VisualModelRunner<TInput, TResult>(new VisualPipeline(backendRegistry, selection, request, sessionOptions), Preprocessor);
        }
    }

    /// <summary>Builds an end-to-end custom visual model definition without requiring a custom registry or untyped decoder. / 构建端到端自定义视觉模型定义，无需用户编写注册中心或非强类型 Decoder。</summary>
    public sealed class VisualModelDefinitionBuilder<TInput, TResult> where TResult : class
    {
        private readonly string _profileId;
        private readonly ModelId _modelId;
        private readonly VisualTaskId _task;
        private readonly string _version;
        private readonly string _modelFormat;
        private readonly List<VisualOutputBinding> _outputs = new List<VisualOutputBinding>();
        private readonly List<VisualAuxiliaryInputBinding> _auxiliaryInputs = new List<VisualAuxiliaryInputBinding>();
        private readonly List<VisualLabel> _labels = new List<VisualLabel>();
        private VisualInputBinding? _input;
        private VisualPreprocessingOptions? _preprocessing;
        private IVisualInputPreprocessor<TInput>? _inputPreprocessor;
        private IVisualResultDecoder<TResult>? _resultDecoder;
        private BackendCapabilities _capabilities = BackendCapabilities.TensorInference;
        private string? _minimumBackendVersion;

        /// <summary>Initializes a custom-model builder. / 初始化自定义模型 Builder。</summary>
        public VisualModelDefinitionBuilder(string profileId, ModelId modelId, VisualTaskId task, string version, string modelFormat)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("A version is required.", nameof(version));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            _profileId = profileId.Trim();
            _modelId = modelId;
            _task = task;
            _version = version.Trim();
            _modelFormat = modelFormat.Trim();
        }

        /// <summary>Sets the primary image input contract. / 设置主图像输入合同。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> WithInput(string name, TensorElementType elementType, TensorShape shape, VisualTensorLayout layout, int minimumBatch = 1, int maximumBatch = 1)
        {
            _input = new VisualInputBinding(name, elementType, shape, layout, minimumBatch, maximumBatch);
            return this;
        }

        /// <summary>Sets the common preprocessing contract consumed by compatible image adapters. / 设置由兼容图像适配器使用的通用预处理合同。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> WithPreprocessing(VisualPreprocessingOptions preprocessing)
        {
            _preprocessing = preprocessing ?? throw new ArgumentNullException(nameof(preprocessing));
            return this;
        }

        /// <summary>Sets the application-facing preprocessor implementation. / 设置面向应用的预处理器实现。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> WithInputPreprocessor(IVisualInputPreprocessor<TInput> preprocessor)
        {
            _inputPreprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
            return this;
        }

        /// <summary>Sets a delegate-based application-facing preprocessor without a separate adapter class. / 直接使用委托设置面向应用的预处理器，无需额外编写适配器类。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> WithInputPreprocessor(Func<TInput, VisualModelProfile, CancellationToken, PreparedVisualInput> prepare)
            => WithInputPreprocessor(new DelegateVisualInputPreprocessor<TInput>(prepare));

        /// <summary>Sets a strongly typed result decoder. / 设置强类型结果 Decoder。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> WithDecoder(IVisualResultDecoder<TResult> decoder)
        {
            _resultDecoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            return this;
        }

        /// <summary>Sets a strongly typed result decoder delegate. / 设置强类型结果 Decoder 委托。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> WithDecoder(Func<VisualDecodeContext, TResult> decode)
            => WithDecoder(new DelegateVisualResultDecoder<TResult>(_task, decode));

        /// <summary>Adds one exact output tensor contract. / 添加一个精确输出张量合同。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> AddOutput(string name, TensorElementType elementType, TensorShape shape)
        {
            _outputs.Add(new VisualOutputBinding(name, elementType, shape));
            return this;
        }

        /// <summary>Adds one class label. / 添加一个类别标签。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> AddLabel(int index, string label)
        {
            _labels.Add(new VisualLabel(index, label));
            return this;
        }

        /// <summary>Adds a non-image input generated by the preprocessor. / 添加由预处理器生成的非图像输入。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> AddAuxiliaryInput(string name, TensorElementType elementType, TensorShape shape)
        {
            _auxiliaryInputs.Add(new VisualAuxiliaryInputBinding(name, elementType, shape));
            return this;
        }

        /// <summary>Sets required backend capabilities and an optional minimum version. / 设置必需后端能力和可选最低版本。</summary>
        public VisualModelDefinitionBuilder<TInput, TResult> RequireBackend(BackendCapabilities capabilities, string? minimumVersion = null)
        {
            _capabilities = capabilities | BackendCapabilities.TensorInference;
            _minimumBackendVersion = string.IsNullOrWhiteSpace(minimumVersion) ? null : minimumVersion!.Trim();
            return this;
        }

        /// <summary>Validates and builds the reusable definition. / 校验并构建可复用定义。</summary>
        public VisualModelDefinition<TInput, TResult> Build()
        {
            if (_input == null) throw new InvalidOperationException("Call WithInput before Build.");
            if (_outputs.Count == 0) throw new InvalidOperationException("Add at least one output before Build.");
            if (_inputPreprocessor == null) throw new InvalidOperationException("Call WithInputPreprocessor before Build.");
            if (_resultDecoder == null) throw new InvalidOperationException("Call WithDecoder before Build.");
            if (_resultDecoder.Task != _task) throw new InvalidOperationException("The typed decoder task does not match the model task.");
            var profile = new VisualModelProfile(
                _profileId, _modelId, _task, _version, _modelFormat, _input,
                _outputs, _labels, new ResultDecoderAdapter<TResult>(_resultDecoder),
                _capabilities, _minimumBackendVersion, _auxiliaryInputs, _preprocessing);
            return new VisualModelDefinition<TInput, TResult>(profile, _inputPreprocessor);
        }
    }

    /// <summary>Runs application inputs through preprocessing, inference, and strongly typed postprocessing. / 依次执行应用输入预处理、推理和强类型后处理。</summary>
    public sealed class VisualModelRunner<TInput, TResult> : IDisposable where TResult : class
    {
        private readonly VisualPipeline _pipeline;
        private readonly IVisualInputPreprocessor<TInput> _preprocessor;

        internal VisualModelRunner(VisualPipeline pipeline, IVisualInputPreprocessor<TInput> preprocessor)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        }

        /// <summary>Gets the underlying profile and backend selection. / 获取底层 Profile 与后端选择。</summary>
        public VisualProfileSelection Selection => _pipeline.Selection;

        /// <summary>Preprocesses, runs, and decodes one input. / 对一个输入执行预处理、推理和解码。</summary>
        public TResult Run(TInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (PreparedVisualInput prepared = _preprocessor.Prepare(input, Selection.Profile, cancellationToken))
            {
                return _pipeline.Run(prepared, options, cancellationToken).GetValue<TResult>();
            }
        }

        /// <summary>Prepares an input without blocking the caller, then uses the backend asynchronous path. / 在不阻塞调用方的情况下准备输入，随后使用后端异步路径。</summary>
        public async Task<TResult> RunAsync(TInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            PreparedVisualInput prepared = await Task.Factory.StartNew(
                () => _preprocessor.Prepare(input, Selection.Profile, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default).ConfigureAwait(false);
            using (prepared)
            {
                VisualInferenceResult result = await _pipeline.RunAsync(prepared, options, cancellationToken).ConfigureAwait(false);
                return result.GetValue<TResult>();
            }
        }

        /// <inheritdoc />
        public void Dispose() => _pipeline.Dispose();
    }

    internal sealed class ResultDecoderAdapter<TResult> : IVisualDecoder where TResult : class
    {
        private readonly IVisualResultDecoder<TResult> _inner;
        public ResultDecoderAdapter(IVisualResultDecoder<TResult> inner) { _inner = inner; }
        public VisualTaskId Task => _inner.Task;
        public object Decode(VisualDecodeContext context) => _inner.Decode(context);
    }
}
