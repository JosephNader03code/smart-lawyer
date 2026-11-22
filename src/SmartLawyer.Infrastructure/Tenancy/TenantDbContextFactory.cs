using Microsoft.EntityFrameworkCore;
using SmartLawyer.Infrastructure.Persistence;

namespace SmartLawyer.Infrastructure.Tenancy;

/// <summary>بيعمل AppDbContext لمكتب محدد (أو من غير مكتب). بنستخدمه في الحالات اللي المكتب فيها لسه بيتحدد.</summary>
public sealed class TenantDbContextFactory(string connectionString)
{
    public AppDbContext Create(Guid? organizationId)
    {
        var tenant = new FixedTenantContext(organizationId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantConnectionInterceptor(tenant))
            .Options;
        return new AppDbContext(options, tenant);
    }
}
