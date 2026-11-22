using SmartLawyer.Core.Arabic;

namespace SmartLawyer.Core.Tests;

public class SearchQueryBuilderTests
{
    [Fact]
    public void BuildTsQuery_NormalizesAndDropsStopWords() =>
        Assert.Equal("شروط | الاخذ | بدفاتر | التجار | دليل | اثبات",
            SearchQueryBuilder.BuildTsQuery("ما هي شروط الأخذ بدفاتر التجار في دليل إثبات؟"));

    [Fact]
    public void BuildTsQuery_StripsOperatorsAndPunctuation() =>
        Assert.Equal("التعويض | الضرر", SearchQueryBuilder.BuildTsQuery("التعويض & !الضرر) | :*"));

    [Fact]
    public void BuildTsQuery_RemovesDuplicates() =>
        Assert.Equal("عقد", SearchQueryBuilder.BuildTsQuery("عقد، عقد"));

    [Fact]
    public void BuildTsQuery_ReturnsNullForOnlyStopWords() =>
        Assert.Null(SearchQueryBuilder.BuildTsQuery("في من على"));

    [Fact]
    public void BuildTsQuery_KeepsNumbers() =>
        Assert.Equal("الماده | 147", SearchQueryBuilder.BuildTsQuery("المادة ١٤٧"));
}
