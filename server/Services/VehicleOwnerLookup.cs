namespace CltPlusPlus.Api.Services;

public interface IVehicleOwnerLookup
{
    Task<VehicleOwnerLookupResult> LookupAsync(string vin, string plateState, string licensePlate, string purpose, CancellationToken cancellationToken = default);
}

public sealed record VehicleOwnerLookupResult(bool Available, string? OwnerName, string? OwnerAddress, string? Source, string? Message);

/// <summary>
/// Safe default. NCDMV owner name/address data is DPPA-protected and requires an authorized data source,
/// credentials, permissible purpose and auditing. Replace this implementation only with an approved provider.
/// </summary>
public sealed class UnavailableVehicleOwnerLookup : IVehicleOwnerLookup
{
    public Task<VehicleOwnerLookupResult> LookupAsync(string vin, string plateState, string licensePlate, string purpose, CancellationToken cancellationToken = default)
        => Task.FromResult(new VehicleOwnerLookupResult(
            false,
            null,
            null,
            null,
            "Automatic owner lookup is not configured. Connect an authorized NCDMV/DPPA-compliant provider before retrieving owner name or address."));
}
