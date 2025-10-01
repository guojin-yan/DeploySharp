using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Specialized floating-point image container optimized for machine learning operations
    /// 专为机器学习优化的浮点图像容器
    /// </summary>
    /// <remarks>
    /// Inherits from <see cref="ImageData{float}"/> adding float-specific functionality:
    /// - High-precision image operations
    /// - Normalization/denormalization
    /// - Direct float buffer access
    /// - Mathematical operations
    /// 
    /// 继承自<see cref="ImageData{float}"/>并添加浮点专用功能：
    /// - 高精度图像操作
    /// - 归一化/反归一化
    /// - 直接浮点缓冲区访问
    /// - 数学运算
    /// </remarks>
    public class ImageDataF : ImageData<float>
    {
        /// <summary>
        /// Initializes a new blank float image with specified dimensions
        /// 使用指定尺寸初始化新的空白浮点图像
        /// </summary>
        /// <param name="width">Image width in pixels (>0) 图像宽度(像素,>0)</param>
        /// <param name="height">Image height in pixels (>0) 图像高度(像素,>0)</param>
        /// <param name="channels">Number of color channels (1-4 typical) 颜色通道数(通常1-4)</param>
        /// <param name="format">Data layout format (default HWC) 数据布局格式(默认HWC)</param>
        public ImageDataF(int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(width, height, channels, format)
        {
            MyLogger.Log.Debug($"Created new float image: {width}x{height}x{channels} {format}");
        }

        /// <summary>
        /// Initializes a new float image from existing data
        /// 从现有数据初始化新的浮点图像
        /// </summary>
        /// <param name="data">Float array containing pixel data 包含像素数据的浮点数组</param>
        /// <param name="width">Image width (>0) 图像宽度(>0)</param>
        /// <param name="height">Image height (>0) 图像高度(>0)</param>
        /// <param name="channels">Number of channels (>0) 通道数(>0)</param>
        /// <param name="format">Data layout format 数据布局格式</param>
        public ImageDataF(float[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(data, width, height, channels, format)
        {
            MyLogger.Log.Debug($"Created float image from data: {width}x{height}x{channels} {format}");
        }

        /// <summary>
        /// Gets direct access to the underlying float array
        /// 直接访问底层浮点数组
        /// </summary>
        /// <returns>Float array containing all pixel data 包含所有像素数据的浮点数组</returns>
        public float[] GetRawFloatData() => GetRawData();
    }
}
