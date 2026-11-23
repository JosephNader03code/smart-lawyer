using Microsoft.EntityFrameworkCore;
using SmartLawyer.Core.Contradictions;
using SmartLawyer.Core.Timeline;
using SmartLawyer.Infrastructure.Llm;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Research;

namespace SmartLawyer.Infrastructure.CaseAnalysis;

public sealed record CaseSummary(Guid Id, string Number, string Title, string? Type, string? Description, DateTimeOffset CreatedAt, int Documents);

public sealed record DocumentSummary(Guid Id, string FileName, string Status, int Pages, string? Error, int Events, int Facts, DateTimeOffset CreatedAt);

public sealed record TimelineItem(
    Guid Id, string Kind, string Title, DateTime Date, bool HasTime, string RawDateText, string Quote, string? Document, bool InConflict);

public sealed record TimelineConflictView(IReadOnlyList<Guid> EventIds, string Message);

public sealed record TimelineView(IReadOnlyList<TimelineItem> Events, IReadOnlyList<TimelineConflictView> Conflicts);

public sealed record ContradictionView(string Kind, string Explanation, double Difference, ClaimView First, ClaimView Second);

public sealed record ClaimView(string Subject, string Quote, DateTime? Time, decimal? Amount, string Document);

public sealed class CaseNotFoundException() : Exception("القضية مش موجودة.");

/// <summary>
/// تحليل القضايا: إنشاء القضية، رفع المستندات واستخراج الوقائع منها، والخط الزمني والتناقضات بخوارزميات ثابتة.
/// كل حاجة هنا تابعة لمكتب المستخدم (فلاتر EF + Row-Level Security).
/// </summary>
public sealed class CaseAnalysisService(AppDbContext db, FactExtractor extractor, QuotaOptions quota, TimeProvider clock)
{
    public static readonly TimeSpan TimeTolerance = TimeSpan.FromMinutes(30);

    /// <summary>قواعد منطقية للترتيب: الحدث التاني لازم ييجي بعد الأول.</summary>
    public static readonly (string First, string Then, string Reason)[] OrderRules =
    [
        (EventKinds.Incident, EventKinds.Notice, "الإخطار عن حادث لازم يكون بعد وقوعه"),
        (EventKinds.Signing, EventKinds.Delivery, "التسليم لازم يكون بعد توقيع العقد"),
    ];

