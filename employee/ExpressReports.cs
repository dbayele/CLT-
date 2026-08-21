using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class ExpressReports
{
    public static void Map(WebApplication app)
    {
        var dbPath = app.Configuration["CLTPP_PUBLIC_SAFETY_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "public-safety.db");
        var evidenceRoot = app.Configuration["CLTPP_PUBLIC_SAFETY_EVIDENCE_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "data", "public-safety-evidence");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        Directory.CreateDirectory(evidenceRoot);
        EnsureSchema(dbPath);

        app.MapGet("/express-reports", async (HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await using var db = Open(dbPath);
            var rows = new List<string>();
            await using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Id, ReportNumber, Status, Title, UpdatedAt, CreatedBy FROM ExpressReports ORDER BY UpdatedAt DESC LIMIT 100";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add($"<tr><td><a href='/express-reports/{r.GetString(0)}'><b>{H(r.GetString(1))}</b></a></td><td>{H(r.GetString(2))}</td><td>{H(r.IsDBNull(3)?"Untitled":r.GetString(3))}</td><td>{H(r.GetString(5))}</td><td>{H(r.GetString(4))}</td></tr>");
            return Html(Page("Express Reports", $"""
            <main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · EXPRESS REPORT</span><h1>Express reports</h1><p>Voice-first police report drafting with visible recording, live transcript editing, and evidence uploads.</p></div><a class='button' href='/express-reports/new'>New express report</a></section>
            <section class='table-card'><table><thead><tr><th>Report</th><th>Status</th><th>Title</th><th>Officer</th><th>Updated</th></tr></thead><tbody>{(rows.Count==0?"<tr><td colspan='5' class='empty'>No express reports.</td></tr>":string.Join("",rows))}</tbody></table></section></main>
            """));
        }).RequireAuthorization();

        app.MapGet("/express-reports/new", (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("New Express Report", EditorHtml(null, token)));
        }).RequireAuthorization();

        app.MapPost("/express-reports", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var f = await ctx.Request.ReadFormAsync();
            var id = Guid.NewGuid();
            var number = $"EXP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000,999999)}";
            var now = DateTimeOffset.UtcNow.ToString("O");
            await using var db = Open(dbPath);
            await using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO ExpressReports(Id,ReportNumber,Status,Title,Transcript,Location,CreatedBy,CreatedAt,UpdatedAt) VALUES($id,$n,'Draft',$t,$x,$l,$o,$c,$u)";
            cmd.Parameters.AddWithValue("$id", id.ToString()); cmd.Parameters.AddWithValue("$n", number);
            cmd.Parameters.AddWithValue("$t", f["title"].ToString().Trim()); cmd.Parameters.AddWithValue("$x", f["transcript"].ToString()); cmd.Parameters.AddWithValue("$l", f["location"].ToString().Trim());
            cmd.Parameters.AddWithValue("$o", ctx.User.Identity?.Name ?? "officer"); cmd.Parameters.AddWithValue("$c", now); cmd.Parameters.AddWithValue("$u", now);
            await cmd.ExecuteNonQueryAsync();
            return Results.Redirect($"/express-reports/{id}");
        }).RequireAuthorization();

        app.MapGet("/express-reports/{id:guid}", async (Guid id, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var report = await ReadReport(dbPath,id);
            if (report is null) return Results.NotFound();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page(report.Value.Number, EditorHtml(report, token)));
        }).RequireAuthorization();

        app.MapPost("/express-reports/{id:guid}/save", async (Guid id, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var f = await ctx.Request.ReadFormAsync();
            await using var db=Open(dbPath); await using var cmd=db.CreateCommand();
            cmd.CommandText="UPDATE ExpressReports SET Title=$t,Transcript=$x,Location=$l,Status='Draft',UpdatedAt=$u WHERE Id=$id AND Status<>'Submitted'";
            cmd.Parameters.AddWithValue("$t",f["title"].ToString().Trim());cmd.Parameters.AddWithValue("$x",f["transcript"].ToString());cmd.Parameters.AddWithValue("$l",f["location"].ToString().Trim());cmd.Parameters.AddWithValue("$u",DateTimeOffset.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$id",id.ToString());
            await cmd.ExecuteNonQueryAsync(); return Results.Redirect($"/express-reports/{id}");
        }).RequireAuthorization();

        app.MapPost("/express-reports/{id:guid}/submit", async (Guid id, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var f=await ctx.Request.ReadFormAsync(); var transcript=f["transcript"].ToString().Trim(); if(string.IsNullOrWhiteSpace(transcript)) return Results.BadRequest("Transcript/narrative is required.");
            await using var db=Open(dbPath); await using var cmd=db.CreateCommand();cmd.CommandText="UPDATE ExpressReports SET Title=$t,Transcript=$x,Location=$l,Status='Submitted',SubmittedAt=$s,SubmittedBy=$o,UpdatedAt=$s WHERE Id=$id AND Status<>'Submitted'";
            cmd.Parameters.AddWithValue("$t",f["title"].ToString().Trim());cmd.Parameters.AddWithValue("$x",transcript);cmd.Parameters.AddWithValue("$l",f["location"].ToString().Trim());cmd.Parameters.AddWithValue("$s",DateTimeOffset.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$o",ctx.User.Identity?.Name??"officer");cmd.Parameters.AddWithValue("$id",id.ToString());await cmd.ExecuteNonQueryAsync();return Results.Redirect($"/express-reports/{id}");
        }).RequireAuthorization();

        // Visible recording only: browser sends MediaRecorder chunks while the recording indicator is active.
        app.MapPost("/express-reports/{id:guid}/audio", async (Guid id, HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            if (await ReadReport(dbPath,id) is null) return Results.NotFound();
            var dir=Path.Combine(evidenceRoot,id.ToString("N"));Directory.CreateDirectory(dir);var fileName=$"audio-{DateTime.UtcNow:yyyyMMddHHmmssfff}.webm";var path=Path.Combine(dir,fileName);
            await using(var fs=File.Create(path)) await ctx.Request.Body.CopyToAsync(fs);
            await AddAttachment(dbPath,id,fileName,"audio/webm",new FileInfo(path).Length,Sha256(path),ctx.User.Identity?.Name??"officer","Audio narration chunk",path);
            return Results.Ok(new{saved=true});
        }).RequireAuthorization();

        app.MapPost("/express-reports/{id:guid}/evidence", async (Guid id, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx); if(await ReadReport(dbPath,id) is null)return Results.NotFound();
            var form=await ctx.Request.ReadFormAsync();var note=form["note"].ToString().Trim();var files=form.Files;
            if(files.Count==0)return Results.BadRequest("Choose at least one photo or video.");
            var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"image/jpeg","image/png","image/webp","video/mp4","video/quicktime","video/webm"};
            var dir=Path.Combine(evidenceRoot,id.ToString("N"));Directory.CreateDirectory(dir);
            foreach(var file in files){if(!allowed.Contains(file.ContentType))continue;if(file.Length>250L*1024*1024)continue;var ext=Path.GetExtension(Path.GetFileName(file.FileName));var stored=$"evidence-{Guid.NewGuid():N}{ext}";var path=Path.Combine(dir,stored);await using(var fs=File.Create(path))await file.CopyToAsync(fs);await AddAttachment(dbPath,id,file.FileName,file.ContentType,file.Length,Sha256(path),ctx.User.Identity?.Name??"officer",note,path);}
            return Results.Redirect($"/express-reports/{id}");
        }).RequireAuthorization();

        app.MapGet("/express-reports/{id:guid}/evidence/{attachmentId:guid}", async (Guid id,Guid attachmentId,HttpContext ctx)=>
        {
            if(!CanPolice(ctx.User))return Results.Forbid();await using var db=Open(dbPath);await using var cmd=db.CreateCommand();cmd.CommandText="SELECT StoredPath,ContentType,OriginalName FROM ExpressReportAttachments WHERE Id=$a AND ExpressReportId=$r";cmd.Parameters.AddWithValue("$a",attachmentId.ToString());cmd.Parameters.AddWithValue("$r",id.ToString());await using var rd=await cmd.ExecuteReaderAsync();if(!await rd.ReadAsync())return Results.NotFound();var path=rd.GetString(0);if(!File.Exists(path))return Results.NotFound();return Results.File(path,rd.GetString(1),rd.GetString(2));
        }).RequireAuthorization();
    }

    private static string EditorHtml((Guid Id,string Number,string Status,string Title,string Transcript,string Location)? r,string token)
    {
        var id=r?.Id;var action=id is null?"/express-reports":$"/express-reports/{id}/save";var attachments=id is null?"":AttachmentHtml(id.Value);
        return $"""
        <main class='wrap'><a class='back' href='/express-reports'>← Express reports</a><section class='page-head'><div><span class='eyebrow'>POLICE · EXPRESS REPORT</span><h1>{H(r?.Number??"New express report")}</h1><p>Status: <b>{H(r?.Status??"New")}</b></p></div></section>
        <div class='alert'><b>Microphone recording is visible and autosaved.</b><span>When recording is active, audio chunks are stored with this draft. Stop recording before leaving the page.</span></div>
        <section class='card'><form id='reportForm' method='post' action='{action}'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Report title<input name='title' value='{H(r?.Title)}' placeholder='Brief incident title'/></label><label>Location<input name='location' value='{H(r?.Location)}' placeholder='Incident location'/></label><label>Transcript / narrative<textarea id='transcript' name='transcript' rows='18'>{H(r?.Transcript)}</textarea></label>
        {(id is null?"<p>Create the draft first to enable recording and evidence uploads.</p><button type='submit'>Create draft</button>":$"<div class='actions'><button type='submit'>Save draft</button>{(r?.Status=="Submitted"?"":$"<button type='submit' formaction='/express-reports/{id}/submit'>Submit report</button>")}<button type='button' id='recordButton'>Start recording</button><strong id='recordState' aria-live='polite'>Not recording</strong></div>")}</form></section>
        {attachments}
        {(id is null?"":$"""<section class='card'><h2>Photo & video evidence</h2><form method='post' enctype='multipart/form-data' action='/express-reports/{id}/evidence'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Evidence files<input type='file' name='files' multiple accept='image/jpeg,image/png,image/webp,video/mp4,video/quicktime,video/webm'/></label><label>Officer note<textarea name='note' rows='3'></textarea></label><button type='submit'>Upload evidence</button></form></section>{RecorderScript(id.Value)}""")}
        </main>""";
    }

    private static string AttachmentHtml(Guid id) => "<section class='card'><h2>Evidence & narration</h2><p>Uploaded files and recorded narration are retained with the draft and are police-only.</p><div id='attachmentList'><a href='/express-reports/"+id+"'>Refresh to view newly uploaded evidence.</a></div></section>";

    private static string RecorderScript(Guid id) => $"""<script>
(()=>{{const btn=document.getElementById('recordButton'),state=document.getElementById('recordState'),text=document.getElementById('transcript');if(!btn)return;let rec=null,stream=null,recognition=null;const SR=window.SpeechRecognition||window.webkitSpeechRecognition;if(SR){{recognition=new SR();recognition.continuous=true;recognition.interimResults=true;recognition.onresult=e=>{{let final='';for(let i=e.resultIndex;i<e.results.length;i++)if(e.results[i].isFinal)final+=e.results[i][0].transcript+' ';if(final)text.value+=(text.value?' ':'')+final.trim();}};}}
btn.onclick=async()=>{{if(rec&&rec.state==='recording'){{rec.stop();stream.getTracks().forEach(t=>t.stop());recognition?.stop();btn.textContent='Start recording';state.textContent='Not recording';return;}}stream=await navigator.mediaDevices.getUserMedia({{audio:true}});rec=new MediaRecorder(stream);rec.ondataavailable=async e=>{{if(e.data.size)await fetch('/express-reports/{id}/audio',{{method:'POST',body:e.data,headers:{{'Content-Type':'audio/webm'}}}});}};rec.start(5000);recognition?.start();btn.textContent='Stop recording';state.textContent='● Recording — audio is being autosaved';}};}})();</script>""";

    private static async Task AddAttachment(string dbPath,Guid reportId,string name,string contentType,long length,string hash,string officer,string? note,string path){await using var db=Open(dbPath);await using var cmd=db.CreateCommand();cmd.CommandText="INSERT INTO ExpressReportAttachments(Id,ExpressReportId,OriginalName,ContentType,ByteLength,Sha256,UploadedBy,UploadedAt,OfficerNote,StoredPath) VALUES($i,$r,$n,$c,$b,$h,$u,$d,$o,$p)";cmd.Parameters.AddWithValue("$i",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$r",reportId.ToString());cmd.Parameters.AddWithValue("$n",name);cmd.Parameters.AddWithValue("$c",contentType);cmd.Parameters.AddWithValue("$b",length);cmd.Parameters.AddWithValue("$h",hash);cmd.Parameters.AddWithValue("$u",officer);cmd.Parameters.AddWithValue("$d",DateTimeOffset.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$o",note??"");cmd.Parameters.AddWithValue("$p",path);await cmd.ExecuteNonQueryAsync();}
    private static async Task<(Guid Id,string Number,string Status,string Title,string Transcript,string Location)?> ReadReport(string path,Guid id){await using var db=Open(path);await using var cmd=db.CreateCommand();cmd.CommandText="SELECT Id,ReportNumber,Status,COALESCE(Title,''),COALESCE(Transcript,''),COALESCE(Location,'') FROM ExpressReports WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());await using var r=await cmd.ExecuteReaderAsync();return await r.ReadAsync()?(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5)):null;}
    private static void EnsureSchema(string path){using var db=Open(path);using var cmd=db.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS ExpressReports(Id TEXT PRIMARY KEY,ReportNumber TEXT NOT NULL UNIQUE,Status TEXT NOT NULL,Title TEXT,Transcript TEXT,Location TEXT,CreatedBy TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,SubmittedAt TEXT,SubmittedBy TEXT);CREATE TABLE IF NOT EXISTS ExpressReportAttachments(Id TEXT PRIMARY KEY,ExpressReportId TEXT NOT NULL,OriginalName TEXT NOT NULL,ContentType TEXT NOT NULL,ByteLength INTEGER NOT NULL,Sha256 TEXT NOT NULL,UploadedBy TEXT NOT NULL,UploadedAt TEXT NOT NULL,OfficerNote TEXT,StoredPath TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_ExpressAttachments_Report ON ExpressReportAttachments(ExpressReportId);""";cmd.ExecuteNonQuery();}
    private static SqliteConnection Open(string p){var c=new SqliteConnection($"Data Source={p}");c.Open();return c;}
    private static string Sha256(string path){using var sha=SHA256.Create();using var fs=File.OpenRead(path);return Convert.ToHexString(sha.ComputeHash(fs));}
    private static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>c.Value.Equals("Police",StringComparison.OrdinalIgnoreCase));
    private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string H(string? s)=>WebUtility.HtmlEncode(s??"");
    private static string Page(string title,string body)=>$"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(title)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Public Safety</div></header>{body}</body></html>";
}
