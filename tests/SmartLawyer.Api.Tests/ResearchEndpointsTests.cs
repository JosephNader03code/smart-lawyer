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
using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Llm;

namespace SmartLawyer.Api.Tests;

public class ResearchEndpointsTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    private const string SupabaseUrl = "https://test-project.supabase.co";
    private const string Secret = "test-secret-that-is-at-least-32-bytes-long!!";

    private HttpClient CreateClient(ILlmClient llm) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b =>
        {
            // الـ API بيتصل بـ role عادي، فحماية Row-Level Security شغالة زي الإنتاج.
            b.UseSetting("ConnectionStrings:Default", fx.AppConnectionString);
            b.UseSetting("Supabase:Url", SupabaseUrl);
            b.UseSetting("Supabase:JwtSecret", Secret);
            b.ConfigureTestServices(s => s.AddSingleton(llm));
        })
        .CreateClient();

    private static string Token(Guid userId, string email, string issuer = SupabaseUrl + "/auth/v1") =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("email", email)]),
            Issuer = issuer,
            Audience = "authenticated",
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
        });

    private static HttpRequestMessage Ask(string question, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/research/ask") { Content = JsonContent.Create(new { question }) };
        if (token is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return req;
    }

    [Fact]
    public async Task Ask_RequiresValidToken()
    {
        var client = CreateClient(new FakeLlm());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Ask("سؤال تجريبي طويل", null))).StatusCode);
        var wrongIssuer = Token(Guid.NewGuid(), "x@test.local", "https://evil.example/auth/v1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Ask("سؤال تجريبي طويل", wrongIssuer))).StatusCode);
    }

    [Fact]
    public async Task Ask_ProvisionsOffice_AndReturnsVerifiedAnswerWithSources()
    {
        int lawId;
        await using (var db = fx.CreateContext(null))
        {
            lawId = (await new LegalImportService(db).ImportLawAsync(new LawImportRequest(
                "قانون تجريبي للـ endpoint", null, null, null, new DateOnly(2000, 1, 1),
                "مادة 1\nيلتزم البائع بتسليم المبيع بالحالة التي كان عليها وقت البيع."))).LawId;
        }
        var key = $"[[article:{lawId}:1]]";
        var llm = new FakeLlm(
            FakeLlm.Tool("get_article", $$"""{"law_id":{{lawId}},"article_number":"1"}"""),
            FakeLlm.Text($"البائع ملزم بالتسليم {key} «يلتزم البائع بتسليم المبيع»."));
        var client = CreateClient(llm);
        var token = Token(Guid.NewGuid(), "lawyer@test.local");

        var response = await client.SendAsync(Ask("إيه التزامات البائع في التسليم؟", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("answered").GetBoolean());
        Assert.Equal(key, body.GetProperty("sources")[0].GetProperty("key").GetString());
        Assert.Equal(14, body.GetProperty("remainingToday").GetInt32());

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meBody = await (await client.SendAsync(me)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("lawyer@test.local", meBody.GetProperty("email").GetString());
        Assert.NotEqual(Guid.Empty, meBody.GetProperty("organizationId").GetGuid());
    }

    [Fact]
    public async Task Ask_Returns503WhenNoAiProviderAvailable()
    {
        var unavailable = new ThrowingLlm();
        var client = CreateClient(unavailable);

        var response = await client.SendAsync(Ask("سؤال تجريبي طويل", Token(Guid.NewGuid(), "a@test.local")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ask_RejectsTooShortQuestion()
    {
        var client = CreateClient(new FakeLlm());
        var response = await client.SendAsync(Ask("؟", Token(Guid.NewGuid(), "b@test.local")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class ThrowingLlm : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
            throw new LlmUnavailableException("مفيش مزوّد");
    }
}
