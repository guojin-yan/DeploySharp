using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the explicit orientation stage used by an OCR workflow. / 标识 OCR 工作流使用的显式方向阶段。</summary>
    public enum OcrOrientationStrategy
    {
        /// <summary>No orientation model is used. / 不使用方向模型。</summary>
        None = 0,
        /// <summary>One orientation result corrects the whole image before detection. / 检测前使用一个方向结果纠正整图。</summary>
        WholeImage = 1,
        /// <summary>Each detected text region is classified before recognition. / 每个检测文本区域在识别前分别分类。</summary>
        PerTextRegion = 2
    }

    /// <summary>Controls how a per-region orientation rejection affects OCR. / 控制逐区域方向拒绝如何影响 OCR。</summary>
    public enum OcrOrientationRejectionPolicy
    {
        /// <summary>Fail the OCR call instead of silently selecting an angle. / 使 OCR 调用失败，不静默选择角度。</summary>
        Fail = 0,
        /// <summary>Use the explicit zero-degree fallback recorded by the rejected result. / 使用拒绝结果记录的显式 0 度回退。</summary>
        UseZeroDegrees = 1
    }

    /// <summary>Defines the numerical meaning of orientation output values. / 定义方向输出值的数值语义。</summary>
    public enum OcrOrientationValueSemantics
    {
        /// <summary>Values are probabilities and must be in [0,1]. / 值是必须位于 [0,1] 的概率。</summary>
        Probability = 0,
        /// <summary>Values are logits and are normalized with softmax when requested. / 值是 logits，并在请求时通过 softmax 归一化。</summary>
        Logits = 1
    }

    /// <summary>Stores a strict two- or four-class OCR orientation model contract. / 存储严格的二分类或四分类 OCR 方向模型契约。</summary>
    public sealed class OcrOrientationSchema
    {
        private readonly IReadOnlyList<TextOrientation> _classToOrientation;

        /// <summary>Initializes an orientation schema with an explicit two- or four-class mapping. / 使用显式二分类或四分类映射初始化方向 Schema。</summary>
        public OcrOrientationSchema(string outputName, TensorShape outputShape, TensorElementType elementType, IEnumerable<TextOrientation> classToOrientation, OcrOrientationValueSemantics semantics = OcrOrientationValueSemantics.Logits, bool applySoftmax = true, bool allowDynamicBatch = false)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An orientation output name is required.", nameof(outputName));
            if (outputShape == null) throw new ArgumentNullException(nameof(outputShape));
            if (outputShape.Rank != 1 && outputShape.Rank != 2) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "Orientation output rank must be one or two.", tensorName: outputName);
            if (outputShape.Rank == 2 && outputShape[0] != 1 && !(allowDynamicBatch && outputShape[0] == -1)) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "Orientation output batch must be one or an explicitly allowed dynamic dimension.", tensorName: outputName);
            if (elementType != TensorElementType.Float32 && elementType != TensorElementType.Float64) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "Orientation output must use Float32 or Float64.", tensorName: outputName);
            if (!Enum.IsDefined(typeof(OcrOrientationValueSemantics), semantics)) throw new ArgumentOutOfRangeException(nameof(semantics));
            if (semantics == OcrOrientationValueSemantics.Logits && !applySoftmax) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "Logit outputs require explicit softmax normalization.", tensorName: outputName);
            if (semantics == OcrOrientationValueSemantics.Probability && applySoftmax) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "Probability outputs must not request a second softmax.", tensorName: outputName);
            var mapping = new List<TextOrientation>();
            if (classToOrientation == null) throw new ArgumentNullException(nameof(classToOrientation));
            foreach (TextOrientation orientation in classToOrientation)
            {
                if (!Enum.IsDefined(typeof(TextOrientation), orientation)) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "The orientation mapping contains an invalid angle.", tensorName: outputName);
                if (mapping.Contains(orientation)) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "Orientation angles must be unique.", tensorName: outputName);
                mapping.Add(orientation);
            }
            if (mapping.Count != 2 && mapping.Count != 4) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "The orientation mapping must contain two or four classes.", tensorName: outputName);
            if (mapping.Count == 2 && (!mapping.Contains(TextOrientation.Degrees0) || !mapping.Contains(TextOrientation.Degrees180))) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "A two-class orientation mapping must explicitly contain 0 and 180 degrees.", tensorName: outputName);
            long declaredClasses = outputShape[outputShape.Rank - 1];
            if (declaredClasses != mapping.Count) throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "The output class dimension must match the explicit orientation mapping.", tensorName: outputName);
            OutputName = outputName;
            OutputShape = new TensorShape(outputShape.ToArray());
            ElementType = elementType;
            _classToOrientation = new ReadOnlyCollection<TextOrientation>(mapping);
            Semantics = semantics;
            ApplySoftmax = applySoftmax;
            AllowDynamicBatch = allowDynamicBatch;
        }

        /// <summary>Gets the required output tensor name. / 获取所需输出张量名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets the explicit output shape. / 获取显式输出形状。</summary>
        public TensorShape OutputShape { get; }
        /// <summary>Gets the required element type. / 获取所需元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the explicit class-to-angle mapping. / 获取显式类别到角度映射。</summary>
        public IReadOnlyList<TextOrientation> ClassToOrientation => _classToOrientation;
        /// <summary>Gets the declared orientation class count. / 获取声明的方向类别数。</summary>
        public int ClassCount => _classToOrientation.Count;
        /// <summary>Gets the output value semantics. / 获取输出值语义。</summary>
        public OcrOrientationValueSemantics Semantics { get; }
        /// <summary>Gets whether logits require softmax normalization. / 获取 logits 是否需要 softmax 归一化。</summary>
        public bool ApplySoftmax { get; }
        /// <summary>Gets whether a dynamic batch dimension is accepted. / 获取是否允许动态 batch 维。</summary>
        public bool AllowDynamicBatch { get; }
    }

    /// <summary>Controls deterministic OCR orientation decoding and bounded workspace. / 控制确定性的 OCR 方向解码和有界工作区。</summary>
    public sealed class OcrOrientationDecoderOptions
    {
        /// <summary>Initializes decoder options. / 初始化解码选项。</summary>
        public OcrOrientationDecoderOptions(float rejectionThreshold = 0.0f, float tieEpsilon = 0.000001f, bool validateProbabilities = true, int maximumResultBytes = 64 * 1024)
        {
            if (float.IsNaN(rejectionThreshold) || float.IsInfinity(rejectionThreshold) || rejectionThreshold < 0 || rejectionThreshold > 1) throw new ArgumentOutOfRangeException(nameof(rejectionThreshold));
            if (float.IsNaN(tieEpsilon) || float.IsInfinity(tieEpsilon) || tieEpsilon < 0) throw new ArgumentOutOfRangeException(nameof(tieEpsilon));
            if (maximumResultBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumResultBytes));
            RejectionThreshold = rejectionThreshold;
            TieEpsilon = tieEpsilon;
            ValidateProbabilities = validateProbabilities;
            MaximumResultBytes = maximumResultBytes;
        }
        /// <summary>Gets the minimum accepted confidence. / 获取最低可接受置信度。</summary>
        public float RejectionThreshold { get; }
        /// <summary>Gets the tie comparison epsilon. / 获取同分比较 epsilon。</summary>
        public float TieEpsilon { get; }
        /// <summary>Gets whether probability ranges are checked. / 获取是否校验概率范围。</summary>
        public bool ValidateProbabilities { get; }
        /// <summary>Gets the maximum result size. / 获取最大结果大小。</summary>
        public int MaximumResultBytes { get; }
    }

    /// <summary>Contains an owned OCR orientation classification result and provenance. / 包含自有 OCR 方向分类结果和来源。</summary>
    public sealed class OcrOrientationResult
    {
        private readonly IReadOnlyList<float> _scores;
        /// <summary>Initializes an orientation result. / 初始化方向结果。</summary>
        public OcrOrientationResult(TextOrientation orientation, int classIndex, float confidence, IEnumerable<float> scores, bool rejected, string profileId, ModelId modelId, BackendId backendId, VisualSize inputSize, VisualSize outputSize, TimeSpan timing, IEnumerable<string>? warnings = null)
        {
            if (!Enum.IsDefined(typeof(TextOrientation), orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
            if (float.IsNaN(confidence) || float.IsInfinity(confidence) || confidence < 0 || confidence > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
            var scoreCopy = new List<float>();
            if (scores == null) throw new ArgumentNullException(nameof(scores));
            foreach (float score in scores) { if (float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 1) throw new ArgumentOutOfRangeException(nameof(scores)); scoreCopy.Add(score); }
            if (scoreCopy.Count != 2 && scoreCopy.Count != 4) throw new ArgumentException("Exactly two or four scores are required.", nameof(scores));
            if (classIndex < 0 || classIndex >= scoreCopy.Count) throw new ArgumentOutOfRangeException(nameof(classIndex));
            if (rejected && orientation != TextOrientation.Degrees0) throw new ArgumentException("Rejected results use the explicit no-rotation fallback.", nameof(orientation));
            if (string.IsNullOrWhiteSpace(profileId) || modelId.IsEmpty || backendId.IsEmpty) throw new ArgumentException("Provenance is required.");
            Orientation = orientation; ClassIndex = classIndex; Confidence = confidence; Rejected = rejected;
            _scores = new ReadOnlyCollection<float>(scoreCopy); ProfileId = profileId; ModelId = modelId; BackendId = backendId; InputSize = inputSize; OutputSize = outputSize; Timing = timing;
            Warnings = new ReadOnlyCollection<string>((warnings ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToList());
            CanonicalSha256 = ComputeSha256();
        }
        /// <summary>Gets the selected clockwise orientation. / 获取选中的顺时针方向。</summary>
        public TextOrientation Orientation { get; }
        /// <summary>Gets the accepted correction angle, or null when confidence rejection occurred. / 获取已接受的纠正角度；置信度拒绝时为 null。</summary>
        public TextOrientation? AcceptedOrientation => Rejected ? (TextOrientation?)null : Orientation;
        /// <summary>Gets the selected class index. / 获取选中的类别索引。</summary>
        public int ClassIndex { get; }
        /// <summary>Gets selected confidence. / 获取选中置信度。</summary>
        public float Confidence { get; }
        /// <summary>Gets all normalized scores in declared class order. / 获取按声明类别顺序排列的全部归一化分数。</summary>
        public IReadOnlyList<float> Scores => _scores;
        /// <summary>Gets whether confidence rejection occurred. / 获取是否因置信度被拒绝。</summary>
        public bool Rejected { get; }
        /// <summary>Gets profile provenance. / 获取 Profile 来源。</summary>
        public string ProfileId { get; }
        /// <summary>Gets model provenance. / 获取模型来源。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets backend provenance. / 获取后端来源。</summary>
        public BackendId BackendId { get; }
        /// <summary>Gets prepared input size. / 获取准备输入尺寸。</summary>
        public VisualSize InputSize { get; }
        /// <summary>Gets the orientation model input size used for this result. / 获取本结果使用的方向模型输入尺寸。</summary>
        public VisualSize OutputSize { get; }
        /// <summary>Gets the corrected image size after applying the accepted right-angle rotation. / 获取应用已接受直角旋转后的纠正图尺寸。</summary>
        public VisualSize CorrectedImageSize => !Rejected && (Orientation == TextOrientation.Clockwise90 || Orientation == TextOrientation.CounterClockwise90)
            ? new VisualSize(InputSize.Height, InputSize.Width)
            : InputSize;
        /// <summary>Gets decoder timing. / 获取解码时长。</summary>
        public TimeSpan Timing { get; }
        /// <summary>Gets deterministic warnings. / 获取确定性警告。</summary>
        public IReadOnlyList<string> Warnings { get; }
        /// <summary>Gets canonical result SHA256. / 获取规范结果 SHA256。</summary>
        public string CanonicalSha256 { get; }

        /// <summary>Maps one corrected-image point back to the original image coordinate space. / 将一个纠正图坐标点映射回原图坐标空间。</summary>
        public PointF ToOriginalPoint(PointF correctedPoint)
        {
            if (float.IsNaN(correctedPoint.X) || float.IsInfinity(correctedPoint.X) || float.IsNaN(correctedPoint.Y) || float.IsInfinity(correctedPoint.Y)) throw new ArgumentOutOfRangeException(nameof(correctedPoint));
            if (Rejected) throw new VisualException(VisualErrorCodes.OcrOrientationCapabilityUnavailable, "A rejected orientation result cannot restore corrected coordinates.", profileId: ProfileId, backendId: BackendId, modelId: ModelId);
            switch (Orientation)
            {
                case TextOrientation.Degrees0: return correctedPoint;
                case TextOrientation.Clockwise90: return new PointF(correctedPoint.Y, InputSize.Height - correctedPoint.X);
                case TextOrientation.Degrees180: return new PointF(InputSize.Width - correctedPoint.X, InputSize.Height - correctedPoint.Y);
                case TextOrientation.CounterClockwise90: return new PointF(InputSize.Width - correctedPoint.Y, correctedPoint.X);
                default: throw new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, "The orientation angle cannot restore coordinates.", profileId: ProfileId, backendId: BackendId, modelId: ModelId);
            }
        }

        internal OcrOrientationResult WithExecution(BackendId backendId, TimeSpan timing)
        {
            return new OcrOrientationResult(Orientation, ClassIndex, Confidence, _scores, Rejected, ProfileId, ModelId, backendId, InputSize, OutputSize, timing, Warnings);
        }

        private string ComputeSha256()
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream, Encoding.UTF8, true))
            {
                // Backend and timing are provenance, not semantic output, so equivalent backends share one canonical digest. / 后端和时长属于来源而非语义输出，因此等价后端共享同一规范摘要。
                writer.Write((int)Orientation); writer.Write(ClassIndex); writer.Write(Confidence); writer.Write(Rejected); writer.Write(ProfileId); writer.Write(ModelId.Value); writer.Write(InputSize.Width); writer.Write(InputSize.Height); writer.Write(OutputSize.Width); writer.Write(OutputSize.Height); foreach (float score in _scores) writer.Write(score); writer.Flush();
                using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(stream.ToArray()));
            }
        }
    }

    /// <summary>Decodes a named two- or four-class orientation tensor without guessing class order. / 解码命名二分类或四分类方向张量，不猜测类别顺序。</summary>
    public sealed class OcrOrientationDecoder : IVisualDecoder
    {
        /// <summary>Initializes a reusable orientation decoder. / 初始化可复用方向解码器。</summary>
        public OcrOrientationDecoder(OcrOrientationSchema schema, OcrOrientationDecoderOptions? options = null) { Schema = schema ?? throw new ArgumentNullException(nameof(schema)); Options = options ?? new OcrOrientationDecoderOptions(); }
        /// <summary>Gets the decoder schema. / 获取解码 Schema。</summary>
        public OcrOrientationSchema Schema { get; }
        /// <summary>Gets decoder options. / 获取解码选项。</summary>
        public OcrOrientationDecoderOptions Options { get; }
        /// <summary>Gets the OCR text-orientation classification task identifier. / 获取 OCR 文本方向分类任务标识。</summary>
        public VisualTaskId Task => VisualTaskId.TextOrientationClassification;
        /// <summary>Decodes one strict named orientation tensor into an owned orientation result. / 将一个严格命名的方向张量解码为自有方向结果。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Profile.Task != Task) throw Failure(context, "The profile task does not match OCR orientation decoding.");
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(Schema.OutputName); }
            catch (Exception exception) { throw Failure(context, "The required orientation output is missing.", exception); }
            if (tensor.ElementType != Schema.ElementType || !ShapeMatches(tensor.Shape)) throw Failure(context, "The orientation output type or shape does not match its schema.", tensorName: Schema.OutputName);
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.OutputName);
            int classCount = Schema.ClassCount;
            if (values.Length < classCount) throw Failure(context, "The orientation output contains fewer values than its declared class count.", tensorName: Schema.OutputName);
            var scores = new float[classCount];
            double sum = 0;
            if (Schema.Semantics == OcrOrientationValueSemantics.Probability)
            {
                for (int index = 0; index < classCount; index++) { if (Options.ValidateProbabilities && (values[index] < 0 || values[index] > 1)) throw Failure(context, "Orientation probabilities must be in [0,1].", tensorName: Schema.OutputName, details: "index=" + index); scores[index] = values[index]; sum += scores[index]; }
                if (Options.ValidateProbabilities && Math.Abs(sum - 1.0) > 0.001) throw Failure(context, "Orientation probabilities must sum to one.", tensorName: Schema.OutputName);
            }
            else
            {
                if (Schema.ApplySoftmax) { double max = values.Take(classCount).Max(); double denominator = 0; for (int index = 0; index < classCount; index++) denominator += Math.Exp(values[index] - max); for (int index = 0; index < classCount; index++) scores[index] = (float)(Math.Exp(values[index] - max) / denominator); }
                else for (int index = 0; index < classCount; index++) scores[index] = values[index];
            }
            int selected = 0;
            for (int index = 1; index < classCount; index++) { context.CancellationToken.ThrowIfCancellationRequested(); if (scores[index] > scores[selected] + Options.TieEpsilon) selected = index; }
            if (Options.MaximumResultBytes < checked(scores.Length * sizeof(float))) throw new VisualException(VisualErrorCodes.OcrOrientationLimitExceeded, "The OCR orientation result exceeds its configured byte limit.", profileId: context.Profile.ProfileId, tensorName: Schema.OutputName, modelId: context.Profile.ModelId, technicalDetails: "requiredBytes=" + checked(scores.Length * sizeof(float)).ToString(CultureInfo.InvariantCulture));
            bool rejected = scores[selected] < Options.RejectionThreshold;
            return new OcrOrientationResult(rejected ? TextOrientation.Degrees0 : Schema.ClassToOrientation[selected], selected, scores[selected], scores, rejected, context.Profile.ProfileId, context.Profile.ModelId, context.Profile.ModelId.IsEmpty ? default(BackendId) : new BackendId("unknown"), context.Input.SourceSize, context.Input.ModelSize, TimeSpan.Zero, rejected ? new[] { "ocr.orientation.rejected" } : Array.Empty<string>());
        }
        private bool ShapeMatches(TensorShape shape) => shape.Rank == Schema.OutputShape.Rank && (shape.Rank == 1 ? shape[0] == Schema.ClassCount : shape[0] == 1 && shape[1] == Schema.ClassCount);
        private static VisualException Failure(VisualDecodeContext context, string message, Exception? inner = null, string? tensorName = null, string? details = null) => new VisualException(VisualErrorCodes.OcrOrientationContractInvalid, message, inner, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
    }

    /// <summary>Runs OCR orientation inference through the shared Visual/Core pipeline. / 通过共享 Visual/Core Pipeline 运行 OCR 方向推理。</summary>
    public sealed class OcrOrientationPipeline : IDisposable
    {
        private readonly VisualPipeline _pipeline;
        /// <summary>Initializes an orientation pipeline from a shared backend selection. / 从共享后端选择初始化方向 Pipeline。</summary>
        public OcrOrientationPipeline(BackendRegistry backendRegistry, VisualProfileSelection selection, BackendRequest request, SessionOptions? sessionOptions = null) { _pipeline = new VisualPipeline(backendRegistry ?? throw new ArgumentNullException(nameof(backendRegistry)), selection ?? throw new ArgumentNullException(nameof(selection)), request ?? throw new ArgumentNullException(nameof(request)), sessionOptions); Selection = selection; }
        /// <summary>Gets the shared visual selection. / 获取共享视觉选择。</summary>
        public VisualProfileSelection Selection { get; }
        /// <summary>Runs synchronous direction classification. / 同步运行方向分类。</summary>
        public OcrOrientationResult Run(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            VisualInferenceResult inference = _pipeline.Run(input, options, cancellationToken);
            return inference.GetValue<OcrOrientationResult>().WithExecution(inference.BackendId, inference.Timing.Total);
        }
        /// <summary>Runs asynchronous direction classification. / 异步运行方向分类。</summary>
        public Task<OcrOrientationResult> RunAsync(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => RunCoreAsync(input, options, cancellationToken);
        /// <summary>Idempotently releases the owned Visual inference pipeline. / 幂等释放所拥有的 Visual 推理 Pipeline。</summary>
        public void Dispose() => _pipeline.Dispose();
        private async Task<OcrOrientationResult> RunCoreAsync(PreparedVisualInput input, VisualExecutionOptions? options, CancellationToken token)
        {
            VisualInferenceResult inference = await _pipeline.RunAsync(input, options, token).ConfigureAwait(false);
            return inference.GetValue<OcrOrientationResult>().WithExecution(inference.BackendId, inference.Timing.Total);
        }
    }

    /// <summary>Provides an image-library-neutral orientation correction extension. / 提供与图像库无关的方向纠正扩展。</summary>
    public interface IOcrOrientationImageInput : IOcrImageInput
    {
        /// <summary>Creates a new owned OCR input after applying the selected right-angle rotation once. / 应用一次选定直角旋转后创建新的自有 OCR 输入。</summary>
        public IOcrImageInput CreateOriented(OcrOrientationResult orientation, CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>Composes orientation inference, one image correction, and the existing two-stage OCR pipeline. / 组合方向推理、一次图像纠正和现有双阶段 OCR Pipeline。</summary>
    public sealed class OcrOrientationWorkflow : IDisposable
    {
        private readonly OcrOrientationPipeline _orientation;
        private readonly OcrPipeline _ocr;
        private int _disposeState;

        /// <summary>Initializes a workflow that owns both supplied pipelines. / 初始化拥有两个给定 Pipeline 的工作流。</summary>
        public OcrOrientationWorkflow(OcrOrientationPipeline orientationPipeline, OcrPipeline ocrPipeline)
        {
            _orientation = orientationPipeline ?? throw new ArgumentNullException(nameof(orientationPipeline));
            _ocr = ocrPipeline ?? throw new ArgumentNullException(nameof(ocrPipeline));
        }

        /// <summary>Gets the explicit whole-image orientation strategy. / 获取显式整图方向策略。</summary>
        public OcrOrientationStrategy Strategy => OcrOrientationStrategy.WholeImage;

        /// <summary>Runs synchronous orientation, correction, detection, and recognition. / 同步运行方向分类、纠正、检测和识别。</summary>
        public OcrResult Run(IOcrOrientationImageInput input, VisualExecutionOptions? orientationOptions = null, OcrExecutionOptions? ocrOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunAsync(input, orientationOptions, ocrOptions, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>Runs orientation and OCR using each backend's true asynchronous path or documented fallback. / 使用各后端真实异步路径或已记录回退运行方向分类和 OCR。</summary>
        public async Task<OcrResult> RunAsync(IOcrOrientationImageInput input, VisualExecutionOptions? orientationOptions = null, OcrExecutionOptions? ocrOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (Volatile.Read(ref _disposeState) != 0) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The OCR orientation workflow has been disposed.");
            OcrOrientationResult orientation = await _orientation.RunAsync(input.DetectionInput, orientationOptions, cancellationToken).ConfigureAwait(false);
            if (orientation.Rejected) throw new VisualException(VisualErrorCodes.OcrOrientationCapabilityUnavailable, "OCR orientation confidence is below the configured rejection threshold.", profileId: orientation.ProfileId, backendId: orientation.BackendId, modelId: orientation.ModelId, technicalDetails: "confidence=" + orientation.Confidence.ToString("R", CultureInfo.InvariantCulture));
            using (IOcrImageInput corrected = input.CreateOriented(orientation, cancellationToken))
            {
                OcrResult correctedResult = await _ocr.RunWithOrientationAsync(corrected, orientation, ocrOptions, cancellationToken).ConfigureAwait(false);
                return RestoreOriginalCoordinates(correctedResult, orientation);
            }
        }

        /// <summary>Idempotently releases the owned OCR and orientation pipelines. / 幂等释放所拥有的 OCR 与方向 Pipeline。</summary>
        /// <remarks>Idempotently releases OCR before orientation so no child session remains active. / 幂等地先释放 OCR 再释放方向 Pipeline，确保没有子会话保持活动。</remarks>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;
            _ocr.Dispose();
            _orientation.Dispose();
        }

        private static OcrResult RestoreOriginalCoordinates(OcrResult corrected, OcrOrientationResult orientation)
        {
            if (orientation.Orientation == TextOrientation.Degrees0) return corrected;
            var restored = new List<OcrRegionResult>(corrected.Regions.Count);
            foreach (OcrRegionResult item in corrected.Regions)
            {
                var vertices = new PointF[item.Region.Polygon.Vertices.Count];
                for (int index = 0; index < vertices.Length; index++) vertices[index] = orientation.ToOriginalPoint(item.Region.Polygon.Vertices[index]);
                TextPolygon polygon = TextPolygon.Canonicalize(vertices, OrientedVertexOrder.CounterClockwise);
                // Crop-corner roles describe the corrected upright image. The final result keeps the authoritative polygon in original coordinates instead of relabeling rotated corners. / 裁剪角点角色描述纠正后的正向图像；最终结果保留原图坐标中的权威 polygon，不错误重标旋转后的角点角色。
                var region = new TextRegion(item.Region.SourceIndex, item.Region.Score, polygon, orientation: item.Region.Orientation, angleRadians: item.Region.AngleRadians, language: item.Region.Language, script: item.Region.Script, externalId: item.Region.ExternalId, metadata: item.Region.Metadata);
                restored.Add(new OcrRegionResult(region, item.Recognition));
            }
            return new OcrResult(restored, orientation.InputSize, corrected.DetectionProfileId, corrected.DetectionModelId, corrected.RecognitionProfileId, corrected.RecognitionModelId, corrected.Timing, orientation);
        }
    }
}
