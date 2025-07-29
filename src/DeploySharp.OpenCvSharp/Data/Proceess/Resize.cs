using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public static class Resize
    {
        public static Mat ImgType0(Mat img, string limit_type, int limit_side_len,
            out float ratio_h, out float ratio_w)
        {
            int w = img.Cols;
            int h = img.Rows;
            float ratio = 1.0f;
            if (limit_type == "min")
            {
                int min_wh = Math.Min(h, w);
                if (min_wh < limit_side_len)
                {
                    if (h < w)
                    {
                        ratio = (float)limit_side_len / (float)h;
                    }
                    else
                    {
                        ratio = (float)limit_side_len / (float)w;
                    }
                }
            }
            else
            {
                int max_wh = Math.Max(h, w);
                if (max_wh > limit_side_len)
                {
                    if (h > w)
                    {
                        ratio = (float)(limit_side_len) / (float)(h);
                    }
                    else
                    {
                        ratio = (float)(limit_side_len) / (float)(w);
                    }
                }
            }

            int resize_h = (int)((float)(h) * ratio);
            int resize_w = (int)((float)(w) * ratio);

            //int resize_h = 960;
            //int resize_w = 960;

            resize_h = Math.Max((int)(Math.Round((float)(resize_h) / 32.0f) * 32), 32);
            resize_w = Math.Max((int)(Math.Round((float)(resize_w) / 32.0f) * 32), 32);

            Mat resize_img = new Mat();
            Cv2.Resize(img, resize_img, new OpenCvSharp.Size(resize_w, resize_h));
            ratio_h = (float)(resize_h) / (float)(h);
            ratio_w = (float)(resize_w) / (float)(w);
            return resize_img;
        }

        public static Mat ClsImg(Mat img, List<int> cls_image_shape)
        {
            int imgC, imgH, imgW;
            imgC = cls_image_shape[0];
            imgH = cls_image_shape[1];
            imgW = cls_image_shape[2];

            float ratio = (float)img.Cols / (float)img.Rows;
            int resize_w, resize_h;
            if (Math.Ceiling(imgH * ratio) > imgW)
                resize_w = imgW;
            else
                resize_w = (int)(Math.Ceiling(imgH * ratio));
            Mat resize_img = new Mat();
            Cv2.Resize(img, resize_img, new OpenCvSharp.Size(resize_w, imgH), 0.0f, 0.0f, InterpolationFlags.Linear);
            return resize_img;
        }


        public static Mat CrnnImg(Mat img, float wh_ratio, int[] rec_image_shape)
        {
            int imgC, imgH, imgW;
            imgC = rec_image_shape[0];
            imgH = rec_image_shape[1];
            imgW = rec_image_shape[2];

            imgW = (int)(imgH * wh_ratio);

            float ratio = (float)(img.Cols) / (float)(img.Rows);
            int resize_w, resize_h;

            if (Math.Ceiling(imgH * ratio) > imgW)
                resize_w = imgW;
            else
                resize_w = (int)(Math.Ceiling(imgH * ratio));
            Mat resize_img = new Mat();
            Cv2.Resize(img, resize_img, new OpenCvSharp.Size(resize_w, imgH), 0.0f, 0.0f, InterpolationFlags.Linear);
            Cv2.CopyMakeBorder(resize_img, resize_img, 0, 0, 0, (int)(imgW - resize_img.Cols), BorderTypes.Constant, new Scalar(127, 127, 127));
            return resize_img;
        }

        public static Mat LetterboxImg(Mat img, int length, out float scales) 
        {
            Mat mat = new Mat();

            // 根据图像长宽比计算缩放比例
            OpenCvSharp.Rect roi = new OpenCvSharp.Rect();

            if (img.Cols > img.Rows)  // 宽>高的情况
            {

                scales = (float)img.Cols / (float)length;
                Cv2.Resize(img, mat, new OpenCvSharp.Size(length, img.Rows / scales));
                roi = new OpenCvSharp.Rect(0, 0, length, (int)(img.Rows / scales));
            }
            else  // 高≥宽的情况
            {
                scales = (float)img.Rows / (float)length;
                Cv2.Resize(img, mat, new OpenCvSharp.Size(img.Cols / scales, length));
                roi = new OpenCvSharp.Rect(0, 0, (int)(img.Cols / scales), length);
            }

            // 创建640x640的黑色背景Mat
            Mat mat1 = Mat.Zeros(length, length, MatType.CV_8UC3);
            // 将缩放后的图像拷贝到中央
            mat.CopyTo(new Mat(mat1, roi));

            return mat1;
        }

    }
}
