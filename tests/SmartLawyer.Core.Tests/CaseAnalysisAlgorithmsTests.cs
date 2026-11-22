using SmartLawyer.Core.Arabic;
using SmartLawyer.Core.Contradictions;
using SmartLawyer.Core.Deadlines;
using SmartLawyer.Core.Timeline;

namespace SmartLawyer.Core.Tests;

public class ArabicDateExtractorTests
{
    [Theory]
    [InlineData("تم التوقيع بتاريخ 10/3/2025 بالقاهرة", 2025, 3, 10)]
    [InlineData("بتاريخ ١٠/٣/٢٠٢٥", 2025, 3, 10)]
    [InlineData("بتاريخ 2025-03-10", 2025, 3, 10)]
    [InlineData("حرر في 15 أغسطس 2025", 2025, 8, 15)]
    [InlineData("حرر في ١٥ اغسطس سنة ٢٠٢٥", 2025, 8, 15)]
    [InlineData("يوم 3 من تموز 2024", 2024, 7, 3)]
    public void ExtractDates_FindsDifferentFormats(string text, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), Assert.Single(ArabicDateExtractor.ExtractDates(text)).Date);

    [Fact]
    public void ExtractDates_KeepsOriginalTextAndOrder()
    {
        const string text = "العقد بتاريخ 10/3/2025 والإخطار في ١٣ أغسطس ٢٠٢٥";
        var dates = ArabicDateExtractor.ExtractDates(text);

        Assert.Equal([new DateOnly(2025, 3, 10), new DateOnly(2025, 8, 13)], dates.Select(d => d.Date));
        Assert.Equal("١٣ أغسطس ٢٠٢٥", dates[1].RawText);
        Assert.Equal(dates[1].RawText, text.Substring(dates[1].Index, dates[1].Length));
    }

    [Theory]
    [InlineData("31/2/2025")]
    [InlineData("10/13/2025")]
    [InlineData("المادة 15 من القانون 2025")] // "القانون" مش شهر
    [InlineData("رقم 123/4567")]
    public void ExtractDates_IgnoresInvalidOrNonDates(string text) =>
        Assert.Empty(ArabicDateExtractor.ExtractDates(text));

    [Theory]
    [InlineData("في تمام الساعة 8:00 مساءً", 20, 0)]
    [InlineData("الساعة ١٠:٣٠ م", 22, 30)]
    [InlineData("الساعة 9 صباحاً", 9, 0)]
    [InlineData("الساعة 12 صباحا", 0, 0)]
    [InlineData("الساعة 14:15", 14, 15)]
    public void ExtractTimes_Handles12And24HourFormats(string text, int h, int m) =>
        Assert.Equal(new TimeOnly(h, m), Assert.Single(ArabicDateExtractor.ExtractTimes(text)).Time);

    [Fact]
    public void ExtractDates_AttachesFollowingTime() =>
        Assert.Equal(new TimeOnly(22, 30),
            Assert.Single(ArabicDateExtractor.ExtractDates("وصلت الشحنة يوم 28/8/2025 الساعة 10:30 مساءً")).Time);
}

public class SimpleTemporalNetworkTests
{
    [Fact]
    public void ConsistentTimeline_HasNoConflict()
    {
        var stn = new SimpleTemporalNetwork();
        stn.AddKnownTime("incident", new(2025, 8, 15), new(2025, 8, 15, 23, 59, 0), "محضر الشرطة");
        stn.AddKnownTime("notice", new(2025, 8, 17), new(2025, 8, 17, 23, 59, 0), "خطاب الإخطار");
        stn.AddMustFollow("incident", "notice", "الإخطار عن حادث لازم يكون بعده");

        Assert.Null(stn.FindConflict());
    }

    [Fact]
    public void NoticeBeforeIncident_IsDetectedWithItsCause()
    {
        // نفس مثال الواجهة: الإخطار 13 أغسطس والحادث 15 أغسطس.
        var stn = new SimpleTemporalNetwork();
        stn.AddKnownTime("incident", new(2025, 8, 15), new(2025, 8, 15, 23, 59, 0), "محضر الشرطة");
        stn.AddKnownTime("notice", new(2025, 8, 13), new(2025, 8, 13, 23, 59, 0), "خطاب الإخطار");
        stn.AddMustFollow("incident", "notice", "الإخطار عن حادث لازم يكون بعده");

        var conflict = stn.FindConflict();

        Assert.NotNull(conflict);
        Assert.Contains("incident", conflict.Cycle);
        Assert.Contains("notice", conflict.Cycle);
        Assert.Contains(conflict.Constraints, c => c.Reason == "الإخطار عن حادث لازم يكون بعده");
        // أقل فرق ممكن = من آخر لحظة يوم 13 لأول لحظة يوم 15.
        Assert.Equal(TimeSpan.FromDays(1).TotalMinutes + 1, conflict.ViolationMinutes, precision: 3);
    }

    [Fact]
    public void RelativeConstraints_CanConflictWithoutKnownDates()
    {
        var stn = new SimpleTemporalNetwork();
        stn.AddConstraint(new("a", "b", 2 * 24 * 60, 2 * 24 * 60, "ب بعد أ بيومين"));
        stn.AddConstraint(new("b", "c", 24 * 60, 24 * 60, "ج بعد ب بيوم"));
        stn.AddConstraint(new("a", "c", 0, 24 * 60, "ج خلال يوم من أ"));

        var conflict = stn.FindConflict();

        Assert.NotNull(conflict);
        Assert.Equal(2 * 24 * 60, conflict.ViolationMinutes, precision: 3);
    }

    [Fact]
    public void InvalidConstraint_IsRejected() =>
        Assert.Throws<ArgumentException>(() => new SimpleTemporalNetwork().AddConstraint(new("a", "b", 10, 5, "غلط")));
}

