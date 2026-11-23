using System.Text.RegularExpressions;
using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Legal;

/// <summary>
/// تقسيم نص قانون لمواد. بيتعرف على عناوين زي:
/// "مادة 1"، "المادة (147)"، "مادة ١٤٧ مكرراً"، "مادة 18 مكرر (أ)"، "(مادة 5)"،
/// "مادة 1 - نص المادة على نفس السطر"، و"المادة الأولى" (مواد الإصدار).
/// العنوان لازم يبقى في أول السطر عشان مايتلخبطش مع "طبقاً للمادة 5" في نص مادة تانية،
/// ولو بعده نص على نفس السطر لازم يتفصل بشرطة أو نقطتين.
/// </summary>
public static partial class LawTextSplitter
{
    [GeneratedRegex(
        @"^\s*\(?\s*(?:ال)?مادة\s*\(?\s*(?<num>\d+)\s*\)?(?<suffix>\s*مكرر[اً]*(?:\s*\(?\s*(?<letter>[أ-ي])\s*\)?)?)?\s*\)?\s*(?:[-–—:.]\s*(?<rest>.*?))?\s*$")]
    private static partial Regex NumericHeading();

    // مواد الإصدار بتتكتب بالحروف. المقارنة بتتم على النص بعد التوحيد (الاولي، الثانيه، ...).
    [GeneratedRegex(
        @"^\(?\s*(?:ال)?ماده\s+(?<ord>(?:الحاديه|الثانيه|الثالثه|الرابعه|الخامسه|السادسه|السابعه|الثامنه|التاسعه|الاولي|العاشره)(?:\s+عشره?)?|العشرون)\s*\)?\s*[-:.]?$")]
    private static partial Regex OrdinalHeading();

    public static IReadOnlyList<LawArticle> Split(string lawText)
    {
        ArgumentNullException.ThrowIfNull(lawText);
        var lines = ArabicNormalizer.NormalizeDigits(lawText).Replace("\r\n", "\n").Split('\n');
        var articles = new List<LawArticle>();
        string? number = null;
        var body = new List<string>();

        void Flush()
        {
            if (number is not null)
            {
                articles.Add(new LawArticle(number, string.Join("\n", body).Trim()));
            }
            body.Clear();
        }

        foreach (var line in lines)
        {
            if (TryParseHeading(line, out var heading, out var rest))
            {
                Flush();
                number = heading;
                if (rest.Length > 0)
                {
                    body.Add(rest);
                }
            }
            else if (number is not null)
            {
                body.Add(line.Trim());
            }
        }
        Flush();
        return MarkIssuanceArticles(articles);
    }

    /// <summary>
    /// قوانين كتير بتبدأ بمواد إصدار مرقمة 1، 2، ... وبعدها القانون نفسه بيبدأ من مادة 1 تاني.
    /// لو الترقيم رجع لـ 1، المواد اللي قبله بتتسمى "إصدار 1"، "إصدار 2"، ... عشان ماتتلخبطش مع مواد القانون.
    /// </summary>
    private static List<LawArticle> MarkIssuanceArticles(List<LawArticle> articles)
    {
        if (articles.Count < 2)
        {
            return articles;
        }
        var restart = articles.FindIndex(1, a => a.Number == "1");
        if (restart <= 0 || articles[0].Number != "1")
        {
            return articles;
        }
        return articles.Select((a, i) => i < restart ? a with { Number = $"إصدار {a.Number}" } : a).ToList();
    }

    private static bool TryParseHeading(string line, out string number, out string rest)
    {
        var m = NumericHeading().Match(line);
        if (m.Success)
        {
            number = m.Groups["num"].Value;
            if (m.Groups["suffix"].Success)
            {
                number += " مكرر";
                if (m.Groups["letter"].Success)
                {
                    number += $" ({m.Groups["letter"].Value})";
                }
            }
            rest = m.Groups["rest"].Value.Trim();
            return true;
        }

        var normalized = ArabicNormalizer.NormalizeForSearch(line);
        var o = OrdinalHeading().Match(normalized);
        if (o.Success)
        {
            // بنرجّع الكلمة زي ما اتكتبت في النص الأصلي (مثلاً "الأولى")، مش الشكل الموحّد.
            var original = line.Trim().Trim('(', ')').Trim();
            var idx = original.IndexOf("مادة", StringComparison.Ordinal);
            number = (idx >= 0 ? original[(idx + "مادة".Length)..] : original).Trim().TrimEnd('-', ':', '.', ')').Trim();
            rest = "";
            return true;
        }

        number = rest = "";
        return false;
    }
}
