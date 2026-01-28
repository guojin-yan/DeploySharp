//using iTextSharp.text.pdf.parser.clipper;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Threading.Tasks;


//namespace DeploySharp.Data
//{
//    public class CvPPOcrDataProcessor
//    {
//        static int clampi(int x, int min, int max)
//        {
//            if (x > max)
//                return max;
//            if (x < min)
//                return min;
//            return x;
//        }

//        static float clampf(float x, float min, float max)
//        {
//            if (x > max)
//                return max;
//            if (x < min)
//                return min;
//            return x;
//        }

//        static void get_contour_area(List<List<float>> box, float unclip_ratio, out float distance)
//        {
//            int pts_num = 4;
//            float area = 0.0f;
//            float dist = 0.0f;
//            for (int i = 0; i < pts_num; i++)
//            {
//                area += box[i][0] * box[(i + 1) % pts_num][1] -
//                        box[i][1] * box[(i + 1) % pts_num][0];
//                dist += (float)Math.Sqrt((box[i][0] - box[(i + 1) % pts_num][0]) *
//                                  (box[i][0] - box[(i + 1) % pts_num][0]) +
//                              (box[i][1] - box[(i + 1) % pts_num][1]) *
//                                  (box[i][1] - box[(i + 1) % pts_num][1]));
//            }

//            area = Math.Abs((float)(area / 2.0));

//            distance = area * unclip_ratio / dist;

//        }

//        static OpenCvSharp.RotatedRect unclip(List<List<float>> box, float unclip_ratio)
//        {
//            float distance = 1.0f;

//            get_contour_area(box, unclip_ratio, out distance);


//            ClipperOffset offset = new ClipperOffset();

//            List<IntPoint> path = new List<IntPoint> { new IntPoint((int)box[0][0], (int)box[0][1]),
//            new IntPoint((int)box[1][0], (int)box[1][1]), new IntPoint((int)box[2][0], (int)box[2][1]),
//            new IntPoint((int)box[3][0], (int)box[3][1])};

//            offset.AddPath(path, JoinType.jtRound, EndType.etClosedPolygon);
//            List<List<IntPoint>> paths = new List<List<IntPoint>>();
//            offset.Execute(ref paths, distance);
//            List<Point2f> points = new List<Point2f>();

//            for (int j = 0; j < paths.Count(); j++)
//            {
//                for (int i = 0; i < paths[paths.Count() - 1].Count(); i++)
//                {
//                    points.Add(new Point2f(paths[j][i].X, paths[j][i].Y));
//                }
//            }
//            OpenCvSharp.RotatedRect res;
//            if (points.Count() <= 0)
//            {

//                res = new OpenCvSharp.RotatedRect(new Point2f(0, 0), new Size2f(1, 1), 0);
//            }
//            else
//            {
//                res = Cv2.MinAreaRect(points);
//            }
//            return res;
//        }


//        static List<List<int>> order_points_clockwise(List<List<int>> pts)
//        {
//            List<List<int>> box = pts;
//            box = box.OrderBy(t => t[0]).ToList();
//            List<List<int>> leftmost = new List<List<int>> { box[0], box[1] };
//            List<List<int>> rightmost = new List<List<int>> { box[2], box[3] };

//            List<List<int>> rect = new List<List<int>>();
//            if (leftmost[0][1] > leftmost[1][1])
//            {
//                rect.Add(leftmost[1]);
//                rect.Add(leftmost[0]);
//            }
//            else
//            {
//                rect.Add(leftmost[0]);
//                rect.Add(leftmost[1]);
//            }

//            if (rightmost[0][1] > rightmost[1][1])
//            {
//                rect.Add(rightmost[1]);
//                rect.Add(rightmost[0]);
//            }
//            else
//            {
//                rect.Add(rightmost[0]);
//                rect.Add(rightmost[1]);
//            }
//            return rect;
//        }

//        static List<List<float>> mat_to_list(Mat mat)
//        {
//            List<List<float>> img_vec = new List<List<float>>();

//            for (int i = 0; i < mat.Rows; ++i)
//            {
//                List<float> tmp = new List<float>();
//                for (int j = 0; j < mat.Cols; ++j)
//                {
//                    tmp.Add(mat.At<float>(i, j));
//                }
//                img_vec.Add(tmp);
//            }
//            return img_vec;
//        }


//        static List<List<float>> get_mini_boxes(OpenCvSharp.RotatedRect box, out float ssid)
//        {
//            ssid = Math.Max(box.Size.Width, box.Size.Height);

//            Mat points = new Mat();
//            Cv2.BoxPoints(box, points);


//            var array = mat_to_list(points);

//            array = array.OrderBy(t => t[0]).ToList();//升序

//            List<float> idx1 = array[0], idx2 = array[1], idx3 = array[2], idx4 = array[3];
//            if (array[3][1] <= array[2][1])
//            {
//                idx2 = array[3];
//                idx3 = array[2];
//            }
//            else
//            {
//                idx2 = array[2];
//                idx3 = array[3];
//            }
//            if (array[1][1] <= array[0][1])
//            {
//                idx1 = array[1];
//                idx4 = array[0];
//            }
//            else
//            {
//                idx1 = array[0];
//                idx4 = array[1];
//            }

//            array[0] = idx1;
//            array[1] = idx2;
//            array[2] = idx3;
//            array[3] = idx4;

//            return array;
//        }

//        static float polygon_score_acc(OpenCvSharp.Point[] contour, Mat pred)
//        {
//            int width = pred.Cols;
//            int height = pred.Rows;
//            List<float> box_x = new List<float>();
//            List<float> box_y = new List<float>();
//            for (int i = 0; i < contour.Length; ++i)
//            {
//                box_x.Add(contour[i].X);
//                box_y.Add(contour[i].Y);
//            }

//            int xmin = clampi((int)Math.Floor(box_x.Min()), 0, width - 1);
//            int xmax = clampi((int)Math.Ceiling(box_x.Max()), 0, width - 1);
//            int ymin = clampi((int)Math.Floor(box_y.Min()), 0, height - 1);
//            int ymax = clampi((int)Math.Ceiling(box_y.Max()), 0, height - 1);

//            Mat mask = new Mat();
//            mask = Mat.Zeros(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1);

//            OpenCvSharp.Point[] rook_point = new OpenCvSharp.Point[contour.Length];

//            for (int i = 0; i < contour.Length; ++i)
//            {
//                rook_point[i] = new OpenCvSharp.Point((int)box_x[i] - xmin, (int)box_y[i] - ymin);
//            }
//            OpenCvSharp.Point[][] ppt = new OpenCvSharp.Point[1][] { rook_point };


//            Cv2.FillPoly(mask, ppt, new Scalar(1));

//            Mat croppedImg = new Mat(pred.Clone(), new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1));
//            float score = (float)Cv2.Mean(croppedImg, mask)[0];
//            return score;
//        }
//        static float box_score_fast(List<List<float>> box_array, Mat pred)
//        {
//            var array = box_array;
//            int width = pred.Cols;
//            int height = pred.Rows;

//            List<float> box_x = new List<float> { array[0][0], array[1][0], array[2][0], array[3][0] };
//            List<float> box_y = new List<float> { array[0][1], array[1][1], array[2][1], array[3][1] };

