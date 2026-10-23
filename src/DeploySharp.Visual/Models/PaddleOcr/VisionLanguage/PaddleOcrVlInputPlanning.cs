using System;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Identifies an official PaddleOCR-VL-1.6 single-image prompt. / 标识 PaddleOCR-VL-1.6 官方单图任务 Prompt。</summary>
    public enum PaddleOcrVlTask
    {
        /// <summary>Recognizes text from a supplied image or region. / 识别输入图像或区域中的文本。</summary>
        TextRecognition = 0,
        /// <summary>Recognizes table structure and content. / 识别表格结构与内容。</summary>
        TableRecognition = 1,
        /// <summary>Recognizes mathematical formulas. / 识别数学公式。</summary>
        FormulaRecognition = 2,
        /// <summary>Recognizes chart content. / 识别图表内容。</summary>
        ChartRecognition = 3,
        /// <summary>Generates text spotting output. / 生成文本定位识别输出。</summary>
        TextSpotting = 4,
        /// <summary>Recognizes seal text. / 识别印章文本。</summary>
        SealRecognition = 5
    }

    /// <summary>Describes scalar geometry and token counts before PaddleOCR-VL-1.6 pixel preparation. / 描述 PaddleOCR-VL-1.6 像素处理之前的尺寸和图像 Token 数。</summary>
    /// <remarks>This is a preparation plan, not an image tensor, tokenizer or inference Session. / 此类型仅规划输入，不代表已生成图像张量、Tokenizer 或推理 Session。</remarks>
    public sealed class PaddleOcrVlImagePlan
    {
        internal PaddleOcrVlImagePlan(VisualSize source, VisualSize prepared, VisualSize resized, PaddleOcrVlTask task)
        {
            SourceSize = source;
            PreparedSize = prepared;
            ResizedSize = resized;
            Task = task;
            GridHeight = resized.Height / 14;
            GridWidth = resized.Width / 14;
            PatchCount = checked(GridHeight * GridWidth);
            ImageTokenCount = PatchCount / 4;
        }

        /// <summary>Gets the source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets dimensions after the optional official spotting 2x Lanczos step. / 获取可选官方 Spotting 双倍 Lanczos 处理后的尺寸。</summary>
        public VisualSize PreparedSize { get; }
        /// <summary>Gets the final bicubic resize dimensions, both divisible by 28. / 获取最终双三次缩放尺寸，宽高均为 28 的倍数。</summary>
        public VisualSize ResizedSize { get; }
        /// <summary>Gets whether a separate 2x Lanczos step is required before bicubic resize. / 获取双三次缩放前是否需要独立的双倍 Lanczos 步骤。</summary>
        public bool RequiresSpottingUpscale => PreparedSize != SourceSize;
        /// <summary>Gets the requested official task. / 获取请求的官方任务。</summary>
        public PaddleOcrVlTask Task { get; }
        /// <summary>Gets spatial grid height before 2x2 merging; image temporal grid is one. / 获取 2x2 合并前的空间网格高度，静态图时间网格为 1。</summary>
        public int GridHeight { get; }
        /// <summary>Gets spatial grid width before 2x2 merging. / 获取 2x2 合并前的空间网格宽度。</summary>
        public int GridWidth { get; }
        /// <summary>Gets the number of flattened 14x14 RGB patches. / 获取展平的 14x14 RGB Patch 数量。</summary>
        public int PatchCount { get; }
        /// <summary>Gets image-placeholder count after spatial 2x2 merging. / 获取空间 2x2 合并后的图像占位 Token 数量。</summary>
        public int ImageTokenCount { get; }
    }

    /// <summary>Plans official PaddleOCR-VL-1.6 image geometry and task prompts without allocating pixels or tensors. / 规划官方 PaddleOCR-VL-1.6 图像几何和任务 Prompt，不分配像素或 Tensor。</summary>
    public static class PaddleOcrVlInputPlanning
    {
        /// <summary>Gets the pinned upstream checkpoint revision used for these contracts. / 获取本合同使用的上游 Checkpoint Revision。</summary>
        public const string OfficialV16Revision = "c5630abae1d940eafe0697512a0325494b02ab42";

        /// <summary>Returns the exact upstream task prompt; does not add a chat template or tokenize. / 返回精确上游任务 Prompt，不添加 Chat Template 或执行分词。</summary>
        public static string GetOfficialV16Prompt(PaddleOcrVlTask task)
        {
            switch (task)
            {
                case PaddleOcrVlTask.TextRecognition: return "OCR:";
                case PaddleOcrVlTask.TableRecognition: return "Table Recognition:";
                case PaddleOcrVlTask.FormulaRecognition: return "Formula Recognition:";
                case PaddleOcrVlTask.ChartRecognition: return "Chart Recognition:";
                case PaddleOcrVlTask.TextSpotting: return "Spotting:";
                case PaddleOcrVlTask.SealRecognition: return "Seal Recognition:";
                default: throw new ArgumentOutOfRangeException(nameof(task));
            }
        }

        /// <summary>Plans one image using the official processor pixel budgets and Python round-to-even geometry. / 使用官方 Processor 像素预算与 Python 银行家舍入规划单图。</summary>
        /// <param name="sourceSize">Original image size. / 原始图像尺寸。</param>
        /// <param name="task">Official task; spotting applies the README's optional 2x step and larger budget. / 官方任务，Spotting 按 README 应用双倍步骤及更大预算。</param>
        /// <param name="minimumPixels">Optional processor min-pixels override; default 112,896. / 可选 Processor 最小像素参数，默认 112,896。</param>
        /// <param name="maximumPixels">Optional processor max-pixels override; default 1,003,520, or 1,605,632 for spotting. / 可选 Processor 最大像素参数，默认普通任务 1,003,520、Spotting 1,605,632。</param>
        /// <param name="maximumSourcePixels">Hard source-image safety limit, checked before any geometry work. / 几何处理前检查的源图像素安全硬上限。</param>
        /// <remarks>The upstream min/max pixel arguments govern resize rounding and are not strict output-area limits. This plan preserves that behavior. Pixel interpolation, normalization, patch packing, tokenizer and inference still need separate execution. / 上游 Min/Max 参数参与尺寸舍入，并非输出面积硬界限；本规划保持该行为。像素插值、归一化、Patch 打包、分词与推理须另行执行。</remarks>
        public static PaddleOcrVlImagePlan PlanOfficialV16(VisualSize sourceSize, PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition,
            int? minimumPixels = null, int? maximumPixels = null, long maximumSourcePixels = 128L * 1024L * 1024L)
        {
            GetOfficialV16Prompt(task);
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) throw new ArgumentException("A positive source image size is required.", nameof(sourceSize));
            if (maximumSourcePixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSourcePixels));
            if ((long)sourceSize.Width * sourceSize.Height > maximumSourcePixels) throw new ArgumentOutOfRangeException(nameof(sourceSize), "Source image exceeds its pixel budget.");
            int minimum = minimumPixels ?? 112896;
            int maximum = maximumPixels ?? (task == PaddleOcrVlTask.TextSpotting ? 1605632 : 1003520);
            if (minimum < 784) throw new ArgumentOutOfRangeException(nameof(minimumPixels), "At least one 28x28 merged patch is required.");
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximumPixels), "Maximum pixels must be at least minimum pixels.");
            bool upscale = task == PaddleOcrVlTask.TextSpotting && sourceSize.Width < 1500 && sourceSize.Height < 1500;
            VisualSize prepared = upscale ? new VisualSize(checked(sourceSize.Width * 2), checked(sourceSize.Height * 2)) : sourceSize;
            const int factor = 28;
            double height = prepared.Height, width = prepared.Width;
            if (height < factor) { width = Math.Round(width * factor / height, MidpointRounding.ToEven); height = factor; }
            if (width < factor) { height = Math.Round(height * factor / width, MidpointRounding.ToEven); width = factor; }
            if (Math.Max(height, width) / Math.Min(height, width) > 200) throw new ArgumentOutOfRangeException(nameof(sourceSize), "Official image aspect ratio must not exceed 200.");
            double resizedHeight = Math.Round(height / factor, MidpointRounding.ToEven) * factor;
            double resizedWidth = Math.Round(width / factor, MidpointRounding.ToEven) * factor;
            if (resizedHeight * resizedWidth > maximum)
            {
                double beta = Math.Sqrt(height * width / maximum);
                resizedHeight = Math.Floor(height / beta / factor) * factor;
                resizedWidth = Math.Floor(width / beta / factor) * factor;
            }
            else if (resizedHeight * resizedWidth < minimum)
            {
                double beta = Math.Sqrt(minimum / (height * width));
                resizedHeight = Math.Ceiling(height * beta / factor) * factor;
                resizedWidth = Math.Ceiling(width * beta / factor) * factor;
            }
            if (resizedHeight < factor || resizedWidth < factor) throw new ArgumentOutOfRangeException(nameof(maximumPixels), "Pixel budget would produce an empty patch dimension.");
            return new PaddleOcrVlImagePlan(sourceSize, prepared, new VisualSize(checked((int)resizedWidth), checked((int)resizedHeight)), task);
        }
    }
}
