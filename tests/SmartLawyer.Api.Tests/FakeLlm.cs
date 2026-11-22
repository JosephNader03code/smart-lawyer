using SmartLawyer.Infrastructure.Llm;

namespace SmartLawyer.Api.Tests;

/// <summary>نموذج وهمي بيرد بردود محددة مسبقاً بالترتيب، وبيسجل الطلبات اللي جاتله.</summary>
public sealed class FakeLlm(params Func<LlmRequest, LlmResponse>[] script) : ILlmClient
{
    private int _next;
    public List<LlmRequest> Requests { get; } = [];

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        // نسخة من الرسايل وقت الطلب، لأن الوكيل بيكمّل يضيف على نفس القائمة.
        Requests.Add(request with { Messages = request.Messages.ToList() });
        if (_next >= script.Length)
        {
            throw new InvalidOperationException("النموذج الوهمي خلصت ردوده");
        }
        return Task.FromResult(script[_next++](request));
    }

    public static Func<LlmRequest, LlmResponse> Text(string content) =>
        _ => new LlmResponse(content, [], "fake", "fake-model", 100, 50, 0m);

    public static Func<LlmRequest, LlmResponse> Tool(string name, string argsJson) =>
        _ => new LlmResponse(null, [new LlmToolCall(Guid.NewGuid().ToString("N"), name, argsJson)], "fake", "fake-model", 100, 20, 0m);
}
