using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the OCR pipeline stage associated with a diagnostic. / 标识诊断关联的 OCR Pipeline 阶段。</summary>
    public enum OcrPipelineStage
    {
        /// <summary>Input validation. / 输入校验。</summary>
        Input = 0,
        /// <summary>Text detection inference and decoding. / 文本检测推理与解码。</summary>
        Detection = 1,
        /// <summary>Perspective crop and batch preparation. / 透视裁剪与批准备。</summary>
        CropAndBatch = 2,
        /// <summary>Text recognition inference and CTC decoding. / 文本识别推理与 CTC 解码。</summary>
        Recognition = 3,
        /// <summary>Reading-order merge and result construction. / 阅读顺序合并与结果构造。</summary>
        Merge = 4,
        /// <summary>Object disposal. / 对象释放。</summary>
        Disposal = 5,
        /// <summary>Per-text-region orientation classification. / 逐文本区域方向分类。</summary>
        OrientationClassification = 6
    }

    /// <summary>Represents a stable OCR pipeline diagnostic with stage and region context. / 表示带阶段和区域上下文的稳定 OCR Pipeline 诊断。</summary>
    public sealed class OcrPipelineException : DeploySharpException
    {
        /// <summary>Initializes an OCR pipeline exception. / 初始化 OCR Pipeline 异常。</summary>
        public OcrPipelineException(string errorCode, string message, OcrPipelineStage stage, Exception? innerException = null, string? profileId = null, int? regionIndex = null, string? tensorName = null, BackendId? backendId = null, ModelId? modelId = null, string? technicalDetails = null)
            : base(errorCode, message, innerException, backendId, modelId, technicalDetails)
        {
            if (!Enum.IsDefined(typeof(OcrPipelineStage), stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            if (regionIndex.HasValue && regionIndex.Value < 0) throw new ArgumentOutOfRangeException(nameof(regionIndex));
            Stage = stage;
            ProfileId = profileId;
            RegionIndex = regionIndex;
            TensorName = tensorName;
        }

        /// <summary>Gets failing stage. / 获取失败阶段。</summary>
        public OcrPipelineStage Stage { get; }
        /// <summary>Gets associated profile ID. / 获取关联 Profile ID。</summary>
        public string? ProfileId { get; }
        /// <summary>Gets source region index when available. / 获取可用时的源区域索引。</summary>
        public int? RegionIndex { get; }
        /// <summary>Gets tensor name when available. / 获取可用时的张量名称。</summary>
        public string? TensorName { get; }
    }

    /// <summary>Defines immutable OCR pipeline concurrency and resource bounds. / 定义不可变 OCR Pipeline 并发与资源边界。</summary>
    public sealed class OcrPipelineOptions
    {
        /// <summary>Initializes OCR pipeline bounds. / 初始化 OCR Pipeline 边界。</summary>
        public OcrPipelineOptions(int maximumConcurrency = 1, int maximumRegions = 128, int maximumRecognitionBatch = 16, long maximumSourcePixels = 128L * 1024L * 1024L, long maximumResultBytes = 16L * 1024L * 1024L, double maximumRecognitionPaddingRatio = 1.0)
        {
            if (maximumConcurrency <= 0 || maximumRegions <= 0 || maximumRecognitionBatch <= 0 || maximumSourcePixels <= 0 || maximumResultBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
            if (double.IsNaN(maximumRecognitionPaddingRatio) || double.IsInfinity(maximumRecognitionPaddingRatio) || maximumRecognitionPaddingRatio < 1.0) throw new ArgumentOutOfRangeException(nameof(maximumRecognitionPaddingRatio));
            MaximumConcurrency = maximumConcurrency;
            MaximumRegions = maximumRegions;
            MaximumRecognitionBatch = maximumRecognitionBatch;
            MaximumSourcePixels = maximumSourcePixels;
            MaximumResultBytes = maximumResultBytes;
            MaximumRecognitionPaddingRatio = maximumRecognitionPaddingRatio;
        }

        /// <summary>Gets maximum concurrent end-to-end calls. / 获取最大并发端到端调用数。</summary>
        public int MaximumConcurrency { get; }
        /// <summary>Gets maximum regions per image. / 获取每张图最大区域数。</summary>
        public int MaximumRegions { get; }
        /// <summary>Gets maximum recognition batch selected by orchestration. / 获取编排选择的最大识别批次。</summary>
        public int MaximumRecognitionBatch { get; }
        /// <summary>Gets maximum source pixel count. / 获取最大源图像素数。</summary>
        public long MaximumSourcePixels { get; }
        /// <summary>Gets maximum approximate owned result bytes. / 获取最大近似自有结果字节数。</summary>
        public long MaximumResultBytes { get; }
        /// <summary>Gets maximum padded-width work divided by natural-width work when forming recognition batches. / 获取组成识别批次时填充后宽度工作量与自然宽度工作量的最大比值。</summary>
        public double MaximumRecognitionPaddingRatio { get; }
    }

    /// <summary>Controls one OCR call without changing reusable pipeline configuration. / 控制一次 OCR 调用而不更改可复用 Pipeline 配置。</summary>
    public sealed class OcrExecutionOptions
    {
        /// <summary>Initializes one OCR execution request. / 初始化一次 OCR 执行请求。</summary>
        public OcrExecutionOptions(TimeSpan? timeout = null, bool disposeInputOnCompletion = false, string? correlationId = null)
        {
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            Timeout = timeout;
            DisposeInputOnCompletion = disposeInputOnCompletion;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
        }

        /// <summary>Gets shared end-to-end timeout. / 获取共享端到端超时。</summary>
        public TimeSpan? Timeout { get; }
        /// <summary>Gets whether the image input is disposed after all outcomes. / 获取是否在所有结果后释放图像输入。</summary>
        public bool DisposeInputOnCompletion { get; }
        /// <summary>Gets optional correlation ID forwarded to child calls. / 获取传递给子调用的可选关联 ID。</summary>
        public string? CorrelationId { get; }
        /// <summary>Gets defaults. / 获取默认值。</summary>
        public static OcrExecutionOptions Default { get; } = new OcrExecutionOptions();
    }

    /// <summary>Runs bounded detector, perspective-crop, recognizer, and CTC stages through two Core-backed Visual pipelines. / 通过两个 Core 支持的 Visual Pipeline 运行有界检测、透视裁剪、识别和 CTC 阶段。</summary>
    public sealed class OcrPipeline : IDisposable
    {
        private readonly object _lifetimeGate = new object();
        private readonly VisualPipeline _detector;
        private readonly VisualPipeline _recognizer;
        private OcrOrientationPipeline? _regionOrientation;
        private TextCropProfile? _orientationCropProfile;
        private OcrOrientationRejectionPolicy _orientationRejectionPolicy;
        private readonly TextCropProfile _cropProfile;
        private readonly OcrPipelineOptions _options;
        private readonly SemaphoreSlim _operationGate;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private bool _disposed;

        /// <summary>Creates and owns detector and recognizer sessions selected through the shared Core backend registry. / 通过共享 Core 后端注册中心创建并拥有检测器和识别器会话。</summary>
        public OcrPipeline(BackendRegistry backendRegistry, VisualProfileSelection detectionSelection, BackendRequest detectionRequest, VisualProfileSelection recognitionSelection, BackendRequest recognitionRequest, TextCropProfile cropProfile, OcrPipelineOptions? options = null, SessionOptions? detectionSessionOptions = null, SessionOptions? recognitionSessionOptions = null)
            : this(backendRegistry, detectionSelection, detectionRequest, backendRegistry, recognitionSelection, recognitionRequest, cropProfile, options, detectionSessionOptions, recognitionSessionOptions)
        {
        }

        /// <summary>Creates detector and recognizer sessions through independently configured backend registries. / 通过独立配置的后端注册中心创建检测与识别 Session。</summary>
        public OcrPipeline(BackendRegistry detectionBackendRegistry, VisualProfileSelection detectionSelection, BackendRequest detectionRequest, BackendRegistry recognitionBackendRegistry, VisualProfileSelection recognitionSelection, BackendRequest recognitionRequest, TextCropProfile cropProfile, OcrPipelineOptions? options = null, SessionOptions? detectionSessionOptions = null, SessionOptions? recognitionSessionOptions = null)
        {
            if (detectionBackendRegistry == null) throw new ArgumentNullException(nameof(detectionBackendRegistry));
            if (recognitionBackendRegistry == null) throw new ArgumentNullException(nameof(recognitionBackendRegistry));
            DetectionSelection = detectionSelection ?? throw new ArgumentNullException(nameof(detectionSelection));
            RecognitionSelection = recognitionSelection ?? throw new ArgumentNullException(nameof(recognitionSelection));
            if (detectionRequest == null) throw new ArgumentNullException(nameof(detectionRequest));
            if (recognitionRequest == null) throw new ArgumentNullException(nameof(recognitionRequest));
            _cropProfile = cropProfile ?? throw new ArgumentNullException(nameof(cropProfile));
            _options = options ?? new OcrPipelineOptions();
            ValidateProfiles(detectionSelection.Profile, recognitionSelection.Profile, cropProfile, _options);
            _operationGate = new SemaphoreSlim(_options.MaximumConcurrency, _options.MaximumConcurrency);
            VisualPipeline? detector = null;
            try
            {
                detector = new VisualPipeline(detectionBackendRegistry, detectionSelection, detectionRequest, detectionSessionOptions);
                _detector = detector;
                _recognizer = new VisualPipeline(recognitionBackendRegistry, recognitionSelection, recognitionRequest, recognitionSessionOptions);
            }
            catch
            {
                detector?.Dispose();
                _operationGate.Dispose();
                _disposeSource.Dispose();
                throw;
            }
        }

        /// <summary>Creates and owns detector, per-text-region orientation classifier, and recognizer sessions. / 创建并拥有检测器、逐文本区域方向分类器与识别器会话。</summary>
        public OcrPipeline(BackendRegistry backendRegistry, VisualProfileSelection detectionSelection, BackendRequest detectionRequest, VisualProfileSelection orientationSelection, BackendRequest orientationRequest, TextCropProfile orientationCropProfile, VisualProfileSelection recognitionSelection, BackendRequest recognitionRequest, TextCropProfile recognitionCropProfile, OcrPipelineOptions? options = null, SessionOptions? detectionSessionOptions = null, SessionOptions? orientationSessionOptions = null, SessionOptions? recognitionSessionOptions = null, OcrOrientationRejectionPolicy orientationRejectionPolicy = OcrOrientationRejectionPolicy.Fail)
            : this(backendRegistry, detectionSelection, detectionRequest, backendRegistry, orientationSelection, orientationRequest, orientationCropProfile, backendRegistry, recognitionSelection, recognitionRequest, recognitionCropProfile, options, detectionSessionOptions, orientationSessionOptions, recognitionSessionOptions, orientationRejectionPolicy)
        {
        }

        /// <summary>Creates detector, orientation, and recognizer sessions through independently configured backend registries. / 通过独立配置的后端注册中心创建检测、方向与识别 Session。</summary>
        public OcrPipeline(BackendRegistry detectionBackendRegistry, VisualProfileSelection detectionSelection, BackendRequest detectionRequest, BackendRegistry orientationBackendRegistry, VisualProfileSelection orientationSelection, BackendRequest orientationRequest, TextCropProfile orientationCropProfile, BackendRegistry recognitionBackendRegistry, VisualProfileSelection recognitionSelection, BackendRequest recognitionRequest, TextCropProfile recognitionCropProfile, OcrPipelineOptions? options = null, SessionOptions? detectionSessionOptions = null, SessionOptions? orientationSessionOptions = null, SessionOptions? recognitionSessionOptions = null, OcrOrientationRejectionPolicy orientationRejectionPolicy = OcrOrientationRejectionPolicy.Fail)
            : this(detectionBackendRegistry, detectionSelection, detectionRequest, recognitionBackendRegistry, recognitionSelection, recognitionRequest, recognitionCropProfile, options, detectionSessionOptions, recognitionSessionOptions)
        {
            if (orientationSelection == null) throw new ArgumentNullException(nameof(orientationSelection));
            if (orientationRequest == null) throw new ArgumentNullException(nameof(orientationRequest));
            _orientationCropProfile = orientationCropProfile ?? throw new ArgumentNullException(nameof(orientationCropProfile));
            if (!Enum.IsDefined(typeof(OcrOrientationRejectionPolicy), orientationRejectionPolicy)) throw new ArgumentOutOfRangeException(nameof(orientationRejectionPolicy));
            _orientationRejectionPolicy = orientationRejectionPolicy;
            try
            {
                ValidateOrientationProfile(orientationSelection.Profile, _orientationCropProfile);
                _regionOrientation = new OcrOrientationPipeline(orientationBackendRegistry ?? throw new ArgumentNullException(nameof(orientationBackendRegistry)), orientationSelection, orientationRequest, orientationSessionOptions);
            }
            catch { Dispose(); throw; }
        }

        /// <summary>Gets detector selection. / 获取检测器选择。</summary>
        public VisualProfileSelection DetectionSelection { get; }
        /// <summary>Gets recognizer selection. / 获取识别器选择。</summary>
        public VisualProfileSelection RecognitionSelection { get; }
        /// <summary>Gets crop profile. / 获取裁剪 Profile。</summary>
        public TextCropProfile CropProfile => _cropProfile;
        /// <summary>Gets pipeline bounds. / 获取 Pipeline 边界。</summary>
        public OcrPipelineOptions Options => _options;
        /// <summary>Gets the explicit orientation strategy configured for this pipeline. / 获取此 Pipeline 配置的显式方向策略。</summary>
        public OcrOrientationStrategy OrientationStrategy => _regionOrientation == null ? OcrOrientationStrategy.None : OcrOrientationStrategy.PerTextRegion;
        /// <summary>Gets the configured handling for rejected per-region orientation results. / 获取逐区域方向拒绝结果的配置处理方式。</summary>
        public OcrOrientationRejectionPolicy OrientationRejectionPolicy => _orientationRejectionPolicy;

        /// <summary>Runs synchronous end-to-end OCR without thread-pool wrapping. / 在不包装线程池任务的情况下运行同步端到端 OCR。</summary>
        public OcrResult Run(IOcrImageInput input, OcrExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAsync(input, null, options ?? OcrExecutionOptions.Default, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Runs synchronous OCR and binds the accepted orientation provenance to the owned result. / 同步运行 OCR，并将已接受的方向来源绑定到自有结果。</summary>
        public OcrResult RunWithOrientation(IOcrImageInput input, OcrOrientationResult orientation, OcrExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (orientation == null) throw new ArgumentNullException(nameof(orientation));
            if (orientation.Rejected) throw new OcrPipelineException(VisualErrorCodes.OcrOrientationCapabilityUnavailable, "A rejected orientation result cannot drive OCR correction.", OcrPipelineStage.Input, profileId: orientation.ProfileId, modelId: orientation.ModelId);
            return ExecuteAsync(input, orientation, options ?? OcrExecutionOptions.Default, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Runs end-to-end OCR using backend asynchronous paths where available. / 在后端可用时使用异步路径运行端到端 OCR。</summary>
        public Task<OcrResult> RunAsync(IOcrImageInput input, OcrExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAsync(input, null, options ?? OcrExecutionOptions.Default, cancellationToken);
        }

        /// <summary>Runs asynchronous OCR and binds accepted orientation provenance. / 异步运行 OCR，并绑定已接受的方向来源。</summary>
        public Task<OcrResult> RunWithOrientationAsync(IOcrImageInput input, OcrOrientationResult orientation, OcrExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (orientation == null) throw new ArgumentNullException(nameof(orientation));
            if (orientation.Rejected) throw new OcrPipelineException(VisualErrorCodes.OcrOrientationCapabilityUnavailable, "A rejected orientation result cannot drive OCR correction.", OcrPipelineStage.Input, profileId: orientation.ProfileId, modelId: orientation.ModelId);
            return ExecuteAsync(input, orientation, options ?? OcrExecutionOptions.Default, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>Cancels active calls, waits for every orchestration slot, then idempotently releases recognizer and detector sessions. / 取消活动调用、等待全部编排槽位，然后幂等释放识别器与检测器会话。</remarks>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }
            int acquired = 0;
            try
            {
                for (; acquired < _options.MaximumConcurrency; acquired++) _operationGate.Wait();
                _regionOrientation?.Dispose();
                _recognizer.Dispose();
                _detector.Dispose();
            }
            finally
            {
                for (int index = 0; index < acquired; index++) _operationGate.Release();
                _operationGate.Dispose();
                _disposeSource.Dispose();
            }
        }

        private async Task<OcrResult> ExecuteAsync(IOcrImageInput input, OcrOrientationResult? orientation, OcrExecutionOptions execution, CancellationToken callerToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken disposeToken = CaptureDisposeToken();
            bool entered = false;
            OcrPipelineStage stage = OcrPipelineStage.Input;
            int? regionIndex = null;
            CancellationToken operationToken = disposeToken;
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            try
            {
                // The default OCR path only needs the pipeline lifetime token. Create timeout/link
                // sources only when the caller requested cancellation or a deadline.
                // 默认 OCR 路径只需要 Pipeline 生命周期令牌；仅在请求取消或超时时创建链接源。
                if (execution.Timeout.HasValue) timeoutSource = new CancellationTokenSource(execution.Timeout.Value);
                if (callerToken.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(callerToken, disposeToken)
                        : CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutSource.Token, disposeToken);
                    operationToken = linked.Token;
                }

                try
                {
                    long sourcePixels = checked((long)input.SourceSize.Width * input.SourceSize.Height);
                    if (sourcePixels > _options.MaximumSourcePixels) throw Limit("OCR source image exceeds its pixel limit.", stage, technicalDetails: "pixels=" + sourcePixels);
                    if (input.DetectionInput.SourceSize != input.SourceSize) throw Failure("OCR input source size does not match detector input.", stage);
                    await _operationGate.WaitAsync(operationToken).ConfigureAwait(false);
                    entered = true;
                    EnsureUsable();

                    stage = OcrPipelineStage.Detection;
                    var detectionWatch = Stopwatch.StartNew();
                    VisualInferenceResult detectionInference = await _detector.RunAsync(input.DetectionInput, new VisualExecutionOptions(correlationId: execution.CorrelationId), operationToken).ConfigureAwait(false);
                    TextDetectionResult detection = detectionInference.GetValue<TextDetectionResult>();
                    detectionWatch.Stop();
                    if (detection.Regions.Count > _options.MaximumRegions) throw Limit("OCR detector returned more regions than the pipeline limit.", stage, DetectionSelection.Profile.ProfileId, technicalDetails: "regions=" + detection.Regions.Count);

                    var orientationDuration = TimeSpan.Zero;
                    IReadOnlyList<TextRegion> regions = detection.Regions;
                    if (_regionOrientation != null)
                    {
                        stage = OcrPipelineStage.OrientationClassification;
                        var orientationRequests = new List<IndexedRequest>(detection.Regions.Count);
                        for (int index = 0; index < detection.Regions.Count; index++)
                        {
                            operationToken.ThrowIfCancellationRequested();
                            TextRegion region = detection.Regions[index];
                            regionIndex = region.SourceIndex;
                            orientationRequests.Add(new IndexedRequest(index, new TextCropRequest(region, _orientationCropProfile ?? throw Failure("The per-region orientation crop profile is missing.", stage, regionIndex: regionIndex))));
                        }
                        regionIndex = null;
                        var orientationWatch = Stopwatch.StartNew();
                        List<OcrBatchDescriptor> orientationBatches = CreateBatches(orientationRequests, _regionOrientation.Selection.Profile.Input.MinimumBatch, EffectiveMaximumBatch(_regionOrientation.Selection), 1.0, operationToken);
                        try
                        {
                            BatchExecution<IReadOnlyList<OcrOrientationResult>>[] orientationResults = await RunBatchesAsync(input, _regionOrientation.Selection.Profile.Input.Name, orientationBatches, _regionOrientation.MaximumConcurrency, (prepared, token) => _regionOrientation.RunBatchAsync(prepared, new VisualExecutionOptions(correlationId: execution.CorrelationId), token), operationToken).ConfigureAwait(false);
                            var oriented = new TextRegion[detection.Regions.Count];
                            for (int batchIndex = 0; batchIndex < orientationBatches.Count; batchIndex++)
                            {
                                OcrBatchDescriptor batch = orientationResults[batchIndex].Batch;
                                IReadOnlyList<OcrOrientationResult> batchResults = orientationResults[batchIndex].Result;
                                if (batchResults.Count != batch.Requests.Count) throw Failure("Orientation result count does not match submitted batch.", stage, _regionOrientation.Selection.Profile.ProfileId);
                                for (int itemIndex = 0; itemIndex < batch.ActualCount; itemIndex++)
                                {
                                    IndexedRequest request = batch.Requests[itemIndex];
                                    OcrOrientationResult orientationResult = batchResults[itemIndex];
                                    TextRegion region = detection.Regions[request.Position];
                                    if (orientationResult.Rejected && _orientationRejectionPolicy == OcrOrientationRejectionPolicy.Fail) throw new OcrPipelineException(VisualErrorCodes.OcrOrientationCapabilityUnavailable, "A text-region orientation result was rejected by its confidence threshold.", stage, profileId: orientationResult.ProfileId, regionIndex: region.SourceIndex, backendId: orientationResult.BackendId, modelId: orientationResult.ModelId, technicalDetails: "confidence=" + orientationResult.Confidence.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                                    oriented[request.Position] = WithOrientation(region, orientationResult);
                                }
                            }
                            regions = Array.AsReadOnly(oriented);
                        }
                        finally { orientationWatch.Stop(); }
                        orientationDuration = orientationWatch.Elapsed;
                    }

                    stage = OcrPipelineStage.CropAndBatch;
                    var requests = new List<IndexedRequest>(regions.Count);
                    for (int index = 0; index < regions.Count; index++)
                    {
                        operationToken.ThrowIfCancellationRequested();
                        regionIndex = regions[index].SourceIndex;
                        requests.Add(new IndexedRequest(index, new TextCropRequest(regions[index], _cropProfile)));
                    }
                    regionIndex = null;
                    var recognized = new RecognizedText[regions.Count];
                    var cropWatch = Stopwatch.StartNew();
                    List<OcrBatchDescriptor> recognitionBatches = CreateBatches(requests, RecognitionSelection.Profile.Input.MinimumBatch, EffectiveMaximumBatch(RecognitionSelection), _options.MaximumRecognitionPaddingRatio, operationToken);
                    cropWatch.Stop();
                    TimeSpan cropDuration = cropWatch.Elapsed;
                    var recognitionWatch = Stopwatch.StartNew();
                    TimeSpan recognitionPreparationWork = TimeSpan.Zero;
                    TimeSpan recognitionInferenceWork = TimeSpan.Zero;
                    TimeSpan recognitionPostprocessingWork = TimeSpan.Zero;
                    try
                    {
                        stage = OcrPipelineStage.Recognition;
                        BatchExecution<VisualInferenceResult>[] recognitionResults = await RunBatchesAsync(input, RecognitionSelection.Profile.Input.Name, recognitionBatches, _recognizer.MaximumConcurrency, (prepared, token) => _recognizer.RunAsync(prepared, new VisualExecutionOptions(correlationId: execution.CorrelationId), token), operationToken).ConfigureAwait(false);
                        for (int batchIndex = 0; batchIndex < recognitionBatches.Count; batchIndex++)
                        {
                            OcrBatchDescriptor prepared = recognitionResults[batchIndex].Batch;
                            recognitionPreparationWork += recognitionResults[batchIndex].Preparation;
                            recognitionInferenceWork += recognitionResults[batchIndex].Result.Timing.Inference;
                            recognitionPostprocessingWork += recognitionResults[batchIndex].Result.Timing.Postprocessing;
                            TextRecognitionBatchResult batch = recognitionResults[batchIndex].Result.GetValue<TextRecognitionBatchResult>();
                            if (batch.Items.Count != prepared.Requests.Count) throw Failure("Recognizer result count does not match submitted batch.", stage, RecognitionSelection.Profile.ProfileId, prepared.Requests[0].Request.Region.SourceIndex, RecognitionSelection.Profile.Outputs[0].Name, "output=" + batch.Items.Count + ";input=" + prepared.Requests.Count);
                            for (int index = 0; index < prepared.ActualCount; index++)
                            {
                                IndexedRequest request = prepared.Requests[index];
                                recognized[request.Position] = batch.Items[index].WithSourceRegionIndex(request.Request.Region.SourceIndex);
                            }
                        }
                    }
                    finally { recognitionWatch.Stop(); }
                    TimeSpan recognitionDuration = recognitionWatch.Elapsed;

                    stage = OcrPipelineStage.Merge;
                    regionIndex = null;
                    var mergeWatch = Stopwatch.StartNew();
                    var results = new List<OcrRegionResult>(regions.Count);
                    long resultBytes = 0;
                    for (int index = 0; index < regions.Count; index++)
                    {
                        operationToken.ThrowIfCancellationRequested();
                        RecognizedText text = recognized[index] ?? throw Failure("OCR recognition did not produce every detected region.", stage, regionIndex: regions[index].SourceIndex);
                        results.Add(new OcrRegionResult(regions[index], text));
                        resultBytes = checked(resultBytes + EncodingBytes(text.Text) + checked((long)text.Tokens.Count * 40) + checked((long)regions[index].Polygon.Vertices.Count * 8));
                        if (resultBytes > _options.MaximumResultBytes) throw Limit("OCR owned result exceeds its byte limit.", stage, regionIndex: regions[index].SourceIndex, technicalDetails: "bytes=" + resultBytes);
                    }
                    mergeWatch.Stop();
                    var detailedTiming = new OcrDetailedStageTiming(
                        detectionInference.Timing.Inference,
                        detectionInference.Timing.Postprocessing,
                        recognitionPreparationWork,
                        recognitionInferenceWork,
                        recognitionPostprocessingWork,
                        recognitionBatches.Count);
                    return new OcrResult(results, input.SourceSize, DetectionSelection.Profile.ProfileId, DetectionSelection.Profile.ModelId, RecognitionSelection.Profile.ProfileId, RecognitionSelection.Profile.ModelId, new OcrStageTiming(detectionWatch.Elapsed, cropDuration, recognitionDuration, mergeWatch.Elapsed, orientationDuration, detailedTiming), orientation);
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, callerToken, stage, regionIndex); }
                catch (OcrPipelineException) { throw; }
                catch (DeploySharpException exception) when (operationToken.IsCancellationRequested) { throw MapCancellation(exception, callerToken, stage, regionIndex); }
                catch (Exception exception)
                {
                    string profile = stage == OcrPipelineStage.Recognition ? RecognitionSelection.Profile.ProfileId : DetectionSelection.Profile.ProfileId;
                    ModelId model = stage == OcrPipelineStage.Recognition ? RecognitionSelection.Profile.ModelId : DetectionSelection.Profile.ModelId;
                    throw new OcrPipelineException(VisualErrorCodes.OcrPipelineFailed, "An OCR pipeline stage failed.", stage, exception, profile, regionIndex, modelId: model, technicalDetails: exception.ToString());
                }
                finally
                {
                    if (entered) _operationGate.Release();
                    if (execution.DisposeInputOnCompletion) input.Dispose();
                }
            }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
            }
        }

        private int EffectiveMaximumBatch(VisualProfileSelection selection)
        {
            return Math.Min(_options.MaximumRecognitionBatch, selection.Profile.Input.MaximumBatch);
        }

        private static List<OcrBatchDescriptor> CreateBatches(List<IndexedRequest> requests, int minimumBatch, int maximumBatch, double maximumPaddingRatio, CancellationToken cancellationToken)
        {
            var sorted = new List<IndexedRequest>(requests);
            sorted.Sort((left, right) => { int width = left.Request.TargetWidth.CompareTo(right.Request.TargetWidth); return width != 0 ? width : left.Position.CompareTo(right.Position); });
            var batches = new List<OcrBatchDescriptor>();
            for (int offset = 0; offset < sorted.Count;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int limit = Math.Min(maximumBatch, sorted.Count - offset);
                int actual = 1;
                long naturalWidth = sorted[offset].Request.TargetWidth;
                for (int candidateIndex = 1; candidateIndex < limit; candidateIndex++)
                {
                    int candidateCount = candidateIndex + 1;
                    int candidateWidth = sorted[offset + candidateIndex].Request.TargetWidth;
                    naturalWidth = checked(naturalWidth + candidateWidth);
                    if (checked((double)candidateWidth * candidateCount) <= naturalWidth * maximumPaddingRatio) actual = candidateCount;
                }
                int padded = Math.Max(actual, minimumBatch);
                int targetWidth = sorted[offset + actual - 1].Request.TargetWidth;
                var batchRequests = new List<IndexedRequest>(padded);
                var crops = new List<TextCropRequest>(padded);
                for (int index = 0; index < actual; index++)
                {
                    IndexedRequest request = sorted[offset + index];
                    batchRequests.Add(request);
                    crops.Add(request.Request.TargetWidth == targetWidth ? request.Request : new TextCropRequest(request.Request.Region, request.Request.Profile, targetWidth));
                }
                while (batchRequests.Count < padded) { batchRequests.Add(batchRequests[batchRequests.Count - 1]); crops.Add(crops[crops.Count - 1]); }
                batches.Add(new OcrBatchDescriptor(batchRequests.AsReadOnly(), crops.AsReadOnly(), actual));
                offset += actual;
            }
            return batches;
        }

        private static async Task<BatchExecution<T>[]> RunBatchesAsync<T>(IOcrImageInput input, string inputName, IReadOnlyList<OcrBatchDescriptor> batches, int maximumConcurrency, Func<PreparedVisualInput, CancellationToken, Task<T>> run, CancellationToken cancellationToken)
        {
            if (batches.Count == 0) return Array.Empty<BatchExecution<T>>();
            int concurrency = Math.Min(batches.Count, Math.Max(1, maximumConcurrency));
            using var gate = new SemaphoreSlim(concurrency, concurrency);
            var tasks = new List<Task<BatchExecution<T>>>(batches.Count);
            try
            {
                for (int index = 0; index < batches.Count; index++)
                {
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    tasks.Add(ExecuteBatchAsync(input, inputName, batches[index], run, cancellationToken, gate));
                }
                return await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }
                throw;
            }
        }

        private static async Task<BatchExecution<T>> ExecuteBatchAsync<T>(IOcrImageInput input, string inputName, OcrBatchDescriptor batch, Func<PreparedVisualInput, CancellationToken, Task<T>> run, CancellationToken cancellationToken, SemaphoreSlim gate)
        {
            PreparedVisualInput? prepared = null;
            try
            {
                long preparationStarted = Stopwatch.GetTimestamp();
                prepared = input.PrepareRecognitionBatch(inputName, batch.Crops, cancellationToken) ?? throw new InvalidOperationException("The OCR image input returned a null prepared batch.");
                TimeSpan preparation = ElapsedSince(preparationStarted);
                T result = await run(prepared, cancellationToken).ConfigureAwait(false);
                return new BatchExecution<T>(batch, result, preparation);
            }
            finally
            {
                if (prepared != null) prepared.Dispose();
                gate.Release();
            }
        }

        private static TimeSpan ElapsedSince(long started)
        {
            long ticks = Stopwatch.GetTimestamp() - started;
            return TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
        }

        private void EnsureUsable()
        {
            lock (_lifetimeGate) if (_disposed) throw new OcrPipelineException(VisualErrorCodes.ObjectDisposed, "The OCR pipeline has been disposed.", OcrPipelineStage.Disposal);
        }

        private CancellationToken CaptureDisposeToken()
        {
            // Capture the token under the lifetime lock so a new call cannot race the CTS disposal window.
            // 在生命周期锁内捕获 token，避免新调用与 CTS 释放窗口竞态。
            lock (_lifetimeGate)
            {
                if (_disposed) throw new OcrPipelineException(VisualErrorCodes.ObjectDisposed, "The OCR pipeline has been disposed.", OcrPipelineStage.Disposal);
                return _disposeSource.Token;
            }
        }

        private OcrPipelineException MapCancellation(Exception exception, CancellationToken callerToken, OcrPipelineStage stage, int? regionIndex)
        {
            if (_disposed || _disposeSource.IsCancellationRequested) return new OcrPipelineException(VisualErrorCodes.ObjectDisposed, "The OCR pipeline was disposed during execution.", stage, exception, regionIndex: regionIndex);
            if (callerToken.IsCancellationRequested) return new OcrPipelineException(VisualErrorCodes.Cancelled, "OCR was cancelled by the caller.", stage, exception, regionIndex: regionIndex);
            return new OcrPipelineException(VisualErrorCodes.Timeout, "OCR exceeded its shared end-to-end timeout.", stage, exception, regionIndex: regionIndex);
        }

        private static void ValidateProfiles(VisualModelProfile detection, VisualModelProfile recognition, TextCropProfile crop, OcrPipelineOptions options)
        {
            if (detection.Task != VisualTaskId.TextDetection) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "The detector profile must use the text-detection task.", OcrPipelineStage.Input, profileId: detection.ProfileId, modelId: detection.ModelId);
            if (recognition.Task != VisualTaskId.TextRecognition) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "The recognizer profile must use the text-recognition task.", OcrPipelineStage.Input, profileId: recognition.ProfileId, modelId: recognition.ModelId);
            if (detection.Input.MinimumBatch != 1 || detection.Input.MaximumBatch < 1) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "The detector profile must accept batch one.", OcrPipelineStage.Input, profileId: detection.ProfileId, modelId: detection.ModelId);
            int heightIndex = recognition.Input.Layout == VisualTensorLayout.Nchw ? 2 : recognition.Input.Layout == VisualTensorLayout.Nhwc ? 1 : -1;
            int widthIndex = recognition.Input.Layout == VisualTensorLayout.Nchw ? 3 : recognition.Input.Layout == VisualTensorLayout.Nhwc ? 2 : -1;
            if (heightIndex < 0) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "OCR recognition requires a batched NCHW or NHWC profile.", OcrPipelineStage.Input, profileId: recognition.ProfileId, modelId: recognition.ModelId);
            long profileHeight = recognition.Input.ShapePattern[heightIndex];
            long profileWidth = recognition.Input.ShapePattern[widthIndex];
            if (profileHeight >= 0 && profileHeight != crop.TargetHeight) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "Recognition profile height does not match crop profile.", OcrPipelineStage.Input, profileId: recognition.ProfileId, modelId: recognition.ModelId);
            if (crop.WidthMode == OcrRecognitionWidthMode.Fixed && profileWidth >= 0 && profileWidth != crop.FixedWidth) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "Recognition profile width does not match fixed crop width.", OcrPipelineStage.Input, profileId: recognition.ProfileId, modelId: recognition.ModelId);
            if (recognition.Input.MinimumBatch > options.MaximumRecognitionBatch) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "Recognition minimum batch exceeds OCR batch limit.", OcrPipelineStage.Input, profileId: recognition.ProfileId, modelId: recognition.ModelId);
        }

        private static void ValidateOrientationProfile(VisualModelProfile orientation, TextCropProfile crop)
        {
            if (orientation.Task != VisualTaskId.TextOrientationClassification) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "The orientation profile must use the text-orientation-classification task.", OcrPipelineStage.Input, profileId: orientation.ProfileId, modelId: orientation.ModelId);
            if (orientation.Input.MinimumBatch <= 0 || orientation.Input.MaximumBatch < orientation.Input.MinimumBatch) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "Per-region orientation requires valid positive batch bounds.", OcrPipelineStage.Input, profileId: orientation.ProfileId, modelId: orientation.ModelId);
            int heightIndex = orientation.Input.Layout == VisualTensorLayout.Nchw ? 2 : orientation.Input.Layout == VisualTensorLayout.Nhwc ? 1 : -1;
            int widthIndex = orientation.Input.Layout == VisualTensorLayout.Nchw ? 3 : orientation.Input.Layout == VisualTensorLayout.Nhwc ? 2 : -1;
            if (heightIndex < 0 || (orientation.Input.ShapePattern[heightIndex] >= 0 && orientation.Input.ShapePattern[heightIndex] != crop.TargetHeight) || (orientation.Input.ShapePattern[widthIndex] >= 0 && orientation.Input.ShapePattern[widthIndex] != crop.FixedWidth)) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "Orientation input dimensions do not match its fixed crop profile.", OcrPipelineStage.Input, profileId: orientation.ProfileId, modelId: orientation.ModelId);
            if (crop.WidthMode != OcrRecognitionWidthMode.Fixed) throw new OcrPipelineException(VisualErrorCodes.ProfileInvalid, "Per-region orientation requires a fixed-width crop profile.", OcrPipelineStage.Input, profileId: orientation.ProfileId, modelId: orientation.ModelId);
        }

        private static TextRegion WithOrientation(TextRegion region, OcrOrientationResult orientation)
        {
            var metadata = new List<KeyValuePair<string, string>>(region.Metadata.Count + 7);
            foreach (KeyValuePair<string, string> pair in region.Metadata) metadata.Add(pair);
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.strategy", "per-text-region"));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.profileId", orientation.ProfileId));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.modelId", orientation.ModelId.Value));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.backendId", orientation.BackendId.Value));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.classIndex", orientation.ClassIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.confidence", orientation.Confidence.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.rejected", orientation.Rejected ? "true" : "false"));
            metadata.Add(new KeyValuePair<string, string>("ocr.orientation.canonicalSha256", orientation.CanonicalSha256));
            return new TextRegion(region.SourceIndex, region.Score, region.Polygon, region.CropQuadrilateral, orientation.Orientation, region.AngleRadians, region.Language, region.Script, region.ExternalId, metadata);
        }

        private static long EncodingBytes(string value) => Encoding.UTF8.GetByteCount(value);
        private static OcrPipelineException Failure(string message, OcrPipelineStage stage, string? profileId = null, int? regionIndex = null, string? tensorName = null, string? technicalDetails = null) => new OcrPipelineException(VisualErrorCodes.OcrPipelineFailed, message, stage, profileId: profileId, regionIndex: regionIndex, tensorName: tensorName, technicalDetails: technicalDetails);
        private static OcrPipelineException Limit(string message, OcrPipelineStage stage, string? profileId = null, int? regionIndex = null, string? technicalDetails = null) => new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, message, stage, profileId: profileId, regionIndex: regionIndex, technicalDetails: technicalDetails);

        private sealed class IndexedRequest
        {
            public IndexedRequest(int position, TextCropRequest request) { Position = position; Request = request; }
            public int Position { get; }
            public TextCropRequest Request { get; }
        }

        private sealed class OcrBatchDescriptor
        {
            public OcrBatchDescriptor(IReadOnlyList<IndexedRequest> requests, IReadOnlyList<TextCropRequest> crops, int actualCount) { Requests = requests; Crops = crops; ActualCount = actualCount; }
            public IReadOnlyList<IndexedRequest> Requests { get; }
            public IReadOnlyList<TextCropRequest> Crops { get; }
            public int ActualCount { get; }
        }

        private sealed class BatchExecution<T>
        {
            public BatchExecution(OcrBatchDescriptor batch, T result, TimeSpan preparation) { Batch = batch; Result = result; Preparation = preparation; }
            public OcrBatchDescriptor Batch { get; }
            public T Result { get; }
            public TimeSpan Preparation { get; }
        }
    }
}
