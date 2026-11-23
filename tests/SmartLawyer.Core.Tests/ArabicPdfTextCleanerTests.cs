using SmartLawyer.Core.Arabic;
using SmartLawyer.Core.Legal;

namespace SmartLawyer.Core.Tests;

public class ArabicPdfTextCleanerTests
{
    // عينة بنفس شكل النص اللي بيطلع من pdftotext: حروف Presentation Forms وعلامات اتجاه.
    private const string Extracted =
        "‫ﻣﺎدة ‪– 86‬ اﻟﺤﻘﻮق اﻟﺘﻲ ﺗﺮد ﻋﻠﻰ ﺷﻲء ﻏﻴﺮ ﻣﺎدي ﺗﻨﻈﻤﻬﺎ ﻗﻮاﻧﻴﻦ ﺧﺎﺻﺔ‪.‬‬\n" +
        "‫ﻣﺎدة ‪– 88‬ ﺗﻔﻘﺪ اﻷﻣﻮال اﻟﻌﺎﻣﺔ ﺻﻔﺘﻬﺎ اﻟﻌﺎﻣﺔ‬\n" +
        "ﺑﺈﻧﻬﺎء ﺗﺨﺼﻴﺼﻬﺎ ﻟﻠﻤﻨﻔﻌﺔ اﻟﻌﺎﻣﺔ\n";

    [Fact]
    public void Clean_ConvertsPresentationFormsAndRemovesDirectionMarks()
    {
        var clean = ArabicPdfTextCleaner.Clean(Extracted);

        Assert.Contains("الحقوق التي ترد على شيء غير مادي", clean);
        Assert.DoesNotContain('‪', clean);
        Assert.DoesNotContain("ﻣ", clean);
    }

    [Fact]
    public void Clean_MakesDashFirstHeadingsSplittable()
    {
        var articles = LawTextSplitter.Split(ArabicPdfTextCleaner.Clean(Extracted));

        Assert.Equal(["86", "88"], articles.Select(a => a.Number));
        Assert.Equal("الحقوق التي ترد على شيء غير مادي تنظمها قوانين خاصة.", articles[0].Text);
        Assert.Equal("تفقد الأموال العامة صفتها العامة\nبإنهاء تخصيصها للمنفعة العامة", articles[1].Text);
    }

    [Fact]
    public void Clean_HandlesRepeatedArticles() =>
        Assert.Equal("18 مكرر (أ)",
            LawTextSplitter.Split(ArabicPdfTextCleaner.Clean("مادة – 18 مكرراً (أ) نص المادة")).Single().Number);

    [Fact]
    public void Clean_HandlesParagraphNumberBeforeDashAndGluedText()
    {
        var a = LawTextSplitter.Split(ArabicPdfTextCleaner.Clean("مادة  (1) – 6النصوص المتعلقة بالأهلية تسري\n) (2فقرة تانية")).Single();
        Assert.Equal("6", a.Number);
        Assert.Equal("(1) النصوص المتعلقة بالأهلية تسري\n(2) فقرة تانية", a.Text);
    }

    [Fact]
    public void FixKaf_ConvertsMisencodedKafButKeepsRealAlefMaddaWords()
    {
        var (text, kept) = ArabicPdfTextCleaner.FixKafShownAsAlefMadda(
            "آان العقد آله باطلاً أمام المحاآم، والطرف الآخر والمنشآت والآن");

        Assert.Equal("كان العقد كله باطلاً أمام المحاكم، والطرف الآخر والمنشآت والآن", text);
        Assert.Equal(["الآخر", "والآن", "والمنشآت"], kept);
    }

    [Fact]
    public void Clean_DoesNotChangeAlefMaddaByItself() =>
        Assert.Contains("آان", ArabicPdfTextCleaner.Clean("آان"));
}
