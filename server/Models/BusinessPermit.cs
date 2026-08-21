namespace CltPlusPlus.Api.Models;

public sealed class BusinessPermitApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessAccountId { get; set; }
    public required string ResidentUserId { get; set; }
    public required string TrackingNumber { get; set; }
    public required string PermitTypeId { get; set; }
    public required string PermitTitle { get; set; }
    public required string Department { get; set; }
    public required string ProjectAddress { get; set; }
    public string? ParcelNumber { get; set; }
    public string? ProjectName { get; set; }
    public string? ApplicantName { get; set; }
    public string? ApplicantPhone { get; set; }
    public string? ApplicantEmail { get; set; }
    public string? ContractorName { get; set; }
    public string? ContractorAccountNumber { get; set; }
    public string? Description { get; set; }
    public string? EstimatedProjectValue { get; set; }
    public string? OfficialApplicationUrl { get; set; }
    public bool RequiresExternalSubmission { get; set; }
    public string Status { get; set; } = "Draft captured in CLT++";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record BusinessPermitInput(string PermitTypeId,string ProjectAddress,string? ParcelNumber,string? ProjectName,string? ApplicantName,string? ApplicantPhone,string? ApplicantEmail,string? ContractorName,string? ContractorAccountNumber,string? Description,string? EstimatedProjectValue);
public sealed record PermitDefinition(string Id,string Category,string Department,string Title,string Description,bool RequiresExternalSubmission,string? OfficialApplicationUrl=null);
