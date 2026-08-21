using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class PoliceCallAssignments
{
    public static void Map(WebApplication app)
    {
        var dbPath=app.Configuration["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!); Ensure(dbPath);

        app.MapGet("/police-call-assignment",async(HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPolice(ctx.User)) return Results.Forbid();
            var token=anti.GetAndStoreTokens(ctx).RequestToken!;
            await using var cn=Open(dbPath); await cn.OpenAsync();
            var c=cn.CreateCommand();
            c.CommandText="SELECT Id,CallNumber,CallType,Location,Status,COALESCE(AssignedTo,'') FROM PoliceCalls WHERE Status<>'Closed' ORDER BY CreatedAt";
            var rows=new List<string>(); await using var r=await c.ExecuteReaderAsync();
            while(await r.ReadAsync()) rows.Add($"<tr><td>{H(r.GetString(1))}</td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td><td>{H(r.GetString(4))}</td><td>{H(r.GetString(5))}</td><td><a class='button' href='/police-call-assignment/{H(r.GetString(0))}'>Assign</a></td></tr>");
            return Html(Page("Call assignment",$"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>POLICE · DISPATCH</span><h1>Assign calls</h1><p>Calls may only be placed in the queue of an officer whose duty state has been approved as On Duty.</p></div><a class='button' href='/officer-status/roster'>Officer roster</a></section><section class='table-card'><table><thead><tr><th>Call</th><th>Type</th><th>Location</th><th>Status</th><th>Officer queue</th><th></th></tr></thead><tbody>{string.Join("",rows)}</tbody></table></section></main>"));
        }).RequireAuthorization();

        app.MapGet("/police-call-assignment/{id:guid}",async(Guid id,HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPolice(ctx.User)) return Results.Forbid(); var token=anti.GetAndStoreTokens(ctx).RequestToken!;
            await using var cn=Open(dbPath); await cn.OpenAsync();
            var c=cn.CreateCommand(); c.CommandText="SELECT CallNumber,CallType,Location,COALESCE(AssignedTo,'') FROM PoliceCalls WHERE Id=$i"; c.Parameters.AddWithValue("$i",id.ToString());
            await using var r=await c.ExecuteReaderAsync(); if(!await r.ReadAsync()) return Results.NotFound(); var num=r.GetString(0);var type=r.GetString(1);var loc=r.GetString(2);var assigned=r.GetString(3); await r.DisposeAsync();
            var opts=await OfficerOptions(cn,assigned);
            return Html(Page("Assign call",$"<main class='wrap'><a href='/police-call-assignment'>← Calls</a><section class='card'><span class='eyebrow'>POLICE · DISPATCH</span><h1>{H(num)}</h1><p>{H(type)} · {H(loc)}</p><form method='post' action='/police-call-assignment/{id}'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Approved on-duty officer<select name='officer' required><option value=''>Select officer</option>{opts}</select></label><button>Place in officer queue</button></form></section></main>"));
        }).RequireAuthorization();

        app.MapPost("/police-call-assignment/{id:guid}",async(Guid id,HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPolice(ctx.User)) return Results.Forbid(); await anti.ValidateRequestAsync(ctx); var f=await ctx.Request.ReadFormAsync(); var officer=f["officer"].ToString().Trim(); if(officer.Length==0)return Results.BadRequest("Officer required.");
            await using var cn=Open(dbPath); await cn.OpenAsync(); if(!await IsOnDuty(cn,officer)) return Results.BadRequest("Calls can only be assigned to an approved on-duty officer queue.");
            using var tx=cn.BeginTransaction(); var now=Now();
            var up=cn.CreateCommand();up.Transaction=tx;up.CommandText="UPDATE PoliceCalls SET AssignedTo=$o,Status='Queued',UpdatedAt=$t WHERE Id=$i AND Status<>'Closed'";up.Parameters.AddWithValue("$o",officer);up.Parameters.AddWithValue("$t",now);up.Parameters.AddWithValue("$i",id.ToString());if(await up.ExecuteNonQueryAsync()!=1){tx.Rollback();return Results.NotFound();}
            await AddHistory(cn,tx,id,ctx.User.Identity?.Name??"dispatcher","Queued","Placed in approved on-duty officer queue: "+officer,now);tx.Commit();return Results.Redirect("/police-call-assignment");
        }).RequireAuthorization();

        app.MapGet("/my-call-queue",async(HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPolice(ctx.User)) return Results.Forbid(); var officer=OfficerKey(ctx.User); var token=anti.GetAndStoreTokens(ctx).RequestToken!;
            await using var cn=Open(dbPath);await cn.OpenAsync(); var onDuty=await IsOnDuty(cn,officer);
            var c=cn.CreateCommand();c.CommandText="SELECT Id,CallNumber,CallType,Priority,Location,Status,UpdatedAt FROM PoliceCalls WHERE AssignedTo=$o AND Status IN ('Queued','En route','On scene') ORDER BY CASE Status WHEN 'En route' THEN 0 WHEN 'On scene' THEN 1 ELSE 2 END,UpdatedAt";c.Parameters.AddWithValue("$o",officer);
            var rows=new List<string>();await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){var id=r.GetString(0);var status=r.GetString(5);var action=status=="Queued"?$"<form method='post' action='/my-call-queue/{H(id)}/en-route'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><button {(onDuty?"":"disabled")}>Select call / En route</button></form>":status=="En route"?$"<form method='post' action='/my-call-queue/{H(id)}/on-scene'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><button {(onDuty?"":"disabled")}>Mark on scene</button></form>":"On scene";rows.Add($"<tr><td>{H(r.GetString(1))}</td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td><td>{H(r.GetString(4))}</td><td>{H(status)}</td><td>{action}</td></tr>");}
            return Html(Page("My call queue",$"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>POLICE · OFFICER QUEUE</span><h1>My call queue</h1><p>{H(officer)} · {(onDuty?"Approved On Duty":"Not currently On Duty")}</p></div><a class='button' href='/officer-status'>My status</a></section><section class='table-card'><table><thead><tr><th>Call</th><th>Type</th><th>Priority</th><th>Location</th><th>Status</th><th>Action</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='6'>No calls in your queue.</td></tr>")}</tbody></table></section></main>"));
        }).RequireAuthorization();

        app.MapPost("/my-call-queue/{id:guid}/en-route",async(Guid id,HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPolice(ctx.User))return Results.Forbid();await anti.ValidateRequestAsync(ctx);var officer=OfficerKey(ctx.User);await using var cn=Open(dbPath);await cn.OpenAsync();if(!await IsOnDuty(cn,officer))return Results.BadRequest("You must have an approved On Duty status to select a call.");using var tx=cn.BeginTransaction();var now=Now();var c=cn.CreateCommand();c.Transaction=tx;c.CommandText="UPDATE PoliceCalls SET Status='En route',UpdatedAt=$t WHERE Id=$i AND AssignedTo=$o AND Status='Queued'";c.Parameters.AddWithValue("$t",now);c.Parameters.AddWithValue("$i",id.ToString());c.Parameters.AddWithValue("$o",officer);if(await c.ExecuteNonQueryAsync()!=1){tx.Rollback();return Results.BadRequest("Call is not available in your queue.");}await SetOfficerStatus(cn,tx,officer,"En route",now);await AddHistory(cn,tx,id,officer,"En route","Officer selected call from queue and is en route.",now);tx.Commit();return Results.Redirect("/my-call-queue");
        }).RequireAuthorization();

        app.MapPost("/my-call-queue/{id:guid}/on-scene",async(Guid id,HttpContext ctx,IAntiforgery anti)=>
        {
            if(!CanPolice(ctx.User))return Results.Forbid();await anti.ValidateRequestAsync(ctx);var officer=OfficerKey(ctx.User);await using var cn=Open(dbPath);await cn.OpenAsync();if(!await IsOnDuty(cn,officer))return Results.BadRequest("You must have an approved On Duty status.");using var tx=cn.BeginTransaction();var now=Now();var c=cn.CreateCommand();c.Transaction=tx;c.CommandText="UPDATE PoliceCalls SET Status='On scene',UpdatedAt=$t WHERE Id=$i AND AssignedTo=$o AND Status='En route'";c.Parameters.AddWithValue("$t",now);c.Parameters.AddWithValue("$i",id.ToString());c.Parameters.AddWithValue("$o",officer);if(await c.ExecuteNonQueryAsync()!=1){tx.Rollback();return Results.BadRequest("Only an en-route call assigned to you can be marked on scene.");}await SetOfficerStatus(cn,tx,officer,"On scene",now);await AddHistory(cn,tx,id,officer,"On scene","Officer marked arrival on scene.",now);tx.Commit();return Results.Redirect("/my-call-queue");
        }).RequireAuthorization();
    }

    static async Task<string> OfficerOptions(SqliteConnection cn,string selected){var c=cn.CreateCommand();c.CommandText="SELECT OfficerKey,COALESCE(OperationalStatus,'Available') FROM OfficerDutyStatus WHERE OnDuty=1 ORDER BY CASE OperationalStatus WHEN 'Available' THEN 0 WHEN 'En route' THEN 1 WHEN 'On scene' THEN 2 ELSE 3 END,OfficerKey";var rows=new List<string>();await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){var o=r.GetString(0);rows.Add($"<option value='{H(o)}' {(o==selected?"selected":"")}>{H(o)} · {H(r.GetString(1))}</option>");}return string.Join("",rows);}
    static async Task<bool> IsOnDuty(SqliteConnection cn,string officer){var c=cn.CreateCommand();c.CommandText="SELECT COUNT(1) FROM OfficerDutyStatus WHERE OfficerKey=$o AND OnDuty=1";c.Parameters.AddWithValue("$o",officer);return Convert.ToInt32(await c.ExecuteScalarAsync())==1;}
    static async Task SetOfficerStatus(SqliteConnection cn,SqliteTransaction tx,string officer,string status,string now){var c=cn.CreateCommand();c.Transaction=tx;c.CommandText="UPDATE OfficerDutyStatus SET OperationalStatus=$s,UpdatedAt=$t,UpdatedBy=$o WHERE OfficerKey=$o AND OnDuty=1";c.Parameters.AddWithValue("$s",status);c.Parameters.AddWithValue("$t",now);c.Parameters.AddWithValue("$o",officer);await c.ExecuteNonQueryAsync();var h=cn.CreateCommand();h.Transaction=tx;h.CommandText="INSERT INTO OfficerDutyStatusHistory(Id,OfficerKey,DutyState,OperationalStatus,ChangedBy,ChangedAt) VALUES($i,$o,'On duty',$s,$o,$t)";h.Parameters.AddWithValue("$i",Guid.NewGuid().ToString());h.Parameters.AddWithValue("$o",officer);h.Parameters.AddWithValue("$s",status);h.Parameters.AddWithValue("$t",now);await h.ExecuteNonQueryAsync();}
    static async Task AddHistory(SqliteConnection cn,SqliteTransaction tx,Guid id,string actor,string action,string detail,string now){var h=cn.CreateCommand();h.Transaction=tx;h.CommandText="INSERT INTO PoliceCallHistory(Id,PoliceCallId,Actor,Action,Detail,OccurredAt) VALUES($x,$i,$a,$ac,$d,$t)";h.Parameters.AddWithValue("$x",Guid.NewGuid().ToString());h.Parameters.AddWithValue("$i",id.ToString());h.Parameters.AddWithValue("$a",actor);h.Parameters.AddWithValue("$ac",action);h.Parameters.AddWithValue("$d",detail);h.Parameters.AddWithValue("$t",now);await h.ExecuteNonQueryAsync();}
    static void Ensure(string p){using var cn=Open(p);cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE INDEX IF NOT EXISTS IX_PoliceCalls_AssignedStatus ON PoliceCalls(AssignedTo,Status,UpdatedAt);";c.ExecuteNonQuery();}
    static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.Claims.Any(c=>c.Type==DepartmentAccess.ClaimType&&string.Equals(c.Value,"Police",StringComparison.OrdinalIgnoreCase));
    static string OfficerKey(ClaimsPrincipal u)=>u.FindFirstValue(ClaimTypes.Email)??u.Identity?.Name??"officer";
    static SqliteConnection Open(string p)=>new($"Data Source={p}"); static string Now()=>DateTimeOffset.UtcNow.ToString("O");static string H(object? x)=>WebUtility.HtmlEncode(x?.ToString()??"");static IResult Html(string x)=>Results.Content(x,"text/html; charset=utf-8");static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{H(t)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>{b}</body></html>";
}
