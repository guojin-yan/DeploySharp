using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed record ReviewItem(string PageId, string DocumentId, int RegionIndex, int LineIndex,
    string Reason, string Text, float? Confidence, JYPPX.DeploySharp.Geometry.PointF[]? SourcePolygon);
internal sealed record FieldCandidate(string PageId, int RegionIndex, string Kind, string Value,
    float Confidence, JYPPX.DeploySharp.Geometry.PointF[]? SourcePolygon);

internal static class WorkbenchOutput
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    public static void Write(string root, IReadOnlyList<ArchivePage> pages, float minimumConfidence)
    {
        var review = new List<ReviewItem>();
        var fields = new List<FieldCandidate>();
        foreach (var page in pages)
        {
            if (page.Error != null) review.Add(new(page.Id, page.DocumentId, -1, -1, "page-failed", page.Error, null, null));
            foreach (string reason in page.ReviewReasons ?? [])
                if (reason is not ("text-needs-review" or "region-needs-review")) review.Add(new(page.Id, page.DocumentId, -1, -1, reason, "", null, null));
            foreach (var region in page.Regions)
            {
                if (region.Status == "needs-review") review.Add(new(page.Id, page.DocumentId, region.Index, -1,
                    "region-needs-review", string.Join("; ", region.Diagnostics), null, region.SourcePolygon));
                for (int index = 0; index < region.Lines.Length; index++)
                {
                    var line = region.Lines[index];
                    if (string.IsNullOrWhiteSpace(line.Text) || line.Confidence < minimumConfidence)
                        review.Add(new(page.Id, page.DocumentId, region.Index, index, "text-needs-review", line.Text, line.Confidence, line.SourcePolygon));
                    foreach (Match match in Regex.Matches(line.Text, @"(?<!\d)\d{4}[年./-]\d{1,2}[月./-]\d{1,2}日?(?!\d)"))
                        fields.Add(new(page.Id, region.Index, "date-candidate", match.Value, line.Confidence, line.SourcePolygon));
                    foreach (Match match in Regex.Matches(line.Text, @"(?<!\d)\d+(?:,\d{3})*\.\d{2}(?!\d)"))
                        fields.Add(new(page.Id, region.Index, "decimal-candidate", match.Value, line.Confidence, line.SourcePolygon));
                }
            }
        }
        File.WriteAllText(Path.Combine(root, "review.json"), JsonSerializer.Serialize(review, Json), Utf8);
        File.WriteAllText(Path.Combine(root, "fields.json"), JsonSerializer.Serialize(fields, Json), Utf8);
        Csv(root, "review.csv", ["DocumentId", "PageId", "Region", "Line", "Reason", "Text", "Confidence"],
            review.Select(r => new[] { r.DocumentId, r.PageId, r.RegionIndex.ToString(), r.LineIndex.ToString(), r.Reason, r.Text, r.Confidence?.ToString("F4", CultureInfo.InvariantCulture) ?? "" }));
        Csv(root, "fields.csv", ["PageId", "Region", "Kind", "Value", "Confidence"],
            fields.Select(f => new[] { f.PageId, f.RegionIndex.ToString(), f.Kind, f.Value, f.Confidence.ToString("F4", CultureInfo.InvariantCulture) }));
        string docs = Path.Combine(root, "documents"); Directory.CreateDirectory(docs);
        foreach (var group in pages.GroupBy(p => p.DocumentId))
        {
            var markdown = new StringBuilder("# " + group.Key + "\n\n");
            foreach (var page in group)
            {
                markdown.Append($"## {page.PageIndex + 1}. {page.Title.Replace('\n', ' ')}\n\nStatus: {page.Status}\n\n");
                if (page.Error != null) markdown.Append(page.Error + "\n\n");
                foreach (var region in page.Regions)
                {
                    markdown.Append($"### Region {region.Index}: {region.Category}\n\n");
                    if (region.TablePath != null) markdown.Append($"[Table HTML](../{page.Id}/{region.TablePath})\n\n");
                    if (region.Latex != null) markdown.Append("```latex\n" + region.Latex + "\n```\n\n");
                    foreach (var line in region.Lines) markdown.Append(line.Text + "\n\n");
                }
            }
            File.WriteAllText(Path.Combine(docs, group.Key + ".md"), markdown.ToString(), Utf8);
        }
        File.WriteAllText(Path.Combine(root, "index.html"), Html(pages, review.Count, fields.Count, minimumConfidence), Utf8);
        Console.WriteLine($"Workbench: documents={pages.Select(p => p.DocumentId).Distinct().Count()}; review-items={review.Count}; field-candidates={fields.Count}");
    }

    private static void Csv(string root, string file, string[] headings, IEnumerable<string[]> rows)
    {
        static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        File.WriteAllText(Path.Combine(root, file), string.Join(",", headings) + "\r\n" +
            string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Quote)))), new UTF8Encoding(true));
    }
    private static string E(string? s) => WebUtility.HtmlEncode(s) ?? "";
    private static string Points(JYPPX.DeploySharp.Geometry.PointF[]? polygon) =>
        polygon == null ? "" : string.Join(" ", polygon.Select(p => p.X.ToString("F2", CultureInfo.InvariantCulture) + "," + p.Y.ToString("F2", CultureInfo.InvariantCulture)));

    private static string Html(IReadOnlyList<ArchivePage> pages, int reviewCount, int fieldCount, float confidence)
    {
        var b = new StringBuilder("""
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>DeploySharp 智能文档工作台</title><style>
*{box-sizing:border-box}body{font:16px system-ui;background:#edf3f8;color:#15354a;max-width:1360px;margin:28px auto;padding:0 22px}
header{background:#14394c;color:#f3f8fc;border-radius:20px;padding:28px}h1{margin:0 0 10px;font-size:32px}.metrics{display:flex;gap:26px;margin-top:24px;flex-wrap:wrap}.metrics b{font-size:27px;display:block;color:#80dfcf}
nav{display:flex;gap:12px;flex-wrap:wrap;margin:22px 0}input,select{padding:11px;border:1px solid #bdcdd7;border-radius:9px}input{flex:1;min-width:240px}a{color:#167765}
article{background:#fff;border-radius:16px;padding:22px;margin:18px 0}article[hidden]{display:none}.layout{display:grid;grid-template-columns:minmax(260px,40%) 1fr;gap:24px}.viewer{position:relative;margin:0;align-self:start}.viewer img{width:100%;display:block}.viewer svg{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}
.details{min-width:0}details{padding:8px 0;border-bottom:1px solid #e4eaee}summary{cursor:pointer}.hit{display:block;background:#f4f8fb;border:0;border-left:3px solid #8ecbbd;text-align:left;width:100%;padding:8px;margin:5px 0;cursor:pointer;color:#183b50;overflow-wrap:anywhere}.hit.low{border-left-color:#d59951;background:#fff7eb}.tag{background:#e6f2ec;border-radius:7px;padding:3px 8px;font-size:13px}.review{background:#fff0da}.meta{color:#617c8b;font-size:14px}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f0f5f8;padding:12px}footer{margin:24px 0}.files{line-height:2}
@media(max-width:760px){.layout{grid-template-columns:1fr}h1{font-size:26px}.viewer{max-width:520px}}
</style><body><header><p>DEPLOYSHARP · C# 多模型应用实战</p><h1>智能文档工作台</h1><p>方向校正 · 版面分流 · OCR / 表格 / 公式 · 来源定位 · 分组归档</p><div class="metrics">
""");
        int documents = pages.Select(p => p.DocumentId).Distinct().Count();
        int tables = pages.Sum(p => p.Regions.Count(r => r.TablePath != null));
        int formulas = pages.Sum(p => p.Regions.Count(r => r.Latex != null));
        b.Append($"<span><b>{documents}</b>文档组</span><span><b>{pages.Count}</b>输入页</span><span><b>{tables}</b>表格产物</span><span><b>{formulas}</b>公式产物</span><span><b>{reviewCount}</b>复核项</span><span><b>{fieldCount}</b>规则候选</span></div></header>");
        b.Append("<nav><input id=q placeholder='搜索页名、识别文字或公式'><select id=doc><option value=''>全部文档组</option>");
        foreach (string doc in pages.Select(p => p.DocumentId).Distinct()) b.Append($"<option>{E(doc)}</option>");
        b.Append("</select><select id=status><option value=''>全部状态</option><option value=needs-review>待复核</option><option value=completed>流程完成</option><option value=failed>失败页</option></select></nav><p class=files><a href=archive.json>完整 JSON</a> · <a href=review.csv>复核 CSV</a> · <a href=review.json>复核坐标</a> · <a href=fields.csv>日期/小数候选</a></p>");
        foreach (var p in pages)
        {
            b.Append($"<article data-doc='{E(p.DocumentId)}' data-status='{E(p.Status)}'><h2>{p.PageIndex + 1}. {E(p.Title)} <span class='tag {(p.Status == "needs-review" ? "review" : "")}'>{E(p.Status)}</span></h2><p class=meta>{E(p.DocumentId)} / {E(p.Id)} · 校正 {p.Rotation}° · {p.Width}×{p.Height}</p>");
            if (p.Error != null) { b.Append("<pre>" + E(p.Error) + "</pre></article>"); continue; }
            b.Append($"<div class=layout><figure class=viewer><img loading=lazy src='{p.Id}/source.png' alt='解码源图'><svg viewBox='0 0 {p.SourceWidth} {p.SourceHeight}'><polygon fill='#f6b84740' stroke='#ed7d31' stroke-width='3'></polygon></svg></figure><div class=details><p>点击文字或区域，在左侧原图定位。</p><p><a href='{p.Id}/upright.png'>正向页</a> · <a href='{p.Id}/regions.jpg'>结果框</a> · <a href='{p.Id}/page.json'>页 JSON</a> · <a href='documents/{p.DocumentId}.md'>文档 Markdown</a></p>");
            foreach (var r in p.Regions)
            {
                b.Append($"<details><summary>R{r.Index} · {E(r.Category)} · {r.Lines.Length} 行/格 · {E(r.Status)}</summary><button class=hit data-points='{Points(r.SourcePolygon)}'>定位整个区域 · {E(r.Engine)}</button>");
                if (r.TablePath != null) b.Append($"<p><a href='{p.Id}/{r.TablePath}'>打开表格 HTML</a></p>");
                if (r.ArtifactPath != null) b.Append($"<p><a href='{p.Id}/{r.ArtifactPath}'>任务结果 JSON</a></p>");
                if (r.Latex != null) b.Append("<pre>" + E(r.Latex) + "</pre>");
                for (int i = 0; i < r.Lines.Length; i++)
                {
                    var l = r.Lines[i];
                    b.Append($"<button class='hit {(l.Confidence < confidence || string.IsNullOrWhiteSpace(l.Text) ? "low" : "")}' data-points='{Points(l.SourcePolygon)}'><span class=meta>R{r.Index}/L{i} · {l.Confidence.ToString("F3", CultureInfo.InvariantCulture)}</span> {E(string.IsNullOrWhiteSpace(l.Text) ? "[空文字]" : l.Text)}</button>");
                }
                if (r.Diagnostics.Length > 0) b.Append("<p class=meta>" + E(string.Join("; ", r.Diagnostics)) + "</p>");
                b.Append("</details>");
            }
            b.Append("</div></div></article>");
        }
        return b.Append("""
<footer>规则候选保留原始识别来源；识别内容与表格诊断可在原图复核。</footer><script>
const q=document.querySelector('#q'),doc=document.querySelector('#doc'),status=document.querySelector('#status');
function filter(){document.querySelectorAll('article').forEach(a=>{a.hidden=!(a.textContent.toLowerCase().includes(q.value.toLowerCase())&&(!doc.value||a.dataset.doc===doc.value)&&(!status.value||a.dataset.status===status.value))})}
[q,doc,status].forEach(e=>e.addEventListener('input',filter));
document.querySelectorAll('[data-points]').forEach(e=>e.addEventListener('click',()=>e.closest('article').querySelector('polygon').setAttribute('points',e.dataset.points)));
</script></body></html>
""").ToString();
    }
}
