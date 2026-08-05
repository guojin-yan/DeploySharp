using System;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static int Main()
    {
        try
        {
            _ = OpenCvRuntimePreflight.Check();
            Console.Error.WriteLine("A native runtime was unexpectedly discoverable in the managed-only consumer.");
            return 2;
        }
        catch (OpenCvVisualException exception) when (exception.ErrorCode == OpenCvErrorCodes.NativeUnavailable)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_OPENCV_NATIVE_MISSING_DIAGNOSTIC_OK " + exception.ErrorCode);
            return 0;
        }
    }
}
