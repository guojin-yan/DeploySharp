using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using DeploySharp.Log;
using log4net;


namespace DeploySharp.Data
{
    /// <summary>
    /// Enumeration defining tensor types (Input/Output)
    /// 定义张量类型的枚举（输入/输出）
    /// </summary>
    public enum TensorType
    {
        /// <summary>
        /// Represents an input tensor
        /// 表示输入张量
        /// </summary>
        Input,

        /// <summary>
        /// Represents an output tensor
        /// 表示输出张量
        /// </summary>
        Output
    }

    /// <summary>
    /// Represents a single tensor node with metadata and data buffer
    /// 表示具有元数据和数据缓冲区的单个张量节点
    /// </summary>
    /// <remarks>
    /// <para>
    /// Manages tensor data including shape, type and buffer memory.
    /// 管理张量数据，包括形状、类型和缓冲区内存。
    /// </para>
    /// <para>
    /// Implements IDisposable for proper resource cleanup.
    /// 实现了IDisposable接口以进行适当的资源清理。
    /// </para>
    /// </remarks>
    public sealed class NodeData : IDisposable
    {
        /// <summary>
        /// Gets or sets the underlying data buffer array
        /// 获取或设置底层数据缓冲区数组
        /// </summary>
        public Array DataBuffer;

        /// <summary>
        /// Gets or sets the name identifier of the node
        /// 获取或设置节点的名称标识符
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the index position in tensor collection
        /// 获取或设置在张量集合中的索引位置
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the tensor type (Input/Output)
        /// 获取或设置张量类型（输入/输出）
        /// </summary>
        public TensorType Type { get; set; }

        /// <summary>
        /// Gets the dimensional shape of the tensor
        /// 获取张量的维度形状
        /// </summary>
        public int[] Shape { get; private set; }

        /// <summary>
        /// Gets the data type of buffer elements
        /// 获取缓冲区元素的数据类型
        /// </summary>
        public Type DataType { get; private set; }

        /// <summary>
        /// Gets the total number of elements in the buffer
        /// 获取缓冲区中的元素总数
        /// </summary>
        public int ElementCount => Shape.Aggregate(1, (a, b) => a * b);

        /// <summary>
        /// Gets the byte size of a single element
        /// 获取单个元素的字节大小
        /// </summary>
        public int ElementSize => GetElementSize(DataType);

        /// <summary>
        /// Initializes a new instance of the NodeData class
        /// 初始化NodeData类的新实例
        /// </summary>
        /// <param name="name">The node name/节点名称</param>
        /// <param name="index">The node index/节点索引</param>
        /// <param name="type">The tensor type/张量类型</param>
        /// <param name="data">The data buffer/数据缓冲区</param>
        /// <param name="shape">The tensor shape/张量形状</param>
        /// <param name="dataType">The element data type/元素数据类型</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any parameter is null
        /// 当任何参数为null时抛出
        /// </exception>
        public NodeData(string name, int index, TensorType type,
              Array data, int[] shape, Type dataType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Index = index;
            Type = type;
            DataBuffer = data ?? throw new ArgumentNullException(nameof(data));
            Shape = (int[])shape.Clone() ?? throw new ArgumentNullException(nameof(shape));
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        }

        /// <summary>
        /// Releases all resources used by the NodeData
        /// 释放NodeData使用的所有资源
        /// </summary>
        public void Dispose()
        {
            // Currently empty but preserves pattern for future implementation
            // 当前为空但保留模式以便未来实现
        }

        private static int GetElementSize(Type type)
        {
            if (type == typeof(float)) return sizeof(float);
            if (type == typeof(double)) return sizeof(double);
            if (type == typeof(int)) return sizeof(int);
            if (type == typeof(byte)) return sizeof(byte);
            throw new NotSupportedException($"Type {type} is not supported");
        }
    }

    /// <summary>
    /// Manages collection of tensor nodes with validation and enumeration capabilities
    /// 管理具有验证和枚举功能的张量节点集合
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides indexed access to nodes by both numeric index and name.
    /// 提供通过数值索引和名称对节点的索引访问。
    /// </para>
    /// <para>
    /// Implements IDisposable for proper cleanup of managed nodes.
    /// 实现了IDisposable接口以正确清理托管节点。
    /// </para>
    /// </remarks>
    public sealed class DataTensor : IEnumerable<NodeData>, IDisposable
    {
        private readonly List<NodeData> _nodes = new List<NodeData>();
        private bool _disposed;

        /// <summary>
        /// Gets the number of nodes in the collection
        /// 获取集合中的节点数量
        /// </summary>
        public int Count => _nodes.Count;