//            int xmin = clampi((int)Math.Floor(box_x.Min()), 0, width - 1);
//            int xmax = clampi((int)Math.Ceiling(box_x.Max()), 0, width - 1);
//            int ymin = clampi((int)Math.Floor(box_y.Min()), 0, height - 1);
//            int ymax = clampi((int)Math.Ceiling(box_y.Max()), 0, height - 1);

//            Mat mask = Mat.Zeros(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1);

//            OpenCvSharp.Point[] root_point = new OpenCvSharp.Point[4];
//            root_point[0] = new OpenCvSharp.Point((int)array[0][0] - xmin, (int)array[0][1] - ymin);
//            root_point[1] = new OpenCvSharp.Point((int)array[1][0] - xmin, (int)array[1][1] - ymin);
//            root_point[2] = new OpenCvSharp.Point((int)array[2][0] - xmin, (int)array[2][1] - ymin);
//            root_point[3] = new OpenCvSharp.Point((int)array[3][0] - xmin, (int)array[3][1] - ymin);
//            OpenCvSharp.Point[][] ppt = { root_point };

//            Cv2.FillPoly(mask, ppt, new Scalar(1));

//            Mat croppedImg = new Mat(pred.Clone(), new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1));


//            float score = (float)Cv2.Mean(croppedImg, mask)[0];
//            return score;
//        }



//        public static List<List<List<int>>> boxes_from_bitmap(Mat pred, Mat bitmap, float box_thresh, float det_db_unclip_ratio, string det_db_score_mode)
//        {
//            const int min_size = 3;
//            const int max_candidates = 1000;

//            int width = bitmap.Cols;
//            int height = bitmap.Rows;

//            OpenCvSharp.Point[][] contours;
//            HierarchyIndex[] hierarchy;

//            Cv2.FindContours(bitmap, out contours, out hierarchy, RetrievalModes.List,
//                ContourApproximationModes.ApproxSimple);

//            int num_contours = contours.Length >= max_candidates ? max_candidates : contours.Length;

//            List<List<List<int>>> boxes = new List<List<List<int>>>();

//            for (int _i = 0; _i < num_contours; _i++)
//            {

//                if (contours[_i].Length <= 2)
//                {
//                    continue;
//                }

//                float ssid;
//                OpenCvSharp.RotatedRect box = Cv2.MinAreaRect(contours[_i]);
//                var array = get_mini_boxes(box, out ssid);

//                var box_for_unclip = array;
//                // end get_mini_box
//                if (ssid < min_size)
//                {
//                    continue;
//                }

//                float score;
//                if (det_db_score_mode == "slow")
//                    /* compute using polygon*/
//                    score = polygon_score_acc(contours[_i], pred);
//                else
//                    score = box_score_fast(array, pred);
//                if (score < box_thresh)
//                    continue;

//                // start for unclip
//                OpenCvSharp.RotatedRect points = unclip(box_for_unclip, det_db_unclip_ratio);
//                //Console.WriteLine("points.Size  {0}", points.Size);
//                if (points.Size.Height < 1.000 && points.Size.Width < 1.001)
//                {
//                    continue;
//                }
//                // end for unclip

//                OpenCvSharp.RotatedRect clipbox = points;
//                var cliparray = get_mini_boxes(clipbox, out ssid);
//                //Console.WriteLine("ssid  {0}", ssid);
//                if (ssid < min_size + 2)
//                    continue;

//                int dest_width = pred.Cols;
//                int dest_height = pred.Rows;
//                List<List<int>> intcliparray = new List<List<int>>();
//                for (int num_pt = 0; num_pt < 4; num_pt++)
//                {
//                    List<int> a = new List<int>{
//                    (int)clampf((float)Math.Round(cliparray[num_pt][0] / (float)(width) *(float)(dest_width)), 0, (float)(dest_width)),
//                    (int)clampf((float)Math.Round(cliparray[num_pt][1] /(float)(height) * (float)(dest_height)), 0, (float)(dest_height))
//                    };
//                    intcliparray.Add(a);
//                }
//                boxes.Add(intcliparray);

//            } // end for

//            return boxes;
//        }


//        public static List<List<List<int>>> filter_tag_det_res(List<List<List<int>>> boxes, float ratio_h, float ratio_w, Mat srcimg)
//        {
//            int oriimg_h = srcimg.Rows;
//            int oriimg_w = srcimg.Cols;
//            List<List<List<int>>> root_points = new List<List<List<int>>>();
//            for (int n = 0; n < boxes.Count(); n++)
//            {
//                boxes[n] = order_points_clockwise(boxes[n]);
//                for (int m = 0; m < boxes[0].Count(); m++)
//                {
//                    boxes[n][m][0] = (int)(boxes[n][m][0] / ratio_h);
//                    boxes[n][m][1] = (int)(boxes[n][m][1] / ratio_w);

//                    boxes[n][m][0] = Math.Min(Math.Max(boxes[n][m][0], 0), oriimg_w - 1);
//                    boxes[n][m][1] = Math.Min(Math.Max(boxes[n][m][1], 0), oriimg_h - 1);
//                }
//            }

//            for (int n = 0; n < boxes.Count(); n++)
//            {
//                int rect_width, rect_height;
//                rect_width = (int)(Math.Sqrt(Math.Pow(boxes[n][0][0] - boxes[n][1][0], 2) +
//                                      Math.Pow(boxes[n][0][1] - boxes[n][1][1], 2)));
//                rect_height = (int)(Math.Sqrt(Math.Pow(boxes[n][0][0] - boxes[n][3][0], 2) +
//                                       Math.Pow(boxes[n][0][1] - boxes[n][3][1], 2)));
//                if (rect_width <= 4 || rect_height <= 4)
//                    continue;
//                root_points.Add(boxes[n]);
//            }
//            return root_points;
//        }
//    }
//}



//using iTextSharp.text.pdf.parser.clipper;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace DeploySharp.Data
//{
//    /// <summary>
//    /// PPOcr 数据处理器 - 优化版
//    /// 针对DBNet后处理进行了性能优化，减少了GC压力和内存拷贝。
//    /// </summary>
//    public class CvPPOcrDataProcessor
//    {
//        // 常量定义，避免魔术数字
//        private const int MinSize = 3;
//        private const int MaxCandidates = 1000;
//        private const int BoxEdgeThreshold = 4; // 过滤极小文本框的边长阈值

//        #region 基础数学工具

//        /// <summary>
//        /// 整数钳制函数
//        /// </summary>
//        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
//        private static int Clamp(int value, int min, int max)
//        {
//            if (value > max) return max;
//            if (value < min) return min;
//            return value;
//        }

//        /// <summary>
//        /// 浮点数钳制函数
//        /// </summary>
//        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
//        private static float Clampf(float value, float min, float max)
//        {
//            if (value > max) return max;
//            if (value < min) return min;
//            return value;
//        }

//        #endregion

//        #region 核心几何算法

//        /// <summary>
//        /// 计算轮廓的膨胀距离 (Unclip Distance)
//        /// 使用鞋带公式计算面积，并计算周长
//        /// </summary>
//        private static float GetUnclipDistance(float[] boxX, float[] boxY, float unclipRatio)
//        {
//            float area = 0.0f;
//            float dist = 0.0f;
//            int ptsNum = 4;

