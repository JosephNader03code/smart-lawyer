using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartLawyer.Core.Arabic;
using SmartLawyer.Core.Citations;
using SmartLawyer.Infrastructure.Llm;

namespace SmartLawyer.Infrastructure.CaseAnalysis;

public static class EventKinds
{
    public const string Signing = "signing", Delivery = "delivery", Notice = "notice", Incident = "incident",
        Payment = "payment", Hearing = "hearing", Other = "other";

    public static readonly string[] All = [Signing, Delivery, Notice, Incident, Payment, Hearing, Other];

    public static string Normalize(string? kind) =>
        All.Contains(kind?.Trim().ToLowerInvariant()) ? kind!.Trim().ToLowerInvariant() : Other;
}

public sealed record ExtractedEvent(string Kind, string Title, string RawDateText, DateTime Date, bool HasTime, string Quote);

public sealed record ExtractedFact(string Subject, string Predicate, DateTime? Time, string? RawTimeText, decimal? Amount, string Quote);

public sealed record ExtractionResult(
    IReadOnlyList<ExtractedEvent> Events,
    IReadOnlyList<ExtractedFact> Facts,
    int Rejected,
    IReadOnlyList<string> RejectionReasons,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    string? Model);

/// <summary>
/// استخراج الأحداث والوقائع من نص مستند بالـ AI، مع تحقق بالكود:
/// - أي حدث أو واقعة لازم يكون معاها اقتباس موجود حرفياً (بعد التوحيد) في المستند، وإلا بتترفض.
/// - التواريخ والأوقات بتتحسب بالكود (ArabicDateExtractor) من نص التاريخ أو الاقتباس، مش من كلام النموذج.
/// - المبلغ مابيتقبلش غير لو الرقم نفسه مكتوب في الاقتباس.
/// </summary>
public sealed partial class FactExtractor(ILlmClient llm)
{
    /// <summary>حجم كل جزء من المستند في الطلب الواحد (مناسب لحدود الدقيقة في الخطط المجانية).</summary>
    public const int ChunkSize = 5000;

    public const string Prompt = """
        أنت مساعد لمحامي مصري. اقرا جزء المستند واستخرج الأحداث والوقائع المهمة للقضية.
        رجّع JSON بس بالشكل ده بالظبط:
        {
          "events": [{"kind": "signing|delivery|notice|incident|payment|hearing|other", "title": "وصف قصير", "date_text": "التاريخ زي ما هو مكتوب في النص حرفياً", "quote": "جملة منقولة حرفياً من النص فيها الحدث"}],
          "facts": [{"subject": "الجهة أو الشيء اللي الواقعة بتخصه (زي الشركة أو الشحنة)، مش الشخص اللي بيحكي", "predicate": "signing|delivery|notice|incident|payment|other", "time_text": "التاريخ والوقت زي ما هما مكتوبين حرفياً أو null", "amount": رقم أو null, "quote": "جملة منقولة حرفياً من النص"}]
        }
        قواعد:
        - الاقتباس (quote) لازم يكون منقول حرفياً من النص من غير أي تعديل. أي اقتباس مش موجود في النص هيترفض.
        - ماتستنتجش تواريخ أو مبالغ مش مكتوبة. لو مفيش تاريخ مكتوب، ماتضيفش الحدث.
        - facts للوقائع اللي ممكن مستند تاني يختلف عليها: وقت حصول حاجة، أو مبلغ.
        - لو مفيش حاجة، رجّع {"events": [], "facts": []}.
        """;

    public async Task<ExtractionResult> ExtractAsync(string documentText, CancellationToken ct = default)
    {
        var events = new List<ExtractedEvent>();
        var facts = new List<ExtractedFact>();
        var reasons = new List<string>();
        int input = 0, output = 0;
        decimal cost = 0;
        string? model = null;

        foreach (var chunk in Chunk(documentText))
        {
            var response = await llm.CompleteAsync(new LlmRequest(
                Prompt, [LlmMessage.User(chunk)], ContainsCaseData: true, MaxTokens: 3000, JsonOutput: true), ct);
            input += response.InputTokens;
            output += response.OutputTokens;
            cost += response.CostUsd;
            model = $"{response.Provider}/{response.Model}";

            if (!TryParse(response.Content, out var root))
            {
                reasons.Add("رد النموذج مش JSON صالح.");
                continue;
            }
            ReadEvents(root, documentText, events, reasons);
            ReadFacts(root, documentText, facts, reasons);
        }

        return new ExtractionResult(
            events.DistinctBy(e => (e.Kind, e.Date, e.Quote)).ToList(),
            facts.DistinctBy(f => (f.Subject, f.Predicate, f.Time, f.Amount, f.Quote)).ToList(),
            reasons.Count, reasons, input, output, cost, model);
    }

    private static void ReadEvents(JsonElement root, string doc, List<ExtractedEvent> into, List<string> reasons)
    {
        foreach (var e in Array(root, "events"))
        {
            var quote = Str(e, "quote");
            var title = Str(e, "title") ?? "حدث";
            if (quote is null || !CitationVerifier.QuoteAppearsIn(quote, doc))
            {
                reasons.Add($"حدث «{title}»: الاقتباس مش موجود حرفياً في المستند: «{Short(quote)}»");
                continue;
            }
            var date = FindDate(Str(e, "date_text"), quote);
            if (date is null)
            {
                reasons.Add($"حدث «{title}»: مفيش تاريخ مكتوب.");
                continue;
            }
            into.Add(new ExtractedEvent(EventKinds.Normalize(Str(e, "kind")), title, date.Value.Raw,
                date.Value.Date.ToDateTime(date.Value.Time ?? TimeOnly.MinValue), date.Value.Time is not null, quote));
        }
    }

