namespace SmartLawyer.Core.Deadlines;

/// <summary>
/// قاعدة ميعاد إجرائي. القيم القانونية نفسها (عدد الأيام، طريقة الحساب) بتيجي من ملف إعدادات
/// ولازم محامي يراجعها ويعلّم عليها Verified=true قبل ما النتيجة تظهر للمحامين من غير تحذير.
/// </summary>
public sealed record DeadlineRule(
    string Id,
    string Name,
    int PeriodDays,
    string LegalBasis,
    bool ExcludeStartDay = true,
    bool ExtendIfLastDayIsHoliday = true,
    bool Verified = false);

public sealed record DeadlineResult(
    DeadlineRule Rule,
    DateOnly StartDate,
    DateOnly LastDay,
    DateOnly? ExtendedFrom,
    IReadOnlyList<string> Warnings);

/// <summary>حساب ثابت للمواعيد (من غير AI): بداية الميعاد + المدة، وترحيل آخر يوم لو كان عطلة.</summary>
public static class DeadlineCalculator
{
    public static DeadlineResult Compute(
        DeadlineRule rule, DateOnly startDate, ISet<DateOnly> holidays, ISet<DayOfWeek> weeklyRestDays)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(holidays);
        ArgumentNullException.ThrowIfNull(weeklyRestDays);
        if (rule.PeriodDays <= 0)
        {
            throw new ArgumentException("مدة الميعاد لازم تكون أكبر من صفر", nameof(rule));
        }

        // لو يوم البداية مابيتحسبش، اليوم الأول هو اللي بعده، وآخر يوم = البداية + المدة.
        var lastDay = rule.ExcludeStartDay ? startDate.AddDays(rule.PeriodDays) : startDate.AddDays(rule.PeriodDays - 1);
        DateOnly? extendedFrom = null;

        if (rule.ExtendIfLastDayIsHoliday)
        {
            var original = lastDay;
            var guard = 0;
            while (holidays.Contains(lastDay) || weeklyRestDays.Contains(lastDay.DayOfWeek))
            {
                lastDay = lastDay.AddDays(1);
                if (++guard > 60)
                {
                    throw new InvalidOperationException("قائمة العطلات فيها أكتر من 60 يوم متتالي؛ راجع الإعدادات.");
                }
            }
            if (lastDay != original)
            {
                extendedFrom = original;
            }
        }

        var warnings = new List<string>();
        if (!rule.Verified)
        {
            warnings.Add("قاعدة الميعاد دي لسه مااتراجعتش من محامي. اتأكد من النص القانوني قبل الاعتماد عليها.");
        }
        if (holidays.Count == 0)
        {
            warnings.Add("مفيش قائمة عطلات رسمية متسجلة، فممكن آخر يوم يكون عطلة ومااتحسبش ترحيله.");
        }
        return new DeadlineResult(rule, startDate, lastDay, extendedFrom, warnings);
    }
}
