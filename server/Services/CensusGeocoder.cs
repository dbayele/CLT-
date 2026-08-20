using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CltPlusPlus.Api.Services;

public sealed class CensusGeocoder(HttpClient http)
{
    public async Task<AddressValidationResult> ValidateAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return new(false, null, null, null, "Enter an address to validate.");

        var url = "https://geocoding.geo.census.gov/geocoder/geographies/onelineaddress" +
                  $"?address={Uri.EscapeDataString(address.Trim())}&benchmark=Public_AR_Current&vintage=Current_Current&format=json";

        try
        {
            var response = await http.GetFromJsonAsync<CensusResponse>(url, cancellationToken);
            var match = response?.Result?.AddressMatches?.FirstOrDefault();
            if (match is null)
                return new(false, null, null, null, "No Census address match was found. Check the street number, street name, city, and state.");

            var geographies = match.Geographies ?? new();
            var place = geographies.Values.SelectMany(x => x).FirstOrDefault(x =>
                string.Equals(x.Name, "Charlotte city", StringComparison.OrdinalIgnoreCase));
            var county = geographies.Values.SelectMany(x => x).FirstOrDefault(x =>
                x.Name?.Contains("Mecklenburg", StringComparison.OrdinalIgnoreCase) == true);

            var inCharlotteMecklenburg = place is not null || county is not null ||
                match.MatchedAddress?.Contains("CHARLOTTE, NC", StringComparison.OrdinalIgnoreCase) == true;

            return new(
                true,
                match.MatchedAddress,
                match.Coordinates?.Y,
                match.Coordinates?.X,
                inCharlotteMecklenburg ? null : "The address matched, but it may be outside Charlotte/Mecklenburg. Agency jurisdiction should be confirmed before routing a public-safety request.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, null, null, null, "Address validation is temporarily unavailable. You can keep the typed address and try again.");
        }
    }

    private sealed class CensusResponse
    {
        [JsonPropertyName("result")] public CensusResult? Result { get; set; }
    }

    private sealed class CensusResult
    {
        [JsonPropertyName("addressMatches")] public List<CensusMatch>? AddressMatches { get; set; }
    }

    private sealed class CensusMatch
    {
        [JsonPropertyName("matchedAddress")] public string? MatchedAddress { get; set; }
        [JsonPropertyName("coordinates")] public CensusCoordinates? Coordinates { get; set; }
        [JsonPropertyName("geographies")] public Dictionary<string, List<CensusGeography>>? Geographies { get; set; }
    }

    private sealed class CensusCoordinates
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
    }

    private sealed class CensusGeography
    {
        [JsonPropertyName("NAME")] public string? Name { get; set; }
    }
}

public sealed record AddressValidationResult(
    bool Valid,
    string? NormalizedAddress,
    double? Latitude,
    double? Longitude,
    string? Warning);
