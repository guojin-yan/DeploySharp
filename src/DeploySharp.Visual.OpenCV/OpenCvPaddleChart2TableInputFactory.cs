#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Prepares PP-Chart2Table images with the official GOT RGB bicubic and normalization settings. / 使用官方 GOT RGB 双三次缩放与归一化设置准备 PP-Chart2Table 图像。</summary>
    public sealed class OpenCvPaddleChart2TableInputFactory
    {
        private readonly OpenCvVisualInputFactory _inner = new OpenCvVisualInputFactory();

        /// <summary>Decodes one source and returns a backend-neutral prepared tensor. / 解码一次源图并返回后端无关的已准备张量。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return _inner.Create(source, "pixel_values", PaddleChart2TableOnnxPreprocessing.CreateOfficial(), source.Sha256, cancellationToken);
        }

        /// <summary>Prepares one absolute local image file. / 准备一个绝对路径的本地图像文件。</summary>
        public PreparedVisualInput CreateFromFile(string path, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromFile(path), cancellationToken);

        /// <summary>Prepares one encoded image byte array. / 准备一份编码图像字节数组。</summary>
        public PreparedVisualInput CreateFromBytes(byte[] bytes, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromBytes(bytes), cancellationToken);
    }
}
#endif