public class ContradictionTests
{
    [Theory]
    [InlineData("أحمد محمد علي", "احمد محمد على", true)]
    [InlineData("الأستاذ أحمد محمد", "أحمد محمد", true)]
    [InlineData("أحمد محمد", "محمود حسن", false)]
    public void NameMatcher_HandlesSpellingVariantsAndTitles(string a, string b, bool same) =>
        Assert.Equal(same, NameMatcher.AreSame(a, b));

    [Fact]
    public void JaroWinkler_KnownValue() =>
        Assert.Equal(0.961, NameMatcher.JaroWinkler("MARTHA", "MARHTA"), precision: 3);

    private static Fact F(string id, string subject, string predicate, DateTime? time, decimal? amount, string doc) =>
        new(id, subject, predicate, time, amount, doc, "صفحة 1");

    [Fact]
    public void Detect_FindsTimeDifferenceBetweenDocuments()
    {
        // نفس مثال الواجهة: الشاهد 8:00 مساءً والمحضر 10:30 مساءً.
        var facts = new[]
        {
            F("1", "شركة الأمل", "delivery", new DateTime(2025, 8, 28, 20, 0, 0), null, "شهادة الشاهد"),
            F("2", "شركه الامل", "delivery", new DateTime(2025, 8, 28, 22, 30, 0), null, "محضر الاستلام"),
        };

        var c = Assert.Single(FactContradictionDetector.Detect(facts, TimeSpan.FromMinutes(30)));

        Assert.Equal(ContradictionKind.Time, c.Kind);
        Assert.Equal(150, c.Difference);
    }

    [Fact]
    public void Detect_FindsAmountDifference_AndIgnoresWithinTolerance()
    {
        var facts = new[]
        {
            F("1", "المجموعة الوطنية", "payment", null, 1_250_000m, "الفاتورة"),
            F("2", "المجموعة الوطنية", "payment", null, 1_500_000m, "صحيفة الدعوى"),
            F("3", "المجموعة الوطنية", "delivery", new DateTime(2025, 8, 28, 20, 0, 0), null, "الفاتورة"),
            F("4", "المجموعة الوطنية", "delivery", new DateTime(2025, 8, 28, 20, 10, 0), null, "محضر الاستلام"),
        };

        var c = Assert.Single(FactContradictionDetector.Detect(facts, TimeSpan.FromMinutes(30)));

        Assert.Equal((ContradictionKind.Amount, 250_000d), (c.Kind, c.Difference));
    }

    [Fact]
    public void Detect_IgnoresSameDocumentDifferentPeopleAndDifferentEvents()
    {
        var t1 = new DateTime(2025, 8, 28, 20, 0, 0);
        var t2 = t1.AddHours(3);
        var facts = new[]
        {
            F("1", "أحمد محمد", "delivery", t1, null, "محضر"),
            F("2", "أحمد محمد", "delivery", t2, null, "محضر"),         // نفس المستند
            F("3", "محمود حسن", "delivery", t2, null, "شهادة"),        // شخص تاني
            F("4", "أحمد محمد", "payment", t2, null, "شهادة"),         // حدث تاني
        };

        Assert.Empty(FactContradictionDetector.Detect(facts, TimeSpan.FromMinutes(30)));
    }
}

public class DeadlineCalculatorTests
{
    // قاعدة تجريبية للاختبار (مش ميعاد قانوني حقيقي).
    private static readonly DeadlineRule Rule = new("test", "ميعاد تجريبي", 10, "قاعدة تجريبية");
    private static readonly HashSet<DayOfWeek> Friday = [DayOfWeek.Friday];

    [Fact]
    public void Compute_ExcludesStartDayByDefault()
    {
        // 2025-08-04 يوم اتنين، +10 = 2025-08-14 خميس.
        var r = DeadlineCalculator.Compute(Rule, new DateOnly(2025, 8, 4), new HashSet<DateOnly> { new(2026, 1, 7) }, Friday);
        Assert.Equal(new DateOnly(2025, 8, 14), r.LastDay);
        Assert.Null(r.ExtendedFrom);
    }

    [Fact]
    public void Compute_ExtendsWhenLastDayIsRestDayOrHoliday()
    {
        // 2025-08-05 + 10 = 2025-08-15 جمعة، و16 عطلة رسمية (تجريبية) ← 17.
        var holidays = new HashSet<DateOnly> { new(2025, 8, 16) };
        var r = DeadlineCalculator.Compute(Rule, new DateOnly(2025, 8, 5), holidays, Friday);

        Assert.Equal(new DateOnly(2025, 8, 17), r.LastDay);
        Assert.Equal(new DateOnly(2025, 8, 15), r.ExtendedFrom);
    }

    [Fact]
    public void Compute_WarnsForUnverifiedRulesAndMissingHolidays()
    {
        var r = DeadlineCalculator.Compute(Rule, new DateOnly(2025, 8, 4), new HashSet<DateOnly>(), Friday);
        Assert.Equal(2, r.Warnings.Count);

        var verified = DeadlineCalculator.Compute(Rule with { Verified = true }, new DateOnly(2025, 8, 4),
            new HashSet<DateOnly> { new(2025, 1, 7) }, Friday);
        Assert.Empty(verified.Warnings);
    }

    [Fact]
    public void Compute_CanIncludeStartDay() =>
        Assert.Equal(new DateOnly(2025, 8, 13),
            DeadlineCalculator.Compute(Rule with { ExcludeStartDay = false, ExtendIfLastDayIsHoliday = false },
                new DateOnly(2025, 8, 4), new HashSet<DateOnly>(), Friday).LastDay);
}
