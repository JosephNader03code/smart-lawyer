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
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>حدث في الخط الزمني للقضية، مستخرج من مستند.</summary>
public sealed class CaseEvent : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CaseId { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public required string RawDateText { get; set; }     // التاريخ زي ما اتكتب في المستند
    public required string Title { get; set; }
    public string? Description { get; set; }
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
    public DateTimeOffset? Time { get; set; }
    public string? Place { get; set; }
    public decimal? Amount { get; set; }
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
