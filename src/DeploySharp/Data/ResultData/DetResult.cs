using System;

namespace DeploySharp.Data
{
    public class DetResult : Result 
    {
        public Rect Bounds { get; set; }

        public DetResult()
        {
            Type = ResultType.Detection;
        }

        public override string ToString()
        {
            return base.ToString() + ": " + Bounds.ToString();
        }
    }
}
