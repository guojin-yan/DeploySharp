using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Provides predefined class name dictionaries for popular computer vision datasets
    /// 为流行的计算机视觉数据集提供预定义的类别名称字典
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains static dictionaries mapping class indices to human-readable class names
    /// for commonly used datasets in object detection and image classification tasks.
    /// </para>
    /// <para>
    /// 包含将类别索引映射到人类可读类别名称的静态字典，
    /// 用于目标检测和图像分类任务中常用的数据集。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// // Get COCO class name for detection result
    /// int classId = 0;
    /// string className = ClassNames.CocoClassNames[classId]; // "person"
    /// 
    /// // Iterate over all COCO classes
    /// foreach (var kvp in ClassNames.CocoClassNames)
    /// {
    ///     Console.WriteLine($"{kvp.Key}: {kvp.Value}");
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public class ClassNames
    {
        /// <summary>
        /// COCO dataset class name mappings (80 classes).
        /// Key: Class ID (starting from 0)
        /// Value: Class name
        /// 
        /// COCO 数据集的 80 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称
        /// </summary>
        /// <remarks>
        /// <para>
        /// COCO (Common Objects in Context) is a large-scale object detection dataset
        /// containing 80 common object categories.
        /// </para>
        /// <para>
        /// COCO（上下文中的常见对象）是一个大规模目标检测数据集，
        /// 包含80个常见物体类别。
        /// </para>
        /// </remarks>
        /// <seealso cref="https://cocodataset.org/"/>
        public static readonly Dictionary<int, string> CocoClassNames = new Dictionary<int, string>
        {
            { 0, "person" },         { 1, "bicycle" },       { 2, "car" },            { 3, "motorcycle" },      { 4, "airplane" },
            { 5, "bus" },            { 6, "train" },         { 7, "truck" },          { 8, "boat" },            { 9, "traffic light" },
            { 10, "fire hydrant" },  { 11, "stop sign" },    { 12, "parking meter" }, { 13, "bench" },          { 14, "bird" },
            { 15, "cat" },           { 16, "dog" },          { 17, "horse" },         { 18, "sheep" },          { 19, "cow" },
            { 20, "elephant" },      { 21, "bear" },         { 22, "zebra" },         { 23, "giraffe" },        { 24, "backpack" },
            { 25, "umbrella" },      { 26, "handbag" },      { 27, "tie" },           { 28, "suitcase" },       { 29, "frisbee" },
            { 30, "skis" },          { 31, "snowboard" },    { 32, "sports ball" },   { 33, "kite" },           { 34, "baseball bat" },
            { 35, "baseball glove" },{ 36, "skateboard" },   { 37, "surfboard" },     { 38, "tennis racket" },  { 39, "bottle" },
            { 40, "wine glass" },    { 41, "cup" },          { 42, "fork" },          { 43, "knife" },          { 44, "spoon" },
            { 45, "bowl" },          { 46, "banana" },       { 47, "apple" },         { 48, "sandwich" },       { 49, "orange" },
            { 50, "broccoli" },      { 51, "carrot" },       { 52, "hot dog" },       { 53, "pizza" },          { 54, "donut" },
            { 55, "cake" },          { 56, "chair" },        { 57, "couch" },         { 58, "potted plant" },   { 59, "bed" },
            { 60, "dining table" },  { 61, "toilet" },       { 62, "tv" },            { 63, "laptop" },         { 64, "mouse" },
            { 65, "remote" },        { 66, "keyboard" },     { 67, "cell phone" },    { 68, "microwave" },      { 69, "oven" },
            { 70, "toaster" },       { 71, "sink" },         { 72, "refrigerator" },  { 73, "book" },           { 74, "clock" },
            { 75, "vase" },          { 76, "scissors" },     { 77, "teddy bear" },    { 78, "hair drier" },     { 79, "toothbrush" }
        };

        /// <summary>
        /// PASCAL VOC dataset class name mappings (20 classes).
        /// Key: Class ID (starting from 1, 0 is typically background)
        /// Value: Class name
        /// 
        /// PASCAL VOC 数据集的 20 个类别名称映射。
        /// Key: 类别ID (从1开始, 0通常为背景)
        /// Value: 类别名称
        /// </summary>
        /// <remarks>
        /// <para>
        /// PASCAL VOC (Visual Object Classes) is a benchmark dataset for object detection
        /// and image segmentation with 20 object categories.
        /// </para>
        /// <para>
        /// PASCAL VOC（视觉对象类别）是一个目标检测和图像分割的基准数据集，
        /// 包含20个物体类别。
        /// </para>
        /// </remarks>
        /// <seealso cref="http://host.robots.ox.ac.uk/pascal/VOC/"/>
        public static readonly Dictionary<int, string> VocClassNames = new Dictionary<int, string>
        {
            { 1, "aeroplane" }, { 2, "bicycle" }, { 3, "bird" }, { 4, "boat" }, { 5, "bottle" },
            { 6, "bus" }, { 7, "car" }, { 8, "cat" }, { 9, "chair" }, { 10, "cow" },
            { 11, "diningtable" }, { 12, "dog" }, { 13, "horse" }, { 14, "motorbike" }, { 15, "person" },
            { 16, "pottedplant" }, { 17, "sheep" }, { 18, "sofa" }, { 19, "train" }, { 20, "tvmonitor" }
        };

        /// <summary>
        /// CIFAR-10 dataset class name mappings (10 classes).
        /// Key: Class ID (starting from 0)
        /// Value: Class name
        /// 
        /// CIFAR-10 数据集的 10 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称
        /// </summary>
        /// <remarks>
        /// <para>
        /// CIFAR-10 is a dataset of 60,000 32x32 color images in 10 classes,
        /// commonly used for image classification benchmarking.
        /// </para>
        /// <para>
        /// CIFAR-10是一个包含60000张32x32彩色图像的数据集，分为10个类别，
        /// 常用于图像分类基准测试。
        /// </para>
        /// </remarks>
        /// <seealso cref="https://www.cs.toronto.edu/~kriz/cifar.html"/>
        public static readonly Dictionary<int, string> Cifar10ClassNames = new Dictionary<int, string>
        {
            { 0, "airplane" }, { 1, "automobile" }, { 2, "bird" }, { 3, "cat" }, { 4, "deer" },
            { 5, "dog" }, { 6, "frog" }, { 7, "horse" }, { 8, "ship" }, { 9, "truck" }
        };

  

        /// <summary>
        /// CIFAR-100 dataset class name mappings (100 classes).
        /// Key: Class ID (starting from 0)
        /// Value: Class name
        /// 
        /// CIFAR-100 数据集的 100 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称
        /// </summary>
        /// <remarks>
        /// <para>
        /// CIFAR-100 is a dataset of 60,000 32x32 color images in 100 classes,
        /// with 600 images per class. This is a more challenging classification
        /// task than CIFAR-10 due to the increased number of fine-grained categories.
        /// </para>
        /// <para>
        /// CIFAR-100是一个包含60000张32x32彩色图像的数据集，分为100个类别，
        /// 每个类别600张图像。由于细粒度类别数量增加，这是一个比CIFAR-10
        /// 更具挑战性的分类任务。
        /// </para>
        /// </remarks>
        /// <seealso cref="https://www.cs.toronto.edu/~kriz/cifar.html"/>
        public static readonly Dictionary<int, string> Cifar100ClassNames = new Dictionary<int, string>
        {
            { 0, "apple" }, { 1, "aquarium_fish" }, { 2, "baby" }, { 3, "bear" }, { 4, "beaver" },
            { 5, "bed" }, { 6, "bee" }, { 7, "beetle" }, { 8, "bicycle" }, { 9, "bottle" },
            { 10, "bowl" }, { 11, "boy" }, { 12, "bridge" }, { 13, "bus" }, { 14, "butterfly" },
            { 15, "camel" }, { 16, "can" }, { 17, "castle" }, { 18, "caterpillar" }, { 19, "cattle" },
            { 20, "chair" }, { 21, "chimpanzee" }, { 22, "clock" }, { 23, "cloud" }, { 24, "cockroach" },
            { 25, "couch" }, { 26, "crab" }, { 27, "crocodile" }, { 28, "cruise_ship" }, { 29, "cup" },
            { 30, "dinosaur" }, { 31, "dolphin" }, { 32, "elephant" }, { 33, "flatfish" }, { 34, "forest" },
            { 35, "fox" }, { 36, "girl" }, { 37, "hamster" }, { 38, "house" }, { 39, "kangaroo" },
            { 40, "computer_keyboard" }, { 41, "lamp" }, { 42, "lawn_mower" }, { 43, "leopard" }, { 44, "lion" },
            { 45, "lizard" }, { 46, "lobster" }, { 47, "man" }, { 48, "maple_tree" }, { 49, "motorcycle" },
            { 50, "mountain" }, { 51, "mouse" }, { 52, "mushroom" }, { 53, "oak_tree" }, { 54, "orange" },
            { 55, "orchid" }, { 56, "otter" }, { 57, "palm_tree" }, { 58, "pear" }, { 59, "pickup_truck" },
            { 60, "pine_tree" }, { 61, "plain" }, { 62, "plate" }, { 63, "poppy" }, { 64, "porcupine" },
            { 65, "possum" }, { 66, "rabbit" }, { 67, "raccoon" }, { 68, "ray" }, { 69, "road" },
            { 70, "rocket" }, { 71, "rose" }, { 72, "sea" }, { 73, "seal" }, { 74, "shark" },
            { 75, "shrew" }, { 76, "skunk" }, { 77, "skyscraper" }, { 78, "snail" }, { 79, "snake" },
            { 80, "spider" }, { 81, "squirrel" }, { 82, "streetcar" }, { 83, "sunflower" }, { 84, "sweet_pepper" },
            { 85, "table" }, { 86, "tank" }, { 87, "telephone" }, { 88, "television" }, { 89, "tiger" },
            { 90, "tractor" }, { 91, "train" }, { 92, "trout" }, { 93, "tulip" }, { 94, "turtle" },
            { 95, "wardrobe" }, { 96, "whale" }, { 97, "willow_tree" }, { 98, "wolf" }, { 99, "woman" }
        };
    }
}
