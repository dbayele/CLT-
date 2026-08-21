using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class InboundCallbackWorkflows
{
    public static void Map(WebApplication app)
    {
        var db=app.Configuration["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);Ensure(db);

        app.MapGet("/inbound-callbacks",async(HttpContext ctx,IAntiforgery anti)=>
        {
            var allowed=Allowed(ctx.User);if(allowed.Length==0)return Results.Forbid();var token=anti.GetAndStoreTokens(ctx).RequestToken!;
            await using var cn=Open(db);await cn.OpenAsync();var c=cn.CreateCommand();var ps=string.Join(",",allowed.Select((_,i)=>$"$d{i}"));
            c.CommandText=$"SELECT q.Id,q.QueueNumber,q.CallerPhone,q.Department,q.Status,q.ReceivedAt,q.ChimeCallId FROM InboundCallQueue q LEFT JOIN InboundChimeCalls i ON i.ChimeCallId=q.ChimeCallId WHERE q.Department IN ({ps}) AND q.CallerPhone<>'' AND (q.Status='Caller disconnected' OR i.Status='Ended') ORDER BY q.ReceivedAt DESC LIMIT 300";for(var i=0;i<allowed.Length;i++)c.Parameters.AddWithValue($"$d{i}",allowed[i]);
            var rows=new List<string>();await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<tr><td><b>{H(r.GetString(1))}</b></td><td>{H(r.GetString(3))}</td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(4))}</td><td>{H(r.GetString(5))}</td><td><form method='post' action='/inbound-callbacks/{r.GetString(0)}'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><button>Call back via Chime</button></form></td></tr>");
            return Html(Page("Disconnected callers",$"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>AMAZON CHIME</span><h1>Disconnected callers</h1><p>Callback is available only for callers captured from an inbound queue. The stored caller number cannot be edited here.</p></div></section><section class='table-card'><table><thead><tr><th>Queue</th><th>Department</th><th>Caller</th><th>Status</th><th>Received</th><th></th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='6'>No disconnected callers available for callback.</td></tr>")}</tbody></table></section></main>"));
        }).RequireAuthorization();

        app.MapPost("/inbound-callbacks/{queueId:guid}",async(Guid queueId,HttpContext ctx,IAntiforgery anti,IHttpClientFactory httpFactory)=>
        {
            var allowed=Allowed(ctx.User);if(allowed.Length==0)return Results.Forbid();await anti.ValidateRequestAsync(ctx);
            await using var cn=Open(db);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText="SELECT q.CallerPhone,q.Department,q.QueueNumber,q.ChimeCallId,COALESCE(i.Status,'') FROM InboundCallQueue q LEFT JOIN InboundChimeCalls i ON i.ChimeCallId=q.ChimeCallId WHERE q.Id=$i";c.Parameters.AddWithValue("$i",queueId.ToString());await using var r=await c.ExecuteReaderAsync();if(!await r.ReadAsync())return Results.NotFound();var phone=r.GetString(0);var dep=r.GetString(1);var qnum=r.GetString(2);var originalCall=r.GetString(3);var inboundStatus=r.GetString(4);if(!allowed.Contains(dep,StringComparer.OrdinalIgnoreCase))return Results.Forbid();if(string.IsNullOrWhiteSpace(phone))return Results.BadRequest("The inbound call does not contain a callback number.");if(inboundStatus!="Ended")return Results.Conflict(new{error="Callback is available after the inbound call has ended."});
            var endpoint=app.Configuration["CLTPP_CHIME_CALL_API_URL"];if(string.IsNullOrWhiteSpace(endpoint))return Results.Problem("CLTPP_CHIME_CALL_API_URL is not configured.");var id=Guid.NewGuid();var now=Now();await Exec(db,"INSERT INTO InboundCallbacks(Id,InboundQueueId,Department,CallerPhone,OriginalChimeCallId,Status,RequestedBy,RequestedAt,UpdatedAt) VALUES($i,$q,$d,$p,$o,'Requested',$by,$t,$t)",( "$i",id),("$q",queueId),("$d",dep),("$p",phone),("$o",originalCall),("$by",ctx.User.Identity?.Name??"employee"),("$t",now));
            try
            {
                var client=httpFactory.CreateClient();var apiKey=app.Configuration["CLTPP_CHIME_CALL_API_KEY"];if(!string.IsNullOrWhiteSpace(apiKey))client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key",apiKey);
                var payload=new{callbackId=id,inboundQueueId=queueId,department=dep,toPhoneNumber=phone,fromPhoneNumber=app.Configuration["CLTPP_CHIME_FROM_PHONE_NUMBER"],sipMediaApplicationId=app.Configuration["CLTPP_CHIME_SIP_MEDIA_APPLICATION_ID"],record=true,recordingDisclosure=true,recordingS3Bucket=app.Configuration["CLTPP_CHIME_RECORDING_S3_BUCKET"],officer=ctx.User.Identity?.Name,source="inbound-disconnect-callback"};var resp=await client.PostAsJsonAsync(endpoint,payload);var body=await resp.Content.ReadAsStringAsync();if(!resp.IsSuccessStatusCode){await Exec(db,"UPDATE InboundCallbacks SET Status='Failed',ProviderResponse=$r,UpdatedAt=$t WHERE Id=$i",("$r",body),("$t",Now()),("$i",id));return Results.Problem("Amazon Chime callback request failed.");}
                await Exec(db,"UPDATE InboundCallbacks SET Status='Submitted to Chime',ProviderResponse=$r,UpdatedAt=$t WHERE Id=$i",("$r",body),("$t",Now()),("$i",id));await Exec(db,"UPDATE InboundCallQueue SET Status='Callback initiated',UpdatedAt=$t WHERE Id=$i",("$t",Now()),("$i",queueId));return Results.Redirect("/inbound-callbacks");
            }
            catch(Exception ex){await Exec(db,"UPDATE InboundCallbacks SET Status='Failed',ProviderResponse=$r,UpdatedAt=$t WHERE Id=$i",("$r",ex.Message),("$t",Now()),("$i",id));return Results.Problem("Amazon Chime callback request failed.");}
        }).RequireAuthorization();

        app.MapPost("/integrations/chime/inbound-callback/{id:guid}",async(Guid id,HttpContext ctx)=>
        {
            var secret=app.Configuration["CLTPP_CHIME_CALLBACK_SECRET"];if(string.IsNullOrWhiteSpace(secret)||!string.Equals(ctx.Request.Headers["X-CLTPP-Chime-Secret"],secret,StringComparison.Ordinal))return Results.Unauthorized();var f=await ctx.Request.ReadFromJsonAsync<CallbackUpdate>();if(f is null)return Results.BadRequest();await Exec(db,"UPDATE InboundCallbacks SET Status=$s,CallbackChimeCallId=$c,RecordingReference=$r,StartedAt=COALESCE(NULLIF($st,''),StartedAt),EndedAt=COALESCE(NULLIF($e,''),EndedAt),ProviderResponse=$p,UpdatedAt=$t WHERE Id=$i",("$s",f.Status??"Updated"),("$c",f.ChimeCallId),("$r",f.RecordingReference),("$st",f.StartedAt),("$e",f.EndedAt),("$p",f.ProviderResponse),("$t",Now()),("$i",id));return Results.Ok();
        });
    }

    sealed class CallbackUpdate{public string? Status{get;set;}public string? ChimeCallId{get;set;}public string? RecordingReference{get;set;}public string? StartedAt{get;set;}public string? EndedAt{get;set;}public string? ProviderResponse{get;set;}}
    static string[] Allowed(ClaimsPrincipal u){if(u.IsInRole("Administrator")||u.IsInRole("Supervisor"))return new[]{"Police","EMS","Fire"};var vals=u.Claims.Where(c=>c.Type==DepartmentAccess.ClaimType).Select(c=>c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);return new[]{"Police","EMS","Fire"}.Where(vals.Contains).ToArray();}
    static void Ensure(string p){using var cn=Open(p);cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS InboundCallbacks(Id TEXT PRIMARY KEY,InboundQueueId TEXT NOT NULL,Department TEXT NOT NULL,CallerPhone TEXT NOT NULL,OriginalChimeCallId TEXT NOT NULL,CallbackChimeCallId TEXT,Status TEXT NOT NULL,RequestedBy TEXT NOT NULL,RequestedAt TEXT NOT NULL,StartedAt TEXT,EndedAt TEXT,RecordingReference TEXT,ProviderResponse TEXT,UpdatedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_InboundCallbacks_Queue ON InboundCallbacks(InboundQueueId,RequestedAt);";c.ExecuteNonQuery();}
    static SqliteConnection Open(string p)=>new($"Data Source={p}");static async Task Exec(string p,string sql,params(string,object?)[] ps){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText=sql;foreach(var x in ps)c.Parameters.AddWithValue(x.Item1,x.Item2?.ToString()??"");await c.ExecuteNonQueryAsync();}static string Now()=>DateTimeOffset.UtcNow.ToString("O");static string H(object? x)=>WebUtility.HtmlEncode(x?.ToString()??"");static IResult Html(string x)=>Results.Content(x,"text/html; charset=utf-8");static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{H(t)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>{b}</body></html>";
}
