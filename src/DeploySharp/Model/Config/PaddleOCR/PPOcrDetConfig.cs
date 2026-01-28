using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{

    public class PPOcrDetConfig : IImgConfig
    {

        /// <summary>
        /// 限制输入图片的最大长边，默认960
        /// </summary>
        private int limitInputSize = 960;


        /// <summary>
        /// DB后处理的Box阈值，默认0.75
        /// </summary>
        public float DBBoxThresh { get; set; } = 0.75f;

        /// <summary>
        /// DB分数模式，默认为空
        /// </summary>
        public string DBScoreMode { get; set; } = "";
        /// <summary>
        /// DB文本框扩展比例，默认2.0
        /// </summary>
        public float DBUnclipRatio { get; set; } = 2.0f;
        // --- 构造函数 ---
        /// <summary>
        /// 默认构造函数：使用默认参数
        /// </summary>
        public PPOcrDetConfig() : this(null)
        {
            // 这里不需要写任何代码，直接调用带路径的构造函数，并传入 null
        }

        /// <summary>
        /// 限制输入图片的最大长边。修改此值会自动更新 InputSizes。
        /// </summary>
        public int LimitInputSize
        {
            get => limitInputSize;
            set
            {
                if (limitInputSize != value)
                {
                    limitInputSize = value;
                    UpdateInputShapes(); // 尺寸改变时自动刷新形状定义
                }
            }
        }

        /// <summary>
        /// 获取或设置单个批处理操作中可以处理的最大项目数。
        /// Gets or sets the maximum number of items that can be processed in a single batch operation.
        /// </summary>
        /// <remarks>
        /// 更改此值会自动更新输入形状定义，以反映新的批量大小。设置适当的批处理大小会影响性能和资源使用情况。
        /// Changing this value automatically updates the input shape definitions to reflect the
        /// new batch size. Setting an appropriate batch size can impact performance and resource usage.
        /// </remarks>
        public override int MaxBatchSize
        {
            get => maxBatchSize;
            set
            {
                if (MaxBatchSize != value)
                {
                    maxBatchSize = value;
                    UpdateInputShapes(); // 尺寸改变时自动刷新形状定义
                }
            }
        }

        // --- 构造函数 (全参数接口，全部使用默认值) ---
        /// <summary>
        /// 初始化 PPOcrDetConfig
        /// </summary>
        /// <param name="modelPath">模型文件路径</param>
        /// <param name="dbBoxThresh">DB检测的Box阈值</param>
        /// <param name="limitInputSize">限制输入长边大小 (修改会自动更新InputSizes)</param>
        /// <param name="dbUnclipRatio">文本框扩展比例</param>
        /// <param name="dbScoreMode">DB分数模式</param>
        /// <param name="confidenceThreshold">通用置信度阈值</param>
        /// <param name="inferBatch">推理批次大小</param>
        /// <param name="resizeMode">图片缩放模式</param>
        /// <param name="normalizationType">归一化类型</param>
        /// <param name="dynamicByInput">是否使用动态输入尺寸</param>
        public PPOcrDetConfig(
            string modelPath,
            float dbBoxThresh = 0.75f,
            int limitInputSize = 960,
            float dbUnclipRatio = 1.5f,
            string dbScoreMode = "",
            float confidenceThreshold = 0.5f,
            int maxBatchSize = 8,
            int inferBatch = 1,
            ImageResizeMode resizeMode = ImageResizeMode.Stretch,
            ImageNormalizationType normalizationType = ImageNormalizationType.ImageNetStandard,
            bool dynamicByInput = true)
        {
            // 1. 基础设置
            this.ModelType = ModelType.PaddleOcrDet;
            this.ModelPath = modelPath;

            // 2. 后端与设备
            this.TargetInferenceBackend = InferenceBackend.OpenVINO;
            this.TargetDeviceType = DeviceType.CPU;
            // 3. 通过属性赋值 (触发逻辑)
            this.DBBoxThresh = dbBoxThresh;
            // 注意：LimitInputSize 的 set 访问器会被调用，从而触发 UpdateInputShapes()
            this.LimitInputSize = limitInputSize;
            this.DBUnclipRatio = dbUnclipRatio;
            this.DBScoreMode = dbScoreMode;
            this.ConfidenceThreshold = confidenceThreshold;
            this.MaxBatchSize = maxBatchSize;
            this.InferBatch = inferBatch;
            // 4. 预处理设置
            this.DataProcessor.ResizeMode = resizeMode;
            this.DataProcessor.NormalizationType = normalizationType;
            this.DynamicByInput = dynamicByInput;

            // 5.初始化形状(如果构造函数逻辑执行完 LimitInputSize 赋值后，形状已被更新，则此步骤可省略或作为保险)
             UpdateInputShapes();
        }
        // --- 私有方法 ---
        /// <summary>
        /// 根据 LimitInputSize 自动更新 InputSizes 和 OutputSizes
        /// </summary>
        private void UpdateInputShapes()
        {
            int size = this.LimitInputSize;
            // 防御性编程：确保列表不为空
            if (this.InputSizes == null) this.InputSizes = new List<int[]>();
            if (this.OutputSizes == null) this.OutputSizes = new List<int[]>();
            // 清除旧尺寸
            this.InputSizes.Clear();
            this.OutputSizes.Clear();
            // 添加新尺寸 [Batch, Channel, Height, Width]
            this.InputSizes.Add(new int[] { MaxBatchSize, 3, size, size });
            this.OutputSizes.Add(new int[] { MaxBatchSize, 1, size, size });
        }
   
    }
}
