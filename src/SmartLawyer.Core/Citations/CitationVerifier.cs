using System.Text.RegularExpressions;
using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Citations;

public sealed record Citation(SourceKey Key, string? Quote);

public sealed record VerificationResult(
    bool IsValid, IReadOnlyList<Citation> Citations, IReadOnlyList<string> Problems);

/// <summary>
/// بيتأكد إن إجابة النموذج مستندة لمصادر حقيقية:
/// 1) لازم يكون فيها استشهاد واحد على الأقل.
/// 2) كل استشهاد لازم يكون لمصدر النموذج جابه فعلاً من قاعدة البيانات في نفس الجلسة.
/// 3) أي نص مقتبس «...» بعد الاستشهاد لازم يكون موجود حرفياً (بعد التوحيد) في نص المصدر.
/// </summary>
public static partial class CitationVerifier
{
    // [[article:12:147]] أو [[article:12:18 مكرر (أ)]] أو [[ruling:1234:85]]، ممكن بعدها اقتباس «...».
    [GeneratedRegex(@"\[\[(?<kind>article|ruling)\s*:\s*(?<a>[^:\]]+?)\s*:\s*(?<b>[^\]]+?)\s*\]\](?:\s*«(?<quote>[^»]{1,1000})»)?")]
    private static partial Regex CitationPattern();

    public static IReadOnlyList<Citation> Parse(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var text = ArabicNormalizer.NormalizeDigits(answer);
        return CitationPattern().Matches(text)
            .Select(m => new Citation(
                new SourceKey(
                    m.Groups["kind"].Value == "article" ? SourceKind.Article : SourceKind.Ruling,
                    m.Groups["a"].Value.Trim(),
                    m.Groups["b"].Value.Trim()),
                m.Groups["quote"].Success ? m.Groups["quote"].Value.Trim() : null))
            .ToList();
    }

    public static VerificationResult Verify(string answer, IReadOnlyDictionary<SourceKey, FetchedSource> fetched)
    {
        ArgumentNullException.ThrowIfNull(fetched);
        var citations = Parse(answer);
        var problems = new List<string>();

        if (citations.Count == 0)
        {
            problems.Add("الإجابة مافيهاش أي استشهاد بمصدر.");
        }

        foreach (var c in citations)
        {
            if (!fetched.TryGetValue(c.Key, out var source))
            {
                problems.Add($"المصدر {c.Key} مش من المصادر اللي اتجابت من قاعدة البيانات.");
                continue;
            }
            if (c.Quote is not null && !QuoteAppearsIn(c.Quote, source.Text))
            {
                problems.Add($"الاقتباس «{c.Quote}» مش موجود في نص {c.Key}.");
            }
        }

        return new VerificationResult(problems.Count == 0, citations, problems);
    }

    /// <summary>مقارنة بعد توحيد النص وتجاهل علامات الترقيم، عشان اختلاف الهمزة أو الفاصلة مايفشلش اقتباس صحيح.</summary>
    public static bool QuoteAppearsIn(string quote, string sourceText)
    {
        var q = string.Join(' ', SearchQueryBuilder.Tokenize(quote));
        if (q.Length == 0)
        {
            return false;
        }
        var s = string.Join(' ', SearchQueryBuilder.Tokenize(sourceText));
        return $" {s} ".Contains($" {q} ", StringComparison.Ordinal);
    }
}
