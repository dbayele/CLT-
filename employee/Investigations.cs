using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class Investigations
{
    public static void Map(WebApplication app)
    {
        EnsureSchema(app.Configuration);

        app.MapGet("/investigations", async (HttpContext ctx) =>
        {
            if (!CanDetective(ctx.User)) return Results.Forbid();
            await using var cn = Open(app.Configuration); await cn.OpenAsync();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id,CaseNumber,Title,Status,Priority,LeadDetective,OriginExpressReportNumber,UpdatedAt FROM Investigations ORDER BY UpdatedAt DESC LIMIT 200";
            var rows = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add($"<tr><td><a href='/investigations/{r.GetString(0)}'><b>{H(r.GetString(1))}</b></a><small>{H(N(r,6))}</small></td><td>{H(r.GetString(2))}</td><td><span class='status'>{H(r.GetString(3))}</span></td><td>{H(r.GetString(4))}</td><td>{H(N(r,5))}</td><td>{H(r.GetString(7))}</td></tr>");
            return Html(Page("Investigations", $"""<main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · DETECTIVES</span><h1>Investigations</h1><p>Create investigations independently or from an Express Report.</p></div><a class='button' href='/investigations/new'>New investigation</a></section><section class='table-card'><table><thead><tr><th>Case</th><th>Title</th><th>Status</th><th>Priority</th><th>Lead detective</th><th>Updated</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='6' class='empty'>No investigations found.</td></tr>")}</tbody></table></section></main>"""));
        }).RequireAuthorization();

        app.MapGet("/investigations/new", async (HttpContext ctx, IAntiforgery anti, string? expressReportId) =>
        {
            if (!CanDetective(ctx.User)) return Results.Forbid();
            string source = ""; string sourceNumber = ""; string sourceTitle = ""; string sourceNarrative = ""; string sourceLocation = "";
            if (Guid.TryParse(expressReportId, out var eid))
            {
                await using var cn = Open(app.Configuration); await cn.OpenAsync(); var cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT ReportNumber,COALESCE(Title,''),COALESCE(Transcript,''),COALESCE(Location,'') FROM ExpressReports WHERE Id=$id"; cmd.Parameters.AddWithValue("$id", eid.ToString());
                await using var r = await cmd.ExecuteReaderAsync(); if (await r.ReadAsync()) { source = eid.ToString(); sourceNumber = r.GetString(0); sourceTitle = r.GetString(1); sourceNarrative = r.GetString(2); sourceLocation = r.GetString(3); }
            }
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("New investigation", $"""<main class='wrap'><a class='back' href='/investigations'>← Investigations</a><section class='card'><span class='eyebrow'>DETECTIVE · NEW INVESTIGATION</span><h1>Create investigation</h1>{(string.IsNullOrWhiteSpace(sourceNumber)?"":$"<div class='notice'><b>Originating Express Report</b><p>{H(sourceNumber)} · {H(sourceTitle)}</p></div>")}<form method='post' action='/investigations'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><input type='hidden' name='originExpressReportId' value='{H(source)}'/><input type='hidden' name='originExpressReportNumber' value='{H(sourceNumber)}'/><label>Investigation title<input name='title' value='{H(sourceTitle)}' required/></label><label>Incident / investigation location<input name='location' value='{H(sourceLocation)}'/></label><label>Priority<select name='priority'><option>Routine</option><option>Priority</option><option>Critical</option></select></label><label>Lead detective<input name='leadDetective' value='{H(ctx.User.Identity?.Name)}'/></label><label>Initial investigative summary<textarea name='summary' rows='12'>{H(sourceNarrative)}</textarea></label><button>Create investigation</button></form></section></main>"""));
        }).RequireAuthorization();

        app.MapPost("/investigations", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanDetective(ctx.User)) return Results.Forbid(); await anti.ValidateRequestAsync(ctx); var f = await ctx.Request.ReadFormAsync();
            var title = f["title"].ToString().Trim(); if (string.IsNullOrWhiteSpace(title)) return Results.BadRequest("Title is required.");
            var id = Guid.NewGuid(); var caseNo = $"INV-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000,999999)}"; var now = DateTimeOffset.UtcNow.ToString("O");
            await using var cn = Open(app.Configuration); await cn.OpenAsync(); var cmd = cn.CreateCommand(); cmd.CommandText = """INSERT INTO Investigations(Id,CaseNumber,Title,Status,Priority,Location,Summary,LeadDetective,OriginExpressReportId,OriginExpressReportNumber,CreatedBy,CreatedAt,UpdatedAt) VALUES($id,$n,$t,'Open',$p,$l,$s,$lead,$oid,$on,$by,$now,$now)""";
            void P(string k,string v)=>cmd.Parameters.AddWithValue(k,v); P("$id",id.ToString());P("$n",caseNo);P("$t",title);P("$p",f["priority"].ToString());P("$l",f["location"].ToString().Trim());P("$s",f["summary"].ToString());P("$lead",f["leadDetective"].ToString().Trim());P("$oid",f["originExpressReportId"].ToString());P("$on",f["originExpressReportNumber"].ToString());P("$by",ctx.User.Identity?.Name??"detective");P("$now",now);await cmd.ExecuteNonQueryAsync();
            await AddHistory(cn,id,ctx.User.Identity?.Name??"detective","Created","Investigation created",now); return Results.Redirect($"/investigations/{id}");
        }).RequireAuthorization();

        app.MapGet("/investigations/{id:guid}", async (Guid id,HttpContext ctx,IAntiforgery anti) =>
        {
            if(!CanDetective(ctx.User)) return Results.Forbid(); await using var cn=Open(app.Configuration);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT CaseNumber,Title,Status,Priority,Location,Summary,LeadDetective,OriginExpressReportId,OriginExpressReportNumber,CreatedBy,CreatedAt,UpdatedAt FROM Investigations WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return Results.NotFound();
            var token=anti.GetAndStoreTokens(ctx).RequestToken!;var origin=N(r,8);var originId=N(r,7);var originLink=!string.IsNullOrWhiteSpace(originId)?$"<a href='/express-reports/{H(originId)}'>{H(origin)}</a>":H(origin);var history=await History(app.Configuration,id);
            return Html(Page(r.GetString(0),$"""<main class='wrap'><a class='back' href='/investigations'>← Investigations</a><section class='page-head'><div><span class='eyebrow'>INVESTIGATION · {H(r.GetString(2))}</span><h1>{H(r.GetString(0))}</h1><p>{H(r.GetString(1))}</p></div></section><div class='detail-grid'><section class='card'><dl><dt>Priority</dt><dd>{H(r.GetString(3))}</dd><dt>Location</dt><dd>{H(N(r,4))}</dd><dt>Lead detective</dt><dd>{H(N(r,6))}</dd><dt>Originating Express Report</dt><dd>{originLink}</dd><dt>Created by</dt><dd>{H(r.GetString(9))}</dd></dl><h2>Investigative summary</h2><p style='white-space:pre-wrap'>{H(N(r,5))}</p></section><aside><section class='card'><h2>Update investigation</h2><form method='post' action='/investigations/{id}/update'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Status<select name='status'>{Options(new[]{"Open","Active","Pending","Suspended","Closed"},r.GetString(2))}</select></label><label>Lead detective<input name='leadDetective' value='{H(N(r,6))}'/></label><label>Case note<textarea name='note' rows='5'></textarea></label><button>Save update</button></form></section><section class='card'><h2>History</h2>{history}</section></aside></div></main>"""));
        }).RequireAuthorization();

        app.MapPost("/investigations/{id:guid}/update", async (Guid id,HttpContext ctx,IAntiforgery anti) =>
        {
            if(!CanDetective(ctx.User))return Results.Forbid();await anti.ValidateRequestAsync(ctx);var f=await ctx.Request.ReadFormAsync();var status=f["status"].ToString();var allowed=new[]{"Open","Active","Pending","Suspended","Closed"};if(!allowed.Contains(status))return Results.BadRequest("Invalid status.");var now=DateTimeOffset.UtcNow.ToString("O");await using var cn=Open(app.Configuration);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="UPDATE Investigations SET Status=$s,LeadDetective=$l,UpdatedAt=$u WHERE Id=$id";cmd.Parameters.AddWithValue("$s",status);cmd.Parameters.AddWithValue("$l",f["leadDetective"].ToString().Trim());cmd.Parameters.AddWithValue("$u",now);cmd.Parameters.AddWithValue("$id",id.ToString());if(await cmd.ExecuteNonQueryAsync()==0)return Results.NotFound();var note=f["note"].ToString().Trim();await AddHistory(cn,id,ctx.User.Identity?.Name??"detective","Updated",string.IsNullOrWhiteSpace(note)?$"Status changed to {status}":note,now);return Results.Redirect($"/investigations/{id}");
        }).RequireAuthorization();
    }

    private static void EnsureSchema(IConfiguration c){using var cn=Open(c);cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS Investigations(Id TEXT PRIMARY KEY,CaseNumber TEXT NOT NULL UNIQUE,Title TEXT NOT NULL,Status TEXT NOT NULL,Priority TEXT NOT NULL,Location TEXT,Summary TEXT,LeadDetective TEXT,OriginExpressReportId TEXT,OriginExpressReportNumber TEXT,CreatedBy TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);CREATE TABLE IF NOT EXISTS InvestigationHistory(Id TEXT PRIMARY KEY,InvestigationId TEXT NOT NULL,Employee TEXT NOT NULL,Action TEXT NOT NULL,Note TEXT,CreatedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_Investigations_Status ON Investigations(Status);CREATE INDEX IF NOT EXISTS IX_InvestigationHistory_Investigation ON InvestigationHistory(InvestigationId);""";cmd.ExecuteNonQuery();}
    private static async Task AddHistory(SqliteConnection cn,Guid id,string employee,string action,string note,string at){var cmd=cn.CreateCommand();cmd.CommandText="INSERT INTO InvestigationHistory(Id,InvestigationId,Employee,Action,Note,CreatedAt) VALUES($id,$i,$e,$a,$n,$t)";cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$i",id.ToString());cmd.Parameters.AddWithValue("$e",employee);cmd.Parameters.AddWithValue("$a",action);cmd.Parameters.AddWithValue("$n",note);cmd.Parameters.AddWithValue("$t",at);await cmd.ExecuteNonQueryAsync();}
    private static async Task<string> History(IConfiguration c,Guid id){await using var cn=Open(c);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT Employee,Action,Note,CreatedAt FROM InvestigationHistory WHERE InvestigationId=$i ORDER BY CreatedAt DESC";cmd.Parameters.AddWithValue("$i",id.ToString());var rows=new List<string>();await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<article><b>{H(r.GetString(1))}</b><p>{H(N(r,2))}</p><small>{H(r.GetString(0))} · {H(r.GetString(3))}</small></article>");return rows.Count>0?string.Join("",rows):"<p class='muted'>No history.</p>";}
    private static SqliteConnection Open(IConfiguration c){var p=c["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");Directory.CreateDirectory(Path.GetDirectoryName(p)!);return new SqliteConnection($"Data Source={p}");}
    private static bool CanDetective(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.IsInRole("Detective");
    private static string Options(IEnumerable<string> values,string selected)=>string.Join("",values.Select(v=>$"<option {(v==selected?"selected":"")}>{H(v)}</option>"));private static string? N(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);private static string H(string? v)=>WebUtility.HtmlEncode(v??"");private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string Page(string title,string body)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(title)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police Investigations</div></header>{body}</body></html>";
}