        /// <summary>
        /// Gets the total number of elements across all node buffers
        /// 获取所有节点缓冲区中的元素总数
        /// </summary>
        public int TotalElements => _nodes.Sum(n => n.ElementCount);

        /// <summary>
        /// Indexer to access nodes by numerical index
        /// 通过数值索引访问节点的索引器
        /// </summary>
        /// <param name="index">The zero-based index/从零开始的索引</param>
        /// <returns>The node at specified index/指定索引处的节点</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when index is out of range
        /// 当索引超出范围时抛出
        /// </exception>
        public NodeData this[int index]
        {
            get
            {
                if (index < 0 || index >= _nodes.Count)
                    throw new IndexOutOfRangeException($"Index {index} out of range");
                return _nodes[index];
            }
        }

        /// <summary>
        /// Indexer to access nodes by name
        /// 通过名称访问节点的索引器
        /// </summary>
        /// <param name="name">The node name/节点名称</param>
        /// <returns>The node with specified name/具有指定名称的节点</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when name is not found
        /// 当找不到名称时抛出
        /// </exception>
        public NodeData this[string name]
        {
            get => _nodes.FirstOrDefault(n => n.Name == name)
                   ?? throw new KeyNotFoundException(name);
        }

        /// <summary>
        /// Adds a new node to the collection using external memory
        /// 使用外部内存向集合中添加新节点
        /// </summary>
        /// <param name="name">The node name/节点名称</param>
        /// <param name="index">The node index/节点索引</param>
        /// <param name="type">The tensor type/张量类型</param>
        /// <param name="data">The data buffer/数据缓冲区</param>
        /// <param name="shape">The tensor shape/张量形状</param>
        /// <param name="dataType">The element data type/元素数据类型</param>
        /// <exception cref="ArgumentException">
        /// Thrown when name is empty or whitespace
        /// 当名称为空或空白时抛出
        /// </exception>
        /// <exception cref="ArgumentNullbrellaException">
        /// Thrown when data, shape or dataType is null
        /// 当data、shape或dataType为null时抛出
        /// </exception>
        public void AddNode(string name, int index, TensorType type,
                          Array data, int[] shape, Type dataType)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Node name cannot be empty", nameof(name));

            if (data == null || shape == null || dataType == null)
                throw new ArgumentNullException();

            _nodes.Add(new NodeData(
                name: name,
                index: index,
                type: type,
                data: data,
                shape: (int[])shape.Clone(),
                dataType: dataType
            ));
        }

        /// <summary>
        /// Adds a new node to the collection with newly allocated memory
        /// 使用新分配的内存向集合中添加新节点
        /// </summary>
        /// <typeparam name="T">The unmanaged element type/非托管元素类型</typeparam>
        /// <param name="name">The node name/节点名称</param>
        /// <param name="index">The node index/节点索引</param>
        /// <param name="type">The tensor type/张量类型</param>
        /// <param name="shape">The tensor shape/张量形状</param>
        public void AddNode<T>(string name, int index, TensorType type,
                             int[] shape) where T : unmanaged
        {
            var elementCount = shape.Aggregate(1, (a, b) => a * b);
            Array data = new T[(elementCount)];
            AddNode(name, index, type, data, shape, typeof(T));
        }

        /// <summary>
        /// Gets a node by name
        /// 通过名称获取节点
        /// </summary>
        /// <param name="name">The node name/节点名称</param>
        /// <returns>The found node/找到的节点</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when node is not found
        /// 当找不到节点时抛出
        /// </exception>
        public NodeData GetNode(string name) =>
            _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Node {name} not found");

        /// <summary>
        /// Attempts to get a node by name
        /// 尝试通过名称获取节点
        /// </summary>
        /// <param name="name">The node name/节点名称</param>
        /// <param name="node">Output parameter for found node/用于找到的节点的输出参数</param>
        /// <returns>true if found; otherwise false/如果找到则为true；否则为false</returns>
        public bool TryGetNode(string name, out NodeData node)
        {
            node = _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.Ordinal));
            return node != null;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of nodes
        /// 返回一个遍历节点集合的枚举器
        /// </summary>
        /// <returns>An enumerator for the nodes/节点的枚举器</returns>
        public IEnumerator<NodeData> GetEnumerator() => _nodes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Releases all resources used by the DataTensor
        /// 释放DataTensor使用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var node in _nodes)
            {
                node.Dispose();
            }

            _nodes.Clear();
            _disposed = true;
        }
    }


}
