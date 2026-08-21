using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class InboundChimeIvr
{
    public static void Map(WebApplication app)
    {
        var dbPath=app.Configuration["CLTPP_PUBLIC_SAFETY_DB"]??Path.Combine(AppContext.BaseDirectory,"data","public-safety.db");Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);Ensure(dbPath);
        InboundCallbackWorkflows.Map(app);

        app.MapPost("/integrations/chime/inbound-ivr",async(HttpContext ctx)=>
        {
            var secret=app.Configuration["CLTPP_CHIME_IVR_SECRET"];if(string.IsNullOrWhiteSpace(secret)||!string.Equals(ctx.Request.Headers["X-CLTPP-Chime-Secret"],secret,StringComparison.Ordinal))return Results.Unauthorized();
            ChimeEvent? e;try{e=await JsonSerializer.DeserializeAsync<ChimeEvent>(ctx.Request.Body,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});}catch{return Results.BadRequest();}if(e is null)return Results.BadRequest();
            var eventType=e.InvocationEventType??"";var callId=e.CallDetails?.Participants?.FirstOrDefault()?.CallId??e.CallDetails?.TransactionId??Guid.NewGuid().ToString("N");var from=e.CallDetails?.Participants?.FirstOrDefault()?.From??"";var to=e.CallDetails?.Participants?.FirstOrDefault()?.To??"";

            if(eventType is "NEW_INBOUND_CALL" or "NEW_INCOMING_CALL")
            {
                await UpsertInbound(dbPath,callId,from,to,"Awaiting selection",null,e.CallDetails?.TransactionId);
                return Results.Json(new{SchemaVersion="1.0",Actions=new object[]{new{Type="PlayAudioAndGetDigits",Parameters=new{CallId=callId,InputDigitRegex="[1-3]",InBetweenDigitsDurationInMilliseconds=5000,Repeat=2,RepeatDurationInMilliseconds=1000,AudioSource=new{Type="S3",BucketName=app.Configuration["CLTPP_CHIME_IVR_PROMPT_BUCKET"],Key=app.Configuration["CLTPP_CHIME_IVR_PROMPT_KEY"]??"ivr/police-ems-fire.wav"},FailureAudioSource=new{Type="S3",BucketName=app.Configuration["CLTPP_CHIME_IVR_PROMPT_BUCKET"],Key=app.Configuration["CLTPP_CHIME_IVR_INVALID_PROMPT_KEY"]??"ivr/invalid-selection.wav"}}}}});
            }

            if(eventType=="ACTION_SUCCESSFUL"&&string.Equals(e.ActionData?.Type,"PlayAudioAndGetDigits",StringComparison.OrdinalIgnoreCase))
            {
                var digit=e.ActionData?.ReceivedDigits??e.ActionData?.Digits??"";var department=digit switch{"1"=>"Police","2"=>"EMS","3"=>"Fire",_=>""};if(department.Length==0)return Results.Json(new{SchemaVersion="1.0",Actions=new object[]{new{Type="Hangup",Parameters=new{CallId=callId}}}});
                var intakeId=Guid.NewGuid();var number=$"IN-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000,999999)}";var now=Now();await Exec(dbPath,"INSERT INTO InboundCallQueue(Id,QueueNumber,ChimeCallId,TransactionId,CallerPhone,CalledNumber,Department,Status,ReceivedAt,UpdatedAt) VALUES($i,$n,$c,$tr,$f,$t,$d,'Queued',$now,$now)",("$i",intakeId),("$n",number),("$c",callId),("$tr",e.CallDetails?.TransactionId),("$f",from),("$t",to),("$d",department),("$now",now));await UpsertInbound(dbPath,callId,from,to,"Queued",department,e.CallDetails?.TransactionId);
                var queueEndpoint=department switch{"Police"=>app.Configuration["CLTPP_CHIME_POLICE_QUEUE_ENDPOINT"],"EMS"=>app.Configuration["CLTPP_CHIME_EMS_QUEUE_ENDPOINT"],"Fire"=>app.Configuration["CLTPP_CHIME_FIRE_QUEUE_ENDPOINT"],_=>null};
                if(string.IsNullOrWhiteSpace(queueEndpoint))return Results.Json(new{SchemaVersion="1.0",Actions=new object[]{new{Type="Speak",Parameters=new{CallId=callId,Text=$"You have been placed in the {department} queue. Please remain on the line."}},new{Type="Pause",Parameters=new{CallId=callId,DurationInMilliseconds=30000}}}});
                return Results.Json(new{SchemaVersion="1.0",Actions=new object[]{new{Type="Speak",Parameters=new{CallId=callId,Text=$"You selected {department}. Please remain on the line while we connect you."}},new{Type="CallAndBridge",Parameters=new{CallId=callId,Endpoints=new[]{new{Uri=queueEndpoint,BridgeEndpointType="PSTN"}}}}}});
            }

            if(eventType=="HANGUP")
            {
                await Exec(dbPath,"UPDATE InboundChimeCalls SET Status='Ended',EndedAt=$t,UpdatedAt=$t WHERE ChimeCallId=$c",("$t",Now()),("$c",callId));await Exec(dbPath,"UPDATE InboundCallQueue SET Status=CASE WHEN Status='Queued' THEN 'Caller disconnected' ELSE Status END,UpdatedAt=$t WHERE ChimeCallId=$c",("$t",Now()),("$c",callId));return Results.Json(new{SchemaVersion="1.0",Actions=Array.Empty<object>()});
            }

            return Results.Json(new{SchemaVersion="1.0",Actions=Array.Empty<object>()});
        });

        app.MapGet("/inbound-queues",async(HttpContext ctx,string? department)=>
        {
            if(!CanDispatch(ctx.User))return Results.Forbid();var allowed=AllowedDepartments(ctx.User);await using var cn=Open(dbPath);await cn.OpenAsync();var c=cn.CreateCommand();var permitted=string.Join(",",allowed.Select((_,i)=>$"$d{i}"));c.CommandText=$"SELECT Id,QueueNumber,CallerPhone,CalledNumber,Department,Status,ReceivedAt FROM InboundCallQueue WHERE Department IN ({permitted}) AND ($filter='' OR Department=$filter) ORDER BY CASE WHEN Status='Queued' THEN 0 ELSE 1 END,ReceivedAt";for(var i=0;i<allowed.Length;i++)c.Parameters.AddWithValue($"$d{i}",allowed[i]);c.Parameters.AddWithValue("$filter",department??"");var rows=new List<string>();await using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add($"<tr><td><b>{H(r.GetString(1))}</b></td><td>{H(r.GetString(2))}</td><td>{H(r.GetString(4))}</td><td>{H(r.GetString(5))}</td><td>{H(r.GetString(6))}</td></tr>");return Html(Page("Inbound queues",$"<main class='wrap'><section class='page-head'><div><span class='eyebrow'>AMAZON CHIME INBOUND IVR</span><h1>Police / EMS / Fire queues</h1><p>Calls are routed only from the caller's explicit menu selection.</p><a class='button' href='/inbound-callbacks'>Disconnected caller callbacks</a></div></section><section class='table-card'><table><thead><tr><th>Queue</th><th>Caller</th><th>Department</th><th>Status</th><th>Received</th></tr></thead><tbody>{(rows.Count>0?string.Join("",rows):"<tr><td colspan='5'>No calls in your permitted queues.</td></tr>")}</tbody></table></section></main>"));
        }).RequireAuthorization();
    }

    private sealed class ChimeEvent{public string? InvocationEventType{get;set;}public ChimeCallDetails? CallDetails{get;set;}public ChimeActionData? ActionData{get;set;}}
    private sealed class ChimeCallDetails{public string? TransactionId{get;set;}public List<ChimeParticipant>? Participants{get;set;}}
    private sealed class ChimeParticipant{public string? CallId{get;set;}public string? From{get;set;}public string? To{get;set;}}
    private sealed class ChimeActionData{public string? Type{get;set;}public string? ReceivedDigits{get;set;}public string? Digits{get;set;}}
    static async Task UpsertInbound(string p,string callId,string from,string to,string status,string? department,string? transaction){await Exec(p,"INSERT INTO InboundChimeCalls(ChimeCallId,TransactionId,CallerPhone,CalledNumber,Department,Status,StartedAt,UpdatedAt) VALUES($c,$tr,$f,$t,$d,$s,$now,$now) ON CONFLICT(ChimeCallId) DO UPDATE SET Department=COALESCE(NULLIF($d,''),Department),Status=$s,UpdatedAt=$now",("$c",callId),("$tr",transaction),("$f",from),("$t",to),("$d",department),("$s",status),("$now",Now()));}
    static string[] AllowedDepartments(ClaimsPrincipal u){if(u.IsInRole("Administrator")||u.IsInRole("Supervisor"))return new[]{"Police","EMS","Fire"};var vals=u.Claims.Where(c=>c.Type==DepartmentAccess.ClaimType).Select(c=>c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);var x=new List<string>();if(vals.Contains("Police"))x.Add("Police");if(vals.Contains("Fire"))x.Add("Fire");if(vals.Contains("EMS"))x.Add("EMS");return x.ToArray();}
    static bool CanDispatch(ClaimsPrincipal u)=>AllowedDepartments(u).Length>0;static void Ensure(string p){using var cn=Open(p);cn.Open();var c=cn.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS InboundChimeCalls(ChimeCallId TEXT PRIMARY KEY,TransactionId TEXT,CallerPhone TEXT,CalledNumber TEXT,Department TEXT,Status TEXT NOT NULL,StartedAt TEXT NOT NULL,EndedAt TEXT,UpdatedAt TEXT NOT NULL);CREATE TABLE IF NOT EXISTS InboundCallQueue(Id TEXT PRIMARY KEY,QueueNumber TEXT NOT NULL UNIQUE,ChimeCallId TEXT NOT NULL,TransactionId TEXT,CallerPhone TEXT,CalledNumber TEXT,Department TEXT NOT NULL,Status TEXT NOT NULL,ReceivedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);CREATE INDEX IF NOT EXISTS IX_InboundCallQueue_DeptStatus ON InboundCallQueue(Department,Status,ReceivedAt);";c.ExecuteNonQuery();}
    static SqliteConnection Open(string p)=>new($"Data Source={p}");static async Task Exec(string p,string sql,params(string,object?)[] ps){await using var cn=Open(p);await cn.OpenAsync();var c=cn.CreateCommand();c.CommandText=sql;foreach(var x in ps)c.Parameters.AddWithValue(x.Item1,x.Item2?.ToString()??"");await c.ExecuteNonQueryAsync();}static string Now()=>DateTimeOffset.UtcNow.ToString("O");static string H(object? x)=>WebUtility.HtmlEncode(x?.ToString()??"");static IResult Html(string x)=>Results.Content(x,"text/html; charset=utf-8");static string Page(string t,string b)=>$"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{H(t)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body>{b}</body></html>";
}
