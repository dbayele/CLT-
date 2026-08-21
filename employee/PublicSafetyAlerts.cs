using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class PublicSafetyAlerts
{
    public static void Map(WebApplication app)
    {
        var dbPath = app.Configuration["CLTPP_PUBLIC_SAFETY_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "public-safety.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        Ensure(dbPath);

        app.MapGet("/public-safety-alerts", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            await using var cn = Open(dbPath); await cn.OpenAsync();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id,Title,Severity,Status,AffectedArea,StartsAt,EndsAt,UpdatedAt FROM PublicSafetyAlerts ORDER BY UpdatedAt DESC LIMIT 200";
            var rows = new List<string>(); await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add($"<tr><td><a href='/public-safety-alerts/{H(r.GetString(0))}'><b>{H(r.GetString(1))}</b></a></td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td><td>{H(N(r,4))}</td><td>{H(N(r,5))}</td><td>{H(N(r,6))}</td><td>{H(r.GetString(7))}</td></tr>");
            return Html(Page("Public Safety Alerts", $"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>CMPD · PUBLIC SAFETY ALERTS</span><h1>Public Safety Alerts</h1><p>Create, publish, schedule, update, expire, and cancel citizen-facing alerts.</p></div><a class='button' href='/public-safety-alerts/new'>New alert</a></section><section class='table-card'><table><thead><tr><th>Alert</th><th>Severity</th><th>Status</th><th>Area</th><th>Starts</th><th>Ends</th><th>Updated</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='7'>No alerts.</td></tr>")}</tbody></table></section></main>"));
        }).RequireAuthorization();

        app.MapGet("/public-safety-alerts/new", (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("New alert", Form(null, token)));
        }).RequireAuthorization();

        app.MapPost("/public-safety-alerts", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPublisher(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx); var f = await ctx.Request.ReadFormAsync();
            var title = f["title"].ToString().Trim(); var message = f["message"].ToString().Trim();
            if (title.Length == 0 || message.Length == 0) return Results.BadRequest("Title and message are required.");
            var id = Guid.NewGuid(); var now = Now();
            await Exec(dbPath,"INSERT INTO PublicSafetyAlerts(Id,Title,Message,Severity,Status,AffectedArea,StartsAt,EndsAt,ExternalUrl,CreatedBy,CreatedAt,UpdatedBy,UpdatedAt) VALUES($i,$t,$m,$sev,'Draft',$a,$s,$e,$u,$b,$n,$b,$n)",("$i",id),("$t",title),("$m",message),("$sev",f["severity"]),("$a",f["affectedArea"]),("$s",f["startsAt"]),("$e",f["endsAt"]),("$u",f["externalUrl"]),("$b",Actor(ctx.User)),("$n",now));
            await Audit(dbPath,id,"Created","Draft",Actor(ctx.User),now);
            return Results.Redirect($"/public-safety-alerts/{id}");
        }).RequireAuthorization();

        app.MapGet("/public-safety-alerts/{id:guid}", async (Guid id, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid(); var x = await Load(dbPath,id); if (x is null) return Results.NotFound();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page(x.Value.Title, Form(x, token)));
        }).RequireAuthorization();

        app.MapPost("/public-safety-alerts/{id:guid}/save", async (Guid id,HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPublisher(ctx.User))return Results.Forbid(); await anti.ValidateRequestAsync(ctx); var f=await ctx.Request.ReadFormAsync(); var now=Now();
            await Exec(dbPath,"UPDATE PublicSafetyAlerts SET Title=$t,Message=$m,Severity=$sev,AffectedArea=$a,StartsAt=$s,EndsAt=$e,ExternalUrl=$u,UpdatedBy=$b,UpdatedAt=$n WHERE Id=$i AND Status IN ('Draft','Scheduled','Published')",("$t",f["title"]),("$m",f["message"]),("$sev",f["severity"]),("$a",f["affectedArea"]),("$s",f["startsAt"]),("$e",f["endsAt"]),("$u",f["externalUrl"]),("$b",Actor(ctx.User)),("$n",now),("$i",id));
            await Audit(dbPath,id,"Updated",null,Actor(ctx.User),now); return Results.Redirect($"/public-safety-alerts/{id}");
        }).RequireAuthorization();

        foreach (var transition in new[]{("publish","Published"),("schedule","Scheduled"),("expire","Expired"),("cancel","Cancelled")})
        {
            app.MapPost($"/public-safety-alerts/{{id:guid}}/{transition.Item1}", async (Guid id,HttpContext ctx,IAntiforgery anti)=>
            {
                if(!CanPublisher(ctx.User))return Results.Forbid(); await anti.ValidateRequestAsync(ctx); var now=Now();
                await Exec(dbPath,"UPDATE PublicSafetyAlerts SET Status=$s,UpdatedBy=$b,UpdatedAt=$n WHERE Id=$i",("$s",transition.Item2),("$b",Actor(ctx.User)),("$n",now),("$i",id));
                await Audit(dbPath,id,transition.Item2,transition.Item2,Actor(ctx.User),now); return Results.Redirect($"/public-safety-alerts/{id}");
            }).RequireAuthorization();
        }

        app.MapGet("/api/public-safety-alerts", async (HttpContext ctx) =>
        {
            await using var cn=Open(dbPath); await cn.OpenAsync(); var cmd=cn.CreateCommand();
            cmd.CommandText="SELECT Id,Title,Message,Severity,AffectedArea,StartsAt,EndsAt,ExternalUrl,UpdatedAt FROM PublicSafetyAlerts WHERE Status='Published' AND (StartsAt IS NULL OR StartsAt='' OR StartsAt<=strftime('%Y-%m-%dT%H:%M:%fZ','now')) AND (EndsAt IS NULL OR EndsAt='' OR EndsAt>=strftime('%Y-%m-%dT%H:%M:%fZ','now')) ORDER BY UpdatedAt DESC";
            var items=new List<object>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) items.Add(new { id=r.GetString(0),title=r.GetString(1),message=r.GetString(2),severity=r.GetString(3),affectedArea=N(r,4),startsAt=N(r,5),endsAt=N(r,6),externalUrl=N(r,7),updatedAt=r.GetString(8)});
            return Results.Ok(items);
        }).AllowAnonymous();
    }

    static string Form((Guid Id,string Title,string Message,string Severity,string Status,string? Area,string? Starts,string? Ends,string? Url)? x,string token)
    {
        var isNew=x is null; var id=x?.Id; var action=isNew?"/public-safety-alerts":$"/public-safety-alerts/{id}/save";
        var controls=isNew?"<button>Create draft</button>":$"<div class='actions'><button>Save</button><button formaction='/public-safety-alerts/{id}/publish'>Publish now</button><button formaction='/public-safety-alerts/{id}/schedule'>Schedule</button><button formaction='/public-safety-alerts/{id}/expire'>Expire</button><button formaction='/public-safety-alerts/{id}/cancel'>Cancel</button></div>";
        return $"<main class='wrap'><a href='/public-safety-alerts'>← Alerts</a><section class='card'><span class='eyebrow'>CMPD · PUBLIC SAFETY ALERT</span><h1>{H(x?.Title??"New alert")}</h1><p>Status: <b>{H(x?.Status??"New")}</b></p><form method='post' action='{action}'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Title<input name='title' required value='{H(x?.Title)}'/></label><label>Severity<select name='severity'>{Opts(new[]{"Information","Advisory","Warning","Critical"},x?.Severity)}</select></label><label>Affected area<input name='affectedArea' value='{H(x?.Area)}' placeholder='Uptown, Division, neighborhood, citywide'/></label><label>Starts at<input type='datetime-local' name='startsAt' value='{H(x?.Starts)}'/></label><label>Ends at<input type='datetime-local' name='endsAt' value='{H(x?.Ends)}'/></label><label>Optional link<input type='url' name='externalUrl' value='{H(x?.Url)}'/></label><label>Message<textarea name='message' rows='10' required>{H(x?.Message)}</textarea></label>{controls}</form></section></main>";
    }

    static async Task<(Guid Id,string Title,string Message,string Severity,string Status,string? Area,string? Starts,string? Ends,string? Url)?> Load(string p,Guid id){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText="SELECT Id,Title,Message,Severity,Status,AffectedArea,StartsAt,EndsAt,ExternalUrl FROM PublicSafetyAlerts WHERE Id=$i";c.Parameters.AddWithValue("$i",id.ToString());await using var r=await c.ExecuteReaderAsync();return await r.ReadAsync()?(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),N(r,5),N(r,6),N(r,7),N(r,8)):null;}
    static async Task Audit(string p,Guid id,string action,string? status,string actor,string at)=>await Exec(p,"INSERT INTO PublicSafetyAlertAudit(Id,AlertId,Action,Status,Actor,OccurredAt) VALUES($i,$a,$x,$s,$u,$t)",("$i",Guid.NewGuid()),("$a",id),("$x",action),("$s",status),("$u",actor),("$t",at));
    static void Ensure(string p){using var cn=Open(p);cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS PublicSafetyAlerts(Id TEXT PRIMARY KEY,Title TEXT NOT NULL,Message TEXT NOT NULL,Severity TEXT NOT NULL,Status TEXT NOT NULL,AffectedArea TEXT,StartsAt TEXT,EndsAt TEXT,ExternalUrl TEXT,CreatedBy TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedBy TEXT NOT NULL,UpdatedAt TEXT NOT NULL);CREATE TABLE IF NOT EXISTS PublicSafetyAlertAudit(Id TEXT PRIMARY KEY,AlertId TEXT NOT NULL,Action TEXT NOT NULL,Status TEXT,Actor TEXT NOT NULL,OccurredAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_PublicSafetyAlerts_Status ON PublicSafetyAlerts(Status,StartsAt,EndsAt,UpdatedAt);CREATE INDEX IF NOT EXISTS IX_PublicSafetyAlertAudit_Alert ON PublicSafetyAlertAudit(AlertId,OccurredAt);";c.ExecuteNonQuery();}
    static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.Claims.Any(c=>c.Type==DepartmentAccess.ClaimType&&string.Equals(c.Value,"Police",StringComparison.OrdinalIgnoreCase));
    static bool CanPublisher(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.IsInRole("Commander");
    static string Actor(ClaimsPrincipal u)=>u.FindFirstValue(ClaimTypes.Email)??u.Identity?.Name??"employee";
    static SqliteConnection Open(string p)=>new($"Data Source={p}");
    static async Task Exec(string p,string sql,params(string,object?)[] ps){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText=sql;foreach(var x in ps)c.Parameters.AddWithValue(x.Item1,x.Item2??DBNull.Value);await c.ExecuteNonQueryAsync();}
    static string? N(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);
    static string Now()=>DateTimeOffset.UtcNow.ToString("O");
    static string H(object? x)=>WebUtility.HtmlEncode(x?.ToString()??"");
    static string Opts(IEnumerable<string> xs,string? selected)=>string.Join("",xs.Select(x=>$"<option {(string.Equals(x,selected,StringComparison.OrdinalIgnoreCase)?"selected":"")}>{H(x)}</option>"));
    static IResult Html(string x)=>Results.Content(x,"text/html; charset=utf-8");
    static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{H(t)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>{b}</body></html>";
}
