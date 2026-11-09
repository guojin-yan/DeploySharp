using JYPPX.DeploySharp.Visual;
using JYPPX.OpenCvSharp.Core;
using Drawing=JYPPX.OpenCvSharp.ImgProc.Cv2;
using ImageCodecs=JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Fonts=JYPPX.OpenCvSharp.ImgProc.HersheyFonts;

internal static class SlidingDrawing
{
    public static void Save(string file,string output,IReadOnlyList<DetectionRow> rows,IReadOnlyList<SlidingWindow>? windows=null)
    {
        using var image=ImageCodecs.ImRead(file);
        int thickness=Math.Max(1,image.Cols/800);
        if(windows!=null)foreach(var w in windows)
        {
            var b=w.Bounds;
            Scalar color=w.Index%2==0?new Scalar(85,180,240):new Scalar(210,160,55);
            Drawing.Rectangle(image,new Point((int)b.X,(int)b.Y),new Point((int)b.Right-1,(int)b.Bottom-1),color,thickness);
            Drawing.PutText(image,"W"+w.Index,new Point((int)b.X+8,Math.Min(image.Rows-6,(int)b.Y+32)),Fonts.HersheySimplex,.7,color,2);
        }
        foreach(var d in rows)
        {
            Scalar color=d.ClassId==8?new Scalar(155,225,65):new Scalar(60,195,245);
            Drawing.Rectangle(image,new Point((int)d.X,(int)d.Y),new Point((int)(d.X+d.Width),(int)(d.Y+d.Height)),color,thickness);
        }
        string legend=$"detections={rows.Count}"+(windows==null?"":"  windows="+windows.Count);
        Drawing.PutText(image,legend,new Point(16,image.Rows-18),Fonts.HersheySimplex,.8,new Scalar(20,25,30),4);
        Drawing.PutText(image,legend,new Point(16,image.Rows-18),Fonts.HersheySimplex,.8,new Scalar(240,245,250),2);
        if(!ImageCodecs.ImWrite(output,image))throw new IOException("Cannot save "+output);
    }
}
