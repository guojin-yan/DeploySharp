using System;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Implements the custom-model preprocessing boundary for encoded OpenCV image sources. / 为 OpenCV 编码图像源实现自定义模型预处理边界。</summary>
    public sealed class OpenCvVisualPreprocessor : IVisualInputPreprocessor<OpenCvImageSource>
    {
        private readonly OpenCvVisualInputFactory _factory;

        /// <summary>Initializes a profile-driven OpenCV preprocessor. / 初始化由 Profile 驱动的 OpenCV 预处理器。</summary>
        public OpenCvVisualPreprocessor(OpenCvVisualInputFactory? factory = null)
        {
            _factory = factory ?? new OpenCvVisualInputFactory();
        }

        /// <inheritdoc />
        public PreparedVisualInput Prepare(OpenCvImageSource input, VisualModelProfile profile, CancellationToken cancellationToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return _factory.Create(input, profile, input.Sha256, cancellationToken);
        }
    }
}
