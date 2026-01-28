using DeploySharp.Data;
using DeploySharp.Engine;
using DeploySharp.Log;
using DeploySharp.Model;
using OpenCvSharp;
using OpenVinoSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeploySharp.OpenCvSharp.PaddleOcr.Demo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            //string imagePath = @"E:\Data\ocr\demo_20.png";
            //Mat img = Cv2.ImRead(imagePath);


            //PPOcrDetConfig detConfig = new PPOcrDetConfig(@"E:\Model\ppocrv5\PP-OCRv5_mobile_det_onnx.engine");
            //detConfig.SetTargetInferenceBackend(InferenceBackend.TensorRT);
            ////detConfig.DynamicByInput = true;
            //detConfig.InputSizes.Add(new int[] { 1, 3, 320, 320 });
            //detConfig.OutputSizes.Add(new int[] { 1, 1, 320, 320 });

            //PPOcrDet ocrDet = new PPOcrDet(detConfig);
            //OcrResult[] result = ocrDet.Predict(img);
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            //result = ocrDet.Predict(img);
            //sw.Stop();
            //Console.WriteLine($"Inference time: {sw.ElapsedMilliseconds} ms");
            //var resultImg = Visualize.DrawObbResult(result, img, new VisualizeOptions(1.0f));
            //Cv2.ImShow("image", resultImg);


            //Cv2.WaitKey();

            //foreach(var r in result)
            //{
            //    Cv2.ImShow("image", CvPPOcrDataProcessor.GetRotateCropImageByRect(img, r.Bounds));
            //    Cv2.WaitKey();
            //}



            //string imagePath = @"E:\Data\ocr\demo_9.png";
            //Mat img = Cv2.ImRead(imagePath);


            //PPOcrClsConfig detConfig = new PPOcrClsConfig(@"E:\Model\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx");
            ////detConfig.SetTargetInferenceBackend(InferenceBackend.TensorRT);
            ////detConfig.DynamicByInput = true;
            ////detConfig.InputSizes.Add(new int[] { 1, 3, 320, 320 });
            ////detConfig.OutputSizes.Add(new int[] { 1, 1, 320, 320 });

            //PPOcrCls ocrCls = new PPOcrCls(detConfig);
            //Result[] result = ocrCls.Predict(img);
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            //result = ocrCls.Predict(img);
            //sw.Stop();
            //Console.WriteLine($"Inference time: {sw.ElapsedMilliseconds} ms");



            //string imagePath = @"E:\Data\ocr\demo_14.jpg";
            //Mat img = Cv2.ImRead(imagePath);


            //PPOcrRecConfig recConfig = new PPOcrRecConfig(@"E:\Model\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx",
            //    @"E:\Model\ppocrv5\ppocrv5_dict.txt");
            ////detConfig.SetTargetInferenceBackend(InferenceBackend.TensorRT);
            //recConfig.DynamicByInput = true;
            ////detConfig.InputSizes.Add(new int[] { 1, 3, 320, 320 });
            ////detConfig.OutputSizes.Add(new int[] { 1, 1, 320, 320 });

            //PPOcrRec ocrRec = new PPOcrRec(recConfig);
            //TextRecResult[] result = ocrRec.Predict(img);
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            //result = ocrRec.Predict(img);
            //sw.Stop();
            //Console.WriteLine($"Inference time: {sw.ElapsedMilliseconds} ms");

            MyLogger.SetLevel(Log.LogLevel.ERROR);




            string imagePath = @"E:\Data\ocr\demo_1.jpg";

            //string imagePath = @"E:\Data\ocr\demo_16.bmp";
            string imagePath1 = @"E:\Data\ocr\demo_2.jpg";
            Mat img = Cv2.ImRead(imagePath);

            Mat img1 = Cv2.ImRead(imagePath1);

            PaddleOCRConfig paddleOCRConfig = new PaddleOCRConfig(
                detModelPath: @"E:\Model\ppocrv5\cuda12\PP-OCRv5_mobile_det_f16_onnx.engine",
                clsModelPath: @"E:\Model\ppocrv5\cuda12\PP-OCRv5_mobile_cls_f16_onnx.engine",
                recModelPath: @"E:\Model\ppocrv5\cuda12\PP-OCRv5_mobile_rec_f16_onnx.engine",
                recDictPath: @"E:\Model\ppocrv5\ppocrv5_dict.txt"
                );


            paddleOCRConfig.GlobalInferenceBackend = InferenceBackend.TensorRT;
            paddleOCRConfig.GlobalDeviceType = DeviceType.GPU0;
            paddleOCRConfig.GlobalOnnxRuntimeDeviceType = OnnxRuntimeDeviceType.Cuda;
            paddleOCRConfig.GlobalMaxBatchSize = 8;
            ////paddleOCRConfig.DetConfig.InputSizes.Add(new int[] { 1, 3, 960, 960 });
            ////paddleOCRConfig.DetConfig.OutputSizes.Add(new int[] { 1, 1, 960, 960 });
            //paddleOCRConfig.DetConfig.SetTargetInferenceBackend(InferenceBackend.TensorRT);
            ////paddleOCRConfig.DetConfig.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.Cuda);
            ////paddleOCRConfig.DetConfig.SetTargetDeviceType(DeviceType.GPU0);


            ////paddleOCRConfig.ClsConfig.InputSizes.Add(new int[] { 8, 3, 80, 160 });
            ////paddleOCRConfig.ClsConfig.OutputSizes.Add(new int[] { 8, 2 });
            //paddleOCRConfig.ClsConfig.SetTargetInferenceBackend(InferenceBackend.TensorRT);
            ////paddleOCRConfig.ClsConfig.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.Cuda);
            ////paddleOCRConfig.ClsConfig.SetTargetDeviceType(DeviceType.GPU0);
            ////paddleOCRConfig.ClsConfig.MaxBatchSize = 12;

            ////paddleOCRConfig.RecConfig.InputSizes.Add(new int[] { 12, 3, 48, 1024 });
            ////paddleOCRConfig.RecConfig.OutputSizes.Add(new int[] { 12, 128, 18385 });
            //paddleOCRConfig.RecConfig.SetTargetInferenceBackend(InferenceBackend.TensorRT);
            ////paddleOCRConfig.RecConfig.SetTargetOnnxRuntimeDeviceType(OnnxRuntimeDeviceType.Cuda);
            ////paddleOCRConfig.RecConfig.SetTargetDeviceType(DeviceType.GPU0);

            ////paddleOCRConfig.RecConfig.MaxBatchSize = 12;
            using (PaddleOcrPredictor paddleOcrPredictor = new PaddleOcrPredictor(paddleOCRConfig))
            {
                OcrResult ocrResult = paddleOcrPredictor.Predict(img);
                ocrResult = paddleOcrPredictor.Predict(img);
                Stopwatch sw = Stopwatch.StartNew();
                //for (int i = 0; i < 10; i++)
                    ocrResult = paddleOcrPredictor.Predict(img);
                sw.Stop();

                Console.WriteLine(ocrResult.ToString());

                Console.WriteLine("---- Profiling Time ----");

                paddleOcrPredictor.PrintTimeProfiling();
                Console.WriteLine($"Inference time: {sw.ElapsedMilliseconds} ms");

                Mat resultMat = Visualize.DrawOcrResult(img, ocrResult, new VisualizeOptions(1.0f));

     

                Cv2.ImShow("image", resultMat);


                Cv2.WaitKey();
            }



        }
    }
}
