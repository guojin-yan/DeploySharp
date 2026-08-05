namespace DeploySharp.Data
{
    /// <summary>
    /// Specifies the semantic order of three-channel image color data
    /// 指定三通道图像颜色数据的语义顺序
    /// </summary>
    public enum ImageColorOrder
    {
        /// <summary>
        /// Blue, green, red channel order
        /// 蓝, 绿, 红通道顺序
        /// </summary>
        Bgr,

        /// <summary>
        /// Red, green, blue channel order
        /// 红, 绿, 蓝通道顺序
        /// </summary>
        Rgb
    }
}
