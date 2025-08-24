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
    /// </summary>
    public enum TensorType
    {
        /// <summary>
        /// Represents an input tensor
        /// </summary>
        Input,

        /// <summary>
        /// Represents an output tensor
        /// </summary>
        Output
    }

    /// <summary>
    /// Represents a single tensor node with metadata and data buffer
    /// </summary>
    public sealed class NodeData : IDisposable
    {
        public Array DataBuffer;

        /// <summary>
        /// Gets or sets the name identifier of the node
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the index position in tensor collection
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the tensor type (Input/Output)
        /// </summary>
        public TensorType Type { get; set; }

        /// <summary>
        /// Gets the dimensional shape of the tensor
        /// </summary>
        public int[] Shape { get; private set; }

        /// <summary>
        /// Gets the data type of buffer elements
        /// </summary>
        public Type DataType { get; private set; }

        /// <summary>
        /// Gets the total number of elements in the buffer
        /// </summary>
        public int ElementCount => Shape.Aggregate(1, (a, b) => a * b);

        /// <summary>
        /// Gets the byte size of a single element
        /// </summary>
        public int ElementSize => GetElementSize(DataType);

        /// <summary>
        /// Initializes a new instance of the NodeData class
        /// </summary>
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
        /// </summary>
        public void Dispose()
        {
            
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
    /// </summary>
    public sealed class DataTensor : IEnumerable<NodeData>, IDisposable
    {
        private readonly List<NodeData> _nodes = new List<NodeData>();
        private bool _disposed;

        /// <summary>
        /// Gets the number of nodes in the collection
        /// </summary>
        public int Count => _nodes.Count;

        /// <summary>
        /// Gets the total number of elements across all node buffers
        /// </summary>
        public int TotalElements => _nodes.Sum(n => n.ElementCount);

        /// <summary>
        /// Indexer to access nodes by numerical index
        /// </summary>
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
        /// </summary>
        public NodeData this[string name]
        {
            get => _nodes.FirstOrDefault(n => n.Name == name)
                   ?? throw new KeyNotFoundException(name);
        }

        /// <summary>
        /// Adds a new node to the collection using external memory
        /// </summary>
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
        /// </summary>
        public void AddNode<T>(string name, int index, TensorType type,
                             int[] shape) where T : unmanaged
        {
            var elementCount = shape.Aggregate(1, (a, b) => a * b);
            Array data = new T[(elementCount)];
            AddNode(name, index, type, data, shape, typeof(T));
        }

        /// <summary>
        /// Gets a node by name
        /// </summary>
        public NodeData GetNode(string name) =>
            _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Node {name} not found");

        /// <summary>
        /// Attempts to get a node by name
        /// </summary>
        public bool TryGetNode(string name, out NodeData node)
        {
            node = _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.Ordinal));
            return node != null;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of nodes
        /// </summary>
        public IEnumerator<NodeData> GetEnumerator() => _nodes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Releases all resources used by the DataTensor
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



        private static int GetElementSize(Type type)
        {
            if (type == typeof(float)) return Marshal.SizeOf(typeof(float));
            if (type == typeof(double)) return Marshal.SizeOf(typeof(double));
            if (type == typeof(int)) return Marshal.SizeOf(typeof(int));
            if (type == typeof(byte)) return Marshal.SizeOf(typeof(byte));
            if (type == typeof(short)) return Marshal.SizeOf(typeof(short));
            if (type == typeof(long)) return Marshal.SizeOf(typeof(long));
            throw new NotSupportedException($"Type {type} is not supported");
        }
    }

    ///// <summary>
    ///// Enumeration defining tensor types (Input/Output)
    ///// </summary>
    //public enum TensorType
    //{
    //    /// <summary>
    //    /// Represents an input tensor
    //    /// </summary>
    //    Input,

    //    /// <summary>
    //    /// Represents an output tensor
    //    /// </summary>
    //    Output
    //}

    ///// <summary>
    ///// Represents a single tensor node with metadata and data buffer
    ///// </summary>
    //public class NodeData
    //{
    //    /// <summary>
    //    /// Gets or sets the name identifier of the node
    //    /// </summary>
    //    public string Name { get; set; }

    //    /// <summary>
    //    /// Gets or sets the index position in tensor collection
    //    /// </summary>
    //    public int Index { get; set; }

    //    /// <summary>
    //    /// Gets or sets the tensor type (Input/Output)
    //    /// </summary>
    //    public TensorType Type { get; set; }

    //    /// <summary>
    //    /// Gets or sets the data buffer array (supports float/double/int/byte)
    //    /// </summary>
    //    public Array Buffer { get; set; }

    //    /// <summary>
    //    /// Gets or sets the dimensional shape of the tensor
    //    /// </summary>
    //    public int[] Shape { get; set; }

    //    /// <summary>
    //    /// Gets or sets the data type of buffer elements
    //    /// </summary>
    //    public Type DataType { get; set; }

    //    /// <summary>
    //    /// Initializes a new instance of the NodeData class
    //    /// </summary>
    //    /// <param name="name">Node name identifier</param>
    //    /// <param name="index">Index position in tensor collection</param>
    //    /// <param name="type">Input/Output type specifier</param>
    //    /// <param name="buffer">Data buffer array</param>
    //    /// <param name="shape">Dimensional shape of the tensor</param>
    //    /// <param name="dataType">Data type of buffer elements</param>
    //    public NodeData(string name, int index, TensorType type,
    //           Array buffer, int[] shape, Type dataType)
    //    {
    //        Name = name;
    //        Index = index;
    //        Type = type;
    //        Buffer = buffer;
    //        Shape = shape;
    //        DataType = dataType;
    //    }
    //}

    ///// <summary>
    ///// Manages collection of tensor nodes with validation and enumeration capabilities
    ///// </summary>
    //public sealed class DataTensor : IEnumerable<NodeData>, IDisposable
    //{
    //    private readonly List<NodeData> _nodes = new List<NodeData>();
    //    private bool _disposed;

    //    /// <summary>
    //    /// Gets the number of nodes in the collection
    //    /// </summary>
    //    public int Count => _nodes.Count;

    //    /// <summary>
    //    /// Gets the total number of elements across all node buffers
    //    /// </summary>
    //    public int TotalElements => _nodes.Sum(n => n.Buffer.Length);

    //    /// <summary>
    //    /// Indexer to access nodes by numerical index
    //    /// </summary>
    //    /// <param name="index">The zero-based index of the node to get</param>
    //    /// <returns>The node at the specified index</returns>
    //    /// <exception cref="IndexOutOfRangeException">Thrown when index is out of range</exception>
    //    public NodeData this[int index]
    //    {
    //        get
    //        {
    //            if (index < 0 || index >= _nodes.Count)
    //                throw new IndexOutOfRangeException($"Index {index} out of range");
    //            return _nodes[index];
    //        }
    //    }

    //    /// <summary>
    //    /// Indexer to access nodes by name
    //    /// </summary>
    //    /// <param name="name">The name of the node to get</param>
    //    /// <returns>The node with the specified name</returns>
    //    /// <exception cref="KeyNotFoundException">Thrown when node with specified name is not found</exception>
    //    public NodeData this[string name]
    //    {
    //        get => _nodes.FirstOrDefault(n => n.Name == name)
    //               ?? throw new KeyNotFoundException(name);
    //    }

    //    /// <summary>
    //    /// Adds a new node to the collection
    //    /// </summary>
    //    /// <param name="name">Node name identifier</param>
    //    /// <param name="index">Index position in tensor collection</param>
    //    /// <param name="type">Input/Output type specifier</param>
    //    /// <param name="buffer">Data buffer array</param>
    //    /// <param name="shape">Dimensional shape of the tensor</param>
    //    /// <param name="dataType">Data type of buffer elements</param>
    //    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    //    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    //    public void AddNode(string name, int index, TensorType type,
    //                      Array buffer, int[] shape, Type dataType)
    //    {
    //        ValidateParameters(name, buffer, shape, dataType);

    //        _nodes.Add(new NodeData(
    //            name: name,
    //            index: index,
    //            type: type,
    //            buffer: buffer,
    //            shape: (int[])shape.Clone(),
    //            dataType: dataType
    //        ));
    //    }

    //    private void ValidateParameters(string name, Array buffer,
    //                                  int[] shape, Type dataType)
    //    {
    //        if (string.IsNullOrWhiteSpace(name))
    //            throw new ArgumentException("Node name cannot be empty", nameof(name));

    //        if (buffer == null || shape == null || dataType == null)
    //            throw new ArgumentNullException();

    //        if (buffer.Length != shape.Aggregate(1, (a, b) => a * b))
    //            throw new ArgumentException("Buffer length doesn't match shape");
    //    }

    //    /// <summary>
    //    /// Gets a node by name
    //    /// </summary>
    //    /// <param name="name">The name of the node to find</param>
    //    /// <returns>The node with the specified name</returns>
    //    /// <exception cref="KeyNotFoundException">Thrown when node with specified name is not found</exception>
    //    public NodeData GetNode(string name) =>
    //        _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.Ordinal))
    //        ?? throw new KeyNotFoundException($"Node {name} not found");

    //    /// <summary>
    //    /// Attempts to get a node by name
    //    /// </summary>
    //    /// <param name="name">The name of the node to find</param>
    //    /// <param name="node">When this method returns, contains the node with the specified name, if found</param>
    //    /// <returns>true if the node was found; otherwise, false</returns>
    //    public bool TryGetNode(string name, out NodeData node)
    //    {
    //        node = _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.Ordinal));
    //        return node != null;
    //    }

    //    /// <summary>
    //    /// Returns an enumerator that iterates through the collection of nodes
    //    /// </summary>
    //    /// <returns>An enumerator that can be used to iterate through the collection</returns>
    //    public IEnumerator<NodeData> GetEnumerator() => _nodes.GetEnumerator();

    //    /// <summary>
    //    /// Returns an enumerator that iterates through the collection of nodes
    //    /// </summary>
    //    /// <returns>An enumerator that can be used to iterate through the collection</returns>
    //    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    //    /// <summary>
    //    /// Releases all resources used by the DataTensor
    //    /// </summary>
    //    public void Dispose()
    //    {
    //        if (_disposed) return;
    //        _nodes.Clear();
    //        _disposed = true;
    //    }
    //}
}
