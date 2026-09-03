using System;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates exact LLaVA-OneVision anyres crops from one OpenCV decode. / 从一次 OpenCV Decode 创建精确 LLaVA-OneVision Anyres Crop。</summary>
    public sealed class OpenCvNativeMultimodalInputFactory
    {
        /// <summary>Decodes once, selects the official grid, creates base/high-resolution Pillow-bicubic crops, and normalizes RGB to [-1,1]. / 单次 Decode，选择官方网格，创建基础/高分辨率 Pillow-bicubic Crop，并将 RGB 归一化到 [-1,1]。</summary>
        public NativeMultimodalPreparedImage Create(OpenCvImageSource source, NativeMultimodalProfile profile, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, profile.Blocker ?? "The native multimodal profile is unavailable.", profileId: profile.ProfileId);
            if (profile.Family != NativeMultimodalFamily.Llava || profile.Processor.PatchSize != 384) throw new VisualException(VisualErrorCodes.NativeMultimodalCapabilityUnavailable, "This OpenCV adapter is audited only for the bound LLaVA-OneVision 384-pixel anyres processor.", profileId: profile.ProfileId);
            if (source.Length > profile.Processor.MaximumImageBytes) throw new VisualException(VisualErrorCodes.NativeMultimodalLimitExceeded, "The encoded image exceeds processor capacity.", profileId: profile.ProfileId);
            OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                    var sourceSize = new VisualSize(decoded.Cols, decoded.Rows);
                    var conversionOptions = new OpenCvPreprocessOptions(new VisualSize(384, 384), OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.UInt8);
                    byte[] rgb = OpenCvVisualInputFactory.CopyRowsAndConvertChannels(decoded, conversionOptions);
                    NativeMultimodalImageGrid grid = profile.Processor.SelectGrid(sourceSize);
                    int patch = profile.Processor.PatchSize;
                    byte[] baseImage = OpenCvVisualInputFactory.PillowBicubicResize(rgb, sourceSize.Width, sourceSize.Height, 3, patch, patch, cancellationToken);
                    int targetHeight = checked(grid.Rows * patch);
                    int targetWidth = checked(grid.Columns * patch);
                    double scaleWidth = (double)targetWidth / sourceSize.Width;
                    double scaleHeight = (double)targetHeight / sourceSize.Height;
                    int resizedWidth;
                    int resizedHeight;
                    if (scaleWidth < scaleHeight)
                    {
                        resizedWidth = targetWidth;
                        resizedHeight = Math.Min(checked((int)Math.Ceiling(sourceSize.Height * scaleWidth)), targetHeight);
                    }
                    else
                    {
                        resizedHeight = targetHeight;
                        resizedWidth = Math.Min(checked((int)Math.Ceiling(sourceSize.Width * scaleHeight)), targetWidth);
                    }
                    byte[] resized = OpenCvVisualInputFactory.PillowBicubicResize(rgb, sourceSize.Width, sourceSize.Height, 3, resizedWidth, resizedHeight, cancellationToken);
                    int left = (targetWidth - resizedWidth) / 2;
                    int top = (targetHeight - resizedHeight) / 2;
                    int crops = checked(grid.PatchCount + 1);
                    var pixels = new float[checked(crops * 3 * patch * patch)];
                    for (int index = 0; index < pixels.Length; index++) pixels[index] = -1f;
                    WriteCrop(baseImage, pixels, 0, patch, cancellationToken);
                    for (int gridRow = 0; gridRow < grid.Rows; gridRow++)
                    {
                        for (int gridColumn = 0; gridColumn < grid.Columns; gridColumn++)
                        {
                            int crop = 1 + (gridRow * grid.Columns) + gridColumn;
                            int cropLeft = gridColumn * patch;
                            int cropTop = gridRow * patch;
                            int copyLeft = Math.Max(cropLeft, left);
                            int copyTop = Math.Max(cropTop, top);
                            int copyRight = Math.Min(cropLeft + patch, left + resizedWidth);
                            int copyBottom = Math.Min(cropTop + patch, top + resizedHeight);
                            for (int y = copyTop; y < copyBottom; y++)
                            {
                                if ((y & 31) == 0) OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                                int sourceOffset = (((y - top) * resizedWidth) + (copyLeft - left)) * 3;
                                int destinationX = copyLeft - cropLeft;
                                WriteRow(resized, sourceOffset, pixels, crop, y - cropTop, patch, destinationX, copyRight - copyLeft);
                            }
                        }
                    }
                    int packedTokens = profile.Processor.GetPackedTokenCount(sourceSize, grid);
                    var tensor = new Tensor<float>(new TensorShape(crops, 3, patch, patch), pixels, TensorBufferOwnership.Transfer);
                    var descriptor = new VisualPreprocessingDescriptor(VisualColorOrder.Rgb, new[] { 127.5f, 127.5f, 127.5f }, new[] { 1f / 127.5f, 1f / 127.5f, 1f / 127.5f }, "OpenCV single decode plus managed Pillow-compatible bicubic LLaVA-OneVision base/anyres crops; centered zero-byte padding before [-1,1] normalization.");
                    var prepared = new PreparedVisualInput("pixel_values", tensor, sourceSize, new VisualSize(patch, patch), crops, VisualTensorLayout.Nchw, ImageTransform.Resize(sourceSize, new VisualSize(patch, patch)), descriptor, source.Sha256);
                    return new NativeMultimodalPreparedImage(profile.ProfileId, prepared, grid, packedTokens);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "The native multimodal OpenCV operation was cancelled.", exception); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The native multimodal image could not be prepared.", exception, "sourceKind=" + source.Kind); }
        }

        /// <summary>Creates an owned prepared image from an absolute PNG/JPEG path. / 从绝对 PNG/JPEG 路径创建自有已准备图像。</summary>
        public NativeMultimodalPreparedImage CreateFromFile(string path, NativeMultimodalProfile profile, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromFile(path), profile, cancellationToken);

        /// <summary>Creates an owned prepared image from copied encoded bytes. / 从复制的编码字节创建自有已准备图像。</summary>
        public NativeMultimodalPreparedImage CreateFromBytes(byte[] bytes, NativeMultimodalProfile profile, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromBytes(bytes), profile, cancellationToken);

        private static void WriteCrop(byte[] source, float[] destination, int crop, int patch, CancellationToken cancellationToken)
        {
            for (int y = 0; y < patch; y++)
            {
                if ((y & 31) == 0) OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                WriteRow(source, y * patch * 3, destination, crop, y, patch, 0, patch);
            }
        }

        private static void WriteRow(byte[] source, int sourceOffset, float[] destination, int crop, int y, int patch, int destinationX, int count)
        {
            int plane = patch * patch;
            int cropOffset = crop * 3 * plane;
            for (int x = 0; x < count; x++)
            {
                int pixel = sourceOffset + (x * 3);
                int spatial = (y * patch) + destinationX + x;
                destination[cropOffset + spatial] = (source[pixel] - 127.5f) / 127.5f;
                destination[cropOffset + plane + spatial] = (source[pixel + 1] - 127.5f) / 127.5f;
                destination[cropOffset + (2 * plane) + spatial] = (source[pixel + 2] - 127.5f) / 127.5f;
            }
        }
    }
}
