using System;

namespace DeploySharp.Data
{
    public class Result
    {
        public ResultType Type { get; set; } = ResultType.Classification;
        public Size ImageSize { get; set; }
        public int Id { get; set; }

        private string _category;
        public string Category
        {
            get => string.IsNullOrEmpty(_category) ? Id.ToString() : _category;
            set => _category = value;
        }

        public float Confidence { get; set; }

        public void UpdateCategory(string[] categories)
        {
            Category = categories.Length > 0 ? categories[0] : null;
        }

        public override string ToString()
        {
            return $"ID: {Id}, Category: {Category}, Confidence: {Confidence:P2}, Image Size: {ImageSize}";
        }
    }

    public enum ResultType
    {
        Classification,
        Detection,
        OrientedBoundingBoxes,
        Segmentation,
        KeyPoints,
        AnomalySegmentation
    }
}
