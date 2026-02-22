# Image Segmentation Tutorial
# 图像分割教程

## Overview / 概述

Image segmentation assigns a class label to each pixel in an image, providing precise object boundaries.

图像分割为图像中的每个像素分配类别标签，提供精确的物体边界。

## Supported Models / 支持的模型

| Model | Type | Description |
|-------|------|-------------|
| YOLOv5-Seg | Instance | Fast instance segmentation |
| YOLOv8-Seg | Instance | State-of-the-art instance segmentation |
| YOLOv11-Seg | Instance | Latest YOLO segmentation |
| Anomalib | Anomaly | Industrial defect detection |
| RFDETR-Seg | Panoptic | Transformer-based segmentation |

## Instance Segmentation / 实例分割

### YOLOv8 Segmentation / YOLOv8 分割

```csharp
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load image
using var image = Image.Load<Rgb24>("scene.jpg");

// Configure segmentation model
var config = new Yolov8SegConfig("yolov8n-seg.onnx")
{
    ConfidenceThreshold = 0.5f,
    NmsThreshold = 0.45f
};

// Create model
using var model = new Yolov8SegModel(config);

// Run segmentation
var results = model.Predict(image);

// Process results
foreach (var seg in results)
{
    Console.WriteLine($"Class: {seg.Category}");
    Console.WriteLine($"Confidence: {seg.Confidence:F2}");
    Console.WriteLine($"Bounding Box: {seg.Bounds}");
    Console.WriteLine($"Mask pixels: {seg.Mask?.Length ?? 0}");
}
```

## Visualization / 可视化

```csharp
using DeploySharp.ImageSharp.Data;

// Draw segmentation masks
var options = new VisualizeOptions
{
    MaskAlpha = 0.5f,           // Mask transparency
    ShowBoundingBox = true,     // Show detection boxes
    ShowLabel = true,           // Show class labels
    ColorMap = ColorMap.COCO
};

var resultImage = Visualize.DrawSegResult(results, image, options);
resultImage.Save("segmented.jpg");
```

## Anomaly Detection / 异常检测

```csharp
// For industrial defect detection
var config = new AnomalibSegConfig("anomalib_model.onnx")
{
    ImageSize = new Size(256, 256),
    Threshold = 0.5f
};

using var model = new AnomalibSegModel(config);
var result = model.Predict(image);

// Check for anomalies
if (result.IsAnomalous)
{
    Console.WriteLine($"Anomaly detected! Score: {result.AnomalyScore:F2}");
    
    // Visualize anomaly map
    var vis = Visualize.DrawAnomalyResult(result, image);
    vis.Save("anomaly.jpg");
}
```

## Working with Masks / 处理掩膜

```csharp
foreach (var seg in results)
{
    // Get mask data
    var mask = seg.Mask;
    var width = seg.MaskWidth;
    var height = seg.MaskHeight;
    
    // Calculate mask area
    int area = mask.Count(p => p > 0.5f);
    
    // Get bounding box from mask
    var bbox = seg.Bounds;
    
    // Extract masked region
    var maskedRegion = ExtractMaskedRegion(image, mask, bbox);
}
```

## Complete Example / 完整示例

```csharp
using System;
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using DeploySharp.ImageSharp.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

class SegmentationDemo
{
    static void Main()
    {
        string modelPath = "yolov8n-seg.onnx";
        string imagePath = "street.jpg";
        
        using var image = Image.Load<Rgb24>(imagePath);
        
        var config = new Yolov8SegConfig(modelPath)
        {
            ConfidenceThreshold = 0.5f
        };
        
        using var model = new Yolov8SegModel(config);
        var results = model.Predict(image);
        
        Console.WriteLine($"Found {results.Count} instances");
        
        foreach (var seg in results)
        {
            Console.WriteLine($"  {seg.Category}: {seg.Confidence:F2}");
        }
        
        // Visualize with masks
        var options = new VisualizeOptions
        {
            MaskAlpha = 0.4f,
            ShowBoundingBox = true
        };
        
        var output = Visualize.DrawSegResult(results, image, options);
        output.Save("output_segmented.jpg");
    }
}
```
