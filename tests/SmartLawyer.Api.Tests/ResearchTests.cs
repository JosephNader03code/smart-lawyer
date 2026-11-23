using Microsoft.EntityFrameworkCore;
using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Research;
using SmartLawyer.Infrastructure.Search;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Api.Tests;

public class ResearchTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private int _lawId;
    private string Key => $"[[article:{_lawId}:2]]";

    public async Task InitializeAsync()
    {
        await using var db = fx.CreateContext(null);
        // نفس العنوان والنص في كل test، فالاستيراد بيرجّع نفس القانون من غير تكرار.
        const string title = "قانون تجريبي للبحث الذكي";
        _lawId = (await new LegalImportService(db).ImportLawAsync(new LawImportRequest(
            title, null, null, null, new DateOnly(2000, 1, 1),
            "مادة 1\nيلتزم المورد بالتسليم في الموعد.\nمادة 2\nإذا استحال التنفيذ بسبب قوة قاهرة وجب على المدين إخطار الدائن فور وقوعها."))).LawId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ResearchAgent Agent(FakeLlm llm, Infrastructure.Persistence.AppDbContext db) => new(llm, new LegalSearchService(db));

    [Fact]
    public async Task Agent_ReturnsVerifiedAnswer_AfterSearchingTheDatabase()
    {
        await using var db = fx.CreateContext(null);
        var llm = new FakeLlm(
            FakeLlm.Tool("search_legislation", """{"query":"اخطار القوه القاهره"}"""),
            FakeLlm.Text($"لازم الإخطار فوراً {Key} «وجب على المدين إخطار الدائن فور وقوعها»."));

        var answer = await Agent(llm, db).AskAsync("هل لازم أخطر الطرف التاني بالقوة القاهرة؟");

        Assert.True(answer.Answered, string.Join("\n", answer.Problems));
        var source = Assert.Single(answer.Sources);
        Assert.Equal(Key, source.Key);
        Assert.Contains("قوة قاهرة", source.Text);
        // نتيجة الأداة اللي النموذج شافها فيها مفتاح الاستشهاد.
        Assert.Contains(Key, llm.Requests[1].Messages.Last().Content);
        Assert.All(llm.Requests, r => Assert.True(r.ContainsCaseData));
    }

    [Fact]
    public async Task Agent_RejectsInventedCitation_GivesOneRepairChance_ThenAbstains()
    {
        await using var db = fx.CreateContext(null);
        var llm = new FakeLlm(
            FakeLlm.Tool("search_legislation", """{"query":"قوة قاهرة"}"""),
            FakeLlm.Text("طبقاً للمادة [[article:999:5]] لازم الإخطار."),
            FakeLlm.Text("طبقاً للطعن [[ruling:1:1]] لازم الإخطار."));

        var answer = await Agent(llm, db).AskAsync("سؤال عن القوة القاهرة");

        Assert.False(answer.Answered);
        Assert.Equal(ResearchAgent.NoAnswerText, answer.Text);
        Assert.Empty(answer.Sources);
        Assert.Equal(3, llm.Requests.Count);
        Assert.Contains("اترفضت", llm.Requests[2].Messages.Last().Content);
    }

    [Fact]
    public async Task Agent_AcceptsRepairedAnswer()
    {
        await using var db = fx.CreateContext(null);
        var llm = new FakeLlm(
            FakeLlm.Tool("search_legislation", """{"query":"قوة قاهرة"}"""),
            FakeLlm.Text($"{Key} «يجوز التأخير بدون إخطار»"),
            FakeLlm.Text($"لازم الإخطار {Key}"));

        Assert.True((await Agent(llm, db).AskAsync("سؤال عن القوة القاهرة")).Answered);
    }

    [Fact]
    public async Task Agent_ReportsToolErrorsBackToModel()
    {
        await using var db = fx.CreateContext(null);
        var llm = new FakeLlm(
            FakeLlm.Tool("get_article", """{"law_id":"مش رقم","article_number":"2"}"""),
            FakeLlm.Tool("unknown_tool", "{}"),
            FakeLlm.Text("مفيش سند."),
            FakeLlm.Text("مفيش سند برضه.")); // الإجابة من غير مصادر بتاخد فرصة تصليح واحدة

        var answer = await Agent(llm, db).AskAsync("سؤال");

        Assert.Contains("law_id", llm.Requests[1].Messages.Last().Content);
        Assert.Contains("مفيش أداة", llm.Requests[2].Messages.Last().Content);
        Assert.False(answer.Answered);
    }

    private async Task<(Guid Org, Guid User)> NewAccountAsync()
    {
        var accounts = new AccountService(new TenantDbContextFactory(fx.ConnectionString));
        var user = Guid.NewGuid();
        return (await accounts.EnsureAccountAsync(user, $"{user:N}@test.local"), user);
    }

    private ResearchService Service(Infrastructure.Persistence.AppDbContext db, FakeLlm llm, int perDay = 15) =>
        new(db, Agent(llm, db), new QuotaOptions { QuestionsPerDay = perDay }, TimeProvider.System);

    [Fact]
    public async Task Service_CachesAnswers_SoRepeatedQuestionIsFreeAndDoesNotUseQuota()
    {
        var (org, user) = await NewAccountAsync();
        var llm = new FakeLlm(
            FakeLlm.Tool("search_legislation", """{"query":"قوة قاهرة"}"""),
            FakeLlm.Text($"لازم الإخطار {Key}"));

        await using (var db = fx.CreateContext(org, asAppRole: true))
        {
            var first = await Service(db, llm).AskAsync(user, "هل لازم الإخطار بالقوة القاهرة؟");
            Assert.False(first.Cached);
            Assert.Equal(14, first.RemainingToday);
        }

        await using (var db = fx.CreateContext(org, asAppRole: true))
        {
            // نفس السؤال بهمزات مختلفة.
            var second = await Service(db, llm).AskAsync(user, "هل لازم الاخطار بالقوه القاهره؟");
            Assert.True(second.Cached);
            Assert.True(second.Answer.Answered);
            Assert.Equal(14, second.RemainingToday);
        }

        Assert.Equal(2, llm.Requests.Count); // النموذج اتنادى في السؤال الأول بس

        await using var check = fx.CreateContext(org, asAppRole: true);
        var usage = await check.LlmUsages.SingleAsync();
        Assert.Equal(("research", 200, 70), (usage.Feature, usage.InputTokens, usage.OutputTokens));
    }

    [Fact]
    public async Task Service_EnforcesDailyQuota()
    {
        var (org, user) = await NewAccountAsync();
        var llm = new FakeLlm(FakeLlm.Text("مفيش سند."), FakeLlm.Text("مفيش سند."), FakeLlm.Text("مفيش سند."), FakeLlm.Text("مفيش سند."));

        await using var db = fx.CreateContext(org, asAppRole: true);
        var service = Service(db, llm, perDay: 2);
        await service.AskAsync(user, "سؤال أول بدون إجابة");
        var second = await service.AskAsync(user, "سؤال تاني بدون إجابة");
        Assert.Equal(0, second.RemainingToday);

        await Assert.ThrowsAsync<QuotaExceededException>(() => service.AskAsync(user, "سؤال تالت"));
    }

    [Fact]
    public async Task Cache_IsNotSharedBetweenOffices()
    {
        var (orgA, userA) = await NewAccountAsync();
        var (orgB, userB) = await NewAccountAsync();
        var llm = new FakeLlm(
            FakeLlm.Tool("search_legislation", """{"query":"قوة قاهرة"}"""),
            FakeLlm.Text($"لازم الإخطار {Key}"),
            FakeLlm.Tool("search_legislation", """{"query":"قوة قاهرة"}"""),
            FakeLlm.Text($"لازم الإخطار {Key}"));

        await using (var a = fx.CreateContext(orgA, asAppRole: true))
        {
            await Service(a, llm).AskAsync(userA, "سؤال سري عن موكل معين");
        }
        await using (var b = fx.CreateContext(orgB, asAppRole: true))
        {
            Assert.False((await Service(b, llm).AskAsync(userB, "سؤال سري عن موكل معين")).Cached);
        }
    }

    [Fact]
    public async Task AccountService_CreatesOfficeOnFirstLogin_AndReusesIt()
    {
        var accounts = new AccountService(new TenantDbContextFactory(fx.ConnectionString));
        var user = Guid.NewGuid();

        var first = await accounts.EnsureAccountAsync(user, "lawyer@test.local");
        var again = await accounts.EnsureAccountAsync(user, "lawyer@test.local");

        Assert.Equal(first, again);
        await using var db = fx.CreateContext(first, asAppRole: true);
        Assert.Equal("owner", (await db.Users.SingleAsync()).Role);
    }
}

