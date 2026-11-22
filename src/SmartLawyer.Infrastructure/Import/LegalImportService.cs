using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartLawyer.Core.Arabic;
using SmartLawyer.Core.Legal;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Persistence.Entities;

namespace SmartLawyer.Infrastructure.Import;

/// <param name="IsCompleteText">
/// true لو الملف فيه نص القانون كامل: أي مادة سارية مش موجودة في الملف بتتعتبر ملغية من ValidFrom.
/// false (الافتراضي) لو الملف فيه المواد المعدلة بس.
/// </param>
public sealed record LawImportRequest(
    string Title, string? Number, int? Year, string? Source, DateOnly ValidFrom, string Text, bool IsCompleteText = false);

public sealed record LawImportResult(
    int LawId, int ArticlesFound, int NewArticles, int NewVersions, int Unchanged, int Repealed = 0);

public sealed record RulingImportRequest(
    Court Court, string Text, string? Source = null, string? Chamber = null, string? Principle = null);

public sealed record RulingImportResult(int RulingId, RulingReference Reference, bool Created);

/// <summary>
/// استيراد نصوص القوانين والأحكام لقاعدة البيانات.
/// لو مادة اتعدلت، النسخة القديمة بتتقفل (ValidTo = يوم قبل التعديل) والجديدة بتتضاف، فنقدر نرجع لنص المادة يوم الواقعة.
/// </summary>
public sealed partial class LegalImportService(AppDbContext db)
{
    public async Task<LawImportResult> ImportLawAsync(LawImportRequest request, CancellationToken ct = default)
    {
        var parsed = LawTextSplitter.Split(request.Text);
        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("مالقيناش أي مادة في النص. اتأكد إن كل مادة بتبدأ بسطر زي «مادة 1».");
        }
        var duplicates = parsed.GroupBy(a => a.Number).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"أرقام مواد متكررة في النص: {string.Join("، ", duplicates)}");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var law = await db.Laws.FirstOrDefaultAsync(l => l.Title == request.Title, ct);
        if (law is null)
        {
            law = new Law { Title = request.Title };
            db.Laws.Add(law);
        }
        law.Number = request.Number ?? law.Number;
        law.Year = request.Year ?? law.Year;
        law.Source = request.Source ?? law.Source;
        await db.SaveChangesAsync(ct);

        var existing = await db.Articles.Include(a => a.Versions)
            .Where(a => a.LawId == law.Id).ToDictionaryAsync(a => a.Number, ct);

        int newArticles = 0, newVersions = 0, unchanged = 0;
        for (var i = 0; i < parsed.Count; i++)
        {
            var (number, text) = (parsed[i].Number, parsed[i].Text);
            if (!existing.TryGetValue(number, out var article))
            {
                article = new Article { LawId = law.Id, Number = number, SortOrder = i };
                article.Versions.Add(NewVersion(request.ValidFrom, text));
                db.Articles.Add(article);
                newArticles++;
                continue;
            }

            article.SortOrder = i;
            var current = article.Versions.OrderByDescending(v => v.ValidFrom).First();
            if (current.Text == text)
            {
                unchanged++;
                continue;
            }
            if (request.ValidFrom <= current.ValidFrom)
            {
                throw new InvalidOperationException(
                    $"المادة {number}: تاريخ السريان الجديد لازم يكون بعد {current.ValidFrom:yyyy-MM-dd}.");
            }
            current.ValidTo = request.ValidFrom.AddDays(-1);
            article.Versions.Add(NewVersion(request.ValidFrom, text));
            newVersions++;
        }

        var repealed = 0;
        if (request.IsCompleteText)
        {
            var present = parsed.Select(a => a.Number).ToHashSet();
            foreach (var (number, article) in existing)
            {
                var open = article.Versions.SingleOrDefault(v => v.ValidTo is null);
                if (present.Contains(number) || open is null)
                {
                    continue;
                }
                if (request.ValidFrom <= open.ValidFrom)
                {
                    throw new InvalidOperationException(
                        $"المادة {number} مش موجودة في النص الكامل، وتاريخ الإلغاء لازم يكون بعد {open.ValidFrom:yyyy-MM-dd}.");
                }
                open.ValidTo = request.ValidFrom.AddDays(-1);
                repealed++;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new LawImportResult(law.Id, parsed.Count, newArticles, newVersions, unchanged, repealed);
    }

    public async Task<RulingImportResult> ImportRulingAsync(RulingImportRequest request, CancellationToken ct = default)
    {
        var text = ArabicNormalizer.NormalizeDigits(request.Text).Trim();
        var header = text.Length > 400 ? text[..400] : text;
        var reference = RulingReferenceParser.FindAll(header).FirstOrDefault()
            ?? throw new InvalidOperationException("مالقيناش رقم الطعن والسنة في أول الحكم (مثال: الطعن رقم 1234 لسنة 85 ق).");

        var principle = request.Principle ?? ExtractLabeled(text, "المبدأ");
        var chamber = request.Chamber ?? ExtractLabeled(text, "الدائرة");

        var ruling = await db.Rulings.FirstOrDefaultAsync(r =>
            r.Court == request.Court && r.AppealNumber == reference.AppealNumber && r.JudicialYear == reference.JudicialYear, ct);
        var created = ruling is null;
        ruling ??= db.Rulings.Add(new Ruling { Text = text, SearchText = "" }).Entity;

        ruling.Court = request.Court;
        ruling.AppealNumber = reference.AppealNumber;
        ruling.JudicialYear = reference.JudicialYear;
        ruling.SessionDate = reference.SessionDate ?? ruling.SessionDate;
        ruling.Chamber = chamber ?? ruling.Chamber;
        ruling.Principle = principle ?? ruling.Principle;
        ruling.Source = request.Source ?? ruling.Source;
        ruling.Text = text;
        ruling.SearchText = ArabicNormalizer.NormalizeForSearch($"{ruling.Principle} {text}");

        await db.SaveChangesAsync(ct);
        return new RulingImportResult(ruling.Id, reference, created);
    }

    private static ArticleVersion NewVersion(DateOnly validFrom, string text) => new()
    {
        ValidFrom = validFrom,
        Text = text,
        SearchText = ArabicNormalizer.NormalizeForSearch(text),
    };

    [GeneratedRegex(@"^[ \t]*(?<label>[^:\n]{2,20})[ \t]*:[ \t]*(?<value>.+)$", RegexOptions.Multiline)]
    private static partial Regex LabeledLine();

    /// <summary>بيدوّر على سطر زي «المبدأ: ...».</summary>
    private static string? ExtractLabeled(string text, string label)
    {
        foreach (Match m in LabeledLine().Matches(text))
        {
            if (m.Groups["label"].Value.Trim() == label)
            {
                return m.Groups["value"].Value.Trim();
            }
        }
        return null;
    }
}
