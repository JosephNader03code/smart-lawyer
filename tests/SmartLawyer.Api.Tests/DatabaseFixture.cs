using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Api.Tests;

/// <summary>
/// بيعمل قاعدة بيانات Postgres جديدة لكل مجموعة tests ويطبّق عليها الـ migrations، وبيمسحها في الآخر.
/// محتاج TEST_DATABASE_URL لاتصال بصلاحية إنشاء databases وroles، مثلاً:
/// Host=localhost;Username=postgres;Password=postgres
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string AppRole = "sl_app_test";
    public const string AppRolePassword = "sl_app_test_pw";

    private readonly string _adminConnection =
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? throw new InvalidOperationException(
            "حط TEST_DATABASE_URL عشان الـ integration tests تشتغل (مثال: Host=localhost;Username=postgres;Password=postgres).");

    private readonly string _databaseName = $"sl_test_{Guid.NewGuid():N}";

    /// <summary>اتصال بصلاحيات كاملة (صاحب الجداول).</summary>
    public string ConnectionString { get; private set; } = "";

    /// <summary>اتصال بـ role عادي من غير BYPASSRLS، زي ما الـ API المفروض يتصل في الإنتاج.</summary>
    public string AppConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await Exec(admin, $"CREATE DATABASE {_databaseName}");
            try
            {
                await Exec(admin, $"""
                DO $$ BEGIN
                  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{AppRole}') THEN
                    CREATE ROLE {AppRole} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER NOBYPASSRLS;
                  END IF;
                END $$;
                """);
            }
            catch (PostgresException e) when (e.SqlState is "23505" or "42710")
            {
                // fixture تانية شغالة بالتوازي عملت الـ role في نفس اللحظة.
            }
        }

        var b = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _databaseName };
        ConnectionString = b.ConnectionString;
        AppConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = AppRole,
            Password = AppRolePassword,
        }.ConnectionString;

        await using (var db = CreateContext(null))
        {
            await db.Database.MigrateAsync();
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await Exec(conn, $"""
            GRANT USAGE ON SCHEMA app TO {AppRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA app TO {AppRole};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA app TO {AppRole};
            """);
    }

    public AppDbContext CreateContext(Guid? organizationId, bool asAppRole = false)
    {
        var tenant = new FixedTenantContext(organizationId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(asAppRole ? AppConnectionString : ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantConnectionInterceptor(tenant))
            .Options;
        return new AppDbContext(options, tenant);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await Exec(admin, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)");
    }

    private static async Task Exec(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
