using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies a supported BRIA background-removal artifact family. / 标识受支持的 BRIA 背景移除工件族。</summary>
    public enum BriaRmbgFamily
    {
        /// <summary>BRIA RMBG 1.4 fixed 1024-by-1024 saliency export. / BRIA RMBG 1.4 固定 1024×1024 显著性导出。</summary>
        Rmbg14 = 0,
        /// <summary>BRIA RMBG 2.0 dynamic alpha export. / BRIA RMBG 2.0 动态 Alpha 导出。</summary>
        Rmbg20 = 1
    }

    /// <summary>Identifies a semantic-alpha tensor layout. / 标识语义 Alpha 张量布局。</summary>
    public enum AlphaTensorLayout
    {
        /// <summary>Batch, channel, height, width. / 批次、通道、高度、宽度。</summary>
        Nchw = 0,
        /// <summary>Batch, height, width, channel. / 批次、高度、宽度、通道。</summary>
        Nhwc = 1
    }

    /// <summary>Owns a finite source-space alpha plane in the inclusive range [0,1]. / 拥有闭区间 [0,1] 内有限的源图 Alpha 平面。</summary>
    public sealed class AlphaMask
    {
        private readonly float[] _values;

        /// <summary>Initializes an alpha mask by defensively copying values. / 通过防御性复制值初始化 Alpha 掩码。</summary>
        public AlphaMask(int width, int height, float[] values) : this(width, height, values, false) { }

        internal AlphaMask(int width, int height, float[] values, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if ((long)width * height != values.LongLength) throw new ArgumentException("Alpha dimensions do not match the value count.", nameof(values));
            for (int index = 0; index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f) throw new ArgumentException("Alpha values must be finite and remain in [0,1].", nameof(values));
            }
            Width = width;
            Height = height;
            _values = takeOwnership ? values : (float[])values.Clone();
        }

        /// <summary>Gets mask width. / 获取掩码宽度。</summary>
        public int Width { get; }
        /// <summary>Gets mask height. / 获取掩码高度。</summary>
        public int Height { get; }
        /// <summary>Gets pixel count. / 获取像素数。</summary>
        public int PixelCount => _values.Length;

        /// <summary>Gets one zero-based alpha value. / 获取一个从零开始的 Alpha 值。</summary>
        public float GetValue(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _values[(y * Width) + x];
        }

        /// <summary>Returns a defensive row-major copy. / 返回行优先防御性副本。</summary>
        public float[] ToArray() => (float[])_values.Clone();

        /// <summary>Computes a canonical SHA256 over dimensions and row-major alpha values. / 对尺寸与行优先 Alpha 值计算规范 SHA256。</summary>
        public string ComputeSha256()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            using (SHA256 sha = SHA256.Create())
            {
                writer.Write(Width);
                writer.Write(Height);
                for (int index = 0; index < _values.Length; index++) writer.Write(_values[index]);
                writer.Flush();
                byte[] hash = sha.ComputeHash(stream.ToArray());
                const string hex = "0123456789abcdef";
                var result = new char[hash.Length * 2];
                for (int index = 0; index < hash.Length; index++)
                {
                    result[index * 2] = hex[hash[index] >> 4];
                    result[(index * 2) + 1] = hex[hash[index] & 15];
                }
                return new string(result);
            }
        }

        /// <summary>Composites an RGB foreground over one RGB background and returns owned RGB bytes. / 将 RGB 前景合成到一个 RGB 背景并返回自有 RGB 字节。</summary>
        public byte[] CompositeRgb(byte[] foregroundRgb, byte backgroundRed, byte backgroundGreen, byte backgroundBlue)
        {
            if (foregroundRgb == null) throw new ArgumentNullException(nameof(foregroundRgb));
            if (foregroundRgb.LongLength != (long)_values.Length * 3) throw new ArgumentException("RGB byte count must equal alpha pixels times three.", nameof(foregroundRgb));
            var result = new byte[foregroundRgb.Length];
            for (int pixel = 0; pixel < _values.Length; pixel++)
            {
                float alpha = _values[pixel];
                int offset = pixel * 3;
                result[offset] = Blend(foregroundRgb[offset], backgroundRed, alpha);
                result[offset + 1] = Blend(foregroundRgb[offset + 1], backgroundGreen, alpha);
                result[offset + 2] = Blend(foregroundRgb[offset + 2], backgroundBlue, alpha);
            }
            return result;
        }

        internal float[] DangerousGetReadOnlyBuffer() => _values;

        private static byte Blend(byte foreground, byte background, float alpha)
        {
            float value = (foreground * alpha) + (background * (1f - alpha));
            return checked((byte)Math.Max(0, Math.Min(255, (int)Math.Round(value, MidpointRounding.AwayFromZero))));
        }
    }

    /// <summary>Contains an owned semantic alpha mask and its authoritative source transform. / 包含自有语义 Alpha 掩码及其权威源图变换。</summary>
    public sealed class BackgroundRemovalResult
    {
        /// <summary>Initializes a background-removal result. / 初始化背景移除结果。</summary>
        public BackgroundRemovalResult(AlphaMask alpha, VisualSize sourceSize, ImageTransform transform, string profileId, ModelId modelId)
        {
            Alpha = alpha ?? throw new ArgumentNullException(nameof(alpha));
            if (alpha.Width != sourceSize.Width || alpha.Height != sourceSize.Height) throw new ArgumentException("Alpha dimensions must equal source-image dimensions.", nameof(alpha));
            Transform = transform ?? throw new ArgumentNullException(nameof(transform));
            if (transform.SourceSize != sourceSize) throw new ArgumentException("Transform source size must equal the result source size.", nameof(transform));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            if (modelId.IsEmpty) throw new ArgumentException("A model ID is required.", nameof(modelId));
            SourceSize = sourceSize;
            ProfileId = profileId;
            ModelId = modelId;
        }

        /// <summary>Gets the owned source-space alpha mask. / 获取自有源图 Alpha 掩码。</summary>
        public AlphaMask Alpha { get; }
        /// <summary>Gets source-image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the preprocessing transform. / 获取预处理变换。</summary>
        public ImageTransform Transform { get; }
        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets model ID. / 获取模型 ID。</summary>
        public ModelId ModelId { get; }
    }

    /// <summary>Defines one strict semantic-alpha output. / 定义一个严格的语义 Alpha 输出。</summary>
    public sealed class AlphaOutputSchema
    {
        /// <summary>Initializes an alpha output schema. / 初始化 Alpha 输出 Schema。</summary>
        public AlphaOutputSchema(string outputName, AlphaTensorLayout layout, bool outputIsProbability = true)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An alpha output name is required.", nameof(outputName));
            if (!Enum.IsDefined(typeof(AlphaTensorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            OutputName = outputName;
            Layout = layout;
            OutputIsProbability = outputIsProbability;
        }

        /// <summary>Gets exact output name. / 获取精确输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets tensor layout. / 获取张量布局。</summary>
        public AlphaTensorLayout Layout { get; }
        /// <summary>Gets whether the graph already emits probabilities. / 获取计算图是否已输出概率。</summary>
        public bool OutputIsProbability { get; }
    }

    /// <summary>Controls bounded semantic-alpha decoding. / 控制有界语义 Alpha 解码。</summary>
    public sealed class AlphaDecoderOptions
    {
        /// <summary>Initializes alpha decoder options. / 初始化 Alpha 解码选项。</summary>
        public AlphaDecoderOptions(long maximumPixels = 64L * 1024 * 1024, long maximumWorkspaceBytes = 512L * 1024 * 1024)
        {
            if (maximumPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPixels));
            if (maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkspaceBytes));
            MaximumPixels = maximumPixels;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets maximum tensor/model/source pixels. / 获取张量、模型或源图最大像素数。</summary>
        public long MaximumPixels { get; }
        /// <summary>Gets maximum estimated workspace bytes. / 获取最大估算工作区字节数。</summary>
        public long MaximumWorkspaceBytes { get; }
    }

    /// <summary>Decodes one BRIA semantic-alpha tensor and restores it to source coordinates. / 解码一个 BRIA 语义 Alpha 张量并恢复到源图坐标。</summary>
    public sealed class AlphaMattingDecoder : IVisualDecoder
    {
        /// <summary>Initializes an alpha decoder. / 初始化 Alpha 解码器。</summary>
        public AlphaMattingDecoder(AlphaOutputSchema schema, AlphaDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new AlphaDecoderOptions();
        }

        /// <summary>Gets foreground-matting task. / 获取前景抠图任务。</summary>
        public VisualTaskId Task => VisualTaskId.ForegroundMatting;
        /// <summary>Gets output schema. / 获取输出 Schema。</summary>
        public AlphaOutputSchema Schema { get; }
        /// <summary>Gets decoder options. / 获取解码选项。</summary>
        public AlphaDecoderOptions Options { get; }

        /// <summary>Returns a source-space alpha mask with owned managed storage. / 返回拥有托管存储的源图 Alpha 掩码。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, "BRIA alpha decoding requires batch size one.");
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(Schema.OutputName); }
            catch (KeyNotFoundException exception) { throw Failure(context, "The configured alpha output is missing.", exception); }
            if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64) throw Failure(context, "Alpha output requires Float32 or Float64.");
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 4 || shape[0] != 1) throw Failure(context, "Alpha output must have rank four and batch one.", technicalDetails: shape.ToString());
            long channels = Schema.Layout == AlphaTensorLayout.Nchw ? shape[1] : shape[3];
            long height = Schema.Layout == AlphaTensorLayout.Nchw ? shape[2] : shape[1];
            long width = Schema.Layout == AlphaTensorLayout.Nchw ? shape[3] : shape[2];
            if (channels != 1 || width <= 0 || height <= 0 || width > int.MaxValue || height > int.MaxValue) throw Failure(context, "Alpha output must contain one finite spatial channel.", technicalDetails: shape.ToString());
            long tensorPixels = checked(width * height);
            long sourcePixels = checked((long)context.Input.SourceSize.Width * context.Input.SourceSize.Height);
            long modelPixels = checked((long)context.Input.ModelSize.Width * context.Input.ModelSize.Height);
            if (tensorPixels > Options.MaximumPixels || sourcePixels > Options.MaximumPixels || modelPixels > Options.MaximumPixels) throw Failure(context, "Alpha pixels exceed the configured bound.");
            if (checked((tensorPixels + sourcePixels + modelPixels) * sizeof(float)) > Options.MaximumWorkspaceBytes) throw Failure(context, "Estimated alpha workspace exceeds the configured bound.");
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.OutputName);
            var plane = new float[checked((int)tensorPixels)];
            for (int y = 0; y < (int)height; y++)
            {
                if ((y & 63) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < (int)width; x++)
                {
                    int sourceIndex = Schema.Layout == AlphaTensorLayout.Nchw ? (y * (int)width) + x : ((y * (int)width + x) * 1);
                    float value = values[sourceIndex];
                    if (!Schema.OutputIsProbability) value = 1f / (1f + (float)Math.Exp(-value));
                    if (value < 0f || value > 1f) throw Failure(context, "Alpha probabilities must remain in [0,1].", technicalDetails: "index=" + sourceIndex + ";value=" + value);
                    plane[(y * (int)width) + x] = value;
                }
            }

            float[] modelPlane = (int)width == context.Input.ModelSize.Width && (int)height == context.Input.ModelSize.Height
                ? plane
                : Resize(plane, (int)width, (int)height, context.Input.ModelSize.Width, context.Input.ModelSize.Height, context.CancellationToken);
            float[] sourcePlane = RestoreSource(modelPlane, context);
            return new BackgroundRemovalResult(new AlphaMask(context.Input.SourceSize.Width, context.Input.SourceSize.Height, sourcePlane, true), context.Input.SourceSize, context.Input.Transform, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private static float[] RestoreSource(float[] model, VisualDecodeContext context)
        {
            int sourceWidth = context.Input.SourceSize.Width;
            int sourceHeight = context.Input.SourceSize.Height;
            int modelWidth = context.Input.ModelSize.Width;
            int modelHeight = context.Input.ModelSize.Height;
            var result = new float[checked(sourceWidth * sourceHeight)];
            for (int y = 0; y < sourceHeight; y++)
            {
                if ((y & 63) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < sourceWidth; x++)
                {
                    // Pixel-center restoration avoids treating a semantic mask as a categorical segmentation map. / 像素中心恢复避免将语义 Alpha 误作离散语义分割图。
                    float modelX = ((x + 0.5f) * context.Input.Transform.ScaleX) + context.Input.Transform.OffsetX - 0.5f;
                    float modelY = ((y + 0.5f) * context.Input.Transform.ScaleY) + context.Input.Transform.OffsetY - 0.5f;
                    result[(y * sourceWidth) + x] = modelX < -0.5f || modelX > modelWidth - 0.5f || modelY < -0.5f || modelY > modelHeight - 0.5f
                        ? 0f
                        : Sample(model, modelWidth, modelHeight, modelX, modelY);
                }
            }
            return result;
        }

        private static float[] Resize(float[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, System.Threading.CancellationToken cancellationToken)
        {
            var result = new float[checked(targetWidth * targetHeight)];
            for (int y = 0; y < targetHeight; y++)
            {
                if ((y & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                float sourceY = ((y + 0.5f) * sourceHeight / targetHeight) - 0.5f;
                for (int x = 0; x < targetWidth; x++) result[(y * targetWidth) + x] = Sample(source, sourceWidth, sourceHeight, ((x + 0.5f) * sourceWidth / targetWidth) - 0.5f, sourceY);
            }
            return result;
        }

        private static float Sample(float[] source, int width, int height, float x, float y)
        {
            x = Math.Max(0f, Math.Min(width - 1, x));
            y = Math.Max(0f, Math.Min(height - 1, y));
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(width - 1, x0 + 1);
            int y1 = Math.Min(height - 1, y0 + 1);
            float tx = x - x0;
            float ty = y - y0;
            float top = source[(y0 * width) + x0] + ((source[(y0 * width) + x1] - source[(y0 * width) + x0]) * tx);
            float bottom = source[(y1 * width) + x0] + ((source[(y1 * width) + x1] - source[(y1 * width) + x0]) * tx);
            return top + ((bottom - top) * ty);
        }

        private static VisualException Failure(VisualDecodeContext context, string message, Exception? exception = null, string? technicalDetails = null)
            => new VisualException(VisualErrorCodes.DecodeFailed, message, exception, context.Profile.ProfileId, SchemaName(context), modelId: context.Profile.ModelId, technicalDetails: technicalDetails);

        private static string? SchemaName(VisualDecodeContext context)
        {
            AlphaMattingDecoder? decoder = context.Profile.Decoder as AlphaMattingDecoder;
            return decoder == null ? null : decoder.Schema.OutputName;
        }
    }

    /// <summary>Controls one immutable artifact-bound BRIA profile. / 控制一个不可变且绑定工件的 BRIA Profile。</summary>
    public sealed class BriaRmbgProfileOptions
    {
        /// <summary>Initializes BRIA profile options. / 初始化 BRIA Profile 选项。</summary>
        public BriaRmbgProfileOptions(int opset, VisualSize modelSize, string inputName, string outputName, string artifactSha256, string upstreamCommit, string exporterVersion, string license, string modelFormat = "onnx", string upstreamRepository = "https://huggingface.co/briaai", int maximumDynamicSide = 4096)
        {
            if (opset <= 0) throw new ArgumentOutOfRangeException(nameof(opset));
            if (string.IsNullOrWhiteSpace(inputName)) throw new ArgumentException("An input name is required.", nameof(inputName));
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            if (string.IsNullOrWhiteSpace(modelFormat)) throw new ArgumentException("A model format is required.", nameof(modelFormat));
            if (maximumDynamicSide <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDynamicSide));
            Opset = opset;
            ModelSize = modelSize;
            InputName = inputName.Trim();
            OutputName = outputName.Trim();
            ArtifactSha256 = NormalizeSha(artifactSha256);
            UpstreamRepository = upstreamRepository ?? string.Empty;
            UpstreamCommit = upstreamCommit ?? string.Empty;
            ExporterVersion = exporterVersion ?? string.Empty;
            License = license ?? string.Empty;
            ModelFormat = modelFormat.Trim();
            MaximumDynamicSide = maximumDynamicSide;
        }

        /// <summary>Gets ONNX opset. / 获取 ONNX opset。</summary>
        public int Opset { get; }
        /// <summary>Gets preferred fixed input size. / 获取首选固定输入尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets input name. / 获取输入名称。</summary>
        public string InputName { get; }
        /// <summary>Gets output name. / 获取输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets artifact SHA256. / 获取工件 SHA256。</summary>
        public string ArtifactSha256 { get; }
        /// <summary>Gets upstream repository. / 获取上游仓库。</summary>
        public string UpstreamRepository { get; }
        /// <summary>Gets upstream commit. / 获取上游提交。</summary>
        public string UpstreamCommit { get; }
        /// <summary>Gets exporter version. / 获取导出器版本。</summary>
        public string ExporterVersion { get; }
        /// <summary>Gets license evidence. / 获取许可证证据。</summary>
        public string License { get; }
        /// <summary>Gets model format. / 获取模型格式。</summary>
        public string ModelFormat { get; }
        /// <summary>Gets dynamic-side safety bound. / 获取动态边长安全上限。</summary>
        public int MaximumDynamicSide { get; }

        private static string NormalizeSha(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length != 64) throw new ArgumentException("Artifact SHA256 must contain 64 hexadecimal characters.", nameof(value));
            for (int index = 0; index < value.Length; index++) if (!Uri.IsHexDigit(value[index])) throw new ArgumentException("Artifact SHA256 must be hexadecimal.", nameof(value));
            return value.ToLowerInvariant();
        }
    }

    /// <summary>Contains an artifact-bound BRIA profile and provenance. / 包含绑定工件的 BRIA Profile 与来源。</summary>
    public sealed class BriaRmbgProfile
    {
        internal BriaRmbgProfile(BriaRmbgFamily family, BriaRmbgProfileOptions options, VisualModelProfile visualProfile)
        {
            Family = family;
            Options = options;
            VisualProfile = visualProfile;
        }

        /// <summary>Gets BRIA family. / 获取 BRIA 模型族。</summary>
        public BriaRmbgFamily Family { get; }
        /// <summary>Gets immutable artifact options. / 获取不可变工件选项。</summary>
        public BriaRmbgProfileOptions Options { get; }
        /// <summary>Gets backend-neutral profile. / 获取后端无关 Profile。</summary>
        public VisualModelProfile VisualProfile { get; }

        /// <summary>Creates a Core artifact bound to the profile SHA. / 创建绑定 Profile SHA 的 Core 工件。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
            => new ModelArtifact(VisualProfile.ModelId, VisualProfile.ModelFormat, path, string.IsNullOrEmpty(Options.ArtifactSha256) ? null : Options.ArtifactSha256, preferredBackend);
    }

    /// <summary>Creates artifact-bound BRIA background-removal profiles. / 创建绑定工件的 BRIA 背景移除 Profile。</summary>
    public static class BriaRmbgProfiles
    {
        /// <summary>Creates an RMBG 1.4 profile with fixed NCHW input and `output` alpha. / 创建固定 NCHW 输入及 `output` Alpha 的 RMBG 1.4 Profile。</summary>
        public static BriaRmbgProfile CreateRmbg14(ModelId modelId, BriaRmbgProfileOptions options) => Create(modelId, BriaRmbgFamily.Rmbg14, options, false);

        /// <summary>Creates an RMBG 2.0 profile with dynamic NCHW input and `alphas` output. / 创建动态 NCHW 输入及 `alphas` 输出的 RMBG 2.0 Profile。</summary>
        public static BriaRmbgProfile CreateRmbg20(ModelId modelId, BriaRmbgProfileOptions options) => Create(modelId, BriaRmbgFamily.Rmbg20, options, true);

        private static BriaRmbgProfile Create(ModelId modelId, BriaRmbgFamily family, BriaRmbgProfileOptions options, bool dynamicSpatial)
        {
            if (modelId.IsEmpty) throw new VisualException(VisualErrorCodes.ProfileInvalid, "A model ID is required.");
            if (options == null) throw new ArgumentNullException(nameof(options));
            TensorShape inputShape = dynamicSpatial ? new TensorShape(1, 3, -1, -1) : new TensorShape(1, 3, options.ModelSize.Height, options.ModelSize.Width);
            TensorShape outputShape = dynamicSpatial ? new TensorShape(1, 1, -1, -1) : new TensorShape(1, 1, options.ModelSize.Height, options.ModelSize.Width);
            string profileId = "bria-rmbg." + (family == BriaRmbgFamily.Rmbg14 ? "1-4" : "2-0") + "." + modelId.Value + ".opset" + options.Opset;
            var decoder = new AlphaMattingDecoder(new AlphaOutputSchema(options.OutputName, AlphaTensorLayout.Nchw, true), new AlphaDecoderOptions(checked((long)options.MaximumDynamicSide * options.MaximumDynamicSide)));
            var visual = new VisualModelProfile(profileId, modelId, VisualTaskId.ForegroundMatting, "bria-rmbg/" + family + "/opset" + options.Opset, options.ModelFormat,
                new VisualInputBinding(options.InputName, TensorElementType.Float32, inputShape, VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(options.OutputName, TensorElementType.Float32, outputShape) },
                Array.Empty<VisualLabel>(), decoder);
            return new BriaRmbgProfile(family, options, visual);
        }
    }
}
