using System.Text.Json;

namespace CltPlusPlus.Api.Models;

public sealed class ServiceRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string TrackingNumber { get; init; }
    public required string ServiceId { get; init; }
    public required string ServiceTitle { get; init; }
    public required string Category { get; init; }
    public required string Location { get; init; }
    public required Dictionary<string, JsonElement> Details { get; init; }
    public required ContactInfo Contact { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Submitted";
}

public sealed class ContactInfo
{
    public bool Anonymous { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? PreferredMethod { get; init; }
}

public sealed class CreateServiceRequest
{
    public required string ServiceId { get; init; }
    public string? Location { get; init; }
    public Dictionary<string, JsonElement>? Details { get; init; }
    public ContactInfo? Contact { get; init; }
}
