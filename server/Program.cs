using System.Security.Claims;
using System.Text.Json;
using CltPlusPlus.Api;
using CltPlusPlus.Api.Models;
using CltPlusPlus.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JsonRequestStore>();
builder.Services.AddSingleton<StripePaymentService>();
builder.Services.AddHttpClient<CensusGeocoder>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CLTPlusPlus-Demo/1.0");
});
builder.Services.AddHttpClient<CivicProfileService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(18);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CLTPlusPlus-Demo/1.0");
});

builder.Services.AddDbContext<ResidentDataContext>(o => DatabaseRuntime.ConfigureResidentDatabase(o, builder.Environment, builder.Configuration));
builder.Services.AddIdentity<ResidentUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 10;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireNonAlphanumeric = false;
    o.Lockout.MaxFailedAccessAttempts = 8;
}).AddEntityFrameworkStores<ResidentDataContext>().AddDefaultTokenProviders();

var auth = builder.Services.AddAuthentication();
if (HasConfig("GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET"))
    auth.AddGoogle("Google", o =>
    {
        o.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"]!;
        o.ClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
if (HasConfig("MICROSOFT_CLIENT_ID", "MICROSOFT_CLIENT_SECRET"))
    auth.AddMicrosoftAccount("Microsoft", o =>
    {
        o.ClientId = builder.Configuration["MICROSOFT_CLIENT_ID"]!;
        o.ClientSecret = builder.Configuration["MICROSOFT_CLIENT_SECRET"]!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
if (HasConfig("FACEBOOK_APP_ID", "FACEBOOK_APP_SECRET"))
    auth.AddFacebook("Facebook", o =>
    {
        o.AppId = builder.Configuration["FACEBOOK_APP_ID"]!;
        o.AppSecret = builder.Configuration["FACEBOOK_APP_SECRET"]!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
        o.Fields.Add("email");
    });
if (HasConfig("APPLE_CLIENT_ID", "APPLE_CLIENT_SECRET"))
    auth.AddOpenIdConnect("Apple", o =>
    {
        o.Authority = "https://appleid.apple.com";
        o.ClientId = builder.Configuration["APPLE_CLIENT_ID"]!;
        o.ClientSecret = builder.Configuration["APPLE_CLIENT_SECRET"]!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
        o.ResponseType = "code";
        o.ResponseMode = "form_post";
        o.Scope.Add("name");
        o.Scope.Add("email");
        o.SaveTokens = true;
    });

var publicUrl = (builder.Configuration["CLTPP_PUBLIC_URL"] ?? "http://localhost:5173").TrimEnd('/');
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(publicUrl).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<ResidentDataContext>().Database.EnsureCreatedAsync();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", (StripePaymentService payments) => Results.Ok(new { status = "ok", product = "CLT++", unofficial = true, database = DatabaseRuntime.Describe(builder.Environment, builder.Configuration), stripe = payments.Enabled ? "configured" : "not-configured" }));

app.MapPost("/api/account/register", async (RegisterResident input, UserManager<ResidentUser> users, SignInManager<ResidentUser> signIn) =>
{
    var email = input.Email.Trim().ToLowerInvariant();
    var user = new ResidentUser { UserName = email, Email = email, DisplayName = input.DisplayName?.Trim() };
    var created = await users.CreateAsync(user, input.Password);
    if (!created.Succeeded) return Results.BadRequest(new { errors = created.Errors.Select(e => e.Description) });
    await signIn.SignInAsync(user, isPersistent: true);
    return Results.Ok(AccountView(user));
});

app.MapPost("/api/account/login", async (LoginRequest input, SignInManager<ResidentUser> signIn, UserManager<ResidentUser> users) =>
{
    var email = input.Email.Trim().ToLowerInvariant();
    var result = await signIn.PasswordSignInAsync(email, input.Password, isPersistent: true, lockoutOnFailure: true);
    if (!result.Succeeded) return Results.BadRequest(new { error = "Invalid email or password." });
    var user = await users.FindByEmailAsync(email);
    return user is null ? Results.BadRequest(new { error = "Account not found." }) : Results.Ok(AccountView(user));
});

app.MapPost("/api/account/logout", async (SignInManager<ResidentUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.Ok(new { signedOut = true });
});

app.MapGet("/api/account/me", async (ClaimsPrincipal principal, UserManager<ResidentUser> users) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    return user is null ? Results.Unauthorized() : Results.Ok(AccountView(user));
});

app.MapGet("/api/account/providers", () => Results.Ok(new[]
{
    new { id = "Google", enabled = HasConfig("GOOGLE_CLIENT_ID", "GOOGLE_CLIENT_SECRET") },
    new { id = "Microsoft", enabled = HasConfig("MICROSOFT_CLIENT_ID", "MICROSOFT_CLIENT_SECRET") },
    new { id = "Apple", enabled = HasConfig("APPLE_CLIENT_ID", "APPLE_CLIENT_SECRET") },
    new { id = "Facebook", enabled = HasConfig("FACEBOOK_APP_ID", "FACEBOOK_APP_SECRET") }
}));

app.MapGet("/api/account/external/{provider}", (string provider, SignInManager<ResidentUser> signIn) =>
{
    var allowed = new[] { "Google", "Microsoft", "Apple", "Facebook" };
    if (!allowed.Contains(provider, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "Unsupported provider." });
    var callback = $"/api/account/external-callback?provider={Uri.EscapeDataString(provider)}";
    var props = signIn.ConfigureExternalAuthenticationProperties(provider, callback);
    return Results.Challenge(props, new[] { provider });
});

app.MapGet("/api/account/external-callback", async (string provider, SignInManager<ResidentUser> signIn, UserManager<ResidentUser> users) =>
{
    var info = await signIn.GetExternalLoginInfoAsync();
    if (info is null) return Results.Redirect($"{publicUrl}/account?auth=failed");

    var existing = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: false);
    if (existing.Succeeded) return Results.Redirect($"{publicUrl}/account?auth=success");

    var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? info.Principal.FindFirstValue("email");
    if (string.IsNullOrWhiteSpace(email)) return Results.Redirect($"{publicUrl}/account?auth=no-email");
    var user = await users.FindByEmailAsync(email);
    if (user is null)
    {
        user = new ResidentUser
        {
            UserName = email.ToLowerInvariant(),
            Email = email.ToLowerInvariant(),
            EmailConfirmed = true,
            DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name)
        };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded) return Results.Redirect($"{publicUrl}/account?auth=failed");
    }
    var linked = await users.AddLoginAsync(user, info);
    if (!linked.Succeeded && !linked.Errors.All(e => e.Code.Contains("LoginAlreadyAssociated", StringComparison.OrdinalIgnoreCase)))
        return Results.Redirect($"{publicUrl}/account?auth=failed");
    await signIn.SignInAsync(user, isPersistent: true);
    return Results.Redirect($"{publicUrl}/account?auth=success");
});

