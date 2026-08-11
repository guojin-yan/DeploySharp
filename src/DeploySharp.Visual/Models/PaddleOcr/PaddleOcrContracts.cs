using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Identifies one PaddleOCR model-family row. / 标识一个 PaddleOCR 模型族清单行。</summary>
    public enum PaddleOcrFamily
    {
        /// <summary>PaddleOCR DB or DB++ text detection. / PaddleOCR DB 或 DB++ 文本检测。</summary>
        PaddleOcrDet = 0,
        /// <summary>PaddleOCR CTC text recognition. / PaddleOCR CTC 文本识别。</summary>
        PaddleOcrRec = 1,
        /// <summary>PaddleOCR 0/180-degree text-line orientation classification. / PaddleOCR 文本行 0/180 度方向分类。</summary>
        PaddleOcrCls = 2
    }

    /// <summary>Identifies DB candidate scoring semantics. / 标识 DB 候选评分语义。</summary>
    public enum PaddleDbScoreMode
    {
        /// <summary>Average probability inside the candidate bounding rectangle. / 对候选外接矩形内概率取平均。</summary>
        Fast = 0,
        /// <summary>Average probability only over threshold-connected pixels. / 仅对阈值连通像素取平均。</summary>
        Slow = 1
    }

    /// <summary>Identifies returned DB geometry. / 标识 DB 返回几何类型。</summary>
    public enum PaddleDbBoxType
    {
        /// <summary>Return one ordered quadrilateral. / 返回一个有序四边形。</summary>
        Quadrilateral = 0,
        /// <summary>Return a managed convex polygon when available. / 可用时返回托管凸多边形。</summary>
        Polygon = 1
    }

    /// <summary>Defines bounded DB/DB++ probability-map postprocessing. / 定义有界 DB/DB++ 概率图后处理。</summary>
    public sealed class PaddleDbPostprocessOptions
    {
        /// <summary>Initializes DB postprocessing options. / 初始化 DB 后处理选项。</summary>
        public PaddleDbPostprocessOptions(float probabilityThreshold = 0.3f, float boxThreshold = 0.6f, float unclipRatio = 1.5f, PaddleDbScoreMode scoreMode = PaddleDbScoreMode.Fast, PaddleDbBoxType boxType = PaddleDbBoxType.Quadrilateral, int minimumSide = 3, int maximumCandidates = 1000, int maximumRegions = 128, long maximumMapPixels = 64L * 1024 * 1024, long maximumWorkspaceBytes = 256L * 1024 * 1024)
        {
            ValidateProbability(probabilityThreshold, nameof(probabilityThreshold));
            ValidateProbability(boxThreshold, nameof(boxThreshold));
            if (float.IsNaN(unclipRatio) || float.IsInfinity(unclipRatio) || unclipRatio <= 0f) throw new ArgumentOutOfRangeException(nameof(unclipRatio));
            if (!Enum.IsDefined(typeof(PaddleDbScoreMode), scoreMode)) throw new ArgumentOutOfRangeException(nameof(scoreMode));
            if (!Enum.IsDefined(typeof(PaddleDbBoxType), boxType)) throw new ArgumentOutOfRangeException(nameof(boxType));
            if (minimumSide <= 0) throw new ArgumentOutOfRangeException(nameof(minimumSide));
            if (maximumCandidates <= 0 || maximumRegions <= 0 || maximumRegions > maximumCandidates) throw new ArgumentOutOfRangeException(nameof(maximumRegions));
            if (maximumMapPixels <= 0 || maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumMapPixels));
            ProbabilityThreshold = probabilityThreshold;
            BoxThreshold = boxThreshold;
            UnclipRatio = unclipRatio;
            ScoreMode = scoreMode;
            BoxType = boxType;
            MinimumSide = minimumSide;
            MaximumCandidates = maximumCandidates;
            MaximumRegions = maximumRegions;
            MaximumMapPixels = maximumMapPixels;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets bitmap probability threshold. / 获取位图概率阈值。</summary>
        public float ProbabilityThreshold { get; }
        /// <summary>Gets candidate mean-score threshold. / 获取候选平均分阈值。</summary>
        public float BoxThreshold { get; }
        /// <summary>Gets DB polygon expansion ratio. / 获取 DB 多边形扩张比例。</summary>
        public float UnclipRatio { get; }
        /// <summary>Gets score mode. / 获取评分模式。</summary>
        public PaddleDbScoreMode ScoreMode { get; }
        /// <summary>Gets returned geometry type. / 获取返回几何类型。</summary>
        public PaddleDbBoxType BoxType { get; }
        /// <summary>Gets minimum tensor-space side. / 获取最小张量空间边长。</summary>
        public int MinimumSide { get; }
        /// <summary>Gets maximum connected candidates. / 获取最大连通候选数。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets maximum returned regions. / 获取最大返回区域数。</summary>
        public int MaximumRegions { get; }
        /// <summary>Gets maximum probability-map pixels. / 获取最大概率图像素数。</summary>
        public long MaximumMapPixels { get; }
        /// <summary>Gets maximum estimated workspace bytes. / 获取最大估算工作区字节数。</summary>
        public long MaximumWorkspaceBytes { get; }

        private static void ValidateProbability(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f) throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Decodes a strict DB probability map into ordered source-space text regions. / 将严格 DB 概率图解码为有序源图文本区域。</summary>
    public sealed class PaddleDbTextDetectionDecoder : IVisualDecoder
    {
        /// <summary>Initializes a DB decoder. / 初始化 DB 解码器。</summary>
        public PaddleDbTextDetectionDecoder(string outputName, PaddleDbPostprocessOptions? options = null, int maximumSide = 4000)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("A probability-map output name is required.", nameof(outputName));
            if (maximumSide <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSide));
            OutputName = outputName;
            Options = options ?? new PaddleDbPostprocessOptions();
            MaximumSide = maximumSide;
        }

        /// <summary>Gets text-detection task. / 获取文本检测任务。</summary>
        public VisualTaskId Task => VisualTaskId.TextDetection;
        /// <summary>Gets exact probability-map output name. / 获取精确概率图输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets DB options. / 获取 DB 选项。</summary>
        public PaddleDbPostprocessOptions Options { get; }
        /// <summary>Gets the maximum accepted probability-map side. / 获取允许的概率图最大边长。</summary>
        public int MaximumSide { get; }

        /// <summary>Thresholds the probability map, scores bounded connected candidates, expands them, and restores source coordinates. / 对概率图阈值化、对有界连通候选评分并扩张，然后恢复源图坐标。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, "PaddleOCR detection requires batch size one.");
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw Failure(context, "The DB probability-map output is missing.", exception); }
            if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64) throw Failure(context, "DB probability maps require Float32 or Float64.");
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 4 || shape[0] != 1 || shape[1] != 1 || shape[2] <= 0 || shape[3] <= 0 || shape[2] > int.MaxValue || shape[3] > int.MaxValue) throw Failure(context, "DB probability maps must have shape [1,1,H,W].", technicalDetails: shape.ToString());
            int height = checked((int)shape[2]);
            int width = checked((int)shape[3]);
            if (height > MaximumSide || width > MaximumSide) throw Failure(context, "DB probability-map dimensions exceed their configured side bound.", technicalDetails: shape.ToString());
            long pixels = checked((long)width * height);
            if (pixels != tensor.Length || pixels > Options.MaximumMapPixels || pixels > int.MaxValue) throw Failure(context, "DB probability-map pixels exceed their configured bound.");
            if (checked(pixels * 17L) > Options.MaximumWorkspaceBytes) throw Failure(context, "Estimated DB workspace exceeds its configured bound.");
            float[] probabilities = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, OutputName);
            var active = new bool[(int)pixels];
            for (int index = 0; index < probabilities.Length; index++)
            {
                float value = probabilities[index];
                if (value < 0f || value > 1f) throw Failure(context, "DB probability values must remain in [0,1].", technicalDetails: "index=" + index + ";value=" + value);
                active[index] = value > Options.ProbabilityThreshold;
            }

            var visited = new bool[active.Length];
            var queue = new int[active.Length];
            var candidates = new List<Candidate>();
            for (int start = 0; start < active.Length; start++)
            {
                if ((start & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                if (!active[start] || visited[start]) continue;
                if (candidates.Count >= Options.MaximumCandidates) throw Failure(context, "DB connected candidates exceed their configured bound.");
                int head = 0;
                int tail = 0;
                int minX = width;
                int minY = height;
                int maxX = -1;
                int maxY = -1;
                double maskScore = 0d;
                visited[start] = true;
                queue[tail++] = start;
                while (head < tail)
                {
                    int current = queue[head++];
                    int x = current % width;
                    int y = current / width;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                    maskScore += probabilities[current];
                    Visit(x - 1, y, width, height, active, visited, queue, ref tail);
                    Visit(x + 1, y, width, height, active, visited, queue, ref tail);
                    Visit(x, y - 1, width, height, active, visited, queue, ref tail);
                    Visit(x, y + 1, width, height, active, visited, queue, ref tail);
                }

                int candidateWidth = maxX - minX + 1;
                int candidateHeight = maxY - minY + 1;
                if (Math.Min(candidateWidth, candidateHeight) < Options.MinimumSide) continue;
                float score = Options.ScoreMode == PaddleDbScoreMode.Slow
                    ? checked((float)(maskScore / tail))
                    : RectangleMean(probabilities, width, minX, minY, maxX, maxY);
                if (score < Options.BoxThreshold) continue;
                candidates.Add(new Candidate(minX, minY, maxX + 1, maxY + 1, score));
            }

            candidates.Sort(CandidateComparer.Instance);
            var regions = new List<TextRegion>(Math.Min(candidates.Count, Options.MaximumRegions));
            for (int index = 0; index < candidates.Count && regions.Count < Options.MaximumRegions; index++)
            {
                Candidate candidate = Expand(candidates[index], width, height, Options.UnclipRatio);
                PointF topLeft = Restore(candidate.Left, candidate.Top, width, height, context);
                PointF topRight = Restore(candidate.Right, candidate.Top, width, height, context);
                PointF bottomRight = Restore(candidate.Right, candidate.Bottom, width, height, context);
                PointF bottomLeft = Restore(candidate.Left, candidate.Bottom, width, height, context);
                var quadrilateral = new TextQuadrilateral(topLeft, topRight, bottomRight, bottomLeft, TextCornerOrder.TopLeftClockwise);
                regions.Add(new TextRegion(index, candidate.Score, quadrilateral.Polygon, quadrilateral, metadata: new[]
                {
                    new KeyValuePair<string, string>("paddle.db.scoreMode", Options.ScoreMode.ToString()),
                    new KeyValuePair<string, string>("paddle.db.boxType", Options.BoxType.ToString()),
                    new KeyValuePair<string, string>("paddle.db.unclipRatio", Options.UnclipRatio.ToString(System.Globalization.CultureInfo.InvariantCulture))
                }));
            }
            return new TextDetectionResult(regions, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private static void Visit(int x, int y, int width, int height, bool[] active, bool[] visited, int[] queue, ref int tail)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int index = (y * width) + x;
            if (!active[index] || visited[index]) return;
            visited[index] = true;
            queue[tail++] = index;
        }

        private static float RectangleMean(float[] values, int width, int minX, int minY, int maxX, int maxY)
        {
            double sum = 0d;
            int count = 0;
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) { sum += values[(y * width) + x]; count++; }
            return checked((float)(sum / count));
        }

        private static Candidate Expand(Candidate candidate, int width, int height, float ratio)
        {
            float boxWidth = candidate.Right - candidate.Left;
            float boxHeight = candidate.Bottom - candidate.Top;
            float area = boxWidth * boxHeight;
            float perimeter = 2f * (boxWidth + boxHeight);
            // DB unclip uses distance = area * ratio / perimeter; this managed rectangle path preserves that official distance rule. / DB unclip 使用 distance = area * ratio / perimeter；此托管矩形路径保留官方距离规则。
            float distance = perimeter <= 0f ? 0f : (area * ratio / perimeter);
            return new Candidate(Math.Max(0f, candidate.Left - distance), Math.Max(0f, candidate.Top - distance), Math.Min(width, candidate.Right + distance), Math.Min(height, candidate.Bottom + distance), candidate.Score);
        }

        private static PointF Restore(float mapX, float mapY, int mapWidth, int mapHeight, VisualDecodeContext context)
        {
            float modelX = mapX * context.Input.ModelSize.Width / mapWidth;
            float modelY = mapY * context.Input.ModelSize.Height / mapHeight;
            PointF source = context.Input.Transform.ToSource(new PointF(modelX, modelY));
            return new PointF(Math.Max(0f, Math.Min(context.Input.SourceSize.Width, source.X)), Math.Max(0f, Math.Min(context.Input.SourceSize.Height, source.Y)));
        }

        private VisualException Failure(VisualDecodeContext context, string message, Exception? exception = null, string? technicalDetails = null)
            => new VisualException(VisualErrorCodes.DecodeFailed, message, exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);

        private readonly struct Candidate
        {
            public Candidate(float left, float top, float right, float bottom, float score) { Left = left; Top = top; Right = right; Bottom = bottom; Score = score; }
            public float Left { get; }
            public float Top { get; }
            public float Right { get; }
            public float Bottom { get; }
            public float Score { get; }
        }

        private sealed class CandidateComparer : IComparer<Candidate>
        {
            public static CandidateComparer Instance { get; } = new CandidateComparer();
            public int Compare(Candidate x, Candidate y)
            {
                int row = x.Top.CompareTo(y.Top);
                if (row != 0) return row;
                int column = x.Left.CompareTo(y.Left);
                return column != 0 ? column : y.Score.CompareTo(x.Score);
            }
        }
    }

    /// <summary>Stores immutable exporter, artifact, dictionary, and license provenance for one PaddleOCR profile. / 存储一个 PaddleOCR Profile 的不可变导出器、工件、字典与许可证来源。</summary>
    public sealed class PaddleOcrArtifactContract
    {
        /// <summary>Initializes an artifact contract. / 初始化工件合同。</summary>
        public PaddleOcrArtifactContract(int opset, string artifactSha256, string upstreamCommit, string exporterVersion, string license, string preprocessingVersion, string postprocessingVersion, string modelFormat = "onnx", string upstreamRepository = "https://github.com/PaddlePaddle/PaddleOCR", string? dictionarySha256 = null, string dictionaryLicense = "")
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            if (string.IsNullOrWhiteSpace(preprocessingVersion)) throw new ArgumentException("A preprocessing version is required.", nameof(preprocessingVersion));
            if (string.IsNullOrWhiteSpace(postprocessingVersion)) throw new ArgumentException("A postprocessing version is required.", nameof(postprocessingVersion));
            Opset = opset;
            ArtifactSha256 = NormalizeSha(artifactSha256, nameof(artifactSha256));
            DictionarySha256 = dictionarySha256 == null ? null : NormalizeSha(dictionarySha256, nameof(dictionarySha256));
            UpstreamRepository = upstreamRepository ?? string.Empty;
            UpstreamCommit = upstreamCommit ?? string.Empty;
            ExporterVersion = exporterVersion ?? string.Empty;
            License = license ?? string.Empty;
            DictionaryLicense = dictionaryLicense ?? string.Empty;
            PreprocessingVersion = preprocessingVersion.Trim();
            PostprocessingVersion = postprocessingVersion.Trim();
            ModelFormat = modelFormat.Trim();
        }

        /// <summary>Gets ONNX opset. / 获取 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets model SHA256. / 获取模型 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets optional dictionary SHA256. / 获取可选字典 SHA256。</summary>
        public string? DictionarySha256 { get; }
        /// <summary>Gets upstream repository. / 获取上游仓库。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets upstream commit. / 获取上游提交。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter version. / 获取导出器版本。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets model license. / 获取模型许可证。</summary>
        public string License { get; }
        /// <summary>Gets dictionary license. / 获取字典许可证。</summary>
        public string DictionaryLicense { get; }
        /// <summary>Gets preprocessing contract version. / 获取前处理合同版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets postprocessing contract version. / 获取后处理合同版本。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets model format. / 获取模型格式。</summary>
        public string ModelFormat { get; }

        private static string NormalizeSha(string value, string name)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length != 64) throw new ArgumentException("SHA256 must contain 64 hexadecimal characters.", name);
            for (int index = 0; index < value.Length; index++) if (!Uri.IsHexDigit(value[index])) throw new ArgumentException("SHA256 must be hexadecimal.", name);
            return value.ToLowerInvariant();
        }
    }

    /// <summary>Contains one artifact-bound PaddleOCR visual profile. / 包含一个绑定工件的 PaddleOCR Visual Profile。</summary>
    public sealed class PaddleOcrProfile
    {
        internal PaddleOcrProfile(PaddleOcrFamily family, PaddleOcrArtifactContract artifact, VisualModelProfile visualProfile, TextCropProfile? cropProfile, OcrCharacterSet? characterSet)
        {
            Family = family;
            Artifact = artifact;
            VisualProfile = visualProfile;
            CropProfile = cropProfile;
            CharacterSet = characterSet;
        }

        /// <summary>Gets model family. / 获取模型族。</summary>
        public PaddleOcrFamily Family { get; }
        /// <summary>Gets artifact contract. / 获取工件合同。</summary>
        public PaddleOcrArtifactContract Artifact { get; }
        /// <summary>Gets backend-neutral profile. / 获取后端无关 Profile。</summary>
        public VisualModelProfile VisualProfile { get; }
        /// <summary>Gets recognition crop profile when applicable. / 适用时获取识别裁剪 Profile。</summary>
        public TextCropProfile? CropProfile { get; }
        /// <summary>Gets recognition character set when applicable. / 适用时获取识别字符表。</summary>
        public OcrCharacterSet? CharacterSet { get; }

        /// <summary>Creates a Core artifact bound to the recorded SHA. / 创建绑定所记录 SHA 的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
            => new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, string.IsNullOrEmpty(Artifact.ArtifactSha256) ? null : Artifact.ArtifactSha256, preferredBackend);
    }

    /// <summary>Creates exact named-tensor PaddleOCR detection, orientation, and recognition profiles. / 创建精确 named-tensor PaddleOCR 检测、方向与识别 Profile。</summary>
    public static class PaddleOcrProfiles
    {
        /// <summary>Creates a DB/DB++ probability-map detection profile. / 创建 DB/DB++ 概率图检测 Profile。</summary>
        public static PaddleOcrProfile CreateDetection(ModelId modelId, PaddleOcrArtifactContract artifact, string inputName = "x", string outputName = "fetch_name_0", PaddleDbPostprocessOptions? postprocess = null, int maximumSide = 4000)
        {
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model ID is required.");
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (string.IsNullOrWhiteSpace(inputName) || string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("Exact tensor names are required.");
            if (maximumSide <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSide));
            var decoder = new PaddleDbTextDetectionDecoder(outputName, postprocess, maximumSide);
            var profile = new VisualModelProfile("paddle-ocr-det." + modelId.Value + ".opset" + artifact.Opset, modelId, VisualTaskId.TextDetection, "paddleocr-db/opset" + artifact.Opset, artifact.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(1, 3, -1, -1), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(1, 1, -1, -1)) }, Array.Empty<VisualLabel>(), decoder);
            return new PaddleOcrProfile(PaddleOcrFamily.PaddleOcrDet, artifact, profile, null, null);
        }

        /// <summary>Creates a CTC probability recognition profile and its matching dynamic-width crop profile. / 创建 CTC 概率识别 Profile 及匹配的动态宽度裁剪 Profile。</summary>
        public static PaddleOcrProfile CreateRecognition(ModelId modelId, PaddleOcrArtifactContract artifact, OcrCharacterSet characterSet, string inputName = "x", string outputName = "fetch_name_0", int inputHeight = 48, int maximumWidth = 3200, int maximumBatch = 64)
        {
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model ID is required.");
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (characterSet == null) throw new ArgumentNullException(nameof(characterSet));
            if (inputHeight <= 0 || maximumWidth <= 0 || maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(inputHeight));
            var ctc = new GreedyCtcDecoder(new CtcOutputSchema(outputName, CtcTensorLayout.BatchTimeClasses), characterSet,
                new CtcDecoderOptions(0, applySoftmax: false, collapseRepeats: true, removeBlank: true, maximumBatch: maximumBatch, maximumSequenceLength: 8192, maximumCharacters: 4096));
            var profile = new VisualModelProfile("paddle-ocr-rec." + modelId.Value + ".opset" + artifact.Opset, modelId, VisualTaskId.TextRecognition, "paddleocr-ctc/opset" + artifact.Opset, artifact.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(-1, 3, inputHeight, -1), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(-1, -1, ctc.ExpectedClassCount)) }, Array.Empty<VisualLabel>(), ctc);
            var crop = new TextCropProfile("paddle-ocr-rec-crop-h" + inputHeight, inputHeight, OcrRecognitionWidthMode.Dynamic, maximumWidth, maximumWidth, 1, TextCropInterpolation.Linear,
                VisualColorOrder.Bgr, VisualTensorLayout.Nchw, new[] { 127.5f }, new[] { 1f / 127.5f }, TextCropColor.Black);
            return new PaddleOcrProfile(PaddleOcrFamily.PaddleOcrRec, artifact, profile, crop, characterSet);
        }

        /// <summary>Creates the legacy PaddleOCR MobileNetV3 BGR 0/180 classifier contract. / 创建旧版 PaddleOCR MobileNetV3 BGR 0/180 分类合同。</summary>
        public static PaddleOcrProfile CreateLegacyClassification(ModelId modelId, PaddleOcrArtifactContract artifact, string inputName = "x", string outputName = "fetch_name_0", float rejectionThreshold = 0.9f, int maximumBatch = 1, bool allowDynamicBatch = false)
        {
            return CreateClassificationCore(modelId, artifact, inputName, outputName, rejectionThreshold, maximumBatch, allowDynamicBatch, new VisualSize(192, 48), "legacy-0", "legacy-180", VisualColorOrder.Bgr,
                new[] { 127.5f }, new[] { 1f / 127.5f }, "paddle-ocr-cls-legacy-bgr-h48-w192");
        }

        /// <summary>Creates the PP-LCNet RGB text-line 0/180 orientation contract. / 创建 PP-LCNet RGB 文本行 0/180 方向合同。</summary>
        public static PaddleOcrProfile CreateTextLineOrientationClassification(ModelId modelId, PaddleOcrArtifactContract artifact, string inputName = "x", string outputName = "fetch_name_0", float rejectionThreshold = 0.9f, int maximumBatch = 1, bool allowDynamicBatch = false)
        {
            return CreateClassificationCore(modelId, artifact, inputName, outputName, rejectionThreshold, maximumBatch, allowDynamicBatch, new VisualSize(160, 80), "0_degree", "180_degree", VisualColorOrder.Rgb,
                new[] { 123.675f, 116.28f, 103.53f }, new[] { 1f / 58.395f, 1f / 57.12f, 1f / 57.375f }, "paddle-ocr-cls-textline-rgb-h80-w160");
        }

        /// <summary>Loads a Paddle dictionary containing one non-empty Unicode token per line and optionally appends the official space class. / 加载每行一个非空 Unicode token 的 Paddle 字典，并可追加官方空格类别。</summary>
        public static OcrCharacterSet LoadCharacterSet(string path, string id, string version, bool useSpaceCharacter, string? expectedFileSha256 = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A dictionary path is required.", nameof(path));
            byte[] bytes = File.ReadAllBytes(path);
            string fileSha;
            using (SHA256 sha = SHA256.Create()) fileSha = OcrCharacterSet.Hex(sha.ComputeHash(bytes));
            if (!string.IsNullOrEmpty(expectedFileSha256) && !string.Equals(expectedFileSha256, fileSha, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "The Paddle dictionary SHA256 does not match the expected artifact binding.");
            string[] lines = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.None);
            var tokens = new List<string>();
            for (int index = 0; index < lines.Length; index++)
            {
                string value = lines[index];
                if (index == lines.Length - 1 && value.Length == 0) continue;
                if (!IsUnicodeToken(value)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Each Paddle dictionary line must contain one or more valid Unicode scalars.", technicalDetails: "line=" + (index + 1));
                tokens.Add(value);
            }
            if (useSpaceCharacter) tokens.Add(" ");
            return new OcrCharacterSet(id, version, tokens);
        }

        private static bool IsUnicodeToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])) return false;
                    index++;
                }
                else if (char.IsLowSurrogate(value[index])) return false;
            }
            return true;
        }

        private static PaddleOcrProfile CreateClassificationCore(ModelId modelId, PaddleOcrArtifactContract artifact, string inputName, string outputName, float rejectionThreshold, int maximumBatch, bool allowDynamicBatch, VisualSize modelSize, string zeroLabel, string oneEightyLabel, VisualColorOrder colorOrder, float[] means, float[] scales, string cropProfileId)
        {
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model ID is required.");
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (string.IsNullOrWhiteSpace(inputName) || string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("Exact tensor names are required.");
            if (float.IsNaN(rejectionThreshold) || float.IsInfinity(rejectionThreshold) || rejectionThreshold < 0f || rejectionThreshold > 1f) throw new ArgumentOutOfRangeException(nameof(rejectionThreshold));
            if (maximumBatch != 1) throw new ArgumentOutOfRangeException(nameof(maximumBatch), "The current orientation result contract requires single-region inference.");
            var mapping = new[] { TextOrientation.Degrees0, TextOrientation.Degrees180 };
            long batchDimension = allowDynamicBatch ? -1 : 1;
            var schema = new OcrOrientationSchema(outputName, new TensorShape(batchDimension, 2), TensorElementType.Float32, mapping, OcrOrientationValueSemantics.Probability, applySoftmax: false, allowDynamicBatch: allowDynamicBatch);
            var decoder = new OcrOrientationDecoder(schema, new OcrOrientationDecoderOptions(rejectionThreshold));
            var profile = new VisualModelProfile("paddle-ocr-cls." + modelId.Value + ".opset" + artifact.Opset, modelId, VisualTaskId.TextOrientationClassification, "paddleocr-cls/opset" + artifact.Opset, artifact.ModelFormat,
                new VisualInputBinding(inputName, TensorElementType.Float32, new TensorShape(batchDimension, 3, modelSize.Height, modelSize.Width), VisualTensorLayout.Nchw, 1, maximumBatch),
                new[] { new VisualOutputBinding(outputName, TensorElementType.Float32, new TensorShape(batchDimension, 2)) },
                new[] { new VisualLabel(0, zeroLabel), new VisualLabel(1, oneEightyLabel) }, decoder);
            var crop = new TextCropProfile(cropProfileId, modelSize.Height, OcrRecognitionWidthMode.Fixed, modelSize.Width, modelSize.Width, 1, TextCropInterpolation.Linear, colorOrder, VisualTensorLayout.Nchw, means, scales, TextCropColor.Black);
            return new PaddleOcrProfile(PaddleOcrFamily.PaddleOcrCls, artifact, profile, crop, null);
        }
    }
}
