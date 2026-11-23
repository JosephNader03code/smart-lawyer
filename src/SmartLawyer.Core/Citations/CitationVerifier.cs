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
/// 3) أي نص مقتبس «...» بعد الاستشهاد لازم يكون موجود حرفياً (بعد التوحيد) في نص المصدر ده.
/// 4) أي اقتباس «...» تاني في الإجابة (قبل الاستشهاد مثلاً) لازم يكون موجود حرفياً في واحد من المصادر اللي اتجابت.
/// </summary>
public static partial class CitationVerifier
{
    // [[article:12:147]] أو [[article:12:18 مكرر (أ)]] أو [[ruling:1234:85]]، ممكن بعدها اقتباس «...».
    [GeneratedRegex(@"\[\[(?<kind>article|ruling)\s*:\s*(?<a>[^:\]]+?)\s*:\s*(?<b>[^\]]+?)\s*\]\](?:\s*«(?<quote>[^»]{1,1000})»)?")]
    private static partial Regex CitationPattern();

    [GeneratedRegex(@"«(?<quote>[^»]{1,1000})»")]
    private static partial Regex AnyQuote();

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

        // الاقتباسات اللي مش لازقة في استشهاد: لازم تكون من أي مصدر اتجاب.
        var checkedQuotes = citations.Where(c => c.Quote is not null).Select(c => c.Quote!).ToHashSet();
        foreach (Match m in AnyQuote().Matches(ArabicNormalizer.NormalizeDigits(answer)))
        {
            var quote = m.Groups["quote"].Value.Trim();
            if (!checkedQuotes.Contains(quote) && !fetched.Values.Any(f => QuoteAppearsIn(quote, f.Text)))
            {
                problems.Add($"الاقتباس «{quote}» مش موجود في أي مصدر من المصادر.");
            }
        }

        return new VerificationResult(problems.Count == 0, citations, problems);
    }

    /// <summary>
    /// مقارنة بعد توحيد النص وتجاهل علامات الترقيم، عشان اختلاف الهمزة أو الفاصلة مايفشلش اقتباس صحيح.
    /// النماذج كتير بتشيل حرف العطف من أول كلمة ("وكان" ← "كان")، فأول كلمة بس مسموح تبقى ناقصة "و" أو "ف".
    /// </summary>
    public static bool QuoteAppearsIn(string quote, string sourceText)
    {
        var q = SearchQueryBuilder.Tokenize(quote).ToList();
        if (q.Count == 0)
        {
            return false;
        }
        var s = SearchQueryBuilder.Tokenize(sourceText).ToList();
        for (var i = 0; i + q.Count <= s.Count; i++)
        {
            var first = s[i];
            if (first != q[0] && first != "و" + q[0] && first != "ف" + q[0])
            {
                continue;
            }
            var match = true;
            for (var k = 1; k < q.Count && match; k++)
            {
                match = s[i + k] == q[k];
            }
            if (match)
            {
                return true;
            }
        }
        return false;
    }
}
