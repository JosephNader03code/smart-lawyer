using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Llm;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Research;
using SmartLawyer.Infrastructure.Search;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        services.AddSingleton(new TenantDbContextFactory(connectionString));
        services.AddScoped<AccountService>();
        services.AddScoped<LegalSearchService>();
        services.AddScoped<LegalImportService>();
        services.TryAddSingleton(new ResearchOptions());
        services.AddScoped(sp => new ResearchAgent(
            sp.GetRequiredService<ILlmClient>(), sp.GetRequiredService<LegalSearchService>(),
            sp.GetRequiredService<ResearchOptions>().Mode));
        services.AddScoped<ResearchService>();
        services.AddScoped<CaseAnalysis.FactExtractor>();
        services.AddScoped<CaseAnalysis.CaseAnalysisService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>
    /// بيسجّل مزوّدين الـ AI من الإعدادات. المفاتيح بتتقرا من متغيرات البيئة (مش من الإعدادات)،
    /// وأي مزوّد محتاج مفتاح ومفتاحه مش موجود بيتقفل.
    /// </summary>
    public static IServiceCollection AddLlm(this IServiceCollection services, LlmOptions options)
    {
        services.AddHttpClient(nameof(OpenAiCompatibleClient), c => c.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<ILlmClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<LlmRouter>>();
            var clients = new List<OpenAiCompatibleClient>();
            foreach (var p in options.Providers)
            {
                var key = string.IsNullOrEmpty(p.ApiKeyEnvVar) ? null : Environment.GetEnvironmentVariable(p.ApiKeyEnvVar);
                if (!string.IsNullOrEmpty(p.ApiKeyEnvVar) && string.IsNullOrEmpty(key))
                {
                    logger.LogInformation("مزوّد {Provider} مقفول: المتغير {Var} مش موجود", p.Name, p.ApiKeyEnvVar);
                    p.Enabled = false;
                }
                clients.Add(new OpenAiCompatibleClient(factory.CreateClient(nameof(OpenAiCompatibleClient)), p, key));
            }
            return new LlmRouter(clients, logger);
        });
        return services;
    }
}
