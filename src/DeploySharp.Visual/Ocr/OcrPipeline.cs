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
        Disposal = 5
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
        public OcrPipelineOptions(int maximumConcurrency = 1, int maximumRegions = 128, int maximumRecognitionBatch = 16, long maximumSourcePixels = 128L * 1024L * 1024L, long maximumResultBytes = 16L * 1024L * 1024L)
        {
            if (maximumConcurrency <= 0 || maximumRegions <= 0 || maximumRecognitionBatch <= 0 || maximumSourcePixels <= 0 || maximumResultBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
            MaximumConcurrency = maximumConcurrency;
            MaximumRegions = maximumRegions;
            MaximumRecognitionBatch = maximumRecognitionBatch;
            MaximumSourcePixels = maximumSourcePixels;
            MaximumResultBytes = maximumResultBytes;
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
        private readonly TextCropProfile _cropProfile;
        private readonly OcrPipelineOptions _options;
        private readonly SemaphoreSlim _operationGate;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private bool _disposed;

        /// <summary>Creates and owns detector and recognizer sessions selected through the shared Core backend registry. / 通过共享 Core 后端注册中心创建并拥有检测器和识别器会话。</summary>
        public OcrPipeline(BackendRegistry backendRegistry, VisualProfileSelection detectionSelection, BackendRequest detectionRequest, VisualProfileSelection recognitionSelection, BackendRequest recognitionRequest, TextCropProfile cropProfile, OcrPipelineOptions? options = null, SessionOptions? detectionSessionOptions = null, SessionOptions? recognitionSessionOptions = null)
        {
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
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
                detector = new VisualPipeline(backendRegistry, detectionSelection, detectionRequest, detectionSessionOptions);
                _detector = detector;
                _recognizer = new VisualPipeline(backendRegistry, recognitionSelection, recognitionRequest, recognitionSessionOptions);
            }
            catch
            {
                detector?.Dispose();
                _operationGate.Dispose();
                _disposeSource.Dispose();
                throw;
            }
        }

        /// <summary>Gets detector selection. / 获取检测器选择。</summary>
        public VisualProfileSelection DetectionSelection { get; }
        /// <summary>Gets recognizer selection. / 获取识别器选择。</summary>
        public VisualProfileSelection RecognitionSelection { get; }
        /// <summary>Gets crop profile. / 获取裁剪 Profile。</summary>
        public TextCropProfile CropProfile => _cropProfile;
        /// <summary>Gets pipeline bounds. / 获取 Pipeline 边界。</summary>
        public OcrPipelineOptions Options => _options;

        /// <summary>Runs synchronous end-to-end OCR without thread-pool wrapping. / 在不包装线程池任务的情况下运行同步端到端 OCR。</summary>
        public OcrResult Run(IOcrImageInput input, OcrExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAsync(input, options ?? OcrExecutionOptions.Default, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Runs end-to-end OCR using backend asynchronous paths where available. / 在后端可用时使用异步路径运行端到端 OCR。</summary>
        public Task<OcrResult> RunAsync(IOcrImageInput input, OcrExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAsync(input, options ?? OcrExecutionOptions.Default, cancellationToken);
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

        private async Task<OcrResult> ExecuteAsync(IOcrImageInput input, OcrExecutionOptions execution, CancellationToken callerToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken disposeToken = CaptureDisposeToken();
            bool entered = false;
            OcrPipelineStage stage = OcrPipelineStage.Input;
            int? regionIndex = null;
            using (var timeoutSource = execution.Timeout.HasValue ? new CancellationTokenSource(execution.Timeout.Value) : new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutSource.Token, disposeToken))
            {
                try
                {
                    long sourcePixels = checked((long)input.SourceSize.Width * input.SourceSize.Height);
                    if (sourcePixels > _options.MaximumSourcePixels) throw Limit("OCR source image exceeds its pixel limit.", stage, technicalDetails: "pixels=" + sourcePixels);
                    if (input.DetectionInput.SourceSize != input.SourceSize) throw Failure("OCR input source size does not match detector input.", stage);
                    await _operationGate.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    EnsureUsable();

                    stage = OcrPipelineStage.Detection;
                    var detectionWatch = Stopwatch.StartNew();
                    VisualInferenceResult detectionInference = await _detector.RunAsync(input.DetectionInput, new VisualExecutionOptions(correlationId: execution.CorrelationId), linked.Token).ConfigureAwait(false);
                    TextDetectionResult detection = detectionInference.GetValue<TextDetectionResult>();
                    detectionWatch.Stop();
                    if (detection.Regions.Count > _options.MaximumRegions) throw Limit("OCR detector returned more regions than the pipeline limit.", stage, DetectionSelection.Profile.ProfileId, technicalDetails: "regions=" + detection.Regions.Count);

                    stage = OcrPipelineStage.CropAndBatch;
                    var requests = new List<IndexedRequest>(detection.Regions.Count);
                    for (int index = 0; index < detection.Regions.Count; index++)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        regionIndex = detection.Regions[index].SourceIndex;
                        requests.Add(new IndexedRequest(index, new TextCropRequest(detection.Regions[index], _cropProfile)));
                    }
                    regionIndex = null;
                    var recognized = new RecognizedText[detection.Regions.Count];
                    var cropDuration = TimeSpan.Zero;
                    var recognitionDuration = TimeSpan.Zero;
                    SortedDictionary<int, List<IndexedRequest>> groups = GroupByWidth(requests);
                    foreach (KeyValuePair<int, List<IndexedRequest>> group in groups)
                    {
                        int offset = 0;
                        while (offset < group.Value.Count)
                        {
                            linked.Token.ThrowIfCancellationRequested();
                            int available = Math.Min(EffectiveMaximumBatch(), group.Value.Count - offset);
                            int actual = available;
                            var batchRequests = new List<TextCropRequest>(Math.Max(actual, RecognitionSelection.Profile.Input.MinimumBatch));
                            for (int index = 0; index < actual; index++) batchRequests.Add(group.Value[offset + index].Request);
                            while (batchRequests.Count < RecognitionSelection.Profile.Input.MinimumBatch) batchRequests.Add(batchRequests[batchRequests.Count - 1]);
                            regionIndex = batchRequests[0].Region.SourceIndex;
                            var cropWatch = Stopwatch.StartNew();
                            using (PreparedVisualInput recognitionInput = input.PrepareRecognitionBatch(RecognitionSelection.Profile.Input.Name, batchRequests.AsReadOnly(), linked.Token))
                            {
                                cropWatch.Stop();
                                cropDuration += cropWatch.Elapsed;
                                stage = OcrPipelineStage.Recognition;
                                var recognitionWatch = Stopwatch.StartNew();
                                VisualInferenceResult recognitionInference = await _recognizer.RunAsync(recognitionInput, new VisualExecutionOptions(correlationId: execution.CorrelationId), linked.Token).ConfigureAwait(false);
                                TextRecognitionBatchResult batch = recognitionInference.GetValue<TextRecognitionBatchResult>();
                                recognitionWatch.Stop();
                                recognitionDuration += recognitionWatch.Elapsed;
                                if (batch.Items.Count != batchRequests.Count) throw Failure("Recognizer result count does not match submitted batch.", stage, RecognitionSelection.Profile.ProfileId, regionIndex, RecognitionSelection.Profile.Outputs[0].Name, "output=" + batch.Items.Count + ";input=" + batchRequests.Count);
                                for (int index = 0; index < actual; index++)
                                {
                                    IndexedRequest request = group.Value[offset + index];
                                    recognized[request.Position] = batch.Items[index].WithSourceRegionIndex(request.Request.Region.SourceIndex);
                                }
                            }
                            stage = OcrPipelineStage.CropAndBatch;
                            offset += actual;
                        }
                    }

                    stage = OcrPipelineStage.Merge;
                    regionIndex = null;
                    var mergeWatch = Stopwatch.StartNew();
                    var results = new List<OcrRegionResult>(detection.Regions.Count);
                    long resultBytes = 0;
                    for (int index = 0; index < detection.Regions.Count; index++)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        RecognizedText text = recognized[index] ?? throw Failure("OCR recognition did not produce every detected region.", stage, regionIndex: detection.Regions[index].SourceIndex);
                        results.Add(new OcrRegionResult(detection.Regions[index], text));
                        resultBytes = checked(resultBytes + EncodingBytes(text.Text) + checked((long)text.Tokens.Count * 40) + checked((long)detection.Regions[index].Polygon.Vertices.Count * 8));
                        if (resultBytes > _options.MaximumResultBytes) throw Limit("OCR owned result exceeds its byte limit.", stage, regionIndex: detection.Regions[index].SourceIndex, technicalDetails: "bytes=" + resultBytes);
                    }
                    mergeWatch.Stop();
                    return new OcrResult(results, input.SourceSize, DetectionSelection.Profile.ProfileId, DetectionSelection.Profile.ModelId, RecognitionSelection.Profile.ProfileId, RecognitionSelection.Profile.ModelId, new OcrStageTiming(detectionWatch.Elapsed, cropDuration, recognitionDuration, mergeWatch.Elapsed));
                }
                catch (OperationCanceledException exception) { throw MapCancellation(exception, callerToken, stage, regionIndex); }
                catch (OcrPipelineException) { throw; }
                catch (DeploySharpException exception) when (linked.IsCancellationRequested) { throw MapCancellation(exception, callerToken, stage, regionIndex); }
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
        }

        private int EffectiveMaximumBatch()
        {
            return Math.Min(_options.MaximumRecognitionBatch, RecognitionSelection.Profile.Input.MaximumBatch);
        }

        private static SortedDictionary<int, List<IndexedRequest>> GroupByWidth(List<IndexedRequest> requests)
        {
            var groups = new SortedDictionary<int, List<IndexedRequest>>();
            foreach (IndexedRequest request in requests)
            {
                if (!groups.TryGetValue(request.Request.TargetWidth, out List<IndexedRequest>? group)) { group = new List<IndexedRequest>(); groups.Add(request.Request.TargetWidth, group); }
                group.Add(request);
            }
            return groups;
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

        private static long EncodingBytes(string value) => Encoding.UTF8.GetByteCount(value);
        private static OcrPipelineException Failure(string message, OcrPipelineStage stage, string? profileId = null, int? regionIndex = null, string? tensorName = null, string? technicalDetails = null) => new OcrPipelineException(VisualErrorCodes.OcrPipelineFailed, message, stage, profileId: profileId, regionIndex: regionIndex, tensorName: tensorName, technicalDetails: technicalDetails);
        private static OcrPipelineException Limit(string message, OcrPipelineStage stage, string? profileId = null, int? regionIndex = null, string? technicalDetails = null) => new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, message, stage, profileId: profileId, regionIndex: regionIndex, technicalDetails: technicalDetails);

        private sealed class IndexedRequest
        {
            public IndexedRequest(int position, TextCropRequest request) { Position = position; Request = request; }
            public int Position { get; }
            public TextCropRequest Request { get; }
        }
    }
}
