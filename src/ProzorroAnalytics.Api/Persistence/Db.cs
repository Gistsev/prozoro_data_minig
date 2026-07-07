using Npgsql;

namespace ProzorroAnalytics.Api.Persistence;

public static class Db
{
    public static async Task<NpgsqlConnection> OpenConnectionAsync(IConfiguration configuration, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(configuration.GetConnectionString("Postgres"));
        await connection.OpenAsync(ct);
        return connection;
    }
}
