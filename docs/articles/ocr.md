# OCR (Optical Character Recognition)
# OCR（光学字符识别）

## Overview / 概述

DeploySharp supports PaddleOCR for text detection, classification, and recognition in images.

DeploySharp 支持 PaddleOCR 进行图像中的文本检测、分类和识别。

## OCR Pipeline / OCR 流程

PaddleOCR uses a three-stage pipeline:

PaddleOCR 使用三阶段流程：

1. **Detection (DBNet)** - Find text regions / 检测文本区域
2. **Classification** - Correct text orientation / 校正文本方向
3. **Recognition (CRNN)** - Convert to text / 转换为文本

## Quick Start / 快速开始

### Using High-Level API / 使用高级 API

```csharp
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.OpenCvSharp.Model;
using OpenCvSharp;

// Load image
Mat image = Cv2.ImRead("document.jpg");

// Create OCR predictor
var config = new PaddleOCRConfig
{
    DetModelPath = "ch_PP-OCRv4_det_infer.onnx",
    ClsModelPath = "ch_ppocr_mobile_v2.0_cls_infer.onnx",
    RecModelPath = "ch_PP-OCRv4_rec_infer.onnx",
    LabelPath = "ppocr_keys_v1.txt"
};

using var ocr = new PaddleOcrPredictor(config);

// Run OCR
var results = ocr.Predict(image);

// Process results
foreach (var text in results)
{
    Console.WriteLine($"Text: {text.Text}");
    Console.WriteLine($"Confidence: {text.Confidence:F2}");
    Console.WriteLine($"Position: {text.Box}");
}
```

### Using Individual Stages / 使用单独阶段

```csharp
// 1. Text Detection / 文本检测
var detConfig = new PPOcrDetConfig("det_model.onnx");
using var detector = new PPOcrDet(detConfig);
var textBoxes = detector.Predict(image);

// 2. Text Classification (optional) / 文本分类（可选）
var clsConfig = new PPOcrClsConfig("cls_model.onnx");
using var classifier = new PPOcrCls(clsConfig);

// 3. Text Recognition / 文本识别
var recConfig = new PPOcrRecConfig("rec_model.onnx", "labels.txt");
using var recognizer = new PPOcrRec(recConfig);

// Process each text region
foreach (var box in textBoxes)
{
    // Extract region
    var region = ExtractRegion(image, box);
    
    // Classify and rotate if needed
    var (angle, clsScore) = classifier.Predict(region);
    if (angle == 180)
        region = Rotate180(region);
    
    // Recognize text
    var (text, recScore) = recognizer.Predict(region);
    
    Console.WriteLine($"Detected: {text} (confidence: {recScore:F2})");
}
```

## Configuration / 配置

```csharp
var config = new PaddleOCRConfig
{
    // Model paths
    DetModelPath = "det_infer.onnx",      // Detection model
    ClsModelPath = "cls_infer.onnx",      // Classification model (optional)
    RecModelPath = "rec_infer.onnx",      // Recognition model
    
    // Labels file for recognition
    LabelPath = "ch_sim_dict.txt",         // Character dictionary
    
    // Detection parameters
    DetDbThreshold = 0.3f,                 // Binary threshold
    DetDbBoxThreshold = 0.5f,              // Box threshold
    DetUnclipRatio = 1.6f,                 // Box expansion ratio
    
    // Classification parameters
    UseCls = true,                         // Enable classification
    ClsThreshold = 0.9f,                   // Classification threshold
    
    // Recognition parameters
    RecBatchNum = 6,                       // Batch size for recognition
    
    // General parameters
    UseGpu = false                         // Use GPU acceleration
};
```

## Visualization / 可视化

```csharp
using DeploySharp.OpenCvSharp.Data;

// Draw text boxes
var visImage = Visualize.DrawOcrResult(results, image);
Cv2.ImShow("OCR Result", visImage);
Cv2.WaitKey();

// Save result
Cv2.ImWrite("ocr_result.jpg", visImage);
```

## Multi-Language Support / 多语言支持

| Language | Model | Dictionary |
|----------|-------|------------|
| Chinese (Simplified) | ch_PP-OCRv4 | ppocr_keys_v1.txt |
| English | en_PP-OCRv4 | en_dict.txt |
| Japanese | japan_PP-OCRv3 | japan_dict.txt |
| Korean | korean_PP-OCRv3 | korean_dict.txt |

## Performance Tips / 性能提示

```csharp
// Disable classification for faster inference
config.UseCls = false;

// Increase batch size for recognition
config.RecBatchNum = 10;

// Adjust detection thresholds
config.DetDbThreshold = 0.5f;  // Higher = fewer detections

// Use GPU
config.UseGpu = true;
```

## Complete Example / 完整示例

```csharp
using System;
using System.Linq;
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.OpenCvSharp.Model;
using DeploySharp.OpenCvSharp.Data;
using OpenCvSharp;

class OcrDemo
{
    static void Main()
    {
        // Configuration
        var config = new PaddleOCRConfig
        {
            DetModelPath = "ch_PP-OCRv4_det_infer.onnx",
            ClsModelPath = "ch_ppocr_mobile_v2.0_cls_infer.onnx",
            RecModelPath = "ch_PP-OCRv4_rec_infer.onnx",
            LabelPath = "ppocr_keys_v1.txt",
            UseCls = true
        };
        
        // Load image
        Mat image = Cv2.ImRead("document.jpg");
        
        // Run OCR
        using var ocr = new PaddleOcrPredictor(config);
        var results = ocr.Predict(image);
        
        // Display results
        Console.WriteLine($"Found {results.Count} text regions:");
        foreach (var result in results)
        {
            Console.WriteLine($"  Text: {result.Text}");
            Console.WriteLine($"  Confidence: {result.Confidence:F2}");
            Console.WriteLine();
        }
        
        // Visualize
        var vis = Visualize.DrawOcrResult(results, image);
        Cv2.ImWrite("ocr_output.jpg", vis);
    }
}
```

## Troubleshooting / 故障排除

| Issue | Solution |
|-------|----------|
| Low accuracy | Check dictionary file matches model |
| Missing text | Lower DetDbThreshold |
| Wrong orientation | Enable classification (UseCls = true) |
| Slow speed | Disable classification, increase batch size |