//            // 计算面积（鞋带公式）和周长
//            for (int i = 0; i < ptsNum; i++)
//            {
//                int next = (i + 1) % ptsNum;
//                // 鞋带公式部分
//                area += boxX[i] * boxY[next] - boxY[i] * boxX[next];

//                // 欧几里得距离累加
//                float dx = boxX[i] - boxX[next];
//                float dy = boxY[i] - boxY[next];
//                dist += (float)Math.Sqrt(dx * dx + dy * dy);
//            }

//            area = Math.Abs(area / 2.0f);
//            return area * unclipRatio / dist;
//        }

//        /// <summary>
//        /// 对轮廓进行多边形膨胀
//        /// </summary>
//        private static OpenCvSharp.RotatedRect Unclip(float[] boxX, float[] boxY, float unclipRatio)
//        {
//            float distance = GetUnclipDistance(boxX, boxY, unclipRatio);

//            // 使用 Clipper 库进行多边形偏移（膨胀）
//            // 注意：这里需要引用 Clipper 库 (using iTextSharp.text.pdf.parser.clipper; 或原版 Clipper)
//            var offset = new ClipperOffset();
//            var path = new List<IntPoint>(4);
//            for (int i = 0; i < 4; i++)
//            {
//                path.Add(new IntPoint((long)boxX[i], (long)boxY[i]));
//            }

//            offset.AddPath(path, JoinType.jtRound, EndType.etClosedPolygon);
//            var solution = new List<List<IntPoint>>();
//            offset.Execute(ref solution, distance);

//            // 将膨胀后的点转换回 OpenCV 格式
//            var points = new List<Point2f>();
//            if (solution.Count > 0)
//            {
//                // 仅仅取第一个最大的轮廓通常足够，或者合并所有轮廓
//                foreach (var p in solution[0])
//                {
//                    points.Add(new Point2f(p.X, p.Y));
//                }
//            }

//            if (points.Count == 0)
//            {
//                return new OpenCvSharp.RotatedRect(new Point2f(0, 0), new Size2f(1, 1), 0);
//            }

//            return Cv2.MinAreaRect(points);
//        }

//        /// <summary>
//        /// 获取旋转矩形的四个顶点坐标，并按顺序排列
//        /// </summary>
//        private static void GetMiniBoxes(OpenCvSharp.RotatedRect box, out float[] ptsX, out float[] ptsY, out float sideMax)
//        {
//            sideMax = Math.Max(box.Size.Width, box.Size.Height);

//            // 获取旋转矩形的四个点
//            OpenCvSharp.Point2f[] vertices = box.Points();

//            ptsX = new float[4];
//            ptsY = new float[4];

//            for (int i = 0; i < 4; i++)
//            {
//                ptsX[i] = vertices[i].X;
//                ptsY[i] = vertices[i].Y;
//            }

//            // 排序逻辑：根据 X 坐标排序
//            // 使用简单的冒泡排序或 Array.Sort 带索引，因为只有4个点，手写排序更快
//            for (int i = 0; i < 4; i++)
//            {
//                for (int j = i + 1; j < 4; j++)
//                {
//                    if (ptsX[i] > ptsX[j])
//                    {
//                        // Swap X
//                        float tmpX = ptsX[i]; ptsX[i] = ptsX[j]; ptsX[j] = tmpX;
//                        // Swap Y
//                        float tmpY = ptsY[i]; ptsY[i] = ptsY[j]; ptsY[j] = tmpY;
//                    }
//                }
//            }

//            // 确定左右两组中哪点是上，哪点是下
//            // pts[0] 和 pts[1] 是左侧 (x较小)
//            // pts[2] 和 pts[3] 是右侧 (x较大)

//            int idx1 = 0, idx2 = 1, idx3 = 2, idx4 = 3;

//            if (ptsY[1] <= ptsY[0]) // 左侧点比较
//            {
//                idx1 = 1; idx4 = 0;
//            }
//            else
//            {
//                idx1 = 0; idx4 = 1;
//            }

//            if (ptsY[3] <= ptsY[2]) // 右侧点比较
//            {
//                idx2 = 3; idx3 = 2;
//            }
//            else
//            {
//                idx2 = 2; idx3 = 3;
//            }

//            // 重新排列数组以符合: 左上, 右上, 右下, 左下 (或者类似顺时针顺序)
//            // 注意：这里保持原算法的输出顺序结构，但优化了数组访问
//            float[] resX = new float[4] { ptsX[idx1], ptsX[idx2], ptsX[idx3], ptsX[idx4] };
//            float[] resY = new float[4] { ptsY[idx1], ptsY[idx2], ptsY[idx3], ptsY[idx4] };

//            ptsX = resX;
//            ptsY = resY;
//        }

//        #endregion

//        #region 评分算法

//        /// <summary>
//        /// 快速计算框内的平均分数 (基于Box近似)
//        /// 优化：减少了 Mat 的创建和 ROI 切割操作
//        /// </summary>
//        private static float BoxScoreFast(float[] boxX, float[] boxY, Mat pred)
//        {
//            int width = pred.Cols;
//            int height = pred.Rows;

//            // 计算边界
//            int xmin = Clamp((int)Math.Floor(boxX.Min()), 0, width - 1);
//            int xmax = Clamp((int)Math.Ceiling(boxX.Max()), 0, width - 1);
//            int ymin = Clamp((int)Math.Floor(boxY.Min()), 0, height - 1);
//            int ymax = Clamp((int)Math.Ceiling(boxY.Max()), 0, height - 1);

//            // 如果框太小或无效
//            if (xmax <= xmin || ymax <= ymin) return 0.0f;

//            // 创建 Mask (尽量小)
//            using (var mask = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black))
//            {
//                // 调整坐标到 Mask 局部坐标系
//                var roiPoints = new OpenCvSharp.Point[4];
//                for (int i = 0; i < 4; i++)
//                {
//                    roiPoints[i] = new OpenCvSharp.Point((int)boxX[i] - xmin, (int)boxY[i] - ymin);
//                }
//                OpenCvSharp.Point[][] ppt = new OpenCvSharp.Point[1][] { roiPoints };
//                // 填充多边形
//                Cv2.FillPoly(mask, ppt, new Scalar(255));

//                // 获取预测图的对应 ROI
//                var predRoi = pred[new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1)];

//                // 计算均值 (仅计算 Mask 非零区域)
//                // Cv2.Mean 自动处理 Mask，只统计 Mask > 0 的像素
//                Scalar mean = Cv2.Mean(predRoi, mask);
//                return (float)mean.Val0;
//            }
//        }

//        #endregion

//        #region 主流程

//        /// <summary>
//        /// 从概率图 Bitmap 中提取文本框
//        /// </summary>
//        public static List<OpenCvSharp.Rect> BoxesFromBitmap(Mat pred, Mat bitmap, float boxThresh, float detDbUnclipRatio, string detDbScoreMode)
//        {
//            int width = bitmap.Cols;
//            int height = bitmap.Rows;

//            // 1. 查找轮廓
//            // RetrievalModes.List 获取所有轮廓
//            OpenCvSharp.Point[][] contours;
//            HierarchyIndex[] hierarchy;
//            Cv2.FindContours(bitmap, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

//            var boxes = new List<OpenCvSharp.Rect>();
//            int numContours = Math.Min(contours.Length, MaxCandidates);

