using System.Text.Json;
using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

public sealed class CivicProfileService(HttpClient http, CensusGeocoder geocoder)
{
    private const string SchoolAssignmentUrl = "https://www.cmsk12.org/your-schools/find-your-school";

    public async Task<CivicProfile?> BuildAsync(string address, CancellationToken ct = default)
    {
        var validated = await geocoder.ValidateAsync(address, ct);
        if (!validated.Valid || validated.Latitude is null || validated.Longitude is null) return null;

        var lat = validated.Latitude.Value;
        var lon = validated.Longitude.Value;
        var council = await GetCouncilAsync(lat, lon, ct);
        var master = await GetMasterAddressAsync(lat, lon, ct);
        var fire = await GetNearestArcGisPointAsync(
            "https://services.arcgis.com/9Nl857LBlQVyzq54/ArcGIS/rest/services/Current_CFD_Fire_Stations/FeatureServer/0/query",
            lat, lon, "NAME,ADDRESS", "NAME", "ADDRESS", ct);
        var policeOffice = await GetNearestArcGisPointAsync(
            "https://services.arcgis.com/9Nl857LBlQVyzq54/ArcGIS/rest/services/CMPD_Police_Division_Office/FeatureServer/0/query",
            lat, lon, "*", null, null, ct);
        var nearby = await GetOpenStreetMapPlacesAsync(lat, lon, ct);
        var census = await GetCensusDistrictsAsync(address, ct);

        var dmv = DmvLocations.All
            .Select(x => x with { DistanceMiles = Miles(lat, lon, x.Latitude, x.Longitude) })
            .OrderBy(x => x.DistanceMiles)
            .FirstOrDefault();

        var reps = RepresentativeDirectory.Resolve(census.CongressionalDistrict, census.StateHouseDistrict, census.StateSenateDistrict);

        return new CivicProfile(
            validated.NormalizedAddress ?? address,
            council.District,
            council.Representative,
            council.Email,
            master.PoliceDivision,
            policeOffice,
            fire,
            dmv is null ? null : new CivicPlace(dmv.Name, dmv.Address, dmv.Phone, dmv.DistanceMiles, dmv.Website),
            nearby.Schools,
            SchoolAssignmentUrl)
        {
            CongressionalDistrict = census.CongressionalDistrict,
            StateHouseDistrict = census.StateHouseDistrict,
            StateSenateDistrict = census.StateSenateDistrict,
            Representatives = reps,
            PostOffice = nearby.PostOffice,
            Hospitals = nearby.Hospitals,
            EmergencyRoom = nearby.EmergencyRoom
        };
    }

    private async Task<(int? District, string? Representative, string? Email)> GetCouncilAsync(double lat, double lon, CancellationToken ct)
    {
        var url = "https://gis.charlottenc.gov/arcgis/rest/services/PLN/CouncilDistricts/MapServer/0/query" +
                  $"?f=json&geometry={lon},{lat}&geometryType=esriGeometryPoint&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=District,DistrictRep,RepEmail&returnGeometry=false";
        using var doc = await GetJsonAsync(url, ct);
        var attrs = FirstAttributes(doc);
        if (attrs is null) return (null, null, null);
        int? district = attrs.Value.TryGetProperty("District", out var d) && int.TryParse(d.GetString(), out var n) ? n : null;
        return (district, GetString(attrs.Value, "DistrictRep"), GetString(attrs.Value, "RepEmail"));
    }

    private async Task<(string? PoliceDivision, int? CouncilDistrict)> GetMasterAddressAsync(double lat, double lon, CancellationToken ct)
    {
        var url = "https://gis.charlottenc.gov/arcgis/rest/services/Accela/Accela/MapServer/1/query" +
                  $"?f=json&geometry={lon},{lat}&geometryType=esriGeometryPoint&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=POLICE_DIVISION,COUNCIL_DISTRICT&returnGeometry=false";
        using var doc = await GetJsonAsync(url, ct);
        var attrs = FirstAttributes(doc);
        if (attrs is null) return (null, null);
        int? council = attrs.Value.TryGetProperty("COUNCIL_DISTRICT", out var c) && c.TryGetInt32(out var n) ? n : null;
        return (GetString(attrs.Value, "POLICE_DIVISION"), council);
    }

    private async Task<CivicPlace?> GetNearestArcGisPointAsync(string endpoint, double lat, double lon, string fields, string? nameField, string? addressField, CancellationToken ct)
    {
        var url = endpoint + $"?f=json&where=1%3D1&outFields={Uri.EscapeDataString(fields)}&returnGeometry=true&outSR=4326";
        using var doc = await GetJsonAsync(url, ct);
        if (!doc.RootElement.TryGetProperty("features", out var features)) return null;
        CivicPlace? best = null;
        foreach (var f in features.EnumerateArray())
        {
            if (!f.TryGetProperty("geometry", out var g) || !g.TryGetProperty("x", out var x) || !g.TryGetProperty("y", out var y)) continue;
            var attrs = f.GetProperty("attributes");
            var name = nameField is not null ? GetString(attrs, nameField) : Guess(attrs, "NAME", "DIVISION", "OFFICE", "FACILITY") ?? "Public safety office";
            var address = addressField is not null ? GetString(attrs, addressField) : Guess(attrs, "ADDRESS", "FULL_ADDRESS", "LOCATION", "STREET") ?? string.Empty;
            var phone = Guess(attrs, "PHONE", "TELEPHONE");
            var dist = Miles(lat, lon, y.GetDouble(), x.GetDouble());
            var place = new CivicPlace(name ?? "Location", address ?? string.Empty, phone, dist);
            if (best is null || dist < best.DistanceMiles) best = place;
        }
        return best;
    }

