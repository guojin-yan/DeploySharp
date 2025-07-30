using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;

namespace DeploySharp.Data
{
    public static class Visualize
    {
        /// <summary>
        /// Result drawing
        /// </summary>
        public static Image<Rgb24> DrawDetResult(BaseResult bresult, Image<Rgb24> image)
        {
            DetResult result = bresult as DetResult;

            // 创建图像副本以避免修改原始图像
            var output = image.Clone();

            for (int i = 0; i < result.count; i++)
            {
                // 绘制边界框
                var box = result.datas[i].box;
                var rect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                        box.BottomRight.X - box.TopLeft.X,
                                        box.BottomRight.Y - box.TopLeft.Y);

                output.Mutate(ctx =>
                {
                    // 红色边界框
                    ctx.Draw(Pens.Solid(Color.Red, 2), rect);

                    // 黄色标签背景
                    var labelRect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                 box.BottomRight.X - box.TopLeft.X, 30);
                    ctx.Fill(Color.Yellow, labelRect);

                    // 黑色文本
                    var text = $"{result.datas[i].lable}-{result.datas[i].score:0.00}";
                    ctx.DrawText(text, SystemFonts.CreateFont("Arial", 16), Color.Black,
                                new SixLabors.ImageSharp.PointF(box.TopLeft.X, box.TopLeft.Y + 5));
                });
            }

