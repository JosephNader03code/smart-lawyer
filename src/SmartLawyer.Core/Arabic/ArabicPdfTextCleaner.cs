using System.Text;
using System.Text.RegularExpressions;

namespace SmartLawyer.Core.Arabic;

/// <summary>
/// تنضيف النص العربي المستخرج من PDF قبل تقسيمه لمواد:
/// - تحويل أشكال الحروف العربية (Presentation Forms زي "ﻣﺎدة") لحروف عادية (NFKC).
/// - حذف علامات اتجاه الكتابة المخفية (LRE/RLE/PDF/LRM/RLM...).
/// - توحيد عنوان المادة لما الرقم يطلع بعد الشرطة: "مادة – 86 النص" أو "مادة (1) – 6النص"
///   ← سطر "مادة 86" وبعده النص (ورقم الفقرة لو موجود).
/// تصليح خطأ "ك" المكتوبة "آ" مش جزء من Clean لأنه بيغيّر حروف، وبيتطلب صراحةً للملفات اللي فيها المشكلة.
/// </summary>
public static partial class ArabicPdfTextCleaner
{
    [GeneratedRegex("[\\u200E\\u200F\\u202A-\\u202E\\u2066-\\u2069\\uFEFF\\f]")]
    private static partial Regex DirectionMarks();

    [GeneratedRegex("[ \\t\\u00A0]+")]
    private static partial Regex Spaces();

    // رقم الفقرة المعكوس بسبب اتجاه الكتابة: ") (2النص" ← "(2) النص".
    [GeneratedRegex(@"^\)\s*\((?<n>\d+)\s*", RegexOptions.Multiline)]
    private static partial Regex MirroredParagraphNumber();

    // "مادة – 86 النص" أو "مادة - 86 مكرراً (أ) النص" (بعد ما الرقم اتنقل بسبب اتجاه الكتابة).
    [GeneratedRegex(@"^\s*(?<prefix>(?:ال)?مادة)\s*(?:\(\s*(?<para>\d+)\s*\)\s*)?[-–—]\s*(?<num>\d+)(?<suffix>\s*مكرر[اً]*(?:\s*\(\s*[أ-ي]\s*\))?)?\s*(?<rest>.*)$", RegexOptions.Multiline)]
    private static partial Regex DashFirstHeading();

    public static string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var s = text.Normalize(NormalizationForm.FormKC);
        s = DirectionMarks().Replace(s, "");
        s = ArabicNormalizer.NormalizeDigits(s);
        s = string.Join('\n', s.Replace("\r\n", "\n").Split('\n').Select(l => Spaces().Replace(l, " ").Trim()));
        s = MirroredParagraphNumber().Replace(s, m => $"({m.Groups["n"].Value}) ");
        s = DashFirstHeading().Replace(s, m =>
        {
            var heading = $"مادة {m.Groups["num"].Value}{m.Groups["suffix"].Value.TrimEnd()}";
            var rest = m.Groups["rest"].Value.Trim();
            if (m.Groups["para"].Success)
            {
                rest = $"({m.Groups["para"].Value}) {rest}";
            }
            return rest.Length == 0 ? heading : $"{heading}\n{rest}";
        });
        return s;
    }

    [GeneratedRegex(@"[\p{L}\p{Mn}]+")]
    private static partial Regex Word();

    // كلمات فيها "آ" فعلاً (من غير السوابق و/ف/ب/ل/ال). أي "آ" في كلمة تانية بتتعامل على إنها "ك" مكتوبة غلط.
    private static readonly HashSet<string> RealAlefMaddaWords =
    [
        "آخر", "آخرين", "آخرون", "آخره", "آخرة", "آخرها", "آخرهم", "آخري", "آتي", "آتية", "آتيه", "آتيين",
        "آثار", "آثاره", "آثارها", "آثارهم", "آن", "آنذاك", "آنفا", "آنفاً", "آنف", "آنفة", "آمن", "آمنة", "آمنا",
        "آباء", "آبائهم", "آبار", "آلة", "آلات", "آلاف", "آلية", "آليات", "آراء", "آرائهم", "آجال", "آجل", "آجلة",
        "مآل", "مآخذ", "منشآت", "منشأة", "مكافآت", "قرآن", "مرآة", "مآثر", "ملاجئ",
    ];

    private static readonly string[] Prefixes = ["وبال", "فبال", "وال", "فال", "بال", "كال", "لل", "ال", "و", "ف", "ب", "ل"];

    /// <summary>
    /// لملفات PDF بخطوط قديمة بتكتب "ك" كأنها "آ" (زي "آان" بدل "كان"، "المحاآم" بدل "المحاكم").
    /// بيحوّل كل "آ" لـ "ك" ما عدا الكلمات اللي فيها "آ" فعلاً (زي "الآخر"، "الآن"، "المنشآت").
    /// بيرجّع النص والكلمات اللي اتسابت زي ما هي عشان تتراجع.
    /// </summary>
    public static (string Text, IReadOnlyList<string> KeptWords) FixKafShownAsAlefMadda(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var kept = new SortedSet<string>(StringComparer.Ordinal);
        var fixedText = Word().Replace(text, m =>
        {
            var w = m.Value;
            if (!w.Contains('آ'))
            {
                return w;
            }
            if (IsRealAlefMaddaWord(w))
            {
                kept.Add(w);
                return w;
            }
            return w.Replace('آ', 'ك');
        });
        return (fixedText, kept.ToList());
    }

    private static bool IsRealAlefMaddaWord(string word)
    {
        if (RealAlefMaddaWords.Contains(word))
        {
            return true;
        }
        foreach (var p in Prefixes)
        {
            if (word.Length > p.Length && word.StartsWith(p, StringComparison.Ordinal)
                && RealAlefMaddaWords.Contains(word[p.Length..]))
            {
                return true;
            }
        }
        return false;
    }
}
