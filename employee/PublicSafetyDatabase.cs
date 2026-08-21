using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace CltPlusPlus.Employee;

public static class PublicSafetyDatabase
{
    public static bool UseSqlServer(IHostEnvironment environment) => environment.IsProduction();

    public static string GetSqlitePath(IConfiguration configuration)
    {
        var path = configuration["CLTPP_PUBLIC_SAFETY_DB"] ?? Path.Combine(AppContext.BaseDirectory, "data", "public-safety.db");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        return path;
    }

    public static string GetSqlServerConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            throw new InvalidOperationException("SQL Server public-safety storage is production-only.");

        var connectionString = configuration["CLTPP_PUBLIC_SAFETY_SQLSERVER_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("CLTPP_PUBLIC_SAFETY_SQLSERVER_CONNECTION_STRING is required in Production.");

        return connectionString;
    }

    public static DbConnection OpenConnection(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsProduction())
            return new SqlConnection(GetSqlServerConnectionString(configuration, environment));

        return new SqliteConnection($"Data Source={GetSqlitePath(configuration)}");
    }
}
