# Pose Estimation Tutorial
# 姿态估计教程

## Overview / 概述

Pose estimation detects human body keypoints (joints) in images, enabling applications like fitness tracking and gesture recognition.

姿态估计检测图像中的人体关键点（关节），支持健身追踪和手势识别等应用。

## Supported Models / 支持的模型

| Model | Keypoints | Best For |
|-------|-----------|----------|
| YOLOv8-Pose | 17 (COCO) | General purpose |
| YOLOv11-Pose | 17 (COCO) | Latest version |
| YOLOv26-Pose | 17 (COCO) | Advanced features |

## COCO Keypoints / COCO 关键点

```
0: Nose            鼻子
1-2: Eyes          眼睛
3-4: Ears          耳朵
5-6: Shoulders     肩膀
7-8: Elbows        肘部
9-10: Wrists       手腕
11-12: Hips        髋部
13-14: Knees       膝盖
15-16: Ankles      脚踝
```

## Basic Usage / 基本用法

```csharp
using DeploySharp.Data;
using DeploySharp.Model;
using DeploySharp.ImageSharp.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load image
using var image = Image.Load<Rgb24>("person.jpg");

// Configure pose model
var config = new Yolov8PoseConfig("yolov8n-pose.onnx")
{
    ConfidenceThreshold = 0.5f,
    KeypointThreshold = 0.3f  // Minimum keypoint confidence
};

// Create model
using var model = new Yolov8PoseModel(config);

// Run pose estimation
var poses = model.Predict(image);

// Process results
foreach (var pose in poses)
{
    Console.WriteLine($"Detected person with confidence {pose.Confidence:F2}");
    
    foreach (var kp in pose.Keypoints)
    {
        Console.WriteLine($"  {kp.Name}: ({kp.X:F1}, {kp.Y:F1}) " +
                         $"confidence {kp.Confidence:F2}");
    }
}
```

## Visualization / 可视化

```csharp
using DeploySharp.ImageSharp.Data;

// Draw poses with skeleton
var options = new VisualizeOptions
{
    SkeletonThickness = 2,
    KeypointRadius = 3,
    ShowKeypointLabels = false
};

var resultImage = Visualize.DrawPoseResult(poses, image, options);
resultImage.Save("poses.jpg");
```

## Working with Keypoints / 处理关键点

```csharp
foreach (var pose in poses)
{
    // Get specific keypoints
    var nose = pose.GetKeypoint("nose");
    var leftWrist = pose.GetKeypoint("left_wrist");
    var rightWrist = pose.GetKeypoint("right_wrist");
    
    // Calculate angles
    if (leftWrist != null && rightWrist != null)
    {
        var distance = CalculateDistance(leftWrist, rightWrist);
        Console.WriteLine($"Hand distance: {distance:F1}px");
    }
    
    // Check pose quality
    int visiblePoints = pose.Keypoints.Count(kp => kp.Confidence > 0.5);
    Console.WriteLine($"Visible keypoints: {visiblePoints}/17");
}
```

## Fall Detection Example / 跌倒检测示例

```csharp
bool IsFallDetected(PoseResult pose)
{
    var head = pose.GetKeypoint("nose");
    var leftAnkle = pose.GetKeypoint("left_ankle");
    var rightAnkle = pose.GetKeypoint("right_ankle");
    
    if (head == null || leftAnkle == null || rightAnkle == null)
        return false;
    
    // Calculate height-to-width ratio
    var avgAnkleY = (leftAnkle.Y + rightAnkle.Y) / 2;
    var height = Math.Abs(avgAnkleY - head.Y);
    var width = Math.Abs(leftAnkle.X - rightAnkle.X);
    
    // If height < width, person might be lying down
    return height < width * 0.8;
}
```

## Multi-Person Tracking / 多人追踪

```csharp
// Track poses across frames
var tracker = new PoseTracker();

foreach (var frame in videoFrames)
{
    var poses = model.Predict(frame);
    var tracked = tracker.Update(poses);
    
    foreach (var person in tracked)
    {
        Console.WriteLine($"Person {person.Id}: {person.Pose.Bounds}");
    }
}
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

class PoseEstimationDemo
{
    static void Main()
    {
        string modelPath = "yolov8n-pose.onnx";
        string imagePath = "people.jpg";
        
        using var image = Image.Load<Rgb24>(imagePath);
        
        var config = new Yolov8PoseConfig(modelPath)
        {
            ConfidenceThreshold = 0.5f,
            KeypointThreshold = 0.3f
        };
        
        using var model = new Yolov8PoseModel(config);
        var poses = model.Predict(image);
        
        Console.WriteLine($"Detected {poses.Count} people");
        
        foreach (var pose in poses)
        {
            var visibleJoints = pose.Keypoints.Count(k => k.Confidence > 0.3);
            Console.WriteLine($"  Person: {visibleJoints}/17 joints visible");
        }
        
        // Visualize
        var options = new VisualizeOptions();
        var output = Visualize.DrawPoseResult(poses, image, options);
        output.Save("output_poses.jpg");
    }
}
```
