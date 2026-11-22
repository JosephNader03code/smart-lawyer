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
}
