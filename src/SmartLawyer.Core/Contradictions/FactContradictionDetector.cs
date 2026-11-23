namespace SmartLawyer.Core.Contradictions;

/// <summary>
/// واقعة صغيرة من مستند: (مين، عمل إيه) ومعاها وقت أو مبلغ. Predicate لازم يكون مفتاح موحّد
/// (زي "delivery" أو "payment") عشان الوقائع اللي عن نفس الحدث تتقارن.
/// </summary>
public sealed record Fact(
    string Id, string Subject, string Predicate, DateTime? Time, decimal? Amount, string SourceDocument, string SourceLocation);

public enum ContradictionKind { Time, Amount }

public sealed record FactContradiction(ContradictionKind Kind, Fact First, Fact Second, double Difference, string Explanation);

/// <summary>
/// كشف التناقضات الرقمية والزمنية بين المستندات بحساب ثابت.
/// بنقارن بس الوقائع اللي عن نفس النوع من الأحداث (blocking بالـ predicate)، ومن مستندات مختلفة:
/// - الأوقات: لو نفس الجهة (مطابقة أسماء) أو نفس اليوم، لأن نفس النوع من الوقائع في نفس اليوم والقضية غالباً نفس الواقعة
///   حتى لو كل مستند سمّى صاحبها بشكل مختلف (الشاهد، السائق، الشركة...).
/// - المبالغ: لازم نفس الجهة، لأن مبالغ مختلفة لجهات مختلفة طبيعية.
/// </summary>
public static class FactContradictionDetector
{
    public static IReadOnlyList<FactContradiction> Detect(
        IReadOnlyList<Fact> facts, TimeSpan timeTolerance, decimal amountTolerance = 0m)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var results = new List<FactContradiction>();

        foreach (var block in facts.GroupBy(f => f.Predicate))
        {
            var items = block.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    var (a, b) = (items[i], items[j]);
                    if (a.SourceDocument == b.SourceDocument)
                    {
                        continue;
                    }
                    var sameSubject = NameMatcher.AreSame(a.Subject, b.Subject);

                    if (a.Time is { } ta && b.Time is { } tb && (sameSubject || ta.Date == tb.Date))
                    {
                        var diff = Math.Abs((tb - ta).TotalMinutes);
                        if (diff > timeTolerance.TotalMinutes)
                        {
                            results.Add(new(ContradictionKind.Time, a, b, diff,
                                $"«{a.SourceDocument}» و«{b.SourceDocument}» مختلفين في وقت نفس الواقعة بفارق {diff:0} دقيقة."));
                        }
                    }

                    if (sameSubject && a.Amount is { } ma && b.Amount is { } mb && Math.Abs(ma - mb) > amountTolerance)
                    {
                        results.Add(new(ContradictionKind.Amount, a, b, (double)Math.Abs(ma - mb),
                            $"«{a.SourceDocument}» و«{b.SourceDocument}» مختلفين في مبلغ نفس الواقعة ({ma:N2} مقابل {mb:N2})."));
                    }
                }
            }
        }

        return results.OrderByDescending(r => r.Difference).ToList();
    }
}
