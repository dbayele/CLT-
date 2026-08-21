using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api;

public static class DatabaseRuntime
{
    public static bool UseRds(IHostEnvironment env, IConfiguration config)
    {
        if (!env.IsProduction()) return false;
        return bool.TryParse(config["CLTPP_RDS_ENABLED"], out var enabled) && enabled;
    }

    public static void ConfigureResidentDatabase(DbContextOptionsBuilder options, IHostEnvironment env, IConfiguration config)
    {
        if (UseRds(env, config))
        {
            var connectionString = config["CLTPP_RDS_CONNECTION_STRING"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("CLTPP_RDS_ENABLED is true in Production, but CLTPP_RDS_CONNECTION_STRING is missing.");

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                npgsql.CommandTimeout(30);
            });
            return;
        }

        var residentDb = config["CLTPP_RESIDENT_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "residents.db");
        Directory.CreateDirectory(Path.GetDirectoryName(residentDb)!);
        options.UseSqlite($"Data Source={residentDb}");
    }

    public static object Describe(IHostEnvironment env, IConfiguration config) => new
    {
        provider = UseRds(env, config) ? "Amazon RDS PostgreSQL" : "SQLite",
        rdsEnabled = UseRds(env, config),
        environment = env.EnvironmentName
    };
}