    private async Task<NearbyResults> GetOpenStreetMapPlacesAsync(double lat, double lon, CancellationToken ct)
    {
        var q = $"[out:json][timeout:12];(nwr(around:16000,{lat},{lon})[amenity=post_office];nwr(around:16000,{lat},{lon})[amenity=school];nwr(around:20000,{lat},{lon})[amenity=hospital];nwr(around:20000,{lat},{lon})[healthcare=hospital];nwr(around:20000,{lat},{lon})[emergency=yes];);out center tags;";
        var url = "https://overpass-api.de/api/interpreter?data=" + Uri.EscapeDataString(q);
        using var doc = await GetJsonAsync(url, ct);
        var places = new List<(string Kind, CivicPlace Place, bool Emergency)>();
        if (doc.RootElement.TryGetProperty("elements", out var elements))
        {
            foreach (var e in elements.EnumerateArray())
            {
                if (!TryCoords(e, out var pLat, out var pLon) || !e.TryGetProperty("tags", out var tags)) continue;
                var amenity = GetString(tags, "amenity") ?? GetString(tags, "healthcare") ?? string.Empty;
                var name = GetString(tags, "name") ?? amenity.Replace('_', ' ');
                var addr = FormatOsmAddress(tags);
                var phone = GetString(tags, "phone") ?? GetString(tags, "contact:phone");
                var website = GetString(tags, "website") ?? GetString(tags, "contact:website");
                var emergency = string.Equals(GetString(tags, "emergency"), "yes", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(GetString(tags, "emergency"), "24_7", StringComparison.OrdinalIgnoreCase) ||
                                (name?.Contains("Emergency", StringComparison.OrdinalIgnoreCase) ?? false);
                places.Add((amenity, new CivicPlace(name ?? "Location", addr, phone, Miles(lat, lon, pLat, pLon), website), emergency));
            }
        }

        var post = places.Where(x => x.Kind == "post_office").OrderBy(x => x.Place.DistanceMiles).Select(x => x.Place).FirstOrDefault();
        var schools = places.Where(x => x.Kind == "school").OrderBy(x => x.Place.DistanceMiles).Select(x => x.Place).Take(6).ToList();
        var hospitals = places.Where(x => x.Kind == "hospital").GroupBy(x => x.Place.Name).Select(g => g.First().Place).OrderBy(x => x.DistanceMiles).Take(6).ToList();
        var er = places.Where(x => x.Emergency).OrderBy(x => x.Place.DistanceMiles).Select(x => x.Place).FirstOrDefault();
        return new NearbyResults(post, schools, hospitals, er);
    }

    private async Task<DistrictResult> GetCensusDistrictsAsync(string address, CancellationToken ct)
    {
        var url = "https://geocoding.geo.census.gov/geocoder/geographies/onelineaddress" +
                  $"?address={Uri.EscapeDataString(address)}&benchmark=Public_AR_Current&vintage=Current_Current&format=json";
        using var doc = await GetJsonAsync(url, ct);
        var result = doc.RootElement.GetProperty("result");
        var matches = result.GetProperty("addressMatches");
        if (matches.GetArrayLength() == 0) return new(null, null, null);
        var geo = matches[0].GetProperty("geographies");
        return new(
            FindDistrict(geo, "Congressional District"),
            FindDistrict(geo, "State Legislative Districts - Lower"),
            FindDistrict(geo, "State Legislative Districts - Upper"));
    }

    private static int? FindDistrict(JsonElement geographies, string keyStartsWith)
    {
        foreach (var prop in geographies.EnumerateObject())
        {
            if (!prop.Name.StartsWith(keyStartsWith, StringComparison.OrdinalIgnoreCase) || prop.Value.GetArrayLength() == 0) continue;
            var item = prop.Value[0];
            foreach (var field in new[] { "BASENAME", "CD119", "SLDLST", "SLDUST", "NAME" })
            {
                if (!item.TryGetProperty(field, out var v)) continue;
                var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
                var digits = new string((s ?? "").Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n)) return n;
            }
        }
        return null;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static JsonElement? FirstAttributes(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("features", out var fs) || fs.GetArrayLength() == 0) return null;
        return fs[0].GetProperty("attributes");
    }

