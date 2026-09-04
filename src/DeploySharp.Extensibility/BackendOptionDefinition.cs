using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Defines one serializable backend option for host-generated forms. / 定义一个供宿主生成表单的可序列化后端参数。</summary>
    public sealed class BackendOptionDefinition
    {
        /// <summary>Initializes an option definition. / 初始化参数定义。</summary>
        public BackendOptionDefinition(
            string name,
            BackendOptionValueType type,
            string? defaultValue = null,
            bool required = false,
            IEnumerable<string>? enumValues = null,
            double? minimum = null,
            double? maximum = null,
            string? visibleWhen = null,
            string? helpText = null)
        {
            Name = ExtGuard.Identifier(name, nameof(name));
            if (!Enum.IsDefined(typeof(BackendOptionValueType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            Type = type;
            DefaultValue = string.IsNullOrWhiteSpace(defaultValue) ? null : defaultValue!.Trim();
            Required = required;
            if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value) throw new ArgumentException("The minimum cannot be greater than the maximum.", nameof(minimum));
            Minimum = minimum;
            Maximum = maximum;
            VisibleWhen = string.IsNullOrWhiteSpace(visibleWhen) ? null : ExtGuard.NotNullOrWhiteSpace(visibleWhen, nameof(visibleWhen));
            HelpText = string.IsNullOrWhiteSpace(helpText) ? null : ExtGuard.NotNullOrWhiteSpace(helpText, nameof(helpText));

            var values = new List<string>();
            if (enumValues != null)
            {
                foreach (string value in enumValues)
                {
                    string normalized = ExtGuard.NotNullOrWhiteSpace(value!, nameof(enumValues));
                    if (values.Contains(normalized)) throw new ArgumentException("Enumeration values must be unique.", nameof(enumValues));
                    values.Add(normalized);
                }
            }
            if (type == BackendOptionValueType.Enum && values.Count == 0) throw new ArgumentException("Enum options require at least one enum value.", nameof(enumValues));
            EnumValues = new ReadOnlyCollection<string>(values);
        }

        /// <summary>Gets the stable option name. / 获取稳定的参数名称。</summary>
        public string Name { get; }
        /// <summary>Gets the option value type. / 获取参数值类型。</summary>
        public BackendOptionValueType Type { get; }
        /// <summary>Gets the optional serialized default value. / 获取可选的序列化默认值。</summary>
        public string? DefaultValue { get; }
        /// <summary>Gets whether the option is required. / 获取参数是否必填。</summary>
        public bool Required { get; }
        /// <summary>Gets allowed values for enum options. / 获取枚举参数的允许值。</summary>
        public IReadOnlyList<string> EnumValues { get; }
        /// <summary>Gets the optional numeric minimum. / 获取可选的数值下限。</summary>
        public double? Minimum { get; }
        /// <summary>Gets the optional numeric maximum. / 获取可选的数值上限。</summary>
        public double? Maximum { get; }
        /// <summary>Gets an optional host expression controlling visibility. / 获取控制可见性的可选宿主表达式。</summary>
        public string? VisibleWhen { get; }
        /// <summary>Gets optional help text. / 获取可选帮助文本。</summary>
        public string? HelpText { get; }
    }
}
