# Object Detection Tutorial
# 目标检测教程

## Overview / 概述

Object detection is a computer vision task that identifies objects in images and provides their locations using bounding boxes.

目标检测是一项计算机视觉任务，用于识别图像中的物体并使用边界框提供其位置。

## Supported Models / 支持的模型

| Model | Description | Best For |
|-------|-------------|----------|
| YOLOv5 | Fast, accurate | Real-time applications |
| YOLOv8 | State-of-the-art | Balanced speed/accuracy |
| YOLOv11 | Latest YOLO | Highest accuracy |
| RT-DETR | Transformer-based | Complex scenes |
| DEIMv2 | Advanced detector | Small objects |

## Basic Usage / 基本用法

### YOLOv8 Detection / YOLOv8 检测

```csharp
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load image
using var image = Image.Load<Rgb24>("street.jpg");

// Configure model
var config = new Yolov8DetConfig("yolov8n.onnx")
{
    ConfidenceThreshold = 0.5f,  // Minimum confidence score
    NmsThreshold = 0.45f         // Non-maximum suppression threshold
};

// Create model
using var model = new Yolov8DetModel(config);

// Run detection
var detections = model.Predict(image);

// Process results
foreach (var det in detections)
{
    Console.WriteLine($"Detected {det.Category} " +
                      $"with confidence {det.Confidence:F2} " +
                      $"at {det.Bounds}");
}
```

## Configuration Options / 配置选项

```csharp
var config = new Yolov8DetConfig("model.onnx")
{
    // Confidence threshold (0-1)
    // 置信度阈值 (0-1)
    ConfidenceThreshold = 0.5f,
    
    // NMS IoU threshold
    // NMS IoU 阈值
    NmsThreshold = 0.45f,
    
    // Input image size
    // 输入图像大小
    ImageSize = new Size(640, 640),
    
    // Class names
    // 类别名称
    Labels = new[] { "person", "car", "dog", ... },
    
    // Inference backend
    // 推理后端
    Backend = InferenceBackend.OpenVINO
};
```

## Visualization / 可视化

```csharp
using DeploySharp.ImageSharp.Data;

// Draw detections on image
var options = new VisualizeOptions
{
    Thickness = 2,                    // Box thickness
    FontSize = 16,                    // Label font size
    ShowConfidence = true,            // Show confidence scores
    ShowLabel = true,                 // Show class labels
    ColorMap = ColorMap.COCO          // Color scheme
};

var resultImage = Visualize.DrawDetResult(
    detections, 
    image, 
    options
);

resultImage.Save("output.jpg");
```

## Batch Processing / 批处理

```csharp
// Process multiple images
var images = new[] { "img1.jpg", "img2.jpg", "img3.jpg" };

foreach (var path in images)
{
    using var img = Image.Load<Rgb24>(path);
    var results = model.Predict(img);
    
    // Save results
    var vis = Visualize.DrawDetResult(results, img, options);
    vis.Save($"result_{Path.GetFileName(path)}");
}
```

## Performance Tips / 性能提示

1. **Use GPU acceleration when available**
   ```csharp
   config.SetTargetInferenceBackend(InferenceBackend.OpenVINO);
   config.Device = Device.GPU;
   ```

2. **Adjust image size for speed/accuracy trade-off**
   ```csharp
   config.ImageSize = new Size(320, 320);  // Faster
   config.ImageSize = new Size(1280, 1280); // More accurate
   ```

3. **Enable model caching**
   ```csharp
   config.CacheModel = true;
   ```

## Advanced: Custom Preprocessing / 高级：自定义预处理

```csharp
// Custom preprocessing pipeline
var processor = new DataProcessorConfig
{
    ResizeMode = ImageResizeMode.LetterBox,
    NormalizationType = NormalizationType.ImageNet,
    Mean = new[] { 0.485f, 0.456f, 0.406f },
    Std = new[] { 0.229f, 0.224f, 0.225f }
};

config.Preprocessor = processor;
```

## Complete Example / 完整示例

```csharp
using System;
using System.Linq;
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using DeploySharp.ImageSharp.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

class DetectionDemo
{
    static void Main()
    {
        // Configuration
        string modelPath = "yolov8n.onnx";
        string imagePath = "input.jpg";
        string outputPath = "output.jpg";
        
        // Load image
        using var image = Image.Load<Rgb24>(imagePath);
        
        // Create model
        var config = new Yolov8DetConfig(modelPath)
        {
            ConfidenceThreshold = 0.5f,
            NmsThreshold = 0.45f
        };
        
        using var model = new Yolov8DetModel(config);
        
        // Detect
        var detections = model.Predict(image);
        
        // Print results
        Console.WriteLine($"Found {detections.Count} objects:");
        foreach (var det in detections)
        {
            Console.WriteLine($"  - {det.Category}: {det.Confidence:P0} " +
                            $"at ({det.Bounds.X}, {det.Bounds.Y}) " +
                            $"size {det.Bounds.Width}x{det.Bounds.Height}");
        }
        
        // Visualize
        var options = new VisualizeOptions { ShowConfidence = true };
        var result = Visualize.DrawDetResult(detections, image, options);
        result.Save(outputPath);
        
        Console.WriteLine($"Saved to {outputPath}");
    }
}
```

## See Also / 另请参阅

- [Image Segmentation](image-segmentation.md)
- [Pose Estimation](pose-estimation.md)
- [Best Practices](best-practices.md)
