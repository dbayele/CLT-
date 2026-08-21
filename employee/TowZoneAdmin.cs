using System.Net;
using System.Security.Claims;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class TowZoneAdmin
{
    public static void Map(WebApplication app)
    {
        IncidentNarrativeAdmin.Map(app);
        app.MapGet("/tow-zones", async (HttpContext ctx) =>
        {
            if (!CanManageTowZones(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            await using var cn = Open(app.Configuration);
            await cn.OpenAsync();
            var zones = new List<ZoneRow>();
            var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT z.Id,z.Name,z.Description,z.BoundaryGeoJson,z.DefaultTowCompanyId,z.Active,
                       w.CompanyName,w.ContactName,w.ContactEmail,w.ContactPhone
                FROM TowZones z
                LEFT JOIN ZoneWreckerCompanies w ON w.Id=z.DefaultTowCompanyId
                ORDER BY z.Name
                """;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                zones.Add(new(r.GetString(0),r.GetString(1),Nullable(r,2),Nullable(r,3),r.GetString(4),r.GetInt64(5)==1,Nullable(r,6),Nullable(r,7),Nullable(r,8),Nullable(r,9)));

            var wreckers = await LoadWreckersAsync(cn);
            var rows = string.Join("", zones.Select(z => $"<tr><td><b>{H(z.Name)}</b><small>{H(z.Description)}</small></td><td>{H(z.CompanyName ?? "Unassigned")}</td><td>{H(z.ContactName)}<small>{H(z.ContactEmail)} · {H(z.ContactPhone)}</small></td><td>{(z.Active?"Active":"Inactive")}</td></tr>"));
            var options = string.Join("", wreckers.Where(x=>x.Active).Select(w=>$"<option value='{H(w.Id)}'>{H(w.CompanyName)} — {H(w.ContactName)}</option>"));
            var html = $"""
            <!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Tow zones · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head>
            <body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Police tow administration</div></header>
            <main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · TOW ADMIN</span><h1>Zone wreckers</h1><p>Configure police tow zones and each zone's default contract towing company.</p></div></section>
            <div class='detail-grid'><section class='card'><h2>Tow zones</h2><div class='table-card'><table><thead><tr><th>Zone</th><th>Default wrecker</th><th>Contact</th><th>Status</th></tr></thead><tbody>{(rows.Length>0?rows:"<tr><td colspan='4' class='empty'>No tow zones configured.</td></tr>")}</tbody></table></div></section>
            <aside><section class='card'><h2>Add zone wrecker</h2><form method='post' action='/tow-zones/wreckers'><label>Company name<input name='companyName' required></label><label>Contact name<input name='contactName' required></label><label>Email<input name='contactEmail' type='email' required></label><label>Phone<input name='contactPhone' required></label><label>Address<input name='address'></label><label>Permit / contract number<input name='permitNumber'></label><button>Add wrecker</button></form></section>
            <section class='card'><h2>Add tow zone</h2><form method='post' action='/tow-zones'><label>Zone name<input name='name' required></label><label>Description<input name='description'></label><label>Default zone wrecker<select name='defaultTowCompanyId' required><option value=''>Select company</option>{options}</select></label><label>Boundary GeoJSON<textarea name='boundaryGeoJson' rows='5' placeholder='Polygon or MultiPolygon GeoJSON'></textarea></label><button>Create zone</button></form></section></aside></div></main></body></html>
            """;
            return Results.Content(html,"text/html; charset=utf-8");
        }).RequireAuthorization();

        app.MapPost("/tow-zones/wreckers", async (HttpContext ctx) =>
        {
            if (!CanManageTowZones(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            var form = await ctx.Request.ReadFormAsync();
            var company = form["companyName"].ToString().Trim();
            var contact = form["contactName"].ToString().Trim();
            var email = form["contactEmail"].ToString().Trim();
            var phone = form["contactPhone"].ToString().Trim();
            if (company.Length==0 || contact.Length==0 || email.Length==0 || phone.Length==0) return Results.BadRequest("Company and contact details are required.");
            await using var cn = Open(app.Configuration); await cn.OpenAsync();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO ZoneWreckerCompanies(Id,CompanyName,ContactName,ContactEmail,ContactPhone,Address,PermitNumber,Active,CreatedAt,UpdatedAt) VALUES($id,$company,$contact,$email,$phone,$address,$permit,1,$now,$now)";
            cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString()); cmd.Parameters.AddWithValue("$company",company); cmd.Parameters.AddWithValue("$contact",contact); cmd.Parameters.AddWithValue("$email",email); cmd.Parameters.AddWithValue("$phone",phone); cmd.Parameters.AddWithValue("$address",form["address"].ToString().Trim()); cmd.Parameters.AddWithValue("$permit",form["permitNumber"].ToString().Trim()); cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            return Results.Redirect("/tow-zones");
        }).RequireAuthorization();

        app.MapPost("/tow-zones", async (HttpContext ctx) =>
        {
            if (!CanManageTowZones(ctx.User)) return Results.Forbid();
            await EnsureSchemaAsync(app.Configuration);
            var form = await ctx.Request.ReadFormAsync();
            var name = form["name"].ToString().Trim();
            var wrecker = form["defaultTowCompanyId"].ToString().Trim();
            if (name.Length==0 || !Guid.TryParse(wrecker,out _)) return Results.BadRequest("Zone name and default zone wrecker are required.");
            await using var cn = Open(app.Configuration); await cn.OpenAsync();
            var cmd = cn.CreateCommand();
            cmd.CommandText = "INSERT INTO TowZones(Id,Name,Description,BoundaryGeoJson,DefaultTowCompanyId,Active,CreatedAt,UpdatedAt) VALUES($id,$name,$description,$boundary,$wrecker,1,$now,$now)";
            cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString()); cmd.Parameters.AddWithValue("$name",name); cmd.Parameters.AddWithValue("$description",form["description"].ToString().Trim()); cmd.Parameters.AddWithValue("$boundary",form["boundaryGeoJson"].ToString().Trim()); cmd.Parameters.AddWithValue("$wrecker",wrecker); cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));
            try { await cmd.ExecuteNonQueryAsync(); } catch (SqliteException) { return Results.BadRequest("Zone name must be unique and the selected wrecker must exist."); }
            return Results.Redirect("/tow-zones");
        }).RequireAuthorization();
    }

    private static bool CanManageTowZones(ClaimsPrincipal user) => user.IsInRole("Administrator") || user.IsInRole("Supervisor") || user.FindAll(DepartmentAccess.ClaimType).Any(c=>string.Equals(c.Value,"Police",StringComparison.OrdinalIgnoreCase));
    private static SqliteConnection Open(IConfiguration c) { var path=c["CLTPP_PUBLIC_SAFETY_DB"] ?? Path.Combine(AppContext.BaseDirectory,"data","public-safety.db"); Directory.CreateDirectory(Path.GetDirectoryName(path)!); return new($"Data Source={path}"); }
    private static async Task EnsureSchemaAsync(IConfiguration c)
    {
        await using var cn=Open(c); await cn.OpenAsync(); var cmd=cn.CreateCommand(); cmd.CommandText="""
        PRAGMA foreign_keys=ON;
        CREATE TABLE IF NOT EXISTS ZoneWreckerCompanies(Id TEXT PRIMARY KEY,CompanyName TEXT NOT NULL,ContactName TEXT NOT NULL,ContactEmail TEXT NOT NULL,ContactPhone TEXT NOT NULL,Address TEXT NULL,PermitNumber TEXT NULL,Active INTEGER NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS TowZones(Id TEXT PRIMARY KEY,Name TEXT NOT NULL UNIQUE,Description TEXT NULL,BoundaryGeoJson TEXT NULL,DefaultTowCompanyId TEXT NOT NULL,Active INTEGER NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,FOREIGN KEY(DefaultTowCompanyId) REFERENCES ZoneWreckerCompanies(Id));
        """; await cmd.ExecuteNonQueryAsync();
    }
    private static async Task<List<WreckerRow>> LoadWreckersAsync(SqliteConnection cn) { var list=new List<WreckerRow>(); var cmd=cn.CreateCommand(); cmd.CommandText="SELECT Id,CompanyName,ContactName,ContactEmail,ContactPhone,Active FROM ZoneWreckerCompanies ORDER BY CompanyName"; await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) list.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt64(5)==1)); return list; }
    private static string? Nullable(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);
    private static string H(string? s)=>WebUtility.HtmlEncode(s??"");
    private sealed record ZoneRow(string Id,string Name,string? Description,string? Boundary,string WreckerId,bool Active,string? CompanyName,string? ContactName,string? ContactEmail,string? ContactPhone);
    private sealed record WreckerRow(string Id,string CompanyName,string ContactName,string ContactEmail,string ContactPhone,bool Active);
}