            return output;
        }

        /// <summary>
        /// Result drawing for OBB
        /// </summary>
        public static Image<Rgb24> DrawObbResult(BaseResult bresult, Image<Rgb24> image)
        {
            ObbResult result = bresult as ObbResult;
            var output = image.Clone();

            for (int i = 0; i < result.count; i++)
            {
                PointF[] points = result.datas[i].box.Points();

                output.Mutate(ctx =>
                {
                    // 绘制旋转框的四条边
                    for (int j = 0; j < 4; j++)
                    {
                        var start = CvDataExtensions.ToPointF( points[j]);
                        var end = CvDataExtensions.ToPointF(points[(j + 1) % 4]);
                        ctx.DrawLine(Color.FromRgb(255, 100, 200), 2, start, end);
                    }

                    // 文本标签
                    var text = $"{result.datas[i].lable}-{result.datas[i].score:0.00}";
                    ctx.DrawText(text, SystemFonts.CreateFont("Arial", 16), Color.Black, CvDataExtensions.ToPointF( points[0]));
                });
            }

            return output;
        }

        /// <summary>
        /// Result drawing for segmentation
        /// </summary>
        public static Image<Rgb24> DrawSegResult(BaseResult bresult, Image<Rgb24> image)
        {
            SegResult result = bresult as SegResult;
            var output = image.Clone();

            for (int i = 0; i < result.count; i++)
            {
                var box = result.datas[i].box;

                output.Mutate(ctx =>
                {
                    // 绘制边界框
                    var rect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                            box.BottomRight.X - box.TopLeft.X,
                                            box.BottomRight.Y - box.TopLeft.Y);
                    ctx.Draw(Pens.Solid(Color.Red, 2), rect);

                    // 绘制标签背景
                    var labelRect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                 box.BottomRight.X - box.TopLeft.X, 30);
                    ctx.Fill(Color.Yellow, labelRect);

                    // 绘制文本
                    var text = $"{result.datas[i].lable}-{result.datas[i].score:0.00}";
                    ctx.DrawText(text, SystemFonts.CreateFont("Arial", 16), Color.Black,
                                new SixLabors.ImageSharp.PointF(box.TopLeft.X, box.TopLeft.Y + 5));

      
                    // 50%透明度的蒙版覆盖
                    ctx.DrawImage(CvDataExtensions.ToImage(result.datas[i].mask), new SixLabors.ImageSharp.Point(0, 0), 0.5f);
     
                });
            }

            return output;
        }

        /// <summary>
        /// Key point result drawing
        /// </summary>
        public static Image<Rgb24> DrawPoses(BaseResult bresult, Image<Rgb24> img, float visual_thresh = 0.2f, bool with_box = true)
        {
            PoseResult pose = bresult as PoseResult;
            var output = img.Clone();

            // 关节点连线关系
            int[,] edges = new int[17, 2] { { 0, 1 }, { 0, 2}, {1, 3}, {2, 4}, {3, 5}, {4, 6}, {5, 7}, {6, 8},
                 {7, 9}, {8, 10}, {5, 11}, {6, 12}, {11, 13}, {12, 14},{13, 15 }, {14, 16 }, {11, 12 } };

            // 颜色库
            Color[] colors = new Color[18] {
                Color.FromRgb(255, 0, 0), Color.FromRgb(255, 85, 0), Color.FromRgb(255, 170, 0),
                Color.FromRgb(255, 255, 0), Color.FromRgb(170, 255, 0), Color.FromRgb(85, 255, 0),
                Color.FromRgb(0, 255, 0), Color.FromRgb(0, 255, 85), Color.FromRgb(0, 255, 170),
                Color.FromRgb(0, 255, 255), Color.FromRgb(0, 170, 255), Color.FromRgb(0, 85, 255),
                Color.FromRgb(0, 0, 255), Color.FromRgb(85, 0, 255), Color.FromRgb(170, 0, 255),
                Color.FromRgb(255, 0, 255), Color.FromRgb(255, 0, 170), Color.FromRgb(255, 0, 85)
            };

            string[] point_str = new string[] {
                "Nose", "Left Eye", "Right Eye", "Left Ear", "Right Ear",
                "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow",
                "Left Wrist", "Right Wrist", "Left Hip", "Right Hip",
                "Left Knee", "Right Knee", "Left Ankle", "Right Ankle"
            };

            for (int i = 0; i < pose.count; ++i)
            {
                output.Mutate(ctx =>
                {
                    // 绘制关节点
                    for (int p = 0; p < 17; p++)
                    {
                        if (pose.datas[i].pose_point.score[p] < visual_thresh) continue;

                        var point = pose.datas[i].pose_point.point[p];
                        ctx.Fill(colors[p], new EllipsePolygon(new SixLabors.ImageSharp.PointF(point.X, point.Y), 2));
                    }

                    if (with_box)
                    {
                        // 绘制边界框
                        var box = pose.datas[i].box;
                        var rect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                box.BottomRight.X - box.TopLeft.X,
                                                box.BottomRight.Y - box.TopLeft.Y);
                        ctx.Draw(Pens.Solid(Color.Red, 2), rect);

                        // 绘制标签背景
                        var labelRect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                     box.BottomRight.X - box.TopLeft.X, 30);
                        ctx.Fill(Color.Yellow, labelRect);

                        // 绘制文本
                        var text = $"{point_str[pose.datas[i].index]}-{pose.datas[i].score:0.00}";
                        ctx.DrawText(text, SystemFonts.CreateFont("Arial", 16), Color.Black,
                                    new SixLabors.ImageSharp.PointF(box.TopLeft.X, box.TopLeft.Y + 5));
                    }

                    // 绘制关节点连线
                    for (int p = 0; p < 17; p++)
                    {
                        if (pose.datas[i].pose_point.score[edges[p, 0]] < visual_thresh ||
                            pose.datas[i].pose_point.score[edges[p, 1]] < visual_thresh)
                        {
                            continue;
                        }

                        var start = pose.datas[i].pose_point.point[edges[p, 0]];
                        var end = pose.datas[i].pose_point.point[edges[p, 1]];

                        // 绘制椭圆形的连线(更粗的线)
                        var path = new PathBuilder().AddLine(start.X, start.Y, end.X, end.Y).Build();
                        ctx.Draw(colors[p], 3, path);
                    }
                });
            }

            return output;
        }

        // 辅助方法：将点数组转换为PointF数组
        private static PointF[] ToPointFArray(IEnumerable<Point> points)
        {
            var result = new List<PointF>();
            foreach (var point in points)
            {
                result.Add(new PointF(point.X, point.Y));
            }
            return result.ToArray();
        }
    }
}
