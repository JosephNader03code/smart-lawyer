using System.Text;

namespace SmartLawyer.Core.Arabic;

/// <summary>
/// توحيد النص العربي عشان البحث يلاقي الكلمة بأي شكل اتكتبت بيه.
/// </summary>
public static class ArabicNormalizer
{
    private const char Tatweel = 'ـ';

    /// <summary>
    /// تحويل الأرقام الهندية (٠-٩) والفارسية (۰-۹) لأرقام عادية، وتوحيد المسافات.
    /// آمن للعرض: مابيغيرش الحروف.
    /// </summary>
    public static string NormalizeDigits(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                _ => c,
            });
        }
        return sb.ToString();
    }

    /// <summary>
    /// توحيد كامل للفهرسة والبحث فقط (مش للعرض):
    /// حذف التشكيل والتطويل، أ/إ/آ/ٱ ← ا، ى ← ي، ة ← ه، ؤ ← و، ئ ← ي، الأرقام ← عادية، المسافات ← مسافة واحدة.
    /// </summary>
    public static string NormalizeForSearch(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var raw in NormalizeDigits(text))
        {
            if (IsDiacritic(raw) || raw == Tatweel)
            {
                continue;
            }

            var c = raw switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                'ؤ' => 'و',
                'ئ' => 'ي',
                _ => raw,
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                }
                lastWasSpace = true;
                continue;
            }

            sb.Append(char.ToLowerInvariant(c));
            lastWasSpace = false;
        }

        if (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }
        return sb.ToString();
    }

    // الفتحة والضمة والكسرة والسكون والشدة والتنوين، والألف الخنجرية.
    private static bool IsDiacritic(char c) => c is >= 'ً' and <= 'ْ' or 'ٰ';
}
