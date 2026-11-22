using Microsoft.Extensions.Logging.Abstractions;
using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Llm;
using SmartLawyer.Infrastructure.Research;
using SmartLawyer.Infrastructure.Search;

namespace SmartLawyer.Api.Tests;

/// <summary>بيتشغّل بس لما LIVE_LLM_MODEL يتحط، لأنه بيكلم نموذج حقيقي (مش في الـ CI).</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIVE_LLM_MODEL")))
        {
            Skip = "اتجاهل: محتاج LIVE_LLM_MODEL و GROQ_API_KEY";
        }
    }
}

public class LiveResearchTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    // نصوص تجريبية مكتوبة للاختبار (مش نصوص قوانين حقيقية).
    private const string Law = """
        مادة 1
        يلتزم المورد بتسليم البضاعة في الموعد المتفق عليه في العقد، وإلا التزم بالتعويض عن التأخير.
        مادة 2
        إذا استحال التنفيذ بسبب قوة قاهرة، وجب على المدين إخطار الدائن كتابةً فور وقوعها، وإلا سقط حقه في التمسك بها.
        مادة 3
        يُقدَّر التعويض بقدر ما لحق الدائن من خسارة وما فاته من كسب.
        """;

    private const string Ruling = """
        الطعن رقم 777 لسنة 90 ق - جلسة 2022/3/1
        الدائرة: التجارية
        المبدأ: عدم إخطار الدائن بالقوة القاهرة فور وقوعها يسقط حق المدين في التمسك بها.
        وحيث إن الطاعن لم يخطر المطعون ضده بالقوة القاهرة إلا بعد شهرين من وقوعها...
        """;

    [LiveFact]
    public async Task RealModel_AnswersWithVerifiedCitations()
    {
        await using var db = fx.CreateContext(null);
        var import = new LegalImportService(db);
        await import.ImportLawAsync(new LawImportRequest("قانون تجريبي للتوريد", null, null, null, new DateOnly(2000, 1, 1), Law));
        await import.ImportRulingAsync(new RulingImportRequest(Infrastructure.Persistence.Entities.Court.Cassation, Ruling));

        var provider = new LlmProviderOptions
        {
            Name = "live",
            BaseUrl = Environment.GetEnvironmentVariable("LIVE_LLM_BASE_URL") ?? "https://api.groq.com/openai/v1",
            Model = Environment.GetEnvironmentVariable("LIVE_LLM_MODEL")!,
            AllowsCaseData = true,
        };
        var client = new OpenAiCompatibleClient(new HttpClient(), provider, Environment.GetEnvironmentVariable("GROQ_API_KEY"));
        var agent = new ResearchAgent(new LlmRouter([client], NullLogger<LlmRouter>.Instance), new LegalSearchService(db));

        var answer = await agent.AskAsync("المورد اتأخر في التسليم وبيقول إن فيه قوة قاهرة، بس ماخطرناش غير بعد شهرين. هل ده بيعفيه من التعويض؟");

        var report = $"Answered={answer.Answered}\nProblems:\n{string.Join("\n", answer.Problems)}\n\n{answer.Text}\n\nSources: {string.Join(", ", answer.Sources.Select(s => s.Key))}\nTokens: {answer.InputTokens}/{answer.OutputTokens}";
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), $"live-research-{provider.Model.Replace('/', '_')}.txt"), report);
        Assert.True(answer.Answered, report);
        Assert.NotEmpty(answer.Sources);
    }
}
