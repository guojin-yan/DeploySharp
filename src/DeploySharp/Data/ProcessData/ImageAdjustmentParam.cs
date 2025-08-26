using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// 图像调整参数（内部结构体）
    /// </summary>
    public struct ImageAdjustmentParam : IEquatable<ImageAdjustmentParam>
    {
        /// <summary>图像各边的填充量（通常为宽/高）</summary>
        public Pair<int, int> Padding { get; }

        /// <summary>图像的宽高比例调整系数</summary>
        public Pair<float, float> Ratio { get; }

        public Size RowImgSize { get; }

        public Size TargetImgSize { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="padding">填充量（First=宽方向，Second=高方向）</param>
        /// <param name="ratio">比例系数（First=宽比例，Second=高比例）</param>
        public ImageAdjustmentParam(Pair<int, int> padding, Pair<float, float> ratio, Size rowImgSize, Size targetImgSize)
        {
            Padding = padding;
            Ratio = ratio;
            RowImgSize = rowImgSize;
            TargetImgSize = targetImgSize;
        }

        /// <summary>
        /// 解构方法（支持模式匹配和解构赋值）
        /// </summary>
        public void Deconstruct(out Pair<int, int> padding, out Pair<float, float> ratio, out Size rowImgSize, out Size targetImgSize)
        {
            padding = Padding;
            ratio = Ratio;
            rowImgSize = RowImgSize;
            targetImgSize = TargetImgSize;
        }

        /// <summary>
        /// 重写 ToString() 以便输出可读信息
        /// </summary>
        public override string ToString() =>
            $"Padding: [宽={Padding.First}, 高={Padding.Second}], Ratio: [宽={Ratio.First}, 高={Ratio.Second}], RowImgSize: [宽={RowImgSize.Width}, 高={RowImgSize.Height}], RowImgSize:  [宽={TargetImgSize.Width}, 高={TargetImgSize.Height}]";

        // 实现 IEquatable<T> 接口（支持值相等比较）
        public bool Equals(ImageAdjustmentParam other) =>
            Padding.Equals(other.Padding) && Ratio.Equals(other.Ratio);

        public override bool Equals(object obj) =>
            obj is ImageAdjustmentParam other && Equals(other);


        // 运算符重载（== 和 !=）
        public static bool operator ==(ImageAdjustmentParam left, ImageAdjustmentParam right) =>
            left.Equals(right);

        public static bool operator !=(ImageAdjustmentParam left, ImageAdjustmentParam right) =>
            !(left == right);


        public Rect AdjustRect(RectF rectangle)
        {
            var x = (rectangle.X - Padding.First) / Ratio.First;
            var y = (rectangle.Y - Padding.Second) / Ratio.Second;
            var w = rectangle.Width / Ratio.First;
            var h = rectangle.Height / Ratio.Second;

            return new Rect((int)x, (int)y, (int)w, (int)h);
        }

        public RectF AdjustRectF(RectF rectangle)
        {
            var x = (rectangle.X - Padding.First) / Ratio.First;
            var y = (rectangle.Y - Padding.Second) / Ratio.Second;
            var w = rectangle.Width / Ratio.First;
            var h = rectangle.Height / Ratio.Second;

            return new RectF(x, y, w, h);
        }

        public Point AdjustPoint(Point point)
        {
            var x = (point.X - Padding.First) / Ratio.First;
            var y = (point.Y - Padding.Second) / Ratio.Second;

            return new Point(x, y);
        }


        public static ImageAdjustmentParam CreateFromImageInfo(Size inputSize, Size imgSize, ResizeMode resizeMode)
        {
            // 确保输入尺寸合法
            if (inputSize.Width <= 0 || inputSize.Height <= 0 || imgSize.Width <= 0 || imgSize.Height <= 0)
                throw new ArgumentException("Image dimensions must be positive.");

            float srcWidth = imgSize.Width;
            float srcHeight = imgSize.Height;
            float targetWidth = inputSize.Width;
            float targetHeight = inputSize.Height;

            float widthRatio, heightRatio;
            int padX = 0, padY = 0;

            switch (resizeMode)
            {
                case ResizeMode.Stretch:
                    // 直接拉伸，比例各自独立
                    widthRatio = targetWidth / srcWidth;
                    heightRatio = targetHeight / srcHeight;

                    // 拉伸模式下无填充
                    return new ImageAdjustmentParam(
                        new Pair<int, int>(0, 0),
                        new Pair<float, float>(widthRatio, heightRatio),
                        imgSize,
                        inputSize
                    );

                case ResizeMode.Pad:
                    // 等比缩放，计算单边填充
                    float scale = Math.Min(targetWidth / srcWidth, targetHeight / srcHeight);
                    widthRatio = heightRatio = scale;

                    // 计算填充量（等比缩放后，宽或高仍小于目标的差值）
                    int finalWidth = (int)(srcWidth * scale);
                    int finalHeight = (int)(srcHeight * scale);
                    padX = (int)(targetWidth - finalWidth) / 2;
                    padY = (int)(targetHeight - finalHeight) / 2;

                    return new ImageAdjustmentParam(
                        new Pair<int, int>(padX, padY),
                        new Pair<float, float>(widthRatio, heightRatio),
                        imgSize,
                        inputSize
                    );

                case ResizeMode.Max:
                    // 等比缩放，不超过目标尺寸
                    float maxScale = Math.Min(targetWidth / srcWidth, targetHeight / srcHeight);
                    widthRatio = heightRatio = maxScale;

                    // 无填充
                    return new ImageAdjustmentParam(
                        new Pair<int, int>(0, 0),
                        new Pair<float, float>(widthRatio, heightRatio),
                        imgSize,
                        inputSize
                    );

                case ResizeMode.Crop:
                    // 等比缩放后裁剪，计算需要裁剪的部分
                    float cropScale = Math.Max(targetWidth / srcWidth, targetHeight / srcHeight);
                    widthRatio = heightRatio = cropScale;

                    // 无填充（裁剪后的边缘将被丢弃）
                    return new ImageAdjustmentParam(
                        new Pair<int, int>(0, 0),
                        new Pair<float, float>(widthRatio, heightRatio),
                        imgSize,
                        inputSize
                    );

                default:
                    throw new ArgumentOutOfRangeException(nameof(resizeMode));
            }
        }
    }

}
