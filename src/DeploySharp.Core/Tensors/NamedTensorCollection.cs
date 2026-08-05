using System;
using System.Collections;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>
    /// Provides an ordered, uniquely named tensor collection. / 提供有序且名称唯一的张量集合。
    /// </summary>
    public abstract class NamedTensorCollection : IReadOnlyList<NamedTensor>
    {
        private readonly List<NamedTensor> _items;
        private readonly Dictionary<string, ITensor> _byName;

        /// <summary>Initializes a named tensor collection. / 初始化命名张量集合。</summary>
        protected NamedTensorCollection(IEnumerable<NamedTensor> tensors)
        {
            if (tensors == null)
            {
                throw new ArgumentNullException(nameof(tensors));
            }

            _items = new List<NamedTensor>();
            _byName = new Dictionary<string, ITensor>(StringComparer.Ordinal);
            foreach (NamedTensor item in tensors)
            {
                if (item == null)
                {
                    throw new ArgumentException("Tensor collections cannot contain null items.", nameof(tensors));
                }

                if (_byName.ContainsKey(item.Name))
                {
                    throw new ArgumentException($"A tensor named '{item.Name}' was added more than once.", nameof(tensors));
                }

                _items.Add(item);
                _byName.Add(item.Name, item.Tensor);
            }
        }

        /// <inheritdoc />
        /// <remarks>Returns the number of named tensors. / 返回命名张量数量。</remarks>
        public int Count => _items.Count;

        /// <inheritdoc />
        /// <remarks>Returns an item in insertion order. / 按插入顺序返回项目。</remarks>
        public NamedTensor this[int index] => _items[index];

        /// <summary>
        /// Gets a tensor by its exact model name. / 按精确模型名称获取张量。
        /// </summary>
        public ITensor GetRequired(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!_byName.TryGetValue(name, out ITensor? tensor))
            {
                throw new KeyNotFoundException($"No tensor named '{name}' exists in the collection.");
            }

            return tensor;
        }

        /// <summary>
        /// Attempts to get a tensor by its exact model name. / 尝试按精确模型名称获取张量。
        /// </summary>
        public bool TryGet(string name, out ITensor? tensor)
        {
            if (name == null)
            {
                tensor = null;
                return false;
            }

            return _byName.TryGetValue(name, out tensor);
        }

        /// <inheritdoc />
        /// <remarks>Enumerates items in insertion order. / 按插入顺序枚举项目。</remarks>
        public IEnumerator<NamedTensor> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        /// <inheritdoc />
        /// <remarks>Enumerates the same sequence through the non-generic interface. / 通过非泛型接口枚举相同序列。</remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
