using System.Text.Json;
using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

public sealed class JsonRequestStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonRequestStore(IConfiguration configuration)
    {
        _path = configuration["CLTPP_DATA_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "data", "requests.json");
    }

    public async Task<ServiceRequest> AddAsync(ServiceRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnsafeAsync();
            all.Add(request);
            await WriteUnsafeAsync(all);
            return request;
        }
        finally { _gate.Release(); }
    }

    public async Task<ServiceRequest?> FindByTrackingAsync(string trackingNumber)
    {
        await _gate.WaitAsync();
        try { return (await ReadUnsafeAsync()).FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase)); }
        finally { _gate.Release(); }
    }

    public async Task AppendDetailAsync(string trackingNumber, string key, JsonElement value)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnsafeAsync();
            var request = all.FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
            if (request is null) return;

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

            await WriteUnsafeAsync(all);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<ServiceRequest>> ReadUnsafeAsync()
    {
        if (!File.Exists(_path)) return new();
        var text = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(text)) return new();
        return JsonSerializer.Deserialize<List<ServiceRequest>>(text, _json) ?? new();
    }

    private async Task WriteUnsafeAsync(List<ServiceRequest> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(all, _json));
    }

    private JsonElement ToElement<T>(T value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, _json));
        return doc.RootElement.Clone();
    }
}
