namespace CltPlusPlus.Api.Models;

public sealed class RentalPropertyRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TrackingNumber { get; set; }
    public required string ResidentUserId { get; set; }
    public Guid? BusinessAccountId { get; set; }
    public required string ApplicantType { get; set; } // Resident or Business
    public required string PropertyAddress { get; set; }
    public string? ParcelNumber { get; set; }
    public string? PropertyName { get; set; }
    public int UnitCount { get; set; } = 1;
    public required string OwnerName { get; set; }
    public required string OwnerMailingAddress { get; set; }
    public required string OwnerEmail { get; set; }
    public required string OwnerPhone { get; set; }
    public string? LocalResponsiblePartyName { get; set; }
    public string? LocalResponsiblePartyEmail { get; set; }
    public string? LocalResponsiblePartyPhone { get; set; }
    public string? PropertyManagerName { get; set; }
    public string? PropertyManagerEmail { get; set; }
    public string? PropertyManagerPhone { get; set; }
    public string RentalStatus { get; set; } = "Active rental";
    public string? Notes { get; set; }
    public string Status { get; set; } = "Submitted";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record RentalPropertyRegistrationInput(
    string ApplicantType,
    string PropertyAddress,
    string? ParcelNumber,
    string? PropertyName,
    int UnitCount,
    string OwnerName,
    string OwnerMailingAddress,
    string OwnerEmail,
    string OwnerPhone,
    string? LocalResponsiblePartyName,
    string? LocalResponsiblePartyEmail,
    string? LocalResponsiblePartyPhone,
    string? PropertyManagerName,
    string? PropertyManagerEmail,
    string? PropertyManagerPhone,
    string? RentalStatus,
    string? Notes);
