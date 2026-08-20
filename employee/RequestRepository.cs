using System.Text.Json;

namespace CltPlusPlus.Employee;

public sealed class RequestRepository
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1,1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public RequestRepository(IConfiguration configuration)
    {
        _path = configuration["CLTPP_DATA_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "data", "requests.json");
    }

    public async Task<IReadOnlyList<ServiceRequestRecord>> ListAsync()
    {
        await _gate.WaitAsync();
        try { return (await ReadUnsafeAsync()).OrderByDescending(x => x.CreatedAt).ToArray(); }
        finally { _gate.Release(); }
    }

    public async Task<ServiceRequestRecord?> FindAsync(string trackingNumber)
    {
        await _gate.WaitAsync();
        try { return (await ReadUnsafeAsync()).FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase)); }
        finally { _gate.Release(); }
    }

    public async Task<ServiceRequestRecord?> UpdateAsync(string trackingNumber, Func<ServiceRequestRecord,bool> authorize, Action<ServiceRequestRecord> update)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnsafeAsync();
            var request = all.FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
            if (request is null || !authorize(request)) return null;
            update(request);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(all, _json));
            return request;
        }
        finally { _gate.Release(); }
    }

    private async Task<List<ServiceRequestRecord>> ReadUnsafeAsync()
    {
        if (!File.Exists(_path)) return new();
        var text = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(text)) return new();
        return JsonSerializer.Deserialize<List<ServiceRequestRecord>>(text, _json) ?? new();
    }
}
