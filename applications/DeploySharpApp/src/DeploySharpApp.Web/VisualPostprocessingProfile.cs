using System;

namespace DeploySharpApp.Web;

public enum VisualPostprocessingKind
{
    Unknown,
    Classification,
    Detection,
    OrientedDetection,
    Segmentation,
    Pose,
    OcrDetection,
    OcrRecognition,
    BackgroundRemoval,
    Captioning,
    Embedding
}

/// <summary>Describes the output decoding contract selected for one visual release artifact.</summary>
public sealed record VisualPostprocessingProfile(
    VisualPostprocessingKind Kind,
    string Coordinates,
    string ScoreSemantics,
    string LabelSemantics,
    string Description,
    double ScoreThreshold = 0.25,
    int TopK = 5)
{
    public bool DrawGeometry => Kind is VisualPostprocessingKind.Detection or VisualPostprocessingKind.OrientedDetection or VisualPostprocessingKind.Segmentation or VisualPostprocessingKind.Pose or VisualPostprocessingKind.OcrDetection;

    public static VisualPostprocessingProfile FromModel(string task, string name, string declared)
    {
        string value = string.Join(" ", task ?? string.Empty, name ?? string.Empty, declared ?? string.Empty).ToLowerInvariant();
        if (value.Contains("ocr") && (value.Contains(" cls") || value.Contains("orientation") || value.Contains("direction"))) return new(VisualPostprocessingKind.Classification, "class score vector", "orientation confidence", "orientation class id", "OCR orientation classification is ranked by score; labels require the declared orientation contract.", 0.0, 4);
        if (value.Contains("ocr") && (value.Contains("rec") || value.Contains("recogn"))) return new(VisualPostprocessingKind.OcrRecognition, "token-id", "token confidence", "dictionary/token ids", "OCR recognition output is decoded from string/tokens when the model exposes a text contract.", 0.2, 1);
        if (value.Contains("ocr") || value.Contains("text detection")) return new(VisualPostprocessingKind.OcrDetection, "polygon or xyxy", "box confidence", "text region", "OCR text regions are drawn from polygon/box outputs; recognition text remains evidence-backed.");
        if (value.Contains("obb") || value.Contains("oriented") || value.Contains("rotated")) return new(VisualPostprocessingKind.OrientedDetection, "cx,cy,w,h,angle", "objectness x class", "class id", "Oriented boxes use cx/cy/width/height/angle and are rendered as rotated polygons.");
        if (value.Contains("pose") || value.Contains("keypoint") || value.Contains("landmark")) return new(VisualPostprocessingKind.Pose, "x,y,confidence", "keypoint confidence", "keypoint index", "Pose outputs are decoded as x/y/confidence triplets and rendered as keypoints.");
        if (value.Contains("segment") || value.Contains("mask") || value.Contains("matting") || value.Contains("background")) return new(value.Contains("matting") || value.Contains("background") ? VisualPostprocessingKind.BackgroundRemoval : VisualPostprocessingKind.Segmentation, "mask grid", "mask probability", "mask class", "Mask tensors are rendered with alpha; box/mask coefficients remain model-contract dependent.");
        if (value.Contains("classif")) return new(VisualPostprocessingKind.Classification, "class score vector", "softmax/logit score", "class id", "Classification outputs are ranked by score and displayed as top-k classes.", 0.0, 5);
        if (value.Contains("caption") || value.Contains("blip") || value.Contains("visual-language")) return new(VisualPostprocessingKind.Captioning, "text", "sequence probability", "decoded text", "Caption text is displayed only when the runtime exposes a decoded text output.", 0.0, 1);
        if (value.Contains("embedding") || value.Contains("clip")) return new(VisualPostprocessingKind.Embedding, "vector", "none", "dimension", "Embedding outputs are summarized by dimension and norm; no geometric overlay is implied.", 0.0, 1);
        return new(VisualPostprocessingKind.Detection, "xyxy or model-declared", "score", "class id", "Detection outputs are decoded from the declared tensor rows; class names are not guessed without a label contract.");
    }
}
