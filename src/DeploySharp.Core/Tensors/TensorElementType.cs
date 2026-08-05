namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Defines backend-neutral tensor element types. / 定义后端无关的张量元素类型。
    /// </summary>
    public enum TensorElementType
    {
        /// <summary>Unknown or backend-specific type. / 未知或后端专用类型。</summary>
        Unknown = 0,
        /// <summary>Boolean. / 布尔类型。</summary>
        Boolean,
        /// <summary>Signed 8-bit integer. / 有符号 8 位整数。</summary>
        Int8,
        /// <summary>Unsigned 8-bit integer. / 无符号 8 位整数。</summary>
        UInt8,
        /// <summary>Signed 16-bit integer. / 有符号 16 位整数。</summary>
        Int16,
        /// <summary>Unsigned 16-bit integer. / 无符号 16 位整数。</summary>
        UInt16,
        /// <summary>Signed 32-bit integer. / 有符号 32 位整数。</summary>
        Int32,
        /// <summary>Unsigned 32-bit integer. / 无符号 32 位整数。</summary>
        UInt32,
        /// <summary>Signed 64-bit integer. / 有符号 64 位整数。</summary>
        Int64,
        /// <summary>Unsigned 64-bit integer. / 无符号 64 位整数。</summary>
        UInt64,
        /// <summary>IEEE 754 half precision. / IEEE 754 半精度浮点数。</summary>
        Float16,
        /// <summary>Brain floating point 16-bit. / Brain 16 位浮点数。</summary>
        BFloat16,
        /// <summary>IEEE 754 single precision. / IEEE 754 单精度浮点数。</summary>
        Float32,
        /// <summary>IEEE 754 double precision. / IEEE 754 双精度浮点数。</summary>
        Float64,
        /// <summary>UTF-16 managed string. / UTF-16 托管字符串。</summary>
        String
    }
}
