using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Contradictions;

/// <summary>مطابقة الأسماء العربية بعد التوحيد بـ Jaro-Winkler (1 = متطابقين).</summary>
public static class NameMatcher
{
    public const double DefaultThreshold = 0.92;

    public static bool AreSame(string a, string b, double threshold = DefaultThreshold) => Similarity(a, b) >= threshold;

    public static double Similarity(string a, string b)
    {
        var x = Strip(a);
        var y = Strip(b);
        if (x.Length == 0 || y.Length == 0)
        {
            return 0;
        }
        return x == y ? 1 : JaroWinkler(x, y);
    }

    // شيل الألقاب الشائعة عشان "الأستاذ أحمد محمد" و"أحمد محمد" يتطابقوا.
    private static readonly string[] Titles = ["السيد", "السيده", "الاستاذ", "الاستاذه", "المستشار", "الدكتور", "د", "المهندس", "م", "الحاج"];

    private static string Strip(string name)
    {
        var words = SearchQueryBuilder.Tokenize(name).Where(w => !Titles.Contains(w));
        return string.Join(' ', words);
    }

    public static double JaroWinkler(string s, string t)
    {
        var range = Math.Max(0, Math.Max(s.Length, t.Length) / 2 - 1);
        var sMatched = new bool[s.Length];
        var tMatched = new bool[t.Length];
        var matches = 0;
        for (var i = 0; i < s.Length; i++)
        {
            for (int j = Math.Max(0, i - range), end = Math.Min(t.Length - 1, i + range); j <= end; j++)
            {
                if (!tMatched[j] && s[i] == t[j])
                {
                    sMatched[i] = tMatched[j] = true;
                    matches++;
                    break;
                }
            }
        }
        if (matches == 0)
        {
            return 0;
        }

        var transpositions = 0;
        for (int i = 0, k = 0; i < s.Length; i++)
        {
            if (!sMatched[i])
            {
                continue;
            }
            while (!tMatched[k])
            {
                k++;
            }
            if (s[i] != t[k])
            {
                transpositions++;
            }
            k++;
        }

        double m = matches;
        var jaro = (m / s.Length + m / t.Length + (m - transpositions / 2.0) / m) / 3;
        var prefix = 0;
        while (prefix < Math.Min(4, Math.Min(s.Length, t.Length)) && s[prefix] == t[prefix])
        {
            prefix++;
        }
        return jaro + prefix * 0.1 * (1 - jaro);
    }
}
