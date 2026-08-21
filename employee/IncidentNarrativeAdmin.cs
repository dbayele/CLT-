using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class IncidentNarrativeAdmin
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/incident-narratives", async (HttpContext ctx) =>
        {
            if (!CanUse(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            await using var cn=Open(app.Configuration); await cn.OpenAsync();
            var cmd=cn.CreateCommand(); cmd.CommandText="SELECT Id,IncidentNumber,PersonRole,FirstName,LastName,Status,CreatedByEmployee,UpdatedAt FROM IncidentNarratives ORDER BY UpdatedAt DESC";
            var rows=new List<string>(); await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync()) rows.Add($"<tr><td><a href='/incident-narratives/{U(r.GetString(0))}'><b>{H(r.GetString(1))}</b></a></td><td>{H(r.GetString(2))}</td><td>{H(N(r,3))} {H(N(r,4))}</td><td><span class='status'>{H(r.GetString(5))}</span></td><td>{H(r.GetString(6))}<small>{H(r.GetString(7))}</small></td></tr>");
            var html=$"""<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Incident Narratives · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police Incident Narratives</div></header><main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · NARRATIVES</span><h1>Incident Narratives</h1><p>Capture participant identity, role, narrative text, and an explicitly attached audio recording.</p></div><a class='button' href='/incident-narratives/new'>New narrative</a></section><section class='table-card'><table><thead><tr><th>Incident</th><th>Role</th><th>Person</th><th>Status</th><th>Officer / updated</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='5' class='empty'>No narratives found.</td></tr>")}</tbody></table></section></main></body></html>""";
            return Results.Content(html,"text/html; charset=utf-8");
        }).RequireAuthorization();

        app.MapGet("/incident-narratives/new", (HttpContext ctx) =>
        {
            if(!CanUse(ctx.User)) return Results.Forbid();
            return Results.Content(NewPage(),"text/html; charset=utf-8");
        }).RequireAuthorization();

        app.MapPost("/incident-narratives", async (HttpContext ctx) =>
        {
            if(!CanUse(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            var f=await ctx.Request.ReadFormAsync();
            var incident=f["incidentNumber"].ToString().Trim(); if(incident.Length==0)return Results.BadRequest("Incident number is required.");
            var role=f["personRole"].ToString().Trim(); if(role.Length==0)return Results.BadRequest("Person role is required.");
            var id=Guid.NewGuid().ToString(); var now=DateTimeOffset.UtcNow.ToString("O");
            await using var cn=Open(app.Configuration); await cn.OpenAsync(); var cmd=cn.CreateCommand();
            cmd.CommandText="""INSERT INTO IncidentNarratives(Id,IncidentNumber,PersonRole,FirstName,MiddleName,LastName,DateOfBirth,Address,City,State,PostalCode,LicenseNumber,LicenseState,IdScanRaw,Narrative,Status,CreatedByEmployee,CreatedAt,UpdatedAt,SubmittedAt)
VALUES($id,$incident,$role,$first,$middle,$last,$dob,$address,$city,$state,$zip,$dl,$dlstate,$raw,$narrative,'Draft',$officer,$now,$now,NULL)""";
            P(cmd,"$id",id);P(cmd,"$incident",incident);P(cmd,"$role",role);P(cmd,"$first",f["firstName"].ToString().Trim());P(cmd,"$middle",f["middleName"].ToString().Trim());P(cmd,"$last",f["lastName"].ToString().Trim());P(cmd,"$dob",f["dateOfBirth"].ToString().Trim());P(cmd,"$address",f["address"].ToString().Trim());P(cmd,"$city",f["city"].ToString().Trim());P(cmd,"$state",f["state"].ToString().Trim());P(cmd,"$zip",f["postalCode"].ToString().Trim());P(cmd,"$dl",f["licenseNumber"].ToString().Trim());P(cmd,"$dlstate",f["licenseState"].ToString().Trim());P(cmd,"$raw",f["idScanRaw"].ToString());P(cmd,"$narrative",f["narrative"].ToString());P(cmd,"$officer",ctx.User.Identity?.Name??"officer");P(cmd,"$now",now);
            await cmd.ExecuteNonQueryAsync(); return Results.Redirect($"/incident-narratives/{id}");
        }).RequireAuthorization();

        app.MapPost("/incident-narratives/{id:guid}/audio", async (Guid id,HttpContext ctx) =>
        {
            if(!CanUse(ctx.User)) return Results.Forbid(); await EnsureSchemaAsync(app.Configuration);
            var f=await ctx.Request.ReadFormAsync(); var file=f.Files.GetFile("audio"); if(file is null||file.Length==0)return Results.BadRequest("Choose an audio recording."); if(file.Length>25_000_000)return Results.BadRequest("Audio file exceeds 25 MB.");
            await using var ms=new MemoryStream(); await file.CopyToAsync(ms); await using var cn=Open(app.Configuration); await cn.OpenAsync();
            var own=cn.CreateCommand(); own.CommandText="SELECT COUNT(1) FROM IncidentNarratives WHERE Id=$id";own.Parameters.AddWithValue("$id",id.ToString());if(Convert.ToInt32(await own.ExecuteScalarAsync())==0)return Results.NotFound();
            var cmd=cn.CreateCommand();cmd.CommandText="INSERT INTO IncidentNarrativeAudio(Id,IncidentNarrativeId,FileName,ContentType,AudioData,UploadedByEmployee,UploadedAt) VALUES($aid,$nid,$name,$type,$data,$officer,$now)";
            cmd.Parameters.AddWithValue("$aid",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$nid",id.ToString());cmd.Parameters.AddWithValue("$name",Path.GetFileName(file.FileName));cmd.Parameters.AddWithValue("$type",file.ContentType??"application/octet-stream");cmd.Parameters.AddWithValue("$data",ms.ToArray());cmd.Parameters.AddWithValue("$officer",ctx.User.Identity?.Name??"officer");cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await cmd.ExecuteNonQueryAsync();return Results.Redirect($"/incident-narratives/{id}");
        }).RequireAuthorization();

        app.MapPost("/incident-narratives/{id:guid}/submit", async (Guid id,HttpContext ctx) =>
        {
            if(!CanUse(ctx.User)) return Results.Forbid(); await EnsureSchemaAsync(app.Configuration);await using var cn=Open(app.Configuration);await cn.OpenAsync();var cmd=cn.CreateCommand();var now=DateTimeOffset.UtcNow.ToString("O");cmd.CommandText="UPDATE IncidentNarratives SET Status='Submitted',SubmittedAt=$now,UpdatedAt=$now WHERE Id=$id AND Status='Draft'";cmd.Parameters.AddWithValue("$id",id.ToString());cmd.Parameters.AddWithValue("$now",now);return await cmd.ExecuteNonQueryAsync()==0?Results.BadRequest("Narrative is not an editable draft."):Results.Redirect($"/incident-narratives/{id}");
        }).RequireAuthorization();

        app.MapGet("/incident-narratives/{id:guid}", async (Guid id,HttpContext ctx) =>
        {
            if(!CanUse(ctx.User)) return Results.Forbid();await EnsureSchemaAsync(app.Configuration);await using var cn=Open(app.Configuration);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT IncidentNumber,PersonRole,FirstName,MiddleName,LastName,DateOfBirth,Address,City,State,PostalCode,LicenseNumber,LicenseState,Narrative,Status,CreatedByEmployee,CreatedAt,UpdatedAt,SubmittedAt,(SELECT COUNT(*) FROM IncidentNarrativeAudio WHERE IncidentNarrativeId=IncidentNarratives.Id) FROM IncidentNarratives WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return Results.NotFound();
            var submit=r.GetString(13)=="Draft"?$"<form method='post' action='/incident-narratives/{id}/submit'><button>Submit narrative</button></form>":"";
            var html=$"""<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(r.GetString(0))} · Narrative</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police Incident Narratives</div></header><main class='wrap'><a class='back' href='/incident-narratives'>← Incident narratives</a><section class='page-head'><div><span class='eyebrow'>{H(r.GetString(1))} · {H(r.GetString(13))}</span><h1>{H(r.GetString(0))}</h1><p>{H(N(r,2))} {H(N(r,3))} {H(N(r,4))}</p></div>{submit}</section><section class='card'><dl><dt>Date of birth</dt><dd>{H(N(r,5))}</dd><dt>Address</dt><dd>{H(N(r,6))}, {H(N(r,7))} {H(N(r,8))} {H(N(r,9))}</dd><dt>Driver license</dt><dd>{H(N(r,10))} · {H(N(r,11))}</dd><dt>Officer</dt><dd>{H(r.GetString(14))}</dd><dt>Audio recordings</dt><dd>{r.GetInt64(18)}</dd></dl><h2>Narrative</h2><p style='white-space:pre-wrap'>{H(N(r,12))}</p></section><section class='card'><h2>Attach audio recording</h2><p>Use the device recorder or choose an existing recording. Recording is explicit and is not started by CLT++.</p><form method='post' enctype='multipart/form-data' action='/incident-narratives/{id}/audio'><input type='file' name='audio' accept='audio/*' capture required><button>Attach recording</button></form></section></main></body></html>""";return Results.Content(html,"text/html; charset=utf-8");
        }).RequireAuthorization();
    }

    private static string NewPage()=>"""<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>New Incident Narrative · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police Incident Narratives</div></header><main class='wrap'><a class='back' href='/incident-narratives'>← Incident narratives</a><section class='card'><span class='eyebrow'>POLICE · NEW NARRATIVE</span><h1>Record an incident narrative</h1><p>Scan an AAMVA/PDF417 ID using a paired scanner, verify the populated fields, select the person's role, and add the narrative.</p><form method='post' action='/incident-narratives'><label>Incident number<input name='incidentNumber' required></label><label>Person role<select name='personRole' required><option value=''>Select role</option><option>Victim</option><option>Witness</option><option>Offender</option><option>Suspect</option><option>Complainant</option><option>Reporting person</option><option>Parent</option><option>Guardian</option><option>Spouse</option><option>Crash participant</option><option>Driver</option><option>Passenger</option><option>Property owner</option><option>Business representative</option><option>Other</option></select></label><label>ID scanner input<textarea id='scan' name='idScanRaw' rows='5' placeholder='Scan PDF417 barcode here'></textarea></label><button id='parse' type='button'>Populate from ID scan</button><div class='two'><label>First name<input id='firstName' name='firstName'></label><label>Middle name<input id='middleName' name='middleName'></label></div><div class='two'><label>Last name<input id='lastName' name='lastName'></label><label>Date of birth<input id='dateOfBirth' name='dateOfBirth'></label></div><label>Address<input id='address' name='address'></label><div class='two'><label>City<input id='city' name='city'></label><label>State<input id='state' name='state'></label></div><div class='two'><label>Postal code<input id='postalCode' name='postalCode'></label><label>Driver license number<input id='licenseNumber' name='licenseNumber'></label></div><label>License state<input id='licenseState' name='licenseState'></label><label>Narrative<textarea name='narrative' rows='12'></textarea></label><button>Create draft narrative</button></form></section></main><script>
const map={DCS:'lastName',DAC:'firstName',DAD:'middleName',DBB:'dateOfBirth',DAG:'address',DAI:'city',DAJ:'state',DAK:'postalCode',DAQ:'licenseNumber',DAA:'licenseState'};
document.getElementById('parse').onclick=()=>{const raw=document.getElementById('scan').value.replace(/\r/g,'\n');for(const line of raw.split('\n')){const s=line.trim();for(const [code,id] of Object.entries(map)){const i=s.indexOf(code);if(i>=0){let v=s.slice(i+3).trim();if(code==='DBB'&&/^\d{8}$/.test(v))v=v.slice(0,2)+'/'+v.slice(2,4)+'/'+v.slice(4);document.getElementById(id).value=v;break}}}};
</script></body></html>""";

    private static async Task EnsureSchemaAsync(IConfiguration c){await using var cn=Open(c);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS IncidentNarratives(Id TEXT PRIMARY KEY,IncidentNumber TEXT NOT NULL,PersonRole TEXT NOT NULL,FirstName TEXT,MiddleName TEXT,LastName TEXT,DateOfBirth TEXT,Address TEXT,City TEXT,State TEXT,PostalCode TEXT,LicenseNumber TEXT,LicenseState TEXT,IdScanRaw TEXT,Narrative TEXT NOT NULL,Status TEXT NOT NULL,CreatedByEmployee TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,SubmittedAt TEXT);CREATE TABLE IF NOT EXISTS IncidentNarrativeAudio(Id TEXT PRIMARY KEY,IncidentNarrativeId TEXT NOT NULL,FileName TEXT NOT NULL,ContentType TEXT NOT NULL,AudioData BLOB NOT NULL,UploadedByEmployee TEXT NOT NULL,UploadedAt TEXT NOT NULL,FOREIGN KEY(IncidentNarrativeId) REFERENCES IncidentNarratives(Id) ON DELETE CASCADE);CREATE INDEX IF NOT EXISTS IX_IncidentNarratives_Incident ON IncidentNarratives(IncidentNumber);CREATE INDEX IF NOT EXISTS IX_IncidentNarratives_Status ON IncidentNarratives(Status);""";await cmd.ExecuteNonQueryAsync();}
    private static SqliteConnection Open(IConfiguration c){var path=c["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");Directory.CreateDirectory(Path.GetDirectoryName(path)!);return new SqliteConnection($"Data Source={path}");}
    private static bool CanUse(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>string.Equals(c.Value,"Police",StringComparison.OrdinalIgnoreCase));
    private static void P(SqliteCommand c,string k,string v)=>c.Parameters.AddWithValue(k,v);private static string H(string? v)=>WebUtility.HtmlEncode(v??"");private static string U(string v)=>Uri.EscapeDataString(v);private static string? N(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);
}
