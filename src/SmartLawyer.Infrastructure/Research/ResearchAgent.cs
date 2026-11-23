using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartLawyer.Core.Citations;
using SmartLawyer.Infrastructure.Llm;
using SmartLawyer.Infrastructure.Search;

namespace SmartLawyer.Infrastructure.Research;

public sealed record ResearchSource(string Key, string Title, string Text);

public sealed record ResearchAnswer(
    bool Answered,
    string Text,
    IReadOnlyList<ResearchSource> Sources,
    IReadOnlyList<string> Problems,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    string? Provider,
    string? Model);

public enum ResearchMode
{
    /// <summary>
    /// «ابحث وبعدين جاوب»: طلب صغير يحوّل السؤال لكلمات بحث، الكود يبحث بنفسه، وطلب واحد للإجابة.
    /// أقل في الـ tokens بكتير، ومناسب للمزوّدين المجانيين اللي ليهم حد في الدقيقة.
    /// </summary>
    Retrieve,

    /// <summary>النموذج بيدوّر بنفسه بالأدوات على كذا خطوة. أدق في الأسئلة المعقدة بس بياخد tokens أكتر.</summary>
    Agent,
}

public sealed class ResearchOptions
{
    public const string Section = "Research";
    public ResearchMode Mode { get; set; } = ResearchMode.Retrieve;
}

/// <summary>
/// وكيل البحث القانوني: النموذج بيدوّر في قاعدة بياناتنا بس (من خلال tools)، وبعدين يكتب الإجابة باستشهادات.
/// الإجابة مابتطلعش للمحامي غير لما الـ CitationVerifier يتأكد إن كل مصدر اتجاب فعلاً وإن الاقتباسات حرفية.
/// لو فشل التحقق، النموذج بياخد فرصة واحدة يصلّح، وبعدها النظام بيقول إنه مش لاقي سند كافي.
/// </summary>
public sealed class ResearchAgent(ILlmClient llm, LegalSearchService search, ResearchMode mode = ResearchMode.Agent)
{
    public const int MaxSteps = 8;
    public const int MaxRepairAttempts = 1;

    /// <summary>نتائج البحث بتعرض أول جزء من المادة عشان نوفر tokens؛ get_article بيجيب النص كامل.</summary>
    public const int SearchExcerptLength = 1200;

    public const string NoAnswerText =
        "مالقيتش سند كافي في قاعدة البيانات الحالية للإجابة على السؤال ده. جرّب تصيغ السؤال بشكل تاني أو تحدد القانون.";

    public const string SystemPrompt = """
        أنت باحث قانوني مساعد لمحامين مصريين. مهمتك تجاوب على السؤال من نصوص القوانين وأحكام المحاكم الموجودة في قاعدة البيانات فقط.

        طريقة الشغل:
        - استخدم الأدوات للبحث في التشريعات والأحكام قبل ما تجاوب. ابحث بكلمات قانونية مختلفة لو أول بحث مارجعش نتايج مفيدة.
        - ماتعتمدش على معلوماتك العامة عن القانون. لو مالقيتش نص يسند الإجابة، قول كده بوضوح.
        - كل جملة فيها حكم قانوني لازم يكون بعدها استشهاد بالمفتاح اللي ظهر في نتيجة الأداة بالظبط، زي:
          [[article:12:147]] أو [[ruling:1234:85]]
        - لو هتقتبس نص، حط الاقتباس بعد الاستشهاد مباشرة بين « »، ولازم يكون منقول حرفياً من نص المصدر.
        - ماتخترعش أرقام مواد أو أحكام. أي مصدر مش من نتائج الأدوات هيترفض.
        - اكتب الإجابة بالعربي الفصيح المبسط، في نقاط قصيرة، وفي الآخر نبّه إن الإجابة محتاجة مراجعة محامٍ.
        """;