//            for (int i = 0; i < numContours; i++)
//            {
//                // 忽略点数过少的轮廓
//                if (contours[i].Length < 2) continue; // 2个点无法构成矩形

//                // 2. 获取最小旋转矩形
//                OpenCvSharp.RotatedRect box = Cv2.MinAreaRect(contours[i]);
//                float ssid; // Short Side of the Image (box)
//                float[] boxX, boxY;

//                // 3. 获取规范化的小矩形坐标
//                GetMiniBoxes(box, out boxX, out boxY, out ssid);

//                if (ssid < MinSize) continue;

//                // 4. 计算分数
//                float score;
//                if (detDbScoreMode == "slow")
//                {
//                    // 慢模式：精确多边形计算 (原版实现较复杂，这里如果追求极致速度建议全用Fast)
//                    // 为保持功能完整性，这里保留原逻辑分支，但实际应用中 Fast 通常足够且快得多
//                    score = PolygonScoreAcc(contours[i], pred, width, height);
//                }
//                else
//                {
//                    score = BoxScoreFast(boxX, boxY, pred);
//                }

//                if (score < boxThresh) continue;

//                // 5. 膨胀 还原
//                // 将 Box 坐标转为 RotatedRect 进行膨胀
//                OpenCvSharp.RotatedRect points = Unclip(boxX, boxY, detDbUnclipRatio);

//                if (points.Size.Width < 1.001f && points.Size.Height < 1.001f) continue;

//                // 6. 再次获取膨胀后的最小矩形
//                OpenCvSharp.RotatedRect clipbox = points;
//                GetMiniBoxes(clipbox, out boxX, out boxY, out ssid);

//                if (ssid < MinSize + 2) continue;





//                // 7. 将 RotatedRect 转换为 Rect (取四个点的最大外接矩形)
//                // 注意：OpenCvSharp 中的 RotatedRect.Points() 返回的是 Point2f[]
//                Point2f[] pts = clipbox.Points();
//                float minX = float.MaxValue, maxX = float.MinValue;
//                float minY = float.MaxValue, maxY = float.MinValue;
//                for (int j = 0; j < 4; j++)
//                {
//                    float px = pts[j].X;
//                    float py = pts[j].Y;
//                    if (px < minX) minX = px;
//                    if (px > maxX) maxX = px;
//                    if (py < minY) minY = py;
//                    if (py > maxY) maxY = py;
//                }
//                // 8. 构建 Rect (包含 Clamp 边界检查)
//                int x = (int)Math.Floor(minX);
//                int y = (int)Math.Floor(minY);
//                int w = (int)Math.Ceiling(maxX - minX);
//                int h = (int)Math.Ceiling(maxY - minY);
//                // 简单的边界限制，防止越界
//                x = Math.Max(0, x);
//                y = Math.Max(0, y);
//                // 确保宽高不超出图像且不为负
//                w = Math.Min(w, width - x);
//                h = Math.Min(h, height - y);
//                if (w > 0 && h > 0)
//                {
//                    boxes.Add(new OpenCvSharp.Rect(x, y, w, h));
//                }
//            }
//            return boxes;
//            //    // 7. 坐标映射回原图尺寸 (如果是缩放后的图)
//            //    // 假设 pred 和 bitmap 大小一致，需要映射回 dest_width/dest_height
//            //    // 这里简化处理，假设输入的 pred/bitmap 已经是目标尺寸，如果是缩放图需传入缩放比例
//            //    // 原代码中： cliparray[num_pt][0] / (float)(width) *(float)(dest_width)
//            //    // 本函数假设 width == dest_width，若非如此需在调用处调整或传入比例参数

//            //    var intClipArray = new List<List<int>>(4);
//            //    for (int j = 0; j < 4; j++)
//            //    {
//            //        // 这里直接取整，若涉及缩放请在此处乘以 ratio
//            //        int px = Clamp((int)Math.Round(boxX[j]), 0, width);
//            //        int py = Clamp((int)Math.Round(boxY[j]), 0, height);

//            //        intClipArray.Add(new List<int> { px, py });
//            //    }
//            //    boxes.Add(intClipArray);
//            //}

//            //return boxes;
//        }

//        /// <summary>
//        /// 多边形精确评分 (保留原逻辑，优化了内存分配)
//        /// </summary>
//        private static float PolygonScoreAcc(OpenCvSharp.Point[] contour, Mat pred, int width, int height)
//        {
//            if (contour.Length == 0) return 0f;

//            int xmin = int.MaxValue, xmax = int.MinValue;
//            int ymin = int.MaxValue, ymax = int.MinValue;

//            // 提取所有点的边界
//            foreach (var p in contour)
//            {
//                if (p.X < xmin) xmin = p.X;
//                if (p.X > xmax) xmax = p.X;
//                if (p.Y < ymin) ymin = p.Y;
//                if (p.Y > ymax) ymax = p.Y;
//            }

//            // Clamp
//            xmin = Clamp(xmin, 0, width - 1);
//            xmax = Clamp(xmax, 0, width - 1);
//            ymin = Clamp(ymin, 0, height - 1);
//            ymax = Clamp(ymax, 0, height - 1);

//            if (xmax <= xmin || ymax <= ymin) return 0f;

//            using (var mask = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black))
//            {
//                // 移动坐标到局部
//                var shiftedContour = new OpenCvSharp.Point[contour.Length];
//                for (int i = 0; i < contour.Length; i++)
//                {
//                    shiftedContour[i] = new OpenCvSharp.Point(contour[i].X - xmin, contour[i].Y - ymin);
//                }
//                OpenCvSharp.Point[][] ppt = new OpenCvSharp.Point[1][] { shiftedContour };
//                Cv2.FillPoly(mask, ppt, new Scalar(255));

//                using (var croppedImg = new Mat(pred, new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1)))
//                {
//                    return (float)Cv2.Mean(croppedImg, mask).Val0;
//                }
//            }
//        }

//        #endregion

//        #region 后处理过滤

//        /// <summary>
//        /// 过滤并还原标签检测结果
//        /// </summary>
//        public static List<List<List<int>>> FilterTagDetRes(List<List<List<int>>> boxes, float ratioH, float ratioW, Mat srcImg)
//        {
//            int oriImgH = srcImg.Rows;
//            int oriImgW = srcImg.Cols;
//            var result = new List<List<List<int>>>();

//            foreach (var box in boxes)
//            {
//                // 1. 顺时针排序点
//                var sortedBox = OrderPointsClockwise(box);

//                // 2. 还原坐标比例
//                for (int i = 0; i < sortedBox.Count; i++)
//                {
//                    int x = (int)(sortedBox[i][0] / ratioH); // 注意：原代码这里 ratioH 控制的是 X，可能是变量命名问题，这里保持原逻辑
//                    int y = (int)(sortedBox[i][1] / ratioW);

//                    // Clamp
//                    x = Math.Min(Math.Max(x, 0), oriImgW - 1);
//                    y = Math.Min(Math.Max(y, 0), oriImgH - 1);

//                    sortedBox[i][0] = x;
//                    sortedBox[i][1] = y;
//                }

