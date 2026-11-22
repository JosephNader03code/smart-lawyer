using Microsoft.Extensions.Logging;

namespace SmartLawyer.Infrastructure.Llm;

/// <summary>
/// بيختار المزوّد: بيمشي على المزوّدين بالترتيب، وبيتخطى أي مزوّد مقفول أو مش مسموح له ببيانات القضايا،
/// ولو مزوّد رجّع حد استخدام أو خطأ سيرفر بيجرب اللي بعده.
/// </summary>
public sealed class LlmRouter(IReadOnlyList<OpenAiCompatibleClient> providers, ILogger<LlmRouter> logger) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var eligible = providers
            .Where(p => p.Provider.Enabled && (!request.ContainsCaseData || p.Provider.AllowsCaseData))
            .ToList();
        if (eligible.Count == 0)
        {
            throw new LlmUnavailableException(request.ContainsCaseData
                ? "مفيش مزوّد AI مسموح له ببيانات القضايا ومتفعّل."
                : "مفيش مزوّد AI متفعّل.");
        }

        Exception? last = null;
        foreach (var client in eligible)
        {
            try
            {
                return await client.CompleteAsync(request, ct);
            }
            // OperationCanceledException من غير ما الطلب الأصلي يتلغي = المزوّد خد وقت أكتر من المسموح.
            catch (Exception ex) when (!ct.IsCancellationRequested
                                       && ex is LlmProviderException { IsRetryable: true } or HttpRequestException or OperationCanceledException)
            {
                logger.LogWarning(ex, "المزوّد {Provider} فشل، بنجرب اللي بعده", client.Provider.Name);
                last = ex;
            }
        }
        throw new LlmUnavailableException("كل مزوّدين الـ AI مش متاحين دلوقتي. جرّب كمان شوية.", last);
    }
}
