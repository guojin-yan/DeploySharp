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
    }
}
