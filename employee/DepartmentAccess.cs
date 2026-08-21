using System.Security.Claims;

namespace CltPlusPlus.Employee;

public static class DepartmentAccess
{
    public const string ClaimType = "department";

    private static readonly Dictionary<string,string> CategoryToDepartment = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Police"] = "Police",
        ["Fire"] = "Fire",
        ["Streets & Transportation"] = "Transportation",
        ["Solid Waste"] = "Solid Waste",
        ["Neighborhoods"] = "Housing & Neighborhood Services",
        ["Trees & Environment"] = "General Services",
        ["Water"] = "Charlotte Water",
        ["Animals"] = "Animal Care & Control"
    };

    public static string For(ServiceRequestRecord request) =>
        request.Processing?.AssignedDepartment
        ?? (CategoryToDepartment.TryGetValue(request.Category, out var department) ? department : request.Category);

    public static bool CanAccess(ClaimsPrincipal user, ServiceRequestRecord request)
    {
        if (user.IsInRole("Administrator") || user.IsInRole("Supervisor")) return true;
        var department = For(request);
        return user.FindAll(ClaimType).Any(c => string.Equals(c.Value, department, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> UserDepartments(ClaimsPrincipal user) =>
        user.FindAll(ClaimType).Select(c => c.Value).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
}
