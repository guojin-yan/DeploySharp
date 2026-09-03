using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Anomalib
{
    /// <summary>Identifies an Anomalib image anomaly exporter family. / 标识一个 Anomalib 图像异常导出族。</summary>
    public enum AnomalibModelFamily
    {
        /// <summary>PaDiM image and pixel anomaly export. / PaDiM 图像与像素异常导出。</summary>
        Padim = 0,
        /// <summary>PatchCore image and pixel anomaly export. / PatchCore 图像与像素异常导出。</summary>
        PatchCore = 1
    }

    /// <summary>Stores one immutable Anomalib exporter contract. / 存储一个不可变 Anomalib 导出器合同。</summary>
    public sealed class AnomalibArtifactContract
    {
        /// <summary>Initializes an Anomalib artifact contract. / 初始化 Anomalib 工件合同。</summary>
        public AnomalibArtifactContract(int opset, string artifactSha256, string upstreamCommit, string exporterVersion, string license = "Apache-2.0", string preprocessingVersion = "anomalib-export-transform-v1", string postprocessingVersion = "anomalib-post-processor-v1", string modelFormat = "onnx", string upstreamRepository = "https://github.com/open-edge-platform/anomalib")
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            if (string.IsNullOrWhiteSpace(preprocessingVersion)) throw new ArgumentException("A preprocessing version is required.", nameof(preprocessingVersion));
            if (string.IsNullOrWhiteSpace(postprocessingVersion)) throw new ArgumentException("A postprocessing version is required.", nameof(postprocessingVersion));
            Opset = opset;
            ArtifactSha256 = NormalizeSha(artifactSha256);
            UpstreamCommit = upstreamCommit ?? string.Empty;
            ExporterVersion = exporterVersion ?? string.Empty;
            License = license ?? string.Empty;
            PreprocessingVersion = preprocessingVersion.Trim();
            PostprocessingVersion = postprocessingVersion.Trim();
            ModelFormat = modelFormat.Trim();
            UpstreamRepository = upstreamRepository ?? string.Empty;
        }

        /// <summary>Gets ONNX opset. / 获取 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets artifact SHA256. / 获取工件 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets upstream commit. / 获取上游提交。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter version. / 获取导出器版本。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets license evidence. / 获取许可证证据。</summary>
        public string License { get; }
        /// <summary>Gets preprocessing contract version. / 获取前处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets model format. / 获取模型格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets upstream repository. / 获取上游仓库。</summary>
        public string UpstreamRepository { get; }

        private static string NormalizeSha(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length != 64) throw new ArgumentException("Artifact SHA256 must contain 64 hexadecimal characters.", nameof(value));
            for (int index = 0; index < value.Length; index++) if (!Uri.IsHexDigit(value[index])) throw new ArgumentException("Artifact SHA256 must be hexadecimal.", nameof(value));
            return value.ToLowerInvariant();
        }
    }

    /// <summary>Decodes Anomalib's four-output export while reusing the common anomaly map decoder. / 解码 Anomalib 四输出导出并复用通用异常图解码器。</summary>
    public sealed class AnomalibExportDecoder : IAnomalyPostprocessor
    {
        private readonly AnomalyDecoder _decoder;

        /// <summary>Initializes an Anomalib export decoder. / 初始化 Anomalib 导出解码器。</summary>
        public AnomalibExportDecoder(string scoreOutputName = "pred_score", string mapOutputName = "anomaly_map", string labelOutputName = "pred_label", string maskOutputName = "pred_mask", AnomalyDecoderOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(scoreOutputName) || string.IsNullOrWhiteSpace(mapOutputName) || string.IsNullOrWhiteSpace(labelOutputName) || string.IsNullOrWhiteSpace(maskOutputName)) throw new ArgumentException("All Anomalib output names are required.");
            ScoreOutputName = scoreOutputName;
            MapOutputName = mapOutputName;
            LabelOutputName = labelOutputName;
            MaskOutputName = maskOutputName;
            Schema = new AnomalyMapSchema(scoreOutputName, mapOutputName, AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 1, AnomalyMapCoordinateSpace.ModelInput);
            _decoder = new AnomalyDecoder(Schema, options ?? new AnomalyDecoderOptions(normalization: AnomalyNormalizationMode.None, threshold: 0.5f, outputSizeMode: AnomalyOutputSizeMode.Source, interpolation: AnomalyMapInterpolation.BilinearHalfPixel, preserveRawMap: true));
        }

        /// <summary>Gets anomaly task. / 获取异常任务。</summary>
        public VisualTaskId Task => VisualTaskId.AnomalyDetection;
        /// <summary>Gets common score/map schema. / 获取通用分数/图 Schema。</summary>
        public AnomalyMapSchema Schema { get; }
        /// <summary>Gets exact image-score output name. / 获取精确图像分数输出名称。</summary>
        public string ScoreOutputName { get; }
        /// <summary>Gets exact anomaly-map output name. / 获取精确异常图输出名称。</summary>
        public string MapOutputName { get; }
        /// <summary>Gets exact label output name. / 获取精确标签输出名称。</summary>
        public string LabelOutputName { get; }
        /// <summary>Gets exact binary-map output name. / 获取精确二值图输出名称。</summary>
        public string MaskOutputName { get; }

        internal AnomalyDecoder CoreDecoder => _decoder;

        /// <summary>Validates and decodes all four named outputs into an owned anomaly result or ordered batch result. / 验证四个命名输出并解码为自有异常结果或有序 Batch 结果。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Outputs.Count != 4) throw Failure(context, "An Anomalib export must contain exactly pred_score, pred_label, anomaly_map, and pred_mask.");
            ValidateAuxiliary(context, LabelOutputName, TensorElementType.Boolean);
            ValidateAuxiliary(context, MaskOutputName, TensorElementType.Boolean);
            var reduced = new InferenceOutputs(new[]
            {
                new NamedTensor(ScoreOutputName, context.Outputs.GetRequired(ScoreOutputName)),
                new NamedTensor(MapOutputName, context.Outputs.GetRequired(MapOutputName))
            });
            return _decoder.DecodeAny(new VisualDecodeContext(context.Input, context.Profile, reduced, context.CancellationToken));
        }

        /// <summary>Decodes an anomaly result through the explicit anomaly contract. / 通过显式异常合同解码异常结果。</summary>
        public AnomalyDetectionResult DecodeAnomaly(VisualDecodeContext context)
        {
            object decoded = Decode(context);
            if (decoded is AnomalyDetectionResult result) return result;
            throw new VisualException(VisualErrorCodes.AnomalyContractInvalid, "A batch anomaly response requires Decode or the batch result type.", profileId: context.Profile.ProfileId, modelId: context.Profile.ModelId);
        }

        internal AnomalyDetectionResult CreateCudaDecodedResult(
            VisualDecodeContext context,
            float imageScore,
            int rawWidth,
            int rawHeight,
            float[] rawValues,
            float[] restoredValues,
            byte[] maskValues,
            int anomalousPixelCount)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Outputs.Count != 4) throw Failure(context, "An Anomalib export must contain exactly pred_score, pred_label, anomaly_map, and pred_mask.");
            ValidateAuxiliary(context, LabelOutputName, TensorElementType.Boolean);
            ValidateAuxiliary(context, MaskOutputName, TensorElementType.Boolean);
            return _decoder.CreateCudaDecodedResult(context, imageScore, rawWidth, rawHeight, rawValues, restoredValues, maskValues, anomalousPixelCount);
        }

        private void ValidateAuxiliary(VisualDecodeContext context, string name, TensorElementType elementType)
        {
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw Failure(context, "An Anomalib auxiliary output is missing.", exception, name); }
            if (tensor.ElementType != elementType) throw Failure(context, "An Anomalib auxiliary output has an unexpected element type.", tensorName: name);
            if (tensor.Length <= 0) throw Failure(context, "An Anomalib auxiliary output cannot be empty.", tensorName: name);
        }

        private VisualException Failure(VisualDecodeContext context, string message, Exception? exception = null, string? tensorName = null)
            => new VisualException(VisualErrorCodes.AnomalyContractInvalid, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId);
    }

    /// <summary>Contains an immutable artifact-bound Anomalib profile. / 包含一个不可变且绑定工件的 Anomalib Profile。</summary>
    public sealed class AnomalibProfile
    {
        internal AnomalibProfile(AnomalibModelFamily family, AnomalibArtifactContract artifact, VisualModelProfile visualProfile)
        {
            Family = family;
            Artifact = artifact;
            VisualProfile = visualProfile;
        }

        /// <summary>Gets model family. / 获取模型族。</summary>
        public AnomalibModelFamily Family { get; }
        /// <summary>Gets artifact contract. / 获取工件合同。</summary>
        public AnomalibArtifactContract Artifact { get; }
        /// <summary>Gets backend-neutral profile. / 获取后端无关 Profile。</summary>
        public VisualModelProfile VisualProfile { get; }

        /// <summary>Creates a Core artifact bound to the profile SHA. / 创建绑定 Profile SHA 的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
            => new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, string.IsNullOrEmpty(Artifact.ArtifactSha256) ? null : Artifact.ArtifactSha256, preferredBackend);
    }

    /// <summary>Creates PaDiM and PatchCore Anomalib segmentation profiles. / 创建 PaDiM 与 PatchCore Anomalib 分割 Profile。</summary>
    public static class AnomalibProfiles
    {
        /// <summary>Creates a PaDiM four-output profile. / 创建 PaDiM 四输出 Profile。</summary>
        public static AnomalibProfile CreatePadim(ModelId modelId, AnomalibArtifactContract artifact, VisualSize modelSize = default(VisualSize), int maximumBatch = 1)
            => Create(modelId, AnomalibModelFamily.Padim, artifact, modelSize, maximumBatch, new TensorShape(maximumBatch > 1 ? -1 : 1, 1), new TensorShape(maximumBatch > 1 ? -1 : 1, 1), new TensorShape(maximumBatch > 1 ? -1 : 1, 1, -1, -1));

        /// <summary>Creates a PatchCore four-output profile. / 创建 PatchCore 四输出 Profile。</summary>
        public static AnomalibProfile CreatePatchCore(ModelId modelId, AnomalibArtifactContract artifact, VisualSize modelSize = default(VisualSize), int maximumBatch = 1)
            => Create(modelId, AnomalibModelFamily.PatchCore, artifact, modelSize, maximumBatch, new TensorShape(maximumBatch > 1 ? -1 : 1), new TensorShape(maximumBatch > 1 ? -1 : 1), new TensorShape(maximumBatch > 1 ? -1 : 1, 1, -1, -1));

        private static AnomalibProfile Create(ModelId modelId, AnomalibModelFamily family, AnomalibArtifactContract artifact, VisualSize modelSize, int maximumBatch, TensorShape scoreShape, TensorShape labelShape, TensorShape maskShape)
        {
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model ID is required.");
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            if (modelSize.Width <= 0 || modelSize.Height <= 0) modelSize = new VisualSize(256, 256);
            var decoder = new AnomalibExportDecoder();
            string id = "anomalib." + (family == AnomalibModelFamily.Padim ? "padim" : "patchcore") + "." + modelId.Value + ".opset" + artifact.Opset;
            var outputs = new[]
            {
                new VisualOutputBinding("pred_score", TensorElementType.Float32, scoreShape),
                new VisualOutputBinding("pred_label", TensorElementType.Boolean, labelShape),
                new VisualOutputBinding("anomaly_map", TensorElementType.Float32, new TensorShape(maximumBatch > 1 ? -1 : 1, 1, -1, -1)),
                new VisualOutputBinding("pred_mask", TensorElementType.Boolean, maskShape)
            };
            var visual = new VisualModelProfile(id, modelId, VisualTaskId.AnomalyDetection, "anomalib/" + family + "/opset" + artifact.Opset, artifact.ModelFormat,
                new VisualInputBinding("input", TensorElementType.Float32, new TensorShape(maximumBatch > 1 ? -1 : 1, 3, modelSize.Height, modelSize.Width), VisualTensorLayout.Nchw, 1, maximumBatch), outputs, Array.Empty<VisualLabel>(), decoder);
            return new AnomalibProfile(family, artifact, visual);
        }
    }
}
