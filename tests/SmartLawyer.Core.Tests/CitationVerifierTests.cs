using SmartLawyer.Core.Citations;

namespace SmartLawyer.Core.Tests;

public class CitationVerifierTests
{
    private static readonly Dictionary<SourceKey, FetchedSource> Fetched = new()
    {
        [SourceKey.Article(12, "147")] = new(SourceKey.Article(12, "147"), "القانون المدني — مادة 147",
            "العقد شريعة المتعاقدين، فلا يجوز نقضه ولا تعديله إلا باتفاق الطرفين."),
        [SourceKey.Article(12, "18 مكرر (أ)")] = new(SourceKey.Article(12, "18 مكرر (أ)"), "مادة 18 مكرر (أ)", "نص تجريبي."),
        [SourceKey.Ruling(1234, 85)] = new(SourceKey.Ruling(1234, 85), "الطعن رقم 1234 لسنة 85 ق",
            "الإخطار بالقوة القاهرة شرط للإعفاء من التعويض."),
    };

    [Fact]
    public void Parse_ReadsArticleAndRulingCitationsWithQuotes()
    {
        var citations = CitationVerifier.Parse(
            "العقد ملزم [[article:12:147]] «العقد شريعة المتعاقدين» وده اتأكد في [[ruling:١٢٣٤:٨٥]].");

        Assert.Equal(2, citations.Count);
        Assert.Equal(SourceKey.Article(12, "147"), citations[0].Key);
        Assert.Equal("العقد شريعة المتعاقدين", citations[0].Quote);
        Assert.Equal(SourceKey.Ruling(1234, 85), citations[1].Key);
        Assert.Null(citations[1].Quote);
    }

    [Fact]
    public void Verify_AcceptsGroundedAnswer()
    {
        var result = CitationVerifier.Verify(
            "لا يجوز تعديل العقد بإرادة منفردة [[article:12:147]] «فلا يجوز نقضه ولا تعديله إلا باتفاق الطرفين». " +
            "والإخطار شرط [[ruling:1234:85]] «الاخطار بالقوه القاهره شرط».", Fetched);

        Assert.True(result.IsValid, string.Join("\n", result.Problems));
    }

    [Fact]
    public void Verify_AcceptsArticleNumbersWithSuffix() =>
        Assert.True(CitationVerifier.Verify("نص [[article:12:18 مكرر (أ)]]", Fetched).IsValid);

    [Fact]
    public void Verify_RejectsAnswerWithoutCitations()
    {
        var result = CitationVerifier.Verify("العقد شريعة المتعاقدين.", Fetched);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Verify_RejectsSourceThatWasNotFetched()
    {
        var result = CitationVerifier.Verify("طبقاً [[article:12:999]] و[[ruling:1:1]]", Fetched);
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Problems.Count);
    }

    [Fact]
    public void Verify_RejectsInventedQuote()
    {
        var result = CitationVerifier.Verify("[[article:12:147]] «يجوز تعديل العقد بإرادة منفردة»", Fetched);
        Assert.False(result.IsValid);
        Assert.Contains("الاقتباس", result.Problems.Single());
    }

    [Fact]
    public void Verify_ChecksQuotesPlacedBeforeTheCitation()
    {
        Assert.True(CitationVerifier.Verify("**«العقد شريعة المتعاقدين»**[[article:12:147]]", Fetched).IsValid);

        var invented = CitationVerifier.Verify("**«يجوز للمدين تعديل العقد وحده»**[[article:12:147]]", Fetched);
        Assert.False(invented.IsValid);
        Assert.Contains("مش موجود في أي مصدر", invented.Problems.Single());
    }

    [Theory]
    [InlineData("المتعاقدين، فلا", true)]
    [InlineData("شريعه المتعاقدين", true)]
    [InlineData("المتعاقد", false)] // جزء من كلمة مش كفاية
    [InlineData("", false)]
    public void QuoteAppearsIn_MatchesWholeWordsAfterNormalization(string quote, bool expected) =>
        Assert.Equal(expected, CitationVerifier.QuoteAppearsIn(quote, "العقد شريعة المتعاقدين، فلا يجوز نقضه"));
}
