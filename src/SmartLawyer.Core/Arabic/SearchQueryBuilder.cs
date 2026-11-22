using System.Text;

namespace SmartLawyer.Core.Arabic;

/// <summary>
/// بيحوّل سؤال المحامي لاستعلام Postgres full-text (to_tsquery) بعد توحيد النص وشيل الكلمات الشائعة.
/// الكلمات بتتربط بـ OR ( | ) والترتيب (ts_rank_cd) بيقدّم النصوص اللي فيها كلمات أكتر.
/// </summary>
public static class SearchQueryBuilder
{
    // كلمات شائعة مالهاش وزن في البحث (بعد التوحيد).
    private static readonly HashSet<string> StopWords =
    [
        "في", "من", "علي", "الي", "الى", "عن", "مع", "او", "و", "ثم", "ما", "ماذا", "هل", "هي", "هو",
        "التي", "الذي", "الذين", "اللتي", "ان", "انه", "كان", "كانت", "يكون", "تكون", "ذلك", "هذا", "هذه",
        "قد", "لا", "لم", "لن", "كل", "اي", "بين", "عند", "بعد", "قبل", "حتي", "اذا", "لو", "به", "بها",
        "له", "لها", "فيه", "فيها", "منه", "منها", "عليه", "عليها", "الواجب", "يجب", "ايه", "اللي", "ده", "دي",
    ];

    /// <summary>بيرجع null لو مفيش كلمات صالحة للبحث.</summary>
    public static string? BuildTsQuery(string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var terms = Tokenize(question)
            .Where(t => !StopWords.Contains(t))
            .Distinct()
            .ToList();
        return terms.Count == 0 ? null : string.Join(" | ", terms);
    }

    /// <summary>تقسيم لكلمات (حروف وأرقام بس) بعد التوحيد. الرموز بتتشال عشان to_tsquery مايتكسرش.</summary>
    public static IEnumerable<string> Tokenize(string text)
    {
        var normalized = ArabicNormalizer.NormalizeForSearch(text);
        var current = new StringBuilder();
        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
