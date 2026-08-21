using System.Text.Json;

namespace CltPlusPlus.Employee;

public sealed class ServiceRequestRecord
{
    public Guid Id { get; set; }
    public string TrackingNumber { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string ServiceTitle { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public Dictionary<string, JsonElement> Details { get; set; } = new();
    public ContactRecord Contact { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public string Status { get; set; } = "Submitted";
    public ProcessingRecord? Processing { get; set; }
}

public sealed class ContactRecord
{
    public bool Anonymous { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PreferredMethod { get; set; }
}

public sealed class ProcessingRecord
{
    public string? AssignedTo { get; set; }
    public string? AssignedDepartment { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<InternalNote> InternalNotes { get; set; } = new();
}

public sealed class InternalNote
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed record AppUser(string Username, string Password, string Role, IReadOnlyList<string> Departments);
