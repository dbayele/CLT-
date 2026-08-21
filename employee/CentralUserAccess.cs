using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class CentralUserAccess
{
    public static void Map(WebApplication app)
    {
        var accessDb=app.Configuration["CLTPP_ACCESS_DB"]??Path.Combine(AppContext.BaseDirectory,"data","access-control.db");
        Directory.CreateDirectory(Path.GetDirectoryName(accessDb)!);Ensure(accessDb);

        app.MapGet("/admin/user-access",async(HttpContext ctx,string? email)=>
        {
            if(!IsAdmin(ctx.User))return Results.Forbid();
            await using var cn=Open(accessDb);await cn.OpenAsync();var c=cn.CreateCommand();
            c.CommandText="SELECT Email,FeatureKey,Scope,Effect,COALESCE(ExpiresAt,''),GrantedBy,GrantedAt FROM FeatureGrants WHERE ($e='' OR lower(Email)=lower($e)) ORDER BY Email,FeatureKey,GrantedAt DESC";
            c.Parameters.AddWithValue("$e",email??"");var rows=new List<string>();await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<tr><td>{H(r.GetString(0))}</td><td>{H(r.GetString(1))}</td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td><td>{H(r.GetString(4))}</td><td>{H(r.GetString(5))}</td><td>{H(r.GetString(6))}</td></tr>");
            return Html(Page("User access",$"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>ADMINISTRATION</span><h1>Central user access</h1><p>Grant or revoke access to specific CLT++ features by email address.</p></div></section><form class='filters'><input type='email' name='email' value='{H(email)}' placeholder='user@example.com'/><button>Search</button></form><section class='card'><h2>Grant or deny feature</h2><form method='post' action='/admin/user-access'><label>Email<input type='email' name='email' required/></label><label>Feature<select name='feature'>{Options(Features)}</select></label><label>Scope<input name='scope' value='global' required/></label><label>Effect<select name='effect'><option>Allow</option><option>Deny</option></select></label><label>Expires at (optional)<input type='datetime-local' name='expiresAt'/></label><label>Reason<textarea name='reason' required></textarea></label><button>Save permission</button></form></section><section class='table-card'><table><thead><tr><th>Email</th><th>Feature</th><th>Scope</th><th>Effect</th><th>Expires</th><th>Changed by</th><th>Changed</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='7'>No grants found.</td></tr>")}</tbody></table></section></main>"));
        }).RequireAuthorization();

        app.MapPost("/admin/user-access",async(HttpContext ctx,IAntiforgery anti)=>
        {
            if(!IsAdmin(ctx.User))return Results.Forbid();await anti.ValidateRequestAsync(ctx);var f=await ctx.Request.ReadFormAsync();var email=f["email"].ToString().Trim().ToLowerInvariant();var feature=f["feature"].ToString();var scope=f["scope"].ToString().Trim();var effect=f["effect"].ToString();var reason=f["reason"].ToString().Trim();if(email.Length==0||!Features.Contains(feature)||scope.Length==0||effect is not ("Allow" or "Deny")||reason.Length==0)return Results.BadRequest();var now=Now();await Exec(accessDb,"INSERT INTO FeatureGrants(Id,Email,FeatureKey,Scope,Effect,ExpiresAt,Reason,GrantedBy,GrantedAt) VALUES($i,$e,$f,$s,$x,NULLIF($exp,''),$r,$by,$t)",( "$i",Guid.NewGuid()),("$e",email),("$f",feature),("$s",scope),("$x",effect),("$exp",f["expiresAt"]),("$r",reason),("$by",ctx.User.Identity?.Name??"administrator"),("$t",now));await Exec(accessDb,"INSERT INTO FeatureGrantAudit(Id,Email,FeatureKey,Scope,Effect,Reason,Actor,OccurredAt) VALUES($i,$e,$f,$s,$x,$r,$a,$t)",( "$i",Guid.NewGuid()),("$e",email),("$f",feature),("$s",scope),("$x",effect),("$r",reason),("$a",ctx.User.Identity?.Name??"administrator"),("$t",now));return Results.Redirect("/admin/user-access?email="+Uri.EscapeDataString(email));
        }).RequireAuthorization();
    }

    public static async Task<bool> HasFeatureAsync(IConfiguration config,ClaimsPrincipal user,string feature,string scope="global")
    {
        if(user.IsInRole("Administrator"))return true;var email=(user.FindFirstValue(ClaimTypes.Email)??user.Identity?.Name??"").Trim().ToLowerInvariant();if(email.Length==0)return false;var p=config["CLTPP_ACCESS_DB"]??Path.Combine(AppContext.BaseDirectory,"data","access-control.db");Ensure(p);await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText="SELECT Effect FROM FeatureGrants WHERE lower(Email)=lower($e) AND FeatureKey=$f AND (Scope=$s OR Scope='global') AND (ExpiresAt IS NULL OR ExpiresAt='' OR ExpiresAt>$now) ORDER BY CASE WHEN Scope=$s THEN 0 ELSE 1 END,GrantedAt DESC LIMIT 1";c.Parameters.AddWithValue("$e",email);c.Parameters.AddWithValue("$f",feature);c.Parameters.AddWithValue("$s",scope);c.Parameters.AddWithValue("$now",Now());var x=await c.ExecuteScalarAsync();return string.Equals(x?.ToString(),"Allow",StringComparison.OrdinalIgnoreCase);
    }

    static readonly string[] Features={"employee.calls-intake","employee.inbound-queues","employee.chime-voice","employee.chime-video","employee.express-reports","employee.warrants","employee.tow","employee.bolo","employee.investigations","employee.crash-reports","employee.trespass","employee.vcat","employee.swat","partners.da","partners.judge","partners.attorney","partners.court-clerk","partners.jail","partners.probation","partners.social-work","partners.bail-bondsman","partners.electronic-monitoring","admin.user-access"};
    static bool IsAdmin(ClaimsPrincipal u)=>u.IsInRole("Administrator");static string Options(IEnumerable<string> xs)=>string.Join("",xs.Select(x=>$"<option value='{H(x)}'>{H(x)}</option>"));
    static void Ensure(string p){Directory.CreateDirectory(Path.GetDirectoryName(p)!);using var cn=Open(p);cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS FeatureGrants(Id TEXT PRIMARY KEY,Email TEXT NOT NULL,FeatureKey TEXT NOT NULL,Scope TEXT NOT NULL,Effect TEXT NOT NULL,ExpiresAt TEXT,Reason TEXT NOT NULL,GrantedBy TEXT NOT NULL,GrantedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_FeatureGrants_EmailFeature ON FeatureGrants(Email,FeatureKey,Scope,GrantedAt);CREATE TABLE IF NOT EXISTS FeatureGrantAudit(Id TEXT PRIMARY KEY,Email TEXT NOT NULL,FeatureKey TEXT NOT NULL,Scope TEXT NOT NULL,Effect TEXT NOT NULL,Reason TEXT NOT NULL,Actor TEXT NOT NULL,OccurredAt TEXT NOT NULL);";c.ExecuteNonQuery();}
    static SqliteConnection Open(string p)=>new($"Data Source={p}");static async Task Exec(string p,string sql,params(string,object?)[] ps){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText=sql;foreach(var x in ps)c.Parameters.AddWithValue(x.Item1,x.Item2?.ToString()??"");await c.ExecuteNonQueryAsync();}static string Now()=>DateTimeOffset.UtcNow.ToString("O");static string H(object? x)=>WebUtility.HtmlEncode(x?.ToString()??"");static IResult Html(string x)=>Results.Content(x,"text/html; charset=utf-8");static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{H(t)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>{b}</body></html>";
}
