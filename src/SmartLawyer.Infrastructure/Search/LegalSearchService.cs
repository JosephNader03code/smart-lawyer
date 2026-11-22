using Microsoft.EntityFrameworkCore;
using SmartLawyer.Core.Arabic;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Persistence.Entities;

namespace SmartLawyer.Infrastructure.Search;

public sealed record ArticleHit(
    int LawId, string LawTitle, string ArticleNumber, string Text, DateOnly ValidFrom, DateOnly? ValidTo, float Rank);

public sealed record RulingHit(
    int Id, Court Court, int AppealNumber, int JudicialYear, DateOnly? SessionDate, string? Chamber,
    string? Principle, string Excerpt, float Rank);

/// <summary>
/// البحث في القوانين والأحكام بـ Postgres full-text search على النص الموحّد.
/// كل النتايج من قاعدة البيانات بس، ومعاها المصدر بالظبط (القانون + رقم المادة، أو رقم الطعن + السنة).
/// </summary>
public sealed class LegalSearchService(AppDbContext db)
{
    public const int MaxLimit = 50;
    public const int MaxQueryLength = 500;

    /// <summary>بحث في نصوص المواد اللي كانت سارية في تاريخ asOf (افتراضياً النهارده).</summary>
    public async Task<IReadOnlyList<ArticleHit>> SearchArticlesAsync(
        string query, DateOnly? asOf = null, int limit = 10, CancellationToken ct = default)
    {
        var tsQuery = SearchQueryBuilder.BuildTsQuery(query);
        if (tsQuery is null)
        {
            return [];
        }
        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return await db.ArticleVersions.AsNoTracking()
            .Where(v => v.ValidFrom <= date && (v.ValidTo == null || v.ValidTo >= date))
            .Where(v => v.SearchVector.Matches(EF.Functions.ToTsQuery(AppDbContext.SearchConfig, tsQuery)))
            .Select(v => new
            {
                v,
                Rank = v.SearchVector.RankCoverDensity(EF.Functions.ToTsQuery(AppDbContext.SearchConfig, tsQuery)),
            })
            .OrderByDescending(x => x.Rank)
            .ThenBy(x => x.v.Article.LawId).ThenBy(x => x.v.Article.SortOrder)
            .Take(Math.Clamp(limit, 1, MaxLimit))
            .Select(x => new ArticleHit(
                x.v.Article.LawId, x.v.Article.Law.Title, x.v.Article.Number, x.v.Text, x.v.ValidFrom, x.v.ValidTo, x.Rank))
            .ToListAsync(ct);
    }

    /// <summary>نص مادة معينة زي ما كان ساري في تاريخ الواقعة. null لو مش موجودة أو ماكانتش سارية.</summary>
    public async Task<ArticleHit?> GetArticleAsync(
        int lawId, string articleNumber, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var number = ArabicNormalizer.NormalizeDigits(articleNumber).Trim();

        return await db.ArticleVersions.AsNoTracking()
            .Where(v => v.Article.LawId == lawId && v.Article.Number == number)
            .Where(v => v.ValidFrom <= date && (v.ValidTo == null || v.ValidTo >= date))
            .Select(v => new ArticleHit(
                v.Article.LawId, v.Article.Law.Title, v.Article.Number, v.Text, v.ValidFrom, v.ValidTo, 1f))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RulingHit>> SearchRulingsAsync(
        string query, int? yearFrom = null, int? yearTo = null, int limit = 10, CancellationToken ct = default)
    {
        var tsQuery = SearchQueryBuilder.BuildTsQuery(query);
        if (tsQuery is null)
        {
            return [];
        }

        var rulings = db.Rulings.AsNoTracking()
            .Where(r => r.SearchVector.Matches(EF.Functions.ToTsQuery(AppDbContext.SearchConfig, tsQuery)));
        if (yearFrom is not null)
        {
            rulings = rulings.Where(r => r.JudicialYear >= yearFrom);
        }
        if (yearTo is not null)
        {
            rulings = rulings.Where(r => r.JudicialYear <= yearTo);
        }

        var hits = await rulings
            .Select(r => new
            {
                r,
                Rank = r.SearchVector.RankCoverDensity(EF.Functions.ToTsQuery(AppDbContext.SearchConfig, tsQuery)),
            })
            .OrderByDescending(x => x.Rank).ThenByDescending(x => x.r.JudicialYear)
            .Take(Math.Clamp(limit, 1, MaxLimit))
            .ToListAsync(ct);

        return hits.Select(x => ToHit(x.r, x.Rank)).ToList();
    }

    public async Task<RulingHit?> GetRulingAsync(
        int appealNumber, int judicialYear, Court court = Court.Cassation, CancellationToken ct = default)
    {
        var r = await db.Rulings.AsNoTracking().FirstOrDefaultAsync(
            x => x.Court == court && x.AppealNumber == appealNumber && x.JudicialYear == judicialYear, ct);
        return r is null ? null : ToHit(r, 1f, excerptLength: int.MaxValue);
    }

    private static RulingHit ToHit(Ruling r, float rank, int excerptLength = 600) => new(
        r.Id, r.Court, r.AppealNumber, r.JudicialYear, r.SessionDate, r.Chamber, r.Principle,
        r.Text.Length <= excerptLength ? r.Text : r.Text[..excerptLength] + "…", rank);
}
