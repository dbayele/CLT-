namespace CltPlusPlus.Api.Models;

public sealed record ServiceDefinition(
    string Id,
    string Category,
    string Title,
    string Description,
    string Icon,
    bool IsPolice = false,
    string? Badge = null);
