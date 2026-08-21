using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;

namespace CltPlusPlus.Employee;

public static class CrashReportAdmin
{
    public static void Map(WebApplication app)
    {
        EnsureSchema(app.Configuration);

        app.MapGet("/crash-reports", async (HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await using var cn = Open(app.Configuration); await cn.OpenAsync();
            var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT Id,ReportNumber,CrashDateTime,Location,Status,CreatedByEmployee,UpdatedAt FROM CrashReports349 ORDER BY UpdatedAt DESC";
            var rows = new List<string>(); await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add($"<tr><td><a href='/crash-reports/{r.GetString(0)}'><b>{H(r.GetString(1))}</b></a></td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td><td><span class='status'>{H(r.GetString(4))}</span></td><td>{H(r.GetString(5))}<small>{H(r.GetString(6))}</small></td></tr>");
            return Html(Page("DMV-349 crash reports", $"""<main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · NC DMV-349</span><h1>Crash reports</h1><p>Structured North Carolina crash reports with DMV-349 coded fields.</p></div><a class='button' href='/crash-reports/new'>New crash report</a></section><section class='table-card'><table><thead><tr><th>Report</th><th>Date/time</th><th>Location</th><th>Status</th><th>Officer</th></tr></thead><tbody>{(rows.Count > 0 ? string.Join("", rows) : "<tr><td colspan='5' class='empty'>No crash reports.</td></tr>")}</tbody></table></section></main>"""));
        }).RequireAuthorization();

        app.MapGet("/crash-reports/new", (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid(); var t = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("New DMV-349 crash report", $"""<main class='wrap'><a class='back' href='/crash-reports'>← Crash reports</a><section class='card'><span class='eyebrow'>NC DMV-349</span><h1>Create crash report</h1>
            <form method='post' action='/crash-reports'><input type='hidden' name='__RequestVerificationToken' value='{H(t)}'/>
            <h2>Reportability</h2><label><input type='checkbox' name='fatality'> Fatality</label><label><input type='checkbox' name='injury'> Non-fatal injury</label><label>Total property damage ($)<input type='number' min='0' name='propertyDamage' value='0'></label><label><input type='checkbox' name='seizedVehicleDamage'> Damage to seized vehicle</label><label><input type='checkbox' name='trafficway'> Crash occurred on a trafficway or after run-off-road before stabilization</label>
            <h2>Crash</h2><div class='two'><label>Date/time<input type='datetime-local' name='crashDateTime' required></label><label>Agency case number<input name='agencyCaseNumber'></label></div><label>Location<input name='location' required></label>
            <div class='two'><label>(1) Locality<select name='locality'>{Opts(Locality)}</select></label><label>(3) Surface condition<select name='surface'>{Opts(Surface)}</select></label></div><div class='two'><label>(7) Ambient light<select name='light'>{Opts(Light)}</select></label><label>(60) Speed limit<input type='number' min='0' name='speedLimit'></label></div>
            <label>(4-5) Weather (max 2)<select multiple size='5' name='weather'>{Opts(Weather)}</select></label><div class='two'><label>(10) First harmful event<select name='firstHarmful'>{Opts(Harmful)}</select></label><label>(11) Most harmful event<select name='mostHarmful'>{Opts(Harmful)}</select></label></div>
            <h2>Vehicles / units</h2><p>Use <b>Add scanned vehicle</b> to capture a plate with the device camera, confirm the OCR result, and add it to the vehicle JSON. Manual editing remains available.</p><button type='button' id='addVehicleScan'>Add scanned vehicle</button><input id='plateCamera' type='file' accept='image/*' capture='environment' hidden><textarea id='vehiclesJson' name='vehiclesJson' rows='12'>[]</textarea>
            <h2>Drivers / occupants / non-motorists</h2><p>Use <b>Add scanned individual</b> to scan the PDF417 barcode on the back of a driver license, confirm/correct fields, then add the person.</p><button type='button' id='addPersonScan'>Add scanned individual</button><input id='licenseCamera' type='file' accept='image/*' capture='environment' hidden><textarea id='personsJson' name='personsJson' rows='12'>[]</textarea>
            <h2>(84) Diagram</h2><textarea name='diagramData' rows='5'></textarea><h2>(85) Narrative</h2><textarea name='narrative' rows='10' required></textarea>
            <button name='action' value='draft'>Save draft</button><button name='action' value='submit'>Validate & submit</button></form></section></main>
            <script>
            const plateInput=document.getElementById('plateCamera'), licInput=document.getElementById('licenseCamera');
            document.getElementById('addVehicleScan').onclick=()=>plateInput.click();
            plateInput.onchange=async()=>{const f=plateInput.files[0];if(!f)return;const fd=new FormData();fd.append('capture',f);fd.append('kind','plate');const res=await fetch('/crash-reports/scan-image',{method:'POST',body:fd});const d=await res.json();let plate=prompt('Confirm license plate',d.candidate||'')||'';let state=prompt('Plate state','NC')||'NC';if(!plate)return;const a=JSON.parse(document.getElementById('vehiclesJson').value||'[]');a.push({vehicleNumber:a.length+1,plate:plate.toUpperCase(),plateState:state.toUpperCase()});document.getElementById('vehiclesJson').value=JSON.stringify(a,null,2)};
            document.getElementById('addPersonScan').onclick=()=>licInput.click();
            licInput.onchange=async()=>{const f=licInput.files[0];if(!f)return;const fd=new FormData();fd.append('capture',f);fd.append('kind','license');const res=await fetch('/crash-reports/scan-image',{method:'POST',body:fd});const d=await res.json();const name=prompt('Confirm full name',d.name||'')||'';const dl=prompt('Confirm driver license number',d.licenseNumber||'')||'';const dob=prompt('Confirm date of birth',d.dateOfBirth||'')||'';const addr=prompt('Confirm address',d.address||'')||'';if(!name)return;const a=JSON.parse(document.getElementById('personsJson').value||'[]');a.push({personTypeCode:1,name,licenseNumber:dl,dateOfBirth:dob,address:addr});document.getElementById('personsJson').value=JSON.stringify(a,null,2)};
            </script>"""));
        }).RequireAuthorization();

