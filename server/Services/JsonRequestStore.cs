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
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(all, _json));
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

    private async Task<List<ServiceRequest>> ReadUnsafeAsync()
    {
        if (!File.Exists(_path)) return new();
        var text = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(text)) return new();
        return JsonSerializer.Deserialize<List<ServiceRequest>>(text, _json) ?? new();
    }
}
