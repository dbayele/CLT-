using Npgsql;

namespace CltPlusPlus.Employee;

public static class RdsConnectionFactory
{
    public static bool IsEnabled(IHostEnvironment env, IConfiguration config) =>
        env.IsProduction() && bool.TryParse(config["CLTPP_RDS_ENABLED"], out var enabled) && enabled;

    public static NpgsqlConnection Create(IHostEnvironment env, IConfiguration config, string? domain = null)
    {
        if (!IsEnabled(env, config))
            throw new InvalidOperationException("Amazon RDS connections are disabled outside Production or until CLTPP_RDS_ENABLED=true.");

        var domainKey = string.IsNullOrWhiteSpace(domain) ? null : $"CLTPP_RDS_{domain.ToUpperInvariant()}_CONNECTION_STRING";
        var connectionString = domainKey is null ? null : config[domainKey];
        connectionString ??= config["CLTPP_RDS_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"No RDS connection string is configured{(domainKey is null ? "." : $" for {domainKey}.")}");

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            Timeout = 15,
            CommandTimeout = 30,
            KeepAlive = 30
        };
        return new NpgsqlConnection(builder.ConnectionString);
    }
}