    public static IReadOnlyList<LlmTool> Tools { get; } =
    [
        new("search_legislation",
            "بحث في نصوص مواد القوانين المصرية السارية في تاريخ معين. بيرجع المواد الأقرب للسؤال ومعاها مفتاح الاستشهاد.",
            Schema(new()
            {
                ["query"] = Prop("string", "كلمات البحث بالعربي"),
                ["as_of_date"] = Prop("string", "تاريخ الواقعة بالشكل yyyy-MM-dd (اختياري، الافتراضي النهارده)"),
            }, "query")),
        new("get_article",
            "جلب نص مادة معينة من قانون معين زي ما كان ساري في تاريخ معين.",
            Schema(new()
            {
                ["law_id"] = Prop("integer", "رقم القانون في قاعدة البيانات (من نتيجة بحث سابقة)"),
                ["article_number"] = Prop("string", "رقم المادة، زي 147 أو 18 مكرر (أ)"),
                ["as_of_date"] = Prop("string", "yyyy-MM-dd (اختياري)"),
            }, "law_id", "article_number")),
        new("search_cassation",
            "بحث في أحكام محكمة النقض والمبادئ القانونية.",
            Schema(new()
            {
                ["query"] = Prop("string", "كلمات البحث بالعربي"),
                ["year_from"] = Prop("integer", "أقل سنة قضائية (اختياري)"),
                ["year_to"] = Prop("integer", "أكبر سنة قضائية (اختياري)"),
            }, "query")),
        new("get_ruling",
            "جلب نص حكم نقض كامل برقم الطعن والسنة القضائية.",
            Schema(new()
            {
                ["appeal_number"] = Prop("integer", "رقم الطعن"),
                ["judicial_year"] = Prop("integer", "السنة القضائية"),
            }, "appeal_number", "judicial_year")),
    ];

    public const string KeywordsPrompt = """
        حوّل سؤال المحامي لكلمات بحث قانونية بالعربي الفصيح، زي المصطلحات اللي بتتكتب في نصوص القوانين المصرية
        (مثلاً: «كسر عربية جاري» ← «خطأ ضرر للغير تعويض مسئولية تقصيرية»).
        اكتب الكلمات بس في سطر واحد، من 4 لـ 12 كلمة، من غير أي شرح.
        """;

    public const string AnswerPrompt = """
        أنت باحث قانوني مساعد لمحامين مصريين. جاوب على السؤال من المصادر المرفقة فقط (نصوص قوانين وأحكام من قاعدة البيانات).
        - ماتعتمدش على معلوماتك العامة عن القانون. لو المصادر المرفقة مش كفاية، قول كده بوضوح.
        - كل جملة فيها حكم قانوني لازم يكون بعدها مفتاح المصدر بالظبط زي ما هو مكتوب في المصادر، زي [[article:12:147]] أو [[ruling:1234:85]].
        - لو هتقتبس، حط الاقتباس بين « » بعد المفتاح مباشرة، ولازم يكون منقول حرفياً من نص المصدر.
        - ماتخترعش أرقام مواد أو أحكام. أي مفتاح مش من المصادر المرفقة هيترفض.
        - اكتب بالعربي الفصيح المبسط، في نقاط قصيرة، وفي الآخر نبّه إن الإجابة محتاجة مراجعة محامٍ.
        """;

    public Task<ResearchAnswer> AskAsync(string question, CancellationToken ct = default) =>
        mode == ResearchMode.Retrieve ? AskRetrieveAsync(question, ct) : AskAgentAsync(question, ct);

    public async Task<ResearchAnswer> AskRetrieveAsync(string question, CancellationToken ct = default)
    {
        var usage = new Usage();

        // 1) كلمات بحث قانونية. سؤال المحامي ممكن يكون فيه تفاصيل قضية، فبيتعامل كبيانات قضايا.
        var kw = await llm.CompleteAsync(new LlmRequest(KeywordsPrompt, [LlmMessage.User(question)], ContainsCaseData: true, MaxTokens: 800), ct);
        usage.Add(kw);
        var keywords = string.IsNullOrWhiteSpace(kw.Content) ? question : kw.Content.Trim();

        // 2) البحث بالكود: بالكلمات القانونية وبنص السؤال نفسه.
        var fetched = new Dictionary<SourceKey, FetchedSource>();
        var byKeywords = await search.SearchArticlesAsync(keywords, null, 5, ct);
        var byQuestion = await search.SearchArticlesAsync(question, null, 3, ct);
        var articles = byKeywords.Concat(byQuestion)
            .GroupBy(h => (h.LawId, h.ArticleNumber)).Select(g => g.First()).Take(7).ToList();
        var rulings = await search.SearchRulingsAsync(keywords, null, null, 3, ct);
        if (articles.Count == 0 && rulings.Count == 0)
        {
            return usage.NoAnswer(["البحث مارجّعش أي مادة أو حكم."]);
        }
        var sources = FormatArticles(articles, fetched, SearchExcerptLength) + FormatRulings(rulings, fetched);

        // 3) طلب الإجابة + التحقق، وفرصة تصليح واحدة.
        var messages = new List<LlmMessage> { LlmMessage.User($"السؤال:\n{question}\n\nالمصادر المتاحة (استخدمها بس):\n{sources}") };
        IReadOnlyList<string> problems = [];
        for (var attempt = 0; attempt <= MaxRepairAttempts; attempt++)
        {
            var response = await llm.CompleteAsync(new LlmRequest(AnswerPrompt, messages, ContainsCaseData: true, MaxTokens: 3000), ct);
            usage.Add(response);
            var answer = response.Content?.Trim() ?? "";
            var verification = CitationVerifier.Verify(answer, fetched);
            if (verification.IsValid)
            {
                return usage.Answered(answer, verification, fetched);
            }
            problems = verification.Problems;
            messages.Add(new LlmMessage(LlmRole.Assistant, answer));
            messages.Add(LlmMessage.User(RepairMessage(problems)));
        }
        return usage.NoAnswer(problems);
    }

