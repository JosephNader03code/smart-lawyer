using System.Globalization;

using SmartLawyer.Infrastructure;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Search;
using SmartLawyer.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

// المكتب الحالي هيتقرا من JWT بتاع Supabase في M3. لحد دلوقتي مفيش مكتب، فبيانات القضايا مش متاحة من الـ API.
builder.Services.AddScoped<ITenantContext>(_ => new FixedTenantContext(null));

var connectionString = builder.Configuration.GetConnectionString("Default");
var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
if (hasDatabase)
{
    builder.Services.AddInfrastructure(connectionString!);
}

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (hasDatabase)
{
    MapLegalEndpoints(app);
}
else
{
    app.Logger.LogWarning("ConnectionStrings:Default مش موجود؛ endpoints البيانات القانونية مش متفعّلة.");
}

app.Run();

static void MapLegalEndpoints(WebApplication app)
{
    var legal = app.MapGroup("/api/legal");

    legal.MapGet("/articles/search", async (string q, string? asOf, int? limit, LegalSearchService search, CancellationToken ct) =>
    {
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
        Results.Ok(await search.SearchRulingsAsync(q, yearFrom, yearTo, limit ?? 10, ct)));

    legal.MapGet("/rulings/{appealNumber:int}/{judicialYear:int}", async (int appealNumber, int judicialYear, Court? court, LegalSearchService search, CancellationToken ct) =>
    {
        var ruling = await search.GetRulingAsync(appealNumber, judicialYear, court ?? Court.Cassation, ct);
        return ruling is null ? Results.NotFound() : Results.Ok(ruling);
    });
}

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

// Exposed for integration tests (WebApplicationFactory).
public partial class Program;