using System.Globalization;
using System.Text.RegularExpressions;
using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Legal;

/// <summary>مرجع حكم: رقم الطعن والسنة القضائية، وتاريخ الجلسة لو موجود.</summary>
public sealed record RulingReference(int AppealNumber, int JudicialYear, DateOnly? SessionDate)
{
    public override string ToString() => $"الطعن رقم {AppealNumber} لسنة {JudicialYear} ق";
}

/// <summary>
/// استخراج مراجع الأحكام من النص، زي:
/// "الطعن رقم 1234 لسنة 85 ق"، "الطعن رقم ١٢٣٤ لسنة ٨٥ قضائية - جلسة 14/3/2016"، "طعن 55 س 70 ق".
/// </summary>
public static partial class RulingReferenceParser
{
    [GeneratedRegex(
        @"(?:ال)?طعن\s*(?:رقم\s*)?(?<no>\d+)\s*(?:لسنة|لسنه|س)\s*(?<year>\d+)\s*(?:ق(?:ضائية|ضائيه)?\b|ق)(?:[^\n\d]{0,20}?جلسة\s*(?<date>\d{1,4}\s*[/\-]\s*\d{1,2}\s*[/\-]\s*\d{1,4}))?")]
    private static partial Regex Reference();

    public static IReadOnlyList<RulingReference> FindAll(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = ArabicNormalizer.NormalizeDigits(text);
        var results = new List<RulingReference>();
        foreach (Match m in Reference().Matches(normalized))
        {
            var no = int.Parse(m.Groups["no"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(m.Groups["year"].Value, CultureInfo.InvariantCulture);
            var date = m.Groups["date"].Success ? ParseDate(m.Groups["date"].Value) : null;
            results.Add(new RulingReference(no, year, date));
        }
        return results;
    }

    /// <summary>بيقبل يوم/شهر/سنة أو سنة/شهر/يوم. بيرجع null لو التاريخ مش صالح.</summary>
    public static DateOnly? ParseDate(string value)
    {
        var parts = Regex.Split(ArabicNormalizer.NormalizeDigits(value).Trim(), @"\s*[/\-]\s*");
        if (parts.Length != 3)
        {
            return null;
        }
        var n = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        var (year, month, day) = parts[0].Length == 4 ? (n[0], n[1], n[2]) : (n[2], n[1], n[0]);
        if (year < 1800 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }
        return new DateOnly(year, month, day);
    }
}
