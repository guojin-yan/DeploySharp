//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DeploySharp.Model
//{
//    public class PaddleOCRConfig
//    {
//        public PPOcrDetConfig DetConfig { get; set; }
//        public PPOcrClsConfig ClsConfig { get; set; }
//        public PPOcrRecConfig RecConfig { get; set; }

//        public PaddleOCRConfig(string detModelPath, string clsModelPath, string recModelPath, string dictPath)
//        {
//            DetConfig = new PPOcrDetConfig(detModelPath);
//            ClsConfig = new PPOcrClsConfig(clsModelPath);
//            RecConfig = new PPOcrRecConfig(recModelPath, dictPath);
//        }
//    }
//}
using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.IO;
using System.Text.Json;

namespace DeploySharp.Model
{
    /// <summary>
    /// PaddleOCR 全流程配置聚合类 (完全模块化版)
    /// Det、Cls、Rec 三个模块均独立可选
    /// </summary>
    public class PaddleOCRConfig
    {
        // --- 1. 子配置 (均为可空) ---

        /// <summary>
        /// 文本检测配置
        /// </summary>
        public PPOcrDetConfig DetConfig { get; set; }

        /// <summary>
        /// 文本方向分类配置
        /// </summary>
        public PPOcrClsConfig ClsConfig { get; set; }

        /// <summary>
        /// 文本识别配置
        /// </summary>
        public PPOcrRecConfig RecConfig { get; set; }


        // --- 2. 状态属性 (自动推断) ---

        /// <summary>
        /// 是否启用了检测模型
        /// </summary>
        public bool UseDet => DetConfig != null;

        /// <summary>
        /// 是否启用了分类模型
        /// </summary>
        public bool UseCls => ClsConfig != null;

        /// <summary>
        /// 是否启用了识别模型
        /// </summary>
        public bool UseRec => RecConfig != null;


        // --- 3. 全局控制属性 (仅作用于已加载的模块) ---

        private int _maxConcurrency = 4;
        /// <summary>
        /// Gets or sets the maximum number of concurrent operations allowed.
        /// 获取或设置允许的最大并发操作数。
        /// </summary>
        /// <remarks>
        /// Set this property to limit the number of operations that can run in parallel.
        /// Adjusting this value can help control resource usage and throughput.
        /// 设置此属性以限制可以并行运行的操作数量。调整此值可以帮助控制资源使用和吞吐量。
        /// </remarks>
        public int MaxConcurrency
        {
            get => _maxConcurrency;
            set
            {
                _maxConcurrency = value;
            }
        }

        private int _globalMaxBatchSize = 8;
        /// <summary>
        /// 全局最大批次大小。仅更新当前已加载的模型配置。
        /// </summary>
        public int GlobalMaxBatchSize
        {
            get => _globalMaxBatchSize;
            set
            {
                _globalMaxBatchSize = value;
                ApplyToActiveConfigs(c => c.MaxBatchSize = value);
            }
        }

        /// <summary>
        /// 全局推理后端。仅更新当前已加载的模型配置。
        /// </summary>
        public InferenceBackend GlobalInferenceBackend
        {
            set => ApplyToActiveConfigs(c => c.TargetInferenceBackend = value);
        }

        /// <summary>
        /// 全局设备类型。仅更新当前已加载的模型配置。
        /// </summary>
        public DeviceType GlobalDeviceType
        {
            set => ApplyToActiveConfigs(c => c.TargetDeviceType = value);
        }

        /// <summary>
        /// 全局ONNX Runtime硬件加速设备类型。仅更新当前已加载的模型配置。
        /// </summary>
        public OnnxRuntimeDeviceType GlobalOnnxRuntimeDeviceType
        {
            set => ApplyToActiveConfigs(c => c.TargetOnnxRuntimeDeviceType = value);
        }


        // --- 4. 构造函数 ---

        /// <summary>
        /// 默认构造函数：创建一个空配置，后续通过 Load 方法动态添加
        /// </summary>
        public PaddleOCRConfig() { }

