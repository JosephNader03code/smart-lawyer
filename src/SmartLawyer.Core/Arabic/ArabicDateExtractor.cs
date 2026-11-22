using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartLawyer.Core.Arabic;

/// <summary>تاريخ أو وقت لقيناه في النص، ومكانه، والنص الأصلي زي ما اتكتب.</summary>
public sealed record ExtractedDate(DateOnly Date, TimeOnly? Time, int Index, int Length, string RawText);

public sealed record ExtractedTime(TimeOnly Time, int Index, int Length, string RawText);

/// <summary>
/// استخراج التواريخ والأوقات من نص عربي بقواعد ثابتة (من غير AI):
/// "15/8/2025"، "2025-08-15"، "15 أغسطس 2025"، "١٥ اغسطس ٢٠٢٥"، "15 أغسطس سنة 2025"،
/// والأوقات زي "الساعة 8:00 مساءً" أو "الساعة 10:30 م". التاريخ اللي يومه أو شهره مش صالح بيتجاهل.
/// </summary>
public static partial class ArabicDateExtractor
{
    private static readonly Dictionary<string, int> Months = new()
    {
        ["يناير"] = 1, ["فبراير"] = 2, ["مارس"] = 3, ["ابريل"] = 4, ["مايو"] = 5, ["يونيو"] = 6,
        ["يوليو"] = 7, ["اغسطس"] = 8, ["سبتمبر"] = 9, ["اكتوبر"] = 10, ["نوفمبر"] = 11, ["ديسمبر"] = 12,
        // أسماء الشهور الشامية، بتظهر في بعض المستندات.
        ["كانون الثاني"] = 1, ["شباط"] = 2, ["اذار"] = 3, ["نيسان"] = 4, ["ايار"] = 5, ["حزيران"] = 6,
        ["تموز"] = 7, ["اب"] = 8, ["ايلول"] = 9, ["تشرين الاول"] = 10, ["تشرين الثاني"] = 11, ["كانون الاول"] = 12,
    };

    [GeneratedRegex(@"(?<!\d)(?:(?<d>\d{1,2})\s*[/\-.]\s*(?<m>\d{1,2})\s*[/\-.]\s*(?<y>\d{4})|(?<y2>\d{4})\s*[/\-.]\s*(?<m2>\d{1,2})\s*[/\-.]\s*(?<d2>\d{1,2}))(?!\d)")]
    private static partial Regex NumericDate();

    // بيشتغل على النص بعد التوحيد (أغسطس ← اغسطس). مواضع الحروف بتفضل زي ما هي لأن التوحيد هنا حرف بحرف.
    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})\s+(?:من\s+)?(?<month>كانون الثاني|كانون الاول|تشرين الاول|تشرين الثاني|[ء-ي]+)\s+(?:سنه\s+|لسنه\s+|عام\s+)?(?<y>\d{4})(?!\d)")]
    private static partial Regex WrittenDate();

    [GeneratedRegex(@"الساعه\s+(?<h>\d{1,2})(?:\s*[:.]\s*(?<min>\d{2}))?\s*(?<period>مساء|م\b|صباحا|صباح|ص\b|ظهرا|عصرا|ليلا)?")]
    private static partial Regex TimeOfDay();

    public static IReadOnlyList<ExtractedDate> ExtractDates(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var digits = ArabicNormalizer.NormalizeDigits(text);
        var normalized = NormalizeKeepingPositions(digits);
        var results = new List<ExtractedDate>();

        foreach (Match m in NumericDate().Matches(digits))
        {
            var (y, mo, d) = m.Groups["y"].Success
                ? (m.Groups["y"].Value, m.Groups["m"].Value, m.Groups["d"].Value)
                : (m.Groups["y2"].Value, m.Groups["m2"].Value, m.Groups["d2"].Value);
            if (TryDate(y, mo, d, out var date))
            {
                results.Add(new ExtractedDate(date, null, m.Index, m.Length, text.Substring(m.Index, m.Length)));
            }
        }

        foreach (Match m in WrittenDate().Matches(normalized))
        {
            if (Months.TryGetValue(m.Groups["month"].Value, out var month)
                && TryDate(m.Groups["y"].Value, month.ToString(CultureInfo.InvariantCulture), m.Groups["d"].Value, out var date))
            {
                results.Add(new ExtractedDate(date, null, m.Index, m.Length, text.Substring(m.Index, m.Length)));
            }
        }

        // ربط الوقت بالتاريخ لو جه بعده في نفس الجملة (خلال 60 حرف).
        var times = ExtractTimes(text);
        return results.OrderBy(r => r.Index).Select(r =>
        {
            var t = times.FirstOrDefault(x => x.Index >= r.Index + r.Length && x.Index - (r.Index + r.Length) <= 60);
            return t is null ? r : r with { Time = t.Time };
        }).ToList();
    }

    public static IReadOnlyList<ExtractedTime> ExtractTimes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = NormalizeKeepingPositions(ArabicNormalizer.NormalizeDigits(text));
        var results = new List<ExtractedTime>();
        foreach (Match m in TimeOfDay().Matches(normalized))
        {
            var hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
            var minute = m.Groups["min"].Success ? int.Parse(m.Groups["min"].Value, CultureInfo.InvariantCulture) : 0;
            var period = m.Groups["period"].Value;
            var pm = period is "مساء" or "م" or "عصرا" or "ليلا" || (period == "ظهرا" && hour < 12);
            if (pm && hour < 12)
            {
                hour += 12;
            }
            if (period is "صباحا" or "صباح" or "ص" && hour == 12)
            {
                hour = 0;
            }
            if (hour is >= 0 and < 24 && minute is >= 0 and < 60)
            {
                results.Add(new ExtractedTime(new TimeOnly(hour, minute), m.Index, m.Length, text.Substring(m.Index, m.Length)));
            }
        }
        return results;
    }

    private static bool TryDate(string y, string m, string d, out DateOnly date)
    {
        date = default;
        var year = int.Parse(y, CultureInfo.InvariantCulture);
        var month = int.Parse(m, CultureInfo.InvariantCulture);
        var day = int.Parse(d, CultureInfo.InvariantCulture);
        if (year is < 1900 or > 2100 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }
        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>توحيد حرف بحرف (من غير حذف) عشان مواضع المطابقة تفضل صالحة على النص الأصلي.</summary>
    private static string NormalizeKeepingPositions(string s) => string.Create(s.Length, s, static (span, src) =>
    {
        for (var i = 0; i < src.Length; i++)
        {
            span[i] = src[i] switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                // التشكيل والتنوين (زي ً في "مساءً") بيتحول لمسافة بدل ما يتحذف.
                >= 'ً' and <= 'ْ' => ' ',
                _ => src[i],
            };
        }
    });
}
