using SmartLawyer.Core.Legal;

namespace SmartLawyer.Core.Tests;

public class LawTextSplitterTests
{
    [Fact]
    public void Split_FindsArticlesInDifferentHeadingStyles()
    {
        const string text = """
            الباب الأول
            مادة 1
            نص المادة الأولى.
            المادة (2)
            نص المادة الثانية
            على سطرين.
            مادة ٣ -
            نص المادة الثالثة.
            """;

        var articles = LawTextSplitter.Split(text);

        Assert.Equal(["1", "2", "3"], articles.Select(a => a.Number));
        Assert.Equal("نص المادة الأولى.", articles[0].Text);
        Assert.Equal("نص المادة الثانية\nعلى سطرين.", articles[1].Text);
    }

    [Fact]
    public void Split_HandlesRepeatedArticles()
    {
        const string text = """
            مادة 18 مكرراً
            نص.
            مادة 18 مكرر (أ)
            نص تاني.
            """;

        var articles = LawTextSplitter.Split(text);

        Assert.Equal(["18 مكرر", "18 مكرر (أ)"], articles.Select(a => a.Number));
    }

    [Fact]
    public void Split_IgnoresArticleReferencesInsideText()
    {
        const string text = """
            مادة 5
            مع مراعاة حكم المادة 3 من هذا القانون، يلتزم المدين بالتعويض.
            """;

        var articles = LawTextSplitter.Split(text);

        var only = Assert.Single(articles);
        Assert.Contains("المادة 3", only.Text);
    }

    [Fact]
    public void Split_ReturnsEmptyWhenNoHeadings() =>
        Assert.Empty(LawTextSplitter.Split("نص من غير مواد"));

    [Fact]
    public void Split_ReadsTextOnSameLineAsHeading()
    {
        const string text = """
            مادة 1 - يلغى القانون رقم 10 لسنة 1990.
            مادة (2): ينشر هذا القانون في الجريدة الرسمية،
            ويعمل به من اليوم التالي.
            """;

        var articles = LawTextSplitter.Split(text);

        Assert.Equal(["1", "2"], articles.Select(a => a.Number));
        Assert.Equal("يلغى القانون رقم 10 لسنة 1990.", articles[0].Text);
        Assert.Equal("ينشر هذا القانون في الجريدة الرسمية،\nويعمل به من اليوم التالي.", articles[1].Text);
    }

    [Fact]
    public void Split_HandlesOrdinalIssuanceArticles()
    {
        const string text = """
            المادة الأولى
            يعمل بأحكام القانون المرافق.
            المادة الثانية
            ينشر هذا القانون.
            مادة 1
            نص المادة الأولى من القانون.
            """;

        var articles = LawTextSplitter.Split(text);

        Assert.Equal(["الأولى", "الثانية", "1"], articles.Select(a => a.Number));
    }

    [Fact]
    public void Split_DoesNotTreatSentenceStartingWithArticleAsHeading()
    {
        const string text = """
            مادة 4
            يعاقب المخالف.
            المادة 3 من هذا القانون تسري على الحالات السابقة.
            """;

        var only = Assert.Single(LawTextSplitter.Split(text));
        Assert.Contains("المادة 3 من هذا القانون", only.Text);
    }
}
