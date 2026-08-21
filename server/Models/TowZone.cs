namespace CltPlusPlus.Api.Models;

// Public-safety configuration maintained by Police.
public sealed class TowZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? BoundaryGeoJson { get; set; }
    public Guid DefaultTowCompanyId { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ZoneWreckerCompany
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CompanyName { get; set; }
    public required string ContactName { get; set; }
    public required string ContactEmail { get; set; }
    public required string ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? PermitNumber { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record TowZoneInput(string Name,string? Description,string? BoundaryGeoJson,Guid DefaultTowCompanyId,bool Active=true);
public sealed record ZoneWreckerInput(string CompanyName,string ContactName,string ContactEmail,string ContactPhone,string? Address,string? PermitNumber,bool Active=true);
