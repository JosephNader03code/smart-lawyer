using System.Text.Json.Nodes;

namespace SmartLawyer.Infrastructure.Llm;

public enum LlmRole { System, User, Assistant, Tool }

public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

public sealed record LlmMessage(
    LlmRole Role, string? Content, IReadOnlyList<LlmToolCall>? ToolCalls = null, string? ToolCallId = null)
{
    public static LlmMessage User(string content) => new(LlmRole.User, content);
    public static LlmMessage ToolResult(string toolCallId, string content) => new(LlmRole.Tool, content, ToolCallId: toolCallId);
}

/// <summary>تعريف tool: الاسم والوصف وJSON schema للمدخلات.</summary>
public sealed record LlmTool(string Name, string Description, JsonObject Parameters);

/// <param name="ContainsCaseData">
/// true لو الطلب فيه بيانات قضايا أو عملاء. الـ router مابيبعتش طلب زي ده غير لمزوّد مسموح له بالبيانات دي.
/// </param>
public sealed record LlmRequest(
    string System,
    IReadOnlyList<LlmMessage> Messages,
    bool ContainsCaseData,
    IReadOnlyList<LlmTool>? Tools = null,
    int MaxTokens = 2048);

public sealed record LlmResponse(
    string? Content,
    IReadOnlyList<LlmToolCall> ToolCalls,
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd);

public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
}

/// <summary>مفيش مزوّد متاح للطلب (كلهم مقفولين، أو وصلوا لحدهم، أو مش مسموح لهم ببيانات القضايا).</summary>
public sealed class LlmUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
