# Best Practices
# 最佳实践

## Performance Optimization / 性能优化

### 1. Choose the Right Backend / 选择合适的后端

| Backend | CPU | GPU | Best For |
|---------|-----|-----|----------|
| OpenVINO | ✅ Fast | ✅ Optimized | Intel hardware |
| ONNX Runtime | ✅ Good | ✅ Flexible | Cross-platform |
| TensorRT | ❌ | ✅ Fastest | NVIDIA GPUs |

```csharp
// OpenVINO on Intel GPU
var config = new Yolov8DetConfig("model.onnx");
config.SetTargetInferenceBackend(InferenceBackend.OpenVINO);
config.Device = Device.GPU;  // Intel integrated/discrete GPU

// TensorRT on NVIDIA GPU
config.SetTargetInferenceBackend(InferenceBackend.TensorRT);
config.Device = Device.GPU;  // NVIDIA GPU
```

### 2. Optimize Input Size / 优化输入大小

```csharp
// Smaller input = faster inference
// 较小的输入 = 更快的推理
config.ImageSize = new Size(320, 320);

// Larger input = better accuracy
// 较大的输入 = 更好的精度
config.ImageSize = new Size(1280, 1280);

// Standard balanced option
// 标准平衡选项
config.ImageSize = new Size(640, 640);
```

### 3. Batch Processing / 批处理

```csharp
// Process multiple images at once for better throughput
// 一次处理多张图像以提高吞吐量
var batchImages = images.Select(img => LoadImage(img)).ToList();
var batchResults = model.PredictBatch(batchImages);
```

### 4. Model Warm-up / 模型预热

```csharp
// Run a dummy inference to warm up the model
// 运行虚拟推理以预热模型
var dummyInput = new DataTensor(new float[1, 3, 640, 640]);
model.Predict(dummyInput);  // First call is slower
```

## Memory Management / 内存管理

### 1. Use `using` Statements / 使用 `using` 语句

```csharp
// Good / 好的做法
using var model = new Yolov8DetModel(config);
using var image = Image.Load("photo.jpg");
var results = model.Predict(image);
// Resources automatically disposed / 资源自动释放

// Avoid / 避免
var model = new Yolov8DetModel(config);  // May leak memory
```

### 2. Dispose Large Objects Promptly / 及时释放大对象

```csharp
// For large batch processing
foreach (var imagePath in imagePaths)
{
    using var image = Image.Load(imagePath);
    var results = model.Predict(image);
    ProcessResults(results);
    // Image disposed immediately after use
    // 图像在使用后立即释放
}
```

### 3. Limit Concurrent Models / 限制并发模型

```csharp
// Don't load multiple large models simultaneously
// 不要同时加载多个大模型
var model1 = new Yolov8DetModel(config1);  // OK
// var model2 = new Yolov8DetModel(config2);  // May cause OOM

// Instead, process sequentially or use model pooling
// 改为顺序处理或使用模型池
```

## Error Handling / 错误处理

### 1. Handle Model Loading Errors / 处理模型加载错误

```csharp
try
{
    var config = new Yolov8DetConfig(modelPath);
    using var model = new Yolov8DetModel(config);
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Model file not found: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Model initialization failed: {ex.Message}");
}
```

### 2. Handle Inference Errors / 处理推理错误

```csharp
try
{
    var results = model.Predict(image);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid input: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Inference failed: {ex.Message}");
}
```

## Configuration Best Practices / 配置最佳实践

### 1. Use Configuration Files / 使用配置文件

```csharp
// Load from JSON
var json = File.ReadAllText("model_config.json");
var config = JsonSerializer.Deserialize<Yolov8DetConfig>(json);
```

### 2. Environment-Specific Settings / 环境特定设置

```csharp
var config = new Yolov8DetConfig(modelPath);

if (Environment.IsDevelopment())
{
    // Development: slower but more verbose
    config.LogLevel = LogLevel.Debug;
    config.Device = Device.CPU;  // For debugging
}
else
{
    // Production: optimized for speed
    config.LogLevel = LogLevel.Warning;
    config.Device = Device.GPU;
}
```

### 3. Confidence Threshold Tuning / 置信度阈值调优

```csharp
// High precision, lower recall
config.ConfidenceThreshold = 0.7f;

// Balanced
config.ConfidenceThreshold = 0.5f;

// High recall, may have false positives
config.ConfidenceThreshold = 0.3f;
```

## Logging and Monitoring / 日志和监控

### 1. Enable Performance Logging / 启用性能日志

```csharp
// Log inference time
var stopwatch = Stopwatch.StartNew();
var results = model.Predict(image);
stopwatch.Stop();
Console.WriteLine($"Inference time: {stopwatch.ElapsedMilliseconds}ms");

// Using built-in profiler
model.ModelInferenceProfiler.PrintAllRecords();
```

### 2. Structured Logging / 结构化日志

```csharp
var logger = LogManager.GetLogger(typeof(Program));
logger.Info($"Processing image: {imagePath}");
logger.Debug($"Input shape: {inputShape}");
```

## Deployment Checklist / 部署检查清单

- [ ] Model file included in deployment
- [ ] Runtime libraries installed (OpenVINO/CUDA/etc.)
- [ ] Correct architecture (x64) selected
- [ ] GPU drivers up to date (if using GPU)
- [ ] Sufficient memory available
- [ ] Error handling implemented
- [ ] Logging configured
- [ ] Performance tested

## Troubleshooting Guide / 故障排除指南

| Symptom | Possible Cause | Solution |
|---------|---------------|----------|
| Slow inference | CPU instead of GPU | Check Device setting |
| Out of memory | Batch too large | Reduce batch size |
| Wrong detections | Wrong input size | Match model training size |
| Model load fails | Missing dependencies | Install runtime packages |
| Crash on startup | Architecture mismatch | Build for x64 |

## Additional Resources / 其他资源

- [GitHub Issues](https://github.com/guojin-yan/DeploySharp/issues)
- [Model Zoo](https://github.com/guojin-yan/DeploySharp/releases)
- [Community QQ Group](http://qm.qq.com): 945057948
