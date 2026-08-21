using System.Text.Json;
using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

public static class BusinessShopliftingService
{
    public const string ServiceId = "business-shoplifting";

    public static ServiceDefinition Definition => new(
        ServiceId,
        "Police",
        "Business Shoplifting Report",
        "Business-only guided report for a completed, non-emergency shoplifting incident. Captures store, reporting employee, suspect, merchandise, evidence, recovery, detention, and loss-prevention details for police review.",
        "store",
        true,
        "Business account required");

    public static string? Validate(IReadOnlyDictionary<string, JsonElement> details, ContactInfo contact)
    {
        if (!True(details, "emergencyConfirmed")) return "Confirm that the incident is over and no immediate police response is required. Call 911 for an active theft, violence, weapons, or immediate danger.";
        if (!Text(details, "businessName")) return "Business name is required.";
        if (!Text(details, "storeAddress")) return "Store/location address is required.";
        if (!Text(details, "incidentDateTime")) return "Incident date and time are required.";
        if (!Text(details, "reportingEmployeeName")) return "Reporting employee or loss-prevention contact is required.";
        if (!Text(details, "reportingEmployeeRole")) return "Reporting employee role is required.";
        if (!Text(details, "suspectDescription")) return "Provide a suspect description.";
        if (!Text(details, "merchandiseDescription")) return "Describe the merchandise involved.";
        if (!Text(details, "narrative")) return "Provide an incident narrative.";
        if (!True(details, "certified")) return "Certification is required before submitting the report.";
        if (string.IsNullOrWhiteSpace(contact.Email) && string.IsNullOrWhiteSpace(contact.Phone)) return "A business contact email or phone number is required.";
        return null;
    }

    private static bool True(IReadOnlyDictionary<string, JsonElement> d, string key) => d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.True;
    private static bool Text(IReadOnlyDictionary<string, JsonElement> d, string key) => d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());
}
