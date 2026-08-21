using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Partners;

public static class DaWorkflows
{
    public static void Map(WebApplication app, string dbPath)
    {
        EnsureSchema(dbPath);

        app.MapGet("/da/express-reports/{reportId:guid}/notes", async (Guid reportId, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!IsDa(ctx.User)) return Results.Forbid();
            var report = await LoadReport(dbPath, reportId);
            if (report is null) return Results.NotFound();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            var notes = await NotesHtml(dbPath, reportId);
            return Html(Page("DA notes", $"""
            <main class='wrap'><a class='back' href='/express-reports/{reportId}'>← Express report</a>
            <section class='page-head'><div><span class='eyebrow'>DISTRICT ATTORNEY · WORK PRODUCT</span><h1>DA notes</h1><p>{H(report.Value.Number)} · {H(report.Value.Title)}</p></div></section>
            <div class='detail-grid'><section class='card'><h2>Express report context</h2><dl><dt>Status</dt><dd>{H(report.Value.Status)}</dd><dt>Location</dt><dd>{H(report.Value.Location)}</dd><dt>Officer</dt><dd>{H(report.Value.CreatedBy)}</dd></dl><h3>Narrative / transcript</h3><p style='white-space:pre-wrap'>{H(report.Value.Transcript)}</p></section>
            <aside><section class='card'><h2>Add prosecutor note</h2><form method='post' action='/da/express-reports/{reportId}/notes'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Note<textarea name='note' rows='8' required></textarea></label><button>Add note</button></form></section></aside></div>
            <section class='card'><h2>DA note history</h2>{notes}</section></main>"""));
        }).RequireAuthorization();

        app.MapPost("/da/express-reports/{reportId:guid}/notes", async (Guid reportId, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!IsDa(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            if (await LoadReport(dbPath, reportId) is null) return Results.NotFound();
            var form = await ctx.Request.ReadFormAsync();
            var text = form["note"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(text)) return Results.BadRequest("Note is required.");
            await using var cn = Open(dbPath); await cn.OpenAsync();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO DaExpressReportNotes(Id,ExpressReportId,Author,Note,CreatedAt) VALUES($id,$r,$a,$n,$t)";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$r", reportId.ToString());
            cmd.Parameters.AddWithValue("$a", ctx.User.Identity?.Name ?? "prosecutor");
            cmd.Parameters.AddWithValue("$n", text);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            return Results.Redirect($"/da/express-reports/{reportId}/notes");
        }).RequireAuthorization();

