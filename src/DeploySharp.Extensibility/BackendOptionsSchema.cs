using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JYPPX.DeploySharp.Extensibility
{
    /// <summary>Provides an immutable, host-neutral backend parameter schema. / 提供不可变且与宿主无关的后端参数 schema。</summary>
    public sealed class BackendOptionsSchema : IBackendOptionsSchema
    {
        /// <summary>Initializes an options schema. / 初始化参数 schema。</summary>
        public BackendOptionsSchema(string schemaId, IEnumerable<BackendOptionDefinition>? options = null)
        {
            SchemaId = ExtGuard.Identifier(schemaId, nameof(schemaId));
            var definitions = new List<BackendOptionDefinition>();
            if (options != null)
            {
                foreach (BackendOptionDefinition option in options)
                {
                    if (option == null) throw new ArgumentException("Options cannot contain null entries.", nameof(options));
                    for (int index = 0; index < definitions.Count; index++) if (string.Equals(definitions[index].Name, option.Name, StringComparison.Ordinal)) throw new ArgumentException("Option names must be unique.", nameof(options));
                    definitions.Add(option);
                }
            }
            Options = new ReadOnlyCollection<BackendOptionDefinition>(definitions);
        }

        /// <inheritdoc />
        public string SchemaId { get; }
        /// <summary>Gets immutable option definitions in display order. / 获取按显示顺序排列的不可变参数定义。</summary>
        public IReadOnlyList<BackendOptionDefinition> Options { get; }
    }
}
