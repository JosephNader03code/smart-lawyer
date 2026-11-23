using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartLawyer.Infrastructure.CaseAnalysis;
using SmartLawyer.Infrastructure.Llm;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Research;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Api.Tests;

public class FactExtractorTests
{
    private const string Doc = """
        خطاب إخطار
        نخطركم بتعذر التسليم نتيجة الحادث الذي وقع للشحنة، تحريراً في ١٣ أغسطس ٢٠٢٥.
        وقيمة الشحنة 1,250,000 جنيه.
        """;

    [Fact]
    public async Task Extract_KeepsOnlyItemsWithRealQuotes_AndComputesDatesByCode()
    {
        var llm = new FakeLlm(FakeLlm.Text("""
            ```json
            {"events":[
              {"kind":"notice","title":"إخطار القوة القاهرة","date_text":"١٣ أغسطس ٢٠٢٥","quote":"نخطركم بتعذر التسليم نتيجة الحادث الذي وقع للشحنة"},
              {"kind":"incident","title":"حادث مؤلَّف","date_text":"1/1/2025","quote":"وقع الحادث على الطريق الصحراوي"},
              {"kind":"delivery","title":"من غير تاريخ","date_text":"","quote":"وقيمة الشحنة"}
            ],
             "facts":[
              {"subject":"الشحنة","predicate":"payment","time_text":null,"amount":1250000,"quote":"وقيمة الشحنة 1,250,000 جنيه"},
              {"subject":"الشحنة","predicate":"payment","time_text":null,"amount":999,"quote":"وقيمة الشحنة 1,250,000 جنيه"}
            ]}
            ```
            """));

        var result = await new FactExtractor(llm).ExtractAsync(Doc);

        var e = Assert.Single(result.Events);
        Assert.Equal(("notice", new DateTime(2025, 8, 13), false), (e.Kind, e.Date, e.HasTime));
        var f = Assert.Single(result.Facts);
        Assert.Equal(1_250_000m, f.Amount);
        Assert.Equal(3, result.Rejected); // حادث مؤلَّف + حدث من غير تاريخ + مبلغ مش مكتوب
        Assert.True(llm.Requests.Single().JsonOutput);
        Assert.True(llm.Requests.Single().ContainsCaseData);
    }

    [Fact]
    public async Task Extract_SurvivesInvalidJson()
    {
        var result = await new FactExtractor(new FakeLlm(FakeLlm.Text("مش JSON"))).ExtractAsync(Doc);
        Assert.Empty(result.Events);
        Assert.Equal(1, result.Rejected);
    }

    [Fact]
    public void Chunk_SplitsOnParagraphsWithinSize()
    {
        var text = string.Join("\n", Enumerable.Range(1, 30).Select(i => new string('ا', 400)));
        var chunks = FactExtractor.Chunk(text, 2000).ToList();
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 2000 + 1));
        Assert.Equal(text.Replace("\n", ""), string.Concat(chunks).Replace("\n", ""));
    }

    [Theory]
    [InlineData(1250000, "قيمتها 1,250,000 جنيه", true)]
    [InlineData(1250000, "قيمتها ١٢٥٠٠٠٠ جنيه", true)]
    [InlineData(999, "قيمتها 1,250,000 جنيه", false)]
    [InlineData(400, "وتم تسليم 400 كرتونة", false)] // كمية مش مبلغ
    public void AmountAppearsIn_ChecksTheNumberIsWritten(decimal amount, string quote, bool expected) =>
        Assert.Equal(expected, FactExtractor.AmountAppearsIn(amount, quote));
}

