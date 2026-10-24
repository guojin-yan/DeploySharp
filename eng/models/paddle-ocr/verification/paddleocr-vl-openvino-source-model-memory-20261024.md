# PaddleOCR-VL OpenVINO 源 Model 生命周期：自动对照

两次运行的六任务、全部 765 Token/文本、图和 KV 长度均匹配固定已验参考；下列仅为两次顺序运行的进程内存观察。

改动将 OpenVINO 原始 Model 的生命周期缩短到编译及元数据复制完成为止；CompiledModel、Core 和推理请求仍由 Session 管理。未改图、精度、生成参数、托管堆或分配器设置。

原始记录：[before](paddleocr-vl-openvino-source-retained-linux-20261024.json)、[after](paddleocr-vl-openvino-source-released-linux-20261024.json)。

Ubuntu 22.04.5 x64 / i7-1165G7 / 16 GB、无 Swap / .NET 10.0.9 / OpenVINO 2026.3.1；CPU Latency、4 线程、1 stream、1 request、每图一个 Session。两次均使用 MALLOC_ARENA_MAX=2、DOTNET_GCHeapHardLimit=0x40000000；该 1 GiB 只限制托管堆。

| 位置 | 保留源 Model GiB | 提前释放源 Model GiB | 差值 GiB |
| --- | ---: | ---: | ---: |
| 四图加载完成 RSS | 6.770 | 6.007 | -0.764 |
| ocr-demo-1 后 RSS | 7.974 | 7.845 | -0.129 |
| table-official-example 后 RSS | 7.728 | 7.765 | 0.037 |
| formula-official-example 后 RSS | 8.040 | 8.048 | 0.008 |
| chart-official-example 后 RSS | 8.634 | 8.594 | -0.040 |
| spotting-demo-1 后 RSS | 11.655 | 11.494 | -0.161 |
| seal-official-example 后 RSS | 11.521 | 11.336 | -0.185 |
| 进程峰值 RSS | 11.861 | 11.616 | -0.245 |

先 before 后 after，各运行一次；没有随机配对、连续时钟/负载遥测或长期 soak。不得解释为泄漏定位、最优内存上界、吞吐/延迟排名、5+50 性能或标注质量。托管堆预算不限制原生和进程总内存。

两份报告的 sourceCommit 都是开发基线 c6c01d6d79000435c5f6fd84abcb9baf37af3b43，不是干净 checkout：before 仅增加同一测试采样，after 再增加上述后端改动。构建还包含未提交的既有其它测试修改；实际运行身份以报告中的五个程序集 SHA 和十三个 native SHA 为准。

完整模型、输入、KV 和环境定义见[原始六任务验证](paddleocr-vl-openvino-linux-20261024.md)。本对照不撤销原始默认异步配置的 OOM 记录；Windows、页面联合质量、其它后端、正式性能及稳定性仍需单独验证。

复算：

```powershell
pwsh -NoProfile -File eng/models/paddle-ocr/scripts/Compare-PaddleOcrVlOpenVinoMemory.ps1 `
  -BeforePath eng/models/paddle-ocr/verification/paddleocr-vl-openvino-source-retained-linux-20261024.json `
  -AfterPath eng/models/paddle-ocr/verification/paddleocr-vl-openvino-source-released-linux-20261024.json `
  -OutputJsonPath artifacts/checks/paddleocr-vl-openvino-memory.json `
  -OutputMarkdownPath artifacts/checks/paddleocr-vl-openvino-memory.md
```
