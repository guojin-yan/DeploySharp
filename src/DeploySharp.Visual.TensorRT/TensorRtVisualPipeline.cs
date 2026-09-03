using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    /// <summary>Runs compact BGR images through CUDA preprocessing and TensorRT on one stream while reusing all fixed device buffers. / 在同一 stream 上通过 CUDA 预处理与 TensorRT 运行紧凑 BGR 图像，并复用所有固定设备缓冲区。</summary>
    public sealed class TensorRtVisualPipeline : IDisposable
    {
        private readonly TensorRtVisualExecutionGate _gate = new TensorRtVisualExecutionGate();
        private readonly VisualModelProfile _profile;
        private readonly OpenCvPreprocessOptions _preprocessing;
        private readonly TensorRtBackendProvider _provider;
        private readonly IInferenceSession _session;
        private readonly ITensorRtDeviceInferenceSession _deviceSession;
        private readonly CudaStream _stream;
        private readonly CudaEvent _preprocessStartEvent;
        private readonly CudaEvent _preprocessEndEvent;
        private readonly CudaEvent _inferenceEndEvent;
        private readonly TensorRtCudaCompiledKernel _preprocessKernel;
        private readonly CudaMemory _inputMemory;
        private readonly TensorRtDeviceTensor _deviceInput;
        private readonly IReadOnlyList<TensorRtDeviceTensor> _deviceInputs;
        private readonly TensorRtCudaDeviceBuffer _kernelInput;
        private readonly ChannelNormalization _normalization;
        private readonly List<OutputSlot> _outputSlots;
        private readonly IReadOnlyList<TensorRtDeviceTensor> _deviceOutputs;
        private readonly InferenceOutputs _managedOutputs;
        private readonly MapPostprocessor? _mapPostprocessor;
        private readonly YoloMaskPostprocessor? _yoloMaskPostprocessor;
        private CudaMemory? _sourceMemory;
        private TensorRtCudaDeviceBuffer? _sourceBuffer;
        private TensorRtCudaVisualPreprocessPlan? _preprocessPlan;
        private int _sourceBytes;
        private int _sourceWidth;
        private int _sourceHeight;
        private PreparedVisualInput? _decodeInput;
        private VisualSize _decodeSourceSize;
        private Geometry _decodeGeometry;
        private bool _disposed;

        /// <summary>Initializes a static batch-one GPU visual pipeline over a caller-owned serialized engine path. / 使用调用方拥有的序列化引擎路径初始化静态单批 GPU 视觉流水线。</summary>
        public TensorRtVisualPipeline(
            VisualModelProfile profile,
            string enginePath,
            OpenCvPreprocessOptions preprocessing,
            TensorRtBackendOptions backendOptions,
            TensorRtCudaVisualPostprocessingMode postprocessingMode = TensorRtCudaVisualPostprocessingMode.WhenSupported)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _preprocessing = preprocessing ?? throw new ArgumentNullException(nameof(preprocessing));
            if (backendOptions == null) throw new ArgumentNullException(nameof(backendOptions));
            if (!Enum.IsDefined(typeof(TensorRtCudaVisualPostprocessingMode), postprocessingMode)) throw new ArgumentOutOfRangeException(nameof(postprocessingMode));
            if (string.IsNullOrWhiteSpace(enginePath)) throw new ArgumentException("A TensorRT engine path is required.", nameof(enginePath));
            string architecture = backendOptions.CudaTargetArchitecture ?? throw new ArgumentException("A CUDA target architecture is required for GPU visual preprocessing.", nameof(backendOptions));
            TensorRtVisualContracts.ValidatePreprocessing(profile, preprocessing);
            _normalization = ResolveNormalization(preprocessing);

            _provider = new TensorRtBackendProvider(backendOptions);
            try
            {
                var artifact = new ModelArtifact(profile.ModelId, "tensorrt-engine", enginePath, preferredBackend: TensorRtBackendProvider.BackendId);
                _session = _provider.CreateSession(
                    artifact,
                    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                    new SessionOptions(1));
                _deviceSession = _session as ITensorRtDeviceInferenceSession ?? throw new NotSupportedException("The TensorRT provider did not expose device inference.");
                TensorRtVisualContracts.ValidateMetadata(_session.Metadata, profile);
                _stream = new CudaStream();
                _preprocessStartEvent = new CudaEvent();
                _preprocessEndEvent = new CudaEvent();
                _inferenceEndEvent = new CudaEvent();
                var compileOptions = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: false);
                _preprocessKernel = TensorRtCudaCompiledKernel.Load(TensorRtCudaRtcCompiler.Compile(TensorRtCudaVisualKernels.NormalizeBgrNchwDefinition, compileOptions), _deviceSession.DeviceOrdinal);

                TensorDescriptor input = _session.Metadata.Inputs[0];
                _inputMemory = new CudaMemory(ByteLength(input));
                _deviceInput = new TensorRtDeviceTensor(input.Name, input.ElementType, input.Shape, _inputMemory);
                _deviceInputs = new[] { _deviceInput };
                _kernelInput = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor(input.Name, input.ElementType, input.Shape, TensorRtCudaBufferAccess.Write), _inputMemory);
                _outputSlots = _session.Metadata.Outputs.Select(OutputSlot.Create).ToList();
                _deviceOutputs = _outputSlots.Select(slot => slot.DeviceTensor).ToList().AsReadOnly();
                _managedOutputs = new InferenceOutputs(_outputSlots.Select(slot => new NamedTensor(slot.Name, slot.Tensor)));
                if (postprocessingMode == TensorRtCudaVisualPostprocessingMode.WhenSupported)
                {
                    _mapPostprocessor = MapPostprocessor.TryCreate(profile, _outputSlots, architecture, _deviceSession.DeviceOrdinal);
                    if (_mapPostprocessor == null) _yoloMaskPostprocessor = YoloMaskPostprocessor.TryCreate(profile, _outputSlots, architecture, _deviceSession.DeviceOrdinal);
                }
            }
            catch
            {
                DisposePartiallyConstructed();
                throw;
            }
        }

        /// <summary>Gets the immutable visual model contract used for decoding. / 获取用于解码的不可变视觉模型合同。</summary>
        public VisualModelProfile Profile => _profile;

        /// <summary>Gets whether this profile is using the admitted CUDA map-postprocessing path. / 获取此 Profile 是否正在使用已准入的 CUDA 图后处理路径。</summary>
        public bool UsesCudaPostprocessing => _mapPostprocessor != null || _yoloMaskPostprocessor != null;

        /// <summary>Runs one compact BGR frame; output arrays and fixed device allocations are reused by subsequent calls. / 运行一个紧凑 BGR 帧；后续调用会复用输出数组与固定设备分配。</summary>
        public VisualInferenceResult Run(OpenCvBgrImage image, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            lock (_gate.SyncRoot)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                EnsureSourceBuffer(image);
                PreparedVisualInput decodeInput = GetDecodeInput(image);
                EnsurePreprocessPlan(image);

                var uploadWatch = Stopwatch.StartNew();
                _sourceMemory!.CopyFrom(image.GetReadOnlyInteropBuffer());
                uploadWatch.Stop();

                TensorRtCudaKernelLaunch? preprocessLaunch = null;
                TensorRtCudaKernelLaunch? postprocessLaunch = null;
                TensorRtCudaKernelLaunch? yoloCandidateLaunch = null;
                TensorRtDeviceInferenceExecution? execution = null;
                try
                {
                    _preprocessStartEvent.Record(_stream);
                    preprocessLaunch = _preprocessPlan!.Launch(_stream);
                    _preprocessEndEvent.Record(_stream);
                    execution = _deviceSession.RunDevice(_deviceInputs, _deviceOutputs, _stream, cancellationToken);
                    _inferenceEndEvent.Record(_stream);
                    if (_mapPostprocessor != null) postprocessLaunch = _mapPostprocessor.Enqueue(decodeInput, _stream);
                    else if (_yoloMaskPostprocessor != null) yoloCandidateLaunch = _yoloMaskPostprocessor.EnqueueCandidateFilter(_stream);
                    execution.Synchronize();
                }
                finally
                {
                    execution?.ReleaseAfterEnqueue();
                    yoloCandidateLaunch?.Dispose();
                    postprocessLaunch?.Dispose();
                    preprocessLaunch?.Dispose();
                }
                TimeSpan cudaPreprocessing = TimeSpan.FromMilliseconds(_preprocessEndEvent.ElapsedTimeSince(_preprocessStartEvent));
                TimeSpan cudaInference = TimeSpan.FromMilliseconds(_inferenceEndEvent.ElapsedTimeSince(_preprocessEndEvent));

                TimeSpan preprocessing = uploadWatch.Elapsed + cudaPreprocessing;
                var decodeContext = new VisualDecodeContext(decodeInput, _profile, _managedOutputs, cancellationToken);
                if (_mapPostprocessor != null)
                {
                    var postprocessWatch = Stopwatch.StartNew();
                    object? decoded = _mapPostprocessor.TryDecode(decodeContext);
                    if (decoded == null)
                    {
                        for (int index = 0; index < _outputSlots.Count; index++) _outputSlots[index].CopyToManaged();
                        decoded = _profile.Decoder.Decode(decodeContext);
                    }
                    postprocessWatch.Stop();
                    TimeSpan postprocessing = _mapPostprocessor.LastCudaDuration + postprocessWatch.Elapsed;
                    return new VisualInferenceResult(decoded, _profile.Task, _profile.ModelId, TensorRtBackendProvider.BackendId, new InferenceTiming(preprocessing, cudaInference, postprocessing));
                }

                if (_yoloMaskPostprocessor != null)
                {
                    var postprocessWatch = Stopwatch.StartNew();
                    object? decoded = _yoloMaskPostprocessor.TryRun(decodeContext, _stream);
                    if (decoded == null)
                    {
                        for (int index = 0; index < _outputSlots.Count; index++) _outputSlots[index].CopyToManaged();
                        decoded = _profile.Decoder.Decode(decodeContext);
                    }
                    postprocessWatch.Stop();
                    TimeSpan postprocessing = _yoloMaskPostprocessor.LastCandidateFilterDuration + postprocessWatch.Elapsed;
                    return new VisualInferenceResult(decoded, _profile.Task, _profile.ModelId, TensorRtBackendProvider.BackendId, new InferenceTiming(preprocessing, cudaInference, postprocessing));
                }

                var outputWatch = Stopwatch.StartNew();
                for (int index = 0; index < _outputSlots.Count; index++) _outputSlots[index].CopyToManaged();
                outputWatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                var decodeWatch = Stopwatch.StartNew();
                object cpuDecoded = _profile.Decoder.Decode(decodeContext);
                decodeWatch.Stop();
                TimeSpan inference = cudaInference + outputWatch.Elapsed;
                return new VisualInferenceResult(cpuDecoded, _profile.Task, _profile.ModelId, TensorRtBackendProvider.BackendId, new InferenceTiming(preprocessing, inference, decodeWatch.Elapsed));
            }
        }

        /// <summary>
        /// Releases the TensorRT session, CUDA stream, events, kernels, and reusable device buffers.
        /// 释放 TensorRT 会话、CUDA 流、事件、内核以及可复用的设备缓冲区。
        /// </summary>
        public void Dispose()
        {
            // Run and Dispose must share the same gate; locking the gate holder
            // itself would allow disposal to race a call holding SyncRoot.
            lock (_gate.SyncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                _decodeInput?.Dispose();
                _sourceMemory?.Dispose();
                _yoloMaskPostprocessor?.Dispose();
                _mapPostprocessor?.Dispose();
                for (int index = _outputSlots.Count - 1; index >= 0; index--) _outputSlots[index].Dispose();
                _inputMemory.Dispose();
                _preprocessKernel.Dispose();
                _inferenceEndEvent.Dispose();
                _preprocessEndEvent.Dispose();
                _preprocessStartEvent.Dispose();
                _stream.Dispose();
                _session.Dispose();
                _provider.Dispose();
            }
        }

        private void EnsureSourceBuffer(OpenCvBgrImage image)
        {
            if (_sourceMemory != null && _sourceBytes >= image.ByteLength)
            {
                if (_sourceWidth == image.Width && _sourceHeight == image.Height) return;
                _sourceBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("source-bgr", TensorElementType.UInt8, new TensorShape(image.Height, image.Width, 3), TensorRtCudaBufferAccess.Read), _sourceMemory);
                _sourceWidth = image.Width;
                _sourceHeight = image.Height;
                _preprocessPlan = null;
                return;
            }
            _sourceMemory?.Dispose();
            _sourceMemory = new CudaMemory(image.ByteLength);
            _sourceBytes = image.ByteLength;
            _sourceWidth = image.Width;
            _sourceHeight = image.Height;
            _sourceBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("source-bgr", TensorElementType.UInt8, new TensorShape(image.Height, image.Width, 3), TensorRtCudaBufferAccess.Read), _sourceMemory);
            _preprocessPlan = null;
        }

        private void EnsurePreprocessPlan(OpenCvBgrImage image)
        {
            if (_preprocessPlan != null) return;
            Geometry geometry = _decodeGeometry;
            _preprocessPlan = TensorRtCudaVisualKernels.CreateNormalizeBgrNchwPlan(
                _preprocessKernel,
                _sourceBuffer!,
                _kernelInput,
                image.Width,
                image.Height,
                _preprocessing.ModelSize.Width,
                _preprocessing.ModelSize.Height,
                geometry.ResizedWidth,
                geometry.ResizedHeight,
                geometry.Left,
                geometry.Top,
                _preprocessing.PaddingColor.Blue,
                _preprocessing.PaddingColor.Green,
                _preprocessing.PaddingColor.Red,
                _normalization.Mean0,
                _normalization.Mean1,
                _normalization.Mean2,
                _normalization.Scale0,
                _normalization.Scale1,
                _normalization.Scale2,
                _preprocessing.ColorOrder == VisualColorOrder.Rgb);
        }

        private PreparedVisualInput GetDecodeInput(OpenCvBgrImage image)
        {
            var sourceSize = new VisualSize(image.Width, image.Height);
            if (_decodeInput != null && _decodeSourceSize == sourceSize) return _decodeInput;
            _decodeInput?.Dispose();
            Geometry geometry = ResolveGeometry(image.Width, image.Height, _preprocessing);
            _decodeGeometry = geometry;
            ImageTransform transform = geometry.ToTransform(sourceSize, _preprocessing.ModelSize);
            var means = Expand(_preprocessing.Means, 0f);
            var scales = Expand(_preprocessing.StandardDeviations, 1f).Select(value => 1f / value).ToArray();
            var descriptor = new VisualPreprocessingDescriptor(_preprocessing.ColorOrder, means, scales, "CUDA fused BGR resize/pad/channel-convert/normalize; compact UInt8 upload.");
            _decodeInput = new PreparedVisualInput(
                _profile.Input.Name,
                new MetadataTensor(_profile.Input.ElementType, _session.Metadata.Inputs[0].Shape),
                sourceSize,
                _preprocessing.ModelSize,
                1,
                VisualTensorLayout.Nchw,
                transform,
                descriptor,
                image.InputId);
            _decodeSourceSize = sourceSize;
            return _decodeInput;
        }

        private static Geometry ResolveGeometry(int sourceWidth, int sourceHeight, OpenCvPreprocessOptions options)
        {
            if (options.ResizeMode == OpenCvResizeMode.Resize) return new Geometry(options.ModelSize.Width, options.ModelSize.Height, 0, 0, ImageTransformKind.Resize);
            double scale = Math.Min((double)options.ModelSize.Width / sourceWidth, (double)options.ModelSize.Height / sourceHeight);
            int resizedWidth = Math.Max(1, Math.Min(options.ModelSize.Width, Round(sourceWidth * scale, options.LetterboxRounding)));
            int resizedHeight = Math.Max(1, Math.Min(options.ModelSize.Height, Round(sourceHeight * scale, options.LetterboxRounding)));
            bool bottomRight = options.ResizeMode == OpenCvResizeMode.LongestSidePadBottomRight;
            int left = bottomRight ? 0 : (options.ModelSize.Width - resizedWidth) / 2;
            int top = bottomRight ? 0 : (options.ModelSize.Height - resizedHeight) / 2;
            return new Geometry(resizedWidth, resizedHeight, left, top, ImageTransformKind.Letterbox);
        }

        private static int Round(double value, OpenCvLetterboxRounding rounding)
        {
            if (rounding == OpenCvLetterboxRounding.Floor) return checked((int)Math.Floor(value));
            if (rounding == OpenCvLetterboxRounding.HalfUp) return checked((int)Math.Floor(value + .5));
            return checked((int)Math.Round(value));
        }

        private static ChannelNormalization ResolveNormalization(OpenCvPreprocessOptions options)
        {
            float[] means = Expand(options.Means, 0f);
            float[] deviations = Expand(options.StandardDeviations, 1f);
            float[] divisors = Expand(options.InputDivisors, 1f);
            return new ChannelNormalization(
                means[0] * divisors[0], means[1] * divisors[1], means[2] * divisors[2],
                1f / (deviations[0] * divisors[0]), 1f / (deviations[1] * divisors[1]), 1f / (deviations[2] * divisors[2]));
        }

        private static float[] Expand(IReadOnlyList<float> values, float fallback)
        {
            var result = new float[3];
            if (values.Count == 0) { result[0] = result[1] = result[2] = fallback; }
            else if (values.Count == 1) { result[0] = result[1] = result[2] = values[0]; }
            else { result[0] = values[0]; result[1] = values[1]; result[2] = values[2]; }
            return result;
        }

        private static int ByteLength(TensorDescriptor descriptor) => checked((int)(descriptor.Shape.GetElementCount() * ElementSize(descriptor.ElementType)));

        private static int ElementSize(TensorElementType type) => type switch
        {
            TensorElementType.Boolean or TensorElementType.Int8 or TensorElementType.UInt8 => 1,
            TensorElementType.Int16 or TensorElementType.UInt16 or TensorElementType.Float16 or TensorElementType.BFloat16 => 2,
            TensorElementType.Int32 or TensorElementType.UInt32 or TensorElementType.Float32 => 4,
            TensorElementType.Int64 or TensorElementType.UInt64 or TensorElementType.Float64 => 8,
            _ => throw new NotSupportedException("The TensorRT tensor element width is unsupported.")
        };

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TensorRtVisualPipeline));
        }

        private void DisposePartiallyConstructed()
        {
            if (_outputSlots != null)
            {
                for (int index = _outputSlots.Count - 1; index >= 0; index--)
                {
                    try { _outputSlots[index].Dispose(); } catch { }
                }
            }
            try { _sourceMemory?.Dispose(); } catch { }
            try { _mapPostprocessor?.Dispose(); } catch { }
            try { _inputMemory?.Dispose(); } catch { }
            try { _preprocessKernel?.Dispose(); } catch { }
            try { _inferenceEndEvent?.Dispose(); } catch { }
            try { _preprocessEndEvent?.Dispose(); } catch { }
            try { _preprocessStartEvent?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _session?.Dispose(); } catch { }
            try { _provider?.Dispose(); } catch { }
        }

        private readonly struct Geometry
        {
            public Geometry(int resizedWidth, int resizedHeight, int left, int top, ImageTransformKind kind) { ResizedWidth = resizedWidth; ResizedHeight = resizedHeight; Left = left; Top = top; Kind = kind; }
            public int ResizedWidth { get; }
            public int ResizedHeight { get; }
            public int Left { get; }
            public int Top { get; }
            public ImageTransformKind Kind { get; }
            public ImageTransform ToTransform(VisualSize source, VisualSize model) => Kind == ImageTransformKind.Resize
                ? ImageTransform.Resize(source, model)
                : new ImageTransform(ImageTransformKind.Letterbox, source, model, (float)ResizedWidth / source.Width, (float)ResizedHeight / source.Height, Left, Top);
        }

        private readonly struct ChannelNormalization
        {
            public ChannelNormalization(float mean0, float mean1, float mean2, float scale0, float scale1, float scale2) { Mean0 = mean0; Mean1 = mean1; Mean2 = mean2; Scale0 = scale0; Scale1 = scale1; Scale2 = scale2; }
            public float Mean0 { get; }
            public float Mean1 { get; }
            public float Mean2 { get; }
            public float Scale0 { get; }
            public float Scale1 { get; }
            public float Scale2 { get; }
        }

        private sealed class MetadataTensor : ITensor
        {
            private static readonly float[] Empty = Array.Empty<float>();
            public MetadataTensor(TensorElementType elementType, TensorShape shape) { ElementType = elementType; Shape = shape; Length = shape.GetElementCount(); }
            public TensorElementType ElementType { get; }
            public TensorShape Shape { get; }
            public long Length { get; }
            public Array Buffer => Empty;
            public TensorBufferOwnership Ownership => TensorBufferOwnership.Transfer;
        }

        private sealed class YoloMaskPostprocessor : IDisposable
        {
            private readonly YoloInstanceSegmentationDecoder _decoder;
            private readonly TensorRtCudaCompiledKernel _filterKernel;
            private readonly TensorRtCudaCompiledKernel _combineKernel;
            private readonly TensorRtCudaCompiledKernel _restoreKernel;
            private readonly TensorRtCudaDeviceBuffer _prototypes;
            private readonly CudaMemory _invalidMemory = new CudaMemory(sizeof(int));
            private readonly TensorRtCudaDeviceBuffer _invalidFlag;
            private readonly CudaPinnedMemory _invalidPinned = new CudaPinnedMemory(sizeof(int));
            private readonly byte[] _invalidBytes = new byte[sizeof(int)];
            private readonly CudaMemory _selectedFlagMemory;
            private readonly CudaMemory _classIndexMemory;
            private readonly CudaMemory _scoreMemory;
            private readonly CudaMemory _candidateBoxMemory;
            private readonly CudaMemory _candidateCoefficientMemory;
            private readonly CudaPinnedMemory _selectedFlagPinned;
            private readonly CudaPinnedMemory _classIndexPinned;
            private readonly CudaPinnedMemory _scorePinned;
            private readonly CudaPinnedMemory _candidateBoxPinned;
            private readonly CudaPinnedMemory _candidateCoefficientPinned;
            private readonly byte[] _selectedFlags;
            private readonly byte[] _classIndexBytes;
            private readonly int[] _classIndices;
            private readonly float[] _scores;
            private readonly float[] _candidateBoxes;
            private readonly float[] _candidateCoefficients;
            private readonly TensorRtCudaYoloCandidatePlan _candidatePlan;
            private readonly CudaEvent _candidateStart = new CudaEvent();
            private readonly CudaEvent _candidateEnd = new CudaEvent();
            private CudaMemory? _coefficientMemory;
            private CudaMemory? _boxMemory;
            private CudaMemory? _activatedMemory;
            private CudaMemory? _maskMemory;
            private CudaMemory? _positiveCountMemory;
            private CudaPinnedMemory? _coefficientPinned;
            private CudaPinnedMemory? _boxPinned;
            private TensorRtCudaYoloMaskPlan? _cudaPlan;
            private float[]? _coefficientValues;
            private float[]? _boxValues;
            private byte[]? _positiveCountBytes;
            private int _instanceCount;
            private VisualSize _sourceSize;
            private float _scaleX;
            private float _scaleY;
            private float _offsetX;
            private float _offsetY;
            private bool _disposed;

            private YoloMaskPostprocessor(YoloInstanceSegmentationDecoder decoder, OutputSlot packedSlot, OutputSlot prototypeSlot, string architecture, int deviceOrdinal)
            {
                _decoder = decoder;
                int candidates = decoder.Contract.CandidateCount;
                int coefficients = decoder.Contract.MaskCoefficientCount;
                _selectedFlags = new byte[candidates];
                _classIndexBytes = new byte[checked(candidates * sizeof(int))];
                _classIndices = new int[candidates];
                _scores = new float[candidates];
                _candidateBoxes = new float[checked(candidates * 4)];
                _candidateCoefficients = new float[checked(candidates * coefficients)];
                _selectedFlagMemory = new CudaMemory(candidates);
                _classIndexMemory = new CudaMemory(_classIndexBytes.Length);
                _scoreMemory = new CudaMemory(checked(candidates * sizeof(float)));
                _candidateBoxMemory = new CudaMemory(checked(_candidateBoxes.Length * sizeof(float)));
                _candidateCoefficientMemory = new CudaMemory(checked(_candidateCoefficients.Length * sizeof(float)));
                _selectedFlagPinned = new CudaPinnedMemory(candidates);
                _classIndexPinned = new CudaPinnedMemory(_classIndexBytes.Length);
                _scorePinned = new CudaPinnedMemory(checked(candidates * sizeof(float)));
                _candidateBoxPinned = new CudaPinnedMemory(checked(_candidateBoxes.Length * sizeof(float)));
                _candidateCoefficientPinned = new CudaPinnedMemory(checked(_candidateCoefficients.Length * sizeof(float)));
                _prototypes = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor(prototypeSlot.Name, TensorElementType.Float32, prototypeSlot.DeviceTensor.Shape, TensorRtCudaBufferAccess.Read), prototypeSlot.Memory);
                _invalidFlag = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-invalid", TensorElementType.Int32, new TensorShape(1), TensorRtCudaBufferAccess.ReadWrite), _invalidMemory);
                var compileOptions = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: false);
                _filterKernel = TensorRtCudaCompiledKernel.Load(TensorRtCudaRtcCompiler.Compile(TensorRtCudaVisualKernels.FilterYoloCandidatesDefinition, compileOptions), deviceOrdinal);
                try
                {
                    _combineKernel = TensorRtCudaCompiledKernel.Load(TensorRtCudaRtcCompiler.Compile(TensorRtCudaVisualKernels.CombineYoloPrototypeMasksDefinition, compileOptions), deviceOrdinal);
                    try { _restoreKernel = TensorRtCudaCompiledKernel.Load(TensorRtCudaRtcCompiler.Compile(TensorRtCudaVisualKernels.RestoreYoloPrototypeMasksDefinition, compileOptions), deviceOrdinal); }
                    catch { _combineKernel.Dispose(); throw; }
                }
                catch { _filterKernel.Dispose(); throw; }
                var packed = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor(packedSlot.Name, TensorElementType.Float32, packedSlot.DeviceTensor.Shape, TensorRtCudaBufferAccess.Read), packedSlot.Memory);
                var flags = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-selected", TensorElementType.UInt8, new TensorShape(candidates), TensorRtCudaBufferAccess.Write), _selectedFlagMemory);
                var classes = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-classes", TensorElementType.Int32, new TensorShape(candidates), TensorRtCudaBufferAccess.Write), _classIndexMemory);
                var scores = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-scores", TensorElementType.Float32, new TensorShape(candidates), TensorRtCudaBufferAccess.Write), _scoreMemory);
                var boxes = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-boxes", TensorElementType.Float32, new TensorShape(candidates, 4), TensorRtCudaBufferAccess.Write), _candidateBoxMemory);
                var coefficientValues = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-coefficients", TensorElementType.Float32, new TensorShape(candidates, coefficients), TensorRtCudaBufferAccess.Write), _candidateCoefficientMemory);
                _candidatePlan = TensorRtCudaVisualKernels.CreateYoloCandidateFilterPlan(
                    _filterKernel, packed, flags, classes, scores, boxes, coefficientValues, _invalidFlag,
                    candidates, decoder.Contract.FieldCount, decoder.Contract.ClassCount, coefficients,
                    decoder.Contract.Layout == YoloPackedTensorLayout.AttributeMajor, decoder.Contract.HasObjectness, decoder.Contract.IsEndToEnd, decoder.Options.ScoreThreshold);
            }

            public TimeSpan LastCandidateFilterDuration => TimeSpan.FromMilliseconds(_candidateEnd.ElapsedTimeSince(_candidateStart));

            public static YoloMaskPostprocessor? TryCreate(VisualModelProfile profile, IReadOnlyList<OutputSlot> slots, string architecture, int deviceOrdinal)
            {
                if (!(profile.Decoder is YoloInstanceSegmentationDecoder decoder)) return null;
                OutputSlot? packed = slots.FirstOrDefault(slot => string.Equals(slot.Name, decoder.Contract.OutputName, StringComparison.Ordinal));
                OutputSlot? prototypes = slots.FirstOrDefault(slot => string.Equals(slot.Name, decoder.Contract.PrototypeOutputName, StringComparison.Ordinal));
                if (packed == null || prototypes == null || packed.ElementType != TensorElementType.Float32 || prototypes.ElementType != TensorElementType.Float32) return null;
                TensorShape prototypeShape = prototypes.DeviceTensor.Shape;
                TensorShape inputShape = profile.Input.ShapePattern;
                if (inputShape.IsDynamic || inputShape.Rank != 4 || prototypeShape.Rank != 4 || prototypeShape[0] != 1 || prototypeShape[1] != decoder.Contract.MaskCoefficientCount
                    || prototypeShape[2] != inputShape[2] / 4 || prototypeShape[3] != inputShape[3] / 4) return null;
                return new YoloMaskPostprocessor(decoder, packed, prototypes, architecture, deviceOrdinal);
            }

            public TensorRtCudaKernelLaunch EnqueueCandidateFilter(CudaStream stream)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(YoloMaskPostprocessor));
                _candidateStart.Record(stream);
                _invalidMemory.FillAsync(0, stream);
                TensorRtCudaKernelLaunch launch = _candidatePlan.Launch(stream);
                _selectedFlagMemory.CopyToAsync(_selectedFlagPinned, stream);
                _classIndexMemory.CopyToAsync(_classIndexPinned, stream);
                _scoreMemory.CopyToAsync(_scorePinned, stream);
                _candidateBoxMemory.CopyToAsync(_candidateBoxPinned, stream);
                _candidateCoefficientMemory.CopyToAsync(_candidateCoefficientPinned, stream);
                _invalidMemory.CopyToAsync(_invalidPinned, stream);
                _candidateEnd.Record(stream);
                return launch;
            }

            public object? TryRun(VisualDecodeContext context, CudaStream stream)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(YoloMaskPostprocessor));
                _invalidPinned.CopyTo(_invalidBytes);
                if (BitConverter.ToInt32(_invalidBytes, 0) != 0) return null;
                _selectedFlagPinned.CopyTo(_selectedFlags);
                _classIndexPinned.CopyTo(_classIndexBytes);
                Buffer.BlockCopy(_classIndexBytes, 0, _classIndices, 0, _classIndexBytes.Length);
                _scorePinned.CopyTo(_scores);
                _candidateBoxPinned.CopyTo(_candidateBoxes);
                _candidateCoefficientPinned.CopyTo(_candidateCoefficients);
                YoloCudaInstanceSegmentationPlan plan = _decoder.PrepareCudaPlan(context, _selectedFlags, _classIndices, _scores, _candidateBoxes, _candidateCoefficients);
                int count = plan.Candidates.Count;
                if (count == 0) return null;
                EnsurePlan(context.Input, plan);
                plan.FillCoefficientBuffer(_coefficientValues!);
                plan.FillModelBoxBuffer(_boxValues!);
                _coefficientPinned!.CopyFrom(_coefficientValues!);
                _boxPinned!.CopyFrom(_boxValues!);
                _positiveCountMemory!.FillAsync(0, stream);
                _coefficientMemory!.CopyFromAsync(_coefficientPinned, checked(_coefficientValues!.Length * sizeof(float)), stream);
                _boxMemory!.CopyFromAsync(_boxPinned, checked(_boxValues!.Length * sizeof(float)), stream);
                TensorRtCudaYoloMaskLaunch? launch = null;
                try { launch = _cudaPlan!.Launch(stream); stream.Synchronize(); }
                finally { launch?.Dispose(); }
                _invalidMemory.CopyTo(_invalidBytes);
                if (BitConverter.ToInt32(_invalidBytes, 0) != 0) return null;
                int pixels = checked(context.Input.SourceSize.Width * context.Input.SourceSize.Height);
                var masks = new byte[checked(count * pixels)];
                _maskMemory!.CopyTo(masks);
                _positiveCountMemory.CopyTo(_positiveCountBytes!);
                var positiveCounts = new int[count];
                Buffer.BlockCopy(_positiveCountBytes!, 0, positiveCounts, 0, checked(count * sizeof(int)));
                return _decoder.CreateCudaDecodedResult(context, plan, masks, positiveCounts);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _cudaPlan = null;
                _boxPinned?.Dispose(); _coefficientPinned?.Dispose();
                _positiveCountMemory?.Dispose(); _maskMemory?.Dispose(); _activatedMemory?.Dispose(); _boxMemory?.Dispose(); _coefficientMemory?.Dispose();
                _candidateEnd.Dispose(); _candidateStart.Dispose();
                _candidateCoefficientPinned.Dispose(); _candidateBoxPinned.Dispose(); _scorePinned.Dispose(); _classIndexPinned.Dispose(); _selectedFlagPinned.Dispose();
                _candidateCoefficientMemory.Dispose(); _candidateBoxMemory.Dispose(); _scoreMemory.Dispose(); _classIndexMemory.Dispose(); _selectedFlagMemory.Dispose();
                _invalidPinned.Dispose(); _invalidMemory.Dispose();
                _restoreKernel.Dispose(); _combineKernel.Dispose(); _filterKernel.Dispose();
            }

            private void EnsurePlan(PreparedVisualInput input, YoloCudaInstanceSegmentationPlan plan)
            {
                ImageTransform transform = input.Transform;
                int count = plan.Candidates.Count;
                if (_cudaPlan != null && _instanceCount == count && _sourceSize == input.SourceSize && _scaleX == transform.ScaleX && _scaleY == transform.ScaleY && _offsetX == transform.OffsetX && _offsetY == transform.OffsetY) return;
                _cudaPlan = null;
                _boxPinned?.Dispose(); _coefficientPinned?.Dispose();
                _positiveCountMemory?.Dispose(); _maskMemory?.Dispose(); _activatedMemory?.Dispose(); _boxMemory?.Dispose(); _coefficientMemory?.Dispose();
                int coefficientElements = checked(count * plan.MaskCoefficientCount);
                int boxElements = checked(count * 4);
                int prototypePixels = checked(plan.PrototypeWidth * plan.PrototypeHeight);
                int sourcePixels = checked(input.SourceSize.Width * input.SourceSize.Height);
                _coefficientValues = new float[coefficientElements]; _boxValues = new float[boxElements]; _positiveCountBytes = new byte[checked(count * sizeof(int))];
                _coefficientPinned = new CudaPinnedMemory(checked(coefficientElements * sizeof(float))); _boxPinned = new CudaPinnedMemory(checked(boxElements * sizeof(float)));
                _coefficientMemory = new CudaMemory(checked(coefficientElements * sizeof(float))); _boxMemory = new CudaMemory(checked(boxElements * sizeof(float)));
                _activatedMemory = new CudaMemory(checked(count * prototypePixels * sizeof(float))); _maskMemory = new CudaMemory(checked(count * sourcePixels)); _positiveCountMemory = new CudaMemory(checked(count * sizeof(int)));
                var coefficients = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-mask-coefficients", TensorElementType.Float32, new TensorShape(count, plan.MaskCoefficientCount), TensorRtCudaBufferAccess.Read), _coefficientMemory);
                var boxes = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-mask-boxes", TensorElementType.Float32, new TensorShape(count, 4), TensorRtCudaBufferAccess.Read), _boxMemory);
                var activated = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-mask-activated", TensorElementType.Float32, new TensorShape(count, plan.PrototypeHeight, plan.PrototypeWidth), TensorRtCudaBufferAccess.Write), _activatedMemory);
                var masks = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-mask-source", TensorElementType.UInt8, new TensorShape(count, input.SourceSize.Height, input.SourceSize.Width), TensorRtCudaBufferAccess.Write), _maskMemory);
                var positiveCounts = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-yolo-mask-positive-counts", TensorElementType.Int32, new TensorShape(count), TensorRtCudaBufferAccess.ReadWrite), _positiveCountMemory);
                _cudaPlan = TensorRtCudaVisualKernels.CreateYoloPrototypeMaskPlan(
                    _combineKernel, _restoreKernel, _prototypes, coefficients, boxes, activated, masks, _invalidFlag, positiveCounts,
                    count, plan.MaskCoefficientCount, plan.PrototypeWidth, plan.PrototypeHeight, input.ModelSize.Width, input.ModelSize.Height,
                    input.SourceSize.Width, input.SourceSize.Height, transform.ScaleX, transform.ScaleY, transform.OffsetX, transform.OffsetY, _decoder.Options.MaskThreshold);
                _instanceCount = count; _sourceSize = input.SourceSize; _scaleX = transform.ScaleX; _scaleY = transform.ScaleY; _offsetX = transform.OffsetX; _offsetY = transform.OffsetY;
            }
        }

        private sealed class MapPostprocessor : IDisposable
        {
            private readonly OutputSlot _mapSlot;
            private readonly OutputSlot? _scoreSlot;
            private readonly AlphaMattingDecoder? _alphaDecoder;
            private readonly AnomalibExportDecoder? _anomalibDecoder;
            private readonly TensorRtCudaCompiledKernel _kernel;
            private readonly TensorRtCudaDeviceBuffer _sourceMap;
            private readonly CudaMemory _invalidMemory = new CudaMemory(sizeof(int));
            private readonly CudaMemory _positiveCountMemory = new CudaMemory(sizeof(int));
            private readonly TensorRtCudaDeviceBuffer _invalidFlag;
            private readonly TensorRtCudaDeviceBuffer _positiveCount;
            private readonly CudaEvent _startEvent = new CudaEvent();
            private readonly CudaEvent _endEvent = new CudaEvent();
            private readonly byte[] _invalidBytes = new byte[sizeof(int)];
            private readonly byte[] _positiveCountBytes = new byte[sizeof(int)];
            private readonly CudaPinnedMemory _invalidPinned = new CudaPinnedMemory(sizeof(int));
            private readonly CudaPinnedMemory _positiveCountPinned = new CudaPinnedMemory(sizeof(int));
            private readonly int _tensorWidth;
            private readonly int _tensorHeight;
            private CudaMemory? _restoredMemory;
            private CudaMemory? _maskMemory;
            private CudaPinnedMemory? _restoredPinned;
            private CudaPinnedMemory? _maskPinned;
            private CudaPinnedMemory? _rawPinned;
            private TensorRtCudaVisualMapRestorePlan? _plan;
            private VisualSize _sourceSize;
            private float _scaleX;
            private float _scaleY;
            private float _offsetX;
            private float _offsetY;
            private bool _disposed;

            private MapPostprocessor(
                OutputSlot mapSlot,
                OutputSlot? scoreSlot,
                AlphaMattingDecoder? alphaDecoder,
                AnomalibExportDecoder? anomalibDecoder,
                string architecture,
                int deviceOrdinal)
            {
                _mapSlot = mapSlot;
                _scoreSlot = scoreSlot;
                _alphaDecoder = alphaDecoder;
                _anomalibDecoder = anomalibDecoder;
                TensorShape shape = mapSlot.DeviceTensor.Shape;
                _tensorHeight = checked((int)shape[2]);
                _tensorWidth = checked((int)shape[3]);
                _sourceMap = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor(mapSlot.Name, TensorElementType.Float32, shape, TensorRtCudaBufferAccess.Read), mapSlot.Memory);
                _invalidFlag = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-map-invalid", TensorElementType.Int32, new TensorShape(1), TensorRtCudaBufferAccess.ReadWrite), _invalidMemory);
                _positiveCount = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-map-positive-count", TensorElementType.Int32, new TensorShape(1), TensorRtCudaBufferAccess.ReadWrite), _positiveCountMemory);
                var compileOptions = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: false);
                _kernel = TensorRtCudaCompiledKernel.Load(TensorRtCudaRtcCompiler.Compile(TensorRtCudaVisualKernels.RestoreSingleChannelMapDefinition, compileOptions), deviceOrdinal);
            }

            public TimeSpan LastCudaDuration => TimeSpan.FromMilliseconds(_endEvent.ElapsedTimeSince(_startEvent));

            public static MapPostprocessor? TryCreate(VisualModelProfile profile, IReadOnlyList<OutputSlot> slots, string architecture, int deviceOrdinal)
            {
                if (profile.Decoder is AlphaMattingDecoder alphaDecoder && alphaDecoder.Schema.Layout == AlphaTensorLayout.Nchw)
                {
                    OutputSlot? map = FindFloatMap(slots, alphaDecoder.Schema.OutputName, profile.Input.ShapePattern);
                    return map == null ? null : new MapPostprocessor(map, null, alphaDecoder, null, architecture, deviceOrdinal);
                }

                if (profile.Decoder is AnomalibExportDecoder anomalibDecoder && SupportsCuda(anomalibDecoder))
                {
                    OutputSlot? map = FindFloatMap(slots, anomalibDecoder.MapOutputName, profile.Input.ShapePattern);
                    OutputSlot? score = slots.FirstOrDefault(slot => string.Equals(slot.Name, anomalibDecoder.ScoreOutputName, StringComparison.Ordinal) && slot.ElementType == TensorElementType.Float32 && slot.DeviceTensor.Shape.GetElementCount() == 1);
                    return map == null || score == null ? null : new MapPostprocessor(map, score, null, anomalibDecoder, architecture, deviceOrdinal);
                }
                return null;
            }

            public TensorRtCudaKernelLaunch Enqueue(PreparedVisualInput input, CudaStream stream)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MapPostprocessor));
                EnsurePlan(input);
                _startEvent.Record(stream);
                _invalidMemory.FillAsync(0, stream);
                _positiveCountMemory.FillAsync(0, stream);
                TensorRtCudaKernelLaunch launch = _plan!.Launch(stream);
                int restoredBytes = checked(input.SourceSize.Width * input.SourceSize.Height * sizeof(float));
                _restoredMemory!.CopyToAsync(_restoredPinned!, restoredBytes, stream);
                _invalidMemory.CopyToAsync(_invalidPinned, sizeof(int), stream);
                if (PreservesRawAnomalyMap)
                {
                    int rawBytes = checked(_tensorWidth * _tensorHeight * sizeof(float));
                    _mapSlot.Memory.CopyToAsync(_rawPinned!, rawBytes, stream);
                }
                if (_anomalibDecoder != null)
                {
                    _maskMemory!.CopyToAsync(_maskPinned!, checked(input.SourceSize.Width * input.SourceSize.Height), stream);
                    _positiveCountMemory.CopyToAsync(_positiveCountPinned, sizeof(int), stream);
                }
                _endEvent.Record(stream);
                return launch;
            }

            public object? TryDecode(VisualDecodeContext context)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MapPostprocessor));
                _invalidPinned.CopyTo(_invalidBytes);
                if (BitConverter.ToInt32(_invalidBytes, 0) != 0) return null;
                int pixels = checked(context.Input.SourceSize.Width * context.Input.SourceSize.Height);
                var restored = new float[pixels];
                _restoredPinned!.CopyTo(restored);
                if (_alphaDecoder != null) return _alphaDecoder.CreateCudaDecodedResult(context, restored);

                _scoreSlot!.CopyToManaged();
                float imageScore = ((float[])_scoreSlot.Tensor.Buffer)[0];
                float[] raw = PreservesRawAnomalyMap ? new float[checked(_tensorWidth * _tensorHeight)] : Array.Empty<float>();
                if (PreservesRawAnomalyMap) _rawPinned!.CopyTo(raw);
                var mask = new byte[pixels];
                _maskPinned!.CopyTo(mask);
                _positiveCountPinned.CopyTo(_positiveCountBytes);
                int positiveCount = BitConverter.ToInt32(_positiveCountBytes, 0);
                return _anomalibDecoder!.CreateCudaDecodedResult(context, imageScore, _tensorWidth, _tensorHeight, raw, restored, mask, positiveCount);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _plan = null;
                _rawPinned?.Dispose();
                _maskPinned?.Dispose();
                _restoredPinned?.Dispose();
                _maskMemory?.Dispose();
                _restoredMemory?.Dispose();
                _endEvent.Dispose();
                _startEvent.Dispose();
                _kernel.Dispose();
                _positiveCountPinned.Dispose();
                _invalidPinned.Dispose();
                _positiveCountMemory.Dispose();
                _invalidMemory.Dispose();
            }

            private void EnsurePlan(PreparedVisualInput input)
            {
                VisualSize sourceSize = input.SourceSize;
                ImageTransform transform = input.Transform;
                if (_plan != null && _sourceSize == sourceSize && _scaleX == transform.ScaleX && _scaleY == transform.ScaleY && _offsetX == transform.OffsetX && _offsetY == transform.OffsetY) return;
                int pixels = checked(sourceSize.Width * sourceSize.Height);
                int restoredBytes = checked(pixels * sizeof(float));
                if (_restoredMemory == null || _restoredMemory.SizeInBytes < restoredBytes)
                {
                    _restoredMemory?.Dispose();
                    _restoredMemory = new CudaMemory(restoredBytes);
                }
                bool anomaly = _anomalibDecoder != null;
                int maskPixels = anomaly ? pixels : 1;
                if (_maskMemory == null || _maskMemory.SizeInBytes < maskPixels)
                {
                    _maskMemory?.Dispose();
                    _maskMemory = new CudaMemory(maskPixels);
                }
                _restoredPinned?.Dispose();
                _restoredPinned = new CudaPinnedMemory(restoredBytes);
                if (anomaly)
                {
                    _maskPinned?.Dispose();
                    _maskPinned = new CudaPinnedMemory(pixels);
                }
                if (PreservesRawAnomalyMap && _rawPinned == null) _rawPinned = new CudaPinnedMemory(checked(_tensorWidth * _tensorHeight * sizeof(float)));
                var restoredBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-map-restored", TensorElementType.Float32, new TensorShape(1, 1, sourceSize.Height, sourceSize.Width), TensorRtCudaBufferAccess.Write), _restoredMemory);
                TensorShape maskShape = anomaly ? new TensorShape(1, 1, sourceSize.Height, sourceSize.Width) : new TensorShape(1);
                var maskBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("visual-map-mask", TensorElementType.UInt8, maskShape, TensorRtCudaBufferAccess.Write), _maskMemory);
                bool applySigmoid = _alphaDecoder != null && !_alphaDecoder.Schema.OutputIsProbability;
                bool validateProbability = anomaly || (_alphaDecoder != null && _alphaDecoder.Schema.OutputIsProbability);
                float threshold = anomaly ? _anomalibDecoder!.CoreDecoder.Options.Threshold : 0f;
                _plan = TensorRtCudaVisualKernels.CreateRestoreSingleChannelMapPlan(
                    _kernel, _sourceMap, restoredBuffer, maskBuffer, _invalidFlag, _positiveCount,
                    _tensorWidth, _tensorHeight, input.ModelSize.Width, input.ModelSize.Height,
                    sourceSize.Width, sourceSize.Height, transform.ScaleX, transform.ScaleY, transform.OffsetX, transform.OffsetY,
                    threshold, applySigmoid, validateProbability, anomaly);
                _sourceSize = sourceSize;
                _scaleX = transform.ScaleX;
                _scaleY = transform.ScaleY;
                _offsetX = transform.OffsetX;
                _offsetY = transform.OffsetY;
            }

            private static OutputSlot? FindFloatMap(IReadOnlyList<OutputSlot> slots, string name, TensorShape inputShape)
            {
                if (inputShape.IsDynamic || inputShape.Rank != 4) return null;
                OutputSlot? slot = slots.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));
                if (slot == null || slot.ElementType != TensorElementType.Float32) return null;
                TensorShape shape = slot.DeviceTensor.Shape;
                if (shape.Rank != 4 || shape[0] != 1 || shape[1] != 1 || shape[2] != inputShape[2] || shape[3] != inputShape[3]) return null;
                return slot;
            }

            private static bool SupportsCuda(AnomalibExportDecoder decoder)
            {
                AnomalyDecoder core = decoder.CoreDecoder;
                AnomalyDecoderOptions options = core.Options;
                AnomalyMapSchema schema = core.Schema;
                return schema.Layout == AnomalyTensorLayout.Nchw
                    && schema.ChannelCount == 1
                    && schema.CoordinateSpace == AnomalyMapCoordinateSpace.ModelInput
                    && schema.ValueMode == AnomalyMapValueMode.Probabilities
                    && options.Normalization == AnomalyNormalizationMode.None
                    && options.ThresholdPolicy == AnomalyThresholdPolicy.Fixed
                    && options.ChannelAggregation == AnomalyChannelAggregation.SingleChannel
                    && options.ChannelIndex == 0
                    && options.OutputSizeMode == AnomalyOutputSizeMode.Source
                    && options.Interpolation == AnomalyMapInterpolation.BilinearHalfPixel
                    && !options.PreserveRawMap;
            }

            private bool PreservesRawAnomalyMap => _anomalibDecoder != null && _anomalibDecoder.CoreDecoder.Options.PreserveRawMap;
        }

        private sealed class OutputSlot : IDisposable
        {
            private readonly byte[]? _bytes;
            private readonly Array _values;

            private OutputSlot(TensorDescriptor descriptor, CudaMemory memory, Array values, byte[]? bytes, ITensor tensor)
            {
                Name = descriptor.Name;
                ElementType = descriptor.ElementType;
                Memory = memory;
                _values = values;
                _bytes = bytes;
                Tensor = tensor;
                DeviceTensor = new TensorRtDeviceTensor(descriptor.Name, descriptor.ElementType, descriptor.Shape, memory);
            }

            public string Name { get; }
            public TensorElementType ElementType { get; }
            public CudaMemory Memory { get; }
            public ITensor Tensor { get; }
            public TensorRtDeviceTensor DeviceTensor { get; }
            public long Length => DeviceTensor.Shape.GetElementCount();

            public static OutputSlot Create(TensorDescriptor descriptor)
            {
                if (descriptor.Shape.IsDynamic) throw new NotSupportedException("GPU visual output shapes must be static.");
                int count = checked((int)descriptor.Shape.GetElementCount());
                var memory = new CudaMemory(ByteLength(descriptor));
                if (descriptor.ElementType == TensorElementType.Float32)
                {
                    var values = new float[count];
                    return new OutputSlot(descriptor, memory, values, null, new Tensor<float>(descriptor.Shape, values, TensorBufferOwnership.Transfer));
                }
                if (descriptor.ElementType == TensorElementType.Boolean)
                {
                    var values = new bool[count];
                    return new OutputSlot(descriptor, memory, values, new byte[count], new Tensor<bool>(descriptor.Shape, values, TensorBufferOwnership.Transfer));
                }
                if (descriptor.ElementType == TensorElementType.UInt8)
                {
                    var values = new byte[count];
                    return new OutputSlot(descriptor, memory, values, values, new Tensor<byte>(descriptor.Shape, values, TensorBufferOwnership.Transfer));
                }
                if (descriptor.ElementType == TensorElementType.Int8)
                {
                    var values = new sbyte[count];
                    return new OutputSlot(descriptor, memory, values, new byte[count], new Tensor<sbyte>(descriptor.Shape, values, TensorBufferOwnership.Transfer));
                }
                if (descriptor.ElementType == TensorElementType.Int32)
                {
                    var values = new int[count];
                    return new OutputSlot(descriptor, memory, values, new byte[checked(count * sizeof(int))], new Tensor<int>(descriptor.Shape, values, TensorBufferOwnership.Transfer));
                }
                if (descriptor.ElementType == TensorElementType.Int64)
                {
                    var values = new long[count];
                    return new OutputSlot(descriptor, memory, values, new byte[checked(count * sizeof(long))], new Tensor<long>(descriptor.Shape, values, TensorBufferOwnership.Transfer));
                }
                memory.Dispose();
                throw new NotSupportedException("GPU visual output type is unsupported: " + descriptor.ElementType);
            }

            public void CopyToManaged()
            {
                if (ElementType == TensorElementType.Float32) { Memory.CopyTo((float[])_values); return; }
                Memory.CopyTo(_bytes!);
                if (ElementType == TensorElementType.Boolean)
                {
                    bool[] values = (bool[])_values;
                    for (int index = 0; index < values.Length; index++) values[index] = _bytes![index] != 0;
                }
                else if (!ReferenceEquals(_bytes, _values)) Buffer.BlockCopy(_bytes!, 0, _values, 0, _bytes!.Length);
            }

            public void Dispose() => Memory.Dispose();
        }
    }
}