    private static string RepairMessage(IReadOnlyList<string> problems) =>
        "الإجابة دي اترفضت في التحقق من المصادر:\n- " + string.Join("\n- ", problems) +
        "\nصلّح الإجابة: استشهد بس بالمفاتيح اللي ظهرت في المصادر، والاقتباسات لازم تكون حرفية. " +
        "لو مفيش سند كافي، قول كده من غير استشهادات وهتتسجل إن مفيش إجابة.";

    private sealed class Usage
    {
        public int Input, Output;
        public decimal Cost;
        public string? Provider, Model;

        public void Add(LlmResponse r)
        {
            Input += r.InputTokens;
            Output += r.OutputTokens;
            Cost += r.CostUsd;
            (Provider, Model) = (r.Provider, r.Model);
        }

        public ResearchAnswer Answered(string answer, VerificationResult v, Dictionary<SourceKey, FetchedSource> fetched) => new(
            true, answer,
            v.Citations.Select(c => c.Key).Distinct().Select(k => new ResearchSource(k.ToString(), fetched[k].Title, fetched[k].Text)).ToList(),
            [], Input, Output, Cost, Provider, Model);

        public ResearchAnswer NoAnswer(IReadOnlyList<string> problems) =>
            new(false, NoAnswerText, [], problems, Input, Output, Cost, Provider, Model);
    }

    public async Task<ResearchAnswer> AskAgentAsync(string question, CancellationToken ct = default)
    {
        var messages = new List<LlmMessage> { LlmMessage.User(question) };
        var fetched = new Dictionary<SourceKey, FetchedSource>();
        int input = 0, output = 0, repairs = 0;
        decimal cost = 0;
        string? provider = null, model = null;
        IReadOnlyList<string> problems = [];

        for (var step = 0; step < MaxSteps; step++)
        {
            // نص السؤال ممكن يكون فيه تفاصيل من قضية، فبنعامله على إنه بيانات قضايا احتياطياً.
            var response = await llm.CompleteAsync(new LlmRequest(SystemPrompt, messages, ContainsCaseData: true, Tools), ct);
            input += response.InputTokens;
            output += response.OutputTokens;
            cost += response.CostUsd;
            (provider, model) = (response.Provider, response.Model);

            if (response.ToolCalls.Count > 0)
            {
                messages.Add(new LlmMessage(LlmRole.Assistant, response.Content, response.ToolCalls));
                foreach (var call in response.ToolCalls)
                {
                    var result = await RunToolAsync(call, fetched, ct);
                    messages.Add(LlmMessage.ToolResult(call.Id, result));
                }
                continue;
            }

            var answer = response.Content?.Trim() ?? "";
            var verification = CitationVerifier.Verify(answer, fetched);
            if (verification.IsValid)
            {
                var used = verification.Citations.Select(c => c.Key).Distinct()
                    .Select(k => new ResearchSource(k.ToString(), fetched[k].Title, fetched[k].Text)).ToList();
                return new ResearchAnswer(true, answer, used, [], input, output, cost, provider, model);
            }

            problems = verification.Problems;
            if (repairs++ >= MaxRepairAttempts)
            {
                break;
            }
            messages.Add(new LlmMessage(LlmRole.Assistant, answer));
            messages.Add(LlmMessage.User(RepairMessage(problems)));
        }

        return new ResearchAnswer(false, NoAnswerText, [], problems, input, output, cost, provider, model);
    }