app.MapPut("/api/account/address", async (ResidentAddressUpdate input, ClaimsPrincipal principal, UserManager<ResidentUser> users, CensusGeocoder geocoder, CancellationToken ct) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var validated = await geocoder.ValidateAsync(input.Address, ct);
    if (!validated.Valid || validated.Latitude is null || validated.Longitude is null)
        return Results.BadRequest(new { error = validated.Warning ?? "Address could not be validated." });
    user.HomeAddress = validated.NormalizedAddress ?? input.Address.Trim();
    user.HomeLatitude = validated.Latitude;
    user.HomeLongitude = validated.Longitude;
    await users.UpdateAsync(user);
    return Results.Ok(AccountView(user));
});

app.MapGet("/api/account/civic-profile", async (ClaimsPrincipal principal, UserManager<ResidentUser> users, CivicProfileService civic, CancellationToken ct) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    if (user is null || string.IsNullOrWhiteSpace(user.HomeAddress)) return Results.BadRequest(new { error = "Add and validate a home address first." });
    var profile = await civic.BuildAsync(user.HomeAddress, ct);
    return profile is null ? Results.BadRequest(new { error = "Unable to build a civic profile for this address." }) : Results.Ok(profile);
});

app.MapGet("/api/account/vehicles", async (ClaimsPrincipal principal, UserManager<ResidentUser> users, ResidentDataContext db) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var vehicles = await db.ResidentVehicles.Where(v => v.ResidentUserId == user.Id).OrderBy(v => v.Nickname ?? v.LicensePlate).ToListAsync();
    return Results.Ok(vehicles);
});

