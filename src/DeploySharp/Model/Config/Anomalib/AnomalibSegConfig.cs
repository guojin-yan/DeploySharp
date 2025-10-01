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
    // 定义对应的数据模型类
    public class TransformConfig
    {
        [JsonPropertyName("__class_fullname__")]
        public string ClassName { get; set; } = "Compose";

        [JsonPropertyName("height")]
        public int? Height { get; set; } = 224;

        [JsonPropertyName("width")]
        public int? Width { get; set; } = 224;

        [JsonPropertyName("mean")]
        public List<float> Mean { get; set; } = [0.485f, 0.456f, 0.406f];

        [JsonPropertyName("std")]
        public List<float> Std { get; set; } = [0.229f, 0.224f, 0.225f];

        [JsonPropertyName("max_pixel_value")]
        public float? MaxPixelValue { get; set; } = 255.0f;

        [JsonPropertyName("interpolation")]
        public int? Interpolation { get; set; } = 1;

        [JsonPropertyName("transpose_mask")]
        public bool? TransposeMask { get; set; } = false;
    }

    public class ComposeConfig
    {
        [JsonPropertyName("transforms")]
        public List<TransformConfig> Transforms { get; set; }
    }

    public class RootTransform
    {
        [JsonPropertyName("transform")]
        public ComposeConfig Transform { get; set; }

        [JsonPropertyName("__version__")]
        public string Version { get; set; } = "1.4.18";
    }

    public class MetaDataConfig
    {
        [JsonPropertyName("task")]
        public string Task { get; set; } = "segmentation";

        [JsonPropertyName("transform")]
        public RootTransform Transform { get; set; }

        [JsonPropertyName("image_threshold")]
        public float ImageThreshold { get; set; } = 10.361607551574707f;

        [JsonPropertyName("pixel_threshold")]
        public float PixelThreshold { get; set; } = 15.020999908447266f;

        [JsonPropertyName("min")]
        public float MinValue { get; set; } = 4.609287261962891f;

        [JsonPropertyName("max")]
        public float MaxValue { get; set; } = 47.419090270996094f;
    }


    public static class JsonConfigParser
    {
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

        // 打印配置信息的实用方法
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


    public class AnomalibSegConfig : IImgConfig
    {

        public MetaDataConfig MetaData = new MetaDataConfig();
        public bool UseMetaData = false;
        public AnomalibSegConfig() { }

        public AnomalibSegConfig(string modelPath,
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
