using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Search;

namespace SmartLawyer.Api.Tests;

public class LegalDataTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    // نص تجريبي قصير (مش نص قانون حقيقي) عشان نختبر الاستيراد والبحث.
    private const string SampleLaw = """
        مادة 1
        يلتزم المورد بتسليم البضاعة في الموعد المتفق عليه في العقد.
        مادة 2
        إذا استحال التنفيذ بسبب قوة قاهرة، وجب على المدين إخطار الدائن فور وقوعها.
        مادة ٣ مكرراً
        يُقدَّر التعويض بقدر الضرر الذي لحق الدائن.
        """;

    private async Task<int> ImportSampleLawAsync(string title, DateOnly validFrom, string text)
    {
        await using var db = fx.CreateContext(null);
        var result = await new LegalImportService(db).ImportLawAsync(
            new LawImportRequest(title, "99", 2000, "نص تجريبي", validFrom, text));
        return result.LawId;
    }

    [Fact]
    public async Task ImportLaw_SplitsArticles_AndSearchFindsThemDespiteSpellingVariants()
    {
        await ImportSampleLawAsync("قانون تجريبي للبحث", new DateOnly(2000, 1, 1), SampleLaw);

        await using var db = fx.CreateContext(null);
        // «اخطار القوه القاهره» من غير همزات ولا تاء مربوطة، والنص فيه «إخطار» و«قوة قاهرة».
        var hits = await new LegalSearchService(db).SearchArticlesAsync("اخطار القوه القاهره", new DateOnly(2024, 1, 1));

        // قوانين تجريبية تانية في نفس الـ database فيها نفس النص، فبنركز على القانون ده.
        var top = Assert.Single(hits, h => h.LawTitle == "قانون تجريبي للبحث");
        Assert.Equal("2", top.ArticleNumber);
        Assert.All(hits, h => Assert.Equal("2", h.ArticleNumber));
    }

    [Fact]
    public async Task GetArticle_ReturnsVersionInForceOnTheDate()
    {
        var lawId = await ImportSampleLawAsync("قانون تجريبي للتعديل", new DateOnly(2000, 1, 1), SampleLaw);
        var amended = SampleLaw.Replace("فور وقوعها", "خلال سبعة أيام من وقوعها");

        await using (var db = fx.CreateContext(null))
        {
            var result = await new LegalImportService(db).ImportLawAsync(
                new LawImportRequest("قانون تجريبي للتعديل", null, null, null, new DateOnly(2020, 7, 1), amended));
            Assert.Equal((0, 1, 2), (result.NewArticles, result.NewVersions, result.Unchanged));
        }

        await using var read = fx.CreateContext(null);
        var search = new LegalSearchService(read);
        var before = await search.GetArticleAsync(lawId, "2", new DateOnly(2015, 5, 1));
        var lastOldDay = await search.GetArticleAsync(lawId, "2", new DateOnly(2020, 6, 30));
        var after = await search.GetArticleAsync(lawId, "٢", new DateOnly(2025, 8, 15));

        Assert.Contains("فور وقوعها", before!.Text);
        Assert.Contains("فور وقوعها", lastOldDay!.Text);
        Assert.Contains("سبعة أيام", after!.Text);
        Assert.Null(await search.GetArticleAsync(lawId, "2", new DateOnly(1990, 1, 1)));
        Assert.NotNull(await search.GetArticleAsync(lawId, "3 مكرر", new DateOnly(2025, 1, 1)));
    }

    [Fact]
    public async Task ImportLaw_ReimportingSameText_DoesNotDuplicate()
    {
        await ImportSampleLawAsync("قانون تجريبي للتكرار", new DateOnly(2000, 1, 1), SampleLaw);
        await using var db = fx.CreateContext(null);
        var again = await new LegalImportService(db).ImportLawAsync(
            new LawImportRequest("قانون تجريبي للتكرار", null, null, null, new DateOnly(2010, 1, 1), SampleLaw));

        Assert.Equal((0, 0, 3), (again.NewArticles, again.NewVersions, again.Unchanged));
    }

    [Fact]
    public async Task ImportCompleteLaw_MarksMissingArticlesAsRepealed()
    {
        var lawId = await ImportSampleLawAsync("قانون تجريبي للإلغاء", new DateOnly(2000, 1, 1), SampleLaw);
        var withoutArticle2 = "مادة 1\nيلتزم المورد بتسليم البضاعة في الموعد المتفق عليه في العقد.\nمادة 3 مكرراً\nيُقدَّر التعويض بقدر الضرر الذي لحق الدائن.";

        await using (var db = fx.CreateContext(null))
        {
            var r = await new LegalImportService(db).ImportLawAsync(new LawImportRequest(
                "قانون تجريبي للإلغاء", null, null, null, new DateOnly(2022, 1, 1), withoutArticle2, IsCompleteText: true));
            Assert.Equal(1, r.Repealed);
        }

        await using var read = fx.CreateContext(null);
        var search = new LegalSearchService(read);
        Assert.NotNull(await search.GetArticleAsync(lawId, "2", new DateOnly(2021, 12, 31)));
        Assert.Null(await search.GetArticleAsync(lawId, "2", new DateOnly(2022, 1, 1)));
        Assert.NotNull(await search.GetArticleAsync(lawId, "1", new DateOnly(2022, 1, 1)));
    }

    [Fact]
    public async Task ImportRuling_ParsesHeader_AndCanBeFoundBySearchAndReference()
    {
        const string ruling = """
            الطعن رقم ٤٥٦٧ لسنة ٨٨ ق - جلسة 2019/5/12
            الدائرة: التجارية
            المبدأ: الإخطار بالقوة القاهرة شرط للإعفاء من التعويض عن التأخير.
            وحيث إن الطاعن تمسك بالقوة القاهرة دون أن يخطر المطعون ضده...
            """;

        await using (var db = fx.CreateContext(null))
        {
            var r = await new LegalImportService(db).ImportRulingAsync(new RulingImportRequest(Court.Cassation, ruling));
            Assert.True(r.Created);
            Assert.Equal((4567, 88), (r.Reference.AppealNumber, r.Reference.JudicialYear));
        }

        await using var read = fx.CreateContext(null);
        var search = new LegalSearchService(read);
        var byRef = await search.GetRulingAsync(4567, 88);
        Assert.NotNull(byRef);
        Assert.Equal("التجارية", byRef.Chamber);
        Assert.Equal(new DateOnly(2019, 5, 12), byRef.SessionDate);
        Assert.StartsWith("الإخطار بالقوة القاهرة", byRef.Principle);

        var hits = await search.SearchRulingsAsync("التعويض عن التاخير", yearFrom: 80);
        Assert.Contains(hits, h => h.AppealNumber == 4567);
        Assert.Empty(await search.SearchRulingsAsync("التعويض عن التاخير", yearTo: 50));
    }

    [Fact]
    public async Task ImportRuling_WithoutReference_IsRejected()
    {
        await using var db = fx.CreateContext(null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new LegalImportService(db).ImportRulingAsync(new RulingImportRequest(Court.Cassation, "نص من غير رقم طعن")));
    }

    [Fact]
    public async Task Search_WithOnlyStopWords_ReturnsNothing()
    {
        await using var db = fx.CreateContext(null);
        Assert.Empty(await new LegalSearchService(db).SearchArticlesAsync("في من على"));
    }
}
