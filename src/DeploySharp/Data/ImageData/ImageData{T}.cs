using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class ImageData<T> where T : unmanaged
    {
        public enum DataFormat { CHW, HWC }

        private readonly int _width;
        private readonly int _height;
        private readonly int _channels;
        private readonly DataFormat _format;
        private readonly Memory<T> _buffer;

        public int Width => _width;
        public int Height => _height;
        public int Channels => _channels;
        public DataFormat Format => _format;

        /// <summary>
        /// Initializes a new blank image with the specified dimensions and format.
        /// </summary>
        public ImageData(int width, int height, int channels, DataFormat format = DataFormat.HWC)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

            _width = width;
            _height = height;
            _channels = channels;
            _format = format;
            _buffer = new Memory<T>(new T[width * height * channels]);
        }

        /// <summary>
        /// Initializes a new image from existing data.
        /// </summary>
        public ImageData(T[] data, int width, int height, int channels, DataFormat format = DataFormat.HWC)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (data.Length != width * height * channels)
                throw new ArgumentException("Data length doesn't match image dimensions");

            _width = width;
            _height = height;
            _channels = channels;
            _format = format;
            _buffer = new Memory<T>(data);
        }

        /// <summary>
        /// Gets or sets a single pixel value for the specified channel (CHW format)
        /// </summary>
        public T this[int channel, int y, int x]
        {
            get
            {
                if (_format != DataFormat.CHW)
                    throw new InvalidOperationException("Channel access only supported in CHW format");
                return _buffer.Span[GetCHWIndex(channel, y, x)];
            }
            set
            {
                if (_format != DataFormat.CHW)
                    throw new InvalidOperationException("Channel access only supported in CHW format");
                _buffer.Span[GetCHWIndex(channel, y, x)] = value;
            }
        }

        public Span<T> GetChannelSpan(int channel)
        {
            if (_format != DataFormat.CHW)
                throw new InvalidOperationException("Channel span only available in CHW format");

            int channelSize = _width * _height;
            return _buffer.Span.Slice(channel * channelSize, channelSize);
        }

        public void SetPixel(int y, int x, ReadOnlySpan<T> pixel)
        {
            if (_format != DataFormat.HWC)
                throw new InvalidOperationException("SetPixel only supported in HWC format");
            if (pixel.Length != _channels)
                throw new ArgumentException($"Pixel must have {_channels} components");

            int index = GetHWCIndex(y, x);
            pixel.CopyTo(_buffer.Span.Slice(index, _channels));
        }

        public T[] GetPixel(int y, int x)
        {
            if (_format != DataFormat.HWC)
                throw new InvalidOperationException("GetPixel only supported in HWC format");

            T[] pixel = new T[_channels];
            int index = GetHWCIndex(y, x);
            _buffer.Span.Slice(index, _channels).CopyTo(pixel);
            return pixel;
        }

        public void SetChannelPixel(int channel, int y, int x, T value)
        {
            if (_format != DataFormat.CHW)
                throw new InvalidOperationException("Channel pixel access only supported in CHW format");
            _buffer.Span[GetCHWIndex(channel, y, x)] = value;
        }

        public T GetChannelPixel(int channel, int y, int x)
        {
            if (_format != DataFormat.CHW)
                throw new InvalidOperationException("Channel pixel access only supported in CHW format");
            return _buffer.Span[GetCHWIndex(channel, y, x)];
        }

        public Span<T> GetRawSpan() => _buffer.Span;
        public T[] GetRawData() => _buffer.ToArray();
        public void Clear() => _buffer.Span.Clear();

        public void Fill(T value) => _buffer.Span.Fill(value);

        public ImageData<T> Clone()
        {
            T[] copy = new T[_buffer.Length];
            _buffer.CopyTo(copy);
            return new ImageData<T>(copy, _width, _height, _channels, _format);
        }


        //    /// <summary>
        //    /// Resizes the image using bilinear interpolation
        //    /// </summary>
        //    public ImageData<T> Resize(int newWidth, int newHeight)
        //    {
        //        if (newWidth <= 0 || newHeight <= 0)
        //            throw new ArgumentOutOfRangeException("Invalid target dimensions");

        //        var result = new ImageData<T>(newWidth, newHeight, _channels, _format);

        //        if (_format == DataFormat.HWC)
        //        {
        //            // HWC格式处理
        //            for (int y = 0; y < newHeight; y++)
        //            {
        //                float srcY = (float)y * (_height - 1) / (newHeight - 1);
        //                int y0 = (int)Math.Min(srcY, _height - 2);
        //                float yLerp = srcY - y0;

        //                for (int x = 0; x < newWidth; x++)
        //                {
        //                    float srcX = (float)x * (_width - 1) / (newWidth - 1);
        //                    int x0 = (int)Math.Min(srcX, _width - 2);
        //                    float xLerp = srcX - x0;

        //                    // 对每个通道插值
        //                    Span<T> pixel = stackalloc T[_channels];
        //                    for (int c = 0; c < _channels; c++)
        //                    {
        //                        // 获取四个邻近点
        //                        var q11 = GetChannelPixelHWC(y0, x0, c);
        //                        var q21 = GetChannelPixelHWC(y0, x0 + 1, c);
        //                        var q12 = GetChannelPixelHWC(y0 + 1, x0, c);
        //                        var q22 = GetChannelPixelHWC(y0 + 1, x0 + 1, c);

        //                        // 双线性插值（需动态调用Lerp）
        //                        pixel[c] = NumericHelper.Lerp(
        //                            NumericHelper.Lerp(q11, q21, xLerp),
        //                            NumericHelper.Lerp(q12, q22, xLerp),
        //                            yLerp
        //                        );
        //                    }
        //                    result.SetPixel(y, x, pixel);
        //                }
        //            }
        //        }
        //        else
        //        {
        //            // CHW格式处理（逐通道独立缩放）
        //            for (int c = 0; c < _channels; c++)
        //            {
        //                var channelSpan = GetChannelSpan(c);
        //                var resultChannel = result.GetChannelSpan(c);

        //                for (int y = 0; y < newHeight; y++)
        //                {
        //                    float srcY = (float)y * (_height - 1) / (newHeight - 1);
        //                    int y0 = (int)Math.Min(srcY, _height - 2);
        //                    float yLerp = srcY - y0;

        //                    for (int x = 0; x < newWidth; x++)
        //                    {
        //                        float srcX = (float)x * (_width - 1) / (newWidth - 1);
        //                        int x0 = (int)Math.Min(srcX, _width - 2);
        //                        float xLerp = srcX - x0;

        //                        // 当前通道的四个邻近点
        //                        int i00 = y0 * _width + x0;
        //                        int i10 = y0 * _width + x0 + 1;
        //                        int i01 = (y0 + 1) * _width + x0;
        //                        int i11 = (y0 + 1) * _width + x0 + 1;

        //                        // 插值计算
        //                        resultChannel[y * newWidth + x] = NumericHelper.Lerp(
        //                            NumericHelper.Lerp(channelSpan[i00], channelSpan[i10], xLerp),
        //                            NumericHelper.Lerp(channelSpan[i01], channelSpan[i11], xLerp),
        //                            yLerp
        //                        );
        //                    }
        //                }
        //            }
        //        }

        //        return result;
        //    }


        //    /// <summary>
        //    /// Applies thresholding to the image (supports per-channel thresholds)
        //    /// </summary>
        //    public void Threshold(ReadOnlySpan<T> thresholds, T maxValue = default, bool isBinary = true)
        //    {
        //        if (thresholds.Length != _channels)
        //            throw new ArgumentException($"Threshold count must match channels ({_channels})");

        //        if (_format == DataFormat.HWC)
        //        {
        //            for (int y = 0; y < _height; y++)
        //            {
        //                for (int x = 0; x < _width; x++)
        //                {
        //                    int baseIdx = GetHWCIndex(y, x);
        //                    for (int c = 0; c < _channels; c++)
        //                    {
        //                        ref T pixel = ref _buffer.Span[baseIdx + c];
        //                        if (NumericHelper.GreaterThan(pixel, thresholds[c]))
        //                            pixel = isBinary ? maxValue : pixel;
        //                        else if (isBinary)
        //                            pixel = default;
        //                    }
        //                }
        //            }
        //        }
        //        else
        //        {
        //            for (int c = 0; c < _channels; c++)
        //            {
        //                var channelSpan = GetChannelSpan(c);
        //                T threshold = thresholds[c];
        //                for (int i = 0; i < channelSpan.Length; i++)
        //                {
        //                    if (NumericHelper.GreaterThan(channelSpan[i], threshold))
        //                        channelSpan[i] = isBinary ? maxValue : channelSpan[i];
        //                    else if (isBinary)
        //                        channelSpan[i] = default;
        //                }
        //            }
        //        }
        //    }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetHWCIndex(int y, int x)
        {
            if (y < 0 || y >= _height) throw new ArgumentOutOfRangeException(nameof(y));
            if (x < 0 || x >= _width) throw new ArgumentOutOfRangeException(nameof(x));
            return (y * _width + x) * _channels;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCHWIndex(int channel, int y, int x)
        {
            if (channel < 0 || channel >= _channels) throw new ArgumentOutOfRangeException(nameof(channel));
            if (y < 0 || y >= _height) throw new ArgumentOutOfRangeException(nameof(y));
            if (x < 0 || x >= _width) throw new ArgumentOutOfRangeException(nameof(x));
            return channel * (_width * _height) + y * _width + x;
        }

        // HWC格式下的通道像素访问辅助方法
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T GetChannelPixelHWC(int y, int x, int channel)
        {
            return _buffer.Span[(y * _width + x) * _channels + channel];
        }
    }


    //internal static class NumericHelper
    //{
    //    // 泛型线性插值
    //    public static T Lerp<T>(T a, T b, float t) where T : unmanaged
    //    {
    //        dynamic da = a, db = b;
    //        return da + (db - da) * t;
    //    }

    //    // 泛型比较
    //    public static bool GreaterThan<T>(T a, T b) where T : unmanaged
    //    {
    //        dynamic da = a, db = b;
    //        return da > db;
    //    }
    //}

}
