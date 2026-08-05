using System;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Maps CLR array element types to backend-neutral tensor element types. / 将 CLR 数组元素类型映射为后端无关的张量元素类型。
    /// </summary>
    public static class TensorElementTypes
    {
        /// <summary>
        /// Resolves the tensor element type represented by <typeparamref name="T"/>. / 解析 <typeparamref name="T"/> 表示的张量元素类型。
        /// </summary>
        public static TensorElementType FromType<T>()
        {
            return FromType(typeof(T));
        }

        /// <summary>
        /// Resolves the tensor element type represented by a CLR type. / 解析 CLR 类型表示的张量元素类型。
        /// </summary>
        public static TensorElementType FromType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type == typeof(bool)) return TensorElementType.Boolean;
            if (type == typeof(sbyte)) return TensorElementType.Int8;
            if (type == typeof(byte)) return TensorElementType.UInt8;
            if (type == typeof(short)) return TensorElementType.Int16;
            if (type == typeof(ushort)) return TensorElementType.UInt16;
            if (type == typeof(int)) return TensorElementType.Int32;
            if (type == typeof(uint)) return TensorElementType.UInt32;
            if (type == typeof(long)) return TensorElementType.Int64;
            if (type == typeof(ulong)) return TensorElementType.UInt64;
            if (type == typeof(float)) return TensorElementType.Float32;
            if (type == typeof(double)) return TensorElementType.Float64;
            if (type == typeof(string)) return TensorElementType.String;

            throw new NotSupportedException($"The CLR type '{type.FullName}' is not a supported tensor element type.");
        }
    }
}
