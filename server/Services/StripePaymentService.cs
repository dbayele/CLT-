using System.Text.Json;
using CltPlusPlus.Api.Models;
using Stripe;
using Stripe.Checkout;

namespace CltPlusPlus.Api.Services;

public sealed class StripePaymentService
{
    private readonly StripeClient? _client;
    private readonly string? _webhookSecret;
    private readonly string _publicUrl;
    private readonly string _path;
    private readonly Dictionary<string, long> _fees;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public StripePaymentService(IConfiguration configuration)
    {
        var secretKey = configuration["STRIPE_SECRET_KEY"];
        _webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"];
        _publicUrl = (configuration["CLTPP_PUBLIC_URL"] ?? "http://localhost:5173").TrimEnd('/');
        _path = configuration["CLTPP_PAYMENT_DATA_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "data", "payments.json");
        _client = string.IsNullOrWhiteSpace(secretKey) ? null : new StripeClient(secretKey);
        _fees = ParseFees(configuration["CLTPP_STRIPE_FEES"]);
    }

    public bool Enabled => _client is not null;

    public PaymentSummary GetSummary(string serviceId, string trackingNumber)
    {
        var amount = _fees.GetValueOrDefault(serviceId);
        if (amount <= 0) return new(false, 0, "usd", "Not required", null);
        var existing = ReadAllAsync().GetAwaiter().GetResult().FirstOrDefault(x => x.TrackingNumber.Equals(trackingNumber, StringComparison.OrdinalIgnoreCase));
        return new(true, amount, "usd", existing?.Status ?? "Not started", existing?.StripeSessionId);
    }

    public async Task<CheckoutResult> CreateCheckoutAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        if (_client is null) throw new InvalidOperationException("Stripe is not configured.");
        if (!_fees.TryGetValue(request.ServiceId, out var amount) || amount <= 0)
            throw new InvalidOperationException("No payment is configured for this service.");

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = request.TrackingNumber,
            CustomerEmail = string.IsNullOrWhiteSpace(request.Contact.Email) ? null : request.Contact.Email,
            SuccessUrl = $"{_publicUrl}/?payment=success&tracking={Uri.EscapeDataString(request.TrackingNumber)}",
            CancelUrl = $"{_publicUrl}/?payment=cancelled&tracking={Uri.EscapeDataString(request.TrackingNumber)}",
            Metadata = new Dictionary<string, string>
            {
                ["trackingNumber"] = request.TrackingNumber,
                ["serviceId"] = request.ServiceId
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = amount,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.ServiceTitle,
                            Description = $"CLT++ request {request.TrackingNumber}"
                        }
                    }
                }
            }
        };

        var service = new SessionService(_client);
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        await UpsertAsync(new PaymentRecord
        {
            TrackingNumber = request.TrackingNumber,
            ServiceId = request.ServiceId,
            Amount = amount,
            Currency = "usd",
            StripeSessionId = session.Id,
            Status = "Pending",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        return new(session.Id, session.Url!, amount, "usd", "Pending");
    }

    public async Task ProcessWebhookAsync(string body, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret)) throw new InvalidOperationException("Stripe webhook secret is not configured.");
        var stripeEvent = EventUtility.ConstructEvent(body, signatureHeader, _webhookSecret);
        if (stripeEvent.Data.Object is not Session session) return;

        var tracking = session.Metadata?.GetValueOrDefault("trackingNumber") ?? session.ClientReferenceId;
        if (string.IsNullOrWhiteSpace(tracking)) return;

        var status = stripeEvent.Type switch
        {
            "checkout.session.completed" when session.PaymentStatus == "paid" => "Paid",
            "checkout.session.async_payment_succeeded" => "Paid",
            "checkout.session.async_payment_failed" => "Failed",
            "checkout.session.expired" => "Expired",
            _ => null
        };
        if (status is null) return;

        var all = await ReadAllAsync();
        var existing = all.FirstOrDefault(x => x.TrackingNumber.Equals(tracking, StringComparison.OrdinalIgnoreCase));
        await UpsertAsync(new PaymentRecord
        {
            TrackingNumber = tracking,
            ServiceId = existing?.ServiceId ?? session.Metadata?.GetValueOrDefault("serviceId") ?? string.Empty,
            Amount = existing?.Amount ?? session.AmountTotal ?? 0,
            Currency = existing?.Currency ?? session.Currency ?? "usd",
            StripeSessionId = session.Id,
            StripePaymentIntentId = session.PaymentIntentId,
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task UpsertAsync(PaymentRecord record)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadAllUnsafeAsync();
            all.RemoveAll(x => x.TrackingNumber.Equals(record.TrackingNumber, StringComparison.OrdinalIgnoreCase));
            all.Add(record);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(all, _json));
        }
        finally { _gate.Release(); }
    }

    private async Task<List<PaymentRecord>> ReadAllAsync()
    {
        await _gate.WaitAsync();
        try { return await ReadAllUnsafeAsync(); }
        finally { _gate.Release(); }
    }

    private async Task<List<PaymentRecord>> ReadAllUnsafeAsync()
    {
        if (!File.Exists(_path)) return new();
        var text = await File.ReadAllTextAsync(_path);
        return string.IsNullOrWhiteSpace(text) ? new() : JsonSerializer.Deserialize<List<PaymentRecord>>(text, _json) ?? new();
    }

    private static Dictionary<string, long> ParseFees(string? raw)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = item.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 2 && long.TryParse(pair[1], out var cents) && cents > 0) result[pair[0]] = cents;
        }
        return result;
    }
}

public sealed class PaymentRecord
{
    public required string TrackingNumber { get; init; }
    public required string ServiceId { get; init; }
    public long Amount { get; init; }
    public string Currency { get; init; } = "usd";
    public required string StripeSessionId { get; init; }
    public string? StripePaymentIntentId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record PaymentSummary(bool Required, long Amount, string Currency, string Status, string? StripeSessionId);
public sealed record CheckoutResult(string SessionId, string Url, long Amount, string Currency, string Status);