app.MapPost("/api/account/vehicles", async (ResidentVehicleInput input, ClaimsPrincipal principal, UserManager<ResidentUser> users, ResidentDataContext db) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var plate = NormalizePlate(input.LicensePlate);
    var state = NormalizeState(input.PlateState);
    if (string.IsNullOrWhiteSpace(plate)) return Results.BadRequest(new { error = "License plate is required." });
    if (await db.ResidentVehicles.AnyAsync(v => v.ResidentUserId == user.Id && v.PlateState == state && v.LicensePlate == plate))
        return Results.BadRequest(new { error = "That license plate is already saved to your account." });
    var vehicle = new ResidentVehicle
    {
        ResidentUserId = user.Id,
        LicensePlate = plate,
        PlateState = state,
        Year = Clean(input.Year, 4),
        Make = Clean(input.Make, 50),
        Model = Clean(input.Model, 50),
        BodyType = Clean(input.BodyType, 40),
        Color = Clean(input.Color, 30),
        Vin = Clean(input.Vin, 17)?.ToUpperInvariant(),
        Nickname = Clean(input.Nickname, 50)
    };
    db.ResidentVehicles.Add(vehicle);
    await db.SaveChangesAsync();
    return Results.Created($"/api/account/vehicles/{vehicle.Id}", vehicle);
});

app.MapPut("/api/account/vehicles/{id:guid}", async (Guid id, ResidentVehicleInput input, ClaimsPrincipal principal, UserManager<ResidentUser> users, ResidentDataContext db) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var vehicle = await db.ResidentVehicles.FirstOrDefaultAsync(v => v.Id == id && v.ResidentUserId == user.Id);
    if (vehicle is null) return Results.NotFound();
    var plate = NormalizePlate(input.LicensePlate);
    var state = NormalizeState(input.PlateState);
    if (await db.ResidentVehicles.AnyAsync(v => v.Id != id && v.ResidentUserId == user.Id && v.PlateState == state && v.LicensePlate == plate))
        return Results.BadRequest(new { error = "That license plate is already saved to your account." });
    vehicle.LicensePlate = plate;
    vehicle.PlateState = state;
    vehicle.Year = Clean(input.Year, 4);
    vehicle.Make = Clean(input.Make, 50);
    vehicle.Model = Clean(input.Model, 50);
    vehicle.BodyType = Clean(input.BodyType, 40);
    vehicle.Color = Clean(input.Color, 30);
    vehicle.Vin = Clean(input.Vin, 17)?.ToUpperInvariant();
    vehicle.Nickname = Clean(input.Nickname, 50);
    await db.SaveChangesAsync();
    return Results.Ok(vehicle);
});

