using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class PPOcrRecConfig : IImgConfig
    {
        /// <summary>
        /// 识别字符字典表
        /// </summary>
        public List<string> DictChars { get; set; } = new List<string>();

        /// <summary>
        /// 识别模型固定输入高度，默认48
        /// </summary>
        private int inferImageHeight = 48;

        /// <summary>
        /// 识别模型最大输入宽度，默认1024
        /// </summary>
        private int maxImageWidth = 1024;

        /// <summary>
        /// 识别模型固定输入高度。修改此值会自动更新 InputSizes。
        /// </summary>
        public int InferImageHeight
        {
            get => inferImageHeight;
            set
            {
                if (inferImageHeight != value)
                {
                    inferImageHeight = value;
                    UpdateInputShapes();
                }
            }
        }

        /// <summary>
        /// 识别模型最大输入宽度。修改此值会自动更新 InputSizes。
        /// </summary>
        public int MaxImageWidth
        {
            get => maxImageWidth;
            set
            {
                if (maxImageWidth != value)
                {
                    maxImageWidth = value;
                    UpdateInputShapes();
                }
            }
        }

        /// <summary>
        /// 获取或设置单个批处理操作中可以处理的最大项目数。
        /// 更改此值会自动更新输入形状定义。
        /// </summary>
        public override int MaxBatchSize
        {
            get => base.maxBatchSize;
            set
            {
                if (base.maxBatchSize != value)
                {
                    base.maxBatchSize = value;
                    UpdateInputShapes();
                }
            }
        }

        // --- 构造函数 ---

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public PPOcrRecConfig() : this(null, null)
        {
        }

        /// <summary>
        /// 初始化 PPOcrRecConfig (基本接口)
        /// </summary>
        /// <param name="modelPath">模型文件路径</param>
        /// <param name="dictPath">字典文件路径</param>
        public PPOcrRecConfig(string modelPath, string dictPath)
            : this(modelPath, dictPath, 48, 1024, 8, 1, 0.2f, ImageResizeMode.CrnnPad, ImageNormalizationType.Scale_Neg1_1, true)
        {
        }

        /// <summary>
        /// 初始化 PPOcrRecConfig (全参数接口，使用默认值)
        /// </summary>
        /// <param name="modelPath">模型文件路径</param>
        /// <param name="dictPath">字典文件路径</param>
        /// <param name="inferImageHeight">固定输入高度 (默认48)</param>
        /// <param name="maxImageWidth">最大输入宽度 (默认1024)</param>
        /// <param name="maxBatchSize">最大批次大小</param>
        /// <param name="inferBatch">推理批次大小</param>
        /// <param name="confidenceThreshold">置信度阈值</param>
        /// <param name="resizeMode">图片缩放模式 (默认CrnnPad)</param>
        /// <param name="normalizationType">归一化类型 (默认Scale_Neg1_1)</param>
        /// <param name="dynamicByInput">是否使用动态输入尺寸</param>
        public PPOcrRecConfig(
            string modelPath,
            string dictPath,
            int inferImageHeight = 48,
            int maxImageWidth = 1024,
            int maxBatchSize = 8,
            int inferBatch = 1,
            float confidenceThreshold = 0.2f,
            ImageResizeMode resizeMode = ImageResizeMode.CrnnPad,
            ImageNormalizationType normalizationType = ImageNormalizationType.Scale_Neg1_1,
            bool dynamicByInput = true)
        {
            // 1. 基础设置
            // 注意：原代码写的是 PaddleOcrDet，这里修正为 PaddleOcrRec
            this.ModelType = ModelType.PaddleOcrRec;
            this.ModelPath = modelPath;

            // 2. 后端与设备
            this.TargetInferenceBackend = InferenceBackend.OpenVINO;
            this.TargetDeviceType = DeviceType.CPU;

            // 3. 字典加载 (如果提供了路径)
            if (!string.IsNullOrEmpty(dictPath))
            {
                LoadDictionary(dictPath);
            }

            // 4. 通过属性赋值 (触发 UpdateInputShapes 逻辑)
            this.InferImageHeight = inferImageHeight;
            this.MaxImageWidth = maxImageWidth;
            this.MaxBatchSize = maxBatchSize;

            // 5. 其他参数设置
            this.InferBatch = inferBatch;
            this.ConfidenceThreshold = confidenceThreshold;

            // 6. 预处理设置
            this.DataProcessor.ResizeMode = resizeMode;
            this.DataProcessor.NormalizationType = normalizationType;
            this.DynamicByInput = dynamicByInput;

            // 7. 初始化形状
            UpdateInputShapes();
        }

        // --- 私有方法 ---

        /// <summary>
        /// 加载字符字典文件
        /// </summary>
        /// <param name="dictPath">字典文件路径</param>
        private void LoadDictionary(string dictPath)
        {
            if (!File.Exists(dictPath))
            {
                throw new FileNotFoundException($"字典文件未找到: {dictPath}");
            }

            // 清空字典
            DictChars.Clear();

            try
            {
                // 使用 using 语句确保文件流正确关闭
                using (StreamReader sr = new StreamReader(dictPath))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        DictChars.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"加载字典文件失败: {dictPath}", ex);
            }

            // 按照PaddleOCR惯例，补全特殊字符
            // "#" 通常用于 padding 或 blank token
            DictChars.Insert(0, "#");
            // " " 用于空格
            DictChars.Add(" ");
        }

        /// <summary>
        /// 根据 InferImageHeight、MaxImageWidth 和 DictChars 自动更新 InputSizes 和 OutputSizes
        /// </summary>
        private void UpdateInputShapes()
        {
            int h = this.InferImageHeight;
            int w = this.MaxImageWidth;
            int dictCount = this.DictChars.Count;

            // 防御性编程：确保列表不为空
            if (this.InputSizes == null) this.InputSizes = new List<int[]>();
            if (this.OutputSizes == null) this.OutputSizes = new List<int[]>();

            // 清除旧尺寸
            this.InputSizes.Clear();
            this.OutputSizes.Clear();

            // 添加新尺寸 [Batch, Channel, Height, Width]
            this.InputSizes.Add(new int[] { MaxBatchSize, 3, h, w });

            // 添加输出尺寸 [Batch, TimeSteps(Width/8), ClassNum]
            // 注意：CRNN下采样倍数通常是8倍 (32 -> 4, 3->3, conv 3 stride 2... 最终 stride 8)
            // 如果模型下采样倍数不同，这里需要调整除数
            int timeSteps = w / 8;
            this.OutputSizes.Add(new int[] { MaxBatchSize, timeSteps, dictCount });
        }
    }
}