        app.MapPost("/crash-reports/scan-image", async (HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var form = await ctx.Request.ReadFormAsync(); var file = form.Files["capture"];
            if (file is null || file.Length == 0 || file.Length > 20L * 1024 * 1024) return Results.BadRequest(new { error = "Image up to 20 MB required." });
            var kind = form["kind"].ToString(); var raw = await OcrAsync(app.Configuration, file);
            if (kind == "plate") return Results.Ok(new { candidate = ExtractPlate(raw) });
            var aamva = ParseAamva(raw); return Results.Ok(aamva);
        }).RequireAuthorization();

        app.MapPost("/crash-reports", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid(); await anti.ValidateRequestAsync(ctx); var f = await ctx.Request.ReadFormAsync();
            bool B(string k) => f.ContainsKey(k); decimal.TryParse(f["propertyDamage"], out var dmg);
            if (!(B("fatality") || B("injury") || dmg >= 1000 || B("seizedVehicleDamage"))) return Results.BadRequest("DMV-349 reportability criteria are not met.");
            if (!B("trafficway")) return Results.BadRequest("Crash must meet the trafficway requirement.");
            if (!JsonArray(f["vehiclesJson"], out var ve)) return Results.BadRequest("Vehicles JSON invalid: " + ve);
            if (!JsonArray(f["personsJson"], out var pe)) return Results.BadRequest("Persons JSON invalid: " + pe);
            if (f["weather"].Count > 2) return Results.BadRequest("Maximum two weather conditions.");
            var id = Guid.NewGuid(); var num = $"DMV349-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000,999999)}"; var now = DateTimeOffset.UtcNow.ToString("O"); var status = f["action"] == "submit" ? "Submitted" : "Draft";
            var payload = JsonSerializer.Serialize(f.ToDictionary(x => x.Key, x => (object)x.Value.ToArray()));
            await using var cn = Open(app.Configuration); await cn.OpenAsync(); var cmd = cn.CreateCommand(); cmd.CommandText = "INSERT INTO CrashReports349(Id,ReportNumber,AgencyCaseNumber,CrashDateTime,Location,PayloadJson,Status,CreatedByEmployee,CreatedAt,UpdatedAt,SubmittedAt) VALUES($id,$n,$c,$dt,$l,$p,$s,$o,$now,$now,$sub)";
            cmd.Parameters.AddWithValue("$id", id.ToString()); cmd.Parameters.AddWithValue("$n", num); cmd.Parameters.AddWithValue("$c", f["agencyCaseNumber"].ToString()); cmd.Parameters.AddWithValue("$dt", f["crashDateTime"].ToString()); cmd.Parameters.AddWithValue("$l", f["location"].ToString()); cmd.Parameters.AddWithValue("$p", payload); cmd.Parameters.AddWithValue("$s", status); cmd.Parameters.AddWithValue("$o", ctx.User.Identity?.Name ?? "officer"); cmd.Parameters.AddWithValue("$now", now); cmd.Parameters.AddWithValue("$sub", status == "Submitted" ? now : DBNull.Value); await cmd.ExecuteNonQueryAsync();
            return Results.Redirect($"/crash-reports/{id}");
        }).RequireAuthorization();

        app.MapGet("/crash-reports/{id:guid}", async (Guid id, HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid(); await using var cn = Open(app.Configuration); await cn.OpenAsync(); var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT ReportNumber,CrashDateTime,Location,PayloadJson,Status,CreatedByEmployee,SubmittedAt FROM CrashReports349 WHERE Id=$id"; cmd.Parameters.AddWithValue("$id", id.ToString()); await using var r = await cmd.ExecuteReaderAsync(); if (!await r.ReadAsync()) return Results.NotFound();
            return Html(Page(r.GetString(0), $"<main class='wrap'><a class='back' href='/crash-reports'>← Crash reports</a><section class='card'><span class='eyebrow'>NC DMV-349 · {H(r.GetString(4))}</span><h1>{H(r.GetString(0))}</h1><p>{H(r.GetString(2))} · {H(r.GetString(1))}</p><p>Officer: {H(r.GetString(5))}</p><pre style='white-space:pre-wrap'>{H(Pretty(r.GetString(3)))}</pre></section></main>"));
        }).RequireAuthorization();
    }

    private static readonly Dictionary<string,string> Locality=new(){{"1","Rural"},{"2","Mixed"},{"3","Urban"}};
    private static readonly Dictionary<string,string> Surface=new(){{"1","Dry"},{"2","Wet"},{"3","Water"},{"4","Ice"},{"5","Snow"},{"6","Slush"},{"7","Sand/Mud/Dirt/Gravel"},{"8","Fuel/Oil"},{"9","Other"},{"10","Unknown"}};
    private static readonly Dictionary<string,string> Weather=new(){{"1","Clear"},{"2","Cloudy"},{"3","Rain"},{"4","Snow"},{"5","Fog/smog/smoke"},{"6","Sleet/hail/freezing rain"},{"7","Severe crosswinds"},{"8","Blowing sand/dirt/snow"},{"9","Other"}};
    private static readonly Dictionary<string,string> Light=new(){{"1","Daylight"},{"2","Dusk"},{"3","Dawn"},{"4","Dark-lighted"},{"5","Dark-not lighted"},{"6","Dark-unknown"},{"7","Other"},{"8","Unknown"}};
    private static readonly Dictionary<string,string> Harmful=new(){{"0","Unknown"},{"1","Ran off road-right"},{"2","Ran off road-left"},{"3","Ran off road-straight"},{"4","Jackknife"},{"5","Overturn/rollover"},{"14","Pedestrian"},{"15","Pedalcyclist"},{"16","RR train/engine"},{"17","Animal"},{"18","Movable object"},{"19","Fixed object"},{"20","Parked motor vehicle"},{"21","Rear end"},{"27","Head on"},{"28","Sideswipe same direction"},{"29","Sideswipe opposite direction"}};

    private static string Opts(Dictionary<string,string> d)=>string.Join("",d.Select(x=>$"<option value='{H(x.Key)}'>{H(x.Key)} — {H(x.Value)}</option>"));
    private static bool JsonArray(string s,out string err){try{using var d=JsonDocument.Parse(string.IsNullOrWhiteSpace(s)?"[]":s);if(d.RootElement.ValueKind!=JsonValueKind.Array){err="must be an array";return false;}err="";return true;}catch(Exception ex){err=ex.Message;return false;}}
    private static async Task<string> OcrAsync(IConfiguration c,IFormFile f){var tess=c["CLTPP_TESSDATA_PATH"];if(string.IsNullOrWhiteSpace(tess)||!Directory.Exists(tess))return "";var tmp=Path.Combine(Path.GetTempPath(),$"cltpp-crash-{Guid.NewGuid():N}{Path.GetExtension(f.FileName)}");await using(var fs=File.Create(tmp))await f.CopyToAsync(fs);try{using var e=new Engine(tess,Language.English,EngineMode.Default);using var img=Image.LoadFromFile(tmp);using var p=e.Process(img);return p.Text??"";}finally{try{File.Delete(tmp);}catch{}}}
    private static string ExtractPlate(string raw)=>raw.ToUpperInvariant().Split(new[]{' ','\r','\n','\t',':',';','|'},StringSplitOptions.RemoveEmptyEntries).Select(Norm).Where(x=>x.Length is >=4 and <=10).OrderByDescending(x=>x.Length).FirstOrDefault()??"";
    private static object ParseAamva(string raw){string Pick(string code){foreach(var line in raw.Replace("\r","\n").Split('\n')){var i=line.IndexOf(code,StringComparison.OrdinalIgnoreCase);if(i>=0)return line[(i+3)..].Trim();}return "";}var first=Pick("DAC");var last=Pick("DCS");var street=Pick("DAG");var city=Pick("DAI");var state=Pick("DAJ");var zip=Pick("DAK");return new{name=string.Join(" ",new[]{first,last}.Where(x=>x.Length>0)),licenseNumber=Pick("DAQ"),dateOfBirth=Pick("DBB"),address=string.Join(", ",new[]{street,city,state,zip}.Where(x=>x.Length>0))};}
    private static string Norm(string? s)=>new((s??"").Trim().ToUpperInvariant().Where(c=>char.IsLetterOrDigit(c)||c=='-').ToArray());
    private static string Pretty(string j){try{return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(j),new JsonSerializerOptions{WriteIndented=true});}catch{return j;}}
    private static void EnsureSchema(IConfiguration c){using var cn=Open(c);cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="CREATE TABLE IF NOT EXISTS CrashReports349(Id TEXT PRIMARY KEY,ReportNumber TEXT NOT NULL UNIQUE,AgencyCaseNumber TEXT,CrashDateTime TEXT NOT NULL,Location TEXT NOT NULL,PayloadJson TEXT NOT NULL,Status TEXT NOT NULL,CreatedByEmployee TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,SubmittedAt TEXT);CREATE INDEX IF NOT EXISTS IX_CrashReports349_Status ON CrashReports349(Status);";cmd.ExecuteNonQuery();}
    private static SqliteConnection Open(IConfiguration c){var p=c["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");Directory.CreateDirectory(Path.GetDirectoryName(p)!);return new SqliteConnection($"Data Source={p}");}
    private static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>c.Value.Equals("Police",StringComparison.OrdinalIgnoreCase));
    private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string H(string? s)=>WebUtility.HtmlEncode(s??"");
    private static string Page(string title,string body)=>$"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(title)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Public Safety</div></header>{body}</body></html>";
}
