namespace SmartLawyer.Infrastructure.Llm;

public sealed class LlmOptions
{
    public const string Section = "Llm";

    /// <summary>المزوّدين بالترتيب: الأول هو المفضّل، والباقي احتياطي لو حصل خطأ أو الحد خلص.</summary>
    public List<LlmProviderOptions> Providers { get; set; } = [];
}

public sealed class LlmProviderOptions
{
    public required string Name { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>عنوان API متوافق مع OpenAI chat completions (Groq وOllama وGemini بيدعموه).</summary>
    public required string BaseUrl { get; set; }

    public required string Model { get; set; }

    /// <summary>اسم متغير البيئة اللي فيه المفتاح (مش المفتاح نفسه). فاضي لـ Ollama.</summary>
    public string? ApiKeyEnvVar { get; set; }

    /// <summary>
    /// مسموح نبعتله بيانات قضايا؟ لازم يفضل false لأي مزوّد شروطه بتسمح باستخدام البيانات في التدريب،
    /// ومايتقلبش true غير بعد مراجعة الشروط وتفعيل عدم الاحتفاظ بالبيانات.
    /// </summary>
    public bool AllowsCaseData { get; set; }

    public decimal InputCostPerMillionTokens { get; set; }
    public decimal OutputCostPerMillionTokens { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
}
