using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class ImageDataB
    {
        // 图像基本信息
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Channels { get; private set; }


        // 原始像素数据
        private byte[] pixelData;

        // 构造函数
        public ImageDataB(int width, int height, int channels)
        {
            Width = width;
            Height = height;
            Channels = channels;

            pixelData = new byte[width * height * channels];
        }

        // 从字节数组构造
        public ImageDataB(byte[] data, int width, int height, int channels)
        {
            Width = width;
            Height = height;
            Channels = channels;
            pixelData = new byte[width * height * channels];
            Array.Copy(data, 0, pixelData, 0, data.Length);
        }

        // 获取原始数据
        public byte[] GetRawData() => pixelData;

        // 获取像素值
        public byte[] GetPixel(int x, int y)
        {
            var pixel = new byte[Channels];
            int offset = (y * Width + x) * Channels;
            Array.Copy(pixelData, offset, pixel, 0, Channels);
            return pixel;
        }

        // 设置像素值
        public void SetPixel(int x, int y, byte[] pixel)
        {
            int offset = (y * Width + x) * Channels;
            Array.Copy(pixel, 0, pixelData, offset, Channels);
        }



        // 克隆对象
        public ImageDataB Clone()
        {
            var clone = new ImageDataB(Width, Height, Channels);
            Array.Copy(pixelData, clone.pixelData, pixelData.Length);
            return clone;
        }
    }
}
