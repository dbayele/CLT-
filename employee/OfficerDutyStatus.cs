using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class OfficerDutyStatus
{
    private static readonly string[] OperationalStatuses = ["Available", "En route", "On scene", "Unavailable"];

    public static void Map(WebApplication app)
    {
        var dbPath = app.Configuration["CLTPP_PUBLIC_SAFETY_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "public-safety.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        Ensure(dbPath);

        app.MapGet("/officer-status", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var officer = OfficerKey(ctx.User);
            var current = await Load(dbPath, officer);
            var history = await History(dbPath, officer);
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            var isOnDuty = current?.OnDuty == true;
            var statusButtons = string.Join("", OperationalStatuses.Select(s => $"<button name='operationalStatus' value='{H(s)}' {(isOnDuty ? "" : "disabled")}>{H(s)}</button>"));
            var body = $"""
            <main class='wrap'>
              <section class='page-head'><div><span class='eyebrow'>POLICE · OFFICER STATUS</span><h1>Duty & availability</h1><p>{H(officer)}</p></div><a class='button' href='/'>Employee home</a></section>
              <div class='detail-grid'>
                <section class='card'>
                  <h2>Current status</h2>
                  <dl><dt>Duty state</dt><dd><b>{(isOnDuty ? "On duty" : "Off duty")}</b></dd><dt>Operational status</dt><dd>{H(current?.OperationalStatus ?? "—")}</dd><dt>Last changed</dt><dd>{H(current?.UpdatedAt ?? "—")}</dd></dl>
                  <form method='post' action='/officer-status/duty'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><button name='onDuty' value='true'>Mark on duty</button><button name='onDuty' value='false'>Mark off duty</button></form>
                  <hr/>
                  <h2>Operational status</h2><p>Operational status can only be changed while on duty.</p>
                  <form method='post' action='/officer-status/operational'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><div style='display:flex;gap:.5rem;flex-wrap:wrap'>{statusButtons}</div></form>
                </section>
                <aside class='card'><h2>Status history</h2>{history}</aside>
              </div>
            </main>""";
            return Html(Page("Officer status", body));
        }).RequireAuthorization();

        app.MapPost("/officer-status/duty", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var form = await ctx.Request.ReadFormAsync();
            var onDuty = string.Equals(form["onDuty"], "true", StringComparison.OrdinalIgnoreCase);
            var officer = OfficerKey(ctx.User);
            var now = Now();
            await Exec(dbPath,
                "INSERT INTO OfficerDutyStatus(OfficerKey,OnDuty,OperationalStatus,UpdatedAt,UpdatedBy) VALUES($o,$d,$s,$t,$b) ON CONFLICT(OfficerKey) DO UPDATE SET OnDuty=$d,OperationalStatus=$s,UpdatedAt=$t,UpdatedBy=$b",
                ("$o", officer), ("$d", onDuty ? 1 : 0), ("$s", onDuty ? "Available" : null), ("$t", now), ("$b", officer));
            await Audit(dbPath, officer, onDuty ? "On duty" : "Off duty", onDuty ? "Available" : null, officer, now);
            return Results.Redirect("/officer-status");
        }).RequireAuthorization();

        app.MapPost("/officer-status/operational", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var form = await ctx.Request.ReadFormAsync();
            var status = form["operationalStatus"].ToString();
            if (!OperationalStatuses.Contains(status, StringComparer.Ordinal)) return Results.BadRequest("Invalid officer status.");
            var officer = OfficerKey(ctx.User);
            var current = await Load(dbPath, officer);
            if (current?.OnDuty != true) return Results.BadRequest("Officer must be on duty before setting an operational status.");
            var now = Now();
            await Exec(dbPath, "UPDATE OfficerDutyStatus SET OperationalStatus=$s,UpdatedAt=$t,UpdatedBy=$b WHERE OfficerKey=$o", ("$s", status), ("$t", now), ("$b", officer), ("$o", officer));
            await Audit(dbPath, officer, "On duty", status, officer, now);
            return Results.Redirect("/officer-status");
        }).RequireAuthorization();

        app.MapGet("/officer-status/roster", async (HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await using var cn = Open(dbPath); await cn.OpenAsync();
            var c = cn.CreateCommand(); c.CommandText = "SELECT OfficerKey,OnDuty,COALESCE(OperationalStatus,''),UpdatedAt FROM OfficerDutyStatus ORDER BY OnDuty DESC,OperationalStatus,OfficerKey";
            var rows = new List<string>(); await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add($"<tr><td>{H(r.GetString(0))}</td><td>{(r.GetInt32(1)==1?"On duty":"Off duty")}</td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td></tr>");
            return Html(Page("Officer roster", $"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>POLICE · STATUS BOARD</span><h1>Officer status roster</h1></div><a class='button' href='/officer-status'>My status</a></section><section class='table-card'><table><thead><tr><th>Officer</th><th>Duty</th><th>Status</th><th>Updated</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='4'>No officer status records.</td></tr>")}</tbody></table></section></main>"));
        }).RequireAuthorization();
    }

    private sealed record StatusRow(bool OnDuty, string? OperationalStatus, string UpdatedAt);
    static async Task<StatusRow?> Load(string p, string officer){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText="SELECT OnDuty,OperationalStatus,UpdatedAt FROM OfficerDutyStatus WHERE OfficerKey=$o";c.Parameters.AddWithValue("$o",officer);await using var r=await c.ExecuteReaderAsync();return await r.ReadAsync()?new StatusRow(r.GetInt32(0)==1,r.IsDBNull(1)?null:r.GetString(1),r.GetString(2)):null;}
    static async Task<string> History(string p,string officer){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText="SELECT DutyState,COALESCE(OperationalStatus,''),ChangedAt FROM OfficerDutyStatusHistory WHERE OfficerKey=$o ORDER BY ChangedAt DESC LIMIT 50";c.Parameters.AddWithValue("$o",officer);var rows=new List<string>();await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<article><b>{H(r.GetString(0))}{(string.IsNullOrWhiteSpace(r.GetString(1))?"":" · "+H(r.GetString(1)))}</b><small>{H(r.GetString(2))}</small></article>");return rows.Count==0?"<p>No status changes yet.</p>":string.Join("",rows);}
    static async Task Audit(string p,string officer,string duty,string? status,string by,string at)=>await Exec(p,"INSERT INTO OfficerDutyStatusHistory(Id,OfficerKey,DutyState,OperationalStatus,ChangedBy,ChangedAt) VALUES($i,$o,$d,$s,$b,$t)",("$i",Guid.NewGuid()),("$o",officer),("$d",duty),("$s",status),("$b",by),("$t",at));
    static void Ensure(string p){using var cn=Open(p);cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS OfficerDutyStatus(OfficerKey TEXT PRIMARY KEY,OnDuty INTEGER NOT NULL,OperationalStatus TEXT,UpdatedAt TEXT NOT NULL,UpdatedBy TEXT NOT NULL);CREATE TABLE IF NOT EXISTS OfficerDutyStatusHistory(Id TEXT PRIMARY KEY,OfficerKey TEXT NOT NULL,DutyState TEXT NOT NULL,OperationalStatus TEXT,ChangedBy TEXT NOT NULL,ChangedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_OfficerDutyStatus_OnDuty ON OfficerDutyStatus(OnDuty,OperationalStatus);CREATE INDEX IF NOT EXISTS IX_OfficerDutyStatusHistory_Officer ON OfficerDutyStatusHistory(OfficerKey,ChangedAt);";c.ExecuteNonQuery();}
    static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.Claims.Any(c=>c.Type==DepartmentAccess.ClaimType&&string.Equals(c.Value,"Police",StringComparison.OrdinalIgnoreCase));
    static string OfficerKey(ClaimsPrincipal u)=>u.FindFirstValue(ClaimTypes.Email)??u.Identity?.Name??"officer";
    static SqliteConnection Open(string p)=>new($"Data Source={p}");
    static async Task Exec(string p,string sql,params(string,object?)[] ps){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText=sql;foreach(var x in ps)c.Parameters.AddWithValue(x.Item1,x.Item2??DBNull.Value);await c.ExecuteNonQueryAsync();}
    static string Now()=>DateTimeOffset.UtcNow.ToString("O");
    static string H(object? x)=>WebUtility.HtmlEncode(x?.ToString()??"");
    static IResult Html(string x)=>Results.Content(x,"text/html; charset=utf-8");
    static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{H(t)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>{b}</body></html>";
}
