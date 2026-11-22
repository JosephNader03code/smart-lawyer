using System.Globalization;
using SmartLawyer.Api.Auth;
using SmartLawyer.Infrastructure;
using SmartLawyer.Infrastructure.Llm;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Research;
using SmartLawyer.Infrastructure.Search;
using SmartLawyer.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<RequestTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestTenantContext>());

var supabase = builder.Configuration.GetSection(SupabaseOptions.Section).Get<SupabaseOptions>() ?? new SupabaseOptions();
builder.Services.AddSupabaseAuth(supabase);

var llmOptions = builder.Configuration.GetSection(LlmOptions.Section).Get<LlmOptions>() ?? new LlmOptions();
builder.Services.AddLlm(llmOptions);
builder.Services.AddSingleton(builder.Configuration.GetSection(QuotaOptions.Section).Get<QuotaOptions>() ?? new QuotaOptions());

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("Default");
var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
if (hasDatabase)
{
    builder.Services.AddInfrastructure(connectionString!);
}

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
if (hasDatabase)
{
    app.UseTenantResolution();
}
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (hasDatabase)
{
    MapLegalEndpoints(app);
    MapAccountEndpoints(app);
    MapResearchEndpoints(app);
}
else
{
    app.Logger.LogWarning("ConnectionStrings:Default مش موجود؛ endpoints قاعدة البيانات مش متفعّلة.");
}

app.Run();

static void MapLegalEndpoints(WebApplication app)
{
    // بيانات قانونية عامة: متاحة من غير تسجيل دخول.
    var legal = app.MapGroup("/api/legal");

    legal.MapGet("/articles/search", async (string q, string? asOf, int? limit, LegalSearchService search, CancellationToken ct) =>
    {
        if (ValidateQuery(q) is { } invalid)
        {
            return invalid;
        }
        if (!TryParseDate(asOf, out var date))
        {
            return Results.BadRequest(new { error = "asOf لازم يكون بالشكل yyyy-MM-dd" });
        }
        return Results.Ok(await search.SearchArticlesAsync(q, date, limit ?? 10, ct));
    });

    legal.MapGet("/laws/{lawId:int}/articles/{number}", async (int lawId, string number, string? asOf, LegalSearchService search, CancellationToken ct) =>
    {
        if (!TryParseDate(asOf, out var date))
        {
            return Results.BadRequest(new { error = "asOf لازم يكون بالشكل yyyy-MM-dd" });
        }
        var article = await search.GetArticleAsync(lawId, number, date, ct);
        return article is null ? Results.NotFound() : Results.Ok(article);
    });

    legal.MapGet("/rulings/search", async (string q, int? yearFrom, int? yearTo, int? limit, LegalSearchService search, CancellationToken ct) =>
        ValidateQuery(q) ?? Results.Ok(await search.SearchRulingsAsync(q, yearFrom, yearTo, limit ?? 10, ct)));

    legal.MapGet("/rulings/{appealNumber:int}/{judicialYear:int}", async (int appealNumber, int judicialYear, Court? court, LegalSearchService search, CancellationToken ct) =>
    {
        var ruling = await search.GetRulingAsync(appealNumber, judicialYear, court ?? Court.Cassation, ct);
        return ruling is null ? Results.NotFound() : Results.Ok(ruling);
    });
}

static void MapAccountEndpoints(WebApplication app)
{
    app.MapGet("/api/me", (RequestTenantContext tenant) =>
        Results.Ok(new { tenant.UserId, tenant.Email, tenant.OrganizationId }))
        .RequireAuthorization();
}

static void MapResearchEndpoints(WebApplication app)
{
    app.MapPost("/api/research/ask", async (AskRequest request, RequestTenantContext tenant, ResearchService research, CancellationToken ct) =>
    {
        var question = request.Question?.Trim() ?? "";
        if (question.Length is < 5 or > 1000)
        {
            return Results.BadRequest(new { error = "السؤال لازم يكون بين 5 و1000 حرف." });
        }
        try
        {
            var result = await research.AskAsync(tenant.UserId!.Value, question, ct);
            return Results.Ok(new
            {
                result.Answer.Answered,
                result.Answer.Text,
                result.Answer.Sources,
                result.Cached,
                result.RemainingToday,
            });
        }
        catch (QuotaExceededException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (LlmUnavailableException ex)
        {
            app.Logger.LogWarning(ex, "مفيش مزوّد AI متاح");
            return Results.Json(new { error = "خدمة البحث مش متاحة دلوقتي. جرّب كمان شوية." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }).RequireAuthorization();
}

static IResult? ValidateQuery(string q) =>
    q.Length > LegalSearchService.MaxQueryLength
        ? Results.BadRequest(new { error = $"السؤال أطول من {LegalSearchService.MaxQueryLength} حرف" })
        : null;

static bool TryParseDate(string? value, out DateOnly? date)
{
    date = null;
    if (string.IsNullOrWhiteSpace(value))
    {
        return true;
    }
    if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
    {
        date = d;
        return true;
    }
    return false;
}

public sealed record AskRequest(string? Question);

// Exposed for integration tests (WebApplicationFactory).
public partial class Program;
