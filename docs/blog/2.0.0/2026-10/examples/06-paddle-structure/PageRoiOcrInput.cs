using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

// The detector tensor maps its ROI back to the full page. Recognition therefore
// receives page-space text quadrilaterals and reads the same decoded page pixels.
// This adapter owns both the detector tensor wrapper and the recognition view.
internal sealed class PageRoiOcrInput : IOcrImageInput
{
    private readonly IOcrRecognitionImageInput _recognition;

    public PageRoiOcrInput(OpenCvDecodedRoiImage page, PreparedVisualInput detectionInput)
    {
        DetectionInput = detectionInput;
        try { _recognition = page.CreateRecognitionInput(); }
        catch { detectionInput.Dispose(); throw; }
    }

    public VisualSize SourceSize => _recognition.SourceSize;
    public PreparedVisualInput DetectionInput { get; }

    public PreparedVisualInput PrepareRecognitionBatch(string inputName,
        IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken) =>
        _recognition.PrepareRecognitionBatch(inputName, requests, cancellationToken);

    public void Dispose()
    {
        DetectionInput.Dispose();
        _recognition.Dispose();
    }
}
