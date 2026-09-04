namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Describes a value type accepted by a backend option. / 描述后端参数接受的值类型。</summary>
    public enum BackendOptionValueType
    {
        /// <summary>Free-form string. / 字符串。</summary>
        String = 0,
        /// <summary>Boolean value. / 布尔值。</summary>
        Boolean = 1,
        /// <summary>Signed integer value. / 有符号整数。</summary>
        Integer = 2,
        /// <summary>Floating-point value. / 浮点数。</summary>
        Number = 3,
        /// <summary>One value from an enumerated set. / 枚举值。</summary>
        Enum = 4
    }
}
