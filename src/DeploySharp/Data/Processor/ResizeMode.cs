using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Provides enumeration over how the image should be resized.
    /// </summary>
    public enum ResizeMode
    {

        /// <summary>
        /// Stretches the resized image to fit the bounds of its container.
        /// </summary>
        Stretch,

        /// <summary>
        /// Pads the resized image to fit the bounds of its container.
        /// If only one dimension is passed, will maintain the original aspect ratio.
        /// </summary>
        Pad,

        /// <summary>
        /// Constrains the resized image to fit the bounds of its container maintaining
        /// the original aspect ratio.
        /// </summary>
        Max,

        /// <summary>
        /// Crops the resized image to fit the bounds of its container.
        /// </summary>
        Crop,

    }
}
