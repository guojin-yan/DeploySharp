using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    //public class ImageData<T> where T : unmanaged
    //{
    //    public enum DataFormat { CHW, HWC }

    //    private readonly int width;
    //    private readonly int height;
    //    private readonly int channels;
    //    private readonly DataFormat format;
    //    private readonly Memory<T> buffer;

    //    public int Width => width;
    //    public int Height => height;
    //    public int Channels => channels;
    //    public DataFormat Format => format;

    //    /// <summary>
    //    /// Initializes a new blank image with the specified dimensions and format.
    //    /// </summary>
    //    public ImageData(int width, int height, int channels, DataFormat format = DataFormat.HWC)
    //    {
    //        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    //        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
    //        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

    //        this.width = width;
    //        this.height = height;
    //        this.channels = channels;
    //        this.format = format;
    //        buffer = new Memory<T>(new T[width * height * channels]);
    //    }

    //    /// <summary>
    //    /// Initializes a new image from existing data.
    //    /// </summary>
    //    public ImageData(T[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
    //    {
    //        if (data == null) throw new ArgumentNullException(nameof(data));
    //        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    //        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
    //        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
    //        if (data.Length != width * height * channels)
    //            throw new ArgumentException("Data length doesn't match image dimensions");

    //        this.width = width;
    //        this.height = height;
    //        this.channels = channels;
    //        this.format = format;
    //        buffer = new Memory<T>(data);
    //    }

    //    /// <summary>
    //    /// Gets or sets a single pixel value for the specified channel (CHW format)
    //    /// </summary>
    //    public T this[int channel, int y, int x]
    //    {
    //        get
    //        {
    //            if (format != DataFormat.CHW)
    //                throw new InvalidOperationException("Channel access only supported in CHW format");
    //            return buffer.Span[GetCHWIndex(channel, y, x)];
    //        }
    //        set
    //        {
    //            if (format != DataFormat.CHW)
    //                throw new InvalidOperationException("Channel access only supported in CHW format");
    //            buffer.Span[GetCHWIndex(channel, y, x)] = value;
    //        }
    //    }

    //    public Span<T> GetChannelSpan(int channel)
    //    {
    //        if (format != DataFormat.CHW)
    //            throw new InvalidOperationException("Channel span only available in CHW format");

    //        int channelSize = width * height;
    //        return buffer.Span.Slice(channel * channelSize, channelSize);
    //    }

    //    public void SetPixel(int y, int x, ReadOnlySpan<T> pixel)
    //    {
    //        if (format != DataFormat.HWC)
    //            throw new InvalidOperationException("SetPixel only supported in HWC format");
    //        if (pixel.Length != channels)
    //            throw new ArgumentException($"Pixel must have {channels} components");

    //        int index = GetHWCIndex(y, x);
    //        pixel.CopyTo(buffer.Span.Slice(index, channels));
    //    }

    //    public T[] GetPixel(int y, int x)
    //    {
    //        if (format != DataFormat.HWC)
    //            throw new InvalidOperationException("GetPixel only supported in HWC format");

    //        T[] pixel = new T[channels];
    //        int index = GetHWCIndex(y, x);
    //        buffer.Span.Slice(index, channels).CopyTo(pixel);
    //        return pixel;
    //    }

    //    public void SetChannelPixel(int channel, int y, int x, T value)
    //    {
    //        if (format != DataFormat.CHW)
    //            throw new InvalidOperationException("Channel pixel access only supported in CHW format");
    //        buffer.Span[GetCHWIndex(channel, y, x)] = value;
    //    }

    //    public T GetChannelPixel(int channel, int y, int x)
    //    {
    //        if (format != DataFormat.CHW)
    //            throw new InvalidOperationException("Channel pixel access only supported in CHW format");
    //        return buffer.Span[GetCHWIndex(channel, y, x)];
    //    }

    //    public Span<T> GetRawSpan() => buffer.Span;
    //    public T[] GetRawData() => buffer.ToArray();
    //    public void Clear() => buffer.Span.Clear();

    //    public void Fill(T value) => buffer.Span.Fill(value);

    //    public ImageData<T> Clone()
    //    {
    //        T[] copy = new T[buffer.Length];
    //        buffer.CopyTo(copy);
    //        return new ImageData<T>(copy, width, height, channels, format);
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    private int GetHWCIndex(int y, int x)
    //    {
    //        if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
    //        if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
    //        return (y * width + x) * channels;
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    private int GetCHWIndex(int channel, int y, int x)
    //    {
    //        if (channel < 0 || channel >= channels) throw new ArgumentOutOfRangeException(nameof(channel));
    //        if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
    //        if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
    //        return channel * (width * height) + y * width + x;
    //    }

    //    // HWC格式下的通道像素访问辅助方法
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    private T GetChannelPixelHWC(int y, int x, int channel)
    //    {
    //        return buffer.Span[(y * width + x) * channels + channel];
    //    }
    //}


    /// <summary>
    /// Generic image container supporting both CHW (channel-height-width) and HWC (height-width-channel) layouts
    /// 支持CHW(通道-高度-宽度)和HWC(高度-宽度-通道)布局的通用图像容器
    /// </summary>
    /// <typeparam name="T">Pixel component type (must be unmanaged) 像素组件类型(必须是非托管类型)</typeparam>
    /// <remarks>
    /// This class provides:
    /// - Memory-efficient storage with Span support
    /// - Type-safe pixel/channel access
    /// - Conversion-free interoperability with native code
    /// - Support for common image formats (float, byte, etc.)
    /// 
    /// 本类提供：
    /// - 支持Span的高效内存存储
    /// - 类型安全的像素/通道访问
    /// - 与本地代码的无转换互操作
    /// - 支持常见图像格式(float、byte等)
    /// </remarks>
    public class ImageData<T> where T : unmanaged
    {
        /// <summary>
        /// Supported data layout formats
        /// 支持的数据布局格式
        /// </summary>
        public enum DataFormat
        {
            /// <summary>
            /// Channel-Height-Width (planar format)
            /// 通道-高度-宽度(平面格式)
            /// </summary>
            CHW,

            /// <summary>
            /// Height-Width-Channel (interleaved format)
            /// 高度-宽度-通道(交错格式)
            /// </summary>
            HWC
        }

        private readonly int width;
        private readonly int height;
        private readonly int channels;
        private readonly DataFormat format;
        private readonly Memory<T> buffer;

        /// <summary>
        /// Image width in pixels 图像宽度(像素)
        /// </summary>
        public int Width => width;

        /// <summary>
        /// Image height in pixels 图像高度(像素)
        /// </summary>
        public int Height => height;

        /// <summary>
        /// Number of color channels 颜色通道数
        /// </summary>
        public int Channels => channels;

        /// <summary>
        /// Data layout format 数据布局格式
        /// </summary>
        public DataFormat Format => format;

        /// <summary>
        /// Total number of elements in the buffer 缓冲区中的元素总数
        /// </summary>
        public int Length => buffer.Length;

        /// <summary>
        /// Initializes a new blank image with the specified dimensions and format
        /// 使用指定尺寸和格式初始化新的空白图像
        /// </summary>
        /// <param name="width">Image width (>0) 图像宽度(>0)</param>
        /// <param name="height">Image height (>0) 图像高度(>0)</param>
        /// <param name="channels">Number of channels (>0) 通道数(>0)</param>
        /// <param name="format">Data layout format 数据布局格式</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when any dimension is ≤0 当任何维度≤0时抛出
        /// </exception>
        public ImageData(int width, int height, int channels, DataFormat format = DataFormat.HWC)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be positive");

            this.width = width;
            this.height = height;
            this.channels = channels;
            this.format = format;
            buffer = new Memory<T>(new T[width * height * channels]);
        }

        /// <summary>
        /// Initializes a new image from existing data
        /// 从现有数据初始化新图像
        /// </summary>
        /// <param name="data">Pixel data array 像素数据数组</param>
        /// <param name="width">Image width (>0) 图像宽度(>0)</param>
        /// <param name="height">Image height (>0) 图像高度(>0)</param>
        /// <param name="channels">Number of channels (>0) 通道数(>0)</param>
        /// <param name="format">Data layout format 数据布局格式</param>
        /// <exception cref="ArgumentNullException">Thrown when data is null 当数据为null时抛出</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when data length doesn't match dimensions 当数据长度不匹配维度时抛出
        /// </exception>
        public ImageData(T[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (data.Length != width * height * channels)
                throw new ArgumentException($"Data length ({data.Length}) doesn't match image dimensions ({width}x{height}x{channels})");

            this.width = width;
            this.height = height;
            this.channels = channels;
            this.format = format;
            buffer = new Memory<T>(data);
        }

        /// <summary>
        /// Gets or sets a single channel pixel value (CHW format only)
        /// 获取或设置单个通道像素值(仅CHW格式)
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when accessed in HWC format 在HWC格式下访问时抛出
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when coordinates are out of bounds 当坐标越界时抛出
        /// </exception>
        public T this[int channel, int y, int x]
        {
            get
            {
                if (format != DataFormat.CHW)
                    throw new InvalidOperationException("Channel indexing only supported in CHW format");
                return buffer.Span[GetCHWIndex(channel, y, x)];
            }
            set
            {
                if (format != DataFormat.CHW)
                    throw new InvalidOperationException("Channel indexing only supported in CHW format");
                buffer.Span[GetCHWIndex(channel, y, x)] = value;
            }
        }

        /// <summary>
        /// Gets a span covering all pixels in specified channel (CHW format only)
        /// 获取覆盖指定通道所有像素的Span(仅CHW格式)
        /// </summary>
        /// <param name="channel">Channel index (0-based) 通道索引(从0开始)</param>
        /// <returns>Read-only span of channel data 通道数据的只读Span</returns>
        public ReadOnlySpan<T> GetChannelSpan(int channel)
        {
            if (format != DataFormat.CHW)
                throw new InvalidOperationException("Channel span only available in CHW format");

            int channelSize = width * height;
            return buffer.Span.Slice(channel * channelSize, channelSize);
        }

        /// <summary>
        /// Sets all components of a pixel (HWC format only)
        /// 设置像素的所有分量(仅HWC格式)
        /// </summary>
        /// <param name="y">Row index (0-based) 行索引(从0开始)</param>
        /// <param name="x">Column index (0-based) 列索引(从0开始)</param>
        /// <param name="pixel">Pixel components 像素分量</param>
        public void SetPixel(int y, int x, ReadOnlySpan<T> pixel)
        {
            if (format != DataFormat.HWC)
                throw new InvalidOperationException("SetPixel only supported in HWC format");
            if (pixel.Length != channels)
                throw new ArgumentException($"Pixel must have {channels} components");

            int index = GetHWCIndex(y, x);
            pixel.CopyTo(buffer.Span.Slice(index, channels));
        }

        /// <summary>
        /// Gets all components of a pixel (HWC format only)
        /// 获取像素的所有分量(仅HWC格式)
        /// </summary>
        /// <param name="y">Row index 行索引</param>
        /// <param name="x">Column index 列索引</param>
        /// <returns>Array containing pixel components 包含像素分量的数组</returns>
        public T[] GetPixel(int y, int x)
        {
            if (format != DataFormat.HWC)
                throw new InvalidOperationException("GetPixel only supported in HWC format");

            T[] pixel = new T[channels];
            int index = GetHWCIndex(y, x);
            buffer.Span.Slice(index, channels).CopyTo(pixel);
            return pixel;
        }

        /// <summary>
        /// Gets direct access to the underlying buffer span
        /// 直接访问底层缓冲区Span
        /// </summary>
        /// <returns>Span covering entire buffer 覆盖整个缓冲区的Span</returns>
        public Span<T> GetRawSpan() => buffer.Span;

        /// <summary>
        /// Gets a copy of the underlying data array
        /// 获取底层数据数组的副本
        public T[] GetRawData() => buffer.ToArray();

        /// <summary>
        /// Clears the image buffer (sets all elements to default(T))
        /// 清除图像缓冲区(将所有元素设置为default(T))
        /// </summary>
        public void Clear() => buffer.Span.Clear();

        /// <summary>
        /// Fills the image buffer with specified value
        /// 使用指定值填充图像缓冲区
        /// </summary>
        /// <param name="value">Fill value 填充值</param>
        public void Fill(T value) => buffer.Span.Fill(value);

        /// <summary>
        /// Creates a deep copy of the image
        /// 创建图像的深拷贝
        /// </summary>
        /// <returns>New independent image instance 新的独立图像实例</returns>
        public ImageData<T> Clone()
        {
            T[] copy = new T[buffer.Length];
            buffer.CopyTo(copy);
            return new ImageData<T>(copy, width, height, channels, format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetHWCIndex(int y, int x)
        {
            if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
            if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
            return (y * width + x) * channels;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCHWIndex(int channel, int y, int x)
        {
            if (channel < 0 || channel >= channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
            if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
            return channel * (width * height) + y * width + x;
        }

        /// <summary>
        /// Gets a channel pixel value (HWC format private access)
        /// 获取通道像素值(HWC格式内部访问)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T GetChannelPixelHWC(int y, int x, int channel)
        {
            return buffer.Span[(y * width + x) * channels + channel];
        }
    }
}