public class CaseAnalysisServiceTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    private const string NoticeDoc = "خطاب الإخطار\nنخطركم بتعذر التسليم نتيجة الحادث الذي وقع للشحنة، تحريراً في ١٣ أغسطس ٢٠٢٥.";
    private const string PoliceDoc = "محضر الشرطة\nوقع الحادث للشحنة بتاريخ 15/8/2025 على الطريق.";
    private const string WitnessDoc = "شهادة الشاهد\nأؤكد أن تسليم البضاعة تم يوم 28/8/2025 الساعة 8:00 مساءً بمقر الشركة.";
    private const string ReceiptDoc = "محضر الاستلام\nأفاد السائق بأن الوصول للموقع كان يوم 28/8/2025 الساعة 10:30 مساءً.";

    private static string Events(string kind, string title, string date, string quote) =>
        $$"""{"events":[{"kind":"{{kind}}","title":"{{title}}","date_text":"{{date}}","quote":"{{quote}}"}],"facts":[]}""";

    private static string Delivery(string subject, string time, string quote) =>
        $$"""{"events":[],"facts":[{"subject":"{{subject}}","predicate":"delivery","time_text":"{{time}}","amount":null,"quote":"{{quote}}"}]}""";

    private async Task<(Guid Org, Guid User)> AccountAsync()
    {
        var user = Guid.NewGuid();
        return (await new AccountService(new TenantDbContextFactory(fx.ConnectionString)).EnsureAccountAsync(user, $"{user:N}@t.local"), user);
    }

    private CaseAnalysisService Service(AppDbContext db, FakeLlm llm, int perDay = 10) =>
        new(db, new FactExtractor(llm), new QuotaOptions { AnalysesPerDay = perDay }, TimeProvider.System);

    private static byte[] Txt(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public async Task Timeline_FlagsNoticeBeforeIncident_AcrossDocuments()
    {
        var (org, user) = await AccountAsync();
        var llm = new FakeLlm(
            FakeLlm.Text(Events("notice", "إخطار القوة القاهرة", "١٣ أغسطس ٢٠٢٥", "نخطركم بتعذر التسليم نتيجة الحادث الذي وقع للشحنة")),
            FakeLlm.Text(Events("incident", "الحادث حسب محضر الشرطة", "15/8/2025", "وقع الحادث للشحنة بتاريخ 15/8/2025")));
        await using var db = fx.CreateContext(org, asAppRole: true);
        var svc = Service(db, llm);
        var c = await svc.CreateCaseAsync("شركة الأمل ضد المجموعة الوطنية", null, null, null);

        var d1 = await svc.AddDocumentAsync(user, c.Id, "خطاب الإخطار.txt", Txt(NoticeDoc));
        await svc.AddDocumentAsync(user, c.Id, "محضر الشرطة.txt", Txt(PoliceDoc));
        var timeline = await svc.GetTimelineAsync(c.Id);

        Assert.Equal(("Processed", 1), (d1.Status, d1.Events));
        Assert.Equal(["notice", "incident"], timeline.Events.Select(e => e.Kind)); // مترتبين بالتاريخ
        var conflict = Assert.Single(timeline.Conflicts);
        Assert.Contains("48 ساعة", conflict.Message);
        Assert.All(timeline.Events, e => Assert.True(e.InConflict));
        Assert.Equal("خطاب الإخطار.txt", timeline.Events[0].Document);
    }

    [Fact]
    public async Task Contradictions_FindsDeliveryTimeDifferenceBetweenWitnessAndReceipt()
    {
        var (org, user) = await AccountAsync();
        var llm = new FakeLlm(
            FakeLlm.Text(Delivery("شركة الأمل", "28/8/2025 الساعة 8:00 مساءً", "أؤكد أن تسليم البضاعة تم يوم 28/8/2025 الساعة 8:00 مساءً بمقر الشركة")),
            FakeLlm.Text(Delivery("شركه الامل", "28/8/2025 الساعة 10:30 مساءً", "أفاد السائق بأن الوصول للموقع كان يوم 28/8/2025 الساعة 10:30 مساءً")));
        await using var db = fx.CreateContext(org, asAppRole: true);
        var svc = Service(db, llm);
        var c = await svc.CreateCaseAsync("قضية تناقضات", null, null, null);
        await svc.AddDocumentAsync(user, c.Id, "شهادة الشاهد.txt", Txt(WitnessDoc));
        await svc.AddDocumentAsync(user, c.Id, "محضر الاستلام.txt", Txt(ReceiptDoc));

        var x = Assert.Single(await svc.GetContradictionsAsync(c.Id));

        Assert.Equal(("time", 150d), (x.Kind, x.Difference));
        Assert.Equal(new[] { "شهادة الشاهد.txt", "محضر الاستلام.txt" }.Order(), new[] { x.First.Document, x.Second.Document }.Order());
    }

    [Fact]
    public async Task Upload_WhenQuotaReached_KeepsTextAndMarksFailed_ThenReanalyzeWorks()
    {
        var (org, user) = await AccountAsync();
        var llm = new FakeLlm(FakeLlm.Text(Events("incident", "الحادث", "15/8/2025", "وقع الحادث للشحنة بتاريخ 15/8/2025")));
        await using var db = fx.CreateContext(org, asAppRole: true);
        var c = await Service(db, llm).CreateCaseAsync("قضية الحد", null, null, null);

        var blocked = await Service(db, llm, perDay: 0).AddDocumentAsync(user, c.Id, "محضر.txt", Txt(PoliceDoc));
        Assert.Equal("Failed", blocked.Status);
        Assert.Contains("للحد اليومي", blocked.Error);
        Assert.Empty(llm.Requests);

        var retried = await Service(db, llm).ReanalyzeAsync(user, c.Id, blocked.Id);
        Assert.Equal(("Processed", 1), (retried.Status, retried.Events));
    }

    [Fact]
    public async Task Upload_RejectsUnsupportedAndEmptyFiles()
    {
        var (org, user) = await AccountAsync();
        await using var db = fx.CreateContext(org, asAppRole: true);
        var svc = Service(db, new FakeLlm());
        var c = await svc.CreateCaseAsync("قضية ملفات", null, null, null);

        await Assert.ThrowsAsync<UnsupportedDocumentException>(() => svc.AddDocumentAsync(user, c.Id, "صورة.jpg", [1, 2, 3]));
        await Assert.ThrowsAsync<UnsupportedDocumentException>(() => svc.AddDocumentAsync(user, c.Id, "فاضي.txt", Txt("  ")));
    }

    [Fact]
    public async Task OtherOffice_CannotSeeOrUploadToCase()
    {
        var (orgA, _) = await AccountAsync();
        var (orgB, userB) = await AccountAsync();
        Guid caseId;
        await using (var a = fx.CreateContext(orgA, asAppRole: true))
        {
            caseId = (await Service(a, new FakeLlm()).CreateCaseAsync("قضية سرية", null, null, null)).Id;
        }

        await using var b = fx.CreateContext(orgB, asAppRole: true);
        var svc = Service(b, new FakeLlm());
        Assert.Empty(await svc.ListCasesAsync());
        await Assert.ThrowsAsync<CaseNotFoundException>(() => svc.GetTimelineAsync(caseId));
        await Assert.ThrowsAsync<CaseNotFoundException>(() => svc.AddDocumentAsync(userB, caseId, "x.txt", Txt(PoliceDoc)));
    }
}

