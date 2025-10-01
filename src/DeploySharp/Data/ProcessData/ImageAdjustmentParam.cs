using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    ///// <summary>
    ///// 图像调整参数（内部结构体）
    ///// </summary>
    //public struct ImageAdjustmentParam : IEquatable<ImageAdjustmentParam>
    //{
    //    /// <summary>图像各边的填充量（通常为宽/高）</summary>
    //    public Pair<int, int> Padding { get; }

    //    /// <summary>图像的宽高比例调整系数</summary>
    //    public Pair<float, float> Ratio { get; }

    //    public Size RowImgSize { get; }

    //    public Size TargetImgSize { get; }

    //    /// <summary>
    //    /// 构造函数
    //    /// </summary>
    //    /// <param name="padding">填充量（First=宽方向，Second=高方向）</param>
    //    /// <param name="ratio">比例系数（First=宽比例，Second=高比例）</param>
    //    public ImageAdjustmentParam(Pair<int, int> padding, Pair<float, float> ratio, Size rowImgSize, Size targetImgSize)
    //    {
    //        Padding = padding;
    //        Ratio = ratio;
    //        RowImgSize = rowImgSize;
    //        TargetImgSize = targetImgSize;
    //    }

    //    /// <summary>
    //    /// 解构方法（支持模式匹配和解构赋值）
    //    /// </summary>
    //    public void Deconstruct(out Pair<int, int> padding, out Pair<float, float> ratio, out Size rowImgSize, out Size targetImgSize)
    //    {
    //        padding = Padding;
    //        ratio = Ratio;
    //        rowImgSize = RowImgSize;
    //        targetImgSize = TargetImgSize;
    //    }

    //    /// <summary>
    //    /// 重写 ToString() 以便输出可读信息
    //    /// </summary>
    //    public override string ToString() =>
    //        $"Padding: [宽={Padding.First}, 高={Padding.Second}], Ratio: [宽={Ratio.First}, 高={Ratio.Second}], RowImgSize: [宽={RowImgSize.Width}, 高={RowImgSize.Height}], RowImgSize:  [宽={TargetImgSize.Width}, 高={TargetImgSize.Height}]";

    //    // 实现 IEquatable<T> 接口（支持值相等比较）
    //    public bool Equals(ImageAdjustmentParam other) =>
    //        Padding.Equals(other.Padding) && Ratio.Equals(other.Ratio);

    //    public override bool Equals(object obj) =>
    //        obj is ImageAdjustmentParam other && Equals(other);


    //    // 运算符重载（== 和 !=）
    //    public static bool operator ==(ImageAdjustmentParam left, ImageAdjustmentParam right) =>
    //        left.Equals(right);

    //    public static bool operator !=(ImageAdjustmentParam left, ImageAdjustmentParam right) =>
    //        !(left == right);


    //    public Rect AdjustRect(RectF rectangle)
    //    {
    //        var x = (rectangle.X - Padding.First) / Ratio.First;
    //        var y = (rectangle.Y - Padding.Second) / Ratio.Second;
    //        var w = rectangle.Width / Ratio.First;
    //        var h = rectangle.Height / Ratio.Second;

    //        return new Rect((int)x, (int)y, (int)w, (int)h);
    //    }

    //    public RectF AdjustRectF(RectF rectangle)
    //    {
    //        var x = (rectangle.X - Padding.First) / Ratio.First;
    //        var y = (rectangle.Y - Padding.Second) / Ratio.Second;
    //        var w = rectangle.Width / Ratio.First;
    //        var h = rectangle.Height / Ratio.Second;

    //        return new RectF(x, y, w, h);
    //    }

    //    public Point AdjustPoint(Point point)
    //    {
    //        var x = (point.X - Padding.First) / Ratio.First;
    //        var y = (point.Y - Padding.Second) / Ratio.Second;

    //        return new Point(x, y);
    //    }


    //    public static ImageAdjustmentParam CreateFromImageInfo(Size inputSize, Size imgSize, ImageResizeMode resizeMode)
    //    {
    //        // 确保输入尺寸合法
    //        if (inputSize.Width <= 0 || inputSize.Height <= 0 || imgSize.Width <= 0 || imgSize.Height <= 0)
    //            throw new ArgumentException("Image dimensions must be positive.");

    //        float srcWidth = imgSize.Width;
    //        float srcHeight = imgSize.Height;
    //        float targetWidth = inputSize.Width;
    //        float targetHeight = inputSize.Height;

    //        float widthRatio, heightRatio;
    //        int padX = 0, padY = 0;

    //        switch (resizeMode)
    //        {
    //            case ImageResizeMode.Stretch:
    //                // 直接拉伸，比例各自独立
    //                widthRatio = targetWidth / srcWidth;
    //                heightRatio = targetHeight / srcHeight;

    //                // 拉伸模式下无填充
    //                return new ImageAdjustmentParam(
    //                    new Pair<int, int>(0, 0),
    //                    new Pair<float, float>(widthRatio, heightRatio),
    //                    imgSize,
    //                    inputSize
    //                );

    //            case ImageResizeMode.Pad:
    //                // 等比缩放，计算单边填充
    //                float scale = Math.Min(targetWidth / srcWidth, targetHeight / srcHeight);
    //                widthRatio = heightRatio = scale;

    //                // 计算填充量（等比缩放后，宽或高仍小于目标的差值）
    //                int finalWidth = (int)(srcWidth * scale);
    //                int finalHeight = (int)(srcHeight * scale);
    //                padX = (int)(targetWidth - finalWidth) / 2;
    //                padY = (int)(targetHeight - finalHeight) / 2;

    //                return new ImageAdjustmentParam(
    //                    new Pair<int, int>(padX, padY),
    //                    new Pair<float, float>(widthRatio, heightRatio),
    //                    imgSize,
    //                    inputSize
    //                );

    //            case ImageResizeMode.Max:
    //                // 等比缩放，不超过目标尺寸
    //                float maxScale = Math.Min(targetWidth / srcWidth, targetHeight / srcHeight);
    //                widthRatio = heightRatio = maxScale;

    //                // 无填充
    //                return new ImageAdjustmentParam(
    //                    new Pair<int, int>(0, 0),
    //                    new Pair<float, float>(widthRatio, heightRatio),
    //                    imgSize,
    //                    inputSize
    //                );

    //            case ImageResizeMode.Crop:
    //                // 等比缩放后裁剪，计算需要裁剪的部分
    //                float cropScale = Math.Max(targetWidth / srcWidth, targetHeight / srcHeight);
    //                widthRatio = heightRatio = cropScale;

    //                // 无填充（裁剪后的边缘将被丢弃）
    //                return new ImageAdjustmentParam(
    //                    new Pair<int, int>(0, 0),
    //                    new Pair<float, float>(widthRatio, heightRatio),
    //                    imgSize,
    //                    inputSize
    //                );

    //            default:
    //                throw new ArgumentOutOfRangeException(nameof(resizeMode));
    //        }
    //    }
    //}
    /// <summary>
    /// Image adjustment parameters (internal structure)
    /// 图像调整参数（内部结构体）
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains parameters for image padding, scaling ratios and size information.
    /// Used for maintaining consistent image transformations across operations.
    /// </para>
    /// <para>
    /// 包含图像的填充量、缩放比例和尺寸信息等参数。
    /// 用于在各种图像操作中保持一致的变换处理。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var param = ImageAdjustmentParam.CreateFromImageInfo(
    ///     new Size(800, 600),
    ///     new Size(400, 300),
    ///     ImageResizeMode.Pad);
    /// 
    /// Rect adjustedRect = param.AdjustRect(originalRect);
    /// </code>
    /// </example>
    /// </remarks>
    public struct ImageAdjustmentParam : IEquatable<ImageAdjustmentParam>
    {
        /// <summary>
        /// Padding amounts for each side of the image (typically width/height)
        /// 图像各边的填充量（通常为宽/高）
        /// </summary>
        /// <value>Pair where First=width padding, Second=height padding</value>
        public Pair<int, int> Padding { get; }

        /// <summary>
        /// Image scaling ratios (width/height)
        /// 图像的宽高比例调整系数
        /// </summary>
        /// <value>Pair where First=width ratio, Second=height ratio</value>
        public Pair<float, float> Ratio { get; }

        /// <summary>
        /// Original image size before adjustment
        /// 调整前的原始图像尺寸
        /// </summary>
        public Size RowImgSize { get; }

        /// <summary>
        /// Target image size after adjustment
        /// 调整后的目标图像尺寸
        /// </summary>
        public Size TargetImgSize { get; }

        /// <summary>
        /// Initializes a new instance of ImageAdjustmentParam
        /// 构造函数
        /// </summary>
        /// <param name="padding">
        /// Padding amounts (First=width direction, Second=height direction)
        /// 填充量（First=宽方向，Second=高方向）
        /// </param>
        /// <param name="ratio">
        /// Scaling ratios (First=width ratio, Second=height ratio)
        /// 比例系数（First=宽比例，Second=高比例）
        /// </param>
        /// <param name="rowImgSize">
        /// Original image size before adjustment
        /// 调整前的原始图像尺寸
        /// </param>
        /// <param name="targetImgSize">
        /// Target image size after adjustment
        /// 调整后的目标图像尺寸
        /// </param>
        public ImageAdjustmentParam(Pair<int, int> padding, Pair<float, float> ratio, Size rowImgSize, Size targetImgSize)
        {
            Padding = padding;
            Ratio = ratio;
            RowImgSize = rowImgSize;
            TargetImgSize = targetImgSize;
        }

        /// <summary>
        /// Deconstruct method (supports pattern matching and deconstruction)
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
        /// Returns a string that represents the current object
        /// 重写 ToString() 以便输出可读信息
        /// </summary>
        public override string ToString() =>
            $"Padding: [Width={Padding.First}, Height={Padding.Second}], Ratio: [Width={Ratio.First}, Height={Ratio.Second}], RowImgSize: [Width={RowImgSize.Width}, Height={RowImgSize.Height}], TargetImgSize: [Width={TargetImgSize.Width}, Height={TargetImgSize.Height}]";

        /// <summary>
        /// Adjusts rectangle coordinates using the current parameters
        /// 使用当前参数调整矩形坐标
        /// </summary>
        /// <param name="rectangle">Source rectangle 源矩形</param>
        /// <returns>Adjusted rectangle with integer coordinates 调整后的整数坐标矩形</returns>
        public Rect AdjustRect(RectF rectangle)
        {
            var x = (rectangle.X - Padding.First) / Ratio.First;
            var y = (rectangle.Y - Padding.Second) / Ratio.Second;
            var w = rectangle.Width / Ratio.First;
            var h = rectangle.Height / Ratio.Second;

            return new Rect((int)x, (int)y, (int)w, (int)h);
        }

        /// <summary>
        /// Adjusts rectangle coordinates using the current parameters (floating-point precision)
        /// 使用当前参数调整矩形坐标（浮点精度）
        /// </summary>
        /// <param name="rectangle">Source rectangle 源矩形</param>
        /// <returns>Adjusted rectangle with floating-point coordinates 调整后的浮点坐标矩形</returns>
        public RectF AdjustRectF(RectF rectangle)
        {
            var x = (rectangle.X - Padding.First) / Ratio.First;
            var y = (rectangle.Y - Padding.Second) / Ratio.Second;
            var w = rectangle.Width / Ratio.First;
            var h = rectangle.Height / Ratio.Second;

            return new RectF(x, y, w, h);
        }

        /// <summary>
        /// Adjusts point coordinates using the current parameters
        /// 使用当前参数调整点坐标
        /// </summary>
        /// <param name="point">Source point 源点</param>
        /// <returns>Adjusted point 调整后的点</returns>
        public Point AdjustPoint(Point point)
        {
            var x = (point.X - Padding.First) / Ratio.First;
            var y = (point.Y - Padding.Second) / Ratio.Second;

            return new Point((int)x, (int)y);
        }

        /// <summary>
        /// Creates adjustment parameters from image specifications
        /// 根据图像信息创建调整参数
        /// </summary>
        /// <param name="inputSize">
        /// Target size for adjustment
        /// 调整的目标尺寸
        /// </param>
        /// <param name="imgSize">
        /// Original image size
        /// 原始图像尺寸
        /// </param>
        /// <param name="resizeMode">
        /// Resize mode to use
        /// 使用的缩放模式
        /// </param>
        /// <returns>New ImageAdjustmentParam instance</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when image dimensions are not positive
        /// 当图像尺寸不是正数时抛出
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when resizeMode is invalid
        /// 当缩放模式无效时抛出
        /// </exception>
        public static ImageAdjustmentParam CreateFromImageInfo(Size inputSize, Size imgSize, ImageResizeMode resizeMode)
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
                case ImageResizeMode.Stretch:
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

                case ImageResizeMode.Pad:
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

                case ImageResizeMode.Max:
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

                case ImageResizeMode.Crop:
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
        

        #region Equality Implementation
        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type
        /// 指示当前对象是否等于同一类型的另一个对象
        /// </summary>
        public bool Equals(ImageAdjustmentParam other) =>
            Padding.Equals(other.Padding) && Ratio.Equals(other.Ratio);

        /// <summary>
        /// Determines whether the specified object is equal to the current object
        /// 确定指定对象是否等于当前对象
        /// </summary>
        public override bool Equals(object obj) =>
            obj is ImageAdjustmentParam other && Equals(other);

        /// <summary>
        /// Equality operator
        /// 相等运算符
        /// </summary>
        public static bool operator ==(ImageAdjustmentParam left, ImageAdjustmentParam right) =>
            left.Equals(right);

        /// <summary>
        /// Inequality operator
        /// 不等运算符
        /// </summary>
        public static bool operator !=(ImageAdjustmentParam left, ImageAdjustmentParam right) =>
            !(left == right);

        #endregion
    }

}