    private async Task<string> RunToolAsync(LlmToolCall call, Dictionary<SourceKey, FetchedSource> fetched, CancellationToken ct)
    {
        JsonElement args;
        try
        {
            args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement;
        }
        catch (JsonException)
        {
            return "خطأ: مدخلات الأداة مش JSON صالح.";
        }

        try
        {
            switch (call.Name)
            {
                case "search_legislation":
                {
                    var hits = await search.SearchArticlesAsync(RequiredString(args, "query"), OptionalDate(args, "as_of_date"), 5, ct);
                    return FormatArticles(hits, fetched, SearchExcerptLength);
                }
                case "get_article":
                {
                    var hit = await search.GetArticleAsync(
                        RequiredInt(args, "law_id"), RequiredString(args, "article_number"), OptionalDate(args, "as_of_date"), ct);
                    return hit is null ? "المادة مش موجودة أو ماكانتش سارية في التاريخ ده." : FormatArticles([hit], fetched);
                }
                case "search_cassation":
                {
                    var hits = await search.SearchRulingsAsync(
                        RequiredString(args, "query"), OptionalInt(args, "year_from"), OptionalInt(args, "year_to"), 5, ct);
                    return FormatRulings(hits, fetched);
                }
                case "get_ruling":
                {
                    var hit = await search.GetRulingAsync(RequiredInt(args, "appeal_number"), RequiredInt(args, "judicial_year"), ct: ct);
                    return hit is null ? "الحكم مش موجود في قاعدة البيانات." : FormatRulings([hit], fetched);
                }
                default:
                    return $"خطأ: مفيش أداة اسمها {call.Name}.";
            }
        }
        catch (ArgumentException ex)
        {
            return $"خطأ في مدخلات الأداة: {ex.Message}";
        }
    }

    private static string FormatArticles(
        IReadOnlyList<ArticleHit> hits, Dictionary<SourceKey, FetchedSource> fetched, int maxLength = int.MaxValue)
    {
        if (hits.Count == 0)
        {
            return "مفيش نتايج. جرّب كلمات تانية.";
        }
        var sb = new StringBuilder();
        foreach (var h in hits)
        {
            var key = SourceKey.Article(h.LawId, h.ArticleNumber);
            var title = $"{h.LawTitle} — مادة {h.ArticleNumber}";
            var shown = h.Text.Length <= maxLength ? h.Text : h.Text[..maxLength] + " … (النص مقطوع؛ استخدم get_article للنص الكامل)";
            // المصدر المسجّل للتحقق هو اللي النموذج شافه بس، عشان مايقتبسش من جزء ماشافوش.
            if (!fetched.TryGetValue(key, out var existing) || existing.Text.Length < shown.Length)
            {
                fetched[key] = new FetchedSource(key, title, h.Text.Length <= maxLength ? h.Text : h.Text[..maxLength]);
            }
            var validity = h.ValidTo is null
                ? $"سارية من {h.ValidFrom:yyyy-MM-dd}"
                : $"كانت سارية من {h.ValidFrom:yyyy-MM-dd} لحد {h.ValidTo:yyyy-MM-dd}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"{key} {title} ({validity}) [law_id={h.LawId}]");
            sb.AppendLine(shown).AppendLine();
        }
        return sb.ToString();
    }

    private static string FormatRulings(IReadOnlyList<RulingHit> hits, Dictionary<SourceKey, FetchedSource> fetched)
    {
        if (hits.Count == 0)
        {
            return "مفيش أحكام مطابقة. جرّب كلمات تانية.";
        }
        var sb = new StringBuilder();
        foreach (var h in hits)
        {
            var key = SourceKey.Ruling(h.AppealNumber, h.JudicialYear);
            var title = $"الطعن رقم {h.AppealNumber} لسنة {h.JudicialYear} ق" + (h.SessionDate is { } d ? $" — جلسة {d:yyyy-MM-dd}" : "");
            var text = (h.Principle is null ? "" : $"المبدأ: {h.Principle}\n") + h.Excerpt;
            fetched[key] = new FetchedSource(key, title, text);
            sb.AppendLine(CultureInfo.InvariantCulture, $"{key} {title}{(h.Chamber is null ? "" : $" — الدائرة: {h.Chamber}")}");
            sb.AppendLine(text).AppendLine();
        }
        return sb.ToString();
    }

    private static JsonObject Schema(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray()),
        ["additionalProperties"] = false,
    };

    private static JsonObject Prop(string type, string description) =>
        new() { ["type"] = type, ["description"] = description };

    private static string RequiredString(JsonElement args, string name) =>
        args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!
            : throw new ArgumentException($"{name} مطلوب");

    private static int RequiredInt(JsonElement args, string name) =>
        OptionalInt(args, name) ?? throw new ArgumentException($"{name} مطلوب ولازم يكون رقم");

    private static int? OptionalInt(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var v))
        {
            return null;
        }
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
        {
            return n;
        }
        // بعض النماذج بتبعت الأرقام كنص.
        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
            ? n
            : null;
    }

    private static DateOnly? OptionalDate(JsonElement args, string name) =>
        args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(v.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
}