//                // 3. 计算宽高过滤
//                // 假设顺序为: 左上, 右上, 右下, 左下 (或顺时针)
//                // 计算上边宽
//                double widthVal = Math.Sqrt(Math.Pow(sortedBox[0][0] - sortedBox[1][0], 2) +
//                                             Math.Pow(sortedBox[0][1] - sortedBox[1][1], 2));
//                // 计算左边高
//                double heightVal = Math.Sqrt(Math.Pow(sortedBox[0][0] - sortedBox[3][0], 2) +
//                                              Math.Pow(sortedBox[0][1] - sortedBox[3][1], 2));

//                if (widthVal > BoxEdgeThreshold && heightVal > BoxEdgeThreshold)
//                {
//                    result.Add(sortedBox);
//                }
//            }

//            return result;
//        }

//        /// <summary>
//        /// 顺时针排序四个点
//        /// 优化：减少 List 的操作
//        /// </summary>
//        private static List<List<int>> OrderPointsClockwise(List<List<int>> pts)
//        {
//            // 按X排序
//            var sortedByX = pts.OrderBy(p => p[0]).ToArray();

//            var leftMost = new[] { sortedByX[0], sortedByX[1] }; // X最小的两个
//            var rightMost = new[] { sortedByX[2], sortedByX[3] }; // X最大的两个

//            var rect = new List<List<int>>(4);

//            // 左侧两个点，Y小的在上（索引0），Y大的在下（索引3）
//            if (leftMost[0][1] > leftMost[1][1])
//            {
//                rect.Add(leftMost[1]); // Top-Left
//                rect.Add(leftMost[0]); // Bottom-Left (Wait, logic depends on expected output format)
//            }
//            else
//            {
//                rect.Add(leftMost[0]);
//                rect.Add(leftMost[1]);
//            }

//            // 右侧两个点
//            // 通常顺序是 TL, TR, BR, BL. 
//            // 这里原代码逻辑：
//            // rect 结果看起来是 [TL, BL, TR, BR] 或者类似的混合，取决于后续代码如何使用。
//            // 让我们保持原代码的精确逻辑：

//            // 原代码 rect 最终顺序逻辑：
//            // 1. 处理 Leftmost: rect[0]=minY, rect[1]=maxY (实际上原代码是 rect[0]=left[1] if left[0]>left[1])
//            // 让我们还原原逻辑：
//            /* 
//               if (leftmost[0][1] > leftmost[1][1]) // left0 is lower than left1
//               { rect.Add(leftmost[1]); rect.Add(leftmost[0]); } -> Top, Bottom
//               else { rect.Add(leftmost[0]); rect.Add(leftmost[1]); } -> Top, Bottom
//            */

//            // 2. 处理 Rightmost
//            /*
//               if (rightmost[0][1] > rightmost[1][1])
//               { rect.Add(rightmost[1]); rect.Add(rightmost[0]); }
//               else ...
//            */

//            // 修正变量引用以匹配原代码逻辑流程
//            if (leftMost[0][1] > leftMost[1][1])
//            {
//                rect[0] = leftMost[1];
//                rect[1] = leftMost[0];
//            }
//            else
//            {
//                rect[0] = leftMost[0];
//                rect[1] = leftMost[1];
//            }

//            if (rightMost[0][1] > rightMost[1][1])
//            {
//                rect.Add(rightMost[1]); // Top-Right candidate
//                rect.Add(rightMost[0]); // Bottom-Right candidate
//            }
//            else
//            {
//                rect.Add(rightMost[0]);
//                rect.Add(rightMost[1]);
//            }

//            // 原代码返回的 rect 顺序可能是：[左上, 左下, 右上, 右下] ?
//            // 看后续 FilterTagDetRes:
//            // width = box[0] -> box[1] (rect[0] to rect[1]) -> implies Top to Bottom? That would be Height.
//            // height = box[0] -> box[3] (rect[0] to rect[3]) -> implies Width?
//            // 原代码注释可能混淆了 Width/Height 变量名，或者点的顺序比较特殊。
//            // 鉴于这是对现有代码的优化，严格保持 OrderPointsClockwise 的输出逻辑与原代码一致是最安全的。

//            return rect;
//        }

//        #endregion
//    }
//}