    public async Task<CaseSummary> CreateCaseAsync(string title, string? number, string? type, string? description, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var c = new LegalCase
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Number = string.IsNullOrWhiteSpace(number) ? $"C-{now:yyyyMMdd-HHmmss}" : number.Trim(),
            Type = type,
            Description = description,
            CreatedAt = now,
        };
        db.Cases.Add(c);
        await db.SaveChangesAsync(ct);
        return new CaseSummary(c.Id, c.Number, c.Title, c.Type, c.Description, c.CreatedAt, 0);
    }

    public async Task<IReadOnlyList<CaseSummary>> ListCasesAsync(CancellationToken ct = default) =>
        await db.Cases.AsNoTracking().OrderByDescending(c => c.CreatedAt)
            .Select(c => new CaseSummary(c.Id, c.Number, c.Title, c.Type, c.Description, c.CreatedAt, c.Documents.Count))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(Guid caseId, CancellationToken ct = default)
    {
        await EnsureCaseAsync(caseId, ct);
        return await db.CaseDocuments.AsNoTracking().Where(d => d.CaseId == caseId).OrderBy(d => d.CreatedAt)
            .Select(d => new DocumentSummary(d.Id, d.FileName, d.Status.ToString(), d.Pages, d.Error,
                db.CaseEvents.Count(e => e.SourceDocumentId == d.Id), db.CaseClaims.Count(f => f.SourceDocumentId == d.Id), d.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>
    /// بيرفع مستند ويحلله. النص بيتحفظ حتى لو التحليل فشل (مثلاً حد الـ AI خلص)، عشان يتعاد بعدين من غير رفع تاني.
    /// </summary>
    public async Task<DocumentSummary> AddDocumentAsync(Guid userId, Guid caseId, string fileName, byte[] content, CancellationToken ct = default)
    {
        await EnsureCaseAsync(caseId, ct);
        var text = DocumentTextReader.Read(fileName, content);
        var doc = new CaseDocument
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            FileName = Path.GetFileName(fileName),
            Kind = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            Pages = Math.Max(1, text.Count(c => c == '\f') + 1),
            Status = DocumentStatus.Processing,
            ExtractedText = text,
            CreatedAt = clock.GetUtcNow(),
        };
        db.CaseDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return await AnalyzeAsync(userId, doc, ct);
    }

    public async Task<DocumentSummary> ReanalyzeAsync(Guid userId, Guid caseId, Guid documentId, CancellationToken ct = default)
    {
        var doc = await db.CaseDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == caseId, ct)
                  ?? throw new CaseNotFoundException();
        db.CaseEvents.RemoveRange(db.CaseEvents.Where(e => e.SourceDocumentId == doc.Id));
        db.CaseClaims.RemoveRange(db.CaseClaims.Where(f => f.SourceDocumentId == doc.Id));
        return await AnalyzeAsync(userId, doc, ct);
    }

    private async Task<DocumentSummary> AnalyzeAsync(Guid userId, CaseDocument doc, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var usage = await db.UsageQuotas.FirstOrDefaultAsync(q => q.UserId == userId && q.Day == today, ct);
        if ((usage?.Analyses ?? 0) >= quota.AnalysesPerDay)
        {
            doc.Status = DocumentStatus.Failed;
            doc.Error = $"وصلت للحد اليومي ({quota.AnalysesPerDay} مستندات). المستند اتحفظ وتقدر تحلله بكرة.";
            await db.SaveChangesAsync(ct);
            return ToSummary(doc, 0, 0);
        }

        ExtractionResult result;
        try
        {
            result = await extractor.ExtractAsync(doc.ExtractedText ?? "", ct);
        }
        catch (LlmUnavailableException ex)
        {
            doc.Status = DocumentStatus.Failed;
            doc.Error = ex.Message;
            await db.SaveChangesAsync(ct);
            return ToSummary(doc, 0, 0);
        }

        foreach (var e in result.Events)
        {
            db.CaseEvents.Add(new CaseEvent
            {
                Id = Guid.NewGuid(), CaseId = doc.CaseId, OccurredAt = e.Date, HasTime = e.HasTime, RawDateText = e.RawDateText,
                Kind = e.Kind, Title = e.Title, Quote = e.Quote, SourceDocumentId = doc.Id, SourceLocation = doc.FileName,
            });
        }
        foreach (var f in result.Facts)
        {
            db.CaseClaims.Add(new CaseClaim
            {
                Id = Guid.NewGuid(), CaseId = doc.CaseId, Subject = f.Subject, Predicate = f.Predicate, Time = f.Time,
                RawTimeText = f.RawTimeText, Amount = f.Amount, Quote = f.Quote, SourceDocumentId = doc.Id, SourceLocation = doc.FileName,
            });
        }

        if (usage is null)
        {
            usage = new UsageQuota { UserId = userId, Day = today };
            db.UsageQuotas.Add(usage);
        }
        usage.Analyses++;
        db.LlmUsages.Add(new LlmUsage
        {
            UserId = userId, Feature = "case-extraction", Model = result.Model ?? "unknown",
            InputTokens = result.InputTokens, OutputTokens = result.OutputTokens, CostUsd = result.CostUsd, CreatedAt = clock.GetUtcNow(),
        });
        doc.Status = DocumentStatus.Processed;
        doc.Error = result.Rejected > 0 ? $"اترفض {result.Rejected} عنصر لأن الاقتباس أو التاريخ مش موجود في المستند." : null;
        await db.SaveChangesAsync(ct);
        return ToSummary(doc, result.Events.Count, result.Facts.Count);
    }

    public async Task<TimelineView> GetTimelineAsync(Guid caseId, CancellationToken ct = default)
    {
        await EnsureCaseAsync(caseId, ct);
        var rows = await (from e in db.CaseEvents.AsNoTracking()
                          where e.CaseId == caseId && e.OccurredAt != null
                          join d in db.CaseDocuments.AsNoTracking() on e.SourceDocumentId equals d.Id into docs
                          from d in docs.DefaultIfEmpty()
                          orderby e.OccurredAt
                          select new { e, Document = d == null ? null : d.FileName }).ToListAsync(ct);

        var conflicts = FindConflicts(rows.Select(r => r.e).ToList());
        var inConflict = conflicts.SelectMany(c => c.EventIds).ToHashSet();
        var items = rows.Select(r => new TimelineItem(r.e.Id, r.e.Kind, r.e.Title, r.e.OccurredAt!.Value, r.e.HasTime,
            r.e.RawDateText, r.e.Quote, r.Document, inConflict.Contains(r.e.Id))).ToList();
        return new TimelineView(items, conflicts);
    }

    /// <summary>
    /// لكل قاعدة ترتيب ولكل زوج أحداث من النوعين، بنبني Simple Temporal Network ونشوف فيه تعارض.
    /// الحدث اللي من غير ساعة بيتعامل على إنه في أي وقت خلال اليوم.
    /// </summary>
    public static IReadOnlyList<TimelineConflictView> FindConflicts(IReadOnlyList<CaseEvent> events)
    {
        var result = new List<TimelineConflictView>();
        foreach (var (first, then, reason) in OrderRules)
        {
            foreach (var a in events.Where(e => e.Kind == first && e.OccurredAt is not null))
            {
                foreach (var b in events.Where(e => e.Kind == then && e.OccurredAt is not null))
                {
                    var stn = new SimpleTemporalNetwork();
                    AddKnown(stn, a);
                    AddKnown(stn, b);
                    stn.AddMustFollow(a.Id.ToString(), b.Id.ToString(), reason);
                    if (stn.FindConflict() is { } c)
                    {
                        var gap = (a.OccurredAt!.Value.Date - b.OccurredAt!.Value.Date).TotalHours;
                        var amount = gap >= 24 ? $"{gap:0} ساعة" : "ساعات";
                        result.Add(new TimelineConflictView([a.Id, b.Id],
                            $"«{b.Title}» ({b.RawDateText}) جه قبل «{a.Title}» ({a.RawDateText}) بـ {amount}، مع إن {reason}."));
                    }
                }
            }
        }
        return result;
    }

    private static void AddKnown(SimpleTemporalNetwork stn, CaseEvent e)
    {
        var t = e.OccurredAt!.Value;
        if (e.HasTime)
        {
            stn.AddKnownTime(e.Id.ToString(), t, t, e.RawDateText);
        }
        else
        {
            stn.AddKnownTime(e.Id.ToString(), t.Date, t.Date.AddDays(1).AddMinutes(-1), e.RawDateText);
        }
    }

    public async Task<IReadOnlyList<ContradictionView>> GetContradictionsAsync(Guid caseId, CancellationToken ct = default)
    {
        await EnsureCaseAsync(caseId, ct);
        var claims = await (from f in db.CaseClaims.AsNoTracking()
                            where f.CaseId == caseId
                            join d in db.CaseDocuments.AsNoTracking() on f.SourceDocumentId equals d.Id
                            select new { f, d.FileName }).ToListAsync(ct);
        var byId = claims.ToDictionary(x => x.f.Id.ToString());
        var facts = claims.Select(x => new Fact(x.f.Id.ToString(), x.f.Subject, x.f.Predicate, x.f.Time, x.f.Amount, x.FileName, x.f.SourceLocation ?? x.FileName)).ToList();

        ClaimView View(Fact f) => new(f.Subject, byId[f.Id].f.Quote, f.Time, f.Amount, f.SourceDocument);
        return FactContradictionDetector.Detect(facts, TimeTolerance)
            .Select(c => new ContradictionView(c.Kind == ContradictionKind.Time ? "time" : "amount", c.Explanation, c.Difference, View(c.First), View(c.Second)))
            .ToList();
    }

    private async Task EnsureCaseAsync(Guid caseId, CancellationToken ct)
    {
        if (!await db.Cases.AnyAsync(c => c.Id == caseId, ct))
        {
            throw new CaseNotFoundException();
        }
    }

    private static DocumentSummary ToSummary(CaseDocument d, int events, int facts) =>
        new(d.Id, d.FileName, d.Status.ToString(), d.Pages, d.Error, events, facts, d.CreatedAt);
}
