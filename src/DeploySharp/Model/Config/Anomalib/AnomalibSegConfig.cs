using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Configuration classes for Anomalib segmentation model
    /// Anomalib分割模型的配置类
    /// </summary>

    /// <summary>
    /// Represents transformation configuration parameters
    /// 表示转换配置参数
    /// </summary>
    /// <remarks>
    /// Maps to JSON transform configuration structure used by Anomalib.
    /// 映射到Anomalib使用的JSON转换配置结构。
    /// </remarks>
    public class TransformConfig
    {
        /// <summary>
        /// Class name identifier (default: "Compose")
        /// 类名标识符(默认:"Compose")
        /// </summary>
        [JsonPropertyName("__class_fullname__")]
        public string ClassName { get; set; } = "Compose";

        /// <summary>
        /// Target image height in pixels (default: 224)
        /// 目标图像高度(像素，默认:224)
        /// </summary>
        [JsonPropertyName("height")]
        public int? Height { get; set; } = 224;

        /// <summary>
        /// Target image width in pixels (default: 224)
        /// 目标图像宽度(像素，默认:224)
        /// </summary>
        [JsonPropertyName("width")]
        public int? Width { get; set; } = 224;

        /// <summary>
        /// Mean values for normalization (default: ImageNet values)
        /// 归一化的均值(默认:ImageNet值)
        /// </summary>
        [JsonPropertyName("mean")]
        public List<float> Mean { get; set; } = [0.485f, 0.456f, 0.406f];

        /// <summary>
        /// Standard deviation values for normalization (default: ImageNet values)
        /// 归一化的标准差(默认:ImageNet值)
        /// </summary>
        [JsonPropertyName("std")]
        public List<float> Std { get; set; } = [0.229f, 0.224f, 0.225f];

        /// <summary>
        /// Maximum pixel value scaling factor (default: 255.0)
        /// 最大像素值缩放因子(默认:255.0)
        /// </summary>
        [JsonPropertyName("max_pixel_value")]
        public float? MaxPixelValue { get; set; } = 255.0f;

        /// <summary>
        /// Interpolation method code (default: 1)
        /// 插值方法代码(默认:1)
        /// </summary>
        [JsonPropertyName("interpolation")]
        public int? Interpolation { get; set; } = 1;

        /// <summary>
        /// Whether to transpose masks (default: false)
        /// 是否转置掩码(默认:false)
        /// </summary>
        [JsonPropertyName("transpose_mask")]
        public bool? TransposeMask { get; set; } = false;
    }

    /// <summary>
    /// Composition of multiple transformation steps
    /// 多个转换步骤的组合
    /// </summary>
    public class ComposeConfig
    {
        /// <summary>
        /// List of transformations to apply sequentially
        /// 按顺序应用的转换列表
        /// </summary>
        [JsonPropertyName("transforms")]
        public List<TransformConfig> Transforms { get; set; }
    }

    /// <summary>
    /// Root transformation configuration container
    /// 根转换配置容器
    /// </summary>
    public class RootTransform
    {
        /// <summary>
        /// Main transformation pipeline configuration
        /// 主要的转换流水线配置
        /// </summary>
        [JsonPropertyName("transform")]
        public ComposeConfig Transform { get; set; }

        /// <summary>
        /// Anomalib version string
        /// Anomalib版本字符串
        /// </summary>
        [JsonPropertyName("__version__")]
        public string Version { get; set; } = "1.4.18";
    }

    /// <summary>
    /// Complete metadata configuration for anomaly detection
    /// 完整的异常检测元数据配置
    /// </summary>
    public class MetaDataConfig
    {
        /// <summary>
        /// Task type (default: "segmentation")
        /// 任务类型(默认:"segmentation")
        /// </summary>
        [JsonPropertyName("task")]
        public string Task { get; set; } = "segmentation";

        /// <summary>
        /// Root transformation configuration
        /// 根转换配置
        /// </summary>
        [JsonPropertyName("transform")]
        public RootTransform Transform { get; set; }

        /// <summary>
        /// Image-level anomaly threshold
        /// 图像级异常阈值
        /// </summary>
        [JsonPropertyName("image_threshold")]
        public float ImageThreshold { get; set; } = 10.361607551574707f;

        /// <summary>
        /// Pixel-level anomaly threshold
        /// 像素级异常阈值
        /// </summary>
        [JsonPropertyName("pixel_threshold")]
        public float PixelThreshold { get; set; } = 15.020999908447266f;

        /// <summary>
        /// Minimum observed value in training data
        /// 训练数据中的最小观测值
        /// </summary>
        [JsonPropertyName("min")]
        public float MinValue { get; set; } = 4.609287261962891f;

        /// <summary>
        /// Maximum observed value in training data
        /// 训练数据中的最大观测值
        /// </summary>
        [JsonPropertyName("max")]
        public float MaxValue { get; set; } = 47.419090270996094f;
    }

    /// <summary>
    /// Provides JSON configuration parsing utilities
    /// 提供JSON配置解析实用工具
    /// </summary>
    public static class JsonConfigParser
    {
        /// <summary>
        /// Parses JSON configuration file into MetaDataConfig object
        /// 将JSON配置文件解析为MetaDataConfig对象
        /// </summary>
        /// <param name="filePath">Path to JSON config file JSON配置文件路径</param>
        /// <returns>Deserialized configuration object 反序列化的配置对象</returns>
        /// <exception cref="ApplicationException">
        /// Thrown when JSON parsing fails JSON解析失败时抛出
        /// </exception>
        public static MetaDataConfig ParseConfig(string filePath)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                return JsonSerializer.Deserialize<MetaDataConfig>(jsonString, options);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to parse JSON config: {ex.Message}");
            }
        }

        /// <summary>
        /// Prints formatted configuration details to console
        /// 将格式化的配置详情打印到控制台
        /// </summary>
        /// <param name="config">Metadata configuration to print 要打印的元数据配置</param>
        public static void PrintConfigDetails(MetaDataConfig config)
        {
            Console.WriteLine($"Task: {config.Task}");
            Console.WriteLine($"Version: {config.Transform.Version}");
            Console.WriteLine($"Thresholds - Image: {config.ImageThreshold}, Pixel: {config.PixelThreshold}");
            Console.WriteLine($"Value Range: [{config.MinValue}, {config.MaxValue}]");

            Console.WriteLine("\nTransforms Pipeline:");
            foreach (var transform in config.Transform.Transform.Transforms)
            {
                Console.WriteLine($"\n{transform.ClassName}");

                if (transform.Height.HasValue && transform.Width.HasValue)
                {
                    Console.WriteLine($"  Size: {transform.Height}x{transform.Width}");
                }

                if (transform.Mean != null && transform.Std != null)
                {
                    Console.WriteLine($"  Normalization - Mean: [{string.Join(", ", transform.Mean)}], " +
                                     $"Std: [{string.Join(", ", transform.Std)}]");
                }

                if (transform.Interpolation.HasValue)
                {
                    Console.WriteLine($"  Interpolation: {transform.Interpolation}");
                }
            }
        }
    }

    /// <summary>
    /// Configuration class for Anomalib segmentation models
    /// Anomalib分割模型的配置类
    /// </summary>
    public class AnomalibSegConfig : IImgConfig
    {
        /// <summary>
        /// Metadata configuration loaded from JSON
        /// 从JSON加载的元数据配置
        /// </summary>
        public MetaDataConfig MetaData = new MetaDataConfig();

        /// <summary>
        /// Flag indicating whether metadata should be used
        /// 指示是否使用元数据的标志
        /// </summary>
        public bool UseMetaData = false;

        /// <summary>
        /// Initializes default configuration
        /// 初始化默认配置
        /// </summary>
        public AnomalibSegConfig() { }

        /// <summary>
        /// Initializes configuration with parameters
        /// 使用参数初始化配置
        /// </summary>
        /// <param name="modelPath">Path to model file 模型文件路径</param>
        /// <param name="metaDataConfigPath">Path to metadata config (optional) 元数据配置文件路径(可选)</param>
        /// <param name="inferenceBackend">Backend to use (default: OpenVINO) 使用的推理后端(默认:OpenVINO)</param>
        /// <param name="deviceType">Device to run on (default: CPU) 运行设备(默认:CPU)</param>
        /// <param name="inferBatch">Batch size (default: 1) 批量大小(默认:1)</param>
        /// <param name="resizeMode">Image resize mode (default: Pad) 图像调整大小模式(默认:Pad)</param>
        /// <param name="normalizationType">Normalization method (default: Scale_0_1) 归一化方法(默认:Scale_0_1)</param>
        public AnomalibSegConfig(
            string modelPath,
            string metaDataConfigPath = null,
            InferenceBackend inferenceBackend = InferenceBackend.OpenVINO,
            DeviceType deviceType = DeviceType.CPU,
            int inferBatch = 1,
            ImageResizeMode resizeMode = ImageResizeMode.Pad,
            ImageNormalizationType normalizationType = ImageNormalizationType.Scale_0_1)
        {
            if (metaDataConfigPath != null)
            {
                MetaData = JsonConfigParser.ParseConfig(metaDataConfigPath);
                JsonConfigParser.PrintConfigDetails(MetaData);
                UseMetaData = true;
            }
            this.ModelPath = modelPath;
            this.TargetInferenceBackend = inferenceBackend;
            this.TargetDeviceType = deviceType;
            this.InferBatch = inferBatch;

            this.DataProcessor.ResizeMode = resizeMode;
            this.DataProcessor.NormalizationType = normalizationType;
        }
    }

}
