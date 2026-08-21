using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;

namespace CltPlusPlus.Employee;

public static class DriverVehicleVerification
{
    public static void Map(WebApplication app)
    {
        EnsureSchema(app.Configuration);

        app.MapGet("/identity-verification", (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("Driver & registration verification", $"""
            <main class='wrap'><a class='back' href='/'>← Work queue</a>
            <section class='page-head'><div><span class='eyebrow'>POLICE · DMV VERIFICATION</span><h1>Driver & registration verification</h1><p>Verify a presented driver license and, when needed, a registration document against an authorized DMV provider.</p></div></section>
            <section class='card'><div class='notice'><b>Authorized access only</b><p>This workflow requires a configured law-enforcement DMV provider and records the officer, time, purpose, identifiers queried, and verification result.</p></div>
            <form method='post' action='/identity-verification/owner'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/>
              <label>Lookup by<select name='identifierType'><option value='plate'>License plate</option><option value='vin'>VIN</option></select></label>
              <label>Plate / VIN<input name='identifier' required autocomplete='off'/></label><label>Plate state<input name='plateState' value='NC' maxlength='3'/></label>
              <label>Law-enforcement purpose<textarea name='purpose' rows='3' required placeholder='Traffic stop, crash investigation, stolen vehicle investigation, etc.'></textarea></label>
              <button type='submit'>Retrieve authorized owner record</button>
            </form></section></main>"""));
        }).RequireAuthorization();

