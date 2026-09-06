using Microsoft.VisualStudio.TestTools.UnitTesting;

// ONNX Runtime owns process-wide native state; keep lifecycle tests isolated while
// preserving the explicit concurrency scenarios inside individual test methods.
[assembly: DoNotParallelize]
