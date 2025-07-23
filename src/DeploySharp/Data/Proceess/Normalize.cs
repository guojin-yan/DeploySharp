﻿using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Normalize data classes using OpenCvSharp.
    /// </summary>
    public static class Normalize
    {
        /// <summary>
        /// Run normalize data classes.
        /// </summary>
        /// <param name="im">The image mat.</param>
        /// <param name="mean">Channel mean.</param>
        /// <param name="scale">Channel variance.</param>
        /// <param name="is_scale">Whether to divide by 255.</param>
        /// <returns>The normalize data.</returns>
        public static Mat Run(Mat im, float[] mean, float[] scale, bool is_scale)
        {
            double e = 1.0;
            if (is_scale)
            {
                e /= 255.0;
            }
            im.ConvertTo(im, MatType.CV_32FC3, e);
            Mat[] bgr_channels = new Mat[3];
            Cv2.Split(im, out bgr_channels);
            for (var i = 0; i < bgr_channels.Length; i++)
            {
                bgr_channels[i].ConvertTo(bgr_channels[i], MatType.CV_32FC1, 1.0 * scale[i],
                    (0.0 - mean[i]) * scale[i]);
            }
            Mat re = new Mat();
            Cv2.Merge(bgr_channels, re);
            return re;
        }
        /// <summary>
        /// Run normalize data classes.
        /// </summary>
        /// <param name="im">The image mat.</param>
        /// <param name="is_scale">Whether to divide by 255.</param>
        /// <returns>The normalize data.</returns>
        public static Mat Run(Mat im, bool is_scale)
        {
            double e = 1.0;
            if (is_scale)
            {
                e /= 255.0;
            }
            im.ConvertTo(im, MatType.CV_32FC3, e);
            return im;
        }

    }
}
