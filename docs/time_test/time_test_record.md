# DeploySharp Time Test Record







## 使用 JYPPX.DeploySharp.OpenCvSharp 部署 PP-OCR，性能测试

下表为使用洗发水图片，跑10次的平均时间测试：

| 推理引擎              | 设备                | 平均耗时 | 设备类型                                                     |
| --------------------- | ------------------- | -------- | ------------------------------------------------------------ |
| OpenVINO              | CPU                 | 288ms    | Intel(R) Core(TM) Ultra 9 288V  8核                          |
| OpenVINO              | IGPU                | 99ms     | Intel(R) Arc(TM) 140V GPU (16GB)                             |
| OpenVINO              | 混合 AUTO：IGPU+CPU | 100ms    | Intel(R) Core(TM) Ultra 9 288V  8核  <br>Intel(R) Arc(TM) 140V GPU (16GB) |
| ONNX Runtime          | CPU                 | 656ms    | AMD Ryzen 7 5800H with Radeon Graphics 8核                   |
| ONNX Runtime DML      | GPU                 | 114ms    | NVIDIA GeForce RTX 3060 Laptop GPU                           |
| ONNX Runtime DML      | IGPU                | 331ms    | Intel(R) Arc(TM) 140V GPU (16GB)                             |
| ONNX Runtime CUDA     | GPU                 | 93ms     | NVIDIA GeForce RTX 3060 Laptop GPU                           |
| ONNX Runtime TensorRT | GPU                 | 52ms     | NVIDIA GeForce RTX 3060 Laptop GPU                           |
| TensorRTSharp         | GPU                 | 51ms     | NVIDIA GeForce RTX 3060 Laptop GPU                           |







#### 测试设备1

|     推理引擎     | 设备 | 设备类型                                         | PP-OCR v4 推理时间 | PP-OCR v5 推理时间 |
| :--------------: | ---- | ------------------------------------------------ | :----------------: | :----------------: |
|     OpenVINO     | CPU  | Intel Core Ultra 9 288V 8核                      |       81 ms        |       148 ms       |
|     OpenVINO     | IGPU | Intel Arc 140V GPU (16GB)                        |       46 ms        |       61 ms        |
|     OpenVINO     | AUTO | Intel Core Ultra 9 288V 8核 + Intel Arc 140V GPU |       47 ms        |       62 ms        |
| ONNX Runtime DML | IGPU | Intel Arc 140V GPU                               |       241 ms       |       188 ms       |

#### 测试设备2

| 推理引擎              | 设备 | 设备类型                | PP-OCR v4 推理时间 | PP-OCR v5 推理时间 |
| --------------------- | ---- | ----------------------- | :----------------: | :----------------: |
| OpenVINO              | CPU  | AMD Ryzen 7 5800H 8核   |        94ms        |       236ms        |
| ONNX Runtime          | CPU  | AMD Ryzen 7 5800H 8核   |       295ms        |       329 ms       |
| ONNX Runtime DML      | GPU  | NVIDIA GeForce RTX 3060 |        73ms        |        81ms        |
| ONNX Runtime CUDA     | GPU  | NVIDIA GeForce RTX 3060 |        62ms        |        62ms        |
| ONNX Runtime TensorRT | GPU  | NVIDIA GeForce RTX 3060 |        28ms        |       40 ms        |
| TensorRT              | GPU  | NVIDIA GeForce RTX 3060 |        29ms        |        49ms        |



#### 优化版本

| 推理引擎              | 设备 | 设备类型                | PP-OCR v4 推理时间 | PP-OCR v5 推理时间 |
| --------------------- | ---- | ----------------------- | :----------------: | :----------------: |
| ONNX Runtime DML      | GPU  | NVIDIA GeForce RTX 3060 |        60ms        |        59ms        |
| ONNX Runtime CUDA     | GPU  | NVIDIA GeForce RTX 3060 |        58ms        |        56ms        |
| ONNX Runtime TensorRT | GPU  | NVIDIA GeForce RTX 3060 |        25ms        |        26ms        |
| TensorRT              | GPU  | NVIDIA GeForce RTX 3060 |        23ms        |        36ms        |





#### 测试设备3


|     推理引擎     | 设备 | 设备类型                              | PP-OCR v4 推理时间 | PP-OCR v5 推理时间 |
| :--------------: | ---- | ------------------------------------- | ------------------ | ------------------ |
|   ONNX Runtime   | CPU  | AMD RYZEN AI MAX+ 395 w/ Radeon 8060S |                    | 239ms              |
|     OpenVINO     | CPU  | AMD RYZEN AI MAX+ 395 w/ Radeon 8060S |                    | 139ms              |
| ONNX Runtime DML | GPU  | AMD Radeon(TM) 8060S Graphics         |                    | 68ms               |




|       推理引擎        | 设备 | 设备类型                                                     | PP-OCR v4 推理时间 | PP-OCR v5 推理时间 |
| :-------------------: | :--: | ------------------------------------------------------------ | ------------------ | ------------------ |
|       OpenVINO        | CPU  | Intel(R) Core(TM) i5-12450H (2.00 GHz)                       |                    | 439ms              |
|       OpenVINO        | IGPU | Intel UHD Graphics 770                                       |                    | 336ms              |
|       OpenVINO        | AUTO | Intel(R) Core(TM) i5-12450H (2.00 GHz)+Intel UHD Graphics 770 |                    | 341ms              |
|      OnnxRuntime      | CPU  | Intel(R) Core(TM) i5-12450H (2.00 GHz)                       |                    | 2078ms             |
|   ONNX Runtime CUDA   | GPU  | NVIDIA GeForce RTX4050 Laptop                                |                    | 108ms              |
| ONNX Runtime TensorRT | GPU  | NVIDIA GeForce RTX4050 Laptop                                |                    | 56ms               |
|   ONNX Runtime DML    | GPU  | NVIDIA GeForce RTX4050 Laptop                                |                    | 95ms               |
|                       |      |                                                              |                    |                    |

