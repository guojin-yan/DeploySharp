using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies a right-angle text orientation supplied by a detector or configuration. / 标识由检测器或配置提供的直角文本方向。</summary>
    public enum TextOrientation
    {
        /// <summary>No rotation. / 不旋转。</summary>
        Degrees0 = 0,
        /// <summary>Rotate 90 degrees clockwise before recognition. / 识别前顺时针旋转 90 度。</summary>
        Clockwise90 = 1,
        /// <summary>Rotate 180 degrees before recognition. / 识别前旋转 180 度。</summary>
        Degrees180 = 2,
        /// <summary>Rotate 90 degrees counter-clockwise before recognition. / 识别前逆时针旋转 90 度。</summary>
        CounterClockwise90 = 3
    }

    /// <summary>Identifies deterministic region reading order. / 标识确定性的区域阅读顺序。</summary>
    public enum TextReadingOrder
    {
        /// <summary>Group rows top-to-bottom, then sort each row left-to-right. / 从上到下分行，再在行内从左到右排序。</summary>
        TopToBottomThenLeftToRight = 0,
        /// <summary>Sort left-to-right, then top-to-bottom. / 从左到右排序，再从上到下排序。</summary>
        LeftToRightThenTopToBottom = 1
    }

    /// <summary>Identifies fixed or content-derived recognition width. / 标识固定或内容推导的识别宽度。</summary>
    public enum OcrRecognitionWidthMode
    {
        /// <summary>Always use the configured fixed width. / 始终使用配置的固定宽度。</summary>
        Fixed = 0,
        /// <summary>Derive width from quadrilateral aspect ratio, align it, and enforce a maximum. / 根据四边形宽高比推导并对齐宽度，同时执行最大值限制。</summary>
        Dynamic = 1
    }

    /// <summary>Identifies interpolation requested from an image crop adapter. / 标识向图像裁剪适配器请求的插值。</summary>
    public enum TextCropInterpolation
    {
        /// <summary>Nearest-neighbor interpolation. / 最近邻插值。</summary>
        Nearest = 0,
        /// <summary>Linear interpolation. / 线性插值。</summary>
        Linear = 1,
        /// <summary>Cubic interpolation. / 三次插值。</summary>
        Cubic = 2
    }

    /// <summary>Stores an RGB padding color without an image-library dependency. / 存储不依赖图像库的 RGB 填充颜色。</summary>
    public readonly struct TextCropColor : IEquatable<TextCropColor>
    {
        /// <summary>Initializes a crop padding color. / 初始化裁剪填充颜色。</summary>
        public TextCropColor(byte red, byte green, byte blue) { Red = red; Green = green; Blue = blue; }
        /// <summary>Gets red. / 获取红色。</summary>
        public byte Red { get; }
        /// <summary>Gets green. / 获取绿色。</summary>
        public byte Green { get; }
        /// <summary>Gets blue. / 获取蓝色。</summary>
        public byte Blue { get; }
        /// <summary>Gets black. / 获取黑色。</summary>
        public static TextCropColor Black { get; } = new TextCropColor(0, 0, 0);
        /// <inheritdoc />
        /// <remarks>Compares all channels exactly. / 精确比较全部通道。</remarks>
        public bool Equals(TextCropColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue;
        /// <inheritdoc />
        /// <remarks>Compares an object by RGB channels. / 按 RGB 通道比较对象。</remarks>
        public override bool Equals(object? obj) => obj is TextCropColor other && Equals(other);
        /// <inheritdoc />
        /// <remarks>Computes a channel hash. / 计算通道哈希。</remarks>
        public override int GetHashCode() => (Red << 16) | (Green << 8) | Blue;
        /// <summary>Compares colors. / 比较颜色。</summary>
        public static bool operator ==(TextCropColor left, TextCropColor right) => left.Equals(right);
        /// <summary>Compares colors for inequality. / 比较颜色是否不相等。</summary>
        public static bool operator !=(TextCropColor left, TextCropColor right) => !left.Equals(right);
    }

    /// <summary>Represents one immutable source-space text region. / 表示一个不可变的源图空间文本区域。</summary>
    public sealed class TextRegion
    {
        private readonly IReadOnlyDictionary<string, string> _metadata;

        /// <summary>Initializes a text region with an authoritative polygon and optional explicit crop corners. / 使用权威多边形及可选显式裁剪角点初始化文本区域。</summary>
        public TextRegion(int sourceIndex, float score, TextPolygon polygon, TextQuadrilateral? cropQuadrilateral = null, TextOrientation orientation = TextOrientation.Degrees0, float? angleRadians = null, string? language = null, string? script = null, string? externalId = null, IEnumerable<KeyValuePair<string, string>>? metadata = null)
        {
            if (sourceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 1) throw new ArgumentOutOfRangeException(nameof(score));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (cropQuadrilateral != null && !SamePolygon(polygon, cropQuadrilateral.Polygon)) throw new ArgumentException("Crop corners must describe the authoritative polygon.", nameof(cropQuadrilateral));
            if (!Enum.IsDefined(typeof(TextOrientation), orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
            if (angleRadians.HasValue && (float.IsNaN(angleRadians.Value) || float.IsInfinity(angleRadians.Value))) throw new ArgumentOutOfRangeException(nameof(angleRadians));
            SourceIndex = sourceIndex;
            Score = score;
            Polygon = polygon;
            CropQuadrilateral = cropQuadrilateral;
            Orientation = orientation;
            AngleRadians = angleRadians;
            Language = NormalizeOptional(language);
            Script = NormalizeOptional(script);
            ExternalId = NormalizeOptional(externalId);
            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (metadata != null)
            {
                foreach (KeyValuePair<string, string> pair in metadata)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) throw new ArgumentException("Metadata keys must be non-empty and values cannot be null.", nameof(metadata));
                    if (copy.Count >= 64) throw new ArgumentOutOfRangeException(nameof(metadata), "A text region accepts at most 64 metadata entries.");
                    copy.Add(pair.Key, pair.Value);
                }
            }
            _metadata = new ReadOnlyDictionary<string, string>(copy);
        }

        /// <summary>Gets the original tensor candidate index. / 获取原始张量候选索引。</summary>
        public int SourceIndex { get; }
        /// <summary>Gets confidence in [0,1]. / 获取 [0,1] 范围置信度。</summary>
        public float Score { get; }
        /// <summary>Gets the authoritative source-space polygon. / 获取权威源图空间多边形。</summary>
        public TextPolygon Polygon { get; }
        /// <summary>Gets explicit perspective-crop corners when the region is a quadrilateral. / 当区域为四边形时获取显式透视裁剪角点。</summary>
        public TextQuadrilateral? CropQuadrilateral { get; }
        /// <summary>Gets derived axis-aligned bounds. / 获取派生轴对齐边界。</summary>
        public RectangleF AxisAlignedBounds => Polygon.AxisAlignedBounds;
        /// <summary>Gets right-angle orientation. / 获取直角方向。</summary>
        public TextOrientation Orientation { get; }
        /// <summary>Gets optional detector angle metadata in radians. / 获取可选的检测器弧度角元数据。</summary>
        public float? AngleRadians { get; }
        /// <summary>Gets optional language. / 获取可选语言。</summary>
        public string? Language { get; }
        /// <summary>Gets optional script. / 获取可选文字系统。</summary>
        public string? Script { get; }
        /// <summary>Gets optional external identifier. / 获取可选外部标识。</summary>
        public string? ExternalId { get; }
        /// <summary>Gets immutable metadata. / 获取不可变元数据。</summary>
        public IReadOnlyDictionary<string, string> Metadata => _metadata;

        private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static bool SamePolygon(TextPolygon first, TextPolygon second)
        {
            if (first.Vertices.Count != second.Vertices.Count) return false;
            for (int index = 0; index < first.Vertices.Count; index++) if (first.Vertices[index] != second.Vertices[index]) return false;
            return true;
        }
    }

    /// <summary>Contains owned text regions in deterministic reading order. / 包含按确定阅读顺序排列的自有文本区域。</summary>
    public sealed class TextDetectionResult
    {
        private readonly IReadOnlyList<TextRegion> _regions;

        /// <summary>Initializes a text detection result. / 初始化文本检测结果。</summary>
        public TextDetectionResult(IEnumerable<TextRegion> regions, VisualSize sourceSize, string profileId, ModelId modelId)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            var copy = new List<TextRegion>();
            foreach (TextRegion region in regions) copy.Add(region ?? throw new ArgumentException("Regions cannot contain null.", nameof(regions)));
            if (copy.Count > 4096) throw new ArgumentOutOfRangeException(nameof(regions));
            _regions = copy.AsReadOnly();
            SourceSize = sourceSize;
            ProfileId = Required(profileId, nameof(profileId));
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            ModelId = modelId;
        }

        /// <summary>Gets regions in reading order. / 获取按阅读顺序排列的区域。</summary>
        public IReadOnlyList<TextRegion> Regions => _regions;
        /// <summary>Gets source size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets detector profile ID. / 获取检测器 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets detector model ID. / 获取检测器模型 ID。</summary>
        public ModelId ModelId { get; }

        private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value;
    }

    /// <summary>Represents one immutable Unicode-scalar OCR character set. / 表示一个不可变 Unicode 标量 OCR 字符表。</summary>
    public sealed class OcrCharacterSet
    {
        private readonly IReadOnlyList<string> _characters;

        /// <summary>Initializes a character set from a Unicode scalar sequence. / 从 Unicode 标量序列初始化字符表。</summary>
        public OcrCharacterSet(string id, string version, string characters)
        {
            Id = VisualGuard.Identifier(id, nameof(id));
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("A character-set version is required.", nameof(version));
            if (string.IsNullOrEmpty(characters)) throw new ArgumentException("A character set cannot be empty.", nameof(characters));
            Version = version;
            var values = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < characters.Length; index++)
            {
                char first = characters[index];
                string scalar;
                if (char.IsHighSurrogate(first))
                {
                    if (index + 1 >= characters.Length || !char.IsLowSurrogate(characters[index + 1])) throw new ArgumentException("The character set contains an invalid surrogate pair.", nameof(characters));
                    scalar = characters.Substring(index, 2);
                    index++;
                }
                else
                {
                    if (char.IsLowSurrogate(first)) throw new ArgumentException("The character set contains an unpaired low surrogate.", nameof(characters));
                    scalar = first.ToString();
                }
                if (!unique.Add(scalar)) throw new ArgumentException("Character-set scalars must be unique.", nameof(characters));
                if (values.Count >= 65535) throw new ArgumentOutOfRangeException(nameof(characters));
                values.Add(scalar);
            }
            _characters = values.AsReadOnly();
            Sha256 = Hash(Id + "\n" + Version + "\n" + string.Join("", values));
        }

        /// <summary>Gets stable character-set ID. / 获取稳定字符表 ID。</summary>
        public string Id { get; }
        /// <summary>Gets character-set version. / 获取字符表版本。</summary>
        public string Version { get; }
        /// <summary>Gets Unicode scalar strings. / 获取 Unicode 标量字符串。</summary>
        public IReadOnlyList<string> Characters => _characters;
        /// <summary>Gets scalar count. / 获取标量数量。</summary>
        public int Count => _characters.Count;
        /// <summary>Gets canonical lowercase SHA256. / 获取规范小写 SHA256。</summary>
        public string Sha256 { get; }

        internal string GetCharacter(int index) => _characters[index];

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        internal static string Hex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    /// <summary>Records one CTC timestep decision and its emission state. / 记录一个 CTC 时间步决策及其发射状态。</summary>
    public sealed class OcrToken
    {
        /// <summary>Initializes a CTC token trace. / 初始化 CTC token 追踪。</summary>
        public OcrToken(int timestep, int classIndex, float confidence, string? text, bool isBlank, bool isCollapsedRepeat, bool isUnknown, bool emitted)
        {
            if (timestep < 0) throw new ArgumentOutOfRangeException(nameof(timestep));
            if (classIndex < 0) throw new ArgumentOutOfRangeException(nameof(classIndex));
            if (float.IsNaN(confidence) || float.IsInfinity(confidence) || confidence < 0 || confidence > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
            if (emitted && string.IsNullOrEmpty(text)) throw new ArgumentException("An emitted token requires text.", nameof(text));
            Timestep = timestep;
            ClassIndex = classIndex;
            Confidence = confidence;
            Text = text;
            IsBlank = isBlank;
            IsCollapsedRepeat = isCollapsedRepeat;
            IsUnknown = isUnknown;
            Emitted = emitted;
        }

        /// <summary>Gets timestep. / 获取时间步。</summary>
        public int Timestep { get; }
        /// <summary>Gets selected class index. / 获取所选类别索引。</summary>
        public int ClassIndex { get; }
        /// <summary>Gets probability after optional softmax. / 获取可选 softmax 后的概率。</summary>
        public float Confidence { get; }
        /// <summary>Gets mapped scalar or replacement text. / 获取映射标量或替换文本。</summary>
        public string? Text { get; }
        /// <summary>Gets whether this is blank. / 获取是否为空白。</summary>
        public bool IsBlank { get; }
        /// <summary>Gets whether repeat collapse suppressed this token. / 获取重复折叠是否抑制该 token。</summary>
        public bool IsCollapsedRepeat { get; }
        /// <summary>Gets whether this is an explicit unknown class. / 获取是否为显式未知类别。</summary>
        public bool IsUnknown { get; }
        /// <summary>Gets whether text was emitted. / 获取是否发射文本。</summary>
        public bool Emitted { get; }
    }

    /// <summary>Represents one owned recognized sequence with full CTC traceability. / 表示一个拥有完整 CTC 可追溯信息的自有识别序列。</summary>
    public sealed class RecognizedText
    {
        private readonly IReadOnlyList<OcrToken> _tokens;

        /// <summary>Initializes a recognized sequence. / 初始化识别序列。</summary>
        public RecognizedText(int sourceRegionIndex, string text, float confidence, IEnumerable<OcrToken> tokens, string characterSetId, string characterSetVersion, string characterSetSha256)
        {
            if (sourceRegionIndex < 0) throw new ArgumentOutOfRangeException(nameof(sourceRegionIndex));
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (float.IsNaN(confidence) || float.IsInfinity(confidence) || confidence < 0 || confidence > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            var copy = new List<OcrToken>();
            foreach (OcrToken token in tokens) copy.Add(token ?? throw new ArgumentException("Tokens cannot contain null.", nameof(tokens)));
            _tokens = copy.AsReadOnly();
            SourceRegionIndex = sourceRegionIndex;
            Text = text;
            Confidence = confidence;
            CharacterSetId = VisualGuard.Identifier(characterSetId, nameof(characterSetId));
            CharacterSetVersion = string.IsNullOrWhiteSpace(characterSetVersion) ? throw new ArgumentException("A character-set version is required.", nameof(characterSetVersion)) : characterSetVersion;
            CharacterSetSha256 = ValidateHash(characterSetSha256);
        }

        /// <summary>Gets source region index. / 获取源区域索引。</summary>
        public int SourceRegionIndex { get; }
        /// <summary>Gets recognized text. / 获取识别文本。</summary>
        public string Text { get; }
        /// <summary>Gets aggregate confidence. / 获取聚合置信度。</summary>
        public float Confidence { get; }
        /// <summary>Gets all timestep token decisions. / 获取全部时间步 token 决策。</summary>
        public IReadOnlyList<OcrToken> Tokens => _tokens;
        /// <summary>Gets character-set ID. / 获取字符表 ID。</summary>
        public string CharacterSetId { get; }
        /// <summary>Gets character-set version. / 获取字符表版本。</summary>
        public string CharacterSetVersion { get; }
        /// <summary>Gets character-set SHA256. / 获取字符表 SHA256。</summary>
        public string CharacterSetSha256 { get; }

        internal RecognizedText WithSourceRegionIndex(int sourceRegionIndex) => new RecognizedText(sourceRegionIndex, Text, Confidence, _tokens, CharacterSetId, CharacterSetVersion, CharacterSetSha256);

        private static string ValidateHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) throw new ArgumentException("A 64-character SHA256 is required.", nameof(value));
            for (int index = 0; index < value.Length; index++) if (!Uri.IsHexDigit(value[index])) throw new ArgumentException("SHA256 must be hexadecimal.", nameof(value));
            return value.ToLowerInvariant();
        }
    }

    /// <summary>Contains one recognition result per backend batch position. / 包含每个后端批次位置的识别结果。</summary>
    public sealed class TextRecognitionBatchResult
    {
        private readonly IReadOnlyList<RecognizedText> _items;

        /// <summary>Initializes a recognition batch result. / 初始化识别批结果。</summary>
        public TextRecognitionBatchResult(IEnumerable<RecognizedText> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copy = new List<RecognizedText>();
            foreach (RecognizedText item in items) copy.Add(item ?? throw new ArgumentException("Recognition items cannot contain null.", nameof(items)));
            _items = copy.AsReadOnly();
        }

        /// <summary>Gets batch-position results. / 获取批次位置结果。</summary>
        public IReadOnlyList<RecognizedText> Items => _items;
    }

    /// <summary>Defines immutable perspective-crop and recognition preprocessing semantics. / 定义不可变透视裁剪与识别预处理语义。</summary>
    public sealed class TextCropProfile
    {
        private readonly IReadOnlyList<float> _means;
        private readonly IReadOnlyList<float> _scales;

        /// <summary>Initializes a bounded crop profile. / 初始化有界裁剪 Profile。</summary>
        public TextCropProfile(string profileId, int targetHeight, OcrRecognitionWidthMode widthMode, int fixedWidth, int maximumWidth, int widthAlignment = 1, TextCropInterpolation interpolation = TextCropInterpolation.Linear, VisualColorOrder colorOrder = VisualColorOrder.Rgb, VisualTensorLayout layout = VisualTensorLayout.Nchw, IEnumerable<float>? means = null, IEnumerable<float>? scales = null, TextCropColor? paddingColor = null, long maximumCropPixels = 16L * 1024L * 1024L)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (targetHeight <= 0 || fixedWidth <= 0 || maximumWidth <= 0 || fixedWidth > maximumWidth) throw new ArgumentOutOfRangeException(nameof(targetHeight));
            if (widthAlignment <= 0 || widthAlignment > 1024) throw new ArgumentOutOfRangeException(nameof(widthAlignment));
            if (!Enum.IsDefined(typeof(OcrRecognitionWidthMode), widthMode)) throw new ArgumentOutOfRangeException(nameof(widthMode));
            if (!Enum.IsDefined(typeof(TextCropInterpolation), interpolation)) throw new ArgumentOutOfRangeException(nameof(interpolation));
            if (colorOrder != VisualColorOrder.Rgb && colorOrder != VisualColorOrder.Bgr && colorOrder != VisualColorOrder.Gray) throw new ArgumentOutOfRangeException(nameof(colorOrder));
            if (layout != VisualTensorLayout.Nchw && layout != VisualTensorLayout.Nhwc) throw new ArgumentOutOfRangeException(nameof(layout));
            if (maximumCropPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCropPixels));
            TargetHeight = targetHeight;
            WidthMode = widthMode;
            FixedWidth = fixedWidth;
            MaximumWidth = maximumWidth;
            WidthAlignment = widthAlignment;
            Interpolation = interpolation;
            ColorOrder = colorOrder;
            Layout = layout;
            MaximumCropPixels = maximumCropPixels;
            PaddingColor = paddingColor ?? TextCropColor.Black;
            int channels = colorOrder == VisualColorOrder.Gray ? 1 : 3;
            _means = CopyFinite(means, channels, false, nameof(means));
            _scales = CopyFinite(scales, channels, false, nameof(scales));
        }

        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets target height. / 获取目标高度。</summary>
        public int TargetHeight { get; }
        /// <summary>Gets width policy. / 获取宽度策略。</summary>
        public OcrRecognitionWidthMode WidthMode { get; }
        /// <summary>Gets fixed width. / 获取固定宽度。</summary>
        public int FixedWidth { get; }
        /// <summary>Gets maximum width. / 获取最大宽度。</summary>
        public int MaximumWidth { get; }
        /// <summary>Gets width alignment. / 获取宽度对齐。</summary>
        public int WidthAlignment { get; }
        /// <summary>Gets interpolation. / 获取插值方式。</summary>
        public TextCropInterpolation Interpolation { get; }
        /// <summary>Gets output color order. / 获取输出颜色顺序。</summary>
        public VisualColorOrder ColorOrder { get; }
        /// <summary>Gets output layout. / 获取输出布局。</summary>
        public VisualTensorLayout Layout { get; }
        /// <summary>Gets per-channel means. / 获取逐通道均值。</summary>
        public IReadOnlyList<float> Means => _means;
        /// <summary>Gets per-channel multiplication scales. / 获取逐通道乘法缩放。</summary>
        public IReadOnlyList<float> Scales => _scales;
        /// <summary>Gets padding color. / 获取填充颜色。</summary>
        public TextCropColor PaddingColor { get; }
        /// <summary>Gets maximum intermediate crop pixels. / 获取最大中间裁剪像素数。</summary>
        public long MaximumCropPixels { get; }

        /// <summary>Calculates aligned output width from explicit quadrilateral geometry and orientation. / 根据显式四边形几何与方向计算对齐输出宽度。</summary>
        public int CalculateWidth(TextQuadrilateral quadrilateral, TextOrientation orientation)
        {
            if (quadrilateral == null) throw new ArgumentNullException(nameof(quadrilateral));
            if (!Enum.IsDefined(typeof(TextOrientation), orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
            if (WidthMode == OcrRecognitionWidthMode.Fixed) return FixedWidth;
            double width = Math.Max(Distance(quadrilateral.TopLeft, quadrilateral.TopRight), Distance(quadrilateral.BottomLeft, quadrilateral.BottomRight));
            double height = Math.Max(Distance(quadrilateral.TopLeft, quadrilateral.BottomLeft), Distance(quadrilateral.TopRight, quadrilateral.BottomRight));
            if (orientation == TextOrientation.Clockwise90 || orientation == TextOrientation.CounterClockwise90)
            {
                double swap = width;
                width = height;
                height = swap;
            }
            if (height <= 0) throw new VisualException(VisualErrorCodes.InputInvalid, "A text crop has zero height.", profileId: ProfileId);
            int raw = Math.Max(1, checked((int)Math.Ceiling(TargetHeight * width / height)));
            int aligned = checked(((raw + WidthAlignment - 1) / WidthAlignment) * WidthAlignment);
            return Math.Min(MaximumWidth, aligned);
        }

        internal int ChannelCount => ColorOrder == VisualColorOrder.Gray ? 1 : 3;
        internal float Mean(int channel) => _means.Count == 0 ? 0 : _means[_means.Count == 1 ? 0 : channel];
        internal float Scale(int channel) => _scales.Count == 0 ? 1 : _scales[_scales.Count == 1 ? 0 : channel];

        private static double Distance(PointF first, PointF second)
        {
            double x = second.X - first.X;
            double y = second.Y - first.Y;
            return Math.Sqrt((x * x) + (y * y));
        }

        private static IReadOnlyList<float> CopyFinite(IEnumerable<float>? values, int channels, bool positive, string name)
        {
            var copy = new List<float>();
            if (values != null) foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || (positive && value <= 0)) throw new ArgumentOutOfRangeException(name);
                copy.Add(value);
            }
            if (copy.Count != 0 && copy.Count != 1 && copy.Count != channels) throw new ArgumentException("Normalization values must be empty, scalar, or channel-sized.", name);
            return copy.AsReadOnly();
        }
    }

    /// <summary>Describes one explicit source quadrilateral crop request. / 描述一个显式源图四边形裁剪请求。</summary>
    public sealed class TextCropRequest
    {
        /// <summary>Initializes a crop request. / 初始化裁剪请求。</summary>
        public TextCropRequest(TextRegion region, TextCropProfile profile)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Quadrilateral = region.CropQuadrilateral ?? throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "Perspective OCR cropping requires explicit quadrilateral corner roles.", profileId: profile.ProfileId);
            TargetWidth = profile.CalculateWidth(Quadrilateral, region.Orientation);
            TargetHeight = profile.TargetHeight;
            if (checked((long)TargetWidth * TargetHeight) > profile.MaximumCropPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "The OCR crop exceeds its pixel limit.", profileId: profile.ProfileId);
        }

        /// <summary>Gets source region. / 获取源区域。</summary>
        public TextRegion Region { get; }
        /// <summary>Gets explicit source corners. / 获取显式源图角点。</summary>
        public TextQuadrilateral Quadrilateral { get; }
        /// <summary>Gets crop profile. / 获取裁剪 Profile。</summary>
        public TextCropProfile Profile { get; }
        /// <summary>Gets target width. / 获取目标宽度。</summary>
        public int TargetWidth { get; }
        /// <summary>Gets target height. / 获取目标高度。</summary>
        public int TargetHeight { get; }
    }

    /// <summary>Provides one detection tensor and image-library-specific recognition crops without leaking vendor types. / 提供一个检测张量和图像库特定识别裁剪，同时不泄漏 vendor 类型。</summary>
    public interface IOcrImageInput : IDisposable
    {
        /// <summary>Gets source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the prepared detector input. / 获取已准备检测器输入。</summary>
        public PreparedVisualInput DetectionInput { get; }
        /// <summary>Creates one owned recognition batch for requests that share target dimensions. / 为目标尺寸相同的请求创建一个自有识别批输入。</summary>
        public PreparedVisualInput PrepareRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken);
    }

    /// <summary>Combines one detected region with its recognized text. / 将一个检测区域与其识别文本组合。</summary>
    public sealed class OcrRegionResult
    {
        /// <summary>Initializes an OCR region result. / 初始化 OCR 区域结果。</summary>
        public OcrRegionResult(TextRegion region, RecognizedText recognition)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Recognition = recognition ?? throw new ArgumentNullException(nameof(recognition));
            if (region.SourceIndex != recognition.SourceRegionIndex) throw new ArgumentException("Detection and recognition source indexes must match.", nameof(recognition));
        }

        /// <summary>Gets detected region. / 获取检测区域。</summary>
        public TextRegion Region { get; }
        /// <summary>Gets recognized text. / 获取识别文本。</summary>
        public RecognizedText Recognition { get; }
    }

    /// <summary>Contains measured OCR stages for one end-to-end call. / 包含一次端到端调用测得的 OCR 阶段。</summary>
    public sealed class OcrStageTiming
    {
        /// <summary>Initializes OCR timing. / 初始化 OCR 时长。</summary>
        public OcrStageTiming(TimeSpan detection, TimeSpan cropAndBatch, TimeSpan recognition, TimeSpan orchestration)
        {
            if (detection < TimeSpan.Zero || cropAndBatch < TimeSpan.Zero || recognition < TimeSpan.Zero || orchestration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(detection));
            Detection = detection;
            CropAndBatch = cropAndBatch;
            Recognition = recognition;
            Orchestration = orchestration;
        }
        /// <summary>Gets detector pipeline time. / 获取检测器 Pipeline 时长。</summary>
        public TimeSpan Detection { get; }
        /// <summary>Gets perspective-crop and batch preparation time. / 获取透视裁剪和批准备时长。</summary>
        public TimeSpan CropAndBatch { get; }
        /// <summary>Gets recognizer pipeline time. / 获取识别器 Pipeline 时长。</summary>
        public TimeSpan Recognition { get; }
        /// <summary>Gets ordering and merge overhead. / 获取排序与合并开销。</summary>
        public TimeSpan Orchestration { get; }
        /// <summary>Gets measured total. / 获取测量总时长。</summary>
        public TimeSpan Total => Detection + CropAndBatch + Recognition + Orchestration;
    }

    /// <summary>Represents an owned, canonical OCR result independent from backend and image lifetimes. / 表示独立于后端和图像生命周期的自有规范 OCR 结果。</summary>
    public sealed class OcrResult
    {
        private readonly IReadOnlyList<OcrRegionResult> _regions;

        /// <summary>Initializes an OCR result in explicit reading order. / 按显式阅读顺序初始化 OCR 结果。</summary>
        public OcrResult(IEnumerable<OcrRegionResult> regions, VisualSize sourceSize, string detectionProfileId, ModelId detectionModelId, string recognitionProfileId, ModelId recognitionModelId, OcrStageTiming timing)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            var copy = new List<OcrRegionResult>();
            foreach (OcrRegionResult region in regions) copy.Add(region ?? throw new ArgumentException("OCR regions cannot contain null.", nameof(regions)));
            _regions = copy.AsReadOnly();
            SourceSize = sourceSize;
            DetectionProfileId = Required(detectionProfileId, nameof(detectionProfileId));
            RecognitionProfileId = Required(recognitionProfileId, nameof(recognitionProfileId));
            if (detectionModelId.IsEmpty || recognitionModelId.IsEmpty) throw new ArgumentException("Both OCR model identifiers are required.");
            DetectionModelId = detectionModelId;
            RecognitionModelId = recognitionModelId;
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        }

        /// <summary>Gets region/text pairs in reading order. / 获取按阅读顺序排列的区域/文本对。</summary>
        public IReadOnlyList<OcrRegionResult> Regions => _regions;
        /// <summary>Gets source size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets detector profile ID. / 获取检测器 Profile ID。</summary>
        public string DetectionProfileId { get; }
        /// <summary>Gets detector model ID. / 获取检测器模型 ID。</summary>
        public ModelId DetectionModelId { get; }
        /// <summary>Gets recognizer profile ID. / 获取识别器 Profile ID。</summary>
        public string RecognitionProfileId { get; }
        /// <summary>Gets recognizer model ID. / 获取识别器模型 ID。</summary>
        public ModelId RecognitionModelId { get; }
        /// <summary>Gets measured stage timing. / 获取测得的阶段时长。</summary>
        public OcrStageTiming Timing { get; }

        /// <summary>Computes canonical SHA256 over provenance, ordered geometry, tokens, confidence, and text. / 对来源、顺序几何、token、置信度和文本计算规范 SHA256。</summary>
        public string ComputeSha256()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(SourceSize.Width);
                writer.Write(SourceSize.Height);
                writer.Write(DetectionProfileId);
                writer.Write(DetectionModelId.Value);
                writer.Write(RecognitionProfileId);
                writer.Write(RecognitionModelId.Value);
                writer.Write(_regions.Count);
                foreach (OcrRegionResult item in _regions)
                {
                    writer.Write(item.Region.SourceIndex);
                    writer.Write(item.Region.Score);
                    writer.Write(item.Region.Polygon.Vertices.Count);
                    foreach (PointF point in item.Region.Polygon.Vertices) { writer.Write(point.X); writer.Write(point.Y); }
                    writer.Write((int)item.Region.Orientation);
                    writer.Write(item.Recognition.Text);
                    writer.Write(item.Recognition.Confidence);
                    writer.Write(item.Recognition.CharacterSetId);
                    writer.Write(item.Recognition.CharacterSetVersion);
                    writer.Write(item.Recognition.CharacterSetSha256);
                    writer.Write(item.Recognition.Tokens.Count);
                    foreach (OcrToken token in item.Recognition.Tokens)
                    {
                        writer.Write(token.Timestep);
                        writer.Write(token.ClassIndex);
                        writer.Write(token.Confidence);
                        writer.Write(token.Text ?? string.Empty);
                        writer.Write(token.IsBlank);
                        writer.Write(token.IsCollapsedRepeat);
                        writer.Write(token.IsUnknown);
                        writer.Write(token.Emitted);
                    }
                }
                writer.Flush();
                using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(stream.ToArray()));
            }
        }

        private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value;
    }
}
