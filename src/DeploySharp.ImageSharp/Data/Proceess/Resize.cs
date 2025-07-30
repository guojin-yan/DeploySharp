using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;

namespace DeploySharp.Data
{
    public static class Resize
    {
        public static Image<Rgb24> ImgType0(Image<Rgb24> img, string limit_type, int limit_side_len,
            out float ratio_h, out float ratio_w)
        {
            int w = img.Width;
            int h = img.Height;
            float ratio = 1.0f;

            // 计算缩放比例
            if (limit_type == "min")
            {
                int min_wh = Math.Min(h, w);
                if (min_wh < limit_side_len)
                {
                    ratio = (float)limit_side_len / (float)(h < w ? h : w);
                }
            }
            else
            {
                int max_wh = Math.Max(h, w);
                if (max_wh > limit_side_len)
                {
                    ratio = (float)limit_side_len / (float)(h > w ? h : w);
                }
            }

            // 计算新尺寸
            int resize_h = (int)(h * ratio);
            int resize_w = (int)(w * ratio);

            resize_h = Math.Max((int)(Math.Round(resize_h / 32.0f) * 32), 32);
            resize_w = Math.Max((int)(Math.Round(resize_w / 32.0f) * 32), 32);

            // 克隆图像以避免修改原始图像
            var result = img.Clone(ctx => ctx.Resize(resize_w, resize_h, KnownResamplers.Lanczos3));

            ratio_h = (float)resize_h / (float)h;
            ratio_w = (float)resize_w / (float)w;
            return result;
        }

        public static Image<Rgb24> ClsImg(Image<Rgb24> img, List<int> cls_image_shape)
        {
            int imgH = cls_image_shape[1];
            float ratio = (float)img.Width / (float)img.Height;

            int resize_w = (int)(Math.Ceiling(imgH * ratio)) > cls_image_shape[2]
                ? cls_image_shape[2]
                : (int)(Math.Ceiling(imgH * ratio));

            return img.Clone(ctx => ctx.Resize(resize_w, imgH, KnownResamplers.Bicubic));
        }

        public static Image<Rgb24> CrnnImg(Image<Rgb24> img, float wh_ratio, int[] rec_image_shape)
        {
            int imgH = rec_image_shape[1];
            int imgW = (int)(imgH * wh_ratio);

            float ratio = (float)img.Width / (float)img.Height;
            int resize_w = (int)(Math.Ceiling(imgH * ratio)) > imgW
                ? imgW
                : (int)(Math.Ceiling(imgH * ratio));

            // 先调整尺寸
            var result = img.Clone(ctx => ctx.Resize(resize_w, imgH, KnownResamplers.Bicubic));

            // 创建目标大小的白色背景图像
            var finalImage = new Image<Rgb24>(imgW, imgH, Color.FromRgb(127, 127, 127));

            // 将调整大小的图像复制到中央
            int xOffset = (imgW - resize_w) / 2;
            finalImage.Mutate(ctx => ctx.DrawImage(result, new SixLabors.ImageSharp.Point(xOffset, 0), 1f));

            return finalImage;
        }

        public static Image<Rgb24> LetterboxImg(Image<Rgb24> img, int length, out float scales)
        {
            Image<Rgb24> result;

            if (img.Width > img.Height)  // 宽>高
            {
                scales = (float)img.Width / length;
                int newHeight = (int)(img.Height / scales);
                result = img.Clone(ctx => ctx.Resize(length, newHeight));
            }
            else  // 高≥宽
            {
                scales = (float)img.Height / length;
                int newWidth = (int)(img.Width / scales);
                result = img.Clone(ctx => ctx.Resize(newWidth, length));
            }

            // 创建方形的灰色背景图像
            var finalImage = new Image<Rgb24>(length, length, Color.FromRgb(0, 0, 0));

            // 计算居中位置
            int x = (length - result.Width) / 2;
            int y = (length - result.Height) / 2;

            // 将缩放后的图像绘制到中央
            finalImage.Mutate(ctx => ctx.DrawImage(result, new SixLabors.ImageSharp.Point(x, y), 1f));

            return finalImage;
        }
    }
}
