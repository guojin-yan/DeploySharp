using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

internal static class InspectionOutput
{
    public static void WriteJson(string file, object value) => File.WriteAllText(file,
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    public static void SaveSummary(string root, InspectionManifest manifest, IReadOnlyList<SceneAnalysisResult> reports)
    {
        var events = reports.SelectMany(r => r.Events.Select(e => new { r.Snapshot, Event = e })).ToArray();
        WriteJson(Path.Combine(root, "summary.json"), new { manifest.Name, Snapshots = reports.Count,
            ObjectObservations = reports.Sum(r => r.Objects.Length), PendingReviewEvents = events.Length,
            Counting = "snapshot-observations-not-unique-people-or-traffic-flow", Reports = reports });
        var csv = new StringBuilder("snapshot,camera,source_seconds,zone,count,triggered,object_ids,evidence\r\n");
        foreach (var report in reports)
            foreach (var zone in report.Zones)
                csv.AppendLine(string.Join(',', new[] { report.Snapshot.Id, report.Camera.Id,
                    report.Snapshot.SourceSeconds.ToString(CultureInfo.InvariantCulture), zone.Id,
                    zone.Count.ToString(CultureInfo.InvariantCulture), zone.Triggered ? "true" : "false",
                    string.Join(';', zone.ObjectIds), report.Snapshot.Id + "/scene.jpg" }.Select(Csv)));
        File.WriteAllText(Path.Combine(root, "observations.csv"), csv.ToString(), new UTF8Encoding(true));
        var review = new StringBuilder("event,snapshot,camera,zone,reason,object_ids,status,evidence\r\n");
        foreach (var item in events)
            review.AppendLine(string.Join(',', new[] { item.Event.Id, item.Snapshot.Id, item.Snapshot.CameraId,
                item.Event.ZoneId, item.Event.Reason, string.Join(';', item.Event.ObjectIds), item.Event.Status,
                item.Snapshot.Id + "/" + item.Event.Evidence }.Select(Csv)));
        File.WriteAllText(Path.Combine(root, "review.csv"), review.ToString(), new UTF8Encoding(true));
        var body = new StringBuilder();
        foreach (var report in reports)
        {
            string directory = report.Snapshot.Id;
            body.Append($"<article data-camera='{H(report.Camera.Id)}' data-event='{(report.Events.Length > 0 ? "yes" : "no")}'><h2>{H(directory)} · {report.Snapshot.SourceSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s</h2><p>{H(report.Camera.Name)}</p><a href='{directory}/scene.jpg'><img src='{directory}/scene.jpg' alt='巡检区域及目标叠框'></a><div class='details'>");
            foreach (var zone in report.Zones)
                body.Append($"<p><span class='{(zone.Triggered ? "review" : "normal")}'>{(zone.Triggered ? "待复核" : "未触发")}</span> {H(zone.Name)}：{zone.Count} 个目标</p>");
            foreach (var obj in report.Objects.Where(o => o.Semantic != null))
            {
                var match = obj.Semantic!;
                body.Append($"<p class='match'><a href='{directory}/roi-{obj.Id:D2}.jpg'>ROI #{obj.Id}</a> · {H(match.Matches[0].Key)} · 候选分数 {match.Matches[0].CandidateScore.ToString("0.000", CultureInfo.InvariantCulture)}{(match.RequiresReview ? " · 描述有歧义" : "")}</p>");
            }
            body.Append($"<p><a href='{directory}/scene.json'>完整 JSON</a> · 检测 {report.Objects.Length} · CLIP {report.Objects.Count(o => o.Semantic != null)}</p></div></article>");
        }
        string cameraOptions = string.Join("", manifest.Cameras.Select(c => $"<option value='{H(c.Id)}'>{H(c.Name)}</option>"));
        string page = $$$"""
        <!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>DeploySharp 园区巡检复核台</title><style>
        *{box-sizing:border-box}body{margin:0;background:#eff4f8;color:#173047;font:16px/1.65 system-ui,sans-serif}header,main{max-width:1440px;margin:auto;padding:28px}header{background:#10283f;color:#fff;border-radius:0 0 20px 20px}h1{margin:4px 0;font-size:34px}h2{font-size:21px;margin:0}header p{color:#c7dce5}.stats{display:flex;gap:14px;flex-wrap:wrap}.stats b{font-size:25px;color:#83decf}.stats span{padding:12px 20px;background:#203e53;border-radius:10px}nav{display:flex;gap:18px;flex-wrap:wrap;margin:18px 0}select,button{padding:8px;border:1px solid #b8c9d5;border-radius:6px;background:white}section{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:22px}article{background:#fff;border-radius:14px;overflow:hidden;padding:18px;box-shadow:0 4px 15px #1730470b}img{width:100%;height:295px;object-fit:contain;background:#e8edf2}article p{margin:8px 0}a{color:#187f78}.review,.normal{border-radius:4px;padding:2px 7px;background:#fff1dc;color:#8d4d14}.normal{background:#e3f1eb;color:#276e5d}.match{font-size:14px;color:#4a6475}.note{padding:14px;background:#e0eaf1;border-radius:9px}.details{padding-top:10px}@media(max-width:800px){section{grid-template-columns:1fr}h1{font-size:26px}}
        </style><header><div>DeploySharp 2.0.0 · NanoDet-Plus + CLIP · {{{H(reports[0].Backend)}}}</div><h1>园区车辆与人员巡检复核台</h1><p>公开固定视角素材复现 · 区域计数、车型候选与事件证据</p><div class="stats"><span><b>{{{reports.Count}}}</b> 张截图</span><span><b>{{{reports.Sum(r => r.Objects.Length)}}}</b> 次目标观测</span><span><b>{{{events.Length}}}</b> 条待复核事件</span></div></header>
        <main><p class="note">同一目标可在不同截图重复出现；计数不是去重人数或交通流量。未触发不代表安全或无人。CLIP 仅为候选检索建议，不作为事件触发条件。</p><nav><label>摄像头 <select id="camera"><option value="all">全部</option>{{{cameraOptions}}}</select></label><label><input id="events" type="checkbox"> 只看待复核截图</label><a href="observations.csv">区域观测 CSV</a><a href="review.csv">事件清单 CSV</a><a href="summary.json">汇总 JSON</a></nav><section>{{{body}}}</section></main>
        <script>function filter(){document.querySelectorAll('article').forEach(x=>x.hidden=(camera.value!=='all'&&x.dataset.camera!==camera.value)||(events.checked&&x.dataset.event!=='yes'))}const camera=document.getElementById('camera'),events=document.getElementById('events');camera.onchange=events.onchange=filter;</script></html>
        """;
        File.WriteAllText(Path.Combine(root, "index.html"), page);
    }
    private static string H(string text) => WebUtility.HtmlEncode(text);
    private static string Csv(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
}
