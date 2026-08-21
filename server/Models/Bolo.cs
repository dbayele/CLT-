namespace CltPlusPlus.Api.Models;

public sealed class BoloRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TrackingNumber { get; set; }
    public required string BoloType { get; set; } // person | vehicle | person-vehicle | other
    public required string Summary { get; set; }
    public string? Details { get; set; }
    public string? SubjectName { get; set; }
    public string? SubjectDescription { get; set; }
    public string? VehicleDescription { get; set; }
    public string? LicensePlate { get; set; }
    public string? PlateState { get; set; }
    public string? Vin { get; set; }
    public string? LastKnownLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Reason { get; set; }
    public string Priority { get; set; } = "Routine";
    public required string SubmittedByEmployee { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RecalledByEmployee { get; set; }
    public string? RecallReason { get; set; }
    public DateTimeOffset? RecalledAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record BoloInput(
    string BoloType,
    string Summary,
    string? Details,
    string? SubjectName,
    string? SubjectDescription,
    string? VehicleDescription,
    string? LicensePlate,
    string? PlateState,
    string? Vin,
    string? LastKnownLocation,
    double? Latitude,
    double? Longitude,
    string? Reason,
    string? Priority,
    DateTimeOffset? ExpiresAt);

public sealed record RecallBoloInput(string Reason);