public class RetrieveModeTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    private async Task<int> SeedAsync()
    {
        await using var db = fx.CreateContext(null);
        return (await new LegalImportService(db).ImportLawAsync(new LawImportRequest(
            "قانون تجريبي لوضع البحث", null, null, null, new DateOnly(2000, 1, 1),
            "مادة 1\nكل خطأ سبب ضررا للغير يلزم من ارتكبه بالتعويض.\nمادة 2\nيلتزم البائع بتسليم المبيع."))).LawId;
    }

    [Fact]
    public async Task Retrieve_UsesTwoSmallCalls_AndVerifiesCitations()
    {
        var lawId = await SeedAsync();
        var llm = new FakeLlm(
            FakeLlm.Text("خطأ ضرر للغير تعويض"),
            FakeLlm.Text($"المتسبب ملزم بالتعويض [[article:{lawId}:1]] «كل خطأ سبب ضررا للغير يلزم من ارتكبه بالتعويض»."));
        await using var db = fx.CreateContext(null);

        var answer = await new ResearchAgent(llm, new LegalSearchService(db), ResearchMode.Retrieve)
            .AskAsync("واحد خبط عربية جاري وكسرها، لازم يعوضه؟");

        Assert.True(answer.Answered, string.Join("\n", answer.Problems));
        Assert.Equal(2, llm.Requests.Count);
        Assert.All(llm.Requests, r => Assert.Null(r.Tools));
        Assert.Contains($"[[article:{lawId}:1]]", llm.Requests[1].Messages[0].Content); // المصادر اتبعتت مع السؤال
    }

    [Fact]
    public async Task Retrieve_SkipsAnswerCallWhenNothingFound()
    {
        await SeedAsync();
        var llm = new FakeLlm(FakeLlm.Text("كلمة غير موجودة إطلاقا زززز"));
        await using var db = fx.CreateContext(null);

        var answer = await new ResearchAgent(llm, new LegalSearchService(db), ResearchMode.Retrieve).AskAsync("سؤال ععععع");

        Assert.False(answer.Answered);
        Assert.Single(llm.Requests);
    }

    [Fact]
    public async Task Retrieve_RepairsOnceThenAbstains()
    {
        await SeedAsync();
        var llm = new FakeLlm(
            FakeLlm.Text("خطأ ضرر تعويض"),
            FakeLlm.Text("[[article:999:1]]"),
            FakeLlm.Text("[[ruling:1:1]]"));
        await using var db = fx.CreateContext(null);

        var answer = await new ResearchAgent(llm, new LegalSearchService(db), ResearchMode.Retrieve).AskAsync("سؤال عن الخطأ والتعويض");

        Assert.False(answer.Answered);
        Assert.Equal(3, llm.Requests.Count);
    }
}
