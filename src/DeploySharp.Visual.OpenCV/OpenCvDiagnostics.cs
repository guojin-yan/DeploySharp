using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.OpenCvSharp;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Defines stable OpenCV adapter diagnostic codes. / 定义稳定的 OpenCV 适配器诊断码。</summary>
    public static class OpenCvErrorCodes
    {
        /// <summary>The input source is outside the accepted boundary. / 输入源超出允许边界。</summary>
        public const string InputBoundary = "DS-OPENCV-5201";
        /// <summary>The image cannot be decoded. / 图像无法解码。</summary>
        public const string DecodeFailed = "DS-OPENCV-5202";
        /// <summary>The preprocessing options are invalid. / 预处理配置无效。</summary>
        public const string PreprocessInvalid = "DS-OPENCV-5203";
        /// <summary>The native OpenCV runtime is unavailable or incompatible. / native OpenCV 运行时缺失或不兼容。</summary>
        public const string NativeUnavailable = "DS-OPENCV-5204";
        /// <summary>A native image operation failed. / native 图像操作失败。</summary>
        public const string OperationFailed = "DS-OPENCV-5205";
        /// <summary>The image operation was cancelled or timed out at a synchronous boundary. / 图像操作在同步边界取消或超时。</summary>
        public const string Cancelled = "DS-OPENCV-5206";
        /// <summary>An owned native object was used after disposal. / 已拥有的 native 对象在释放后被使用。</summary>
        public const string ObjectDisposed = "DS-OPENCV-5207";
    }

    /// <summary>Represents an OpenCV adapter failure while retaining the original exception. / 表示 OpenCV 适配器故障并保留原始异常。</summary>
    public sealed class OpenCvVisualException : DeploySharpException
    {
        /// <summary>Initializes an OpenCV adapter exception. / 初始化 OpenCV 适配器异常。</summary>
        public OpenCvVisualException(string errorCode, string message, Exception? innerException = null, string? technicalDetails = null)
            : base(errorCode, message, innerException, technicalDetails: technicalDetails)
        {
        }
    }

    /// <summary>Contains native runtime information obtained by a guarded preflight. / 包含受保护预检得到的 native 运行时信息。</summary>
    public sealed class OpenCvRuntimeInfo
    {
        internal OpenCvRuntimeInfo(string managedPackageVersion, string openCvVersion, string nativeLibraryName, string nativeVersion, bool isCompatible)
        {
            ManagedPackageVersion = managedPackageVersion;
            OpenCvVersion = openCvVersion;
            NativeLibraryName = nativeLibraryName;
            NativeVersion = nativeVersion;
            IsCompatible = isCompatible;
        }

        /// <summary>Gets the managed package version. / 获取 managed 包版本。</summary>
        public string ManagedPackageVersion { get; }
        /// <summary>Gets the OpenCV version targeted by the managed wrapper. / 获取 managed wrapper 目标 OpenCV 版本。</summary>
        public string OpenCvVersion { get; }
        /// <summary>Gets the primary native library name. / 获取主 native 库名称。</summary>
        public string NativeLibraryName { get; }
        /// <summary>Gets the version reported by the loaded native runtime. / 获取已加载 native 运行时报告的版本。</summary>
        public string NativeVersion { get; }
        /// <summary>Gets whether managed and native versions match exactly. / 获取 managed 与 native 版本是否完全匹配。</summary>
        public bool IsCompatible { get; }
    }

    /// <summary>Runs native OpenCV checks before image operations. / 在图像操作前执行 native OpenCV 检查。</summary>
    public static class OpenCvRuntimePreflight
    {
        /// <summary>Checks the managed/native pair and returns its version information. / 检查 managed/native 组合并返回版本信息。</summary>
        public static OpenCvRuntimeInfo Check()
        {
            try
            {
                string nativeVersion = OpenCvSharpBuildInfo.GetNativeOpenCvVersion();
                bool compatible = OpenCvSharpBuildInfo.IsNativeOpenCvVersionCompatible();
                if (!compatible)
                {
                    throw new OpenCvVisualException(
                        OpenCvErrorCodes.NativeUnavailable,
                        "The loaded OpenCV native runtime does not match the managed wrapper.",
                        technicalDetails: "managed=" + OpenCvSharpBuildInfo.OpenCvVersion + ";native=" + nativeVersion);
                }

                return new OpenCvRuntimeInfo(
                    OpenCvSharpBuildInfo.PackageVersion,
                    OpenCvSharpBuildInfo.OpenCvVersion,
                    OpenCvSharpBuildInfo.CurrentNativeLibraryName,
                    nativeVersion,
                    compatible);
            }
            catch (OpenCvVisualException)
            {
                throw;
            }
            catch (DllNotFoundException exception)
            {
                throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime is not installed or is not discoverable.", exception, "library=" + OpenCvSharpBuildInfo.CurrentNativeLibraryName);
            }
            catch (BadImageFormatException exception)
            {
                throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime architecture is incompatible with this process.", exception, "processBits=" + (IntPtr.Size * 8));
            }
            catch (EntryPointNotFoundException exception)
            {
                throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime does not expose the required ABI entry point.", exception, "library=" + OpenCvSharpBuildInfo.CurrentNativeLibraryName);
            }
            catch (OpenCvException exception)
            {
                throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime preflight failed.", exception, "managed=" + OpenCvSharpBuildInfo.OpenCvVersion);
            }
            catch (Exception exception)
            {
                throw new OpenCvVisualException(OpenCvErrorCodes.NativeUnavailable, "The OpenCV native runtime preflight failed.", exception, "managed=" + OpenCvSharpBuildInfo.OpenCvVersion);
            }
        }
    }
}
