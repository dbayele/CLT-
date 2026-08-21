using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;

namespace CltPlusPlus.Employee;

public static class VehicleCameraLookup
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/vehicle-lookup", (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            var token = anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("Vehicle camera lookup", $"""
            <main class='wrap'><a class='back' href='/'>← Work queue</a><section class='page-head'><div><span class='eyebrow'>POLICE · VEHICLE LOOKUP</span><h1>Camera-assisted vehicle lookup</h1><p>Capture a plate or VIN, confirm the OCR result, then retrieve configured non-owner vehicle details.</p></div></section>
            <section class='card'><form method='post' enctype='multipart/form-data' action='/vehicle-lookup/read'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><label>Camera capture<input type='file' name='capture' accept='image/*' capture='environment' required/></label><label>Read as<select name='identifierType'><option value='plate'>License plate</option><option value='vin'>VIN</option></select></label><button type='submit'>Read image</button></form></section></main>"""));
        }).RequireAuthorization();

        app.MapPost("/vehicle-lookup/read", async (HttpContext ctx, IAntiforgery anti) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files["capture"];
            if (file is null || file.Length == 0 || file.Length > 20L*1024*1024) return Results.BadRequest("A camera image up to 20 MB is required.");
            var kind = form["identifierType"].ToString()=="vin" ? "vin" : "plate";
            var temp=Path.Combine(Path.GetTempPath(),$"cltpp-ocr-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
            await using(var fs=File.Create(temp)) await file.CopyToAsync(fs);
            string text="";
            try
            {
                var tess=app.Configuration["CLTPP_TESSDATA_PATH"];
                if(!string.IsNullOrWhiteSpace(tess)&&Directory.Exists(tess))
                {
                    using var engine=new Engine(tess,Language.English,EngineMode.Default);
                    using var image=Image.LoadFromFile(temp);
                    using var page=engine.Process(image);
                    text=page.Text??"";
                }
            }
            finally { try{File.Delete(temp);}catch{} }
            var candidate=Extract(text,kind);
            var token=anti.GetAndStoreTokens(ctx).RequestToken!;
            return Html(Page("Confirm vehicle",$"""
            <main class='wrap'><a class='back' href='/vehicle-lookup'>← New capture</a><section class='page-head'><div><span class='eyebrow'>CONFIRM OCR</span><h1>Confirm vehicle identifier</h1><p>Correct the OCR result before lookup.</p></div></section><section class='card'><form method='post' action='/vehicle-lookup/query'><input type='hidden' name='__RequestVerificationToken' value='{H(token)}'/><input type='hidden' name='identifierType' value='{kind}'/><label>{(kind=="vin"?"VIN":"License plate")}<input name='identifier' value='{H(candidate)}' required/></label>{(kind=="plate"?"<label>Plate state<input name='plateState' value='NC' maxlength='3' required/></label>":"")}<button type='submit'>Look up vehicle details</button></form></section></main>"""));
        }).RequireAuthorization();

        app.MapPost("/vehicle-lookup/query", async (HttpContext ctx, IAntiforgery anti, IHttpClientFactory factory) =>
        {
            if (!CanPolice(ctx.User)) return Results.Forbid();
            await anti.ValidateRequestAsync(ctx);
            var form=await ctx.Request.ReadFormAsync();
            var kind=form["identifierType"].ToString()=="vin"?"vin":"plate";
            var id=Normalize(form["identifier"].ToString());
            var state=Normalize(form["plateState"].ToString());
            if(kind=="vin"&&id.Length!=17)return Results.BadRequest("A VIN must be 17 characters.");
            if(kind=="plate"&&string.IsNullOrWhiteSpace(id))return Results.BadRequest("A license plate is required.");
            var baseUrl=app.Configuration["CLTPP_VEHICLE_PROVIDER_URL"];
            object output;
            if(string.IsNullOrWhiteSpace(baseUrl)) output=new{available=false,identifierType=kind,identifier=id,plateState=state,message="Vehicle data provider is not configured."};
            else
            {
                var client=factory.CreateClient();
                var url=baseUrl.TrimEnd('/')+$"/vehicle?identifierType={Uri.EscapeDataString(kind)}&identifier={Uri.EscapeDataString(id)}&plateState={Uri.EscapeDataString(state)}";
                using var response=await client.GetAsync(url);
                var json=await response.Content.ReadAsStringAsync();
                output=Sanitize(json,kind,id,state);
            }
            return Html(Page("Vehicle details",$"""<main class='wrap'><a class='back' href='/vehicle-lookup'>← New lookup</a><section class='page-head'><div><span class='eyebrow'>VEHICLE DETAILS</span><h1>{H(kind=="vin"?id:$"{state} {id}")}</h1></div></section><section class='card'><pre style='white-space:pre-wrap'>{H(JsonSerializer.Serialize(output,new JsonSerializerOptions{{WriteIndented=true}}))}</pre></section></main>"""));
        }).RequireAuthorization();
    }

    private static object Sanitize(string json,string kind,string id,string state)
    {
        try
        {
            using var doc=JsonDocument.Parse(json);var r=doc.RootElement;
            string? Get(params string[] names){foreach(var n in names)if(r.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null)return v.ToString();return null;}
            return new{available=true,identifierType=kind,identifier=id,plateState=state,year=Get("year","modelYear"),make=Get("make"),model=Get("model"),bodyType=Get("bodyType","body_style"),color=Get("color"),vin=Get("vin"),plate=Get("plate","licensePlate")};
        }
        catch{return new{available=false,identifierType=kind,identifier=id,plateState=state,message="Vehicle provider returned an unreadable response."};}
    }
    private static string Extract(string raw,string kind){var t=raw.ToUpperInvariant().Split(new[]{' ','\r','\n','\t',':',';','|'},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(x=>x.Length>0).ToList();if(kind=="vin")return t.FirstOrDefault(x=>x.Length==17&&!x.Any(c=>"IOQ".Contains(c)))??"";return t.Where(x=>x.Length is >=4 and <=10).OrderByDescending(x=>x.Length).FirstOrDefault()??"";}
    private static string Normalize(string? s)=>new((s??"").Trim().ToUpperInvariant().Where(c=>char.IsLetterOrDigit(c)||c=='-').ToArray());
    private static bool CanPolice(ClaimsPrincipal u)=>u.IsInRole("Administrator")||u.IsInRole("Supervisor")||u.FindAll(DepartmentAccess.ClaimType).Any(c=>c.Value.Equals("Police",StringComparison.OrdinalIgnoreCase));
    private static IResult Html(string s)=>Results.Content(s,"text/html; charset=utf-8");private static string H(string? s)=>WebUtility.HtmlEncode(s??"");
    private static string Page(string title,string body)=>$"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{H(title)} · CLT++ Employee</title><link rel='stylesheet' href='/app.css'></head><body><header class='top'><a href='/' class='brand'>CLT<span>++</span> <small>EMPLOYEE</small></a><div>Public Safety</div></header>{body}</body></html>";
}
