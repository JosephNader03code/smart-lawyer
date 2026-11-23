using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartLawyer.Core.Arabic;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Persistence.Entities;

namespace SmartLawyer.Infrastructure.Research;

public sealed class QuotaOptions
{
    public const string Section = "Quotas";
    public int QuestionsPerDay { get; set; } = 15;
    public int AnalysesPerDay { get; set; } = 3;
}

public sealed class QuotaExceededException(int limit) : Exception($"وصلت للحد اليومي ({limit} سؤال). الحد بيتجدد بكرة.")
{
    public int Limit { get; } = limit;
}

public sealed record ResearchResult(ResearchAnswer Answer, bool Cached, int RemainingToday);

/// <summary>
/// بيشغّل وكيل البحث مع: كاش للأسئلة المتكررة لكل مكتب، وحد يومي لكل محامي، وتسجيل التكلفة في llm_usages.
/// </summary>
public sealed class ResearchService(AppDbContext db, ResearchAgent agent, QuotaOptions quota, TimeProvider clock)
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResearchResult> AskAsync(Guid userId, string question, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var hash = Hash(question);

        var usage = await db.UsageQuotas.FirstOrDefaultAsync(q => q.UserId == userId && q.Day == today, ct);
        var used = usage?.Questions ?? 0;

        var cached = await db.AnswerCache.AsNoTracking()
            .Where(c => c.QuestionHash == hash && c.CreatedAt > now - CacheLifetime)
            .Select(c => c.AnswerJson).FirstOrDefaultAsync(ct);
        if (cached is not null)
        {
            var answer = JsonSerializer.Deserialize<ResearchAnswer>(cached, Json)!;
            return new ResearchResult(answer with { InputTokens = 0, OutputTokens = 0, CostUsd = 0 }, true,
                Math.Max(0, quota.QuestionsPerDay - used));
        }

        if (used >= quota.QuestionsPerDay)
        {
            throw new QuotaExceededException(quota.QuestionsPerDay);
        }

        var result = await agent.AskAsync(question, ct);

        if (usage is null)
        {
            usage = new UsageQuota { UserId = userId, Day = today };
            db.UsageQuotas.Add(usage);
        }
        usage.Questions++;

        if (result.Provider is not null)
        {
            db.LlmUsages.Add(new LlmUsage
            {
                UserId = userId,
                Feature = "research",
                Model = $"{result.Provider}/{result.Model}",
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                CostUsd = result.CostUsd,
                CreatedAt = now,
            });
        }

        if (result.Answered)
        {
            var old = await db.AnswerCache.FirstOrDefaultAsync(c => c.QuestionHash == hash, ct);
            if (old is not null)
            {
                db.AnswerCache.Remove(old);
            }
            db.AnswerCache.Add(new AnswerCacheEntry
            {
                QuestionHash = hash,
                Question = question,
                AnswerJson = JsonSerializer.Serialize(result, Json),
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return new ResearchResult(result, false, Math.Max(0, quota.QuestionsPerDay - usage.Questions));
    }

    /// <summary>نفس السؤال بهمزات أو مسافات مختلفة بيدّي نفس المفتاح.</summary>
    public static string Hash(string question) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ArabicNormalizer.NormalizeForSearch(question))));
}