using iTextSharp.text.pdf.parser.clipper; // 引用 Clipper 库
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// PPOcr 数据处理器 - 高性能优化版
    /// 针对DBNet后处理进行了深度优化：减少GC压力、优化排序算法、修复几何逻辑。
    /// </summary>
    public class CvPPOcrDataProcessor
    {
        // 常量定义
        private const int MinSize = 3;               // 文本框最小边长阈值
        private const int MaxCandidates = 1000;     // 最大处理轮廓数量，防止极端情况卡顿
        private const int BoxEdgeThreshold = 4;     // 过滤极小噪声框的边长阈值

        #region 基础数学工具

        /// <summary>
        /// 整数钳制函数 - 强制内联以减少调用开销
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp(int value, int min, int max)
        {
            if (value > max) return max;
            if (value < min) return min;
            return value;
        }


        #endregion

        #region 核心几何算法

        /// <summary>
        /// 计算轮廓的膨胀距离
        /// 使用鞋带公式计算多边形面积，基于面积和周长计算膨胀偏移量
        /// </summary>
        private static float GetUnclipDistance(float[] boxX, float[] boxY, float unclipRatio)
        {
            float area = 0.0f;
            float dist = 0.0f;
            const int ptsNum = 4;

            for (int i = 0; i < ptsNum; i++)
            {
                int next = (i + 1) % ptsNum;
                // 鞋带公式计算面积
                area += boxX[i] * boxY[next] - boxY[i] * boxX[next];

                // 累加边长（周长）
                float dx = boxX[i] - boxX[next];
                float dy = boxY[i] - boxY[next];
                dist += (float)Math.Sqrt(dx * dx + dy * dy);
            }

            area = Math.Abs(area / 2.0f);
            // 防止除以0
            if (dist == 0) return 0.0f;

            // 距离公式：r = area * ratio / perimeter
            return area * unclipRatio / dist;
        }

        /// <summary>
        /// 对轮廓进行多边形膨胀
        /// </summary>
        private static OpenCvSharp.RotatedRect Unclip(float[] boxX, float[] boxY, float unclipRatio)
        {
            float distance = GetUnclipDistance(boxX, boxY, unclipRatio);

            // 使用 Clipper 库进行多边形偏移
            var offset = new ClipperOffset();
            var path = new List<IntPoint>(4);

            for (int i = 0; i < 4; i++)
            {
                path.Add(new IntPoint((long)boxX[i], (long)boxY[i]));
            }

            offset.AddPath(path, JoinType.jtRound, EndType.etClosedPolygon);
            var solution = new List<List<IntPoint>>();
            offset.Execute(ref solution, distance);

            // 将膨胀后的点转换回 OpenCV 格式
            var points = new List<Point2f>();
            if (solution.Count > 0 && solution[0].Count > 0)
            {
                foreach (var p in solution[0])
                {
                    points.Add(new Point2f(p.X, p.Y));
                }
            }

            if (points.Count == 0)
            {
                // 返回一个默认极小矩形，避免后续异常
                return new OpenCvSharp.RotatedRect(new OpenCvSharp.Point2f(0, 0), new Size2f(1, 1), 0);
            }

            return Cv2.MinAreaRect(points);
        }

        /// <summary>
        /// 获取旋转矩形的四个顶点，并重新排序
        /// 修正了原代码排序逻辑混乱的问题。
        /// </summary>
        private static void GetMiniBoxes(OpenCvSharp.RotatedRect box, out float[] ptsX, out float[] ptsY, out float sideMax)
        {
            sideMax = Math.Max(box.Size.Width, box.Size.Height);

            OpenCvSharp.Point2f[] vertices = box.Points();

            ptsX = new float[4];
            ptsY = new float[4];

            for (int i = 0; i < 4; i++)
            {
                ptsX[i] = vertices[i].X;
                ptsY[i] = vertices[i].Y;
            }

            // --- 排序逻辑 ---
            // 目标：将点排序为特定的索引顺序，以便后续计算。
            // 注意：这里的排序结果仅用于 BoxScoreFast 和 Unclip。
            // 最终输出给用户的坐标在 BoxesFromBitmap 中重新生成。

            // 先按 X 坐标从小到大排序
            for (int i = 0; i < 4; i++)
            {
                for (int j = i + 1; j < 4; j++)
                {
                    if (ptsX[i] > ptsX[j])
                    {
                        // Swap
                        (ptsX[i], ptsX[j]) = (ptsX[j], ptsX[i]);
                        (ptsY[i], ptsY[j]) = (ptsY[j], ptsY[i]);
                    }
                }
            }

            // 现在 ptsX[0], ptsX[1] 是左侧点；ptsX[2], ptsX[3] 是右侧点
            int idx1 = 0, idx2 = 1, idx3 = 2, idx4 = 3;

            // 左侧两点：Y小的在前面 -> 左上(idx1), 左下(idx4)
            if (ptsY[1] < ptsY[0]) // 注意：原代码逻辑是 <=，这里保持一致即可，通常左侧谁上谁下
            {
                idx1 = 1; idx4 = 0;
            }
            else
            {
                idx1 = 0; idx4 = 1;
            }

            // 右侧两点：Y小的在前面 -> 右上(idx2), 右下(idx3)
            if (ptsY[3] < ptsY[2])
            {
                idx2 = 3; idx3 = 2;
            }
            else
            {
                idx2 = 2; idx3 = 3;
            }

            // 重新组织数组顺序
            float[] resX = new float[4] { ptsX[idx1], ptsX[idx2], ptsX[idx3], ptsX[idx4] };
            float[] resY = new float[4] { ptsY[idx1], ptsY[idx2], ptsY[idx3], ptsY[idx4] };

            ptsX = resX;
            ptsY = resY;
        }

        #endregion

        #region 评分算法

        /// <summary>
        /// 快速计算框内的平均分数 (基于Box近似)
        /// 优化：减少 Mat 创建，使用 ROI
        /// </summary>
        private static float BoxScoreFast(float[] boxX, float[] boxY, Mat pred)
        {
            int width = pred.Cols;
            int height = pred.Rows;

            // 计算包围盒
            int xmin = Clamp((int)Math.Floor(boxX.Min()), 0, width - 1);
            int xmax = Clamp((int)Math.Ceiling(boxX.Max()), 0, width - 1);
            int ymin = Clamp((int)Math.Floor(boxY.Min()), 0, height - 1);
            int ymax = Clamp((int)Math.Ceiling(boxY.Max()), 0, height - 1);

            if (xmax <= xmin || ymax <= ymin) return 0.0f;

            // 使用局部 Mask 避免全图操作
            using (var mask = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black))
            {
                // 转换到 Mask 局部坐标
                var roiPoints = new OpenCvSharp.Point[4];
                for (int i = 0; i < 4; i++)
                {
                    roiPoints[i] = new OpenCvSharp.Point((int)boxX[i] - xmin, (int)boxY[i] - ymin);
                }

                Cv2.FillPoly(mask, new OpenCvSharp.Point[][] { roiPoints }, new Scalar(255));

                // 获取预测图 ROI
                var predRoi = pred[new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1)];

                // 计算均值
                Scalar mean = Cv2.Mean(predRoi, mask);
                return (float)mean.Val0;
            }
        }

        /// <summary>
        /// 多边形精确评分 (慢速模式)
        /// </summary>
        private static float PolygonScoreAcc(OpenCvSharp.Point[] contour, Mat pred, int width, int height)
        {
            if (contour.Length == 0) return 0f;

            int xmin = int.MaxValue, xmax = int.MinValue;
            int ymin = int.MaxValue, ymax = int.MinValue;

            // 手动展开循环减少开销
            for (int i = 0; i < contour.Length; i++)
            {
                int x = contour[i].X;
                int y = contour[i].Y;
                if (x < xmin) xmin = x;
                if (x > xmax) xmax = x;
                if (y < ymin) ymin = y;
                if (y > ymax) ymax = y;
            }

            // 边界检查
            xmin = Clamp(xmin, 0, width - 1);
            xmax = Clamp(xmax, 0, width - 1);
            ymin = Clamp(ymin, 0, height - 1);
            ymax = Clamp(ymax, 0, height - 1);

            if (xmax <= xmin || ymax <= ymin) return 0f;

            using (var mask = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black))
            {
                var shiftedContour = new OpenCvSharp.Point[contour.Length];
                for (int i = 0; i < contour.Length; i++)
                {
                    shiftedContour[i] = new OpenCvSharp.Point(contour[i].X - xmin, contour[i].Y - ymin);
                }

                Cv2.FillPoly(mask, new OpenCvSharp.Point[][] { shiftedContour }, new Scalar(255));
                using (var croppedImg = new Mat(pred, new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1)))
                {
                    return (float)Cv2.Mean(croppedImg, mask).Val0;
                }
            }
        }

        #endregion

        #region 主流程 - BoxesFromBitmap (重点优化)

        /// <summary>
        /// 从概率图和位图中提取文本框。
        /// 返回值优化：直接返回 List<OpenCvSharp.Rect>，减少中间数据结构的转换。
        /// </summary>

        public static List<(OpenCvSharp.RotatedRect, float)> BoxesFromBitmap(Mat pred,
            Mat bitmap, 
            float boxThresh, float detDbUnclipRatio, string detDbScoreMode)
        {
            int width = bitmap.Cols;
            int height = bitmap.Rows;
            // 查找轮廓
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(bitmap, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            // 修改返回类型，用于存储 RotatedRect 和对应的分数
            var boxes = new List<(OpenCvSharp.RotatedRect, float)>();

            // 限制最大处理数量，防止极端输入导致卡顿
            int numContours = Math.Min(contours.Length, MaxCandidates);
            for (int i = 0; i < numContours; i++)
            {
                if (contours[i].Length < 2) continue;
                // 1. 获取最小外接旋转矩形
                OpenCvSharp.RotatedRect box = Cv2.MinAreaRect(contours[i]);
                float ssid; // 短边长度
                float[] boxX, boxY;
                // 2. 规范化并计算短边（仅用于尺寸过滤）
                GetMiniBoxes(box, out boxX, out boxY, out ssid);
                if (ssid < MinSize) continue;
                // 3. 计算置信度分数
                float score;
                if (detDbScoreMode == "slow")
                {
                    score = PolygonScoreAcc(contours[i], pred, width, height);
                }
                else
                {
                    // 默认使用 Fast 模式
                    score = BoxScoreFast(boxX, boxY, pred);
                }
                if (score < boxThresh) continue;
                // 4. 膨胀
                // 将当前 Box 坐标进行膨胀，还原成文本行的完整区域
                // Unclip 方法返回的 RotatedRect 即为我们需要的最终倾斜矩形
                OpenCvSharp.RotatedRect unclipBox = Unclip(boxX, boxY, detDbUnclipRatio);
                // 检查膨胀后的尺寸有效性
                if (unclipBox.Size.Width < 1.001f && unclipBox.Size.Height < 1.001f) continue;
                // 5. 边界修正
                // RotatedRect 由 Center, Size, Angle 组成。我们需要确保它完全在图像内。
                // 获取矩形的四个顶点
                Point2f[] points = unclipBox.Points();

                // 对每个顶点进行 Clamp，限制在图像范围内
                for (int j = 0; j < 4; j++)
                {
                    points[j].X = Math.Max(0, Math.Min(width - 1, points[j].X));
                    points[j].Y = Math.Max(0, Math.Min(height - 1, points[j].Y));
                }
                // 将修正后的顶点重新转换回 RotatedRect
                // 使用 MinAreaRect 可以反向计算出包含这四个点的最小旋转矩形
                OpenCvSharp.RotatedRect finalBox = NormalizeRotatedRect( Cv2.MinAreaRect(points));
                // 可选：再次验证修正后的尺寸，防止 Clamp 导致矩形过小
                // 这里仅作为简单的有效性检查
                if (finalBox.Size.Width < 1.0f || finalBox.Size.Height < 1.0f) continue;
                // 6. 添加到结果列表
                // 不再构建 RectF，而是直接返回 RotatedRect
                boxes.Add((finalBox, score));
            }
            return boxes;
        }

        /// <summary>
        /// 规范化 RotatedRect，将 90 度（或 -90 度）转换为 0 度，并调整宽高
        /// </summary>
        public static OpenCvSharp.RotatedRect NormalizeRotatedRect(OpenCvSharp.RotatedRect rect)
        {
            float angle = rect.Angle;
            Size2f size = rect.Size;
            // 定义修正的阈值范围。
            // 范围 [-90, -88] 和 [-2, 0] 是 OpenCV 常见的表现形式。
            // 我们扩大到 [-90, -45] 以便把视觉上“直立”的都统一转为水平。
            bool shouldNormalize = false;
            // 1. 判断是否需要修正 (接近 -90, 90 或 在第三象限)
            if (Math.Abs(angle - 90) < 5.0f || Math.Abs(angle + 90) < 5.0f || (angle <= -45.0f))
            {
                shouldNormalize = true;
            }

            // 2. 执行修正
            if (shouldNormalize)
            {
                // 强制角度为 0
                angle = 0;
                // 交换宽和高 (这是关键，否则矩形旋转 90 度后形状会错位)
                float width = size.Width;
                size.Width = size.Height;
                size.Height = width;
            }

            // 4. 返回修正后的矩形
            return new OpenCvSharp.RotatedRect(rect.Center, size, angle);
        }
        //public static List<(RectF, float)> BoxesFromBitmap(Mat pred, Mat bitmap, float boxThresh, float detDbUnclipRatio, string detDbScoreMode)
        //{
        //    int width = bitmap.Cols;
        //    int height = bitmap.Rows;

        //    // 查找轮廓
        //    OpenCvSharp.Point[][] contours;
        //    HierarchyIndex[] hierarchy;
        //    Cv2.FindContours(bitmap, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        //    var boxes = new List<(RectF, float)>();
        //    // 限制最大处理数量，防止极端输入导致卡顿
        //    int numContours = Math.Min(contours.Length, MaxCandidates);

        //    for (int i = 0; i < numContours; i++)
        //    {
        //        if (contours[i].Length < 2) continue;

        //        // 1. 获取最小外接旋转矩形
        //        OpenCvSharp.RotatedRect box = Cv2.MinAreaRect(contours[i]);
        //        float ssid; // 短边长度
        //        float[] boxX, boxY;

        //        // 2. 规范化并计算短边
        //        GetMiniBoxes(box, out boxX, out boxY, out ssid);

        //        if (ssid < MinSize) continue;

        //        // 3. 计算置信度分数
        //        float score;
        //        if (detDbScoreMode == "slow")
        //        {
        //            score = PolygonScoreAcc(contours[i], pred, width, height);
        //        }
        //        else
        //        {
        //            // 默认使用 Fast 模式，速度提升明显
        //            score = BoxScoreFast(boxX, boxY, pred);
        //        }

        //        if (score < boxThresh) continue;

        //        // 4. 膨胀
        //        // 将当前 Box 坐标进行膨胀，还原成文本行的完整区域
        //        OpenCvSharp.RotatedRect unclipBox = Unclip(boxX, boxY, detDbUnclipRatio);

        //        // 检查膨胀后的尺寸有效性
        //        if (unclipBox.Size.Width < 1.001f && unclipBox.Size.Height < 1.001f) continue;

        //        // 5. 再次获取规范化点（用于最终计算外接矩形）
        //        GetMiniBoxes(unclipBox, out boxX, out boxY, out ssid);

        //        if (ssid < MinSize + 2) continue;

        //        // 6. 将旋转矩形转换为正矩形
        //        // 优化：不再经过 float[] -> 转换 -> Rect 的繁琐步骤，而是直接从 RotatedRect 计算
        //        // 如果需要旋转矩形坐标，应在此处返回 RotatedRect 或 Point2f[]。
        //        // 根据函数签名 List<Rect>，我们需要计算外接矩形。

        //        // 既然已经有 boxX, boxY，直接算 Min/Max 即可，这比 unclipBox.Points() 再遍历更直接
        //        // 因为 boxX/Y 在 GetMiniBoxes 中是经过顺序处理的，但求包围盒不需要顺序，只需要极值

        //        // 这里使用 unclipBox.Points() 的结果会更精确反映 Clipper 膨胀后的实际几何形状
        //        // 因为 GetMiniBoxes 做了排序和假设，可能引入微小误差。
        //        // 但是为了保持和原 DBNet 逻辑一致（通常使用 boxX/boxY），这里继续使用 boxX/boxY。
        //        // 如果追求 Clipper 的绝对精度，应重新从 solution 计算。

        //        // 性能优化：直接从 boxX, boxY 数组计算极值
        //        float minX = float.MaxValue, maxX = float.MinValue;
        //        float minY = float.MaxValue, maxY = float.MinValue;

        //        for (int k = 0; k < 4; k++)
        //        {
        //            float px = boxX[k];
        //            float py = boxY[k];
        //            if (px < minX) minX = px;
        //            if (px > maxX) maxX = px;
        //            if (py < minY) minY = py;
        //            if (py > maxY) maxY = py;
        //        }

        //        // 构建 Rect 并 Clamp 边界
        //        double x = Math.Floor(minX);
        //        double y = Math.Floor(minY);
        //        double w = Math.Ceiling(maxX - minX);
        //        double h = Math.Ceiling(maxY - minY);

        //        // 边界保护
        //        if (x < 0) x = 0;
        //        if (y < 0) y = 0;
        //        if (w > width - x) w = width - x;
        //        if (h > height - y) h = height - y;

        //        if (w > 0 && h > 0)
        //        {
        //            boxes.Add((new RectF((float)x, (float)y, (float)w, (float)h), score));
        //        }
        //    }
        //    return boxes;


        //    //int width = bitmap.Cols;
        //    //int height = bitmap.Rows;
        //    //OpenCvSharp.Point[][] contours;
        //    //HierarchyIndex[] hierarchy;
        //    //Cv2.FindContours(bitmap, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        //    //// 使用 ConcurrentBag 替代 List，用于线程安全地收集结果
        //    //var boxes = new ConcurrentBag<(RectF, float)>();
        //    //int numContours = Math.Min(contours.Length, MaxCandidates);
        //    //// --- 并行处理开始 ---
        //    //Parallel.For(0, numContours, i =>
        //    //{
        //    //    // OpenCV 的 Mat 数据指针在 C# 层是固定的，
        //    //    // 但为了避免极少数情况下的底层竞态或为了绝对安全，
        //    //    // 建议确保在调用此方法期间 pred 不被释放。
        //    //    // 由于 pred 是外部传入且在此方法内不释放，它是只读共享资源，OpenCV Sharp 通常处理得很好。
        //    //    if (contours[i].Length < 2) return;
        //    //    // 1. 获取最小外接旋转矩形
        //    //    OpenCvSharp.RotatedRect box = Cv2.MinAreaRect(contours[i]);
        //    //    float ssid;
        //    //    float[] boxX, boxY;
        //    //    // 2. 规范化并计算短边
        //    //    GetMiniBoxes(box, out boxX, out boxY, out ssid);
        //    //    if (ssid < MinSize) return;
        //    //    // 3. 计算置信度分数
        //    //    float score;
        //    //    if (detDbScoreMode == "slow")
        //    //    {
        //    //        score = PolygonScoreAcc(contours[i], pred, width, height);
        //    //    }
        //    //    else
        //    //    {
        //    //        score = BoxScoreFast(boxX, boxY, pred);
        //    //    }
        //    //    if (score < boxThresh) return;
        //    //    // 4. 膨胀
        //    //    OpenCvSharp.RotatedRect unclipBox = Unclip(boxX, boxY, detDbUnclipRatio);
        //    //    if (unclipBox.Size.Width < 1.001f && unclipBox.Size.Height < 1.001f) return;
        //    //    // 5. 再次获取规范化点
        //    //    GetMiniBoxes(unclipBox, out boxX, out boxY, out ssid);
        //    //    if (ssid < MinSize + 2) return;
        //    //    // 6. 转换为正矩形
        //    //    float minX = float.MaxValue, maxX = float.MinValue;
        //    //    float minY = float.MaxValue, maxY = float.MinValue;
        //    //    for (int k = 0; k < 4; k++)
        //    //    {
        //    //        float px = boxX[k];
        //    //        float py = boxY[k];
        //    //        if (px < minX) minX = px;
        //    //        if (px > maxX) maxX = px;
        //    //        if (py < minY) minY = py;
        //    //        if (py > maxY) maxY = py;
        //    //    }
        //    //    double x = Math.Floor(minX);
        //    //    double y = Math.Floor(minY);
        //    //    double w = Math.Ceiling(maxX - minX);
        //    //    double h = Math.Ceiling(maxY - minY);
        //    //    if (x < 0) x = 0;
        //    //    if (y < 0) y = 0;
        //    //    if (w > width - x) w = width - x;
        //    //    if (h > height - y) h = height - y;
        //    //    if (w > 0 && h > 0)
        //    //    {
        //    //        // 线程安全地添加到集合中
        //    //        boxes.Add((new RectF((float)x, (float)y, (float)w, (float)h), score));
        //    //    }
        //    //});
        //    //// --- 并行处理结束 ---
        //    //// 如果需要保持顺序（比如从上到下，从左到右），需要在这里排序
        //    //// 并行处理会导致顺序打乱
        //    //return boxes.ToList();
        //}

        #endregion


        public static Mat GetRotateCropImageByRect(Mat srcimage, RotatedRect rect)
        {
            // 1. 获取旋转矩形的参数
            Point2f center = CvDataExtensions.ToPointF(rect.Center);   // 中心点
            Size2f size = CvDataExtensions.ToSizeF(rect.Size);        // 宽和高
            float angle = rect.Angle;     

            if (angle < -45)
            {
                angle += 90f;
                float temp = size.Width;
                size.Width = size.Height;
                size.Height = temp;
            }
         
            float dy = size.Height / 2.0f;
        
            Point2f[] srcPts = new Point2f[3];
            Point2f[] dstPts = new Point2f[3];
   

            double radians = angle * Math.PI / 180.0;
            double sin = Math.Sin(radians);
            double cos = Math.Cos(radians);
            double halfW = size.Width / 2.0;
            double halfH = size.Height / 2.0;

            srcPts[0] = new Point2f(
                (float)(center.X + (-halfW * cos - (-halfH) * sin)),
                (float)(center.Y + (-halfW * sin + (-halfH) * cos))
            );
            // Point 2: Top-Right (relative to center: +w/2, -h/2)
            srcPts[1] = new Point2f(
                (float)(center.X + (halfW * cos - (-halfH) * sin)),
                (float)(center.Y + (halfW * sin + (-halfH) * cos))
            );
            // Point 3: Bottom-Left (relative to center: -w/2, +h/2)
            srcPts[2] = new Point2f(
                (float)(center.X + (-halfW * cos - (halfH) * sin)),
                (float)(center.Y + (-halfW * sin + (halfH) * cos))
            );
            // Point 4: Bottom-Right (relative to center: +w/2, +h/2) - 仿射变换只需要3个点
            // 对应的目标图像坐标（即我们要把上面的点变到哪里去）
            dstPts[0] = new Point2f(0, 0);                   // 变到新图的左上角
            dstPts[1] = new Point2f(size.Width, 0);         // 变到新图的右上角
            dstPts[2] = new Point2f(0, size.Height);        // 变到新图的左下角
                                                            // 5. 获取仿射变换矩阵 (将 srcPts 映射到 dstPts)
            Mat M = Cv2.GetAffineTransform(srcPts, dstPts);
            // 6. 执行仿射变换
            Mat dst_img = new Mat();
            Cv2.WarpAffine(srcimage, dst_img, M, new OpenCvSharp.Size((int)size.Width, (int)size.Height),
                           InterpolationFlags.Linear, BorderTypes.Replicate);
            // 7. 判断并修正竖向文本
            // 如果高度远大于宽度（例如1.5倍），通常说明文字是竖排的，需要旋转90度变正
            // 注意：这里使用 RotateMode.Clockwise90 (顺时针90度)
            if (dst_img.Rows >= dst_img.Cols * 1.5)
            {
                //// 修正：使用 Cv2.Rotate 是最简单且不会导致镜像的方法
                //// RotateFlags.Rotate90Clockwise = 1 (需要 OpenCvSharp 4.x+)
                //Cv2.Rotate(dst_img, dst_img, RotateFlags.Rotate90Clockwise);

                Mat srcCopy = new Mat(dst_img.Rows, dst_img.Cols, dst_img.Depth());
                Cv2.Transpose(dst_img, srcCopy);
                Cv2.Flip(srcCopy, srcCopy, 0);
                return srcCopy;
            }
            return dst_img;
        }

    }
}
