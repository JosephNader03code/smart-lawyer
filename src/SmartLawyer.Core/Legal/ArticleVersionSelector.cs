namespace SmartLawyer.Core.Legal;

/// <summary>نسخة من نص مادة كانت سارية من ValidFrom لحد ValidTo (null = لسه سارية).</summary>
public sealed record ArticleVersionPeriod(DateOnly ValidFrom, DateOnly? ValidTo);

public static class ArticleVersionSelector
{
    /// <summary>
    /// بيرجع رقم النسخة اللي كانت سارية في تاريخ الواقعة، أو null لو المادة ماكانتش موجودة وقتها.
    /// ValidTo يعني آخر يوم سريان (شامل).
    /// </summary>
    public static int? SelectIndex(IReadOnlyList<ArticleVersionPeriod> versions, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(versions);
        for (var i = 0; i < versions.Count; i++)
        {
            var v = versions[i];
            if (v.ValidFrom <= asOf && (v.ValidTo is null || asOf <= v.ValidTo))
            {
                return i;
            }
        }
        return null;
    }
}