        app.MapPost("/identity-verification/owner", async (HttpContext ctx, IAntiforgery anti, IHttpClientFactory factory) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var form = await ctx.Request.ReadFormAsync();
            var kind = form["identifierType"].ToString()=="vin" ? "vin" : "plate";
            var id = Normalize(form["identifier"].ToString());
            var state = Normalize(form["plateState"].ToString());
            var purpose = form["purpose"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(purpose)) return Results.BadRequest("Identifier and purpose are required.");
            var provider = app.Configuration["CLTPP_AUTHORIZED_DMV_VERIFY_URL"];
            if (string.IsNullOrWhiteSpace(provider)) return Results.BadRequest("Authorized DMV verification provider is not configured.");

            var client = factory.CreateClient();
            var url = provider.TrimEnd('/') + $"/vehicle-owner?identifierType={Uri.EscapeDataString(kind)}&identifier={Uri.EscapeDataString(id)}&plateState={Uri.EscapeDataString(state)}&purpose={Uri.EscapeDataString(purpose)}";
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return Results.BadRequest("Authorized DMV provider did not return a usable owner record.");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var ownerName = Get(root,"ownerName","name") ?? "";
            var ownerAddress = Get(root,"ownerAddress","address") ?? "";
            var vin = Get(root,"vin") ?? (kind=="vin"?id:"");
            var plate = Get(root,"plate","licensePlate") ?? (kind=="plate"?id:"");
            var session = Guid.NewGuid();
            SaveSession(app.Configuration, session, ctx.User.Identity?.Name??"officer", purpose, kind, id, state, ownerName, ownerAddress, vin, plate);

            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("Scan driver license", $"""
            <main class='wrap'><a class='back' href='/identity-verification'>← Start over</a><section class='page-head'><div><span class='eyebrow'>STEP 2 · DRIVER LICENSE</span><h1>Scan the back of the driver license</h1><p>Vehicle owner record: <b>{H(ownerName)}</b> · {H(ownerAddress)}</p></div></section>
            <section class='card'><div class='notice'><b>Scan is visible and officer-initiated</b><p>The browser attempts to read the PDF417 barcode. Confirm or correct the fields before verification.</p></div>
              <div id='scanArea'><label>Camera<input id='dlCapture' type='file' accept='image/*' capture='environment'/></label><button type='button' id='scanBtn'>Read license barcode</button><p id='scanStatus' class='muted'></p></div>
              <form method='post' action='/identity-verification/license' id='dlForm'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><input type='hidden' name='sessionId' value='{session}'/>
                <label>Driver license number<input name='licenseNumber' id='licenseNumber' required/></label>
                <label>First name<input name='firstName' id='firstName' required/></label><label>Last name<input name='lastName' id='lastName' required/></label>
                <label>Date of birth<input name='dateOfBirth' id='dateOfBirth' placeholder='YYYYMMDD' required/></label>
                <label>Address<input name='address' id='address' required/></label>
                <button type='submit'>Verify driver against owner record</button>
              </form>
            </section></main>
            <script>
            const status=document.getElementById('scanStatus');
            document.getElementById('scanBtn').onclick=async()=>{{
              const f=document.getElementById('dlCapture').files[0]; if(!f){{status.textContent='Capture the back of the license first.';return;}}
              if(!('BarcodeDetector' in window)){{status.textContent='PDF417 scanning is not supported in this browser. Enter the fields manually.';return;}}
              try{{const detector=new BarcodeDetector({{formats:['pdf417']}});const bmp=await createImageBitmap(f);const codes=await detector.detect(bmp);if(!codes.length)throw new Error('No PDF417 barcode found');parseAamva(codes[0].rawValue||'');status.textContent='Barcode read. Confirm the fields below.';}}catch(e){{status.textContent='Could not read the barcode. Enter or correct the fields manually.';}}
            }};
            function pick(raw,key){{const m=raw.match(new RegExp('(?:^|\\n|\\r)'+key+'([^\\r\\n]+)'));return m?m[1].trim():'';}}
            function parseAamva(raw){{document.getElementById('licenseNumber').value=pick(raw,'DAQ');document.getElementById('lastName').value=pick(raw,'DCS');document.getElementById('firstName').value=pick(raw,'DAC')||pick(raw,'DCT');document.getElementById('dateOfBirth').value=pick(raw,'DBB');const street=pick(raw,'DAG'),city=pick(raw,'DAI'),state=pick(raw,'DAJ'),zip=pick(raw,'DAK');document.getElementById('address').value=[street,city,state,zip].filter(Boolean).join(', ');}}
            </script>"""));
        }).RequireAuthorization();

        app.MapPost("/identity-verification/license", async (HttpContext ctx, IAntiforgery anti, IHttpClientFactory factory) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var form=await ctx.Request.ReadFormAsync();
            if(!Guid.TryParse(form["sessionId"],out var sessionId)) return Results.BadRequest("Invalid verification session.");
            var session=LoadSession(app.Configuration,sessionId,ctx.User.Identity?.Name??""); if(session is null)return Results.NotFound();
            var dl=Normalize(form["licenseNumber"].ToString());var first=form["firstName"].ToString().Trim();var last=form["lastName"].ToString().Trim();var dob=form["dateOfBirth"].ToString().Trim();var address=form["address"].ToString().Trim();
            var provider=app.Configuration["CLTPP_AUTHORIZED_DMV_VERIFY_URL"];if(string.IsNullOrWhiteSpace(provider))return Results.BadRequest("Authorized DMV verification provider is not configured.");
            var client=factory.CreateClient();var url=provider.TrimEnd('/')+$"/driver?licenseNumber={Uri.EscapeDataString(dl)}&purpose={Uri.EscapeDataString(session.Purpose)}";using var response=await client.GetAsync(url);
            if(!response.IsSuccessStatusCode)return Results.BadRequest("Authorized DMV provider did not return a usable driver record.");
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var r=doc.RootElement;
            var verifiedName=JoinName(Get(r,"firstName"),Get(r,"lastName"));var verifiedAddress=Get(r,"address","mailingAddress")??"";var verifiedDob=Get(r,"dateOfBirth","dob")??"";
            var presentedName=JoinName(first,last);var nameMatch=Loose(presentedName,session.OwnerName)&&Loose(verifiedName,session.OwnerName);var addressMatch=LooseAddress(address,session.OwnerAddress)&&LooseAddress(verifiedAddress,session.OwnerAddress);var overall=nameMatch&&addressMatch;
            UpdateLicenseResult(app.Configuration,sessionId,dl,presentedName,address,dob,verifiedName,verifiedAddress,verifiedDob,overall);
            var warrantMatches=SearchActiveWarrants(app.Configuration,sessionId,ctx.User.Identity?.Name??"officer",verifiedName,dob,dl);
            var token=anti.GetAndStoreTokens(ctx).RequestToken!;
            var next=overall?$"<div class='success'><b>Driver matches the registered owner record.</b><p>No registration document is required by this workflow.</p></div>":$"""<div class='error'><b>Driver and registered owner do not match.</b><p>Scan the registration document, or record that it was not furnished.</p></div><form method='post' enctype='multipart/form-data' action='/identity-verification/registration'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><input type='hidden' name='sessionId' value='{sessionId}'/><label>North Carolina registration document<input type='file' name='registration' accept='image/*' capture='environment'/></label><button name='action' value='scan'>Scan & verify registration</button><button name='action' value='not-furnished' class='secondary'>Registration not furnished</button></form>""";
            return Html(Page("Verification result",$"""<main class='wrap'><a class='back' href='/identity-verification'>← New verification</a><section class='page-head'><div><span class='eyebrow'>IDENTITY COMPARISON</span><h1>{(overall?"Match":"Mismatch")}</h1></div></section><section class='card'><dl><dt>Registered owner</dt><dd>{H(session.OwnerName)} · {H(session.OwnerAddress)}</dd><dt>Presented driver</dt><dd>{H(presentedName)} · {H(address)}</dd><dt>DMV driver record</dt><dd>{H(verifiedName)} · {H(verifiedAddress)}</dd></dl>{next}</section>{warrantMatches}</main>"""));
        }).RequireAuthorization();

        app.MapPost("/identity-verification/registration", async (HttpContext ctx, IAntiforgery anti, IHttpClientFactory factory) =>
        {
            if(!CanPolice(ctx.User))return Results.Forbid();await anti.ValidateRequestAsync(ctx);var form=await ctx.Request.ReadFormAsync();if(!Guid.TryParse(form["sessionId"],out var sid))return Results.BadRequest("Invalid session.");var session=LoadSession(app.Configuration,sid,ctx.User.Identity?.Name??"");if(session is null)return Results.NotFound();
            if(form["action"]=="not-furnished"){UpdateRegistration(app.Configuration,sid,"Not furnished",null,null,false);return Html(Page("Registration not furnished",$"<main class='wrap'><section class='card'><h1>Registration not furnished</h1><p>This disposition has been recorded in the verification audit.</p><a class='button' href='/identity-verification'>Done</a></section></main>"));}
            var file=form.Files["registration"];if(file is null||file.Length==0||file.Length>20L*1024*1024)return Results.BadRequest("Registration image up to 20 MB is required.");
            var text=await OcrAsync(app.Configuration,file);var plate=ExtractAfter(text,"PLATE","LICENSE PLATE")??session.Plate;var vin=ExtractVin(text)??session.Vin;var provider=app.Configuration["CLTPP_AUTHORIZED_DMV_VERIFY_URL"];if(string.IsNullOrWhiteSpace(provider))return Results.BadRequest("Authorized DMV verification provider is not configured.");
            var client=factory.CreateClient();var url=provider.TrimEnd('/')+$"/registration?vin={Uri.EscapeDataString(vin??"")}&plate={Uri.EscapeDataString(plate??"")}&plateState={Uri.EscapeDataString(session.PlateState)}&purpose={Uri.EscapeDataString(session.Purpose)}";using var response=await client.GetAsync(url);if(!response.IsSuccessStatusCode)return Results.BadRequest("Authorized DMV provider did not return a usable registration record.");using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var r=doc.RootElement;var regOwner=Get(r,"ownerName","registeredOwner")??"";var regAddress=Get(r,"ownerAddress","address")??"";var valid=Loose(regOwner,session.OwnerName)&&LooseAddress(regAddress,session.OwnerAddress);UpdateRegistration(app.Configuration,sid,"Scanned",regOwner,regAddress,valid);
            return Html(Page("Registration verification",$"<main class='wrap'><a class='back' href='/identity-verification'>← New verification</a><section class='card'><span class='eyebrow'>REGISTRATION</span><h1>{(valid?"Registration verified":"Registration did not verify")}</h1><p>DMV registration owner: <b>{H(regOwner)}</b><br>{H(regAddress)}</p><p>OCR plate: {H(plate)} · VIN: {H(vin)}</p></section></main>"));
        }).RequireAuthorization();
    }

    private sealed record Session(Guid Id,string Officer,string Purpose,string IdentifierType,string Identifier,string PlateState,string OwnerName,string OwnerAddress,string? Vin,string? Plate);
    private static string Db(IConfiguration c)=>c["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");
    private static void EnsureSchema(IConfiguration c){Directory.CreateDirectory(Path.GetDirectoryName(Db(c))!);using var cn=new SqliteConnection($"Data Source={Db(c)}");cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="""CREATE TABLE IF NOT EXISTS DmvVerificationAudit (Id TEXT PRIMARY KEY,Officer TEXT NOT NULL,Purpose TEXT NOT NULL,IdentifierType TEXT NOT NULL,Identifier TEXT NOT NULL,PlateState TEXT,OwnerName TEXT,OwnerAddress TEXT,Vin TEXT,Plate TEXT,LicenseNumber TEXT,PresentedDriverName TEXT,PresentedDriverAddress TEXT,PresentedDob TEXT,DmvDriverName TEXT,DmvDriverAddress TEXT,DmvDriverDob TEXT,DriverOwnerMatch INTEGER,RegistrationDisposition TEXT,RegistrationOwnerName TEXT,RegistrationOwnerAddress TEXT,RegistrationVerified INTEGER,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);CREATE TABLE IF NOT EXISTS WarrantLookupAudit(Id TEXT PRIMARY KEY,VerificationSessionId TEXT NOT NULL,Officer TEXT NOT NULL,NameQueried TEXT,DateOfBirthQueried TEXT,LicenseIdQueried TEXT,MatchCount INTEGER NOT NULL,SearchedAt TEXT NOT NULL);""";cmd.ExecuteNonQuery();}
    private static void SaveSession(IConfiguration c,Guid id,string officer,string purpose,string kind,string ident,string state,string owner,string addr,string? vin,string? plate){using var cn=new SqliteConnection($"Data Source={Db(c)}");cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="INSERT INTO DmvVerificationAudit(Id,Officer,Purpose,IdentifierType,Identifier,PlateState,OwnerName,OwnerAddress,Vin,Plate,CreatedAt,UpdatedAt) VALUES($id,$o,$p,$k,$i,$s,$n,$a,$v,$pl,$t,$t)";cmd.Parameters.AddWithValue("$id",id.ToString());cmd.Parameters.AddWithValue("$o",officer);cmd.Parameters.AddWithValue("$p",purpose);cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",ident);cmd.Parameters.AddWithValue("$s",state);cmd.Parameters.AddWithValue("$n",owner);cmd.Parameters.AddWithValue("$a",addr);cmd.Parameters.AddWithValue("$v",vin??"");cmd.Parameters.AddWithValue("$pl",plate??"");cmd.Parameters.AddWithValue("$t",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();}
    private static Session? LoadSession(IConfiguration c,Guid id,string officer){using var cn=new SqliteConnection($"Data Source={Db(c)}");cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="SELECT Officer,Purpose,IdentifierType,Identifier,PlateState,OwnerName,OwnerAddress,Vin,Plate FROM DmvVerificationAudit WHERE Id=$id AND Officer=$o";cmd.Parameters.AddWithValue("$id",id.ToString());cmd.Parameters.AddWithValue("$o",officer);using var r=cmd.ExecuteReader();return r.Read()?new(id,r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?"":r.GetString(4),r.IsDBNull(5)?"":r.GetString(5),r.IsDBNull(6)?"":r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8)):null;}
    private static void UpdateLicenseResult(IConfiguration c,Guid id,string dl,string pn,string pa,string dob,string dn,string da,string dd,bool match){using var cn=new SqliteConnection($"Data Source={Db(c)}");cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="UPDATE DmvVerificationAudit SET LicenseNumber=$dl,PresentedDriverName=$pn,PresentedDriverAddress=$pa,PresentedDob=$dob,DmvDriverName=$dn,DmvDriverAddress=$da,DmvDriverDob=$dd,DriverOwnerMatch=$m,UpdatedAt=$t WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());cmd.Parameters.AddWithValue("$dl",dl);cmd.Parameters.AddWithValue("$pn",pn);cmd.Parameters.AddWithValue("$pa",pa);cmd.Parameters.AddWithValue("$dob",dob);cmd.Parameters.AddWithValue("$dn",dn);cmd.Parameters.AddWithValue("$da",da);cmd.Parameters.AddWithValue("$dd",dd);cmd.Parameters.AddWithValue("$m",match?1:0);cmd.Parameters.AddWithValue("$t",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();}
    private static string SearchActiveWarrants(IConfiguration c,Guid sessionId,string officer,string name,string dob,string dl){using var cn=new SqliteConnection($"Data Source={Db(c)}");cn.Open();var matches=new List<(string Id,string Number,string Subject,string? Dob,string? License,string Incident,string Charges)>();try{using var cmd=cn.CreateCommand();cmd.CommandText="""SELECT Id,WarrantNumber,SubjectName,DateOfBirth,LicenseId,IncidentNumber,Charges FROM ArrestWarrants WHERE Status='Active' AND ((LicenseId<>'' AND upper(LicenseId)=upper($dl)) OR (upper(SubjectName)=upper($name) AND ($dob='' OR DateOfBirth=$dob))) ORDER BY IssuedAt DESC LIMIT 25""";cmd.Parameters.AddWithValue("$dl",dl);cmd.Parameters.AddWithValue("$name",name);cmd.Parameters.AddWithValue("$dob",dob);using var r=cmd.ExecuteReader();while(r.Read())matches.Add((r.GetString(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.GetString(5),r.GetString(6)));}catch(SqliteException){ }
        using(var audit=cn.CreateCommand()){audit.CommandText="INSERT INTO WarrantLookupAudit(Id,VerificationSessionId,Officer,NameQueried,DateOfBirthQueried,LicenseIdQueried,MatchCount,SearchedAt) VALUES($id,$s,$o,$n,$d,$l,$c,$t)";audit.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());audit.Parameters.AddWithValue("$s",sessionId.ToString());audit.Parameters.AddWithValue("$o",officer);audit.Parameters.AddWithValue("$n",name);audit.Parameters.AddWithValue("$d",dob);audit.Parameters.AddWithValue("$l",dl);audit.Parameters.AddWithValue("$c",matches.Count);audit.Parameters.AddWithValue("$t",DateTimeOffset.UtcNow.ToString("O"));audit.ExecuteNonQuery();}
        if(matches.Count==0)return "<section class='card'><h2>Warrant check</h2><div class='success'><b>No active warrant matches returned.</b><p>The warrant database was searched automatically using the verified ID fields. Continue to verify status through required agency systems.</p></div></section>";
        var rows=string.Join("",matches.Select(x=>$"<tr><td><a href='/warrants/{H(x.Id)}'><b>{H(x.Number)}</b></a></td><td>{H(x.Subject)}<small>{H(x.Dob)}</small></td><td>{H(x.License)}</td><td>{H(x.Incident)}</td><td>{H(x.Charges)}</td><td><a class='button' href='/warrants/{H(x.Id)}'>Verify & serve warrant</a></td></tr>"));return $"<section class='card'><h2>Potential active warrant matches</h2><div class='error'><b>{matches.Count} potential match(es) found.</b><p>These are search candidates, not automatic identity determinations. Open the warrant and independently verify the subject and current active status before service.</p></div><div class='table-card'><table><thead><tr><th>Warrant</th><th>Subject</th><th>License / ID</th><th>Incident</th><th>Charges</th><th></th></tr></thead><tbody>{rows}</tbody></table></div></section>";}
    private static void UpdateRegistration(IConfiguration c,Guid id,string disposition,string? owner,string? address,bool verified){using var cn=new SqliteConnection($"Data Source={Db(c)}");cn.Open();using var cmd=cn.CreateCommand();cmd.CommandText="UPDATE DmvVerificationAudit SET RegistrationDisposition=$d,RegistrationOwnerName=$o,RegistrationOwnerAddress=$a,RegistrationVerified=$v,UpdatedAt=$t WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString());cmd.Parameters.AddWithValue("$d",disposition);cmd.Parameters.AddWithValue("$o",owner??"");cmd.Parameters.AddWithValue("$a",address??"");cmd.Parameters.AddWithValue("$v",verified?1:0);cmd.Parameters.AddWithValue("$t",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();}
    private static async Task<string> OcrAsync(IConfiguration c,IFormFile file){var temp=Path.Combine(Path.GetTempPath(),$"cltpp-reg-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");await using(var fs=File.Create(temp))await file.CopyToAsync(fs);try{var tess=c["CLTPP_TESSDATA_PATH"];if(string.IsNullOrWhiteSpace(tess)||!Directory.Exists(tess))return "";using var engine=new Engine(tess,Language.English,EngineMode.Default);using var image=Image.LoadFromFile(temp);using var page=engine.Process(image);return page.Text??"";}finally{try{File.Delete(temp);}catch{}}}
    private static string? ExtractVin(string t)=>t.ToUpperInvariant().Split(new[]{' ','\r','\n','\t',':',';','|'},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).FirstOrDefault(x=>x.Length==17&&!x.Any(c=>"IOQ".Contains(c)));
    private static string? ExtractAfter(string text,params string[] keys){foreach(var line in text.Split('\n'))foreach(var k in keys)if(line.Contains(k,StringComparison.OrdinalIgnoreCase)){var parts=line.Split(new[]{':',' '},StringSplitOptions.RemoveEmptyEntries);var v=parts.Select(Normalize).Where(x=>x.Length>=4).LastOrDefault();if(!string.IsNullOrWhiteSpace(v))return v;}return null;}
    private static string? Get(JsonElement e,params string[] names){foreach(var n in names)if(e.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null)return v.ToString();return null;}
    private static string Normalize(string? s)=>new((s??"").Trim().ToUpperInvariant().Where(c=>char.IsLetterOrDigit(c)||c=='-').ToArray());
    private static string JoinName(string? a,string? b)=>string.Join(" ",new[]{a,b}.Where(x=>!string.IsNullOrWhiteSpace(x))).Trim();
    private static bool Loose(string? a,string? b)=>Normalize(a)==Normalize(b)&&!string.IsNullOrWhiteSpace(Normalize(a));
    private static bool LooseAddress(string? a,string? b){static string N(string? s)=>new((s??"").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());var x=N(a);var y=N(b);return x.Length>5&&y.Length>5&&(x==y||x.Contains(y)||y.Contains(x));}
    private static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>c.Value.Equals("Police",StringComparison.OrdinalIgnoreCase));
    private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string H(string? s)=>WebUtility.HtmlEncode(s??"");
    private static string Page(string title,string body)=>$"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(title)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Public Safety</div></header>{body}</body></html>";
}
