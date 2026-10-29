using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using RectangleF = JYPPX.DeploySharp.Geometry.RectangleF;
using PointF = JYPPX.DeploySharp.Geometry.PointF;

internal sealed record ProductJob(Prompt[] Candidates, ProductItem[] Items, float MinimumTextConfidence = .8f);
internal sealed record ProductItem(string Id, string Title, string Image, string? ExpectedCategory);
internal sealed record ProductLine(string Text, float Confidence, bool OnForeground, bool Accepted, PointF[] Polygon);
internal sealed record ProductResult(string Id, string Title, string Status, string[] Reasons, string? ExpectedCategory,
    int Width, int Height, RectangleF ForegroundBounds, double ForegroundRatio, SemanticMatch[] Candidates,
    double Margin, ProductLine[] Lines, string ProductText, string? Error);

internal sealed class ProductCatalog(BackendRegistry backends, BackendRequest request, AppOptions options) : IDisposable
{
    private OcrService? _ocr;
    private VisualPipeline? _matting;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<int> RunAsync(CancellationToken token)
    {
        var job = JsonSerializer.Deserialize<ProductJob>(File.ReadAllText(options.Manifest)) ?? throw new InvalidDataException("Empty product job.");
        if (!float.IsFinite(job.MinimumTextConfidence) || job.MinimumTextConfidence is < 0 or > 1 ||
            job.Candidates is not { Length: >= 2 and <= 64 } || job.Candidates.Any(c => c == null || !SafeId(c.Key) || string.IsNullOrWhiteSpace(c.Text)) ||
            job.Candidates.Select(c => c.Key).Distinct().Count() != job.Candidates.Length || job.Items is not { Length: > 0 and <= 100 } ||
            job.Items.Any(p => p == null || !SafeId(p.Id) || string.IsNullOrWhiteSpace(p.Image) || string.IsNullOrWhiteSpace(p.Title) ||
                p.ExpectedCategory != null && !job.Candidates.Any(c => c.Key == p.ExpectedCategory)) ||
            job.Items.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != job.Items.Length)
            throw new InvalidDataException("Check unique safe IDs, candidate keys, expected category, title and image paths.");
        var group = new PromptGroup("product", [], job.Candidates);
        float[][] vectors = await ClipModels.EncodePromptsAsync(backends, request, options.Models, job.Candidates, token);
        var textByKey = job.Candidates.Select((p, i) => (p.Key, Vector: vectors[i])).ToDictionary(x => x.Key, x => x.Vector);
        var imageDefinition = ClipModels.CreateImageEncoder();
        using var encoder = imageDefinition.CreateRunner(backends, new ModelArtifact(imageDefinition.Profile.ModelId, "onnx",
            Path.Combine(options.Models, "clip-vit-b-32.image-encoder.onnx")), request);
        var matte = BriaRmbgProfiles.CreateRmbg14(new ModelId("bria/rmbg-1.4"),
            new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", "", "", "onnx", "LicenseRef-BRIA-RMBG-1.4"));
        var profiles = new VisualProfileRegistry(); profiles.Register(matte.VisualProfile); profiles.Freeze();
        _matting = new VisualPipeline(backends, profiles.Select(new ModelArtifact(matte.VisualProfile.ModelId, "onnx",
            Path.Combine(options.Models, "bria-rmbg-1.4.onnx")), backends, request, matte.VisualProfile.Task), request);
        _ocr = new OcrService(backends, request, options.Models);
        string root = Path.Combine(options.Output, options.Backend);
        Directory.CreateDirectory(root);
        var reports = new List<ProductResult>();
        foreach (ProductItem item in job.Items)
        {
            token.ThrowIfCancellationRequested();
            string directory = Path.Combine(root, item.Id);
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.GetFullPath(item.Image, Path.GetDirectoryName(options.Manifest)!);
                using var image = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(path), token);
                var whole = new RectangleF(0, 0, image.SourceSize.Width, image.SourceSize.Height);
                using var input = image.PrepareRectangle(new RectangleRoiGeometry(whole), matte.VisualProfile.Input.Name,
                    OpenCvStage19Preprocessing.CreateBriaRmbgOptions(matte), token);
                var removed = (await _matting.RunAsync(input, cancellationToken: token)).GetValue<BackgroundRemovalResult>();
                float[] alpha = removed.Alpha.ToArray();
                var bounds = ForegroundBounds(alpha, image.SourceSize, out int foregroundPixels);
                double foregroundRatio = (double)foregroundPixels / alpha.Length;
                var embedding = await encoder.RunAsync(new RoiBatch(image, [new RectangleRoiGeometry(bounds)]), cancellationToken: token);
                var candidates = ClipModels.Match(embedding.Values[0], group, textByKey);
                double margin = candidates[0].CandidateScore - candidates[1].CandidateScore;
                var ocr = await _ocr.RecognizeAsync(image, whole, token);
                var lines = ocr.Regions.OrderBy(x => x.Region.Polygon.Vertices.Min(p => p.Y)).ThenBy(x => x.Region.Polygon.Vertices.Min(p => p.X))
                    .Select(x =>
                    {
                        var points = x.Region.Polygon.Vertices;
                        int cx = Math.Clamp((int)points.Average(p => p.X), 0, image.SourceSize.Width - 1);
                        int cy = Math.Clamp((int)points.Average(p => p.Y), 0, image.SourceSize.Height - 1);
                        bool onForeground = alpha[cy * image.SourceSize.Width + cx] >= .2f;
                        bool accepted = onForeground && x.Recognition.Confidence >= job.MinimumTextConfidence && !string.IsNullOrWhiteSpace(x.Recognition.Text);
                        return new ProductLine(x.Recognition.Text, x.Recognition.Confidence, onForeground, accepted, points.ToArray());
                    }).ToArray();
                var reasons = new List<string>();
                if (margin < .1) reasons.Add("close-candidates");
                if (item.ExpectedCategory != null && item.ExpectedCategory != candidates[0].Key) reasons.Add("category-mismatch");
                if (foregroundRatio < .01) reasons.Add("empty-foreground");
                if (lines.Any(l => l.OnForeground && !l.Accepted && !string.IsNullOrWhiteSpace(l.Text))) reasons.Add("low-confidence-text");
                var result = new ProductResult(item.Id, item.Title, reasons.Count == 0 ? "ready-for-review" : "needs-review", reasons.ToArray(),
                    item.ExpectedCategory, image.SourceSize.Width, image.SourceSize.Height, bounds, foregroundRatio, candidates.Take(3).ToArray(), margin, lines,
                    string.Join(Environment.NewLine, lines.Where(l => l.Accepted).Select(l => l.Text)), null);
                token.ThrowIfCancellationRequested();
                SaveImages(path, directory, alpha, image.SourceSize, token);
                SaveJson(Path.Combine(directory, "product.json"), result);
                File.WriteAllText(Path.Combine(directory, "product.txt"), result.ProductText, new UTF8Encoding(false));
                reports.Add(result);
                Console.WriteLine($"{item.Id}: category={candidates[0].Key}; text-lines={lines.Length}; foreground-lines={lines.Count(l => l.OnForeground)}; foreground={foregroundRatio:F3}; status={result.Status}");
            }
            catch (Exception error) when (error is not OperationCanceledException && !token.IsCancellationRequested)
            {
                var failed = new ProductResult(item.Id, item.Title, "failed", [], item.ExpectedCategory, 0, 0, new RectangleF(0, 0, 0, 0), 0, [], 0, [], "", error.Message);
                SaveJson(Path.Combine(directory, "product.json"), failed); reports.Add(failed);
                Console.Error.WriteLine(item.Id + ": " + error.Message);
            }
        }
        SaveJson(Path.Combine(root, "catalog.json"), new { deploySharp = "2.0.0", options.Backend, job.Candidates, job.MinimumTextConfidence, products = reports });
        File.WriteAllText(Path.Combine(root, "catalog.csv"), "Id,Title,Category,Status,Text\r\n" + string.Join("\r\n", reports.Select(r =>
            string.Join(",", new[] { r.Id, r.Title, r.Candidates.FirstOrDefault()?.Key ?? "", r.Status, r.ProductText }.Select(Csv)))), new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(root, "index.html"), CatalogHtml(reports), new UTF8Encoding(false));
        Console.WriteLine($"Completed: products={reports.Count}; failed={reports.Count(r => r.Status == "failed")}; output={root}");
        return reports.Any(r => r.Status == "failed") ? 1 : reports.Any(r => r.Status == "needs-review") ? 3 : 0;
    }

    private static RectangleF ForegroundBounds(float[] alpha, VisualSize size, out int count)
    {
        int left = size.Width, top = size.Height, right = -1, bottom = -1; count = 0;
        for (int y = 0; y < size.Height; y++)
            for (int x = 0; x < size.Width; x++)
                if (alpha[y * size.Width + x] >= .2f) { count++; left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        if (count == 0) return new RectangleF(0, 0, size.Width, size.Height);
        int padding = Math.Max(4, (int)(Math.Max(right - left, bottom - top) * .06));
        left = Math.Max(0, left - padding); top = Math.Max(0, top - padding); right = Math.Min(size.Width - 1, right + padding); bottom = Math.Min(size.Height - 1, bottom + padding);
        return new RectangleF(left, top, right - left + 1, bottom - top + 1);
    }

    private static void SaveImages(string source, string directory, float[] alpha, VisualSize size, CancellationToken token)
    {
        using var original = ImageCodecs.ImRead(source);
        using var transparent = new Mat(size.Height, size.Width, MatType.CV_8UC4);
        using var white = new Mat(size.Height, size.Width, MatType.CV_8UC3);
        using var mask = new Mat(size.Height, size.Width, MatType.CV_8UC1);
        int sourceStep = checked((int)original.Step.ToUInt64()), rgbaStep = checked((int)transparent.Step.ToUInt64()),
            whiteStep = checked((int)white.Step.ToUInt64()), maskStep = checked((int)mask.Step.ToUInt64());
        var bgr = new byte[size.Width * 3]; var bgra = new byte[size.Width * 4]; var composite = new byte[bgr.Length]; var maskRow = new byte[size.Width];
        for (int y = 0; y < size.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            Marshal.Copy(IntPtr.Add(original.Data, y * sourceStep), bgr, 0, bgr.Length);
            for (int x = 0; x < size.Width; x++)
            {
                float a = alpha[y * size.Width + x];
                byte value = (byte)Math.Clamp((int)MathF.Round(a * 255), 0, 255);
                for (int c = 0; c < 3; c++) { bgra[x * 4 + c] = bgr[x * 3 + c]; composite[x * 3 + c] = (byte)Math.Clamp((int)MathF.Round(bgr[x * 3 + c] * a + 255 * (1 - a)), 0, 255); }
                bgra[x * 4 + 3] = value; maskRow[x] = value;
            }
            Marshal.Copy(bgra, 0, IntPtr.Add(transparent.Data, y * rgbaStep), bgra.Length);
            Marshal.Copy(composite, 0, IntPtr.Add(white.Data, y * whiteStep), composite.Length);
            Marshal.Copy(maskRow, 0, IntPtr.Add(mask.Data, y * maskStep), maskRow.Length);
        }
        Write("source.jpg", original); Write("transparent.png", transparent); Write("white.jpg", white); Write("alpha.png", mask);
        void Write(string file, Mat image) { if (!ImageCodecs.ImWrite(Path.Combine(directory, file), image)) throw new IOException("Cannot save " + file); }
    }
    private static bool SafeId(string? id) => Regex.IsMatch(id ?? "", "^[a-z0-9][a-z0-9-]{0,63}$");
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string E(string? value) => WebUtility.HtmlEncode(value) ?? "";
    private static void SaveJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json), new UTF8Encoding(false));
    private static string CatalogHtml(IReadOnlyList<ProductResult> products)
    {
        var body = new StringBuilder("<!doctype html><html lang=zh-CN><meta charset=utf-8><title>DeploySharp 商品图册</title><style>body{font:17px system-ui;background:#eef3f8;color:#15354a;max-width:1200px;margin:32px auto;padding:0 18px}article{background:white;padding:24px;border-radius:18px;margin:20px 0}.photos{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}.photos img{width:100%;height:240px;object-fit:contain;border-radius:10px}.alpha{background:repeating-conic-gradient(#d7e0e5 0% 25%,white 0% 50%) 50%/20px 20px}pre{white-space:pre-wrap}a{color:#147e6a}input{padding:12px;width:60%;border:1px solid #bdcdd7;border-radius:10px}label{font-size:14px}</style><body><h1>DeploySharp · 商品图片整理</h1><p>原图、透明素材、分类候选与可见文字。</p><input id=q placeholder='搜索商品、类别或文字'><p><a href=catalog.csv>CSV 清单</a> · <a href=catalog.json>完整 JSON</a></p>");
        foreach (var p in products)
        {
            body.Append($"<article><h2>{E(p.Title)} · {E(p.Id)}</h2><p>{E(p.Status)} {E(string.Join(", ", p.Reasons))}</p>");
            if (p.Error != null) body.Append("<pre>" + E(p.Error) + "</pre>");
            else
            {
                body.Append($"<div class=photos><div><label>原图</label><img src='{p.Id}/source.jpg'></div><div><label>透明 PNG</label><img class=alpha src='{p.Id}/transparent.png'></div><div><label>白底图</label><img src='{p.Id}/white.jpg'></div></div><p><b>类别：</b>{E(p.Candidates[0].Key)} · <b>候选分差：</b>{p.Margin:F3}</p><p>");
                body.Append(string.Join(" · ", p.Candidates.Select(c => E(c.Key) + $" ({c.CandidateScore:F3})")) + "</p><h3>主体上的可见文字</h3><pre>" + E(p.ProductText.Length == 0 ? "未识别到主体文字" : p.ProductText) + "</pre>");
                body.Append($"<details><summary>全部 OCR 文字（{p.Lines.Length} 行）</summary><pre>" + E(string.Join("\n", p.Lines.Select(l => (l.Accepted ? "[采用] " : l.OnForeground ? "[低分] " : "[其他] ") + l.Text))) + $"</pre></details><p><a href='{p.Id}/transparent.png'>透明图</a> · <a href='{p.Id}/alpha.png'>Alpha</a> · <a href='{p.Id}/product.json'>坐标和候选</a></p>");
            }
            body.Append("</article>");
        }
        return body.Append("<script>document.getElementById('q').addEventListener('input',e=>{let q=e.target.value.toLowerCase();document.querySelectorAll('article').forEach(a=>a.hidden=!a.textContent.toLowerCase().includes(q))})</script></body></html>").ToString();
    }
    public void Dispose() { _ocr?.Dispose(); _matting?.Dispose(); }
}
