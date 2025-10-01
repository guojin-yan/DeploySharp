using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Specialized byte-based image container with additional image processing operations
    /// 专门针对字节数据的图像容器，提供额外的图像处理操作
    /// </summary>
    /// <remarks>
    /// Inherits from <see cref="ImageData{byte}"/> adding byte-specific functionality:
    /// - Direct byte array access
    /// - Common image processing operations
    /// - Format conversion helpers
    /// 
    /// 继承自<see cref="ImageData{byte}"/>并添加字节专用功能:
    /// - 直接字节数组访问
    /// - 常见图像处理操作
    /// - 格式转换助手
    /// </remarks>
    public class ImageDataB : ImageData<byte>
    {
        /// <summary>
        /// Initializes a new blank byte image with specified dimensions
        /// 使用指定尺寸初始化新的空白字节图像
        /// </summary>
        /// <param name="width">Image width in pixels (>0) 图像宽度(像素,>0)</param>
        /// <param name="height">Image height in pixels (>0) 图像高度(像素,>0)</param>
        /// <param name="channels">Number of color channels (1-4 typical) 颜色通道数(通常1-4)</param>
        /// <param name="format">Data layout format (default HWC) 数据布局格式(默认HWC)</param>
        public ImageDataB(int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(width, height, channels, format)
        {
            MyLogger.Log.Debug($"Created new byte image: {width}x{height}x{channels} {format}");
        }

        /// <summary>
        /// Initializes a new byte image from existing data
        /// 从现有数据初始化新的字节图像
        /// </summary>
        /// <param name="data">Byte array containing pixel data 包含像素数据的字节数组</param>
        /// <param name="width">Image width (>0) 图像宽度(>0)</param>
        /// <param name="height">Image height (>0) 图像高度(>0)</param>
        /// <param name="channels">Number of channels (>0) 通道数(>0)</param>
        /// <param name="format">Data layout format 数据布局格式</param>
        public ImageDataB(byte[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
            : base(data, width, height, channels, format)
        {
            MyLogger.Log.Debug($"Created byte image from data: {width}x{height}x{channels} {format}");
        }

        /// <summary>
        /// Gets direct access to the underlying byte array
        /// 直接访问底层字节数组
        /// </summary>
        /// <returns>Byte array containing all pixel data 包含所有像素数据的字节数组</returns>
        public byte[] GetRawByteData() => GetRawData();
    }
}
