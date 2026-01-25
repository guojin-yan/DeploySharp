using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public class PPOcrClsConfig : IImgConfig
    {
        /// <summary>
        /// 分类模型输入高度，默认80
        /// </summary>
        private int clsImageHeight = 80;
        /// <summary>
        /// 分类模型输入宽度，默认160
        /// </summary>
        private int clsImageWidth = 160;
        /// <summary>
        /// 分类模型输入高度。修改此值会自动更新 InputSizes。
        /// </summary>
        public int ClsImageHeight
        {
            get => clsImageHeight;
            set
            {
                if (clsImageHeight != value)
                {
                    clsImageHeight = value;
                    UpdateInputShapes(); // 尺寸改变时自动刷新形状定义
                }
            }
        }
        /// <summary>
        /// 分类模型输入宽度。修改此值会自动更新 InputSizes。
        /// </summary>
        public int ClsImageWidth
        {
            get => clsImageWidth;
            set
            {
                if (clsImageWidth != value)
                {
                    clsImageWidth = value;
                    UpdateInputShapes(); // 尺寸改变时自动刷新形状定义
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
                    UpdateInputShapes(); // 尺寸改变时自动刷新形状定义
                }
            }
        }
        // --- 构造函数 ---
        /// <summary>
        /// 默认构造函数：使用默认参数
        /// </summary>
        public PPOcrClsConfig() : this(null)
        {
        }
        /// <summary>
        /// 初始化 PPOcrClsConfig
        /// </summary>
        /// <param name="modelPath">模型文件路径</param>
        /// <param name="clsImageHeight">输入图片高度 (默认80)</param>
        /// <param name="clsImageWidth">输入图片宽度 (默认160)</param>
        /// <param name="maxBatchSize">最大批次大小</param>
        /// <param name="inferBatch">推理批次大小</param>
        /// <param name="confidenceThreshold">置信度阈值</param>
        /// <param name="resizeMode">图片缩放模式</param>
        /// <param name="normalizationType">归一化类型</param>
        /// <param name="dynamicByInput">是否使用动态输入尺寸</param>
        public PPOcrClsConfig(
            string modelPath,
            int clsImageHeight = 80,
            int clsImageWidth = 160,
            int maxBatchSize = 8,
            int inferBatch = 1,
            float confidenceThreshold = 0.9f,
            ImageResizeMode resizeMode = ImageResizeMode.Stretch,
            ImageNormalizationType normalizationType = ImageNormalizationType.Scale_Neg1_1,
            bool dynamicByInput = true)
        {
            // 1. 基础设置
            // 注意：原代码写的是 PaddleOcrDet，这里修正为 PaddleOcrCls
            this.ModelType = ModelType.PaddleOcrCls;
            this.ModelPath = modelPath;
            // 2. 后端与设备
            this.TargetInferenceBackend = InferenceBackend.OpenVINO;
            this.TargetDeviceType = DeviceType.CPU;
            // 3. 通过属性赋值 (触发 UpdateInputShapes 逻辑)
            this.ClsImageHeight = clsImageHeight;
            this.ClsImageWidth = clsImageWidth;
            this.MaxBatchSize = maxBatchSize;
            // 4. 其他参数设置
            this.InferBatch = inferBatch;
            this.ConfidenceThreshold = confidenceThreshold;
            // 5. 预处理设置
            this.DataProcessor.ResizeMode = resizeMode;
            this.DataProcessor.NormalizationType = normalizationType;
            this.DynamicByInput = dynamicByInput;
            // 6. 初始化形状 (作为保险，确保形状与属性一致)
            UpdateInputShapes();
        }
        // --- 私有方法 ---
        /// <summary>
        /// 根据 ClsImageHeight 和 ClsImageWidth 自动更新 InputSizes 和 OutputSizes
        /// </summary>
        private void UpdateInputShapes()
        {
            int h = this.ClsImageHeight;
            int w = this.ClsImageWidth;
            // 防御性编程：确保列表不为空
            if (this.InputSizes == null) this.InputSizes = new List<int[]>();
            if (this.OutputSizes == null) this.OutputSizes = new List<int[]>();
            // 清除旧尺寸
            this.InputSizes.Clear();
            this.OutputSizes.Clear();
            // 添加新尺寸 [Batch, Channel, Height, Width]
            this.InputSizes.Add(new int[] { MaxBatchSize, 3, h, w });

            // 分类模型的输出通常是 [Batch, ClassNum] (例如2类: 正向, 旋转180)
            this.OutputSizes.Add(new int[] { MaxBatchSize, 2 });
        }
    }
}