        app.MapGet("/da/charges/new", async (HttpContext ctx, IAntiforgery anti, Guid expressReportId) =>
        {
            if (!IsDa(ctx.User)) return Results.Forbid();
            var report = await LoadReport(dbPath, expressReportId);
            if (report is null) return Results.NotFound();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("Create charge", $"""
            <main class='wrap'><a class='back' href='/express-reports/{expressReportId}'>← Express report</a>
            <section class='page-head'><div><span class='eyebrow'>DISTRICT ATTORNEY · CHARGING</span><h1>Create charge</h1><p>{H(report.Value.Number)} · {H(report.Value.Title)}</p></div></section>
            <div class='detail-grid'><section class='card'><h2>Express report context</h2><dl><dt>Location</dt><dd>{H(report.Value.Location)}</dd><dt>Officer</dt><dd>{H(report.Value.CreatedBy)}</dd><dt>Status</dt><dd>{H(report.Value.Status)}</dd></dl><h3>Narrative / transcript</h3><p style='white-space:pre-wrap'>{H(report.Value.Transcript)}</p></section>
            <aside><section class='card'><h2>Charge worksheet</h2><div class='notice'><b>Prosecutor selection required</b><p>CLT++ does not recommend a statute or charge. Select and verify the applicable North Carolina statute and offense level/class.</p></div>
            <form method='post' action='/da/charges'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><input type='hidden' name='expressReportId' value='{expressReportId}'/>
            <label>NC General Statute citation<input name='statuteCitation' placeholder='14-72' required/></label><label>Offense title<input name='offenseTitle' required/></label>
            <label>Offense level / class<select name='offenseLevel' required><option value=''>Select</option><optgroup label='Felony'><option>Class A felony</option><option>Class B1 felony</option><option>Class B2 felony</option><option>Class C felony</option><option>Class D felony</option><option>Class E felony</option><option>Class F felony</option><option>Class G felony</option><option>Class H felony</option><option>Class I felony</option></optgroup><optgroup label='Misdemeanor'><option>Class A1 misdemeanor</option><option>Class 1 misdemeanor</option><option>Class 2 misdemeanor</option><option>Class 3 misdemeanor</option></optgroup><option>Infraction / other</option></select></label>
            <label>Count / allegation detail<textarea name='allegation' rows='5' required></textarea></label><label>Prosecutor notes<textarea name='notes' rows='4'></textarea></label><label>Statute source version<input name='sourceVersion' value='NC General Statutes through S.L. 2026-30' required/></label><button>Create proposed charge</button></form></section></aside></div></main>"""));
        }).RequireAuthorization();

        app.MapPost("/da/charges", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!IsDa(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var f = await ctx.Request.ReadFormAsync();
            if (!Guid.TryParse(f["expressReportId"], out var reportId) || await LoadReport(dbPath, reportId) is null) return Results.BadRequest("Valid Express Report is required.");
            var statute=f["statuteCitation"].ToString().Trim(); var title=f["offenseTitle"].ToString().Trim(); var level=f["offenseLevel"].ToString().Trim(); var allegation=f["allegation"].ToString().Trim();
            if(new[]{statute,title,level,allegation}.Any(string.IsNullOrWhiteSpace)) return Results.BadRequest("Statute, offense title, offense level/class, and allegation are required.");
            await using var cn=Open(dbPath); await cn.OpenAsync();
            var cmd=cn.CreateCommand();cmd.CommandText="""INSERT INTO DaCharges(Id,ExpressReportId,StatuteCitation,OffenseTitle,OffenseLevel,Allegation,ProsecutorNotes,SourceVersion,Status,CreatedBy,CreatedAt,UpdatedAt) VALUES($id,$r,$s,$t,$l,$a,$n,$v,'Proposed',$by,$at,$at)""";
            void P(string k,string v)=>cmd.Parameters.AddWithValue(k,v);P("$id",Guid.NewGuid().ToString());P("$r",reportId.ToString());P("$s",statute);P("$t",title);P("$l",level);P("$a",allegation);P("$n",f["notes"].ToString().Trim());P("$v",f["sourceVersion"].ToString().Trim());P("$by",ctx.User.Identity?.Name??"prosecutor");P("$at",DateTimeOffset.UtcNow.ToString("O"));await cmd.ExecuteNonQueryAsync();
            return Results.Redirect($"/da/charges?expressReportId={reportId}");
        }).RequireAuthorization();

        app.MapGet("/da/charges", async (HttpContext ctx, Guid? expressReportId) =>
        {
            if(!IsDa(ctx.User)) return Results.Forbid();
            await using var cn=Open(dbPath); await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT Id,ExpressReportId,StatuteCitation,OffenseTitle,OffenseLevel,Status,CreatedBy,CreatedAt FROM DaCharges WHERE ($rid='' OR ExpressReportId=$rid) ORDER BY CreatedAt DESC";cmd.Parameters.AddWithValue("$rid",expressReportId?.ToString()??"");var rows=new List<string>();await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<tr><td><b>{H(r.GetString(2))}</b></td><td>{H(r.GetString(3))}</td><td>{H(r.GetString(4))}</td><td>{H(r.GetString(5))}</td><td>{H(r.GetString(6))}</td><td>{H(r.GetString(7))}</td></tr>");return Html(Page("DA charges",$"""<main class='wrap'><a class='back' href='{(expressReportId.HasValue?$"/express-reports/{expressReportId}":"/")}'>← Back</a><section class='page-head'><div><span class='eyebrow'>DISTRICT ATTORNEY</span><h1>Charges</h1><p>Prosecutor-entered charging records linked to Express Reports.</p></div></section><section class='table-card'><table><thead><tr><th>Statute</th><th>Offense</th><th>Level / class</th><th>Status</th><th>Prosecutor</th><th>Created</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='6'>No charges recorded.</td></tr>")}</tbody></table></section></main>"""));
        }).RequireAuthorization();
    }

    private static void EnsureSchema(string dbPath){using var cn=Open(dbPath);cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS DaExpressReportNotes(Id TEXT PRIMARY KEY,ExpressReportId TEXT NOT NULL,Author TEXT NOT NULL,Note TEXT NOT NULL,CreatedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_DaNotes_Report ON DaExpressReportNotes(ExpressReportId);CREATE TABLE IF NOT EXISTS DaCharges(Id TEXT PRIMARY KEY,ExpressReportId TEXT NOT NULL,StatuteCitation TEXT NOT NULL,OffenseTitle TEXT NOT NULL,OffenseLevel TEXT NOT NULL,Allegation TEXT NOT NULL,ProsecutorNotes TEXT NULL,SourceVersion TEXT NOT NULL,Status TEXT NOT NULL,CreatedBy TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_DaCharges_Report ON DaCharges(ExpressReportId);""";cmd.ExecuteNonQuery();}
    private static async Task<string> NotesHtml(string dbPath,Guid id){await using var cn=Open(dbPath);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT Author,Note,CreatedAt FROM DaExpressReportNotes WHERE ExpressReportId=$id ORDER BY CreatedAt DESC";cmd.Parameters.AddWithValue("$id",id.ToString());var rows=new List<string>();await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<article><b>{H(r.GetString(0))}</b><small>{H(r.GetString(2))}</small><p style='white-space:pre-wrap'>{H(r.GetString(1))}</p></article>");return rows.Count==0?"<p class='muted'>No DA notes yet.</p>":string.Join("",rows);}
    private static async Task<(Guid Id,string Number,string Status,string Title,string Transcript,string Location,string CreatedBy)?> LoadReport(string dbPath,Guid id){await using var cn=Open(dbPath);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT Id,ReportNumber,Status,COALESCE(Title,''),COALESCE(Transcript,''),COALESCE(Location,''),CreatedBy FROM ExpressReports WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());await using var r=await cmd.ExecuteReaderAsync();return await r.ReadAsync()?(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6)):null;}
    private static SqliteConnection Open(string p)=>new($"Data Source={p}");private static bool IsDa(ClaimsPrincipal u)=>u.IsInRole("DistrictAttorney")||u.IsInRole("Administrator");private static string H(string? s)=>WebUtility.HtmlEncode(s??"");private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(t)} · CMPD Partners</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CMPD <span>Partners</span></a><div>District Attorney workspace</div></header>{b}</body></html>";
}
