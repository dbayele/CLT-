using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

var builder=WebApplication.CreateBuilder(args);
var app=builder.Build();
var db=builder.Configuration["CLTPP_ACCESS_DB"]??Path.Combine(AppContext.BaseDirectory,"data","access-control.db");Directory.CreateDirectory(Path.GetDirectoryName(db)!);Ensure(db);

app.MapPost("/v1/access/check",async(HttpContext ctx,AccessCheckRequest req)=>
{
    var secret=builder.Configuration["CLTPP_ACCESS_API_SECRET"]??"";var supplied=ctx.Request.Headers["X-CLTPP-Access-Secret"].ToString();if(secret.Length==0||!Fixed(secret,supplied))return Results.Unauthorized();
    var email=(req.Email??"").Trim().ToLowerInvariant();var feature=(req.Feature??"").Trim();var scope=string.IsNullOrWhiteSpace(req.Scope)?"global":req.Scope!.Trim();if(email.Length==0||feature.Length==0)return Results.BadRequest();
    await using var cn=new SqliteConnection($"Data Source={db}");await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText="SELECT Effect,Scope,ExpiresAt FROM FeatureGrants WHERE lower(Email)=lower($e) AND FeatureKey=$f AND (Scope=$s OR Scope='global') AND (ExpiresAt IS NULL OR ExpiresAt='' OR ExpiresAt>$now) ORDER BY CASE WHEN Effect='Deny' THEN 0 ELSE 1 END,CASE WHEN Scope=$s THEN 0 ELSE 1 END,GrantedAt DESC LIMIT 1";c.Parameters.AddWithValue("$e",email);c.Parameters.AddWithValue("$f",feature);c.Parameters.AddWithValue("$s",scope);c.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await using var r=await c.ExecuteReaderAsync();if(await r.ReadAsync()){var effect=r.GetString(0);return Results.Ok(new AccessDecision(string.Equals(effect,"Allow",StringComparison.OrdinalIgnoreCase),effect,"explicit",feature,scope));}
    return Results.Ok(new AccessDecision(req.BaselineAllowed,req.BaselineAllowed?"Allow":"Deny","baseline",feature,scope));
});
app.MapGet("/health",()=>Results.Ok(new{status="ok",service="CLT++ Access API"}));app.Run();

void Ensure(string p){using var cn=new SqliteConnection($"Data Source={p}");cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS FeatureGrants(Id TEXT PRIMARY KEY,Email TEXT NOT NULL,FeatureKey TEXT NOT NULL,Scope TEXT NOT NULL,Effect TEXT NOT NULL,ExpiresAt TEXT,Reason TEXT NOT NULL,GrantedBy TEXT NOT NULL,GrantedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_FeatureGrants_EmailFeature ON FeatureGrants(Email,FeatureKey,Scope,GrantedAt);CREATE TABLE IF NOT EXISTS FeatureGrantAudit(Id TEXT PRIMARY KEY,Email TEXT NOT NULL,FeatureKey TEXT NOT NULL,Scope TEXT NOT NULL,Effect TEXT NOT NULL,Reason TEXT NOT NULL,Actor TEXT NOT NULL,OccurredAt TEXT NOT NULL);";c.ExecuteNonQuery();}
bool Fixed(string a,string b){var x=Encoding.UTF8.GetBytes(a);var y=Encoding.UTF8.GetBytes(b);return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);} 
record AccessCheckRequest(string? Email,string? Feature,string? Scope,bool BaselineAllowed,string? Application);
record AccessDecision(bool Allowed,string Effect,string Source,string Feature,string Scope);
