namespace SmartLawyer.Infrastructure.Persistence.Entities;

/// <summary>أي بيانات خاصة بمكتب لازم تطبّق الـ interface ده عشان العزل يشتغل عليها.</summary>
public interface ITenantOwned
{
    Guid OrganizationId { get; set; }
}

public sealed class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string Plan { get; set; } = "free";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppUser : ITenantOwned
{
    public Guid Id { get; set; }                         // نفس الـ id بتاع Supabase Auth
    public Guid OrganizationId { get; set; }
    public required string Email { get; set; }
    public string Role { get; set; } = "lawyer";
}

public enum DocumentStatus { Uploaded = 0, Processing = 1, Processed = 2, Failed = 3 }

public sealed class LegalCase : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Number { get; set; }
    public required string Title { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<CaseDocument> Documents { get; set; } = [];
}

public sealed class CaseDocument : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CaseId { get; set; }
    public LegalCase Case { get; set; } = null!;
    public required string FileName { get; set; }
    public string? Kind { get; set; }
    public int Pages { get; set; }
    public DocumentStatus Status { get; set; }
    public string? ExtractedText { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>حدث في الخط الزمني للقضية، مستخرج من مستند.</summary>
public sealed class CaseEvent : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CaseId { get; set; }
    /// <summary>الوقت المحلي زي ما اتكتب في المستند (من غير منطقة زمنية). محسوب بالكود من RawDateText.</summary>
    public DateTime? OccurredAt { get; set; }

    /// <summary>true لو المستند ذكر الساعة، false لو التاريخ بس (يعني الحدث في أي وقت خلال اليوم).</summary>
    public bool HasTime { get; set; }

    public required string RawDateText { get; set; }     // التاريخ زي ما اتكتب في المستند
    public required string Kind { get; set; }            // signing / delivery / notice / incident / payment / other
    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>جملة منقولة حرفياً من المستند بتثبت الحدث.</summary>
    public required string Quote { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string? SourceLocation { get; set; }
}

/// <summary>ادعاء صغير مستخرج من مستند (مين، عمل إيه، إمتى، فين، كام) لكشف التناقضات.</summary>
public sealed class CaseClaim : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CaseId { get; set; }
    public required string Subject { get; set; }
    public required string Predicate { get; set; }
    public string? ObjectValue { get; set; }
    public DateTime? Time { get; set; }
    public string? RawTimeText { get; set; }
    public string? Place { get; set; }
    public decimal? Amount { get; set; }

    /// <summary>جملة منقولة حرفياً من المستند بتثبت الواقعة.</summary>
    public required string Quote { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string? SourceLocation { get; set; }
}

/// <summary>استهلاك يومي لكل مستخدم، لتطبيق حدود الاستخدام.</summary>
public sealed class UsageQuota : ITenantOwned
{
    public Guid UserId { get; set; }
    public DateOnly Day { get; set; }
    public Guid OrganizationId { get; set; }
    public int Questions { get; set; }
    public int Analyses { get; set; }
    public int DeepAnalyses { get; set; }
}

/// <summary>كل استدعاء لنموذج AI بيتسجل هنا عشان نتابع التكلفة.</summary>
public sealed class LlmUsage : ITenantOwned
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public required string Feature { get; set; }
    public required string Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public decimal CostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// ربط المستخدم (من Supabase Auth) بمكتبه. مفيش عليه Row-Level Security لأننا محتاجينه عشان نعرف المكتب أصلاً،
/// وفيه أرقام بس من غير أي بيانات حساسة.
/// </summary>
public sealed class Membership
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>إجابات بحث محفوظة لكل مكتب، عشان السؤال المتكرر مايتحسبش تاني على حد الاستخدام ولا على التكلفة.</summary>
public sealed class AnswerCacheEntry : ITenantOwned
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public required string QuestionHash { get; set; }
    public required string Question { get; set; }
    public required string AnswerJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