    /// <summary>
    /// التاريخ من نص التاريخ، ولو مش موجود فيه، من الاقتباس. ولو التاريخ من غير ساعة والاقتباس فيه ساعة
    /// لنفس اليوم (أو ساعة واحدة بس)، بنضيفها.
    /// </summary>
    public static (DateOnly Date, TimeOnly? Time, string Raw)? FindDate(string? dateText, string quote)
    {
        var fromText = ArabicDateExtractor.ExtractDates(dateText ?? "").FirstOrDefault();
        var inQuote = ArabicDateExtractor.ExtractDates(quote);
        var date = fromText ?? inQuote.FirstOrDefault();
        if (date is null)
        {
            return null;
        }
        var time = date.Time
                   ?? inQuote.FirstOrDefault(d => d.Date == date.Date && d.Time is not null)?.Time
                   ?? (ArabicDateExtractor.ExtractTimes(quote) is { Count: 1 } single ? single[0].Time : (TimeOnly?)null)
                   ?? (ArabicDateExtractor.ExtractTimes(dateText ?? "") is { Count: 1 } t2 ? t2[0].Time : (TimeOnly?)null);
        return (date.Date, time, date.RawText);
    }

    private static void ReadFacts(JsonElement root, string doc, List<ExtractedFact> into, List<string> reasons)
    {
        foreach (var f in Array(root, "facts"))
        {
            var quote = Str(f, "quote");
            var subject = Str(f, "subject");
            if (quote is null || subject is null || !CitationVerifier.QuoteAppearsIn(quote, doc))
            {
                reasons.Add($"واقعة «{subject}»: الاقتباس مش موجود حرفياً في المستند: «{Short(quote)}»");
                continue;
            }
            var timeText = Str(f, "time_text");
            var date = FindDate(timeText, quote);
            DateTime? time = date?.Time is { } t ? date.Value.Date.ToDateTime(t) : null;
            var amount = Decimal(f, "amount") is { } a && AmountAppearsIn(a, quote) ? a : (decimal?)null;
            if (time is null && amount is null)
            {
                reasons.Add($"واقعة «{subject}»: مفيش وقت (تاريخ وساعة) ولا مبلغ مكتوب في الاقتباس.");
                continue;
            }
            into.Add(new ExtractedFact(subject, EventKinds.Normalize(Str(f, "predicate")), time, date?.Raw, amount, quote));
        }
    }

    private static string Short(string? s) => s is null ? "" : s.Length <= 120 ? s : s[..120] + "…";

    [GeneratedRegex(@"\d[\d,٬.]*")]
    private static partial Regex Number();

    private static readonly string[] Currencies = ["جنيه", "جنيها", "ج.م", "ج م", "دولار", "ريال", "درهم", "يورو", "دينار", "EGP", "USD"];

    /// <summary>
    /// المبلغ لازم يكون مكتوب كرقم في الاقتباس (بعد شيل الفواصل)، والاقتباس لازم يذكر عملة،
    /// عشان الكميات (زي "400 كرتونة") ماتتحسبش مبالغ.
    /// </summary>
    public static bool AmountAppearsIn(decimal amount, string quote) =>
        Currencies.Any(c => ArabicNormalizer.NormalizeForSearch(quote).Contains(ArabicNormalizer.NormalizeForSearch(c), StringComparison.OrdinalIgnoreCase))
        && Number().Matches(ArabicNormalizer.NormalizeDigits(quote))
            .Select(m => m.Value.Replace(",", "").Replace("٬", "").TrimEnd('.'))
            .Any(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d == amount);

    /// <summary>تقسيم المستند لأجزاء عند حدود الفقرات.</summary>
    public static IEnumerable<string> Chunk(string text, int size = ChunkSize)
    {
        var current = new System.Text.StringBuilder();
        foreach (var para in text.Split('\n'))
        {
            if (current.Length > 0 && current.Length + para.Length > size)
            {
                yield return current.ToString();
                current.Clear();
            }
            // فقرة أطول من الحجم كله بتتقسم بالعافية.
            for (var p = para; p.Length > 0;)
            {
                var take = Math.Min(p.Length, size);
                current.Append(p.AsSpan(0, take)).Append('\n');
                p = p[take..];
                if (p.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
        }
        if (current.ToString().Trim().Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static bool TryParse(string? content, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }
        // بعض النماذج بتحط الـ JSON جوه ```json ... ```.
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }
        try
        {
            root = JsonDocument.Parse(content[start..(end + 1)]).RootElement;
            return root.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<JsonElement> Array(JsonElement root, string name) =>
        root.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
            : [];

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static decimal? Decimal(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return null;
        }
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
        {
            return d;
        }
        return v.ValueKind == JsonValueKind.String
               && decimal.TryParse(ArabicNormalizer.NormalizeDigits(v.GetString()!).Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out d)
            ? d
            : null;
    }
}
