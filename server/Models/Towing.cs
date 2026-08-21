namespace CltPlusPlus.Api.Models;

public sealed class BusinessAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ResidentUserId { get; set; }
    public required string LegalName { get; set; }
    public string? DbaName { get; set; }
    public required string ContactName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public required string Address { get; set; }
    public string? TowingPermitNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TowingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TrackingNumber { get; set; }
    public Guid BusinessAccountId { get; set; }
    public required string ResidentUserId { get; set; }
    public required string RequestType { get; set; } // property-trespass | repossession
    public required string TowingCompanyName { get; set; }
    public required string TowingCompanyContactName { get; set; }
    public required string TowingCompanyPhone { get; set; }
    public required string TowingCompanyEmail { get; set; }
    public required string TowingCompanyAddress { get; set; }
    public string? TowingPermitNumber { get; set; }
    public required string TowFromAddress { get; set; }
    public required string StorageAddress { get; set; }
    public string? PropertyOwnerOrLienholder { get; set; }
    public string? AuthorizationReference { get; set; }
    public required string LicensePlate { get; set; }
    public string PlateState { get; set; } = "NC";
    public required string Vin { get; set; }
    public string? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? BodyType { get; set; }
    public string? Color { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerAddress { get; set; }
    public string? OwnerDataSource { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record BusinessAccountInput(string LegalName,string? DbaName,string ContactName,string Email,string Phone,string Address,string? TowingPermitNumber);
public sealed record TowingRequestInput(
    string RequestType,
    string TowingCompanyName,
    string TowingCompanyContactName,
    string TowingCompanyPhone,
    string TowingCompanyEmail,
    string TowingCompanyAddress,
    string? TowingPermitNumber,
    string TowFromAddress,
    string StorageAddress,
    string? PropertyOwnerOrLienholder,
    string? AuthorizationReference,
    string LicensePlate,
    string? PlateState,
    string Vin,
    string? Year,
    string? Make,
    string? Model,
    string? BodyType,
    string? Color,
    bool DppaPurposeCertified);
