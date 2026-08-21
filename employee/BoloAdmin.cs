using System.Net;
using System.Security.Claims;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class BoloAdmin
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/bolos", async (HttpContext ctx, string? status) =>
        {
            if (!CanUseBolo(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            await using var cn = Open(app.Configuration); await cn.OpenAsync();
            var list = new List<BoloRow>();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id,TrackingNumber,BoloType,Summary,SubjectName,VehicleDescription,LicensePlate,PlateState,LastKnownLocation,Priority,SubmittedByEmployee,Status,SubmittedAt,ExpiresAt,RecalledByEmployee,RecallReason,RecalledAt FROM BoloRecords WHERE ($status='' OR Status=$status) ORDER BY CASE WHEN Status='Active' THEN 0 ELSE 1 END, SubmittedAt DESC";
            cmd.Parameters.AddWithValue("$status", status ?? "");
            await using var r = await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync()) list.Add(new(
                r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),N(r,4),N(r,5),N(r,6),N(r,7),N(r,8),r.GetString(9),r.GetString(10),r.GetString(11),r.GetString(12),N(r,13),N(r,14),N(r,15),N(r,16)));

            var rows = string.Join("", list.Select(x=>$"<tr><td><a href='/bolos/{U(x.Id)}'><b>{H(x.Tracking)}</b></a><small>{H(x.BoloType)}</small></td><td>{H(x.Summary)}</td><td>{H(x.SubjectName ?? x.VehicleDescription ?? x.LicensePlate ?? "—")}</td><td><span class='status'>{H(x.Priority)} · {H(x.Status)}</span></td><td>{H(x.SubmittedBy)}<small>{H(x.SubmittedAt)}</small></td></tr>"));
            var body=$"""
            <!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>BOLOs · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>
            <header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police BOLO management</div></header>
            <main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · BOLO</span><h1>Be On the Lookout</h1><p>Submit, review, and recall BOLOs. Recalled records remain in the audit history.</p></div><a class='button' href='/bolos/new'>Submit BOLO</a></section>
            <form class='filters' method='get'><select name='status'><option value=''>All statuses</option><option {(status=="Active"?"selected":"")}>Active</option><option {(status=="Recalled"?"selected":"")}>Recalled</option><option {(status=="Expired"?"selected":"")}>Expired</option></select><button>Filter</button></form>
            <section class='table-card'><table><thead><tr><th>BOLO</th><th>Summary</th><th>Subject / vehicle</th><th>Priority / status</th><th>Submitted</th></tr></thead><tbody>{(rows.Length>0?rows:"<tr><td colspan='5' class='empty'>No BOLOs found.</td></tr>")}</tbody></table></section></main></body></html>""";
            return Results.Content(body,"text/html; charset=utf-8");
        }).RequireAuthorization();

        app.MapGet("/bolos/new", (HttpContext ctx) =>
        {
            if (!CanUseBolo(ctx.User)) return Results.Forbid();
            var body="""
            <!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Submit BOLO · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>
            <header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police BOLO management</div></header>
            <main class='wrap'><a class='back' href='/bolos'>← BOLOs</a><section class='card'><span class='eyebrow'>NEW BOLO</span><h1>Submit a BOLO</h1><form method='post' action='/bolos'>
            <label>Type<select name='boloType'><option value='person'>Person</option><option value='vehicle'>Vehicle</option><option value='person-vehicle'>Person + vehicle</option><option value='other'>Other</option></select></label>
            <label>Priority<select name='priority'><option>Routine</option><option>High</option><option>Critical</option></select></label>
            <label>Summary<input name='summary' maxlength='300' required></label>
            <label>Details<textarea name='details' rows='5'></textarea></label>
            <label>Subject name<input name='subjectName'></label><label>Subject description<textarea name='subjectDescription' rows='3'></textarea></label>
            <label>Vehicle description<input name='vehicleDescription'></label><label>License plate<input name='licensePlate'></label><label>Plate state<input name='plateState' maxlength='3' value='NC'></label><label>VIN<input name='vin' maxlength='17'></label>
            <label>Last known location<input name='lastKnownLocation'></label><label>Reason<textarea name='reason' rows='3'></textarea></label><label>Expiration (optional)<input name='expiresAt' type='datetime-local'></label>
            <button>Submit BOLO</button></form></section></main></body></html>""";
            return Results.Content(body,"text/html; charset=utf-8");
        }).RequireAuthorization();

        app.MapPost("/bolos", async (HttpContext ctx) =>
        {
            if (!CanUseBolo(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            var f=await ctx.Request.ReadFormAsync();
            var summary=f["summary"].ToString().Trim(); if(summary.Length==0) return Results.BadRequest("Summary is required.");
            var id=Guid.NewGuid().ToString(); var tracking=$"BOLO-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000,999999)}"; var now=DateTimeOffset.UtcNow.ToString("O");
            await using var cn=Open(app.Configuration); await cn.OpenAsync(); var cmd=cn.CreateCommand();
            cmd.CommandText="""INSERT INTO BoloRecords(Id,TrackingNumber,BoloType,Summary,Details,SubjectName,SubjectDescription,VehicleDescription,LicensePlate,PlateState,Vin,LastKnownLocation,Latitude,Longitude,Reason,Priority,SubmittedByEmployee,Status,SubmittedAt,ExpiresAt,RecalledByEmployee,RecallReason,RecalledAt,UpdatedAt)
            VALUES($id,$tracking,$type,$summary,$details,$subject,$subjectDesc,$vehicle,$plate,$state,$vin,$location,NULL,NULL,$reason,$priority,$employee,'Active',$now,$expires,NULL,NULL,NULL,$now)""";
            void P(string k,string v)=>cmd.Parameters.AddWithValue(k,v);
            P("$id",id);P("$tracking",tracking);P("$type",f["boloType"].ToString());P("$summary",summary);P("$details",f["details"].ToString().Trim());P("$subject",f["subjectName"].ToString().Trim());P("$subjectDesc",f["subjectDescription"].ToString().Trim());P("$vehicle",f["vehicleDescription"].ToString().Trim());P("$plate",f["licensePlate"].ToString().Trim().ToUpperInvariant());P("$state",f["plateState"].ToString().Trim().ToUpperInvariant());P("$vin",f["vin"].ToString().Trim().ToUpperInvariant());P("$location",f["lastKnownLocation"].ToString().Trim());P("$reason",f["reason"].ToString().Trim());P("$priority",f["priority"].ToString());P("$employee",ctx.User.Identity?.Name??"officer");P("$now",now);P("$expires",DateTimeOffset.TryParse(f["expiresAt"],out var exp)?exp.ToString("O"):"");
            await cmd.ExecuteNonQueryAsync(); return Results.Redirect($"/bolos/{id}");
        }).RequireAuthorization();

        app.MapGet("/bolos/{id:guid}", async (Guid id,HttpContext ctx) =>
        {
            if(!CanUseBolo(ctx.User)) return Results.Forbid(); await EnsureSchemaAsync(app.Configuration); await using var cn=Open(app.Configuration); await cn.OpenAsync(); var cmd=cn.CreateCommand(); cmd.CommandText="SELECT TrackingNumber,BoloType,Summary,Details,SubjectName,SubjectDescription,VehicleDescription,LicensePlate,PlateState,Vin,LastKnownLocation,Reason,Priority,SubmittedByEmployee,Status,SubmittedAt,ExpiresAt,RecalledByEmployee,RecallReason,RecalledAt FROM BoloRecords WHERE Id=$id"; cmd.Parameters.AddWithValue("$id",id.ToString()); await using var r=await cmd.ExecuteReaderAsync(); if(!await r.ReadAsync()) return Results.NotFound();
            var status=r.GetString(14); var recall=status=="Active"?$"<section class='card'><h2>Recall BOLO</h2><form method='post' action='/bolos/{id}/recall'><label>Recall reason<textarea name='reason' required rows='4'></textarea></label><button>Recall BOLO</button></form></section>":$"<section class='card'><h2>Recall history</h2><p><b>Recalled by:</b> {H(N(r,17))}</p><p><b>Reason:</b> {H(N(r,18))}</p><p><b>At:</b> {H(N(r,19))}</p></section>";
            var html=$"""<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(r.GetString(0))} · BOLO</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police BOLO management</div></header><main class='wrap'><a class='back' href='/bolos'>← BOLOs</a><section class='page-head'><div><span class='eyebrow'>{H(r.GetString(12))} · {H(status)}</span><h1>{H(r.GetString(2))}</h1><p>{H(r.GetString(0))}</p></div></section><div class='detail-grid'><section class='card'><h2>BOLO details</h2><dl><dt>Type</dt><dd>{H(r.GetString(1))}</dd><dt>Submitted by</dt><dd>{H(r.GetString(13))}</dd><dt>Submitted</dt><dd>{H(r.GetString(15))}</dd><dt>Expires</dt><dd>{H(N(r,16))}</dd><dt>Subject</dt><dd>{H(N(r,4))}</dd><dt>Subject description</dt><dd>{H(N(r,5))}</dd><dt>Vehicle</dt><dd>{H(N(r,6))}</dd><dt>Plate</dt><dd>{H(N(r,7))} {H(N(r,8))}</dd><dt>VIN</dt><dd>{H(N(r,9))}</dd><dt>Last known location</dt><dd>{H(N(r,10))}</dd><dt>Reason</dt><dd>{H(N(r,11))}</dd></dl><p>{H(N(r,3))}</p></section><aside>{recall}</aside></div></main></body></html>"""; return Results.Content(html,"text/html; charset=utf-8");
        }).RequireAuthorization();

        app.MapPost("/bolos/{id:guid}/recall", async (Guid id,HttpContext ctx) =>
        {
            if(!CanUseBolo(ctx.User)) return Results.Forbid(); var f=await ctx.Request.ReadFormAsync(); var reason=f["reason"].ToString().Trim(); if(reason.Length==0)return Results.BadRequest("Recall reason is required."); await EnsureSchemaAsync(app.Configuration); await using var cn=Open(app.Configuration); await cn.OpenAsync(); var cmd=cn.CreateCommand(); cmd.CommandText="UPDATE BoloRecords SET Status='Recalled',RecalledByEmployee=$by,RecallReason=$reason,RecalledAt=$now,UpdatedAt=$now WHERE Id=$id AND Status='Active'"; cmd.Parameters.AddWithValue("$by",ctx.User.Identity?.Name??"officer");cmd.Parameters.AddWithValue("$reason",reason);cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$id",id.ToString());var changed=await cmd.ExecuteNonQueryAsync();return changed==0?Results.BadRequest("BOLO is not active or was already recalled."):Results.Redirect($"/bolos/{id}");
        }).RequireAuthorization();
    }

    private static bool CanUseBolo(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>string.Equals(c.Value,"Police",StringComparison.OrdinalIgnoreCase));
    private static SqliteConnection Open(IConfiguration c){var p=c["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");Directory.CreateDirectory(Path.GetDirectoryName(p)!);return new($"Data Source={p}");}
    private static async Task EnsureSchemaAsync(IConfiguration c){await using var cn=Open(c);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS BoloRecords(Id TEXT PRIMARY KEY,TrackingNumber TEXT NOT NULL UNIQUE,BoloType TEXT NOT NULL,Summary TEXT NOT NULL,Details TEXT NULL,SubjectName TEXT NULL,SubjectDescription TEXT NULL,VehicleDescription TEXT NULL,LicensePlate TEXT NULL,PlateState TEXT NULL,Vin TEXT NULL,LastKnownLocation TEXT NULL,Latitude REAL NULL,Longitude REAL NULL,Reason TEXT NULL,Priority TEXT NOT NULL,SubmittedByEmployee TEXT NOT NULL,Status TEXT NOT NULL,SubmittedAt TEXT NOT NULL,ExpiresAt TEXT NULL,RecalledByEmployee TEXT NULL,RecallReason TEXT NULL,RecalledAt TEXT NULL,UpdatedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_Bolo_Status ON BoloRecords(Status);""";await cmd.ExecuteNonQueryAsync();}
    private static string? N(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i); private static string H(string? s)=>WebUtility.HtmlEncode(s??"");private static string U(string s)=>Uri.EscapeDataString(s);
    private sealed record BoloRow(string Id,string Tracking,string BoloType,string Summary,string? SubjectName,string? VehicleDescription,string? LicensePlate,string? PlateState,string? LastLocation,string Priority,string SubmittedBy,string Status,string SubmittedAt,string? ExpiresAt,string? RecalledBy,string? RecallReason,string? RecalledAt);
}
