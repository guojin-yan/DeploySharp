using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    /// <summary>
    /// Owns a bounded set of independent TensorRT visual pipelines. Each item has its own TensorRT context, CUDA stream,
    /// device input/output buffers, and CUDA post-processing state; calls are leased rather than sharing one mutable pipeline.
    /// / 拥有一组有界且相互独立的 TensorRT 视觉流水线。每个成员拥有自己的 TensorRT context、CUDA stream、设备输入输出缓冲区和 CUDA 后处理状态；调用通过租约分配，不共享一个可变 Pipeline。
    /// </summary>
    public sealed class TensorRtVisualPipelinePool : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<TensorRtVisualPipeline> _availablePipelines;
        private readonly IReadOnlyList<TensorRtVisualPipeline> _allPipelines;
        private readonly SemaphoreSlim _available;
        private readonly CancellationTokenSource _disposeCancellation = new CancellationTokenSource();
        private int _active;
        private bool _disposed;

        /// <summary>Initializes a pool over already-created independent pipelines. The pool takes ownership and disposes them. / 根据已创建的独立 Pipeline 初始化池；池接管所有权并在释放时销毁它们。</summary>
        public TensorRtVisualPipelinePool(IEnumerable<TensorRtVisualPipeline> pipelines)
        {
            if (pipelines == null) throw new ArgumentNullException(nameof(pipelines));
            TensorRtVisualPipeline[] values = pipelines.ToArray();
            if (values.Length == 0) throw new ArgumentException("At least one TensorRT visual pipeline is required.", nameof(pipelines));
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null) throw new ArgumentException("The TensorRT visual pipeline collection cannot contain null values.", nameof(pipelines));
                if (index > 0 && (values[index].Profile.Task != values[0].Profile.Task || values[index].Profile.ModelId != values[0].Profile.ModelId))
                    throw new ArgumentException("All TensorRT visual pipelines in one pool must use the same model and task profile.", nameof(pipelines));
            }

            _allPipelines = Array.AsReadOnly(values);
            _availablePipelines = new Queue<TensorRtVisualPipeline>(values);
            _available = new SemaphoreSlim(values.Length, values.Length);
        }

        /// <summary>Gets the model profile shared by every independent context. / 获取所有独立 context 共享的模型 Profile。</summary>
        public VisualModelProfile Profile => _allPipelines[0].Profile;

        /// <summary>Gets the number of independent TensorRT contexts and CUDA streams. / 获取独立 TensorRT context 和 CUDA stream 数量。</summary>
        public int Count => _allPipelines.Count;

        /// <summary>Runs one compact BGR frame on the first available independent pipeline. / 在第一个空闲的独立 Pipeline 上运行一帧紧凑 BGR 图像。</summary>
        public Task<VisualInferenceResult> RunAsync(OpenCvBgrImage image, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            return ExecuteAsync(pipeline => pipeline.Run(image, cancellationToken), cancellationToken);
        }

        /// <summary>Runs one axis-aligned ROI without a CPU crop on the TensorRT CUDA path. / 在 TensorRT CUDA 路径上运行一个不需要 CPU 裁剪的轴对齐 ROI。</summary>
        public Task<VisualInferenceResult> RunRoiAsync(OpenCvBgrImage image, JYPPX.DeploySharp.Geometry.RectangleF roi, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            return ExecuteAsync(pipeline => pipeline.RunRoi(image, roi, cancellationToken), cancellationToken);
        }

        /// <summary>Runs frames concurrently with bounded context usage and returns results in input order. / 以有界 context 并发运行多帧，并按输入顺序返回结果。</summary>
        public async Task<IReadOnlyList<VisualInferenceResult>> RunManyAsync(IReadOnlyList<OpenCvBgrImage> images, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (images == null) throw new ArgumentNullException(nameof(images));
            for (int index = 0; index < images.Count; index++) if (images[index] == null) throw new ArgumentException("The image collection cannot contain null values.", nameof(images));
            Task<VisualInferenceResult>[] tasks = new Task<VisualInferenceResult>[images.Count];
            for (int index = 0; index < images.Count; index++) tasks[index] = RunAsync(images[index], cancellationToken);
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>Runs axis-aligned ROIs concurrently with bounded independent CUDA streams and returns results in input order. / 使用有界独立 CUDA stream 并发运行轴对齐 ROI，并按输入顺序返回结果。</summary>
        public async Task<IReadOnlyList<VisualInferenceResult>> RunRoiManyAsync(IReadOnlyList<(OpenCvBgrImage Image, JYPPX.DeploySharp.Geometry.RectangleF Roi)> items, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Task<VisualInferenceResult>[] tasks = new Task<VisualInferenceResult>[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                if (items[index].Image == null) throw new ArgumentException("The ROI image collection cannot contain null images.", nameof(items));
                tasks[index] = RunRoiAsync(items[index].Image, items[index].Roi, cancellationToken);
            }
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>Stops new leases, waits for active calls, then disposes every independent pipeline. / 停止新租约、等待活动调用完成，再释放所有独立 Pipeline。</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeCancellation.Cancel();
                while (_active != 0) Monitor.Wait(_gate);
            }

            for (int index = _allPipelines.Count - 1; index >= 0; index--) _allPipelines[index].Dispose();
            // Keep managed gates alive for waiters already unwinding from disposal cancellation. / 保留托管门闩，供已因释放取消而开始退出的等待者安全完成路径。
        }

        private async Task<VisualInferenceResult> ExecuteAsync(Func<TensorRtVisualPipeline, VisualInferenceResult> operation, CancellationToken cancellationToken)
        {
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellation.Token);
            await _available.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            TensorRtVisualPipeline? pipeline = null;
            try
            {
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(TensorRtVisualPipelinePool));
                    pipeline = _availablePipelines.Dequeue();
                    _active = checked(_active + 1);
                }

                // TensorRT and CUDA calls are synchronous at the managed boundary. Do not pass a cancellation token to
                // Task.Run: cancelling the await must never return a pipeline while native work still uses its context
                // and buffers. The operation receives the caller token for pre-launch checks.
                // / TensorRT 和 CUDA 在托管边界是同步调用。不能把取消令牌传给 Task.Run：取消等待绝不能在 native 工作仍使用 context/缓冲区时归还 Pipeline；操作本身仍接收调用方令牌用于启动前检查。
                return await Task.Run(() => operation(pipeline)).ConfigureAwait(false);
            }
            finally
            {
                if (pipeline != null)
                {
                    lock (_gate)
                    {
                        _availablePipelines.Enqueue(pipeline);
                        _active--;
                        if (_active == 0) Monitor.PulseAll(_gate);
                    }
                }
                _available.Release();
            }
        }
    }
}
