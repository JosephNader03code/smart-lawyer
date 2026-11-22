using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLawyer.Infrastructure.Persistence.Entities;

namespace SmartLawyer.Api.Tests;

public class TenantIsolationTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    private async Task<(Guid OrgA, Guid OrgB)> SeedAsync()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        await using (var db = fx.CreateContext(null))
        {
            db.Organizations.AddRange(
                new Organization { Id = orgA, Name = "مكتب أ", CreatedAt = DateTimeOffset.UtcNow },
                new Organization { Id = orgB, Name = "مكتب ب", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using (var a = fx.CreateContext(orgA, asAppRole: true))
        {
            a.Cases.Add(new LegalCase { Id = Guid.NewGuid(), Number = $"A-{orgA:N}"[..12], Title = "قضية سرية لمكتب أ", CreatedAt = DateTimeOffset.UtcNow });
            await a.SaveChangesAsync();
        }
        return (orgA, orgB);
    }

    [Fact]
    public async Task Office_SeesOnlyItsOwnCases()
    {
        var (orgA, orgB) = await SeedAsync();

        await using var a = fx.CreateContext(orgA, asAppRole: true);
        await using var b = fx.CreateContext(orgB, asAppRole: true);

        Assert.Single(await a.Cases.ToListAsync());
        Assert.Empty(await b.Cases.ToListAsync());
    }

    [Fact]
    public async Task NoOffice_SeesNoCases() // زي أداة الاستيراد
    {
        await SeedAsync();
        await using var none = fx.CreateContext(null, asAppRole: true);
        Assert.Empty(await none.Cases.ToListAsync());
    }

    [Fact]
    public async Task WritingAnotherOfficesData_IsBlockedByEf()
    {
        var (orgA, orgB) = await SeedAsync();
        await using var b = fx.CreateContext(orgB, asAppRole: true);
        b.Cases.Add(new LegalCase { Id = Guid.NewGuid(), OrganizationId = orgA, Number = "X-1", Title = "تزوير", CreatedAt = DateTimeOffset.UtcNow });

        await Assert.ThrowsAsync<InvalidOperationException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task RowLevelSecurity_BlocksRawSqlThatBypassesEf()
    {
        var (orgA, orgB) = await SeedAsync();

        await using var conn = new NpgsqlConnection(fx.AppConnectionString);
        await conn.OpenAsync();
        await using (var set = new NpgsqlCommand($"SELECT set_config('app.current_org', '{orgB}', false)", conn))
        {
            await set.ExecuteScalarAsync();
        }

        // SQL مباشر من غير فلاتر EF: المكتب ب مايشوفش قضايا المكتب أ.
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM cases WHERE organization_id = @a", conn))
        {
            count.Parameters.AddWithValue("a", orgA);
            Assert.Equal(0L, await count.ExecuteScalarAsync());
        }

        // ومايقدرش يكتب قضية باسم المكتب أ.
        await using var insert = new NpgsqlCommand(
            "INSERT INTO cases (id, organization_id, number, title, created_at) VALUES (@id, @a, 'X-2', 'تزوير', now())", conn);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("a", orgA);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege (row-level security)
    }
}
