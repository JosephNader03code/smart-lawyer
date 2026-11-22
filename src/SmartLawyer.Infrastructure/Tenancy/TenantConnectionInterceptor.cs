using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SmartLawyer.Infrastructure.Tenancy;

/// <summary>
/// مع كل فتح اتصال بنكتب رقم المكتب في إعداد الجلسة app.current_org،
/// وسياسات Row-Level Security في Postgres بتقراه (حماية تانية بعد فلاتر EF).
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = CreateCommand(connection);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = CreateCommand(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_org', @org, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "org";
        p.Value = tenant.OrganizationId?.ToString() ?? string.Empty;
        cmd.Parameters.Add(p);
        return cmd;
    }
}
