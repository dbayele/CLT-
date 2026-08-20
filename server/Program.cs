using System.Text.Json;
using CltPlusPlus.Api.Models;
using CltPlusPlus.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JsonRequestStore>();
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
        if (string.IsNullOrWhiteSpace(contact.Email))
            return Results.BadRequest(new { error = "An email address is required for this demo non-emergency report flow." });
    }

    if (service.Id == "crime-tip") contact = contact.WithAnonymousDefault();

    var request = new ServiceRequest
    {
        TrackingNumber = $"CLTPP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000, 999999)}",
        ServiceId = service.Id,
        ServiceTitle = service.Title,
        Category = service.Category,
        Location = input.Location?.Trim() ?? string.Empty,
        Details = details,
        Contact = contact
    };

    await store.AddAsync(request);
    return Results.Created($"/api/requests/{request.TrackingNumber}", new
    {
        request.TrackingNumber,
        request.Status,
        request.CreatedAt,
        disclaimer = "Stored in the CLT++ demo only; not transmitted to CMPD, 311, or Crime Stoppers."
    });
});

app.MapGet("/api/requests/{trackingNumber}", async (string trackingNumber, JsonRequestStore store) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    return request is null ? Results.NotFound(new { error = "Request not found." }) : Results.Ok(request);
});

app.Run();

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
