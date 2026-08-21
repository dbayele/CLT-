namespace CltPlusPlus.Api.Models;

public sealed class ResidentVehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ResidentUserId { get; set; }
    public required string LicensePlate { get; set; }
    public string PlateState { get; set; } = "NC";
    public string? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? BodyType { get; set; }
    public string? Color { get; set; }
    public string? Vin { get; set; }
    public string? Nickname { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record ResidentVehicleInput(
    string LicensePlate,
    string? PlateState,
    string? Year,
    string? Make,
    string? Model,
    string? BodyType,
    string? Color,
    string? Vin,
    string? Nickname);
