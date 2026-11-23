using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartLawyer.Infrastructure.Llm;

/// <summary>عميل لأي API متوافق مع OpenAI chat completions (Groq، Ollama، Gemini OpenAI-compatible).</summary>
public sealed class OpenAiCompatibleClient(HttpClient http, LlmProviderOptions provider, string? apiKey) : ILlmClient
{
    public LlmProviderOptions Provider { get; } = provider;

    /// <summary>لو المزوّد طلب انتظار قصير (حد الدقيقة)، بنستنى ونعيد بدل ما نفشل.</summary>
    public static readonly TimeSpan MaxRateLimitWait = TimeSpan.FromSeconds(30);
    public const int MaxRateLimitRetries = 4;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await SendOnceAsync(request, ct);
            }
            catch (LlmProviderException ex) when (ex.Status == HttpStatusCode.TooManyRequests
                                                  && attempt < MaxRateLimitRetries
                                                  && ex.RetryAfter is { } wait && wait <= MaxRateLimitWait)
            {
                await Task.Delay(wait + TimeSpan.FromMilliseconds(250), ct);
            }
        }
    }

    private async Task<LlmResponse> SendOnceAsync(LlmRequest request, CancellationToken ct)
    {
        var body = BuildBody(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, Provider.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Provider.TimeoutSeconds));
        using var response = await http.SendAsync(message, cts.Token);
        var json = await response.Content.ReadAsStringAsync(cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new LlmProviderException(Provider.Name, response.StatusCode, Truncate(json, 500),
                ParseRetryAfter(response, json));
        }
        return ParseResponse(json);
    }

    public JsonObject BuildBody(LlmRequest request)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = request.System } };
        foreach (var m in request.Messages)
        {
            var node = new JsonObject
            {
                ["role"] = m.Role switch
                {
                    LlmRole.User => "user",
                    LlmRole.Assistant => "assistant",
                    LlmRole.Tool => "tool",
                    _ => "system",
                },
                ["content"] = m.Content ?? "",
            };
            if (m.ToolCallId is not null)
            {
                node["tool_call_id"] = m.ToolCallId;
            }
            if (m.ToolCalls is { Count: > 0 })
            {
                node["tool_calls"] = new JsonArray(m.ToolCalls.Select(tc => (JsonNode)new JsonObject
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = tc.Name, ["arguments"] = tc.ArgumentsJson },
                }).ToArray());
            }
            messages.Add(node);
        }

        var body = new JsonObject
        {
            ["model"] = Provider.Model,
            ["messages"] = messages,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = 0.1,
        };
        if (request.JsonOutput)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }
        if (request.Tools is { Count: > 0 })
        {
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = t.Parameters.DeepClone(),
                },
            }).ToArray());
            body["tool_choice"] = "auto";
        }
        return body;
    }

    public LlmResponse ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var message = root.GetProperty("choices")[0].GetProperty("message");

        var content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        var toolCalls = new List<LlmToolCall>();
        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                var args = fn.TryGetProperty("arguments", out var a)
                    ? (a.ValueKind == JsonValueKind.String ? a.GetString() ?? "{}" : a.GetRawText())
                    : "{}";
                var id = call.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString()!
                    : Guid.NewGuid().ToString("N");
                toolCalls.Add(new LlmToolCall(id, fn.GetProperty("name").GetString()!, args));
            }
        }

        int input = 0, output = 0;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            input = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            output = usage.TryGetProperty("completion_tokens", out var o) ? o.GetInt32() : 0;
        }
        var cost = (input * Provider.InputCostPerMillionTokens + output * Provider.OutputCostPerMillionTokens) / 1_000_000m;
        return new LlmResponse(content, toolCalls, Provider.Name, Provider.Model, input, output, cost);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    /// <summary>من هيدر Retry-After، أو من رسالة زي "Please try again in 352.5ms" / "in 17.1s".</summary>
    public static TimeSpan? ParseRetryAfter(HttpResponseMessage response, string body)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }
        var m = System.Text.RegularExpressions.Regex.Match(body, @"try again in (?<v>[\d.]+)(?<u>ms|s)");
        if (m.Success && double.TryParse(m.Groups["v"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            return m.Groups["u"].Value == "ms" ? TimeSpan.FromMilliseconds(v) : TimeSpan.FromSeconds(v);
        }
        return null;
    }
}

public sealed class LlmProviderException(string provider, HttpStatusCode status, string body, TimeSpan? retryAfter = null)
    : Exception($"{provider} رجّع {(int)status}: {body}")
{
    public HttpStatusCode Status { get; } = status;
    public TimeSpan? RetryAfter { get; } = retryAfter;

    /// <summary>أخطاء ينفع نجرب بعدها مزوّد تاني: حد الاستخدام، أو مشكلة في السيرفر.</summary>
    public bool IsRetryable => Status == HttpStatusCode.TooManyRequests || (int)Status >= 500;
}
