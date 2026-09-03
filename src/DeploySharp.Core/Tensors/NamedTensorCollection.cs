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
        private readonly IReadOnlyList<NamedTensor> _items;
        private readonly Dictionary<string, ITensor>? _byName;

        /// <summary>Initializes a named tensor collection. / 初始化命名张量集合。</summary>
        protected NamedTensorCollection(IEnumerable<NamedTensor> tensors)
        {
            if (tensors == null)
            {
                throw new ArgumentNullException(nameof(tensors));
            }

            int capacity = tensors is ICollection<NamedTensor> collection ? collection.Count : 0;
            var items = capacity > 0 ? new List<NamedTensor>(capacity) : new List<NamedTensor>();
            var byName = capacity > 0 ? new Dictionary<string, ITensor>(capacity, StringComparer.Ordinal) : new Dictionary<string, ITensor>(StringComparer.Ordinal);
            foreach (NamedTensor item in tensors)
            {
                if (item == null)
                {
                    throw new ArgumentException("Tensor collections cannot contain null items.", nameof(tensors));
                }

                if (byName.ContainsKey(item.Name))
                {
                    throw new ArgumentException($"A tensor named '{item.Name}' was added more than once.", nameof(tensors));
                }

                items.Add(item);
                byName.Add(item.Name, item.Tensor);
            }
            _items = items;
            _byName = byName;
        }

        /// <summary>Initializes a collection containing one tensor without creating an intermediate enumerable. / 初始化只包含一个张量且不创建中间可枚举对象的集合。</summary>
        protected NamedTensorCollection(string name, ITensor tensor)
        {
            NamedTensor item = new NamedTensor(name, tensor);
            _items = new[] { item };
            _byName = null;
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

            if (_byName != null && _byName.TryGetValue(name, out ITensor? tensor))
            {
                return tensor;
            }

            if (_byName == null && _items.Count == 1 && string.Equals(_items[0].Name, name, StringComparison.Ordinal))
            {
                return _items[0].Tensor;
            }

            throw new KeyNotFoundException($"No tensor named '{name}' exists in the collection.");
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

            if (_byName != null)
            {
                return _byName.TryGetValue(name, out tensor);
            }

            if (_items.Count == 1 && string.Equals(_items[0].Name, name, StringComparison.Ordinal))
            {
                tensor = _items[0].Tensor;
                return true;
            }

            tensor = null;
            return false;
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
