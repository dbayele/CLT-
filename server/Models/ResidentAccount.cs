using Microsoft.AspNetCore.Identity;

namespace CltPlusPlus.Api.Models;

public sealed class ResidentUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? HomeAddress { get; set; }
    public double? HomeLatitude { get; set; }
    public double? HomeLongitude { get; set; }
}

public sealed record RegisterResident(string Email, string Password, string? DisplayName);
public sealed record ResidentAddressUpdate(string Address);

public sealed record CivicProfile(
    string Address,
    int? CouncilDistrict,
    string? CouncilMember,
    string? CouncilEmail,
    string? PoliceDivision,
    CivicPlace? PoliceDivisionOffice,
    CivicPlace? FireStation,
    CivicPlace? DmvOffice,
    IReadOnlyList<CivicPlace> NearbySchools,
    string SchoolAssignmentUrl)
{
    public int? CongressionalDistrict { get; init; }
    public int? StateHouseDistrict { get; init; }
    public int? StateSenateDistrict { get; init; }
    public IReadOnlyList<RepresentativeInfo> Representatives { get; init; } = Array.Empty<RepresentativeInfo>();
    public CivicPlace? PostOffice { get; init; }
    public IReadOnlyList<CivicPlace> Hospitals { get; init; } = Array.Empty<CivicPlace>();
    public CivicPlace? EmergencyRoom { get; init; }
}

public sealed record CivicPlace(string Name, string Address, string? Phone, double? DistanceMiles, string? Website = null);
public sealed record RepresentativeInfo(string Chamber, int? District, string Name, string Website, string? Phone);
