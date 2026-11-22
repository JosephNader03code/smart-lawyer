using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Search;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        services.AddScoped<LegalSearchService>();
        services.AddScoped<Import.LegalImportService>();
        return services;
    }
}
