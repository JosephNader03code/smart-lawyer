using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLawyer.Infrastructure.Llm;

namespace SmartLawyer.Api.Tests;

public class LlmClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            return respond(request);
        }
    }

    private const string OkBody = """
        {"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[
          {"id":"call_1","type":"function","function":{"name":"search_legislation","arguments":"{\"query\":\"عقد\"}"}}]}}],
         "usage":{"prompt_tokens":1000,"completion_tokens":200}}
        """;

    private static LlmProviderOptions Provider(string name, bool allowsCaseData = false) => new()
    {
        Name = name, BaseUrl = $"https://{name}.example/v1", Model = $"{name}-model", AllowsCaseData = allowsCaseData,
        InputCostPerMillionTokens = 1m, OutputCostPerMillionTokens = 2m,
    };

    private static OpenAiCompatibleClient Client(LlmProviderOptions p, StubHandler h, string? key = "test-key") =>
        new(new HttpClient(h), p, key);

    private static LlmRequest Request(bool caseData) => new(
        "system", [LlmMessage.User("سؤال")], caseData,
        [new LlmTool("search_legislation", "بحث", new JsonObject { ["type"] = "object" })]);

    [Fact]
    public async Task Client_SendsOpenAiFormat_AndParsesToolCallsAndCost()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody, Encoding.UTF8, "application/json") });

        var response = await Client(Provider("groq"), handler).CompleteAsync(Request(caseData: false));

        var sent = JsonNode.Parse(handler.Bodies.Single())!;
        Assert.Equal("groq-model", (string?)sent["model"]);
        Assert.Equal("system", (string?)sent["messages"]![0]!["role"]);
        Assert.Equal("search_legislation", (string?)sent["tools"]![0]!["function"]!["name"]);
        Assert.Equal("https://groq.example/v1/chat/completions", handler.Requests.Single().RequestUri!.ToString());
        Assert.Equal("Bearer test-key", handler.Requests.Single().Headers.Authorization!.ToString());

        var call = Assert.Single(response.ToolCalls);
        Assert.Equal(("call_1", "search_legislation"), (call.Id, call.Name));
        Assert.Contains("عقد", call.ArgumentsJson);
        Assert.Equal(0.0014m, response.CostUsd); // 1000×1/1M + 200×2/1M
    }

    [Fact]
    public async Task Router_NeverSendsCaseDataToProviderThatDisallowsIt()
    {
        var gemini = new StubHandler(_ => throw new InvalidOperationException("مش المفروض يتنادى"));
        var ollama = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody) });
        var router = new LlmRouter(
            [Client(Provider("gemini", allowsCaseData: false), gemini), Client(Provider("ollama", allowsCaseData: true), ollama, null)],
            NullLogger<LlmRouter>.Instance);

        var response = await router.CompleteAsync(Request(caseData: true));

        Assert.Equal("ollama", response.Provider);
        Assert.Empty(gemini.Requests);
    }

    [Fact]
    public async Task Router_ThrowsWhenNoProviderAllowsCaseData()
    {
        var router = new LlmRouter([Client(Provider("gemini"), new StubHandler(_ => throw new InvalidOperationException()))],
            NullLogger<LlmRouter>.Instance);

        await Assert.ThrowsAsync<LlmUnavailableException>(() => router.CompleteAsync(Request(caseData: true)));
    }

    [Fact]
    public async Task Router_FallsBackWhenRateLimited()
    {
        var limited = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") });
        var backup = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody) });
        var router = new LlmRouter([Client(Provider("groq"), limited), Client(Provider("gemini"), backup)], NullLogger<LlmRouter>.Instance);

        Assert.Equal("gemini", (await router.CompleteAsync(Request(caseData: false))).Provider);
    }

    [Fact]
    public async Task Router_DoesNotRetryOnClientErrors()
    {
        var bad = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") });
        var backup = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody) });
        var router = new LlmRouter([Client(Provider("groq"), bad), Client(Provider("gemini"), backup)], NullLogger<LlmRouter>.Instance);

        await Assert.ThrowsAsync<LlmProviderException>(() => router.CompleteAsync(Request(caseData: false)));
        Assert.Empty(backup.Requests);
    }
}

public class RateLimitTests
{
    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _i;
        public int Calls => _i;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(responses[Math.Min(_i++, responses.Length - 1)]);
    }

    private const string Ok = """{"choices":[{"message":{"content":"تمام"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""";

    private static HttpResponseMessage Limited(string wait) => new(System.Net.HttpStatusCode.TooManyRequests)
    {
        Content = new StringContent($$$"""{"error":{"message":"Rate limit reached. Please try again in {{{wait}}}."}}"""),
    };

    private static SmartLawyer.Infrastructure.Llm.OpenAiCompatibleClient Client(HttpMessageHandler h) =>
        new(new HttpClient(h), new() { Name = "groq", BaseUrl = "https://x.example/v1", Model = "m" }, "k");

    private static SmartLawyer.Infrastructure.Llm.LlmRequest Req() =>
        new("s", [SmartLawyer.Infrastructure.Llm.LlmMessage.User("q")], false);

    [Fact]
    public async Task ShortRateLimit_IsRetriedAfterWaiting()
    {
        var h = new SequenceHandler(Limited("300ms"), new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Ok) });
        Assert.Equal("تمام", (await Client(h).CompleteAsync(Req())).Content);
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task LongRateLimit_IsNotWaitedFor()
    {
        var h = new SequenceHandler(Limited("8m30s"));
        await Assert.ThrowsAsync<SmartLawyer.Infrastructure.Llm.LlmProviderException>(() => Client(h).CompleteAsync(Req()));
        Assert.Equal(1, h.Calls);
    }

    [Theory]
    [InlineData("Please try again in 352.5ms.", 352.5)]
    [InlineData("Please try again in 17.168s.", 17168)]
    public void ParseRetryAfter_ReadsGroqMessage(string body, double ms) =>
        Assert.Equal(TimeSpan.FromMilliseconds(ms),
            SmartLawyer.Infrastructure.Llm.OpenAiCompatibleClient.ParseRetryAfter(new HttpResponseMessage(), body));
}
