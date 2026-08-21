using System.Text.Json;
using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

// Transitional file store split along the same security boundary as the SQL stores.
// Police/Fire records are persisted to CLTPP_PUBLIC_SAFETY_REQUESTS_PATH; all other
// service requests go to CLTPP_CIVIC_REQUESTS_PATH. The API searches both stores by
// tracking number so callers do not need to know which database owns the record.
public sealed class JsonRequestStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _civicPath;
    private readonly string _publicSafetyPath;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonRequestStore(IConfiguration configuration)
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        _civicPath = configuration["CLTPP_CIVIC_REQUESTS_PATH"] ?? Path.Combine(dataDir, "civic-requests.json");
        _publicSafetyPath = configuration["CLTPP_PUBLIC_SAFETY_REQUESTS_PATH"] ?? Path.Combine(dataDir, "public-safety-requests.json");
    }

    public async Task<ServiceRequest> AddAsync(ServiceRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var path = IsPublicSafety(request) ? _publicSafetyPath : _civicPath;
            var all = await ReadUnsafeAsync(path);
            all.Add(request);
            await WriteUnsafeAsync(path, all);
            return request;
        }
        finally { _gate.Release(); }
    }

    public async Task<ServiceRequest?> FindByTrackingAsync(string trackingNumber)
    {
        await _gate.WaitAsync();
        try
        {
            var ps = (await ReadUnsafeAsync(_publicSafetyPath)).FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
            if (ps is not null) return ps;
            return (await ReadUnsafeAsync(_civicPath)).FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
        }
        finally { _gate.Release(); }
    }

    public async Task AppendDetailAsync(string trackingNumber, string key, JsonElement value)
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var path in new[] { _publicSafetyPath, _civicPath })
            {
                var all = await ReadUnsafeAsync(path);
                var request = all.FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
                if (request is null) continue;

                if (request.Details.TryGetValue(key, out var existing) && existing.ValueKind == JsonValueKind.Array)
                {
                    var items = existing.EnumerateArray().Select(x => x.Clone()).ToList();
                    items.Add(value.Clone());
                    request.Details[key] = ToElement(items);
                }
                else
                {
                    request.Details[key] = ToElement(new[] { value.Clone() });
                }

                await WriteUnsafeAsync(path, all);
                return;
            }
        }
        finally { _gate.Release(); }
    }

    private static bool IsPublicSafety(ServiceRequest request) =>
        request.Category.Equals("Police", StringComparison.OrdinalIgnoreCase) ||
        request.Category.Equals("Fire", StringComparison.OrdinalIgnoreCase);

    private async Task<List<ServiceRequest>> ReadUnsafeAsync(string path)
    {
        if (!File.Exists(path)) return new();
        var text = await File.ReadAllTextAsync(path);
        if (string.IsNullOrWhiteSpace(text)) return new();
        return JsonSerializer.Deserialize<List<ServiceRequest>>(text, _json) ?? new();
    }

    private async Task WriteUnsafeAsync(string path, List<ServiceRequest> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(all, _json));
    }

    private JsonElement ToElement<T>(T value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, _json));
        return doc.RootElement.Clone();
    }
}