app.MapDelete("/api/account/vehicles/{id:guid}", async (Guid id, ClaimsPrincipal principal, UserManager<ResidentUser> users, ResidentDataContext db) =>
{
    if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var vehicle = await db.ResidentVehicles.FirstOrDefaultAsync(v => v.Id == id && v.ResidentUserId == user.Id);
    if (vehicle is null) return Results.NotFound();
    db.ResidentVehicles.Remove(vehicle);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/services", (string? category, string? q) =>
{
    IEnumerable<ServiceDefinition> services = ServiceCatalog.All;
    if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        services = services.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(q))
        services = services.Where(s => s.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || s.Description.Contains(q, StringComparison.OrdinalIgnoreCase) || s.Category.Contains(q, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(services);
});

app.MapGet("/api/address/validate", async (string address, CensusGeocoder geocoder, CancellationToken ct) => Results.Ok(await geocoder.ValidateAsync(address, ct)));

app.MapPost("/api/requests", async (CreateServiceRequest input, JsonRequestStore store) =>
{
    var service = ServiceCatalog.All.FirstOrDefault(s => s.Id == input.ServiceId);
    if (service is null) return Results.BadRequest(new { error = "Unknown service." });
    var details = input.Details ?? new Dictionary<string, JsonElement>();
    var contact = input.Contact ?? new ContactInfo { Anonymous = service.Id == "crime-tip" };
    if (service.Id == "crime-report")
    {
        if (!details.TryGetValue("emergencyConfirmed", out var gate) || gate.ValueKind != JsonValueKind.True) return Results.BadRequest(new { error = "Confirm that this is not an emergency or crime in progress before continuing." });
        if (!details.TryGetValue("jurisdictionConfirmed", out var jurisdiction) || jurisdiction.ValueKind != JsonValueKind.True) return Results.BadRequest(new { error = "Confirm that the incident location was validated for this demo workflow." });
        if (!details.TryGetValue("incidentType", out var incidentType) || string.IsNullOrWhiteSpace(incidentType.GetString())) return Results.BadRequest(new { error = "Select an eligible incident type." });
        if (!details.TryGetValue("narrative", out var narrative) || string.IsNullOrWhiteSpace(narrative.GetString())) return Results.BadRequest(new { error = "Provide an incident narrative." });
        if (!details.TryGetValue("certified", out var certified) || certified.ValueKind != JsonValueKind.True) return Results.BadRequest(new { error = "Certification is required before submitting the report." });
        if (string.IsNullOrWhiteSpace(contact.Email)) return Results.BadRequest(new { error = "An email address is required for this demo non-emergency report flow." });
    }
    if (service.Id == "crime-tip") contact = contact.WithAnonymousDefault();
    var isCrimeReport = service.Id == "crime-report";
    var request = new ServiceRequest
    {
        TrackingNumber = isCrimeReport ? $"TMP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000, 999999)}" : $"CLTPP-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000, 999999)}",
        ServiceId = service.Id, ServiceTitle = service.Title, Category = service.Category,
        Location = input.Location?.Trim() ?? string.Empty, Details = details, Contact = contact,
        Status = isCrimeReport ? "Pending review" : "Submitted"
    };
    await store.AddAsync(request);
    return Results.Created($"/api/requests/{request.TrackingNumber}", new
    {
        request.TrackingNumber, request.Status, request.CreatedAt,
        reportKind = isCrimeReport ? "temporary" : "service-request",
        disclaimer = isCrimeReport ? "Temporary CLT++ demo report only; not transmitted to CMPD and not an official police report." : "Stored in the CLT++ demo only; not transmitted to CMPD, Charlotte Fire, 311, or Crime Stoppers."
    });
});

app.MapPost("/api/requests/{trackingNumber}/payments/checkout", async (
    string trackingNumber,
    JsonRequestStore store,
    StripePaymentService payments,
    CancellationToken cancellationToken) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    if (request is null) return Results.NotFound(new { error = "Request not found." });
    if (!payments.Enabled) return Results.Problem("Stripe is not configured on this server.", statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        var checkout = await payments.CreateCheckoutAsync(request, cancellationToken);
        return Results.Ok(checkout);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/requests/{trackingNumber}/payments", async (
    string trackingNumber,
    JsonRequestStore store,
    StripePaymentService payments) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    if (request is null) return Results.NotFound(new { error = "Request not found." });
    return Results.Ok(payments.GetSummary(request.ServiceId, request.TrackingNumber));
});

app.MapPost("/api/payments/stripe/webhook", async (HttpRequest request, StripePaymentService payments) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var signature = request.Headers["Stripe-Signature"].ToString();
    if (string.IsNullOrWhiteSpace(signature)) return Results.BadRequest(new { error = "Missing Stripe signature." });

    try
    {
        await payments.ProcessWebhookAsync(body, signature);
        return Results.Ok();
    }
    catch (StripeException)
    {
        return Results.BadRequest(new { error = "Invalid Stripe webhook signature or event." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/requests/{trackingNumber}/supplements", async (string trackingNumber, SupplementalRequest input, JsonRequestStore store) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    if (request is null) return Results.NotFound(new { error = "Report not found." });
    if (request.ServiceId != "crime-report") return Results.BadRequest(new { error = "Supplements are available only for crime reports." });
    if (string.IsNullOrWhiteSpace(input.Narrative)) return Results.BadRequest(new { error = "Supplement narrative is required." });
    var supplement = new JsonElementBuilder().Build(new { submittedAt = DateTimeOffset.UtcNow, narrative = input.Narrative.Trim(), people = input.People ?? "", property = input.Property ?? "", vehicles = input.Vehicles ?? "", evidence = input.Evidence ?? "" });
    await store.AppendDetailAsync(request.TrackingNumber, "supplements", supplement);
    return Results.Ok(new { status = "Supplement received", trackingNumber = request.TrackingNumber });
});

app.MapGet("/api/requests/{trackingNumber}", async (string trackingNumber, JsonRequestStore store, StripePaymentService payments) =>
{
    var request = await store.FindByTrackingAsync(trackingNumber);
    if (request is null) return Results.NotFound(new { error = "Request not found." });
    return Results.Ok(new
    {
        request.Id,
        request.TrackingNumber,
        request.ServiceId,
        request.ServiceTitle,
        request.Category,
        request.Location,
        request.Details,
        request.Contact,
        request.CreatedAt,
        request.Status,
        payment = payments.GetSummary(request.ServiceId, request.TrackingNumber)
    });
});

app.Run();

bool HasConfig(string key1, string key2) => !string.IsNullOrWhiteSpace(builder.Configuration[key1]) && !string.IsNullOrWhiteSpace(builder.Configuration[key2]);
static object AccountView(ResidentUser u) => new { u.Email, u.DisplayName, u.HomeAddress, u.HomeLatitude, u.HomeLongitude };
static string NormalizePlate(string? value) => new string((value ?? "").Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
static string NormalizeState(string? value) => string.IsNullOrWhiteSpace(value) ? "NC" : new string(value.Trim().ToUpperInvariant().Where(char.IsLetter).Take(3).ToArray());
static string? Clean(string? value, int max) { var s = value?.Trim(); return string.IsNullOrWhiteSpace(s) ? null : s[..Math.Min(s.Length, max)]; }
public sealed record LoginRequest(string Email, string Password);
public sealed record SupplementalRequest(string Narrative, string? People, string? Property, string? Vehicles, string? Evidence);

static class ContactExtensions
{
    public static ContactInfo WithAnonymousDefault(this ContactInfo contact) => new()
    {
        Anonymous = contact.Anonymous || (string.IsNullOrWhiteSpace(contact.Name) && string.IsNullOrWhiteSpace(contact.Email) && string.IsNullOrWhiteSpace(contact.Phone)),
        Name = contact.Anonymous ? null : contact.Name, Email = contact.Anonymous ? null : contact.Email,
        Phone = contact.Anonymous ? null : contact.Phone, PreferredMethod = contact.Anonymous ? null : contact.PreferredMethod
    };
}
sealed class JsonElementBuilder
{
    public JsonElement Build<T>(T value) { using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value)); return doc.RootElement.Clone(); }
}
