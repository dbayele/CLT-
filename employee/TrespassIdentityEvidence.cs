using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class TrespassIdentityEvidence
{
    public static void Map(WebApplication app)
    {
        EnsureSchema(app.Configuration);

        app.MapGet("/trespass-identity", async (HttpContext ctx) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await using var cn = Open(app.Configuration); await cn.OpenAsync();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT Id,RecordNumber,PropertyName,OffenderName,CompletedAt FROM TrespassRecords ORDER BY CompletedAt DESC LIMIT 100";
            var rows = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add($"<tr><td><a href='/trespass-identity/{H(r.GetString(0))}'><b>{H(r.GetString(1))}</b></a></td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(3))}</td><td>{H(r.GetString(4))}</td></tr>");
            return Html(Page("Trespass identity & photo", $"""
            <main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · TRESPASS</span><h1>Identity scans & offender photos</h1><p>Attach visible driver-license scans and an officer-captured offender photograph to a completed trespass record.</p></div></section>
            <section class='table-card'><table><thead><tr><th>Record</th><th>Property</th><th>Trespasser</th><th>Completed</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='4' class='empty'>No completed trespass records found.</td></tr>")}</tbody></table></section></main>"""));
        }).RequireAuthorization();

        app.MapGet("/trespass-identity/{id:guid}", async (Guid id, HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var rec = await LoadRecord(app.Configuration,id); if (rec is null) return Results.NotFound();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            var existing = await Existing(app.Configuration,id);
            return Html(Page(rec.Value.number, $"""
            <main class='wrap'><a class='back' href='/trespass-identity'>← Trespass identity records</a>
            <section class='page-head'><div><span class='eyebrow'>TRESPASS · {H(rec.Value.number)}</span><h1>{H(rec.Value.property)}</h1><p>Trespasser: {H(rec.Value.offender)}</p></div></section>
            <section class='card'><div class='notice'><b>Officer-initiated capture only</b><p>License scanning and photography are visible actions. No facial recognition or background camera capture is performed.</p></div>{existing}
            <form method='post' action='/trespass-identity/{id}/licenses'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/>
              <h2>Property owner / authorized agent license</h2>
              <label>Capture back of license<input id='ownerCapture' type='file' accept='image/*' capture='environment'></label><button type='button' id='ownerScan'>Read owner PDF417</button><p id='ownerStatus' class='muted'></p>
              <div class='two'><label>License number<input id='ownerDl' name='ownerLicenseNumber'></label><label>State<input id='ownerState' name='ownerLicenseState' value='NC'></label></div>
              <div class='two'><label>First name<input id='ownerFirst' name='ownerFirstName'></label><label>Last name<input id='ownerLast' name='ownerLastName'></label></div>
              <label>Date of birth<input id='ownerDob' name='ownerDob'></label><label>Address<input id='ownerAddress' name='ownerAddress'></label>
              <h2>Trespasser license</h2>
              <label>Capture back of license<input id='offenderCapture' type='file' accept='image/*' capture='environment'></label><button type='button' id='offenderScan'>Read trespasser PDF417</button><p id='offenderStatus' class='muted'></p>
              <div class='two'><label>License number<input id='offenderDl' name='offenderLicenseNumber'></label><label>State<input id='offenderState' name='offenderLicenseState' value='NC'></label></div>
              <div class='two'><label>First name<input id='offenderFirst' name='offenderFirstName'></label><label>Last name<input id='offenderLast' name='offenderLastName'></label></div>
              <label>Date of birth<input id='offenderDob' name='offenderDob'></label><label>Address<input id='offenderAddress' name='offenderAddress'></label>
              <button>Save license scan details</button>
            </form></section>
            <section class='card'><h2>Offender photograph</h2><p>Take a photograph for this trespass record. The image is stored as protected public-safety evidence with a SHA-256 hash and audit metadata.</p>
              <form method='post' enctype='multipart/form-data' action='/trespass-identity/{id}/photo'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Camera<input type='file' name='photo' accept='image/*' capture='environment' required></label><label>Photo note<input name='note' placeholder='Optional context'></label><button>Attach offender photo</button></form>
            </section></main>
            <script>
            function pick(raw,key){const m=raw.match(new RegExp('(?:^|\\n|\\r)'+key+'([^\\r\\n]+)'));return m?m[1].trim():'';}
            async function scan(prefix){const input=document.getElementById(prefix+'Capture'),status=document.getElementById(prefix+'Status'),f=input.files[0];if(!f){status.textContent='Capture the back of the license first.';return;}if(!('BarcodeDetector' in window)){status.textContent='PDF417 scanning is not supported in this browser. Enter the fields manually.';return;}try{const d=new BarcodeDetector({formats:['pdf417']});const bmp=await createImageBitmap(f);const codes=await d.detect(bmp);if(!codes.length)throw new Error();const raw=codes[0].rawValue||'';document.getElementById(prefix+'Dl').value=pick(raw,'DAQ');document.getElementById(prefix+'State').value=pick(raw,'DAJ')||'NC';document.getElementById(prefix+'Last').value=pick(raw,'DCS');document.getElementById(prefix+'First').value=pick(raw,'DAC')||pick(raw,'DCT');document.getElementById(prefix+'Dob').value=pick(raw,'DBB');const a=[pick(raw,'DAG'),pick(raw,'DAI'),pick(raw,'DAJ'),pick(raw,'DAK')].filter(Boolean).join(', ');document.getElementById(prefix+'Address').value=a;status.textContent='Barcode read. Confirm or correct the fields before saving.';}catch{status.textContent='Could not read the barcode. Enter or correct the fields manually.';}}
            document.getElementById('ownerScan').onclick=()=>scan('owner');document.getElementById('offenderScan').onclick=()=>scan('offender');
            </script>"""));
        }).RequireAuthorization();

        app.MapPost("/trespass-identity/{id:guid}/licenses", async (Guid id,HttpContext ctx,IAntiforgery anti) =>
        {
            if(!CanPolice(ctx.User))return Results.Forbid(); await anti.ValidateRequestAsync(ctx); if(await LoadRecord(app.Configuration,id) is null)return Results.NotFound();
            var f=await ctx.Request.ReadFormAsync(); await using var cn=Open(app.Configuration); await cn.OpenAsync();
            var cmd=cn.CreateCommand();cmd.CommandText="""INSERT INTO TrespassIdentityScans(Id,TrespassRecordId,OwnerLicenseNumber,OwnerLicenseState,OwnerFirstName,OwnerLastName,OwnerDob,OwnerAddress,OffenderLicenseNumber,OffenderLicenseState,OffenderFirstName,OffenderLastName,OffenderDob,OffenderAddress,CapturedByEmployee,CapturedAt) VALUES($id,$rid,$odl,$ost,$ofn,$oln,$odob,$oa,$fdl,$fst,$ffn,$fln,$fdob,$fa,$officer,$at)""";
            void P(string k,string v)=>cmd.Parameters.AddWithValue(k,v);P("$id",Guid.NewGuid().ToString());P("$rid",id.ToString());P("$odl",f["ownerLicenseNumber"].ToString().Trim());P("$ost",f["ownerLicenseState"].ToString().Trim());P("$ofn",f["ownerFirstName"].ToString().Trim());P("$oln",f["ownerLastName"].ToString().Trim());P("$odob",f["ownerDob"].ToString().Trim());P("$oa",f["ownerAddress"].ToString().Trim());P("$fdl",f["offenderLicenseNumber"].ToString().Trim());P("$fst",f["offenderLicenseState"].ToString().Trim());P("$ffn",f["offenderFirstName"].ToString().Trim());P("$fln",f["offenderLastName"].ToString().Trim());P("$fdob",f["offenderDob"].ToString().Trim());P("$fa",f["offenderAddress"].ToString().Trim());P("$officer",ctx.User.Identity?.Name??"officer");P("$at",DateTimeOffset.UtcNow.ToString("O"));await cmd.ExecuteNonQueryAsync();return Results.Redirect($"/trespass-identity/{id}");
        }).RequireAuthorization();

        app.MapPost("/trespass-identity/{id:guid}/photo", async (Guid id,HttpContext ctx,IAntiforgery anti) =>
        {
            if(!CanPolice(ctx.User))return Results.Forbid();await anti.ValidateRequestAsync(ctx);if(await LoadRecord(app.Configuration,id) is null)return Results.NotFound();var f=await ctx.Request.ReadFormAsync();var file=f.Files["photo"];if(file is null||file.Length==0||file.Length>25L*1024*1024)return Results.BadRequest("Photo up to 25 MB is required.");if(!file.ContentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase))return Results.BadRequest("Only image files are accepted.");
            var root=app.Configuration["CLTPP_PUBLIC_SAFETY_EVIDENCE_PATH"]??Path.Combine(AppContext.BaseDirectory,"data","evidence");var dir=Path.Combine(root,"trespass",id.ToString("N"));Directory.CreateDirectory(dir);var ext=Path.GetExtension(file.FileName);var evid=Guid.NewGuid();var path=Path.Combine(dir,evid.ToString("N")+ext);await using(var fs=File.Create(path))await file.CopyToAsync(fs);string hash;await using(var fs=File.OpenRead(path)){hash=Convert.ToHexString(await SHA256.HashDataAsync(fs));}
            await using var cn=Open(app.Configuration);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="INSERT INTO TrespassOffenderPhotos(Id,TrespassRecordId,FileName,ContentType,StoragePath,Sha256,SizeBytes,Note,CapturedByEmployee,CapturedAt) VALUES($id,$rid,$name,$type,$path,$hash,$size,$note,$officer,$at)";cmd.Parameters.AddWithValue("$id",evid.ToString());cmd.Parameters.AddWithValue("$rid",id.ToString());cmd.Parameters.AddWithValue("$name",Path.GetFileName(file.FileName));cmd.Parameters.AddWithValue("$type",file.ContentType);cmd.Parameters.AddWithValue("$path",path);cmd.Parameters.AddWithValue("$hash",hash);cmd.Parameters.AddWithValue("$size",file.Length);cmd.Parameters.AddWithValue("$note",f["note"].ToString().Trim());cmd.Parameters.AddWithValue("$officer",ctx.User.Identity?.Name??"officer");cmd.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));await cmd.ExecuteNonQueryAsync();return Results.Redirect($"/trespass-identity/{id}");
        }).RequireAuthorization();
    }

    private static async Task<(string number,string property,string offender)?> LoadRecord(IConfiguration c,Guid id){await using var cn=Open(c);await cn.OpenAsync();var cmd=cn.CreateCommand();cmd.CommandText="SELECT RecordNumber,PropertyName,OffenderName FROM TrespassRecords WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());await using var r=await cmd.ExecuteReaderAsync();return await r.ReadAsync()?(r.GetString(0),r.GetString(1),r.GetString(2)):null;}
    private static async Task<string> Existing(IConfiguration c,Guid id){await using var cn=Open(c);await cn.OpenAsync();var scans=cn.CreateCommand();scans.CommandText="SELECT COUNT(1) FROM TrespassIdentityScans WHERE TrespassRecordId=$id";scans.Parameters.AddWithValue("$id",id.ToString());var sc=Convert.ToInt32(await scans.ExecuteScalarAsync());var photos=cn.CreateCommand();photos.CommandText="SELECT COUNT(1) FROM TrespassOffenderPhotos WHERE TrespassRecordId=$id";photos.Parameters.AddWithValue("$id",id.ToString());var pc=Convert.ToInt32(await photos.ExecuteScalarAsync());return $"<div class='access-strip'><b>{sc}</b><span>license scan set(s)</span><b>{pc}</b><span>offender photo(s)</span></div>";}
    private static void EnsureSchema(IConfiguration c){using var cn=Open(c);cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS TrespassIdentityScans(Id TEXT PRIMARY KEY,TrespassRecordId TEXT NOT NULL,OwnerLicenseNumber TEXT,OwnerLicenseState TEXT,OwnerFirstName TEXT,OwnerLastName TEXT,OwnerDob TEXT,OwnerAddress TEXT,OffenderLicenseNumber TEXT,OffenderLicenseState TEXT,OffenderFirstName TEXT,OffenderLastName TEXT,OffenderDob TEXT,OffenderAddress TEXT,CapturedByEmployee TEXT NOT NULL,CapturedAt TEXT NOT NULL);CREATE TABLE IF NOT EXISTS TrespassOffenderPhotos(Id TEXT PRIMARY KEY,TrespassRecordId TEXT NOT NULL,FileName TEXT NOT NULL,ContentType TEXT NOT NULL,StoragePath TEXT NOT NULL,Sha256 TEXT NOT NULL,SizeBytes INTEGER NOT NULL,Note TEXT,CapturedByEmployee TEXT NOT NULL,CapturedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_TrespassIdentityScans_Record ON TrespassIdentityScans(TrespassRecordId);CREATE INDEX IF NOT EXISTS IX_TrespassOffenderPhotos_Record ON TrespassOffenderPhotos(TrespassRecordId);""";cmd.ExecuteNonQuery();}
    private static SqliteConnection Open(IConfiguration c){var p=c["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");Directory.CreateDirectory(Path.GetDirectoryName(p)!);return new SqliteConnection($"Data Source={p}");}
    private static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>c.Value.Equals("Police",StringComparison.OrdinalIgnoreCase));private static string H(string? v)=>WebUtility.HtmlEncode(v??"");private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string Page(string title,string body)=>$"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(title)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Public Safety</div></header>{body}</body></html>";
}
