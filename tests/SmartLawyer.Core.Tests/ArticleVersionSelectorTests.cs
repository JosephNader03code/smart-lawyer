using SmartLawyer.Core.Legal;

namespace SmartLawyer.Core.Tests;

public class ArticleVersionSelectorTests
{
    private static readonly ArticleVersionPeriod[] Versions =
    [
        new(new DateOnly(1949, 10, 15), new DateOnly(2020, 6, 30)),
        new(new DateOnly(2020, 7, 1), null),
    ];

    [Theory]
    [InlineData(2010, 1, 1, 0)]
    [InlineData(2020, 6, 30, 0)] // آخر يوم في النسخة القديمة
    [InlineData(2020, 7, 1, 1)]  // أول يوم في النسخة الجديدة
    [InlineData(2025, 8, 15, 1)]
    public void SelectIndex_ReturnsVersionInForceOnDate(int y, int m, int d, int expected) =>
        Assert.Equal(expected, ArticleVersionSelector.SelectIndex(Versions, new DateOnly(y, m, d)));

    [Fact]
    public void SelectIndex_ReturnsNullBeforeArticleExisted() =>
        Assert.Null(ArticleVersionSelector.SelectIndex(Versions, new DateOnly(1900, 1, 1)));
}
