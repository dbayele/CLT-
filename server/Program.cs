using System.Text.Json;
using CltPlusPlus.Api.Models;
using CltPlusPlus.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JsonRequestStore>();
builder.Services.AddHttpClient<CensusGeocoder>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CLTPlusPlus-Demo/1.0");
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true)));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", product = "CLT++", unofficial = true }));

app.MapGet("/api/services", (string? category, string? q) =>
{
    IEnumerable<ServiceDefinition> services = ServiceCatalog.All;
    if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        services = services.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    if (!string.IsNullOrWhiteSpace(q))
        services = services.Where(s =>
            s.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.Category.Contains(q, StringComparison.OrdinalIgnoreCase));

    return Results.Ok(services);
});

app.MapGet("/api/address/validate", async (string address, CensusGeocoder geocoder, CancellationToken cancellationToken) =>
{
    var result = await geocoder.ValidateAsync(address, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/requests", async (CreateServiceRequest input, JsonRequestStore store) =>
{
    var service = ServiceCatalog.All.FirstOrDefault(s => s.Id == input.ServiceId);
    if (service is null) return Results.BadRequest(new { error = "Unknown service." });

    var details = input.Details ?? new Dictionary<string, JsonElement>();
    var contact = input.Contact ?? new ContactInfo { Anonymous = service.Id == "crime-tip" };

    if (service.Id == "crime-report")
    {
        if (!details.TryGetValue("emergencyConfirmed", out var gate) || gate.ValueKind != JsonValueKind.True)
            return Results.BadRequest(new { error = "Confirm that this is not an emergency or crime in progress before continuing." });
        if (!details.TryGetValue("jurisdictionConfirmed", out var jurisdiction) || jurisdiction.ValueKind != JsonValueKind.True)
            return Results.BadRequest(new { error = "Confirm that the incident location was validated for this demo workflow." });
        if (!details.TryGetValue("incidentType", out var incidentType) || string.IsNullOrWhiteSpace(incidentType.GetString()))
            return Results.BadRequest(new { error = "Select an eligible incident type." });
        if (!details.TryGetValue("narrative", out var narrative) || string.IsNullOrWhiteSpace(narrative.GetString()))
            return Results.BadRequest(new { error = "Provide an incident narrative." });
        if (!details.TryGetValue("certified", out var certified) || certified.ValueKind != JsonValueKind.True)
            return Results.BadRequest(new { error = "Certification is required before submitting the report." });
        if (string.IsNullOrWhiteSpace(contact.Email))
            return Results.BadRequest(new { error = "An email address is required for this demo non-emergency report flow." });
    }

    if (service.Id == "crime-tip") contact = contact.WithAnonymousDefault();

    var isCrimeReport = service.Id == "crime-report";
    var request = new ServiceRequest
    {
        TrackingNumber = isCrimeReport
            ? $"TMP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000, 999999)}"
            : $"CLTPP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000, 999999)}",
        ServiceId = service.Id,
        ServiceTitle = service.Title,
        Category = service.Category,
        Location = input.Location?.Trim() ?? string.Empty,
        Details = details,
        Contact = contact,
        Status = isCrimeReport ? "Pending review" : "Submitted"
    };

    await store.AddAsync(request);
    return Results.Created($"/api/requests/{request.TrackingNumber}", new
    {
        request.TrackingNumber,
        request.Status,
        request.CreatedAt,
        reportKind = isCrimeReport ? "temporary" : "service-request",
        disclaimer = isCrimeReport
            ? "Temporary CLT++ demo report only; not transmitted to CMPD and not an official police report."
            : "Stored in the CLT++ demo only; not transmitted to CMPD, Charlotte Fire, 311, or Crime Stoppers."
    });
});

app.MapPost("/api/requests/{trackingNumber}/supplements", async (string trackingNumber, SupplementalRequest input, JsonRequestStore store) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    if (request is null) return Results.NotFound(new { error = "Report not found." });
    if (request.ServiceId != "crime-report") return Results.BadRequest(new { error = "Supplements are available only for crime reports." });
    if (string.IsNullOrWhiteSpace(input.Narrative)) return Results.BadRequest(new { error = "Supplement narrative is required." });

    var supplement = new JsonElementBuilder().Build(new
    {
        submittedAt = DateTimeOffset.UtcNow,
        narrative = input.Narrative.Trim(),
        people = input.People ?? string.Empty,
        property = input.Property ?? string.Empty,
        vehicles = input.Vehicles ?? string.Empty,
        evidence = input.Evidence ?? string.Empty
    });

    await store.AppendDetailAsync(request.TrackingNumber, "supplements", supplement);
    return Results.Ok(new { status = "Supplement received", trackingNumber = request.TrackingNumber });
});

app.MapGet("/api/requests/{trackingNumber}", async (string trackingNumber, JsonRequestStore store) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    return request is null ? Results.NotFound(new { error = "Request not found." }) : Results.Ok(request);
});

app.Run();

public sealed record SupplementalRequest(string Narrative, string? People, string? Property, string? Vehicles, string? Evidence);

static class ContactExtensions
{
    public static ContactInfo WithAnonymousDefault(this ContactInfo contact) => new()
    {
        Anonymous = contact.Anonymous || (string.IsNullOrWhiteSpace(contact.Name) && string.IsNullOrWhiteSpace(contact.Email) && string.IsNullOrWhiteSpace(contact.Phone)),
        Name = contact.Anonymous ? null : contact.Name,
        Email = contact.Anonymous ? null : contact.Email,
        Phone = contact.Anonymous ? null : contact.Phone,
        PreferredMethod = contact.Anonymous ? null : contact.PreferredMethod
    };
}

sealed class JsonElementBuilder
{
    public JsonElement Build<T>(T value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return doc.RootElement.Clone();
    }
}