        /// <summary>
        /// 快捷构造函数：支持任意路径组合，传 null 即表示不加载该模块
        /// </summary>
        public PaddleOCRConfig(
            string detModelPath = null,
            string clsModelPath = null,
            string recModelPath = null,
            string recDictPath = null)
        {
            LoadDet(detModelPath);
            LoadCls(clsModelPath);
            LoadRec(recModelPath, recDictPath);
        }


        // --- 5. 独立加载方法 (链式调用) ---

        /// <summary>
        /// 加载检测模型
        /// </summary>
        public PaddleOCRConfig LoadDet(string modelPath)
        {
            if (!string.IsNullOrEmpty(modelPath))
            {
                CheckFile(modelPath, "检测模型");
                DetConfig = new PPOcrDetConfig(modelPath);
                // 如果之前设置过全局 Batch，这里同步一下
                if (_globalMaxBatchSize > 1) DetConfig.MaxBatchSize = _globalMaxBatchSize;
            }
            else
            {
                DetConfig = null;
            }
            return this;
        }

        /// <summary>
        /// 加载分类模型
        /// </summary>
        public PaddleOCRConfig LoadCls(string modelPath)
        {
            if (!string.IsNullOrEmpty(modelPath))
            {
                CheckFile(modelPath, "分类模型");
                ClsConfig = new PPOcrClsConfig(modelPath);
                if (_globalMaxBatchSize > 1) ClsConfig.MaxBatchSize = _globalMaxBatchSize;
            }
            else
            {
                ClsConfig = null;
            }
            return this;
        }

        /// <summary>
        /// 加载识别模型
        /// </summary>
        public PaddleOCRConfig LoadRec(string modelPath, string dictPath)
        {
            // 只要 modelPath 不为空，dictPath 就是必须的
            if (!string.IsNullOrEmpty(modelPath))
            {
                CheckFile(modelPath, "识别模型");
                CheckFile(dictPath, "识别字典");
                RecConfig = new PPOcrRecConfig(modelPath, dictPath);
                if (_globalMaxBatchSize > 1) RecConfig.MaxBatchSize = _globalMaxBatchSize;
            }
            else
            {
                RecConfig = null;
            }
            return this;
        }


        // --- 6. 校验与辅助方法 ---

        /// <summary>
        /// 校验当前配置的有效性
        /// </summary>
        public void Validate()
        {
            // 基础检查：至少得有一个模型吧？
            if (!UseDet && !UseCls && !UseRec)
            {
                throw new InvalidOperationException("配置无效：未加载任何模型。");
            }

            //// 依赖关系检查：
            //// 如果没有 Det，通常意味着用户输入的是裁剪好的图片。
            //// 如果没有 Det，Cls（方向分类）就没有意义（因为它是基于检测框的）。
            //if (!UseDet && UseCls)
            //{
            //    throw new InvalidOperationException("配置逻辑错误：启用了分类模块(Cls)，但未启用检测模块。");
            //}

            // 字典有效性检查
            if (UseRec && (RecConfig.DictChars == null || RecConfig.DictChars.Count == 0))
            {
                throw new InvalidOperationException("识别模块(Rec)已启用，但字典加载失败或为空。");
            }
        }

        /// <summary>
        /// 内部辅助：仅对当前已激活的配置执行操作
        /// </summary>
        private void ApplyToActiveConfigs(Action<IImgConfig> action)
        {
            if (UseDet) action(DetConfig);
            if (UseCls) action(ClsConfig);
            if (UseRec) action(RecConfig);
        }

        private void CheckFile(string path, string name)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"{name} 文件未找到: {path}");
        }

        // --- 序列化支持 ---

        public void SaveToJson(string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        public static PaddleOCRConfig LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("配置文件未找到", filePath);
            string json = File.ReadAllText(filePath);
            // 注意：从 JSON 反序列化后，对象属性直接赋值，可能不会触发构造函数里的逻辑
            // 如果有复杂的初始化逻辑，建议反序列化后手动调用 Validate 或修正逻辑
            return JsonSerializer.Deserialize<PaddleOCRConfig>(json);
        }
    }
}