    private static string? GetString(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
    private static string? Guess(JsonElement attrs, params string[] keys)
    {
        foreach (var key in keys)
            foreach (var p in attrs.EnumerateObject())
                if (p.Name.Contains(key, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind != JsonValueKind.Null && !string.IsNullOrWhiteSpace(p.Value.ToString())) return p.Value.ToString();
        return null;
    }
    private static bool TryCoords(JsonElement e, out double lat, out double lon)
    {
        lat = lon = 0;
        if (e.TryGetProperty("lat", out var la) && e.TryGetProperty("lon", out var lo)) { lat = la.GetDouble(); lon = lo.GetDouble(); return true; }
        if (e.TryGetProperty("center", out var c) && c.TryGetProperty("lat", out la) && c.TryGetProperty("lon", out lo)) { lat = la.GetDouble(); lon = lo.GetDouble(); return true; }
        return false;
    }
    private static string FormatOsmAddress(JsonElement tags)
    {
        var parts = new[] { GetString(tags,"addr:housenumber"), GetString(tags,"addr:street"), GetString(tags,"addr:city"), GetString(tags,"addr:state"), GetString(tags,"addr:postcode") }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" ", parts!);
    }
    private static double Miles(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 3958.7613;
        var dLat = (lat2-lat1)*Math.PI/180; var dLon=(lon2-lon1)*Math.PI/180;
        var a=Math.Sin(dLat/2)*Math.Sin(dLat/2)+Math.Cos(lat1*Math.PI/180)*Math.Cos(lat2*Math.PI/180)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return Math.Round(r*2*Math.Atan2(Math.Sqrt(a),Math.Sqrt(1-a)),2);
    }

    private sealed record NearbyResults(CivicPlace? PostOffice, IReadOnlyList<CivicPlace> Schools, IReadOnlyList<CivicPlace> Hospitals, CivicPlace? EmergencyRoom);
    private sealed record DistrictResult(int? CongressionalDistrict, int? StateHouseDistrict, int? StateSenateDistrict);
}

internal static class DmvLocations
{
    public static readonly DmvLocation[] All =
    {
        new("NCDMV Charlotte North Driver License Office", "9711 David Taylor Dr, Charlotte, NC", null, 35.3387, -80.7608, "https://www.ncdot.gov/dmv/"),
        new("NCDMV Charlotte South Driver License Office", "201 W Arrowood Rd, Suite H, Charlotte, NC", null, 35.1366, -80.8934, "https://www.ncdot.gov/dmv/"),
        new("East Charlotte License Plate Agency", "5309-E E Independence Blvd, Charlotte, NC 28212", "704-900-5727", 35.1870, -80.7565, "https://www.ncdot.gov/dmv/"),
        new("West Charlotte License Plate Agency", "3250-G Wilkinson Blvd, Charlotte, NC 28208", "980-237-9658", 35.2248, -80.8930, "https://www.ncdot.gov/dmv/"),
        new("South Charlotte License Plate Agency", "809 E Arrowood Rd, Suite 800, Charlotte, NC 28217", "704-525-3832", 35.1350, -80.8785, "https://www.ncdot.gov/dmv/")
    };
}
internal sealed record DmvLocation(string Name,string Address,string? Phone,double Latitude,double Longitude,string Website,double? DistanceMiles=null);

internal static class RepresentativeDirectory
{
    private static readonly Dictionary<int,string> Congress = new() { [8]="Mark Harris", [12]="Alma S. Adams", [14]="Tim Moore" };
    private static readonly Dictionary<int,string> House = new() { [88]="Mary Belk",[92]="Terry M. Brown Jr.",[98]="Beth Helfrich",[99]="Nasif Majeed",[100]="Julia Greenfield",[101]="Carolyn G. Logan",[102]="Becky Carney",[103]="Laura Budd",[104]="Brandon Lofton",[105]="Tricia Ann Cotham",[106]="Carla D. Cunningham",[107]="Aisha O. Dew",[112]="Jordan Lopez" };
    private static readonly Dictionary<int,string> Senate = new() { [37]="Vickie Sawyer",[38]="Mujtaba A. Mohammed",[39]="DeAndrea Salvador",[40]="Joyce Waddell",[41]="Caleb Theodros",[42]="Woodson Bradley" };

    public static IReadOnlyList<RepresentativeInfo> Resolve(int? cd, int? hd, int? sd)
    {
        var list = new List<RepresentativeInfo>();
        if (cd is int c && Congress.TryGetValue(c,out var cn)) list.Add(new("U.S. House",c,cn,"https://www.house.gov/representatives/find-your-representative","202-224-3121"));
        if (hd is int h && House.TryGetValue(h,out var hn)) list.Add(new("North Carolina House",h,hn,"https://www.ncleg.gov/Members/MemberList/H/District",null));
        if (sd is int s && Senate.TryGetValue(s,out var sn)) list.Add(new("North Carolina Senate",s,sn,"https://www.ncleg.gov/Members/MemberList/S/District",null));
        list.Add(new("U.S. Senate",null,"Thom Tillis","https://www.tillis.senate.gov/","202-224-6342"));
        list.Add(new("U.S. Senate",null,"Ted Budd","https://www.budd.senate.gov/","202-224-3154"));
        return list;
    }
}