public class CaseEndpointsTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    private const string Secret = "test-secret-that-is-at-least-32-bytes-long!!";

    private HttpClient Client(ILlmClient llm) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseSetting("ConnectionStrings:Default", fx.AppConnectionString);
        b.UseSetting("Supabase:Url", "https://test-project.supabase.co");
        b.UseSetting("Supabase:JwtSecret", Secret);
        b.ConfigureTestServices(s => s.AddSingleton(llm));
    }).CreateClient();

    private static string Token(Guid user) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim("sub", user.ToString()), new Claim("email", $"{user:N}@t.local")]),
        Issuer = "https://test-project.supabase.co/auth/v1",
        Audience = "authenticated",
        Expires = DateTime.UtcNow.AddMinutes(10),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
    });

    private static HttpRequestMessage Req(HttpMethod m, string url, string token, HttpContent? content = null)
    {
        var r = new HttpRequestMessage(m, url) { Content = content };
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return r;
    }

    [Fact]
    public async Task CreateCase_UploadDocument_AndReadTimeline_OverHttp()
    {
        var llm = new FakeLlm(FakeLlm.Text("""{"events":[{"kind":"incident","title":"الحادث","date_text":"15/8/2025","quote":"وقع الحادث للشحنة"}],"facts":[]}"""));
        var client = Client(llm);
        var token = Token(Guid.NewGuid());

        var created = await (await client.SendAsync(Req(HttpMethod.Post, "/api/cases", token, JsonContent.Create(new { title = "قضية عبر HTTP" }))))
            .Content.ReadFromJsonAsync<JsonElement>();
        var caseId = created.GetProperty("id").GetGuid();

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("محضر الشرطة\nوقع الحادث للشحنة بتاريخ 15/8/2025 على الطريق."));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "محضر الشرطة.txt");
        var upload = await client.SendAsync(Req(HttpMethod.Post, $"/api/cases/{caseId}/documents", token, form));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var timeline = await (await client.SendAsync(Req(HttpMethod.Get, $"/api/cases/{caseId}/timeline", token))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("incident", timeline.GetProperty("events")[0].GetProperty("kind").GetString());

        // مستخدم من مكتب تاني بياخد 404.
        var other = await client.SendAsync(Req(HttpMethod.Get, $"/api/cases/{caseId}/timeline", Token(Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        var unsupported = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "صورة.jpg" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Req(HttpMethod.Post, $"/api/cases/{caseId}/documents", token, unsupported))).StatusCode);
    }
}
