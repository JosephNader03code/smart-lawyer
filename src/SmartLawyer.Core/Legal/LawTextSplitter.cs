using System.Text.RegularExpressions;
using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Legal;

/// <summary>
/// تقسيم نص قانون لمواد. بيتعرف على عناوين زي:
/// "مادة 1"، "المادة (147)"، "مادة ١٤٧ مكرراً"، "مادة 18 مكرر (أ)"، و"مادة 1 -".
/// العنوان لازم يبقى في أول السطر عشان مايتلخبطش مع "طبقاً للمادة 5" في نص مادة تانية.
/// </summary>
public static partial class LawTextSplitter
{
    [GeneratedRegex(
        @"^[ \t]*(?:ال)?مادة[ \t]*\(?[ \t]*(?<num>\d+)[ \t]*\)?(?<suffix>[ \t]*مكرر[اً]*(?:[ \t]*\(?[ \t]*(?<letter>[أ-ي])[ \t]*\)?)?)?[ \t]*[-–:.]?[ \t]*$",
        RegexOptions.Multiline)]
    private static partial Regex ArticleHeading();

    public static IReadOnlyList<LawArticle> Split(string lawText)
    {
        ArgumentNullException.ThrowIfNull(lawText);
        var text = ArabicNormalizer.NormalizeDigits(lawText).Replace("\r\n", "\n");
        var matches = ArticleHeading().Matches(text);
        var articles = new List<LawArticle>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var start = m.Index + m.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var body = text[start..end].Trim();

            var number = m.Groups["num"].Value;
            if (m.Groups["suffix"].Success)
            {
                number += " مكرر";
                if (m.Groups["letter"].Success)
                {
                    number += $" ({m.Groups["letter"].Value})";
                }
            }

            articles.Add(new LawArticle(number, body));
        }

        return articles;
    }
}
