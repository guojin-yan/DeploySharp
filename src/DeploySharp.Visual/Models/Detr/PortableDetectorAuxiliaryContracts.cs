using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Detr
{
    /// <summary>Identifies the semantic value of a detector auxiliary input. / 标识检测器辅助输入的语义值。</summary>
    public enum PortableDetectorAuxiliaryInputKind
    {
        /// <summary>Image shape after resize, expressed as height then width. / 缩放后的图像尺寸，顺序为高、宽。</summary>
        ImageShape = 0,
        /// <summary>Source-to-model scale, expressed as vertical then horizontal scale. / 源图到模型的缩放比例，顺序为纵向、横向。</summary>
        ScaleFactor = 1,
        /// <summary>Target size consumed by an exported postprocessor. / 导出后处理器使用的目标尺寸。</summary>
        OriginalTargetSizes = 2
    }

    /// <summary>Identifies which image space supplies a size auxiliary value. / 标识尺寸辅助值取自哪个图像空间。</summary>
    public enum PortableDetectorAuxiliarySizeSpace
    {
        /// <summary>The original source image. / 原始源图。</summary>
        Source = 0,
        /// <summary>The prepared model canvas. / 已准备的模型画布。</summary>
        Model = 1
    }

    /// <summary>Identifies the two-value size axis order. / 标识双值尺寸的轴顺序。</summary>
    public enum PortableDetectorSizeOrder
    {
        /// <summary>Height followed by width. / 高后宽。</summary>
        HeightWidth = 0,
        /// <summary>Width followed by height. / 宽后高。</summary>
        WidthHeight = 1
    }

    /// <summary>Describes whether an artifact declares a fixed or dynamic batch axis while execution remains one image per decode. / 描述工件声明固定还是动态批次轴；执行仍限定每次解码一张图。</summary>
    public sealed class PortableDetectorBatchContract
    {
        /// <summary>Initializes a batch contract. / 初始化批次合同。</summary>
        public PortableDetectorBatchContract(bool hasDynamicAxis, int minimumBatch = 1, int maximumBatch = 1)
        {
            if (minimumBatch != 1 || maximumBatch != 1) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Portable detector source geometry currently supports exactly one image per decode.");
            HasDynamicAxis = hasDynamicAxis;
            MinimumBatch = minimumBatch;
            MaximumBatch = maximumBatch;
        }

        /// <summary>Gets whether ONNX metadata declares a dynamic batch axis. / 获取 ONNX 元数据是否声明动态批次轴。</summary>
        public bool HasDynamicAxis { get; }
        /// <summary>Gets the minimum executable batch. / 获取最小可执行批次。</summary>
        public int MinimumBatch { get; }
        /// <summary>Gets the maximum executable batch; source restoration currently requires one. / 获取最大可执行批次；当前源图恢复要求其为一。</summary>
        public int MaximumBatch { get; }
        /// <summary>Gets the artifact shape-pattern batch dimension. / 获取工件 shape pattern 的批次维。</summary>
        public long ShapeDimension => HasDynamicAxis ? -1L : MinimumBatch;
    }

    /// <summary>Defines one typed auxiliary tensor and its single geometry-generation rule. / 定义一个类型化辅助张量及其唯一几何生成规则。</summary>
    public sealed class PortableDetectorAuxiliaryInputContract
    {
        internal PortableDetectorAuxiliaryInputContract(string name, PortableDetectorAuxiliaryInputKind kind, TensorElementType elementType, long batchDimension, PortableDetectorAuxiliarySizeSpace sizeSpace, PortableDetectorSizeOrder sizeOrder)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An auxiliary tensor name is required.");
            if (batchDimension != -1 && batchDimension != 1) throw new VisualException(VisualErrorCodes.ProfileInvalid, "Portable detector auxiliary tensors support a dynamic or one-image batch axis.", tensorName: name);
            Name = name.Trim();
            Kind = kind;
            ElementType = elementType;
            ShapePattern = new TensorShape(batchDimension, 2);
            SizeSpace = sizeSpace;
            SizeOrder = sizeOrder;
        }

        /// <summary>Gets the exact backend input name. / 获取精确后端输入名称。</summary>
        public string Name { get; }
        /// <summary>Gets the semantic value kind. / 获取语义值类型。</summary>
        public PortableDetectorAuxiliaryInputKind Kind { get; }
        /// <summary>Gets the required element type. / 获取所需元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the dynamic or fixed shape pattern. / 获取动态或固定 shape pattern。</summary>
        public TensorShape ShapePattern { get; }
        /// <summary>Gets the size space used by size-valued tensors. / 获取尺寸类张量使用的尺寸空间。</summary>
        public PortableDetectorAuxiliarySizeSpace SizeSpace { get; }
        /// <summary>Gets the size axis order used by size-valued tensors. / 获取尺寸类张量使用的轴顺序。</summary>
        public PortableDetectorSizeOrder SizeOrder { get; }

        /// <summary>Creates an owned managed tensor from prepared geometry; adapters and backends must not recompute these values. / 根据已准备几何创建自有托管张量；适配器与后端不得重复计算这些值。</summary>
        public NamedTensor CreateTensor(PreparedVisualInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.IsDisposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The prepared visual input has been disposed.", tensorName: Name);
            if (input.BatchSize != 1) throw new VisualException(VisualErrorCodes.InputInvalid, "Portable detector auxiliary generation requires one image per prepared input.", tensorName: Name);
            if (Kind == PortableDetectorAuxiliaryInputKind.ScaleFactor)
            {
                return new NamedTensor(Name, new Tensor<float>(new TensorShape(1, 2), new[] { input.Transform.ScaleY, input.Transform.ScaleX }, TensorBufferOwnership.Transfer));
            }

            VisualSize size = SizeSpace == PortableDetectorAuxiliarySizeSpace.Source ? input.SourceSize : input.ModelSize;
            long first = SizeOrder == PortableDetectorSizeOrder.WidthHeight ? size.Width : size.Height;
            long second = SizeOrder == PortableDetectorSizeOrder.WidthHeight ? size.Height : size.Width;
            if (ElementType == TensorElementType.Int64)
            {
                return new NamedTensor(Name, new Tensor<long>(new TensorShape(1, 2), new[] { first, second }, TensorBufferOwnership.Transfer));
            }
            if (ElementType == TensorElementType.Float32)
            {
                return new NamedTensor(Name, new Tensor<float>(new TensorShape(1, 2), new[] { (float)first, (float)second }, TensorBufferOwnership.Transfer));
            }
            throw new VisualException(VisualErrorCodes.ProfileInvalid, "The auxiliary generation rule has an unsupported element type.", tensorName: Name);
        }

        internal VisualAuxiliaryInputBinding ToVisualBinding() => new VisualAuxiliaryInputBinding(Name, ElementType, ShapePattern);
    }

    internal static class PortableDetectorAuxiliaryContracts
    {
        internal static IReadOnlyList<PortableDetectorAuxiliaryInputContract> Create(PortableDetectorFamily family, PortableDetectorBatchContract batch)
        {
            long dimension = batch.ShapeDimension;
            var result = new List<PortableDetectorAuxiliaryInputContract>();
            if (family == PortableDetectorFamily.DEIMv2Det)
            {
                result.Add(new PortableDetectorAuxiliaryInputContract("orig_target_sizes", PortableDetectorAuxiliaryInputKind.OriginalTargetSizes, TensorElementType.Int64, dimension, PortableDetectorAuxiliarySizeSpace.Model, PortableDetectorSizeOrder.HeightWidth));
            }
            else if (family == PortableDetectorFamily.RTDETRDet)
            {
                result.Add(new PortableDetectorAuxiliaryInputContract("im_shape", PortableDetectorAuxiliaryInputKind.ImageShape, TensorElementType.Float32, dimension, PortableDetectorAuxiliarySizeSpace.Model, PortableDetectorSizeOrder.HeightWidth));
                result.Add(new PortableDetectorAuxiliaryInputContract("scale_factor", PortableDetectorAuxiliaryInputKind.ScaleFactor, TensorElementType.Float32, dimension, PortableDetectorAuxiliarySizeSpace.Source, PortableDetectorSizeOrder.HeightWidth));
            }
            else if (family == PortableDetectorFamily.RTDETRv2Det)
            {
                result.Add(new PortableDetectorAuxiliaryInputContract("orig_target_sizes", PortableDetectorAuxiliaryInputKind.OriginalTargetSizes, TensorElementType.Int64, dimension, PortableDetectorAuxiliarySizeSpace.Source, PortableDetectorSizeOrder.WidthHeight));
            }
            else if (family == PortableDetectorFamily.PPYOLOEDet)
            {
                result.Add(new PortableDetectorAuxiliaryInputContract("scale_factor", PortableDetectorAuxiliaryInputKind.ScaleFactor, TensorElementType.Float32, dimension, PortableDetectorAuxiliarySizeSpace.Source, PortableDetectorSizeOrder.HeightWidth));
            }
            return new ReadOnlyCollection<PortableDetectorAuxiliaryInputContract>(result);
        }
    }
}
