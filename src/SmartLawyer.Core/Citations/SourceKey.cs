using System.Globalization;

namespace SmartLawyer.Core.Citations;

public enum SourceKind { Article, Ruling }

/// <summary>
/// مفتاح مصدر بيستخدمه النموذج في الاستشهاد:
/// [[article:{lawId}:{articleNumber}]] أو [[ruling:{appealNumber}:{judicialYear}]].
/// </summary>
public sealed record SourceKey(SourceKind Kind, string First, string Second)
{
    public static SourceKey Article(int lawId, string articleNumber) =>
        new(SourceKind.Article, lawId.ToString(CultureInfo.InvariantCulture), articleNumber.Trim());

    public static SourceKey Ruling(int appealNumber, int judicialYear) =>
        new(SourceKind.Ruling, appealNumber.ToString(CultureInfo.InvariantCulture), judicialYear.ToString(CultureInfo.InvariantCulture));

    public override string ToString() =>
        $"[[{(Kind == SourceKind.Article ? "article" : "ruling")}:{First}:{Second}]]";
}

/// <summary>مصدر النموذج قرأه فعلاً أثناء البحث (نتيجة tool).</summary>
public sealed record FetchedSource(SourceKey Key, string Title, string Text);
